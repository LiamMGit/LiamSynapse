using System.Buffers;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Synapse.Networking;
using Synapse.Networking.Models;
using Synapse.TestClient.Extras;
using Synapse.TestClient.Models;

namespace Synapse.TestClient;

public class Client
{
    private static readonly Random _random = new();

    private static readonly string[] _adjectives =
    [
        "Radiant",
        "Serene",
        "Vibrant",
        "Majestic",
        "Exquisite",
        "Blissful",
        "Pensive",
        "Whimsical",
        "Resplendent",
        "Tranquil",
        "Luminous",
        "Enchanting",
        "Spirited",
        "Harmonious",
        "Captivating",
        "Mellifluous"
    ];

    private static readonly string[] _nouns =
    [
        "Sunshine",
        "Mountain",
        "Ocean",
        "Meadow",
        "Rainbow",
        "Whisper",
        "Adventure",
        "Harmony",
        "Cascade",
        "Mystery",
        "Garden",
        "Infinity",
        "Journey",
        "Silhouette",
        "Symphony",
        "Serendipity"
    ];

    private readonly string[] _testMessages =
    [
        "Hello, World!",
        "undefined",
        "!@#$%^&*()`~",
        "The cake is a lie",
        "Terrible terrible damage",
        "C#",
        "Wow what a cool event!",
        "Cyan is a furry",
        "I come to cleanse this land",
        "ITS NO USE!",
        "we are back",
        "what is a beat saber? a miserable pile of cubes",
        "NUCLEAR LAUNCH DETECTED",
        "beat saber. beat saber never changes",
        "get pwned n00b",
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua",
        "hey guys, i think we should all use audiolink",
        "mawntee is a furry",
        "owo whats this?",
        "The right man in the wrong place can make all the difference in the world",
        "All your base are belong to us",
        "I used to be a player like you, then I took an arrow in the knee",
        "Press alt+f4 for free robux",
        "Entaro adun",
        "|||||||||||",
        "This entire discord server must be purged.",
        "She sells seashells by the sea shore.",
        "Connection terminated.",
        "LEEEEEEEEERRRRRROOOOOOOOYYYYYYYYYYYYY",
        "blame reaxt",
        "vivify is kewl",
        "Why did i spend time writing these?",
        "good morning cyan",
        "har har har har",
        "TWENTY EIGHT EXCEPTIONS",
        "Stay a while and listen",
        "Did I miss anything?",
        "I see much of myself in you, and I can tell you from personal experience that things do indeed get better.",
        "Resources.FindObjectOfTypeAll<Player>().ToList().ForEach(n => n.GiveHug());",
        "MY NEXT BIG MAP IS COMPLETE*. this blows everything I've made previously out of the water.",
        "If Extra Sensory 1 is so great, why isn't there an Extra Sensory 2?",
        "It's time to write shaders and chew bubble gum... and I’m all outta gum.",
        "It's dangerous to go alone, take this!",
        "Praise the skybox!",
        "Grass grows, birds fly, the sun shines, and brother, I write shaders."
    ];

    private readonly ILogger<Client> _log;
    private readonly ListingService _listingService;
    private readonly TimeSyncManager _timeSyncManager;

    private readonly List<byte[]> _queuedPackets = [];

    private string _address = string.Empty;
    private AsyncTcpLocalClient? _client;

    public Client(ILogger<Client> log, ListingService listingService)
    {
        _log = log;
        _listingService = listingService;
        _timeSyncManager = new TimeSyncManager(this);
    }

    internal Status Status { get; private set; } = new();

    public string Username { get; } = _adjectives[_random.Next(_adjectives.Length)] +
                                      _nouns[_random.Next(_nouns.Length)] + _random.Next(99);

    public override string ToString()
    {
        return Username;
    }

    public async Task Send(ReadOnlySequence<byte> data, CancellationToken cancellationToken = default)
    {
        if (_client is not { IsConnected: true })
        {
            _log.LogWarning("[{Client}] Client not connected! Delaying sending packet", this);
            _queuedPackets.Add(data.ToArray());
            return;
        }

        await _client.Send(data, cancellationToken);
    }

    public async Task Send(ServerOpcode opcode, bool value)
    {
        using PacketBuilder packetBuilder = new((byte)opcode);
        packetBuilder.Write(value);
        await Send(packetBuilder.ToBytes());
    }

    public async Task Send(ServerOpcode opcode, int value)
    {
        using PacketBuilder packetBuilder = new((byte)opcode);
        packetBuilder.Write(value);
        await Send(packetBuilder.ToBytes());
    }

    public async Task Send(ServerOpcode opcode, float value)
    {
        using PacketBuilder packetBuilder = new((byte)opcode);
        packetBuilder.Write(value);
        await Send(packetBuilder.ToBytes());
    }

    public async Task Send(ServerOpcode opcode, string value)
    {
        using PacketBuilder packetBuilder = new((byte)opcode);
        packetBuilder.Write(value);
        await Send(packetBuilder.ToBytes());
    }

    internal Task Disconnect(DisconnectCode code, Exception? exception = null, bool notify = true)
    {
        return Disconnect(code.ToReason(), exception, notify ? code : null);
    }

