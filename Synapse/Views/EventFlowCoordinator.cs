using System;
using System.Collections.Generic;
using System.Linq;
using HMUI;
using IPA.Utilities.Async;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SiraUtil.Logging;
using Synapse.Extras;
using Synapse.HarmonyPatches;
using Synapse.Managers;
using Synapse.Models;
using Synapse.Networking.Models;
using UnityEngine;
using Zenject;

namespace Synapse.Views;

internal class EventFlowCoordinator : FlowCoordinator
{
    private Config _config = null!;
    private CountdownManager _countdownManager = null!;
    private bool _dirtyListing;
    private GameplaySetupViewController _gameplaySetupViewController = null!;
    private EventIntroViewController _introViewController = null!;
    private EventLeaderboardViewController _leaderboardViewController = null!;
    private LevelStartManager _levelStartManager = null!;
    private Listing? _listing;
    private EventLoadingViewController _loadingViewController = null!;
    private EventLobbyNavigationViewController _lobbyNavigationViewController = null!;
    private SiraLog _log = null!;
    private MapDownloadingManager _mapDownloadingManager = null!;
    private MenuPrefabManager _menuPrefabManager = null!;
    private EventModsDownloadingViewController _modsDownloadingViewController = null!;
    private EventModsViewController _modsViewController = null!;
    private NetworkManager _networkManager = null!;
    private ResultsViewController _resultsViewController = null!;
    private SimpleDialogPromptViewController _simpleDialogPromptViewController = null!;

    private Queue<Action> _transitionFinished = new();

    internal event Action<EventFlowCoordinator>? Finished;

    private event Action TransitionFinished
    {
        add
        {
            if (!_isInTransition)
            {
                UnityMainThreadTaskScheduler.Factory.StartNew(value);
                return;
            }

            _transitionFinished.Enqueue(value);
        }

        remove => _transitionFinished = new Queue<Action>(_transitionFinished.Where(n => n != value));
    }

    // screw it
    public static bool IsActive { get; private set; }

    public override void BackButtonWasPressed(ViewController topView)
    {
        if (!topView.isInTransition)
        {
            Finished?.Invoke(this);
        }
    }

