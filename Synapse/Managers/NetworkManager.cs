using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SiraUtil.Logging;
using Synapse.Extras;
using Synapse.Models;
using Unclassified.Net;

namespace Synapse.Managers;

internal enum Stage
{
    Connecting,
    Authenticating,
    ReceivingData,
    Timeout,
    Refused,
    Failed
}

internal enum ClosedReason
{
    ClosedLocally,
    Aborted,
    ResetRemotely,
    ClosedRemotely
}

internal class NetworkManager : IDisposable
{
    private readonly Config _config;
    private readonly ListingManager _listingManager;
    private readonly SiraLog _log;
    private readonly IPlatformUserModel _platformUserModel;

    private readonly List<ArraySegment<byte>> _queuedPackets = [];
    private readonly Task<AuthenticationToken> _tokenTask;
    private string _address = string.Empty;

    private AsyncTcpClient? _client;

    private int _dequeueAmount;

    [UsedImplicitly]
    private NetworkManager(
        SiraLog log,
        Config config,
        IPlatformUserModel platformUserModel,
        ListingManager listingManager)
    {
        _log = log;
        _config = config;
        _platformUserModel = platformUserModel;
        _listingManager = listingManager;
        _tokenTask = GetToken();
    }

    ////internal event Action<FailReason>? ConnectionFailed;

    internal event Action<ClosedReason>? Closed;

    internal event Action<Stage, int>? Connecting;

    internal event Action<string>? Disconnected;

    internal event Action<ChatMessage>? ChatReceived;

    internal event Action<LeaderboardScores>? LeaderboardReceived;

    internal event Action<int, Map?>? MapUpdated;

    internal event Action<string>? MotdUpdated;

    internal event Action<PlayerScore?>? PlayerScoreUpdated;

    internal event Action<float?>? StartTimeUpdated;

    internal event Action? StopLevelReceived;

    internal event Action<string>? UserBanned;

    internal event Action<float, float>? PongReceived;

    internal Status Status { get; private set; } = new();

    public void Dispose()
    {
        _ = Disconnect("Disposed");
    }

    internal async Task Disconnect(string reason, bool local = true)
    {
        if (_client == null)
        {
            return;
        }

        _log.Warn($"Disconnected from {_address} ({reason})");
        if (local && _client.IsConnected)
        {
            await Send(ServerOpcode.Disconnect);
        }

        _client.Disconnect();

        Disconnected?.Invoke(reason);
    }

    internal async Task RunAsync()
    {
        if (_client != null)
        {
            _log.Error("Client still running, disconnecting");
            await Disconnect("Joining from another client");
        }

        string? stringAddress = _listingManager.Listing?.IpAddress;
        if (stringAddress == null)
        {
            _log.Error("No IP found");
            return;
        }

        Status = new Status();
        AsyncTcpClient client = new();
        int portIdx = stringAddress.LastIndexOf(':');
        IPAddress address = IPAddress.Parse(stringAddress.Substring(0, portIdx));
        int port = int.Parse(stringAddress.Substring(portIdx + 1));
        _address = $"{address}:{port}";
        _log.Info($"Connecting to {_address}");
        client.IPAddress = address;
        client.Port = port;
        client.AutoReconnect = true;
        client.AutoReconnectTries = 2;
        client.Message += OnMessageReceived;
        client.ConnectedCallback += OnConnected;
        client.ReceivedCallback += OnReceived;
        _client = client;
        await client.RunAsync();
        client.Message -= OnMessageReceived;
        client.ConnectedCallback -= OnConnected;
        client.ReceivedCallback -= OnReceived;
        client.Dispose();
        _client = null;
    }

    internal async Task Send(ArraySegment<byte> data)
    {
        if (_client is not { IsConnected: true })
        {
            _log.Error("Client not connected! Delaying sending packet");
            _log.Error(new StackTrace());
            _queuedPackets.Add(data);
            return;
        }

        await _client.Send(data);
    }

    internal async Task Send(ServerOpcode opcode)
    {
        using PacketBuilder packetBuilder = new(opcode);
        await Send(packetBuilder.ToSegment());
    }

