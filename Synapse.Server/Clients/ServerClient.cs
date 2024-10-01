using System.Net;
using Microsoft.Extensions.Logging;
using Synapse.Networking.Models;
using Synapse.Server.Models;

namespace Synapse.Server.Clients;

public class ServerClient(ILogger<ServerClient> log) : IClient
{
    public IPAddress Address => IPAddress.Loopback;

    public bool Chatter => false;

    public int Division => -1;

    public string Id => string.Empty;

    public string Username => "Server";

    public string DisplayUsername => Username;

    public Task Disconnect(DisconnectCode _) => Task.CompletedTask;

    public string GetColor()
    {
        return "yellow";
    }

    public int GetImmunity()
    {
        return int.MaxValue;
    }

    public bool HasPermission(Permission permission)
    {
        return true;
    }

    public Task SendChatMessage(ChatMessage message)
    {
        string id = message.Id;
        string client = $"({id}) {message.Username}";
        log.LogInformation("[{Client}] {Message}", client, message.Message);
        return Task.CompletedTask;
    }

    public Task SendOpcode(ClientOpcode opcode) => Task.CompletedTask;

    public Task SendRefusal(string reason) => Task.CompletedTask;

    public Task SendServerMessage(string message, params object?[] args)
    {
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
#pragma warning disable CA2254
        log.LogInformation(message, args);
#pragma warning restore CA2254
        return Task.CompletedTask;
    }

    public Task SendString(ClientOpcode opcode, string message) => Task.CompletedTask;

    public Task SendInt(ClientOpcode opcode, int value) => Task.CompletedTask;

    public override string ToString()
    {
        return Username;
    }
}
