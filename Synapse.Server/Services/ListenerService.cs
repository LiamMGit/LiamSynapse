using System.Collections.Concurrent;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Synapse.Networking;
using Synapse.Networking.Models;
using Synapse.Server.Clients;
using Synapse.Server.Extras;

namespace Synapse.Server.Services;

public interface IListenerService
{
    public event Action<IClient>? ClientConnected;

    public event Action<IClient, string>? CommandReceived;

    public event Action<IClient, int>? LeaderboardRequested;

    public event Action<IClient, ScoreSubmission>? ScoreSubmissionReceived;

    public ConcurrentDictionary<IClient, byte> Chatters { get; }

    public ConcurrentDictionary<string, IClient> Clients { get; }

    public void AllClients(Func<IClient, Task> action);

    public void BanIp(IClient client);

    public void Blacklist(IClient client, string? reason, DateTime? banTime);

    public void BroadcastChatMessage(string id, string username, string? color, string message);

    public void BroadcastServerMessage([StructuredMessageTemplate] string message, params object?[] args);

    public void BroadcastString(ClientOpcode clientOpcode, string message);

    public Task RunAsync();

    public Task Stop();

    public void Whitelist(IClient client);
}

public class ListenerService : IListenerService
{
    private readonly IBlacklistService _blacklistService;
    private readonly ILogger<ListenerService> _log;

    private readonly AsyncTcpListener _listener;
    private readonly int _maxPlayers;
    private readonly IServiceProvider _provider;
    private readonly IClient _serverClient;

    public ListenerService(
        ILogger<ListenerService> log,
        IConfiguration config,
        IServiceProvider provider,
        IBlacklistService blacklistService,
        IClient serverClient)
    {
        _log = log;
        _provider = provider;
        _blacklistService = blacklistService;
        _serverClient = serverClient;
        int port = config.GetValue<int>("Port");
        _listener = new AsyncTcpListener(port);
        _maxPlayers = config.GetValue<int>("MaxPlayers");
        log.LogInformation("Listening on [{Port}]", port);
    }

    public event Action<IClient>? ClientConnected;

    public event Action<IClient, string>? CommandReceived;

    public event Action<IClient, int>? LeaderboardRequested;

    public event Action<IClient, ScoreSubmission>? ScoreSubmissionReceived;

    public ConcurrentDictionary<IClient, byte> Chatters { get; } = new();

    public ConcurrentDictionary<string, IClient> Clients { get; } = new();

    public Task RunAsync()
    {
        _listener.ClientConnectedCallback = OnClientConnected;
        return _listener.RunAsync();
    }

    public void AllClients(Func<IClient, Task> action)
    {
        foreach (IClient client in Clients.Values)
        {
            try
            {
                action(client);
            }
            catch (Exception e)
            {
                _log.LogError(e, "Exception on {Client}", client);
            }
        }

        action(_serverClient);
    }

    public void BanIp(IClient client)
    {
        string id = client.Id;
        if (!Clients.ContainsKey(id))
        {
            return;
        }

        string ip = client.Address.ToString();
        _blacklistService.AddBannedIp(ip);
    }

    public void Blacklist(IClient client, string? reason, DateTime? banTime)
    {
        string id = client.Id;
        if (!Clients.TryRemove(id, out _))
        {
            return;
        }

        string username = client.Username;
        _blacklistService.AddBlacklist(id, username, reason, banTime);
        _ = client.Disconnect(DisconnectCode.Banned); // TODO: pass reason / bantime to this
        BroadcastString(ClientOpcode.UserBanned, id);
    }

    public void BroadcastChatMessage(string id, string username, string? color, string message)
    {
        AllClients(n => n.SendChatMessage(new ChatMessage(id, username, color, MessageType.Say, message)));
    }

    public void BroadcastServerMessage(string message, params object?[] args)
    {
        AllClients(n => n.SendServerMessage(message, args));
    }

    /*public void Broadcast(byte[] bytes)
    {
        AllClients(n => n.Send(new ArraySegment<byte>(bytes, 0, bytes.Length)));
    }*/

    public void BroadcastString(ClientOpcode opcode, string message)
    {
        AllClients(n => n.SendString(opcode, message));
    }

