using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HMUI;
using IPA.Utilities.Async;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Synapse.Extras;
using Synapse.Managers;
using Synapse.Models;
using UnityEngine;
using Zenject;

namespace Synapse.Views
{
    internal class EventFlowCoordinator : FlowCoordinator
    {
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
        private Listing _listing = null!;

        private bool _dirtyListing;
        private CancellationTokenSource? _startCancel;
        private Queue<Action> _transitionFinished = new();

        internal event Action<EventFlowCoordinator>? didFinishEvent;

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
                SetTitle(_listing.Title);
                showBackButton = true;

                _gameplaySetupViewController.Setup(false, false, true, false, PlayerSettingsPanelController.PlayerSettingsPanelLayout.Singleplayer);

                if (!_dirtyListing && _modsDownloadingViewController.DownloadFinished)
                {
                    ProvideInitialViewControllers(_modsDownloadingViewController);
                }
                else
                {
                    _dirtyListing = false;
                    if (_modsViewController.Init())
                    {
                        _modsViewController.didAcceptEvent += OnAcceptModsDownload;
                        ProvideInitialViewControllers(_modsViewController);
                    }
                    else
                    {
                        ProvideInitialViewControllers(_loadingViewController);
                        _resultsViewController.continueButtonPressedEvent += HandleResultsViewControllerContinueButtonPressed;
                        _lobbyViewController.StartLevel += TryStartLevel;
                        _networkManager.MapUpdated += OnMapUpdated;
                        _networkManager.StartTimeUpdated += OnStartTimeUpdated;
                        _networkManager.Connecting += OnConnecting;
                        _networkManager.Disconnected += OnDisconnected;
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
                _modsViewController.didAcceptEvent -= OnAcceptModsDownload;
                _lobbyViewController.StartLevel -= TryStartLevel;
                _networkManager.MapUpdated -= OnMapUpdated;
                _networkManager.StartTimeUpdated -= OnStartTimeUpdated;
                _networkManager.Connecting -= OnConnecting;
                _networkManager.Disconnected -= OnDisconnected;
                _ = _networkManager.Disconnect("Leaving");
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
                    SetTitle(_listing.Title, animationType);
                    showBackButton = true;
                    break;
                case SimpleDialogPromptViewController:
                    SetTitle(null, animationType);
                    showBackButton = false;
                    break;
                case EventLoadingViewController:
                case EventModsDownloadingViewController:
                    SetTitle(_listing.Title, animationType);
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
                didFinishEvent?.Invoke(this);
            }
        }

        [UsedImplicitly]
        [Inject]
        private void Construct(
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
            NetworkManager networkManager)
        {
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
            listingManager.ListingFound += n => _listing = n;
            _networkManager = networkManager;
        }

        private void OnAcceptModsDownload(List<RequiredMod> requiredMods)
        {
            _modsDownloadingViewController.Init(requiredMods);
            ReplaceTopViewController(
                _modsDownloadingViewController,
                null,
                ViewController.AnimationType.In,
                ViewController.AnimationDirection.Vertical);
        }

        private void OnMapUpdated(int index, Map map)
        {
            TransitionFinished += () =>
            {
                if (topViewController == _loadingViewController)
                {
                    ReplaceTopViewController(
                        _lobbyViewController,
                        null,
                        ViewController.AnimationType.In,
                        ViewController.AnimationDirection.Vertical);
                }
            };
        }

        private void OnStartTimeUpdated(DateTime? startTime)
        {
            if (startTime == null || (_networkManager.Status.HasScore && !(_networkManager.Status.Map.Ruleset?.AllowResubmission ?? false)))
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

        private void OnConnecting(Stage stage, int _)
        {
            if (stage != Stage.Failed)
            {
                return;
            }

            TransitionFinished += () =>
            {
                _simpleDialogPromptViewController.Init(
                    "Connection Error",
                    "Connection failed after 3 tries",
                    "OK",
                    _ => { didFinishEvent?.Invoke(this); });

                ReplaceTopViewController(
                    _simpleDialogPromptViewController,
                    null,
                    ViewController.AnimationType.In,
                    ViewController.AnimationDirection.Vertical);
            };
        }

        private void OnDisconnected(string reason)
        {
            TransitionFinished += () =>
            {
                _simpleDialogPromptViewController.Init(
                    "Disconnected",
                    reason,
                    "OK",
                    _ => { didFinishEvent?.Invoke(this); });

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
                    _levelStartManager.StartLevel(n.Difficulty, n.Preview, HandleLevelDidFinish);
                };
            };
        }

        private void HandleResultsViewControllerContinueButtonPressed(ResultsViewController viewController)
        {
            DismissViewController(viewController, ViewController.AnimationDirection.Horizontal, () => viewController._restartButton.gameObject.SetActive(true));
        }

        private void HandleLevelDidFinish(
            StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupData,
            LevelCompletionResults levelCompletionResults)
        {
            TransitionDidFinish();
            IDifficultyBeatmap difficultyBeatmap = standardLevelScenesTransitionSetupData.difficultyBeatmap;
            IReadonlyBeatmapData transformedBeatmapData = standardLevelScenesTransitionSetupData.transformedBeatmapData;
            if (levelCompletionResults.levelEndStateType is not LevelCompletionResults.LevelEndStateType.Failed
                and not LevelCompletionResults.LevelEndStateType.Cleared)
            {
                SubmitScore(0);
                return;
            }

            ////this._menuLightsManager.SetColorPreset((levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared) ? this._resultsClearedLightsPreset : this._resultsFailedLightsPreset, true);
            ////levelCompletionResults.SetField(nameof(levelCompletionResults.levelEndStateType), LevelCompletionResults.LevelEndStateType.Failed);
            _resultsViewController.Init(levelCompletionResults, transformedBeatmapData, difficultyBeatmap, false, false);
            _resultsViewController._restartButton.gameObject.SetActive(false);
            TransitionFinished += () => PresentViewController(
                _resultsViewController,
                null,
                ViewController.AnimationDirection.Horizontal,
                true);

            SubmitScore(levelCompletionResults.modifiedScore);
        }

        private void SubmitScore(int score)
        {
            int index = _networkManager.Status.Index;
            ScoreSubmission scoreSubmission = new()
            {
                Index = index,
                Score = score
            };
            string scoreJson = JsonConvert.SerializeObject(scoreSubmission, JsonSettings.Settings);
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
