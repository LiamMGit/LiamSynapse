using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SiraUtil.Logging;
using Synapse.Extras;
using Synapse.Models;
using Unclassified.Net;
#if !V1_29_1
using System.Threading;
#endif

namespace Synapse.Managers;

internal enum ConnectingStage
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

    internal event Action<ChatMessage>? ChatReceived;

    ////internal event Action<FailReason>? ConnectionFailed;

    internal event Action<ClosedReason>? Closed;

    internal event Action<ConnectingStage, int>? Connecting;

    internal event Action<string>? Disconnected;

    internal event Action<string>? FinishUrlUpdated;

    internal event Action<float>? IntroStartTimeUpdated;

    internal event Action<LeaderboardScores>? LeaderboardReceived;

    internal event Action<int, Map>? MapUpdated;

    internal event Action<string>? MotdUpdated;

    internal event Action<PlayerScore?>? PlayerScoreUpdated;

    internal event Action<float, float>? PongReceived;

    internal event Action<IStageStatus>? StageUpdated;

    internal event Action<float>? StartTimeUpdated;

    internal event Action? StopLevelReceived;

    internal event Action<string>? UserBanned;

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
        try
        {
            await client.RunAsync();
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception e)
        {
            _log.Critical($"An unexpected exception has occurred: {e}");
            await Disconnect("An unexpected error has occured");
        }

        client.Message -= OnMessageReceived;
        client.ConnectedCallback -= OnConnected;
        client.ReceivedCallback -= OnReceived;
        client.Dispose();
        if (_client == client)
        {
            _client = null;
        }
    }

    internal async Task Send(ArraySegment<byte> data, CancellationToken cancellationToken = default)
    {
        if (_client is not { IsConnected: true })
        {
            _log.Error("Client not connected! Delaying sending packet");
            _log.Error(new StackTrace());
            _queuedPackets.Add(data);
            return;
        }

        await _client.Send(data, cancellationToken);
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

    private async Task OnConnected(AsyncTcpClient client, bool isReconnected, CancellationToken cancelToken)
    {
        try
        {
            _log.Debug(
                isReconnected
                    ? $"Successfully reconnected to {_address}"
                    : $"Successfully connected to {_address}");
            Connecting?.Invoke(ConnectingStage.Authenticating, -1);

            AuthenticationToken token = await _tokenTask;
            using PacketBuilder packetBuilder = new(ServerOpcode.Authentication);
            packetBuilder.Write(token.userId);
            packetBuilder.Write(token.userName);
            packetBuilder.Write((byte)token.platform);
            packetBuilder.Write(token.sessionToken);
            packetBuilder.Write(
                _listingManager.Listing?.Guid ?? throw new InvalidOperationException("No listing loaded"));
            await Send(packetBuilder.ToSegment(), cancelToken);

            ArraySegment<byte>[] queued = _queuedPackets.ToArray();
            _queuedPackets.Clear();
            foreach (ArraySegment<byte> packet in queued)
            {
                await client.Send(packet, cancelToken);
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
                Connecting?.Invoke(ConnectingStage.Failed, args.ReconnectTries);
                break;

            case Message.Connecting:
                Connecting?.Invoke(ConnectingStage.Connecting, args.ReconnectTries);
                break;

            case Message.Timeout:
                Connecting?.Invoke(ConnectingStage.Timeout, args.ReconnectTries);
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
                        Connecting?.Invoke(ConnectingStage.Refused, args.ReconnectTries);
                        break;

                    default:
                        _log.Warn($"Unhandled socket error: {error}");
                        Connecting?.Invoke((ConnectingStage)error, args.ReconnectTries);
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

    private async Task OnReceived(AsyncTcpClient client, int count, CancellationToken cancelToken)
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

                    _ = ProcessPacket(client, await client.ByteBuffer.DequeueAsync(_dequeueAmount, cancelToken));
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
                    _ = ProcessPacket(client, await client.ByteBuffer.DequeueAsync(length, cancelToken));
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
                Connecting?.Invoke(ConnectingStage.ReceivingData, -1);
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

            case ClientOpcode.Status:
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

                if (status.Stage.GetType() != lastStatus.Stage.GetType())
                {
                    StageUpdated?.Invoke(status.Stage);
                }

                switch (status.Stage)
                {
                    case IntroStatus introStatus:
                        if (lastStatus.Stage is not IntroStatus lastIntroStatus)
                        {
                            lastIntroStatus = new IntroStatus();
                        }

                        if (Math.Abs(lastIntroStatus.StartTime - introStatus.StartTime) > 0.001)
                        {
                            IntroStartTimeUpdated?.Invoke(introStatus.StartTime);
                        }

                        break;

                    case PlayStatus playStatus:
                        if (lastStatus.Stage is not PlayStatus lastPlayStatus)
                        {
                            lastPlayStatus = new PlayStatus();
                        }

                        if (lastPlayStatus.Index != playStatus.Index)
                        {
                            MapUpdated?.Invoke(playStatus.Index, playStatus.Map);
                        }

                        // i hate floats
                        if (Math.Abs(lastPlayStatus.StartTime - playStatus.StartTime) > 0.001)
                        {
                            StartTimeUpdated?.Invoke(playStatus.StartTime);
                        }

                        if (lastPlayStatus.PlayerScore != playStatus.PlayerScore)
                        {
                            PlayerScoreUpdated?.Invoke(playStatus.PlayerScore);
                        }

                        break;

                    case FinishStatus finishStatus:
                        if (lastStatus.Stage is not FinishStatus lastFinishStatus)
                        {
                            lastFinishStatus = new FinishStatus();
                        }

                        if (lastFinishStatus.Url != finishStatus.Url)
                        {
                            FinishUrlUpdated?.Invoke(finishStatus.Url);
                        }

                        break;
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
