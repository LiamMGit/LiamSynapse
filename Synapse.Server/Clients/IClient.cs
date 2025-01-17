using System.Buffers;
using System.Net;
using JetBrains.Annotations;
using Synapse.Networking.Models;
using Synapse.Server.Models;

namespace Synapse.Server.Clients;

public interface IClient
{
    public IPAddress Address { get; }

    public bool Chatter { get; }

    public int Division { get; }

    public string Id { get; }

    public string Username { get; }

    public string DisplayUsername { get; }

    public Task Disconnect(DisconnectCode code);

    public string? GetColor();

    public int GetImmunity();

    public bool HasPermission(Permission permission);

    public Task Send(ReadOnlySequence<byte> data, CancellationToken token = default);

    public Task SendChatMessage(ChatMessage message);

    public Task SendOpcode(ClientOpcode opcode);

    public Task SendRefusal(string reason);

    public Task SendPriorityServerMessage([StructuredMessageTemplate] string message, params object?[] args);

    public Task SendServerMessage([StructuredMessageTemplate] string message, params object?[] args);

    public Task Send(ClientOpcode opcode, string value);

    public Task Send(ClientOpcode opcode, byte value);
}
