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

namespace Synapse.Views
{
    ////[HotReload(RelativePathToLayout = @"../Resources/Loading.bsml")]
    [ViewDefinition("Synapse.Resources.Loading.bsml")]
    internal class EventLoadingViewController : BSMLAutomaticViewController
    {
        [UIObject("spinny")]
        private readonly GameObject _loadingObject = null!;

        [UIComponent("text")]
        private readonly TMP_Text _textObject = null!;

        private NetworkManager _networkManager = null!;
        private PrefabManager _prefabManager = null!;

        private bool _mapUpdated;
        private bool _prefabDownloaded;

        private float _angle;
        private string _connectingText = string.Empty;
        private Display _display;

        internal event Action? Finished;

        private enum Display
        {
            Joining,
            Connecting,
            PrefabDownload
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            _connectingText = "Connecting...";
            _networkManager.Connecting += OnConnecting;
            _networkManager.MapUpdated += OnMapUpdated;
            _prefabManager.Loaded += OnPrefabLoaded;
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
            _networkManager.Connecting -= OnConnecting;
            _networkManager.MapUpdated -= OnMapUpdated;
            _prefabManager.Loaded -= OnPrefabLoaded;
        }

        [UsedImplicitly]
        [Inject]
        private void Construct(NetworkManager networkManager, PrefabManager prefabManager)
        {
            _networkManager = networkManager;
            _prefabManager = prefabManager;
        }

        private void Update()
        {
            _angle += Time.deltaTime * 200;
            _loadingObject.transform.localEulerAngles = new Vector3(0, 0, _angle);
            string text = _display switch
            {
                Display.Connecting => _connectingText,
                Display.Joining => "Joining...",
                Display.PrefabDownload => $"Downloading assets... {_prefabManager.DownloadProgress:0%}",
                _ => "ERR"
            };
            _textObject.text = text;
        }

        private void Refresh()
        {
            if (_mapUpdated)
            {
                if (_prefabDownloaded)
                {
                    _display = Display.Joining;
                    Finished?.Invoke();
                }
                else
                {
                    _display = Display.PrefabDownload;
                }
            }
            else
            {
                _display = Display.Connecting;
            }
        }

        private void OnMapUpdated(int index, Map? _)
        {
            _mapUpdated = true;
            Refresh();
        }

        private void OnPrefabLoaded(bool success)
        {
            if (!success)
            {
                return;
            }

            _prefabDownloaded = true;
            Refresh();
        }

        private void OnConnecting(Stage stage, int retries)
        {
            switch (stage)
            {
                case Stage.Failed:
                case Stage.Connecting:
                    return;
            }

            string text = stage switch
            {
                ////Stage.Connecting => "Connecting...",
                Stage.Authenticating => "Authenticating...",
                Stage.ReceivingData => "Receiving data...",
                Stage.Timeout => "Connection timed out, retrying...",
                Stage.Refused => "Connection refused, retrying...",
                _ => $"{(SocketError)stage}, retrying..."
            };

            if (retries > 0)
            {
                text += $" ({retries + 1})";
            }

            _connectingText = text;
        }
    }
}
