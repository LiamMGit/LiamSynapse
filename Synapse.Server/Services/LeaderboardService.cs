using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Synapse.Networking.Models;
using Synapse.Server.Clients;
using Synapse.Server.Extras;
using Synapse.Server.Models;

namespace Synapse.Server.Services;

public interface ILeaderboardService
{
    public event Action<IClient>? ScoreSubmitted;

    public IReadOnlyList<IReadOnlyList<SavedScore>> AllScores { get; }

    public void BroadcastLeaderboard(int index);

    public void DropScores(int index);

    public void GenerateTestScores();

    public void RemoveScore(int index, SavedScore score);

    public Task SubmitTournamentScores(int index);

    public bool TryGetScore(int index, IClient serverClient, out SavedScore score);
}

public class LeaderboardService : ILeaderboardService
{
    private static readonly LeaderboardCell _placeholderCell = new()
    {
        Rank = -1,
        PlayerName = "...",
        Percentage = -1,
        Score = -1
    };

    private static readonly Random _random = new();
    private readonly IBackupService _backupService;
    private readonly ConcurrentList<ImmutableList<LeaderboardCell>?> _cachedAllScores;
    private readonly ConcurrentList<ImmutableList<LeaderboardCell>?> _cachedScores;
    private readonly IListenerService _listenerService;

    private readonly ILogger<LeaderboardService> _log;
    private readonly IMapService _mapService;

    private readonly ConcurrentList<ConcurrentDictionary<string, SavedScore>> _savedIds;
    private readonly ConcurrentList<ConcurrentList<SavedScore>> _sortedAllScores;
    private readonly ConcurrentList<ConcurrentList<SavedScore>> _sortedScores;
    private readonly ITournamentService _tournamentService;

    public LeaderboardService(
        ILogger<LeaderboardService> log,
        IMapService mapService,
        IListenerService listenerService,
        ITournamentService tournamentService,
        IBackupService backupService)
    {
        _log = log;
        _mapService = mapService;
        _listenerService = listenerService;
        _tournamentService = tournamentService;
        _backupService = backupService;
        listenerService.ScoreSubmissionReceived += OnScoreSubmissionReceived;
        listenerService.LeaderboardRequested += OnLeaderboardRequested;

        int mapCount = _mapService.MapCount;
        _cachedScores = ConcurrentList<ImmutableList<LeaderboardCell>?>.Prefilled(mapCount);
        _cachedAllScores = ConcurrentList<ImmutableList<LeaderboardCell>?>.Prefilled(mapCount);

        _savedIds = new ConcurrentList<ConcurrentDictionary<string, SavedScore>>(
            Enumerable
                .Range(0, mapCount)
                .Select(_ => new ConcurrentDictionary<string, SavedScore>()));

        _sortedScores =
            new ConcurrentList<ConcurrentList<SavedScore>>(
                Enumerable
                    .Range(0, mapCount)
                    .Select(_ => new ConcurrentList<SavedScore>()));

        _sortedAllScores =
            new ConcurrentList<ConcurrentList<SavedScore>>(
                Enumerable
                    .Range(0, mapCount)
                    .Select(_ => new ConcurrentList<SavedScore>()));

        backupService.BackupsLoaded += OnBackupsLoaded;
    }

    public event Action<IClient>? ScoreSubmitted;

    public IReadOnlyList<IReadOnlyList<SavedScore>> AllScores => _sortedAllScores.AsReadOnly();

    public void BroadcastLeaderboard(int index)
    {
        RateLimiter.Timeout(() => _listenerService.AllClients(n => SendLeaderboard(n, index)), 4000);
    }

    public void DropScores(int index)
    {
        _savedIds[index].Clear();
        _sortedAllScores[index].Clear();
        _sortedScores[index].Clear();
        _cachedScores[index] = null;
        _cachedAllScores[index] = null;
        _backupService.SaveScores(index, _sortedAllScores[index], null);
        BroadcastLeaderboard(index);
    }

