using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HMUI;
using IPA.Utilities.Async;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SiraUtil.Logging;
using Synapse.Extras;
using Synapse.HarmonyPatches;
using Synapse.Managers;
using Synapse.Models;
using UnityEngine;
using Zenject;

namespace Synapse.Views
{
    internal class EventFlowCoordinator : FlowCoordinator
    {
        private SiraLog _log = null!;
        private Config _config = null!;
        private GameplaySetupViewController _gameplaySetupViewController = null!;
        private ResultsViewController _resultsViewController = null!;
        private EventLobbyViewController _lobbyViewController = null!;
        private EventLoadingViewController _loadingViewController = null!;
        private EventModsViewController _modsViewController = null!;
        private EventModsDownloadingViewController _modsDownloadingViewController = null!;
        private EventLeaderboardViewController _leaderboardViewController = null!;
        private SimpleDialogPromptViewController _simpleDialogPromptViewController = null!;
        private MapDownloadingManager _mapDownloadingManager = null!;
        private LevelStartManager _levelStartManager = null!;
        private NetworkManager _networkManager = null!;
        private PrefabManager _prefabManager = null!;

        private Listing? _listing;
        private bool _dirtyListing;
        private CancellationTokenSource? _startCancel;
        private Queue<Action> _transitionFinished = new();

        internal event Action<EventFlowCoordinator>? Finished;

        private event Action TransitionFinished
        {
            add
            {
                if (!_isInTransition)
                {
                    // not sure why this is the only thing to have problems with background threads
                    UnityMainThreadTaskScheduler.Factory.StartNew(value.Invoke);
                    return;
                }

                _transitionFinished.Enqueue(value);
            }

            remove => _transitionFinished = new Queue<Action>(_transitionFinished.Where(n => n != value));
        }

        // screw it
        public static bool IsActive { get; private set; }