    internal async Task Send(ServerOpcode opcode, bool value)
    {
        using PacketBuilder packetBuilder = new(opcode);
        packetBuilder.Write(value);
        await Send(packetBuilder.ToSegment());
    }

    internal async Task Send(ServerOpcode opcode, int value)
    {
        using PacketBuilder packetBuilder = new(opcode);
        packetBuilder.Write(value);
        await Send(packetBuilder.ToSegment());
    }

    internal async Task Send(ServerOpcode opcode, string value)
    {
        using PacketBuilder packetBuilder = new(opcode);
        packetBuilder.Write(value);
        await Send(packetBuilder.ToSegment());
    }

    private async Task<AuthenticationToken> GetToken()
    {
#if !V1_29_1
        UserInfo userInfo = await _platformUserModel.GetUserInfo(CancellationToken.None);
#else
        UserInfo userInfo = await _platformUserModel.GetUserInfo();
#endif
        if (userInfo == null)
        {
            throw new InvalidOperationException("No authentication token provider could be created");
        }

        PlatformAuthenticationTokenProvider provider = new(_platformUserModel, userInfo);
        return await provider.GetAuthenticationToken();
    }

    private async Task OnConnected(AsyncTcpClient client, bool isReconnected)
    {
        try
        {
            _log.Debug(
                isReconnected
                    ? $"Successfully reconnected to {_address}"
                    : $"Successfully connected to {_address}");
            Connecting?.Invoke(Stage.Authenticating, -1);

            AuthenticationToken token = await _tokenTask;
            using PacketBuilder packetBuilder = new(ServerOpcode.Authentication);
            packetBuilder.Write(token.userId);
            packetBuilder.Write(token.userName);
            packetBuilder.Write((byte)token.platform);
            packetBuilder.Write(token.sessionToken);
            packetBuilder.Write(_listingManager.Listing?.Guid ?? throw new InvalidOperationException("No listing loaded"));
            await Send(packetBuilder.ToSegment());

            ArraySegment<byte>[] queued = _queuedPackets.ToArray();
            _queuedPackets.Clear();
            foreach (ArraySegment<byte> packet in queued)
            {
                await client.Send(packet);
            }
        }
        catch (Exception e)
        {
            _log.Error($"Exception while connecting\n{e}");
            await Disconnect("Exception while connecting");
        }
    }

    private void OnMessageReceived(object _, AsyncTcpEventArgs args)
    {
        if (args.Exception != null)
        {
            _log.Error(args.Message);
            _log.Error(args.Exception);
        }
        else
        {
            _log.Debug(args.Message);
        }

        switch (args.Message)
        {
            case Message.FailedAfterRetries:
                Connecting?.Invoke(Stage.Failed, args.ReconnectTries);
                break;

            case Message.Connecting:
                Connecting?.Invoke(Stage.Connecting, args.ReconnectTries);
                break;

            case Message.Timeout:
                Connecting?.Invoke(Stage.Timeout, args.ReconnectTries);
                break;

            case Message.ConnectionAborted:
                Closed?.Invoke(ClosedReason.Aborted);
                break;

            case Message.ConnectionFailed:
                SocketError error = ((SocketException?)args.Exception)?.SocketErrorCode ??
                                    throw new InvalidOperationException();
                switch (error)
                {
                    case SocketError.ConnectionRefused:
                        Connecting?.Invoke(Stage.Refused, args.ReconnectTries);
                        break;

                    default:
                        _log.Warn($"Unhandled socket error: {error}");
                        Connecting?.Invoke((Stage)error, args.ReconnectTries);
                        break;
                }

                break;

            case Message.ConnectionClosedLocally:
                Closed?.Invoke(ClosedReason.ClosedLocally);
                break;

            case Message.ConnectionClosedRemotely:
                Closed?.Invoke(ClosedReason.ClosedRemotely);
                break;

            case Message.ConnectionResetRemotely:
                Closed?.Invoke(ClosedReason.ResetRemotely);
                break;
        }
    }

