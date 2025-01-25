using Microsoft.Extensions.Logging;
using Synapse.Server.Extras;
using Synapse.Server.Models;
using Synapse.Server.Services;

namespace Synapse.Server.TournamentFormats;

public class NoneFormat : ITournamentFormat
{
    public event Action<LogLevel, string, object[]>? Log;

    public event Action<string, object[]>? Broadcast;

    public NoneFormat(IMapService mapService)
    {
        int mapCount = mapService.Maps.Count;
        ActivePlayers = ConcurrentList<SavedScore[]?>.Prefilled(mapCount, _ => null);
    }

    public ConcurrentList<SavedScore[]?> ActivePlayers { get; }


    public string GetColor(int index, string id)
    {
        return string.Empty;
    }

    public bool IsEliminated(int index, string id)
    {
        return false;
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
        ActivePlayers[index] = scores.ToArray();
    }
}