        public override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            // TODO: light color presets
            // ReSharper disable once InvertIf
            if (addedToHierarchy)
            {
                IsActive = true;
                SetTitle(_listing?.Title ?? "N/A");
                showBackButton = true;

                _gameplaySetupViewController.Setup(false, false, true, false, PlayerSettingsPanelController.PlayerSettingsPanelLayout.Singleplayer);

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
                else if (!_dirtyListing && _modsDownloadingViewController.DownloadFinished)
                {
                    ProvideInitialViewControllers(_modsDownloadingViewController);
                }
                else
                {
                    if (_dirtyListing)
                    {
                        RequiredMods? versionMods = _listing.RequiredMods.FirstOrDefault(n => n.GameVersion == Plugin.GAME_VERSION);
                        if (versionMods == null)
                        {
                            SetTitle(null);
                            showBackButton = false;
                            _simpleDialogPromptViewController.Init(
                                "Error",
                                $"{_listing.Title} only allows versions {string.Join(", ", _listing.RequiredMods.Select(n => n.GameVersion))}",
                                "Ok",
                                _ => { Finished?.Invoke(this); });
                            ProvideInitialViewControllers(_simpleDialogPromptViewController);
                            return;
                        }

                        List<ModInfo>? modsToDownload = _modsViewController.Init(versionMods.Mods);
                        if (modsToDownload != null)
                        {
                            _modsDownloadingViewController.Init(modsToDownload);
                            _modsViewController.Finished += OnAcceptModsDownload;
                            ProvideInitialViewControllers(_modsViewController);
                            return;
                        }

                        _ = _prefabManager.Download();
                    }

                    _dirtyListing = false;
                    _resultsViewController.continueButtonPressedEvent += HandleResultsViewControllerContinueButtonPressed;
                    _lobbyViewController.StartLevel += TryStartLevel;
                    _loadingViewController.Finished += OnLoadingFinished;
                    _networkManager.StartTimeUpdated += OnStartTimeUpdated;
                    _networkManager.Disconnected += OnDisconnected;
                    _prefabManager.Show();
                    if (_config.JoinChat == null)
                    {
                        _simpleDialogPromptViewController.Init(
                            "Chat",
                            "Automatically join chatrooms for events?",
                            "Yes",
                            "No",
                            n =>
                            {
                                switch (n)
                                {
                                    case 0:
                                        _config.JoinChat = true;
                                        _ = _networkManager.SendBool(true, ServerOpcode.SetChatter);
                                        break;

                                    case 1:
                                        _config.JoinChat = false;
                                        break;
                                }

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
                _lobbyViewController.StartLevel -= TryStartLevel;
                _loadingViewController.Finished -= OnLoadingFinished;
                _networkManager.StartTimeUpdated -= OnStartTimeUpdated;
                _networkManager.Disconnected -= OnDisconnected;
                _ = _networkManager.Disconnect("Leaving");
                _prefabManager.Hide();
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

        public override void TopViewControllerWillChange(ViewController oldViewController, ViewController newViewController, ViewController.AnimationType animationType)
        {
            switch (newViewController)
            {
                case EventLobbyViewController:
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
                case EventLobbyViewController:
                    SetLeftScreenViewController(_gameplaySetupViewController, animationType);
                    SetTitle(_listing?.Title ?? "N/A", animationType);
                    showBackButton = true;
                    break;
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

        public override void BackButtonWasPressed(ViewController topView)
        {
            if (!topView.isInTransition)
            {
                Finished?.Invoke(this);
            }
        }

        [UsedImplicitly]
        [Inject]
        private void Construct(
            SiraLog log,
            Config config,
            GameplaySetupViewController gameplaySetupViewController,
            ResultsViewController resultsViewController,
            EventLobbyViewController lobbyViewController,
            EventLoadingViewController loadingViewController,
            EventModsViewController modsViewController,
            EventModsDownloadingViewController modsDownloadingViewController,
            EventLeaderboardViewController eventLeaderboardViewController,
            SimpleDialogPromptViewController simpleDialogPromptViewController,
            MapDownloadingManager mapDownloadingManager,
            LevelStartManager levelStartManager,
            ListingManager listingManager,
            NetworkManager networkManager,
            PrefabManager prefabManager)
        {
            _log = log;
            _config = config;
            _gameplaySetupViewController = gameplaySetupViewController;
            _resultsViewController = resultsViewController;
            _lobbyViewController = lobbyViewController;
            _loadingViewController = loadingViewController;
            _modsViewController = modsViewController;
            _modsDownloadingViewController = modsDownloadingViewController;
            _leaderboardViewController = eventLeaderboardViewController;
            _simpleDialogPromptViewController = simpleDialogPromptViewController;
            _mapDownloadingManager = mapDownloadingManager;
            _levelStartManager = levelStartManager;
            listingManager.ListingFound += OnListingFound;
            _networkManager = networkManager;
            _prefabManager = prefabManager;
        }

        private void OnListingFound(Listing? listing)
        {
            _dirtyListing = true;
            _listing = listing;
        }

        private void OnAcceptModsDownload()
        {
            _dirtyListing = false;
            ReplaceTopViewController(
                _modsDownloadingViewController,
                null,
                ViewController.AnimationType.In,
                ViewController.AnimationDirection.Vertical);
        }

        private void OnLoadingFinished(string? error)
        {
            TransitionFinished += () =>
            {
                if (error == null)
                {
                    if (topViewController == _loadingViewController)
                    {
                        ReplaceTopViewController(
                            _lobbyViewController,
                            null,
                            ViewController.AnimationType.In,
                            ViewController.AnimationDirection.Vertical);
                    }
                }
                else
                {
                    _ = _networkManager.Disconnect("Error");

                    _simpleDialogPromptViewController.Init(
                        "Error",
                        error,
                        "Ok",
                        _ => { Finished?.Invoke(this); });

                    ReplaceTopViewController(
                        _simpleDialogPromptViewController,
                        null,
                        ViewController.AnimationType.In,
                        ViewController.AnimationDirection.Vertical);
                }
            };
        }

        private void OnStartTimeUpdated(DateTime? startTime)
        {
            if (startTime == null || (_networkManager.Status.PlayerScore != null && !(_networkManager.Status.Map?.Ruleset?.AllowResubmission ?? false)))
            {
                return;
            }

            _startCancel?.Cancel();
            _ = DelayedStart(startTime.Value, (_startCancel = new CancellationTokenSource()).Token);
        }

        private async Task DelayedStart(DateTime startTime, CancellationToken token)
        {
            TimeSpan diff = startTime - DateTime.UtcNow;
            if (diff.Ticks > 0)
            {
                await Task.Delay(diff, token);
            }

            TransitionFinished += () =>
            {
                token.ThrowIfCancellationRequested();
                if (topViewController == _lobbyViewController)
                {
                    TryStartLevel();
                }
                else if (topViewController == _resultsViewController)
                {
                    DismissViewController(
                        _resultsViewController,
                        ViewController.AnimationDirection.Horizontal,
                        TryStartLevel);
                }
            };
        }

        private void OnDisconnected(string reason)
        {
            TransitionFinished += () =>
            {
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

        private void TryStartLevel()
        {
            TransitionFinished += () =>
            {
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
                        _log.Error($"Failed to start level: {e}");
                        TransitionDidFinish();
                    }
                };
            };
        }

        private void HandleResultsViewControllerContinueButtonPressed(ResultsViewController viewController)
        {
            DismissViewController(viewController, ViewController.AnimationDirection.Horizontal, () => viewController._restartButton.gameObject.SetActive(true));
        }

        private void HandleLevelDidFinish(
            DownloadedMap map,
            StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupData,
            LevelCompletionResults levelCompletionResults)
        {
            TransitionDidFinish();
            IDifficultyBeatmap difficultyBeatmap = standardLevelScenesTransitionSetupData.difficultyBeatmap;
            IReadonlyBeatmapData transformedBeatmapData = standardLevelScenesTransitionSetupData.transformedBeatmapData;
            switch (levelCompletionResults.levelEndStateType)
            {
                case LevelCompletionResults.LevelEndStateType.Incomplete:
                    if (levelCompletionResults.levelEndAction is LevelCompletionResults.LevelEndAction.Quit)
                    {
                        SubmitScore(map.Index, 0, 0);
                    }

                    return;

                case LevelCompletionResults.LevelEndStateType.Cleared
                    or LevelCompletionResults.LevelEndStateType.Failed:
                    ////this._menuLightsManager.SetColorPreset((levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared) ? this._resultsClearedLightsPreset : this._resultsFailedLightsPreset, true);
                    ////levelCompletionResults.SetField(nameof(levelCompletionResults.levelEndStateType), LevelCompletionResults.LevelEndStateType.Failed);
                    _resultsViewController.Init(
                        levelCompletionResults,
                        transformedBeatmapData,
                        difficultyBeatmap,
                        false,
                        false);
                    _resultsViewController._restartButton.gameObject.SetActive(false);
                    TransitionFinished += () => PresentViewController(
                        _resultsViewController,
                        null,
                        ViewController.AnimationDirection.Horizontal,
                        true);

                    SubmitScore(map.Index, levelCompletionResults.modifiedScore, AccuracyHelper.Accuracy);
                    break;
            }
        }

        private void SubmitScore(int index, int score, float accuracy)
        {
            ScoreSubmission scoreSubmission = new()
            {
                Index = index,
                Score = score,
                Accuracy = accuracy
            };
            string scoreJson = JsonConvert.SerializeObject(scoreSubmission, JsonSettings.Settings);
            _log.Warn(scoreJson);
            _ = _networkManager.SendString(scoreJson, ServerOpcode.ScoreSubmission);
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
}
