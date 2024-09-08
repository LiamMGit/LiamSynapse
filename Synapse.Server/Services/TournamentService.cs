using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Synapse.Server.Models;
using Synapse.Server.TournamentFormats;

namespace Synapse.Server.Services;

public interface ITournamentService
{
    public string GetColor(int index, string id);

    public bool IsEliminated(int index, string id);

    public Task SubmitScores(int index, IReadOnlyList<SavedScore> scores);
}

public class TournamentService : ITournamentService
{
    private readonly IBackupService _backupService;
    private readonly ITournamentFormat _format;
    private readonly ILogger<TournamentService> _log;

    public TournamentService(
        ILogger<TournamentService> log,
        IFormatFactory formatFactory,
        IBackupService backupService)
    {
        _log = log;
        _backupService = backupService;
        _format = formatFactory.Create();
        backupService.BackupsLoaded += OnBackupsLoaded;
    }

    public string GetColor(int index, string id)
    {
        return _format.GetColor(index, id);
    }

    public bool IsEliminated(int index, string id)
    {
        return _format.IsEliminated(index, id);
    }

    public async Task SubmitScores(int index, IReadOnlyList<SavedScore> scores)
    {
        try
        {
            await Task.Run(() => _format.SubmitScores(index, scores));
        }
        catch (Exception e)
        {
            _log.LogCritical(e, "Exception while submitting scores to format");
        }

        await _backupService.SaveScores(
            index,
            scores,
            _format.ActivePlayers[index]?.Select(n => n.Id).ToImmutableList() ?? ImmutableList<string>.Empty);
    }

    private void OnBackupsLoaded(IReadOnlyList<Backup> backups)
    {
        _format.OnBackupsLoaded(backups);
    }
}
