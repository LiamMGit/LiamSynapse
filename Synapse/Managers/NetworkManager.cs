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

namespace Synapse.Managers
{
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
        private readonly SiraLog _log;
        private readonly Config _config;
        private readonly IPlatformUserModel _platformUserModel;
        private readonly ListingManager _listingManager;
        private readonly Task<AuthenticationToken> _tokenTask;

        private readonly List<ArraySegment<byte>> _queuedPackets = new();

        private AsyncTcpClient? _client;
        private string _address = string.Empty;

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

        internal event Action<Stage, int>? Connecting;

        ////internal event Action<FailReason>? ConnectionFailed;

        internal event Action<ClosedReason>? Closed;

        internal event Action<string>? Disconnected;

        internal event Action<ChatMessage>? ChatRecieved;

        internal event Action<string>? MotdUpdated;

        internal event Action<DateTime?>? StartTimeUpdated;

        internal event Action<PlayerScore?>? PlayerScoreUpdated;

        internal event Action<int, Map>? MapUpdated;

        internal event Action<string>? UserBanned;

        internal event Action<LeaderboardScores>? LeaderboardReceived;

        internal Status Status { get; private set; } = new();

        public void Dispose()
        {
            _ = Disconnect("Disposed");
        }

        internal async Task RunAsync()
        {
            if (_client != null)
            {
                _log.Error("Client still running");
                return;
            }

            string? stringAddress = _listingManager.Listing?.IpAddress;
            if (stringAddress == null)
            {
                _log.Error("No IP found");
                return;
            }

            Status = new Status();
            AsyncTcpClient client = new();
            int portidx = stringAddress.LastIndexOf(':');
            IPAddress address = IPAddress.Parse(stringAddress.Substring(0, portidx));
            int port = int.Parse(stringAddress.Substring(portidx + 1));
            _address = $"{address}:{port}";
            _log.Info($"Connecting to {_address}");
            client.IPAddress = address;
            client.Port = port;
            client.AutoReconnect = true;
            client.AutoReconnectTries = 2;
            client.Message += OnMessageRecieved;
            client.ConnectedCallback += OnConnected;
            client.ReceivedCallback += OnReceived;
            _client = client;
            await client.RunAsync();
            client.Message -= OnMessageRecieved;
            client.ConnectedCallback -= OnConnected;
            client.ReceivedCallback -= OnReceived;
            client.Dispose();
            _client = null;
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
                await _client.Send(new ArraySegment<byte>(new[] { (byte)ServerOpcode.Disconnect }, 0, 1));
            }

            _client.AutoReconnect = false;
            _client.Disconnect();

