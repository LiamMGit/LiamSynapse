using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HMUI;
using IPA.Utilities.Async;
using JetBrains.Annotations;
using SRT.Managers;
using SRT.Models;
using UnityEngine;
using Zenject;

namespace SRT.Views
{
    public class EventFlowCoordinator : FlowCoordinator
    {
        private GameplaySetupViewController _gameplaySetupViewController = null!;
        private EventLobbyViewController _lobbyViewController = null!;
        private EventLoadingViewController _loadingViewController = null!;
        private EventModsViewController _modsViewController = null!;
        private EventModsDownloadingViewController _modsDownloadingViewController = null!;
        private EventMapDownloadingViewController _mapDownloadingViewController = null!;
        private SimpleDialogPromptViewController _simpleDialogPromptViewController = null!;
        private MenuTransitionsHelper _menuTransitionsHelper = null!;
        private HeckIntegrationManager? _heckIntegrationManager;
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
            EventLobbyViewController lobbyViewController,
            EventLoadingViewController loadingViewController,
            EventModsViewController modsViewController,
            EventModsDownloadingViewController modsDownloadingViewController,
            EventMapDownloadingViewController mapDownloadingViewController,
            SimpleDialogPromptViewController simpleDialogPromptViewController,
            MenuTransitionsHelper menuTransitionsHelper,
            ListingManager listingManager,
            NetworkManager networkManager,
            [InjectOptional] HeckIntegrationManager? heckIntegrationManager)
        {
            _gameplaySetupViewController = gameplaySetupViewController;
            _lobbyViewController = lobbyViewController;
            _loadingViewController = loadingViewController;
            _modsViewController = modsViewController;
            _modsDownloadingViewController = modsDownloadingViewController;
            _mapDownloadingViewController = mapDownloadingViewController;
            _simpleDialogPromptViewController = simpleDialogPromptViewController;
            _menuTransitionsHelper = menuTransitionsHelper;
            _heckIntegrationManager = heckIntegrationManager;
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
                        _mapDownloadingViewController.OnStartLevel += StartLevel;
                        _networkManager.PlayStatusUpdated += OnPlayStatusUpdated;
                        _networkManager.Connecting += OnConnecting;
                        _networkManager.Disconnected += OnDisconnected;
                        _networkManager.Connect();
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
                _modsViewController.didAcceptEvent -= OnAcceptModsDownload;
                _mapDownloadingViewController.OnStartLevel -= StartLevel;
                _networkManager.PlayStatusUpdated -= OnPlayStatusUpdated;
                _networkManager.Connecting -= OnConnecting;
                _networkManager.Disconnected -= OnDisconnected;
                _ = _networkManager.Disconnect("Leaving");
            }
        }

        private void StartLevel(IDifficultyBeatmap difficultyBeatmap, IPreviewBeatmapLevel previewBeatmapLevel)
        {
            GameplayModifiers modifiers = new()
            {
                _noFailOn0Energy = true
            };
            if (_heckIntegrationManager != null)
            {
                _heckIntegrationManager.StartPlayViewInterruptedLevel(
                    "screw yo analytics",
                    difficultyBeatmap,
                    previewBeatmapLevel,
                    null, // no environment override
                    _gameplaySetupViewController.colorSchemesSettings.GetOverrideColorScheme(), // TODO: make this toggleable by event coordinator
                    modifiers, // TODO: allow event coordinator to define modifiers
                    _gameplaySetupViewController.playerSettings,
                    null,
                    "Quit",
                    false,
                    false,
                    null,
                    null,
                    null);
            }
            else
            {
                _menuTransitionsHelper.StartStandardLevel(
                    "screw yo analytics",
                    difficultyBeatmap,
                    previewBeatmapLevel,
                    null, // no environment override
                    _gameplaySetupViewController.colorSchemesSettings
                        .GetOverrideColorScheme(), // TODO: make this toggleable by event coordinator
                    modifiers, // TODO: allow event coordinator to define modifiers
                    _gameplaySetupViewController.playerSettings,
                    null,
                    "Quit",
                    false,
                    false,
                    null,
                    null,
                    null);
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
                        playStatus == 0 ? _lobbyViewController : _mapDownloadingViewController,
                        null,
                        ViewController.AnimationType.In,
                        ViewController.AnimationDirection.Vertical);
                }
                else if (topViewController == _lobbyViewController && playStatus == 1)
                {
                    ReplaceTopViewController(
                        _mapDownloadingViewController,
                        null,
                        ViewController.AnimationType.In,
                        ViewController.AnimationDirection.Vertical);
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

        public override void TransitionDidFinish()
        {
            base.TransitionDidFinish();
            if (_transitionFinished.Count > 0)
            {
                _transitionFinished.Dequeue().Invoke();
            }
        }

        public override void TopViewControllerWillChange(ViewController oldViewController, ViewController newViewController, ViewController.AnimationType animationType)
        {
            if (newViewController == _lobbyViewController)
            {
                SetLeftScreenViewController(_gameplaySetupViewController, animationType);
            }

            if (newViewController == _simpleDialogPromptViewController)
            {
                SetTitle(null, animationType);
                showBackButton = false;
            }
        }

        public override void BackButtonWasPressed(ViewController topView)
        {
            if (!topView.isInTransition)
            {
                didFinishEvent?.Invoke(this);
            }
        }
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
