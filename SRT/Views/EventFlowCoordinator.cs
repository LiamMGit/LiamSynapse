using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using HMUI;
using IPA.Utilities.Async;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SRT.Managers;
using SRT.Models;
using UnityEngine;
using Zenject;

namespace SRT.Views
{
    public class EventFlowCoordinator : FlowCoordinator
    {
        private static readonly JsonSerializerSettings _jsonSerializerSettings = new()
        {
            ContractResolver = new DefaultContractResolver()
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            }
        };

        private GameplaySetupViewController _gameplaySetupViewController = null!;
        private ResultsViewController _resultsViewController = null!;
        private EventLobbyViewController _lobbyViewController = null!;
        private EventLoadingViewController _loadingViewController = null!;
        private EventModsViewController _modsViewController = null!;
        private EventModsDownloadingViewController _modsDownloadingViewController = null!;
        private EventMapDownloadingViewController _mapDownloadingViewController = null!;
        private SimpleDialogPromptViewController _simpleDialogPromptViewController = null!;
        private LevelStartManager _levelStartManager = null!;
        private NetworkManager _networkManager = null!;
        private Listing _listing = null!;

        private bool _dirtyListing;

        public event Action<EventFlowCoordinator>? didFinishEvent;

        private event Action TransitionFinished
        {
            add
            {
                if (!_isInTransition)
                {
                    // not sure why this is the only thing to have problems with background threads
                    UnityMainThreadTaskScheduler.Factory.StartNew(() => value?.Invoke());
                    return;
                }

                _transitionFinished.Enqueue(value);
            }

            remove => _transitionFinished = new Queue<Action>(_transitionFinished.Where(n => n != value));
        }

        private Queue<Action> _transitionFinished = new();

        // screw it
        public static bool IsActive { get; private set; }

        [Inject]
        [UsedImplicitly]
        private void Construct(
            GameplaySetupViewController gameplaySetupViewController,
            ResultsViewController resultsViewController,
            EventLobbyViewController lobbyViewController,
            EventLoadingViewController loadingViewController,
            EventModsViewController modsViewController,
            EventModsDownloadingViewController modsDownloadingViewController,
            EventMapDownloadingViewController mapDownloadingViewController,
            SimpleDialogPromptViewController simpleDialogPromptViewController,
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
            _mapDownloadingViewController = mapDownloadingViewController;
            _simpleDialogPromptViewController = simpleDialogPromptViewController;
            _levelStartManager = levelStartManager;
            listingManager.ListingFound += n => _listing = n;
            _networkManager = networkManager;
        }

        public override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            // TODO: light color presets
            if (addedToHierarchy)
            {
                IsActive = true;
                SetTitle(_listing.Title);
                showBackButton = true;

                // TODO: allow to be controlled by coordinator
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
                        _mapDownloadingViewController.MapDownloaded += OnMapDownloaded;
                        _resultsViewController.continueButtonPressedEvent += HandleResultsViewControllerContinueButtonPressed;
                        _networkManager.PlayStatusUpdated += OnPlayStatusUpdated;
                        _networkManager.Connecting += OnConnecting;
                        _networkManager.Disconnected += OnDisconnected;
                        _ = _networkManager.RunAsync();
                    }
                }
            }
        }

        public override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            if (removedFromHierarchy)
            {
                IsActive = false;
                _mapDownloadingViewController.Cancel();
                _mapDownloadingViewController.MapDownloaded -= OnMapDownloaded;
                _resultsViewController.continueButtonPressedEvent -= HandleResultsViewControllerContinueButtonPressed;
                _modsViewController.didAcceptEvent -= OnAcceptModsDownload;
                _networkManager.PlayStatusUpdated -= OnPlayStatusUpdated;
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
                case EventMapDownloadingViewController:
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

        private void OnAcceptModsDownload(List<RequiredMod> requiredMods)
        {
            _modsDownloadingViewController.Init(requiredMods);
            ReplaceTopViewController(
                _modsDownloadingViewController,
                null,
                ViewController.AnimationType.In,
                ViewController.AnimationDirection.Vertical);
        }

        private void OnPlayStatusUpdated(int playStatus)
        {
            TransitionFinished += () =>
            {
                if (topViewController == _loadingViewController)
                {
                    ReplaceTopViewController(
                        _lobbyViewController,
                        playStatus == 1 ? TryStartLevel : null,
                        ViewController.AnimationType.In,
                        ViewController.AnimationDirection.Vertical);
                }
                else if (topViewController == _lobbyViewController && playStatus == 1)
                {
                    TryStartLevel();
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
                if (_mapDownloadingViewController.BeatmapLevel != null)
                {
                    (IDifficultyBeatmap Difficulty, IPreviewBeatmapLevel Preview) beatmapLevel = _mapDownloadingViewController.BeatmapLevel.Value;
                    _levelStartManager.StartLevel(beatmapLevel.Difficulty, beatmapLevel.Preview, HandleLevelDidFinish);
                }
                else
                {
                    ReplaceTopViewController(
                        _mapDownloadingViewController,
                        null,
                        ViewController.AnimationType.In,
                        ViewController.AnimationDirection.Vertical);
                }
            };
        }

        private void OnMapDownloaded((IDifficultyBeatmap Difficulty, IPreviewBeatmapLevel Preview) beatmapLevel)
        {
            TransitionFinished += () =>
            {
                if (topViewController == _mapDownloadingViewController)
                {
                    ReplaceTopViewController(
                        _lobbyViewController,
                        () => _levelStartManager.StartLevel(beatmapLevel.Difficulty, beatmapLevel.Preview, HandleLevelDidFinish),
                        ViewController.AnimationType.In,
                        ViewController.AnimationDirection.Vertical);
                }
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
            IDifficultyBeatmap difficultyBeatmap = standardLevelScenesTransitionSetupData.difficultyBeatmap;
            IReadonlyBeatmapData transformedBeatmapData = standardLevelScenesTransitionSetupData.transformedBeatmapData;
            if (levelCompletionResults.levelEndStateType is not LevelCompletionResults.LevelEndStateType.Failed
                and not LevelCompletionResults.LevelEndStateType.Cleared)
            {
                SubmitScore(-1);
                return;
            }

            ////this._menuLightsManager.SetColorPreset((levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared) ? this._resultsClearedLightsPreset : this._resultsFailedLightsPreset, true);
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
            ScoreSubmission scoreSubmission = new()
            {
                Index = _networkManager.Status.Index,
                Score = score
            };
            string scoreJson = JsonConvert.SerializeObject(scoreSubmission, _jsonSerializerSettings);
            _ = _networkManager.SendString(scoreJson, ServerOpcode.ScoreSubmission);
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
