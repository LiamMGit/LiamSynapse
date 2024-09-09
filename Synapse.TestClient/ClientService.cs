using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Synapse.Networking.Models;
using Synapse.TestClient.Extras;

namespace Synapse.TestClient;

public class ClientService
{
    private static readonly Random _random = new();

    private readonly HashSet<Client> _clients = [];

    private readonly ILogger<ClientService> _log;
    private readonly IServiceProvider _provider;

    public ClientService(
        ILogger<ClientService> log,
        IServiceProvider provider)
    {
        _log = log;
        _provider = provider;
    }

    public async Task Deploy(int duration, CancellationToken token)
    {
        Client instance = ActivatorUtilities.CreateInstance<Client>(_provider);
        try
        {
            _log.LogInformation(
                "Deploying [{Client}] for [{Duration}] seconds",
                instance,
                duration < 0 ? "permanently" : duration * 0.001f);
            _ = instance.RunAsync();
            _clients.Add(instance);
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
            _clients.Remove(instance);
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
                //await Task.Delay(_random.Next(100, 2000), token);
            }

            await Task.WhenAll(tasks);
        }
        catch (Exception e)
        {
            _log.LogError(e, "Failed to deploy");
        }
    }

    public async Task Score(CancellationToken token)
    {
        List<Task> tasks = [];
        foreach (Client client in _clients)
        {
            if (client.Status.Stage is PlayStatus playStatus)
            {
                ScoreSubmission scoreSubmission = new()
                {
                    Index = playStatus.Index,
                    Score = _random.Next(99999)
                };
                string scoreJson = JsonSerializer.Serialize(scoreSubmission, JsonSettings.Settings);
                tasks.Add(await Task.Delay(_random.Next(10, 100), token).ContinueWith(_ => client.Send(ServerOpcode.ScoreSubmission, scoreJson), token));
            }
            else
            {
                _log.LogError("[{Client}] Can not submit score, no map active", client);
            }
        }

        await Task.WhenAll(tasks);
    }

    public void SendRandomMessages()
    {
        foreach (Client client in _clients)
        {
            _ = client.SendRandomMessages();
        }
    }
}
