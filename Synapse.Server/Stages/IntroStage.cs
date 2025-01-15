using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Synapse.Networking.Models;
using Synapse.Server.Clients;
using Synapse.Server.Extras;
using Synapse.Server.Services;

namespace Synapse.Server.Stages;

public enum IntroState
{
    Waiting,
    Playing
}

public class IntroStage : Stage
{
    private readonly TimeSpan _duration;
    private readonly TimeSpan _intermission;
    private readonly string _url;
    private readonly IListingService _listingService;
    private readonly ILogger<IntroStage> _log;
    private readonly string _motd;
    private readonly IClient _serverClient;
    private readonly ITimeService _timeService;

    private CancellationTokenSource _cancellationTokenSource = new();
    private float _endTime = float.MaxValue;
    private float _startTime = float.MaxValue;

    private IntroState _state;

    public IntroStage(
        ILogger<IntroStage> log,
        IConfiguration config,
        ITimeService timeService,
        IClient serverClient,
        IListingService listingService)
    {
        _log = log;
        _timeService = timeService;
        _serverClient = serverClient;
        _listingService = listingService;
        IConfigurationSection introSection = config.GetRequiredSection("Event").GetRequiredSection("Intro");
        _motd = introSection.GetRequiredSection("Motd").Get<string>() ?? string.Empty;
        _intermission = introSection.GetRequiredSection("Intermission").Get<TimeSpan>();
        _duration = introSection.GetRequiredSection("Duration").Get<TimeSpan>();
        _url = introSection.GetRequiredSection("Url").Get<string>() ?? string.Empty;
    }

    public void AutoPlay(IClient client)
    {
        _ = Play(_duration, client);
    }

    public void AutoStart(IClient client)
    {
        _ = Start(_intermission, client);
    }

    public override void Enter()
    {
        _cancellationTokenSource.Cancel();
        Listing? listing = _listingService.Listing;
        if (listing == null)
        {
            _log.LogError("Unable to auto start event, failed to find listing");
            return;
        }

        TimeSpan diff = listing.Time - DateTime.Now;
        if (diff.Ticks > 0)
        {
            _ = AutoStartEvent(diff);
        }
        else
        {
            _log.LogError("Unable to auto start event, start time has already passed");
        }
    }

    public override Status GetStatus()
    {
        return new Status
        {
            Motd = _motd,
            Stage = new IntroStatus
            {
                StartTime = _startTime,
                Url = _url
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
            "Playing intro for [{Duration}] seconds",
            timer.TotalSeconds);
        _state = IntroState.Playing;
        if (_startTime > _timeService.Time)
        {
            _startTime = _timeService.Time;
            UpdateStatus();
        }

        await Task.Delay(timer, (_cancellationTokenSource = new CancellationTokenSource()).Token);
        Exit();
    }

    public override void PrintStatus(IClient client)
    {
        switch (_state)
        {
            case IntroState.Waiting:
                client.SendServerMessage("Waiting to play intro in {Time} seconds", _startTime - _timeService.Time);
                break;

            case IntroState.Playing:
                client.SendServerMessage("Playing intro for {Time} seconds", _endTime - _timeService.Time);
                break;
        }
    }

    public async Task Start(TimeSpan timer, IClient client)
    {
        if (!Active)
        {
            await client.SendServerMessage("Cannot start, unstaged");
            return;
        }

        if (_state != IntroState.Waiting)
        {
            await client.SendServerMessage("Cannot start intro, already playing");
            return;
        }

        await _cancellationTokenSource.CancelAsync();

        _startTime = _timeService.Time + (float)timer.TotalSeconds;
        UpdateStatus();
        client.LogAndSend(_log, "Starting intro in {Time} seconds", timer.TotalSeconds);

        _state = IntroState.Waiting;
        await Task.Delay(timer, (_cancellationTokenSource = new CancellationTokenSource()).Token);
        AutoPlay(_serverClient);
    }

    public void Stop(IClient client)
    {
        _startTime = float.MaxValue;
        UpdateStatus();
        client.LogAndSend(_log, "Stopped intro");
    }

    private async Task AutoStartEvent(TimeSpan timer)
    {
        _log.LogInformation("Starting event in {Time} seconds", timer.TotalSeconds);
        await Task.Delay(timer, (_cancellationTokenSource = new CancellationTokenSource()).Token);
        AutoStart(_serverClient);
    }
}
