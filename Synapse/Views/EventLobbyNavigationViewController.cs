using System;
using System.Threading;
using System.Threading.Tasks;
using HMUI;
using JetBrains.Annotations;
using Synapse.Extras;
using Synapse.Managers;
using Synapse.Networking.Models;
using Zenject;

namespace Synapse.Views;

internal class EventLobbyNavigationViewController : NavigationController
{
    private EventLobbyChatViewController _chatViewController = null!;
    private Config _config = null!;
    private NetworkManager _networkManager = null!;
    private EventLobbySongInfoViewController _songInfoViewController = null!;

    private CancellationTokenSource? _startCancel;
    private TimeSyncManager _timeSyncManager = null!;

    public event Action? IntroStarted;

    public event Action? OutroStarted;

    public override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

        // ReSharper disable once InvertIf
        if (addedToHierarchy)
        {
            IStageStatus stage = _networkManager.Status.Stage;

            switch (stage)
            {
                case PlayStatus:
                    if (!_config.LastEvent.SeenIntro)
                    {
                        IntroStarted?.Invoke();
                    }

                    break;

                case IntroStatus introStatus:
                    _ = DelayedIntro(introStatus.StartTime);
                    break;

                case FinishStatus:
                    if (!_config.LastEvent.SeenOutro)
                    {
                        OutroStarted?.Invoke();
                    }

                    break;
            }

            _networkManager.IntroStartTimeUpdated += OnIntroStartTimeUpdated;
            _networkManager.StageUpdated += OnStageUpdated;
            _chatViewController.IntroStarted += OnIntroStarted;
            _chatViewController.OutroStarted += OnOutroStarted;
            SetChildViewControllers(
                _chatViewController,
                _songInfoViewController);
        }
    }

    public override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);

        // ReSharper disable once InvertIf
        if (removedFromHierarchy)
        {
            _startCancel?.Cancel();
            _networkManager.IntroStartTimeUpdated -= OnIntroStartTimeUpdated;
            _networkManager.StageUpdated -= OnStageUpdated;
            _chatViewController.IntroStarted -= OnIntroStarted;
            _chatViewController.OutroStarted -= OnOutroStarted;
            ClearChildViewControllers();
        }
    }

    [UsedImplicitly]
    [Inject]
    private void Construct(
        Config config,
        NetworkManager networkManager,
        TimeSyncManager timeSyncManager,
        EventLobbyChatViewController chatViewController,
        EventLobbySongInfoViewController songInfoViewController)
    {
        _config = config;
        _networkManager = networkManager;
        _timeSyncManager = timeSyncManager;
        _chatViewController = chatViewController;
        _songInfoViewController = songInfoViewController;
    }

    private async Task DelayedIntro(float startTime)
    {
        _startCancel?.Cancel();
        CancellationToken token = (_startCancel = new CancellationTokenSource()).Token;
        float diff = startTime - _timeSyncManager.SyncTime;
        if (diff > 0)
        {
            await Task.Delay(diff.ToTimeSpan(), token);
        }

        IntroStarted?.Invoke();
    }

    private void OnStageUpdated(IStageStatus stage)
    {
        _startCancel?.Cancel();
        if (stage is FinishStatus)
        {
            OutroStarted?.Invoke();
        }
    }

    private void OnIntroStarted()
    {
        _startCancel?.Cancel();
        IntroStarted?.Invoke();
    }

    private void OnOutroStarted()
    {
        _startCancel?.Cancel();
        OutroStarted?.Invoke();
    }

    private void OnIntroStartTimeUpdated(float startTime)
    {
        _ = DelayedIntro(startTime);
    }
}
