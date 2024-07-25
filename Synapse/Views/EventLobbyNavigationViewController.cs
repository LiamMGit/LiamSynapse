using System;
using System.Threading;
using System.Threading.Tasks;
using HMUI;
using JetBrains.Annotations;
using Synapse.Extras;
using Synapse.Managers;
using Synapse.Models;
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

    public event Action? StartIntro;

    public event Action? StartLevel;

    public override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

        // ReSharper disable once InvertIf
        if (addedToHierarchy)
        {
            switch (_networkManager.Status.Stage)
            {
                case PlayStatus playStatus:
                    if (_config.LastSeenIntro != _menuPrefabManager.LastHash)
                    {
                        StartIntro?.Invoke();
                    }
                    else
                    {
                        _ = DelayedStart(playStatus.StartTime);
                    }

                    break;

                case IntroStatus introStatus:
                    if (_config.LastSeenIntro != _menuPrefabManager.LastHash)
                    {
                        _ = DelayedIntro(introStatus.StartTime);
                    }

                    break;
            }

            _networkManager.StartTimeUpdated += OnStartTimeUpdated;
            _networkManager.IntroStartTimeUpdated += OnIntroStartTimeUpdated;
            _chatViewController.StartIntro += OnStartIntro;
            _songInfoViewController.StartLevel += OnStartLevel;
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
            _networkManager.StartTimeUpdated -= OnStartTimeUpdated;
            _networkManager.IntroStartTimeUpdated -= OnIntroStartTimeUpdated;
            _chatViewController.StartIntro -= OnStartIntro;
            _songInfoViewController.StartLevel -= OnStartLevel;
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

        StartIntro?.Invoke();
    }

    private async Task DelayedStart(float startTime)
    {
        if (_networkManager.Status.Stage is PlayStatus { PlayerScore: not null } playStatus &&
            !(playStatus.Map.Ruleset?.AllowResubmission ?? false))
        {
            return;
        }

        _startCancel?.Cancel();
        CancellationToken token = (_startCancel = new CancellationTokenSource()).Token;
        float diff = startTime - _timeSyncManager.SyncTime;
        if (diff > 0)
        {
            await Task.Delay(diff.ToTimeSpan(), token);
        }

        StartLevel?.Invoke();
    }

    private void OnIntroStartTimeUpdated(float startTime)
    {
        _ = DelayedIntro(startTime);
    }

    private void OnStartIntro()
    {
        _startCancel?.Cancel();
        StartIntro?.Invoke();
    }

    private void OnStartLevel()
    {
        StartLevel?.Invoke();
    }

    private void OnStartTimeUpdated(float startTime)
    {
        _ = DelayedStart(startTime);
    }
}
