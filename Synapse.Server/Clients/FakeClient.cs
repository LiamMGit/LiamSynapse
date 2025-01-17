using System.Buffers;
using System.Net;
using System.Text;
using Synapse.Networking.Models;
using Synapse.Server.Models;

namespace Synapse.Server.Clients;

public class FakeClient(string id, string username) : IClient
{
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

    private static readonly Random _random = new();

    private static FakeClient[]? _fakes;

    public FakeClient() : this(
        RandomString(17),
        _adjectives[_random.Next(_adjectives.Length)] +
        _nouns[_random.Next(_nouns.Length)] +
        _random.Next(99) +
        RandomUnicode(4))
    {
    }

    public static FakeClient[] Fakes
    {
        get
        {
            if (_fakes != null)
            {
                return _fakes;
            }

            const int amount = 20;
            _fakes = new FakeClient[amount];
            for (int i = 0; i < amount; i++)
            {
                _fakes[i] = new FakeClient();
            }

            return _fakes;
        }
    }

    public IPAddress Address => IPAddress.Loopback;

    public bool Chatter => false;

    public int Division { get; } = _random.Next(2);

    public string Id { get; } = id;

    public string Username { get; } = username;

    public string DisplayUsername => Username;

    public Task Disconnect(DisconnectCode _)
    {
        return Task.CompletedTask;
    }

    public string? GetColor()
    {
        return null;
    }

    public int GetImmunity()
    {
        return 0;
    }

    public bool HasPermission(Permission permission)
    {
        return false;
    }

    public Task Send(ReadOnlySequence<byte> data, CancellationToken token) => Task.CompletedTask;

    public Task SendChatMessage(ChatMessage message) => Task.CompletedTask;

    public Task SendOpcode(ClientOpcode opcode) => Task.CompletedTask;

    public Task SendRefusal(string reason) => Task.CompletedTask;

    public Task SendPriorityServerMessage(string message, params object?[] args) => Task.CompletedTask;

    public Task SendServerMessage(string message, params object?[] args) => Task.CompletedTask;

    public Task Send(ClientOpcode opcode, string value) => Task.CompletedTask;

    public Task Send(ClientOpcode opcode, byte value) => Task.CompletedTask;

    public override string ToString()
    {
        return $"{DisplayUsername} ({Id})";
    }

    private static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(
            Enumerable
                .Repeat(chars, length)
                .Select(s => s[_random.Next(s.Length)])
                .ToArray());
    }

    private static string RandomUnicode(int length)
    {
        StringBuilder plainText = new();
        for (int j = 0; j < length; ++j)
        {
            plainText.Append((char)_random.Next(char.MinValue, char.MaxValue));
        }

        return plainText.ToString();
    }
}