    public void GenerateTestScores()
    {
        int index = _mapService.Index;

        List<FakeClient> list = FakeClient.Fakes.ToList();
        int count = _random.Next(list.Count / 4);
        list.RemoveRange(_random.Next(list.Count - count), count);
        list.ForEach(
            n =>
            {
                int score = _random.Next(0, 9999999);
                SubmitScore(n, index, score, score / 9999999f);
            });
        int score = _random.Next(0, 9999999);
        SubmitScore(FakeClient.Aeroluna, index, score, score / 9999999f);
    }

    public void RemoveScore(int index, SavedScore score)
    {
        _savedIds[index].Remove(score.Id, out _);
        _sortedAllScores[index].Remove(score);
        _sortedScores[index].Remove(score);
        _cachedScores[index] = null;
        _cachedAllScores[index] = null;
        _backupService.SaveScores(index, _sortedAllScores[index], null);
        BroadcastLeaderboard(index);
    }

    public async Task SubmitTournamentScores(int index)
    {
        await _tournamentService.SubmitScores(index, _sortedScores[index]);
        _cachedScores[index] = null;
        _cachedAllScores[index] = null;
        BroadcastLeaderboard(index);
    }

    public bool TryGetScore(int index, IClient serverClient, out SavedScore score)
    {
        return _savedIds[index].TryGetValue(serverClient.Id, out score);
    }

    private IReadOnlyList<LeaderboardCell> GetLeaderboard(
        ConcurrentList<ConcurrentList<SavedScore>> sortedScores,
        ConcurrentList<ImmutableList<LeaderboardCell>?> cache,
        int index,
        string id,
        out int leaderboardSpecialIndex)
    {
        ConcurrentList<SavedScore> mapSortedScores = sortedScores[index];
        int playerScoreIndex = mapSortedScores.FindIndex(n => n.Id == id);

        cache[index] ??= Enumerable
            .Range(0, Math.Min(12, mapSortedScores.Count))
            .Select(
                n =>
                {
                    SavedScore score = sortedScores[index][n];
                    return new LeaderboardCell
                    {
                        Rank = n,
                        PlayerName = score.Username,
                        Percentage = score.Percentage,
                        Score = score.Score,
                        Color = _tournamentService.GetColor(index, score.Id)
                    };
                })
            .ToImmutableList();

        ImmutableList<LeaderboardCell> scores = cache[index]!;

        if (playerScoreIndex <= 11)
        {
            leaderboardSpecialIndex = playerScoreIndex;
            return scores;
        }

        leaderboardSpecialIndex = 11;
        List<LeaderboardCell> modifiedScores = scores.ToList();
        SavedScore playerScore = mapSortedScores[playerScoreIndex];
        modifiedScores[10] = _placeholderCell;
        modifiedScores[11] = new LeaderboardCell
        {
            Rank = playerScoreIndex,
            PlayerName = playerScore.Username,
            Percentage = playerScore.Percentage,
            Score = playerScore.Score,
            Color = _tournamentService.GetColor(index, playerScore.Id)
        };

        return modifiedScores;
    }

    private void OnBackupsLoaded(IReadOnlyList<Backup> backups)
    {
        for (int i = 0; i < backups.Count; i++)
        {
            Backup backup = backups[i];
            SavedScore[] orderedScores = backup.Scores.OrderByDescending(n => n.Score).ToArray();
            _savedIds[i] = new ConcurrentDictionary<string, SavedScore>(orderedScores.ToDictionary(n => n.Id, n => n));
            _cachedScores[i] = null;
            _cachedAllScores[i] = null;
            _sortedAllScores[i] = new ConcurrentList<SavedScore>(orderedScores);
            if (backup.ActivePlayers != null)
            {
                _sortedScores[i] =
                    new ConcurrentList<SavedScore>(orderedScores.Where(n => backup.ActivePlayers.Contains(n.Id)));
            }
            else
            {
                _sortedScores[i] = new ConcurrentList<SavedScore>(orderedScores);
            }
        }

        for (int i = backups.Count; i < _mapService.MapCount; i++)
        {
            _savedIds[i] = new ConcurrentDictionary<string, SavedScore>();
            _cachedScores[i] = null;
            _cachedAllScores[i] = null;
            _sortedAllScores[i] = new ConcurrentList<SavedScore>();
            _sortedScores[i] = new ConcurrentList<SavedScore>();
        }
    }

