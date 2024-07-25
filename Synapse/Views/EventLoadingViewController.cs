using System;
using System.Net.Sockets;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using JetBrains.Annotations;
using Synapse.Managers;
using Synapse.Models;
using TMPro;
using UnityEngine;
using Zenject;

namespace Synapse.Views;

////[HotReload(RelativePathToLayout = @"../Resources/Loading.bsml")]
[ViewDefinition("Synapse.Resources.Loading.bsml")]
internal class EventLoadingViewController : BSMLAutomaticViewController
{
    [UIObject("spinny")]
    private readonly GameObject _loadingObject = null!;

    [UIComponent("text")]
    private readonly TMP_Text _textObject = null!;

    private float _angle;
    private string _connectingText = string.Empty;
    private Display _display;
    private bool _finished;
    private MenuPrefabManager _menuPrefabManager = null!;

    private NetworkManager _networkManager = null!;
    private bool _prefabDownloaded;

    private bool _stageUpdated;
    private bool _timeSynced;
    private TimeSyncManager _timeSyncManager = null!;

    internal event Action<string?>? Finished;

    private enum Display
    {
        Joining,
        Connecting,
        DownloadingPrefab,
        Synchronizing
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        _connectingText = "Connecting...";
        _stageUpdated = false;
        _prefabDownloaded = false;
        _finished = false;
        Refresh();
        _networkManager.Connecting += OnConnecting;
        _networkManager.StageUpdated += OnStageUpdated;
        _menuPrefabManager.Loaded += OnMenuPrefabLoaded;
        _timeSyncManager.Synced += OnSynced;
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
        _networkManager.Connecting -= OnConnecting;
        _networkManager.StageUpdated -= OnStageUpdated;
        _menuPrefabManager.Loaded -= OnMenuPrefabLoaded;
        _timeSyncManager.Synced -= OnSynced;
    }

    [UsedImplicitly]
    [Inject]
    private void Construct(
        NetworkManager networkManager,
        MenuPrefabManager menuPrefabManager,
        TimeSyncManager timeSyncManager)
    {
        _networkManager = networkManager;
        _menuPrefabManager = menuPrefabManager;
        _timeSyncManager = timeSyncManager;
    }

    private void Finish(string? error = null)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        Finished?.Invoke(error);
    }

    private void OnConnecting(ConnectingStage connectingStage, int retries)
    {
        if (connectingStage == ConnectingStage.Failed)
        {
            Finish($"Connection failed after {retries} tries");
            return;
        }

        string text = connectingStage switch
        {
            ConnectingStage.Connecting => "Connecting...",
            ConnectingStage.Authenticating => "Authenticating...",
            ConnectingStage.ReceivingData => "Receiving data...",
            ConnectingStage.Timeout => "Connection timed out, retrying...",
            ConnectingStage.Refused => "Connection refused, retrying...",
            _ => $"{(SocketError)connectingStage}, retrying..."
        };

        if (retries > 0)
        {
            text += $" ({retries + 1})";
        }

        _connectingText = text;
    }

    private void OnMenuPrefabLoaded(bool success)
    {
        if (!success)
        {
            Finish("Error occurred while downloading bundle");
            return;
        }

        _prefabDownloaded = true;
        Refresh();
    }

    private void OnStageUpdated(IStageStatus _)
    {
        _stageUpdated = true;
        Refresh();
    }

    private void OnSynced(bool success)
    {
        if (!success)
        {
            Finish("Time sync timed out");
            return;
        }

        _timeSynced = true;
        Refresh();
    }

    private void Refresh()
    {
        if (_stageUpdated)
        {
            if (_prefabDownloaded)
            {
                if (_timeSynced)
                {
                    _display = Display.Joining;
                    Finish();
                }
                else
                {
                    _display = Display.Synchronizing;
                }
            }
            else
            {
                _display = Display.DownloadingPrefab;
            }
        }
        else
        {
            _display = Display.Connecting;
        }
    }

    private void Update()
    {
        _angle += Time.deltaTime * 200;
        _loadingObject.transform.localEulerAngles = new Vector3(0, 0, _angle);
        string text = _display switch
        {
            Display.Connecting => _connectingText,
            Display.Joining => "Joining...",
            Display.DownloadingPrefab => $"Downloading assets... {_menuPrefabManager.DownloadProgress:0%}",
            Display.Synchronizing => "Synchronizing...",
            _ => "ERR"
        };
        _textObject.text = text;
    }
}
