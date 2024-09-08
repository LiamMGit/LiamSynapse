using Microsoft.Extensions.Logging;
using Synapse.Networking.Models;
using Synapse.Server.Clients;
using Synapse.Server.Extras;
using Synapse.Server.Models;
using Synapse.Server.Services;

namespace Synapse.Server.Stages;

public enum PlayState
{
    Waiting,
    Playing
}

public class PlayStage : Stage
{
    private readonly TaskCompletionSource _backupsLoaded = new();
    private readonly ILeaderboardService _leaderboardService;
    private readonly ILogger<PlayStage> _log;
    private readonly IMapService _mapService;
    private readonly IClient _serverClient;
    private readonly ITimeService _timeService;

    private CancellationTokenSource _cancellationTokenSource = new();
    private float _endTime = float.MaxValue;
    private float _startTime = float.MaxValue;

    private PlayState _state = PlayState.Waiting;

    public PlayStage(
        ILogger<PlayStage> log,
        ITimeService timeService,
        IMapService mapService,
        ILeaderboardService leaderboardService,
        IClient serverClient,
        IBackupService backupService)
    {
        _log = log;
        _timeService = timeService;
        _mapService = mapService;
        _leaderboardService = leaderboardService;
        _serverClient = serverClient;
        backupService.BackupsLoaded += OnBackupsLoaded;
    }

    public override Status AdjustStatus(Status status, IClient client)
    {
        PlayStatus playStatus = (PlayStatus)status.Stage;
        // ReSharper disable once InvertIf
        if (_leaderboardService.TryGetScore(playStatus.Index, client, out SavedScore savedScore))
        {
            PlayerScore score = new()
            {
                Score = savedScore.Score,
                Percentage = savedScore.Percentage
            };

            status = status with { Stage = playStatus with { PlayerScore = score } };
        }

        return status;
    }

    public void AutoPlay(IClient client)
    {
        _ = Play(_mapService.CurrentMap.Duration, client);
    }

    public void AutoStart(IClient client, bool resetMotd = false)
    {
        _ = Start(_mapService.CurrentMap.Intermission, client, resetMotd);
    }

    public override void Enter()
    {
        SetIndex(_mapService.Index, _serverClient, false, true);
    }

    public override Status GetStatus()
    {
        ConfigMap configMap = _mapService.CurrentMap;
        Map map = new()
        {
            Name = configMap.Name,
            Characteristic = configMap.Characteristic,
            Difficulty = configMap.Difficulty,
            AltCoverUrl = configMap.AltCoverUrl,
            Ruleset = configMap.Ruleset,
            Downloads = configMap.Downloads
        };

        return new Status
        {
            Motd = configMap.Motd,
            Stage = new PlayStatus
            {
                Index = _mapService.Index,
                StartTime = _startTime,
                Map = map
            }
        };
    }

    public async Task Play(TimeSpan timer, IClient client)
    {
        if (!Active)
        {
            await client.SendServerMessage("Cannot play, unstaged");
            return;
        }

        await _cancellationTokenSource.CancelAsync();

        _endTime = _timeService.Time + (float)timer.TotalSeconds;
        client.LogAndSend(
            _log,
            "Playing [{NewMapName}] for [{Duration}] seconds",
            _mapService.CurrentMap.Name,
            timer.TotalSeconds);

        _state = PlayState.Playing;
        if (_startTime > _timeService.Time)
        {
            _startTime = _timeService.Time;
            UpdateStatus();
        }

        await Task.Delay(timer, (_cancellationTokenSource = new CancellationTokenSource()).Token);
        SetIndex(_mapService.Index + 1, _serverClient, true, true);
    }

    public override Task Prepare()
    {
        return _backupsLoaded.Task;
    }

