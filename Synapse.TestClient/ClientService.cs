using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Synapse.Networking.Models;

namespace Synapse.TestClient;

public class ClientService(
    ILogger<ClientService> log,
    IServiceProvider provider)
{
    public HashSet<Client> Clients { get; } = [];

    public async Task Deploy(int duration, CancellationToken token)
    {
        Client instance = ActivatorUtilities.CreateInstance<Client>(provider);
        try
        {
            log.LogInformation(
                "Deploying [{Client}] for [{Duration}] seconds",
                instance,
                duration < 0 ? "permanently" : duration * 0.001f);
            _ = instance.RunAsync();
            Clients.Add(instance);
            await Task.Delay(duration, token);
            _ = instance.Disconnect(DisconnectCode.DisconnectedByUser);
        }
        catch (OperationCanceledException)
        {
            _ = instance.Disconnect(DisconnectCode.DisconnectedByUser);
        }
        catch (Exception e)
        {
            _ = instance.Disconnect(DisconnectCode.UnexpectedException, e);
        }
        finally
        {
            Clients.Remove(instance);
        }
    }

    public async Task MassDeploy(int count, int duration, CancellationToken token)
    {
        try
        {
            List<Task> tasks = [];
            for (int i = 0; i < count; i++)
            {
                tasks.Add(Deploy(duration, token));
                await Task.Delay(10, token);
            }

            await Task.WhenAll(tasks);
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to deploy");
        }
    }

    public async Task Score(CancellationToken token)
    {
        Task[] tasks = Clients.Select(client => client.CreateAndSubmitScore(token)).ToArray();
        await Task.WhenAll(tasks);
    }

    public void SendRandomMessages()
    {
        foreach (Client client in Clients)
        {
            _ = client.SendRandomMessages();
        }
    }
}