    public override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        // TODO: light color presets
        // ReSharper disable once InvertIf
        if (addedToHierarchy)
        {
            IsActive = true;
            SetTitle(_listing?.Title ?? "N/A");
            showBackButton = true;

            _gameplaySetupViewController.Setup(
                false,
                false,
                true,
                false,
                PlayerSettingsPanelController.PlayerSettingsPanelLayout.Singleplayer);

            if (_listing == null)
            {
                SetTitle(null);
                showBackButton = false;
                _simpleDialogPromptViewController.Init(
                    "Error",
                    "Listing failed to load",
                    "Ok",
                    _ => { Finished?.Invoke(this); });
                ProvideInitialViewControllers(_simpleDialogPromptViewController);
            }
            else if (_modsDownloadingViewController.DownloadFinished)
            {
                ProvideInitialViewControllers(_modsDownloadingViewController);
            }
            else
            {
                if (_dirtyListing)
                {
                    if (!_listing.GameVersion.MatchesGameVersion())
                    {
                        SetTitle(null);
                        showBackButton = false;
                        _simpleDialogPromptViewController.Init(
                            "Error",
                            $"{_listing.Title} only allows versions {_listing.GameVersion.Replace(",", ", ")}",
                            "Ok",
                            _ => { Finished?.Invoke(this); });
                        ProvideInitialViewControllers(_simpleDialogPromptViewController);
                        return;
                    }

                    RequiredMods? versionMods =
                        _listing.RequiredMods.FirstOrDefault(n => n.GameVersion.MatchesGameVersion());
                    if (versionMods != null)
                    {
                        List<ModInfo>? modsToDownload = _modsViewController.Init(versionMods.Mods);
                        if (modsToDownload != null)
                        {
                            _modsDownloadingViewController.Init(modsToDownload);
                            _modsViewController.Finished += OnAcceptModsDownload;
                            ProvideInitialViewControllers(_modsViewController);
                            return;
                        }
                    }
                }

                _dirtyListing = false;
                _menuPrefabManager.Reset(false);
                _ = _menuPrefabManager.Download();
                _resultsViewController.continueButtonPressedEvent += HandleResultsViewControllerContinueButtonPressed;
                _countdownManager.LevelStarted += OnLevelStarted;
                _lobbyNavigationViewController.IntroStarted += OnIntroStarted;
                _loadingViewController.Finished += OnLoadingFinished;
                _introViewController.Finished += OnIntroFinished;
                _networkManager.Disconnected += OnDisconnected;
                _menuPrefabManager.Show();
                if (_config.JoinChat == null)
                {
                    _simpleDialogPromptViewController.Init(
                        "Chat",
                        "Automatically join chatrooms for events?",
                        "Yes",
                        "No",
                        n =>
                        {
                            _config.JoinChat = n switch
                            {
                                0 => true,
                                _ => false
                            };

                            TransitionFinished += () =>
                            {
                                ReplaceTopViewController(
                                    _loadingViewController,
                                    null,
                                    ViewController.AnimationType.In,
                                    ViewController.AnimationDirection.Vertical);
                            };
                            _ = _networkManager.RunAsync();
                        });
                    ProvideInitialViewControllers(_simpleDialogPromptViewController);
                }
                else
                {
                    ProvideInitialViewControllers(_loadingViewController);
                    _ = _networkManager.RunAsync();
                }
            }
        }
    }

    public override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        // ReSharper disable once InvertIf
        if (removedFromHierarchy)
        {
            IsActive = false;
            _resultsViewController.continueButtonPressedEvent -= HandleResultsViewControllerContinueButtonPressed;
            _modsViewController.Finished -= OnAcceptModsDownload;
            _countdownManager.LevelStarted -= OnLevelStarted;
            _lobbyNavigationViewController.IntroStarted -= OnIntroStarted;
            _loadingViewController.Finished -= OnLoadingFinished;
            _networkManager.Disconnected -= OnDisconnected;
            _ = _networkManager.Disconnect(DisconnectCode.DisconnectedByUser);
            _menuPrefabManager.Hide();
        }
    }

    public override void TopViewControllerWillChange(
        ViewController oldViewController,
        ViewController newViewController,
        ViewController.AnimationType animationType)
    {
        switch (newViewController)
        {
            case EventLobbyNavigationViewController:
                SetLeftScreenViewController(_gameplaySetupViewController, animationType);
                SetRightScreenViewController(_leaderboardViewController, animationType);
                break;
            default:
                SetLeftScreenViewController(null, animationType);
                SetRightScreenViewController(null, animationType);
                SetBottomScreenViewController(null, animationType);
                break;
        }

        switch (newViewController)
        {
            case EventLobbyNavigationViewController:
                SetTitle(_listing?.Title ?? "N/A", animationType);
                showBackButton = true;
                break;
            case EventIntroViewController:
            case SimpleDialogPromptViewController:
                SetTitle(null, animationType);
                showBackButton = false;
                break;
            case EventLoadingViewController:
            case EventModsDownloadingViewController:
                SetTitle(_listing?.Title ?? "N/A", animationType);
                showBackButton = true;
                break;
            default:
                showBackButton = false;
                break;
        }
    }

    public override void TransitionDidFinish()
    {
        base.TransitionDidFinish();
        while (!_isInTransition && _transitionFinished.Count > 0)
        {
            _transitionFinished.Dequeue().Invoke();
        }
    }

    [UsedImplicitly]
    [Inject]
    private void Construct(
        SiraLog log,
        Config config,
        GameplaySetupViewController gameplaySetupViewController,
        ResultsViewController resultsViewController,
        EventIntroViewController introViewController,
        EventLeaderboardViewController leaderboardViewController,
        EventLoadingViewController loadingViewController,
        EventLobbyNavigationViewController lobbyNavigationViewController,
        EventModsDownloadingViewController modsDownloadingViewController,
        EventModsViewController modsViewController,
        SimpleDialogPromptViewController simpleDialogPromptViewController,
        CountdownManager countdownManager,
        MapDownloadingManager mapDownloadingManager,
        LevelStartManager levelStartManager,
        ListingManager listingManager,
        NetworkManager networkManager,
        MenuPrefabManager menuPrefabManager)
    {
        _log = log;
        _config = config;
        _gameplaySetupViewController = gameplaySetupViewController;
        _resultsViewController = resultsViewController;
        _introViewController = introViewController;
        _leaderboardViewController = leaderboardViewController;
        _loadingViewController = loadingViewController;
        _lobbyNavigationViewController = lobbyNavigationViewController;
        _modsDownloadingViewController = modsDownloadingViewController;
        _modsViewController = modsViewController;
        _simpleDialogPromptViewController = simpleDialogPromptViewController;
        _countdownManager = countdownManager;
        _mapDownloadingManager = mapDownloadingManager;
        _levelStartManager = levelStartManager;
        listingManager.ListingFound += OnListingFound;
        _networkManager = networkManager;
        _menuPrefabManager = menuPrefabManager;
    }

    private void HandleLevelDidFinish(
        DownloadedMap map,
        StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupData,
        LevelCompletionResults levelCompletionResults)
    {
        TransitionDidFinish();
        _menuPrefabManager.HideParticles();

        if (levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Incomplete &&
            levelCompletionResults.levelEndAction != LevelCompletionResults.LevelEndAction.Quit)
        {
            return;
        }

#if LATEST
        BeatmapKey beatmapKey = standardLevelScenesTransitionSetupData.beatmapKey;
        BeatmapLevel beatmapLevel = standardLevelScenesTransitionSetupData.beatmapLevel;
#else
        IDifficultyBeatmap difficultyBeatmap = standardLevelScenesTransitionSetupData.difficultyBeatmap;
#endif
        IReadonlyBeatmapData transformedBeatmapData = standardLevelScenesTransitionSetupData.transformedBeatmapData;
        ////this._menuLightsManager.SetColorPreset((levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared) ? this._resultsClearedLightsPreset : this._resultsFailedLightsPreset, true);
        ////levelCompletionResults.SetField(nameof(levelCompletionResults.levelEndStateType), LevelCompletionResults.LevelEndStateType.Failed);
        _resultsViewController.Init(
            levelCompletionResults,
            transformedBeatmapData,
#if LATEST
            beatmapKey,
            beatmapLevel,
#else
            difficultyBeatmap,
#endif
            false,
            false);
        _resultsViewController._restartButton.gameObject.SetActive(false);
        TransitionFinished += () => PresentViewController(
            _resultsViewController,
            null,
            ViewController.AnimationDirection.Horizontal,
            true);

        SubmitScore(map.Index, levelCompletionResults.modifiedScore, ScorePercentageHelper.ScorePercentage);
    }

    private void HandleResultsViewControllerContinueButtonPressed(ResultsViewController viewController)
    {
        DismissViewController(
            viewController,
            ViewController.AnimationDirection.Horizontal,
            () => viewController._restartButton.gameObject.SetActive(true));
    }

    private void OnAcceptModsDownload()
    {
        ReplaceTopViewController(
            _modsDownloadingViewController,
            null,
            ViewController.AnimationType.In,
            ViewController.AnimationDirection.Vertical);
    }

    private void OnDisconnected(string reason)
    {
        TransitionFinished += () =>
        {
            if (topViewController == _simpleDialogPromptViewController)
            {
                return;
            }

            _simpleDialogPromptViewController.Init(
                "Disconnected",
                reason,
                "Ok",
                _ => { Finished?.Invoke(this); });

            ReplaceTopViewController(
                _simpleDialogPromptViewController,
                null,
                ViewController.AnimationType.In,
                ViewController.AnimationDirection.Vertical);
        };
    }

    private void OnIntroFinished()
    {
        TransitionFinished += () =>
        {
            if (topViewController == _introViewController)
            {
                DismissViewController(
                    _introViewController,
                    ViewController.AnimationDirection.Vertical);
            }
        };
    }

    private void OnIntroStarted()
    {
        TransitionFinished += () =>
        {
            if (topViewController == _lobbyNavigationViewController)
            {
                PresentViewController(_introViewController, null, ViewController.AnimationDirection.Vertical);
            }
        };
    }

    private void OnLevelStarted()
    {
        TransitionFinished += () =>
        {
            if (topViewController == _resultsViewController)
            {
                DismissViewController(
                    _resultsViewController,
                    ViewController.AnimationDirection.Horizontal,
                    OnLevelStarted);
                return;
            }

            if (topViewController != _lobbyNavigationViewController)
            {
                return;
            }

            TransitionDidStart();
            _mapDownloadingManager.MapDownloadedOnce += n =>
            {
                _transitionFinished.Clear();
                try
                {
                    _levelStartManager.StartLevel(n, HandleLevelDidFinish);
                }
                catch (Exception e)
                {
                    _log.Error($"Failed to start level:\n{e}");
                    TransitionDidFinish();
                }
            };
        };
    }

    private void OnListingFound(Listing? listing)
    {
        _dirtyListing = true;
        _listing = listing;
    }

    private void OnLoadingFinished(string? error)
    {
        if (error != null)
        {
            OnDisconnected(error);
            return;
        }

        TransitionFinished += () =>
        {
            if (topViewController == _loadingViewController)
            {
                ReplaceTopViewController(
                    _lobbyNavigationViewController,
                    null,
                    ViewController.AnimationType.In,
                    ViewController.AnimationDirection.Vertical);
            }
        };
    }

    private void SubmitScore(int index, int score, float percentage)
    {
        ScoreSubmission scoreSubmission = new()
        {
            Index = index,
            Score = score,
            Percentage = percentage
        };
        string scoreJson = JsonConvert.SerializeObject(scoreSubmission, JsonSettings.Settings);
        _log.Warn(scoreJson);
        _ = _networkManager.Send(ServerOpcode.ScoreSubmission, scoreJson);
        _leaderboardViewController.ChangeSelection(index);
    }

    internal class EventFlowCoordinatorFactory : IFactory<EventFlowCoordinator>
    {
        private readonly IInstantiator _instantiator;
        private readonly MainFlowCoordinator _mainFlowCoordinator;

        [UsedImplicitly]
        private EventFlowCoordinatorFactory(IInstantiator instantiator, MainFlowCoordinator mainFlowCoordinator)
        {
            _instantiator = instantiator;
            _mainFlowCoordinator = mainFlowCoordinator;
        }

        public EventFlowCoordinator Create()
        {
            GameObject gameObject = new(nameof(EventFlowCoordinator));
            gameObject.transform.SetParent(_mainFlowCoordinator.transform.parent);
            gameObject.layer = _mainFlowCoordinator.gameObject.layer;

            return _instantiator.InstantiateComponent<EventFlowCoordinator>(gameObject);
        }
    }
}
