using System.Net;
using Microsoft.Extensions.Logging;
using Synapse.Networking.Models;
using Synapse.Server.Models;

namespace Synapse.Server.Clients;

public class ServerClient(ILogger<ServerClient> log) : IClient
{
    public IPAddress Address => IPAddress.Loopback;

    public bool Chatter => false;

    public string Id => string.Empty;

    public string Username => "Server";

    public Task Disconnect(string reason, bool local = true)
    {
        return Task.CompletedTask;
    }

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

    public Task SendOpcode(ClientOpcode opcode)
    {
        return Task.CompletedTask;
    }

    public Task SendRefusal(string reason)
    {
        return Task.CompletedTask;
    }

    public Task SendServerMessage(string message, params object?[] args)
    {
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
#pragma warning disable CA2254
        log.LogInformation(message, args);
#pragma warning restore CA2254
        return Task.CompletedTask;
    }

    public Task SendString(ClientOpcode opcode, string message)
    {
        return Task.CompletedTask;
    }

    public override string ToString()
    {
        return Username;
    }
}
