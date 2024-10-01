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
    private MenuPrefabManager _menuPrefabManager = null!;
    private NetworkManager _networkManager = null!;
    private EventLobbySongInfoViewController _songInfoViewController = null!;

    private CancellationTokenSource? _startCancel;
    private TimeSyncManager _timeSyncManager = null!;

    public event Action? IntroStarted;

    public override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

        // ReSharper disable once InvertIf
        if (addedToHierarchy)
        {
            if (_config.LastEvent.SeenIntro)
            {
                switch (_networkManager.Status.Stage)
                {
                    case PlayStatus:
                        IntroStarted?.Invoke();
                        break;

                    case IntroStatus introStatus:
                        _ = DelayedIntro(introStatus.StartTime);
                        break;
                }
            }

            _networkManager.IntroStartTimeUpdated += OnIntroStartTimeUpdated;
            _chatViewController.IntroStarted += OnIntroStarted;
            SetChildViewControllers(
            [
                _chatViewController,
                _songInfoViewController
            ]);
        }
    }

    public override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);

        // ReSharper disable once InvertIf
        if (removedFromHierarchy)
        {
            _networkManager.IntroStartTimeUpdated -= OnIntroStartTimeUpdated;
            _chatViewController.IntroStarted -= OnIntroStarted;
            ClearChildViewControllers();
        }
    }

    [UsedImplicitly]
    [Inject]
    private void Construct(
        Config config,
        NetworkManager networkManager,
        TimeSyncManager timeSyncManager,
        MenuPrefabManager menuPrefabManager,
        EventLobbyChatViewController chatViewController,
        EventLobbySongInfoViewController songInfoViewController)
    {
        _config = config;
        _networkManager = networkManager;
        _timeSyncManager = timeSyncManager;
        _menuPrefabManager = menuPrefabManager;
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

    private void OnIntroStarted()
    {
        _startCancel?.Cancel();
        IntroStarted?.Invoke();
    }

    private void OnIntroStartTimeUpdated(float startTime)
    {
        _ = DelayedIntro(startTime);
    }
}