    internal async Task Disconnect(string reason, Exception? exception = null, DisconnectCode? notifyCode = null)
    {
        if (_client == null)
        {
            return;
        }

        _log.LogInformation(exception, "[{Client}] Disconnected: {Reason}", this, reason);

        AsyncTcpClient client = _client;
        _client = null;

        _timeSyncManager.Dispose();

        await client.Disconnect(notifyCode);
    }

    internal async Task RunAsync()
    {
        if (_client != null)
        {
            _log.LogError("[{Client}] Client still running, disposing", this);
            _client.Dispose();
        }

        string stringAddress = _listingService.Listing.IpAddress;

        Status = new Status();
        int portIdx = stringAddress.LastIndexOf(':');
        IPAddress address = IPAddress.Parse(stringAddress.AsSpan(0, portIdx));
        int port = int.Parse(stringAddress[(portIdx + 1)..]);
        _address = $"{address}:{port}";
        using AsyncTcpLocalClient client = new(address, port, 3);
        _log.LogDebug("[{Client}] Connecting to {Address}", this, _address);
        client.Message += OnMessageReceived;
        client.ConnectedCallback = OnConnected;
        client.ReceivedCallback = OnReceived;
        _client = client;
        try
        {
            await client.RunAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (AsyncTcpFailedAfterRetriesException e)
        {
            await Disconnect($"Connection failed after {e.ReconnectTries} tries", e.InnerException);
        }
        catch (AsyncTcpSocketException e)
        {
            await Disconnect(DisconnectCode.ConnectionClosedUnexpectedly, e, false);
        }
        catch (Exception e)
        {
            await Disconnect(DisconnectCode.UnexpectedException, e, false);
        }

        client.Message -= OnMessageReceived;
        client.ConnectedCallback = null;
        client.ReceivedCallback = null;
    }

    internal async Task SendRandomMessages()
    {
        while (_client is { IsConnected: true })
        {
            await Send(ServerOpcode.ChatMessage, _testMessages[_random.Next(_testMessages.Length)]);
            await Task.Delay(_random.Next(10000, 200000));
        }
    }

    private async Task OnConnected(CancellationToken cancelToken)
    {
        try
        {
            if (_client is not { IsConnected: true })
            {
                throw new InvalidOperationException("Client not connected.");
            }

            _log.LogDebug("[{Client}] Successfully connected to {Address}", this, _address);

            using PacketBuilder packetBuilder = new((byte)ServerOpcode.Authentication);
            packetBuilder.Write($"{_random.Next(9999999):D7}");
            packetBuilder.Write(Username);
            packetBuilder.Write((byte)Platform.Test);
            packetBuilder.Write("token");
            packetBuilder.Write("1.29.1");
            packetBuilder.Write(_listingService.Listing.Guid);
            await _client.Send(packetBuilder.ToBytes(), cancelToken);

            byte[][] queued = _queuedPackets.ToArray();
            _queuedPackets.Clear();
            foreach (byte[] data in queued)
            {
                _ = _client.Send(new ReadOnlySequence<byte>(data), cancelToken);
            }

            _ = _timeSyncManager.StartSync();
        }
        catch (Exception e)
        {
            await Disconnect(DisconnectCode.UnexpectedException, e);
        }
    }

    private void OnMessageReceived(object? _, AsyncTcpMessageEventArgs args)
    {
        if (args.Exception != null)
        {
            _log.LogError(args.Exception, "[{Client}] {Message}", this, args.Message);
        }
    }

    private async Task OnReceived(byte opcode, BinaryReader reader, CancellationToken cancelToken)
    {
        switch ((ClientOpcode)opcode)
        {
            case ClientOpcode.Authenticated:
            {
                _log.LogDebug("[{Client}] Authenticated {Address}", this, _address);
                _ = Send(ServerOpcode.SetChatter, true);

                break;
            }

            case ClientOpcode.Disconnect:
            {
                DisconnectCode disconnectCode = (DisconnectCode)reader.ReadByte();
                await Disconnect($"Disconnected by server: {disconnectCode.ToReason()}");

                break;
            }

            case ClientOpcode.RefusedPacket:
            {
                string refusal = reader.ReadString();
                _log.LogWarning("[{Client}] Packet refused by server ({Refusal})", this, refusal);

                break;
            }

            case ClientOpcode.Ping:
                float clientTime = reader.ReadSingle();
                float serverTime = reader.ReadSingle();
                _timeSyncManager.Pong(clientTime, serverTime);
                break;

            case ClientOpcode.Status:
            {
                string fullStatus = reader.ReadString();
                Status status = JsonSerializer.Deserialize<Status>(fullStatus, JsonSettings.Settings)!;
                Status = status;
                break;
            }

            case ClientOpcode.ChatMessage:
            {
                break;
            }

            case ClientOpcode.UserBanned:
            {
                break;
            }

            case ClientOpcode.LeaderboardScores:
            {
                break;
            }

            case ClientOpcode.StopLevel:
                break;

            default:
                _log.LogWarning("[{Client}] Unhandled opcode: ({Opcode})", this, opcode);
                return;
        }
    }
}
