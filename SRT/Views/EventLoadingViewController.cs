using System.Net.Sockets;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using IPA.Utilities.Async;
using SRT.Managers;
using TMPro;
using UnityEngine;
using Zenject;

namespace SRT.Views
{
    //[HotReload(RelativePathToLayout = @"../Resources/Loading.bsml")]
    [ViewDefinition("SRT.Resources.Loading.bsml")]
    public class EventLoadingViewController : BSMLAutomaticViewController
    {
        [UIObject("spinny")]
        private readonly GameObject _loadingObject = null!;

        [UIComponent("text")]
        private readonly TMP_Text _textObject = null!;

        private NetworkManager _networkManager = null!;

        private float _angle;

        private string _text = string.Empty;

        [Inject]
        private void Construct(NetworkManager networkManager)
        {
            _networkManager = networkManager;
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            _text = "Connecting...";
            _networkManager.Connecting += OnConnecting;
            _networkManager.PlayStatusUpdated += OnPlayStatusUpdated;
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
            _networkManager.Connecting -= OnConnecting;
            _networkManager.PlayStatusUpdated -= OnPlayStatusUpdated;
        }

        private void Update()
        {
            _angle += Time.deltaTime * 200;
            _loadingObject.transform.localEulerAngles = new Vector3(0, 0, _angle);
            _textObject.text = _text;
        }

        private void OnPlayStatusUpdated(int status)
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
