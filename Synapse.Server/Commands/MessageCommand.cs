using Synapse.Server.Clients;
using Synapse.Server.Extras;
using Synapse.Server.Models;
using Synapse.Server.Services;

namespace Synapse.Server.Commands;

public class MessageCommand(
    IListenerService listenerService)
{
    [Command("say", Permission.Coordinator)]
    public void Say(IClient client, string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            client.SendServerMessage("Invalid message");
            return;
        }

        listenerService.BroadcastServerMessage("[Server] {Say}", arguments);
        ////_log.LogInformation("[Server] {Say}", arguments);
    }
}
