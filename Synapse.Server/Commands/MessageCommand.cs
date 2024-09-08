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
        string message = arguments.Unwrap();

        listenerService.BroadcastServerMessage("[Server] {Say}", message);
        ////_log.LogInformation("[Server] {Say}", arguments);
    }
}
