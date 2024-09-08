using Microsoft.Extensions.Logging;
using Synapse.Networking.Models;
using Synapse.Server.Clients;
using Synapse.Server.Extras;
using Synapse.Server.Models;
using Synapse.Server.Services;

namespace Synapse.Server.Commands;

public class ScoresCommand(
    ILogger<ScoresCommand> log,
    ILeaderboardService leaderboardService,
    IMapService mapService,
    IEventService eventService,
    IListenerService listenerService,
    IBackupService backupService)
{
    [Command("scores", Permission.Coordinator)]
    public void Scores(IClient client, string arguments)
    {
        arguments.SplitCommand(out string subCommand, out string subArguments);
        switch (subCommand)
        {
            case "test":
                if (string.IsNullOrWhiteSpace(subArguments))
                {
                    leaderboardService.GenerateTestScores();
                    client.SendServerMessage("Generated test scores");
                }
                else
                {
                    client.SendServerMessage("Invalid message");
                }

                break;

            case "refresh":
            {
                int mapIndexInt;
                if (string.IsNullOrWhiteSpace(subArguments))
                {
                    mapIndexInt = mapService.Index;
                }
                else if (!int.TryParse(subArguments, out mapIndexInt) ||
                         mapIndexInt < 0 ||
                         mapIndexInt >= mapService.MapCount)
                {
                    client.SendServerMessage("Invalid map index");
                    return;
                }

                leaderboardService.BroadcastLeaderboard(mapIndexInt);
                client.SendServerMessage("Refreshed leaderboards for ({Index})", mapIndexInt);
            }

                break;

            case "remove":
            {
                int mapIndexInt;
                string flags = subArguments.GetFlags(out string extra);
                extra.SplitCommand(out string mapIndex, out string id);
                if (string.IsNullOrWhiteSpace(id))
                {
                    id = mapIndex;
                    mapIndexInt = mapService.Index;
                }
                else if (!int.TryParse(mapIndex, out mapIndexInt) ||
                         mapIndexInt < 0 ||
                         mapIndexInt >= mapService.MapCount)
                {
                    client.SendServerMessage("Invalid map index");
                    return;
                }

                if (leaderboardService
                    .AllScores[mapIndexInt]
                    .TryScanQuery(
                        client,
                        id,
                        flags.Contains('i') ? n => n.Id : n => n.Username,
                        out SavedScore score))
                {
                    client.LogAndSend(
                        log,
                        "Removed score [{Score}] from map [{Map}]",
                        score,
                        mapService.Maps[mapIndexInt].Name);
                    leaderboardService.RemoveScore(mapIndexInt, score);
                    if (listenerService.Clients.TryGetValue(score.Id, out IClient? target))
                    {
                        eventService.SendStatus(target);
                    }
                }
            }

                break;

            // TODO: add a "are you sure" prompt
            case "drop":
            {
                int mapIndexInt;
                if (string.IsNullOrWhiteSpace(subArguments))
                {
                    mapIndexInt = mapService.Index;
                }
                else if (!int.TryParse(subArguments, out mapIndexInt) ||
                         mapIndexInt < 0 ||
                         mapIndexInt >= mapService.MapCount)
                {
                    client.SendServerMessage("Invalid map index");
                    return;
                }

                int scoresCount = leaderboardService.AllScores[mapIndexInt].Count;
                leaderboardService.DropScores(mapIndexInt);
                client.LogAndSend(
                    log,
                    "Removed [{ScoreCount}] score(s) from map [{Map}]",
                    scoresCount,
                    mapService.Maps[mapIndexInt].Name);
                eventService.UpdateStatus(false);
            }

                break;

            case "resubmit":
            {
                if (string.IsNullOrWhiteSpace(subArguments) ||
                    !int.TryParse(subArguments, out int mapIndexInt) ||
                    mapIndexInt < 0 ||
                    mapIndexInt >= mapService.Index)
                {
                    client.SendServerMessage("Invalid map index");
                    return;
                }

                _ = ResubmitScores(client, mapIndexInt, mapService.Index);
            }

                break;

            case "list":
            {
                int mapIndexInt;
                string flags = subArguments.GetFlags(out string extra);
                if (string.IsNullOrWhiteSpace(extra))
                {
                    mapIndexInt = mapService.Index;
                }
                else if (!int.TryParse(extra, out mapIndexInt) ||
                         mapIndexInt < 0 ||
                         mapIndexInt >= mapService.MapCount)
                {
                    client.SendServerMessage("Invalid map index");
                    return;
                }

                string map = mapService.Maps[mapIndexInt].Name;
                IReadOnlyList<SavedScore> scores = leaderboardService.AllScores[mapIndexInt];
                string message = scores.Count switch
                {
                    > 0 => $"{map} ({mapIndexInt}) has {scores.Count} scores",
                    _ => $"No scores currently submitted for [{map}]"
                };
                client.SendServerMessage(message);

                if (flags.Contains('v'))
                {
                    int limit = flags.Contains('e') ? 100 : 20;
                    IEnumerable<SavedScore> chatters = leaderboardService.AllScores[mapIndexInt].Take(limit);
                    string scoresMessage = string.Join(", ", chatters.Select(n => n.ToString()));
                    if (scores.Count > limit)
                    {
                        scoresMessage += ", ...";
                    }

                    client.SendServerMessage(scoresMessage);
                }
            }

                break;

            case "backup":
                Backup(subArguments);

                break;

            default:
                client.SendServerMessage("Did not recognize scores subcommand [{Message}]", subCommand);
                break;
        }
    }

    private void Backup(string arguments)
    {
        arguments.SplitCommand(out string subCommand, out _);
        switch (subCommand)
        {
            case "reload":
                backupService.LoadBackups();

                break;

            default:
                log.LogWarning("Did not recognize command [{Message}]", subCommand);
                break;
        }
    }

    private async Task ResubmitScores(IClient client, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            await leaderboardService.SubmitTournamentScores(i);
        }

        int count = end - start;
        Map[] dest = new Map[count];
        Array.Copy(mapService.Maps.ToArray(), start, dest, 0, count);
        string affected = string.Join(", ", dest.Select(n => n.Name));
        client.LogAndSend(log, "Resubmitted scores for [{Map}]", affected);
    }
}
