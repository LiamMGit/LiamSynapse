using Microsoft.Extensions.Logging;
using Synapse.Server.Extras;
using Synapse.Server.Models;

namespace Synapse.Server.TournamentFormats;

public interface ITournamentFormat
{
    public event Action<LogLevel, string, object[]>? Log;

    public event Action<string, object[]> Broadcast;

    public ConcurrentList<SavedScore[]?> ActivePlayers { get; }

    public string GetColor(int index, string id);

    public bool IsEliminated(int index, string id);

    public void OnBackupsLoaded(IReadOnlyList<Backup> backups);

    public void SubmitScores(int index, IReadOnlyList<SavedScore> scores);
}