    public override void PrintStatus(IClient client)
    {
        switch (_state)
        {
            case PlayState.Playing:
                client.SendServerMessage(
                    "Currently playing map {Index} [{Name}]",
                    _mapService.Index + 1,
                    _mapService.CurrentMap.Name);
                client.SendServerMessage(
                    "Map ends in {Time} seconds",
                    _endTime - _timeService.Time);
                break;

            case PlayState.Waiting:
                client.SendServerMessage(
                    "Currently waiting to start map {Index} [{Name}]",
                    _mapService.Index + 1,
                    _mapService.CurrentMap.Name);
                client.SendServerMessage(
                    "Map starts in {Time} seconds",
                    _startTime - _timeService.Time);
                break;
        }
    }

    public void SetIndex(int index, IClient client, bool submit, bool autostart)
    {
        _cancellationTokenSource.Cancel();

        if (submit)
        {
            _ = _leaderboardService.SubmitTournamentScores(_mapService.Index);
        }

        if (index >= _mapService.MapCount)
        {
            _startTime = float.MaxValue;
            Exit();
            return;
        }

        _state = PlayState.Waiting;
        _mapService.Index = index;
        if (autostart)
        {
            AutoStart(client, true);
        }
        else
        {
            _startTime = float.MaxValue;
            UpdateStatus(true);
        }
    }

    public async Task Start(TimeSpan timer, IClient client, bool resetMotd = false)
    {
        if (!Active)
        {
            await client.SendServerMessage("Cannot start, unstaged");
            return;
        }

        if (_state != PlayState.Waiting)
        {
            await client.SendServerMessage("Cannot start map, already playing");
            return;
        }

        await _cancellationTokenSource.CancelAsync();

        _startTime = _timeService.Time + (float)timer.TotalSeconds;
        UpdateStatus(resetMotd);
        string mapName = _mapService.CurrentMap.Name;
        client.LogAndSend(
            _log,
            "Starting [{MapName}] ({Index}) in {Time} seconds",
            mapName,
            _mapService.Index,
            timer.TotalSeconds);

        await Task.Delay(timer, (_cancellationTokenSource = new CancellationTokenSource()).Token);
        AutoPlay(_serverClient);
    }

    public void Stop(IClient client)
    {
        SetIndex(_mapService.Index, client, false, false);
        client.LogAndSend(_log, "Stopped {Map}", _mapService.CurrentMap.Name);
    }

    private void OnBackupsLoaded(IReadOnlyList<Backup> _)
    {
        _backupsLoaded.SetResult();
    }

    /*private async Task StartCountdown(CancellationToken token)
    {
        ////_listenerService.BroadcastServerMessage("Next map starting in");

        ////int lastCount = int.MaxValue;

        float diff = _startTime - _timeService.Time;
        _listenerService.BroadcastServerMessage("Next map starting in {Seconds}", (int)diff);
        while (diff > 0)
        {
            diff = StartTime - DateTime.UtcNow;
            TimeSpan alteredDiff = diff + TimeSpan.FromSeconds(1);
            string diffString = (int)alteredDiff.TotalMinutes > 0
                ? $"{alteredDiff.Minutes}:{alteredDiff.Seconds:D2}"
                : $"{alteredDiff.Seconds}";
            if (diff.TotalSeconds < lastCount)
            {
                lastCount = diff.TotalSeconds switch
                {
                    >= 300 => (int)Math.Floor(diff.TotalSeconds / 300.0) * 300,
                    >= 60 => (int)Math.Floor(diff.TotalSeconds / 60.0) * 60,
                    > 30 => (int)Math.Floor(diff.TotalSeconds / 30.0) * 30,
                    > 10 => (int)Math.Floor(diff.TotalSeconds / 10.0) * 10,
                    <= 10 => (int)Math.Floor(diff.TotalSeconds),
                    _ => lastCount
                };

                if (diffString != "0")
                {
                    ////_log.LogInformation("{Count}", diffString);
                    _listenerService.BroadcastServerMessage("{Count}", diffString);
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), token);
        }

        AutoPlay(_serverClient);
    }*/
}