    private async Task OnReceived(AsyncTcpClient client, int count)
    {
        try
        {
            while (true)
            {
                if (_dequeueAmount > 0)
                {
                    if (client.ByteBuffer.Count < _dequeueAmount)
                    {
                        return;
                    }

                    await ProcessPacket(client, await client.ByteBuffer.DequeueAsync(_dequeueAmount));
                    _dequeueAmount = 0;
                    continue;
                }

                byte[] lengthBytes = client.ByteBuffer.Peek(2);
                if (lengthBytes.Length != 2)
                {
                    break;
                }

                int length = BitConverter.ToUInt16(lengthBytes, 0);
                if (length <= count)
                {
                    await ProcessPacket(client, await client.ByteBuffer.DequeueAsync(length));
                }
                else
                {
                    _dequeueAmount = length;
                    return;
                }
            }
        }
        catch (Exception e)
        {
            _log.Error($"Received invalid packet\n{e}");
            client.ByteBuffer.Clear();
            _dequeueAmount = 0;
        }
    }

    private async Task ProcessPacket(AsyncTcpClient client, byte[] bytes)
    {
        using MemoryStream stream = new(bytes);
        using BinaryReader reader = new(stream);
        reader.ReadUInt16();
        ClientOpcode opcode = (ClientOpcode)reader.ReadByte();
        switch (opcode)
        {
            case ClientOpcode.Authenticated:
            {
                _log.Debug($"Authenticated {_address}");
                Connecting?.Invoke(Stage.ReceivingData, -1);
                if (_config.JoinChat ?? false)
                {
                    _ = Send(ServerOpcode.SetChatter, true);
                }

                break;
            }

            case ClientOpcode.Disconnected:
            {
                string message = reader.ReadString();
                await Disconnect(message, false);

                break;
            }

            case ClientOpcode.RefusedPacket:
            {
                string refusal = reader.ReadString();
                _log.Warn($"Packet refused by server ({refusal})");

                break;
            }

            case ClientOpcode.Ping:
                float clientTime = reader.ReadSingle();
                float serverTime = reader.ReadSingle();
                PongReceived?.Invoke(clientTime, serverTime);
                break;

            case ClientOpcode.PlayStatus:
            {
                string fullStatus = reader.ReadString();
                Status status = JsonConvert.DeserializeObject<Status>(fullStatus, JsonSettings.Settings)!;
                Status lastStatus = Status;
                Status = status;
                _log.Info(fullStatus);

                if (lastStatus.Motd != status.Motd)
                {
                    MotdUpdated?.Invoke(status.Motd);
                }

                if (lastStatus.Index != status.Index)
                {
                    MapUpdated?.Invoke(status.Index, status.Map);
                }

                // i hate floats
                if ((lastStatus.StartTime == null && status.StartTime != null) ||
                    (lastStatus.StartTime != null && status.StartTime == null) ||
                    (lastStatus.StartTime != null && status.StartTime != null &&
                     Math.Abs(lastStatus.StartTime.Value - status.StartTime.Value) > 0.001))
                {
                    StartTimeUpdated?.Invoke(status.StartTime);
                }

                if (lastStatus.PlayerScore != status.PlayerScore)
                {
                    PlayerScoreUpdated?.Invoke(status.PlayerScore);
                }

                break;
            }

            case ClientOpcode.ChatMessage:
            {
                string message = reader.ReadString();
                ChatReceived?.Invoke(JsonConvert.DeserializeObject<ChatMessage>(message, JsonSettings.Settings));

                break;
            }

            case ClientOpcode.UserBanned:
            {
                string message = reader.ReadString();
                UserBanned?.Invoke(message);

                break;
            }

            case ClientOpcode.LeaderboardScores:
            {
                string message = reader.ReadString();
                LeaderboardReceived?.Invoke(
                    JsonConvert.DeserializeObject<LeaderboardScores>(message, JsonSettings.Settings)!);

                break;
            }

            case ClientOpcode.StopLevel:
                StopLevelReceived?.Invoke();
                break;

            default:
                _log.Warn($"Unhandled opcode: ({opcode})");
                client.ByteBuffer.Clear();
                return;
        }
    }
}
