using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using IPA.Logging;
using IPA.Utilities.Async;
using ModestTree;
using Newtonsoft.Json;
using SiraUtil.Logging;
using SRT.Models;
using Unclassified.Net;
using Unclassified.Util;
using Zenject;

namespace SRT.Managers
{
    public enum Stage
    {
        Connecting,
        Authenticating,
        ReceivingData,
        Timeout,
        Refused,
        Failed
    }

    public enum ClosedReason
    {
        ClosedLocally,
        Aborted,
        ResetRemotely,
        ClosedRemotely
    }

    public enum ClientOpcode
    {
        Authenticated = 0,
        Disconnected = 1,
        RefusedPacket = 2,
        PlayStatus = 4,
        ChatMessage = 10,
        UserBanned = 11
    }

    public enum ServerOpcode
    {
        Authentication = 0,
        Disconnect = 1,
        ChatMessage = 10
    }

    internal class NetworkManager : IDisposable
    {
        private static readonly IPAddress _address = IPAddress.IPv6Loopback;

        private readonly SiraLog _log;
        private readonly IPlatformUserModel _platformUserModel;
        private readonly ListingManager _listingManager;
        private readonly Task<AuthenticationToken> _tokenTask;

        private AsyncTcpClient? _client;

        private NetworkManager(SiraLog log, IPlatformUserModel platformUserModel, ListingManager listingManager)
        {
            _log = log;
            _platformUserModel = platformUserModel;
            _listingManager = listingManager;
            _tokenTask = GetToken();
        }

        public event Action<Stage, int>? Connecting;

        ////public event Action<FailReason>? ConnectionFailed;

        public event Action<ClosedReason>? Closed;

        public event Action<string>? Disconnected;

        public event Action<ChatMessage>? ChatRecieved;

        public event Action<string>? MotdUpdated;

        public event Action<int>? PlayStatusUpdated;

        public event Action<Map>? MapUpdated;

        public event Action<string>? UserBanned;

        internal Status Status { get; private set; } = new();

        public void Dispose()
        {
            _ = Disconnect("Disposed");
        }

        public async Task Disconnect(string reason, bool local = true)
        {
            if (_client == null)
            {
                return;
            }

            Status = new Status();

            _log.Warn($"Disconnected from {_address} ({reason})");
            if (local && _client.IsConnected)
            {
                await _client.Send(new ArraySegment<byte>(new[] { (byte)ServerOpcode.Disconnect }, 0, 1));
            }

            _client.Dispose();
            _client = null;

            Disconnected?.Invoke(reason);
        }

        public void Connect()
        {
            if (_client?.IsConnected ?? false)
            {
                _log.Error("Client already connected!");
                return;
            }

            string? stringAddress = _listingManager.Listing?.IpAddress;
            if (stringAddress == null)
            {
                _log.Error("No IP found.");
                return;
            }

            AsyncTcpClient client = new();
            int portidx = stringAddress.LastIndexOf(':');
            IPAddress address = IPAddress.Parse(stringAddress.Substring(0, portidx));
            int port = int.Parse(stringAddress.Substring(portidx + 1));
            _log.Info($"Connecting to {address}:{port}");
            client.IPAddress = address;
            client.Port = port;
            client.AutoReconnect = true;
            client.AutoReconnectTries = 2;
            client.Message += OnMessageRecieved;
            client.ConnectedCallback += OnConnected;
            client.ReceivedCallback += OnReceived;

            _ = client.RunAsync().ConfigureAwait(false);
            _client = client;
        }

        public async Task Send(byte[] bytes)
        {
            if (_client is not { IsConnected: true })
            {
                _log.Error("Client not connected! Could not send packet");
                return;
            }

            await _client.Send(new ArraySegment<byte>(bytes, 0, bytes.Length));
        }

        private async Task<AuthenticationToken> GetToken()
        {
            UserInfo userInfo = await _platformUserModel.GetUserInfo();
            if (userInfo == null)
            {
                throw new Exception("No authentication token provider could be created");
            }

            return await new PlatformAuthenticationTokenProvider(_platformUserModel, userInfo).GetAuthenticationToken();
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
                byte[] bytes = stream.ToArray();
                await client.Send(new ArraySegment<byte>(bytes, 0, bytes.Length));
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
                ClientOpcode opcode = (ClientOpcode)reader.ReadByte();
                switch (opcode)
                {
                    case ClientOpcode.Authenticated:
                        {
                            _log.Debug($"Authenticated {_address}");
                            Connecting?.Invoke(Stage.ReceivingData, -1);
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
                            Status status = JsonConvert.DeserializeObject<Status>(fullStatus);
                            _log.Info(fullStatus);

                            if (Status.Motd != status.Motd)
                            {
                                MotdUpdated?.Invoke(status.Motd);
                            }

                            if (Status.PlayStatus != status.PlayStatus)
                            {
                                PlayStatusUpdated?.Invoke(status.PlayStatus);
                            }

                            if (Status.Index != status.Index)
                            {
                                MapUpdated?.Invoke(status.Map);
                            }

                            Status = status;
                        }

                        break;

                    case ClientOpcode.ChatMessage:
                        {
                            string message = reader.ReadString();
                            ChatRecieved?.Invoke(JsonConvert.DeserializeObject<ChatMessage>(message));
                        }

                        break;

                    case ClientOpcode.UserBanned:
                        {
                            string message = reader.ReadString();
                            UserBanned?.Invoke(message);
                        }

                        break;

                    default:
                        _log.Warn($"Unhandled opcode: ({opcode})");
                        break;
                }
            }
            catch (Exception e)
            {
                _log.Error("Recieved invalid packet");
                _log.Error(e);
                client.ByteBuffer.Clear();
            }
        }
    }
}
