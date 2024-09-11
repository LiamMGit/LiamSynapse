using System.Text.Json;
using Microsoft.Extensions.Logging;
using Synapse.Server.Extras;
using Synapse.Server.Models;

namespace Synapse.Server.Services;

public interface IBackupService
{
    public event Action<IReadOnlyList<Backup>>? BackupsLoaded;

    public Task LoadBackups();

    public Task SaveScores(int index, IReadOnlyList<SavedScore> scores, IReadOnlyList<string>? activePlayers);
}

public class BackupService : IBackupService
{
    private readonly string _directory;
    private readonly ILogger<BackupService> _log;
    private readonly IMapService _mapService;

    private ConcurrentList<Backup>? _backups;

    private bool _saving;

    public BackupService(
        ILogger<BackupService> log,
        IMapService mapService,
        IDirectoryService directoryService)
    {
        _log = log;
        _mapService = mapService;
        _directory = Directory.CreateDirectory(Path.Combine(directoryService.EventDirectory, "backups")).FullName;
        _ = LoadBackups();
    }

    public event Action<IReadOnlyList<Backup>>? BackupsLoaded
    {
        add
        {
            if (_backups != null)
            {
                try
                {
                    value?.Invoke(_backups);
                }
                catch (Exception e)
                {
                    _log.LogCritical(e, "Exception while loading backup");
                }
            }

            InternalBackupsLoaded += value;
        }

        remove => InternalBackupsLoaded -= value;
    }

    private event Action<IReadOnlyList<Backup>>? InternalBackupsLoaded;

    public async Task LoadBackups()
    {
        try
        {
            string[] files = Directory.EnumerateFiles(_directory, "scores??.json").ToArray();

            List<Backup> backups = [];
            for (int i = 0; i < _mapService.MapCount; i++)
            {
                string? file = files.FirstOrDefault(n => n.EndsWith($"scores{i:D2}.json"));
                if (file == null)
                {
                    break;
                }

                using StreamReader reader = new(file);
                Backup backup = await JsonSerializer.DeserializeAsync<Backup>(reader.BaseStream, JsonUtils.Settings);
                _log.LogInformation("Loaded score backup [{File}]", Path.GetFileName(file));
                backups.Add(backup);
            }

            _backups = new ConcurrentList<Backup>(backups);
            _mapService.Index = backups.Count(n => n.ActivePlayers != null);
            InternalBackupsLoaded?.Invoke(_backups);
        }
        catch (Exception e)
        {
            _log.LogCritical(e, "Exception while loading backup");
        }
    }

    public async Task SaveScores(int index, IReadOnlyList<SavedScore> scores, IReadOnlyList<string>? activePlayers)
    {
        try
        {
            if (_backups == null || _saving)
            {
                _log.LogError("Could not save backup");
                return;
            }

            _saving = true;
            bool backupExists = _backups.Count > index;
            string path = Path.Combine(_directory, $"scores{index:D2}.json");
            Backup backup = new()
            {
                Index = index,
                Scores = scores.ToArray(),
                ActivePlayers = activePlayers?.ToArray() ?? (backupExists ? _backups[index].ActivePlayers : null)
            };

            if (backupExists)
            {
                _backups[index] = backup;
            }

            await using StreamWriter output = new(path);
            await JsonSerializer.SerializeAsync(output.BaseStream, backup, JsonUtils.PrettySettings);
            _saving = false;
            ////_log.LogInformation("Created backup {File}", path);
        }
        catch (Exception e)
        {
            _log.LogError(e, "Exception while saving backup");
        }
    }
}