            Disconnected?.Invoke(reason);
        }

        internal async Task Send(byte[] bytes)
        {
            ArraySegment<byte> packet = new(bytes, 0, bytes.Length);
            if (_client is not { IsConnected: true })
            {
                _log.Error("Client not connected! Delaying sending packet");
                _log.Error(new StackTrace());
                _queuedPackets.Add(packet);
                return;
            }

            await _client.Send(packet);
        }

        internal async Task SendBool(bool message, ServerOpcode opcode)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write((byte)opcode);
            writer.Write(message);
            await Send(stream.ToArray());
        }

        internal async Task SendInt(int message, ServerOpcode opcode)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write((byte)opcode);
            writer.Write(message);
            await Send(stream.ToArray());
        }

        internal async Task SendString(string message, ServerOpcode opcode)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write((byte)opcode);
            writer.Write(message);
            await Send(stream.ToArray());
        }

        private async Task<AuthenticationToken> GetToken()
        {
            UserInfo userInfo = await _platformUserModel.GetUserInfo();
            if (userInfo == null)
            {
                throw new InvalidOperationException("No authentication token provider could be created");
            }

            PlatformAuthenticationTokenProvider provider = new(_platformUserModel, userInfo);
            return await provider.GetAuthenticationToken();
        }

        private void OnMessageRecieved(object _, AsyncTcpEventArgs args)
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
                    SocketError error = ((SocketException?)args.Exception)?.SocketErrorCode ?? throw new InvalidOperationException();
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

        private async Task OnConnected(AsyncTcpClient client, bool isReconnected)
        {
            try
            {
                _log.Debug(isReconnected
                    ? $"Successfully reconnected to {_address}"
                    : $"Successfully connected to {_address}");
                Connecting?.Invoke(Stage.Authenticating, -1);

                using MemoryStream stream = new();
                using BinaryWriter writer = new(stream);
                AuthenticationToken token = await _tokenTask;
                writer.Write((byte)ServerOpcode.Authentication);
                writer.Write(token.userId);
                writer.Write(token.userName);
                writer.Write((byte)token.platform);
                writer.Write(token.sessionToken);
                writer.Write(_listingManager.Listing?.Guid ?? throw new InvalidOperationException("No listing loaded"));
                byte[] bytes = stream.ToArray();
                await client.Send(new ArraySegment<byte>(bytes, 0, bytes.Length));

                ArraySegment<byte>[] queued = _queuedPackets.ToArray();
                _queuedPackets.Clear();
                foreach (ArraySegment<byte> packet in queued)
                {
                    await client.Send(packet);
                }
            }
            catch (Exception e)
            {
                _log.Error("Exception while connecting");
                _log.Error(e);
                await Disconnect("Exception while connecting");
            }
        }

        private async Task OnReceived(AsyncTcpClient client, int count)
        {
            try
            {
                using MemoryStream stream = new(await client.ByteBuffer.DequeueAsync(count));
                using BinaryReader reader = new(stream);
                int opcodeByte;
                while ((opcodeByte = stream.ReadByte()) != -1)
                {
                    ClientOpcode opcode = (ClientOpcode)opcodeByte;
                    switch (opcode)
                    {
                        case ClientOpcode.Authenticated:
                            {
                                _log.Debug($"Authenticated {_address}");
                                Connecting?.Invoke(Stage.ReceivingData, -1);
                                if (_config.JoinChat ?? false)
                                {
                                    _ = SendBool(true, ServerOpcode.SetChatter);
                                }
                            }

                            break;

                        case ClientOpcode.Disconnected:
                            {
                                string message = reader.ReadString();
                                await Disconnect(message, false);
                            }

                            break;

                        case ClientOpcode.RefusedPacket:
                            {
                                string refusal = reader.ReadString();
                                _log.Warn($"Packet refused by server ({refusal})");
                            }

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

                                if (lastStatus.StartTime != status.StartTime)
                                {
                                    StartTimeUpdated?.Invoke(status.StartTime);
                                }

                                if (lastStatus.PlayerScore != status.PlayerScore)
                                {
                                    PlayerScoreUpdated?.Invoke(status.PlayerScore);
                                }
                            }

                            break;

                        case ClientOpcode.ChatMessage:
                            {
                                string message = reader.ReadString();
                                _log.Debug(message);
                                ChatRecieved?.Invoke(JsonConvert.DeserializeObject<ChatMessage>(message, JsonSettings.Settings));
                            }

                            break;

                        case ClientOpcode.UserBanned:
                            {
                                string message = reader.ReadString();
                                UserBanned?.Invoke(message);
                            }

                            break;

                        case ClientOpcode.LeaderboardScores:
                            {
                                string message = reader.ReadString();
                                LeaderboardReceived?.Invoke(JsonConvert.DeserializeObject<LeaderboardScores>(message, JsonSettings.Settings)!);
                            }

                            break;

                        default:
                            _log.Warn($"Unhandled opcode: ({opcode})");
                            client.ByteBuffer.Clear();
                            return;
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error("Received invalid packet");
                _log.Error(e);
                client.ByteBuffer.Clear();
            }
        }
    }
}