    private void OnLeaderboardRequested(IClient client, int index)
    {
        _ = SendLeaderboard(client, index);
    }

    private void OnScoreSubmissionReceived(IClient client, ScoreSubmission scoreSubmission)
    {
        SubmitScore(client, scoreSubmission.Index, scoreSubmission.Score, scoreSubmission.Percentage);
    }

    private async Task SendLeaderboard(IClient client, int index)
    {
        if (index > _mapService.MapCount || index < 0)
        {
            await client.SendRefusal($"Index out of range ({index})");
            return;
        }

        string id = client.Id;

        IReadOnlyList<LeaderboardCell> scores = GetLeaderboard(
            _sortedScores,
            _cachedScores,
            index,
            id,
            out int leaderboardSpecialIndex);

        IReadOnlyList<LeaderboardCell> elimScores = GetLeaderboard(
            _sortedAllScores,
            _cachedAllScores,
            index,
            id,
            out int leaderboardSpecialElimIndex);

        LeaderboardScores leaderboardScores = new()
        {
            Index = index,
            Title = _mapService.Maps[index].Name,
            PlayerScoreIndex = leaderboardSpecialIndex,
            Scores = scores,
            ElimPlayerScoreIndex = leaderboardSpecialElimIndex,
            ElimScores = elimScores
        };

        await client.SendString(
            ClientOpcode.LeaderboardScores,
            JsonSerializer.Serialize(leaderboardScores, JsonUtils.Settings));
    }

    private void SubmitScore(IClient client, int index, int score, float percentage)
    {
        try
        {
            if (index > _mapService.Index)
            {
                _ = client.SendRefusal("Not accepting scores for map");
                return;
            }

            string id = client.Id;

            if (!_savedIds[index].ContainsKey(id))
            {
                _log.LogInformation(
                    "[{Client}] scored [{Score} ({Percentage})] on [{Map}]",
                    client,
                    score,
                    percentage,
                    _mapService.Maps[index].Name);
                Submit();
                ScoreSubmitted?.Invoke(client);
            }
            else if (_mapService.Maps[index].Ruleset?.AllowResubmission ?? false)
            {
                ConcurrentList<SavedScore> sortedElim = _sortedAllScores[index];
                int prevIndex = sortedElim.FindIndex(n => n.Id == client.Id);
                SavedScore prevScore = sortedElim[prevIndex];
                if (prevScore.Score <= score)
                {
                    sortedElim.RemoveAt(prevIndex);
                    ConcurrentList<SavedScore> sorted = _sortedScores[index];
                    sorted.RemoveAt(sorted.FindIndex(n => n.Id == client.Id));
                    _log.LogInformation(
                        "[{Client}] rescored [{Score} ({Percentage})] on [{Map}]",
                        client,
                        score,
                        percentage,
                        _mapService.Maps[index].Name);
                    Submit();
                }
            }
            else
            {
                _ = client.SendRefusal("Already scored");
            }

            void Submit()
            {
                SavedScore savedScore = new(score, percentage, id, client.Username);
                _savedIds[index][id] = savedScore;
                _sortedAllScores[index].InsertIntoSortedList(savedScore);
                _cachedAllScores[index] = null;
                if (!_tournamentService.IsEliminated(index, id))
                {
                    _sortedScores[index].InsertIntoSortedList(savedScore);
                    _cachedScores[index] = null;
                }

                RateLimiter.Timeout(
                    () => _backupService.SaveScores(index, _sortedAllScores[index], null),
                    2000);

                BroadcastLeaderboard(index);
            }
        }
        catch (Exception e)
        {
            _log.LogError(e, "Exception while submitting score for [{Client}]", client);
        }
    }
}