    public async Task Stop()
    {
        AllClients(n => n.Disconnect(DisconnectCode.ServerClosing));
        _log.LogInformation("Closing...");
        await Task.Delay(2000);
        _listener.Dispose();
    }

    public void Whitelist(IClient client)
    {
        string id = client.Id;
        if (!Clients.ContainsKey(id))
        {
            return;
        }

        string username = client.Username;
        _blacklistService.AddWhitelist(id, username);
    }

    private async Task OnClientConnected(AsyncTcpServerClient tcpClient, CancellationToken token)
    {
        ConnectedClient client = ActivatorUtilities.CreateInstance<ConnectedClient>(_provider, tcpClient);

        string ip = client.Address.ToString();

        /*if (RateLimiter.RateLimit(this, 4, 10000, ip))
        {
            await client.Disconnect(DisconnectCode.RateLimited);
            return;
        }*/

        if (CheckMaxConnection(client))
        {
            return;
        }

        if (_blacklistService.BannedIps.ContainsKey(ip))
        {
            await client.Disconnect(DisconnectCode.Banned);
            return;
        }

        client.Connected += OnJoined;
        client.Disconnected += OnDisconnected;
        client.ChatJoined += OnChatJoined;
        client.ChatLeft += OnChatLeft;
        client.ChatMessageReceived += OnChatMessageReceived;
        client.CommandReceived += OnCommandReceived;
        client.ScoreSubmissionReceived += OnScoreSubmissionReceived;
        client.LeaderboardRequested += OnLeaderboardRequested;
        await client.RunAsync();
        client.Connected -= OnJoined;
        client.Disconnected -= OnDisconnected;
        client.ChatJoined -= OnChatJoined;
        client.ChatLeft -= OnChatLeft;
        client.ChatMessageReceived -= OnChatMessageReceived;
        client.CommandReceived -= OnCommandReceived;
        client.ScoreSubmissionReceived -= OnScoreSubmissionReceived;
        client.LeaderboardRequested -= OnLeaderboardRequested;
    }

    private void OnChatJoined(ConnectedClient client)
    {
        Chatters.TryAdd(client, 0);
        BroadcastServerMessage("{Username} has joined.", client.Username);
    }

    private void OnChatLeft(ConnectedClient client)
    {
        Chatters.TryRemove(client, out _);
        BroadcastServerMessage("{Username} has left.", client.Username);
    }

    private void OnChatMessageReceived(ConnectedClient connectedClient, string message)
    {
        string censored = StringUtils.Sanitize(message);
        if (string.IsNullOrWhiteSpace(censored))
        {
            return;
        }

        ////_log.LogInformation("[{Client}]{Extra} {Message}", connectedClient, censored == message ? string.Empty : " (censored)", message);
        BroadcastChatMessage(connectedClient.Id, connectedClient.Username, connectedClient.GetColor(), censored);
    }

    private void OnCommandReceived(ConnectedClient client, string command)
    {
        CommandReceived?.Invoke(client, command);
    }

    private void OnDisconnected(ConnectedClient client, string reason)
    {
        Clients.Remove(client.Id, out _);
    }

    private void OnJoined(ConnectedClient client)
    {
        if (Clients.TryGetValue(client.Id, out IClient? existing))
        {
            _ = existing.Disconnect(DisconnectCode.DuplicateConnection);
        }

        if (CheckMaxConnection(client))
        {
            return;
        }

        Clients.TryAdd(client.Id, client);
        _log.LogInformation("{Client} ({GameVersion}) connected", client, client.GameVersion);

        ClientConnected?.Invoke(client);
    }

    private void OnLeaderboardRequested(ConnectedClient connectedClient, int index)
    {
        try
        {
            LeaderboardRequested?.Invoke(connectedClient, index);
        }
        catch (Exception e)
        {
            _log.LogError(e, "Error receiving leaderboard request");
        }
    }

    private void OnScoreSubmissionReceived(ConnectedClient connectedClient, ScoreSubmission scoreSubmission)
    {
        ScoreSubmissionReceived?.Invoke(connectedClient, scoreSubmission);
    }

    private bool CheckMaxConnection(ConnectedClient client)
    {
        if (Clients.Count + 1 > _maxPlayers)
        {
            return false;
        }

        _ = client.Disconnect(DisconnectCode.MaximumConnections);
        return true;
    }
}
