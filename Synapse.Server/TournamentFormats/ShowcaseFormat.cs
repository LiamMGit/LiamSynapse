using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Synapse.Server.Clients;
using Synapse.Server.Extras;
using Synapse.Server.Models;
using Synapse.Server.Services;

namespace Synapse.Server.TournamentFormats;

public class ShowcaseFormat : ITournamentFormat
{
    private const string RICH_COLOR = "<color=#ff6464>";

    private readonly IListenerService _listenerService;
    private readonly int _mapCount;

    public ShowcaseFormat(
        IListenerService listenerService,
        IMapService mapService)
    {
        _listenerService = listenerService;
        _mapCount = mapService.Maps.Count;
        if (_mapCount < 3)
        {
            throw new InvalidOperationException("Not enough maps for this format");
        }

        ActivePlayers = ConcurrentList<SavedScore[]?>.Prefilled(_mapCount, _ => null);
    }

    public event Action<LogLevel, string, object[]>? Log;

    public event Action<string, object[]>? Broadcast;

    public ConcurrentList<SavedScore[]?> ActivePlayers { get; }

    // TODO: maybe cache this?
    public string GetColor(int index, string id)
    {
        const string grey = "#808080";
        const string red = "red";
        if (index <= 0)
        {
            return string.Empty;
        }

        SavedScore[]? lastSet = ActivePlayers[index - 1];
        if (lastSet?.All(n => n.Id != id) ?? false)
        {
            return grey;
        }

        SavedScore[]? currentSet = ActivePlayers[index];
        if (currentSet != null)
        {
            return currentSet.Any(n => n.Id == id) ? string.Empty : red;
        }

        return string.Empty;
    }

    public bool IsEliminated(int index, string id)
    {
        if (index <= 0)
        {
            return false;
        }

        SavedScore[]? lastSet = ActivePlayers[index - 1];
        return lastSet?.All(n => n.Id != id) ?? false;
    }

    public void OnBackupsLoaded(IReadOnlyList<Backup> backups)
    {
        for (int i = 0; i < backups.Count; i++)
        {
            Backup backup = backups[i];
            if (backup.ActivePlayers == null)
            {
                return;
            }

            ActivePlayers[i] =
                backup.Scores.Where(n => backup.ActivePlayers.Contains(n.Id)).OrderBy(n => n.Score).ToArray();
        }
    }

