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

        private float _angle;

        private string _text = string.Empty;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            _text = "Connecting...";
            _networkManager.Connecting += OnConnecting;
            _networkManager.MapUpdated += OnMapUpdated;
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
            _networkManager.Connecting -= OnConnecting;
            _networkManager.MapUpdated -= OnMapUpdated;
        }

        [UsedImplicitly]
        [Inject]
        private void Construct(NetworkManager networkManager)
        {
            _networkManager = networkManager;
        }

        private void Update()
        {
            _angle += Time.deltaTime * 200;
            _loadingObject.transform.localEulerAngles = new Vector3(0, 0, _angle);
            _textObject.text = _text;
        }

        private void OnMapUpdated(int index, Map _)
        {
            _text = "Joining...";
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

            _text = text;
        }
    }
}