    public void SubmitScores(int index, IReadOnlyList<SavedScore> scores)
    {
        if (index == 0)
        {
            ActivePlayers[index] = scores.ToArray();
            int playerCount = scores.Count;

            if (playerCount > 0)
            {
                ////_log.LogInformation("{PlayerCount} players qualified, glhf!", playerCount);
                SendBroadcast(RICH_COLOR + "{PlayerCount} players qualified, glhf!", playerCount);
            }
            else
            {
                SendLog(LogLevel.Warning, "No player scores submitted, cannot run tournament format");
            }
        }
        else
        {
            SavedScore[]? prevScores = ActivePlayers[index - 1];
            if (prevScores == null)
            {
                SendLog(LogLevel.Warning, "Scores for previous map not found");
                return;
            }

            SavedScore[] activeScores = scores.IntersectBy(prevScores.Select(n => n.Id), n => n.Id).ToArray();
            int playerCount = activeScores.Length;

            if (playerCount <= 0)
            {
                ActivePlayers[index] = [];
                SendLog(LogLevel.Warning, "No active player scores submitted");
                return;
            }

            int targetPlayerCount = playerCount switch
            {
                > 20 => 20,
                > 12 => 8,
                >= 4 => 4,
                _ => 3
            };

            if (index <= _mapCount - 3)
            {
                int places;
                if (playerCount <= targetPlayerCount)
                {
                    places = playerCount;
                }
                else
                {
                    int placesAway = _mapCount - 2 - index;
                    float cutoff = (float)Math.Pow((float)targetPlayerCount / playerCount, 1f / placesAway);
                    if (index < _mapCount - 3)
                    {
                        float safetyCurve = (1 - ((float)index / _mapCount)) * 0.8f;
                        cutoff = Math.Max(safetyCurve, cutoff);
                    }

                    places = (int)Math.Ceiling(playerCount * Math.Min(cutoff, 1));
                }

                SavedScore[] currPlayers = GetTopScores(activeScores, places);
                int playersKept = currPlayers.Length;
                ActivePlayers[index] = currPlayers;
                LogElimination(prevScores, currPlayers, playersKept);
                SendLog(
                    LogLevel.Debug,
                    "Elimination Occurred [map index: {Index}, playerCount: {PlayerCount}, playersKept: {PlayersKept}, targetPlayerCount: {TargetPlayerCount}]",
                    index,
                    playerCount,
                    playersKept,
                    targetPlayerCount);
            }
            else if (index == _mapCount - 2)
            {
                targetPlayerCount = (int)Math.Ceiling(targetPlayerCount / 2f);

                int places = Math.Min(playerCount, targetPlayerCount);
                SavedScore[] currPlayers = GetTopScores(activeScores, places);
                int playersKept = currPlayers.Length;
                ActivePlayers[index] = currPlayers;
                LogElimination(prevScores, currPlayers, playersKept);

                // TODO: remove these debug messages
                SendLog(
                    LogLevel.Debug,
                    "Elimination Occurred [map index: {Index}, playerCount: {PlayerCount}, playersKept: {PlayersKept}, targetPlayerCount: {TargetPlayerCount}]",
                    index,
                    playerCount,
                    playersKept,
                    targetPlayerCount);
            }
            else
            {
                SavedScore[] currPlayers = GetTopScores(activeScores, 1);
                ActivePlayers[index] = currPlayers;
                string[] winners = currPlayers.Select(n => n.Username).ToArray();
                string winnerNames = winners.Length switch
                {
                    2 => $"{winners[0]} and {winners[1]} won",
                    > 1 => string.Join(", ", winners, 0, winners.Length - 1) +
                           ", and " +
                           winners.Last() +
                           " won",
                    _ => winners[0] + " wins"
                };
                ////_log.LogInformation("{Winner} won!", winner.Username);
                SendBroadcast(RICH_COLOR + "{Winner}!", winnerNames);
                SendLog(
                    LogLevel.Debug,
                    "Elimination Occurred [map index: {Index}, playerCount: {PlayerCount}",
                    index,
                    playerCount);
            }
        }

        return;

        SavedScore[] GetTopScores(SavedScore[] activeScores, int places)
        {
            SavedScore[] currPlayers = activeScores[..places];

            // check for ties
            List<SavedScore>? tieScores = null;
            SavedScore lastPlace = currPlayers[^1];
            for (int i = places; i < activeScores.Length; i++)
            {
                SavedScore score = activeScores[i];
                if (score.Score < lastPlace.Score)
                {
                    break;
                }

                tieScores ??= [];
                tieScores.Add(score);
            }

            if (tieScores == null)
            {
                return currPlayers;
            }

            SavedScore[] result = new SavedScore[currPlayers.Length + tieScores.Count];
            currPlayers.CopyTo(result, 0);
            tieScores.CopyTo(result, currPlayers.Length);
            return result;

        }

        void LogElimination(SavedScore[] prevScores, IEnumerable<SavedScore> currPlayers, int playersKept)
        {
            int playersEliminated = prevScores.Length - playersKept;
            SavedScore[] eliminatedScores = prevScores.ExceptBy(currPlayers.Select(n => n.Id), n => n.Id).ToArray();
            string keptString = $"{playersKept} player" + (playersKept != 1 ? "s remain!" : " remains!");
            switch (playersEliminated)
            {
                case <= 0:
                    ////_log.LogInformation("Nobody was eliminated, {PlayersKept} remain!", playersKept);
                    SendBroadcast(RICH_COLOR + "Nobody was eliminated, {PlayersKept}", keptString);
                    break;

                case <= 5:
                {
                    string[] eliminated = eliminatedScores
                        .Select(n => n.Username)
                        .ToArray();
                    string eliminatedNames = eliminated.Length switch
                    {
                        2 => $"{eliminated[0]} and {eliminated[1]} were",
                        > 1 => string.Join(", ", eliminated, 0, eliminated.Length - 1) +
                               ", and " +
                               eliminated.Last() +
                               " were",
                        _ => eliminated[0] + " was"
                    };

                    ////_log.LogInformation("{EliminatedNames} were eliminated, {PlayersKept} remain!", eliminatedNames, playersKept);
                    SendBroadcast(
                        RICH_COLOR + "{EliminatedNames} eliminated, {PlayersKept}",
                        eliminatedNames,
                        keptString);
                }
                    break;

                default:
                    ////_log.LogInformation("{PlayersEliminated} players were eliminated, {PlayersKept} remain!", playersEliminated, playersKept);
                    SendBroadcast(
                        RICH_COLOR + "{PlayersEliminated} players were eliminated, {PlayersKept}",
                        playersEliminated,
                        keptString);
                    break;
            }

            string[] eliminatedIds = eliminatedScores.Select(n => n.Id).ToArray();
            foreach (IClient client in _listenerService.Clients.Values.Where(n => eliminatedIds.Contains(n.Id)))
            {
                client.SendPriorityServerMessage("<color=red>You were eliminated!");
            }
        }
    }

    private void SendBroadcast([StructuredMessageTemplate] string message, params object[] args)
    {
        Broadcast?.Invoke(message, args);
    }

    private void SendLog(LogLevel logLevel, [StructuredMessageTemplate] string message, params object[] args)
    {
        Log?.Invoke(logLevel, message, args);
    }
}
