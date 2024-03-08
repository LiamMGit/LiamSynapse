using System;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.ViewControllers;
using JetBrains.Annotations;
using Synapse.Extras;
using Synapse.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Synapse.Views
{
    internal abstract class EventDownloadingViewController : BSMLAutomaticViewController
    {
        private CancellationTokenManager _cancellationTokenManager = null!;
        private Image _loadingBar = null!;

        private float _lastProgress;
        private View _currentView = View.None;

        protected enum View
        {
            None,
            Quit,
            Downloading,
            Error
        }

        protected float DownloadProgress { get; set; }

        protected string DownloadText { get; set; } = string.Empty;

        protected string QuitText { get; set; } = string.Empty;

        protected View NewView { get; set; } = View.None;

        protected string LastError { get; set; } = string.Empty;

        protected abstract TMP_Text PercentageTMP { get; }

        protected abstract TMP_Text DownloadingTMP { get; }

        protected abstract TMP_Text ErrorTMP { get; }

        protected abstract TMP_Text QuitTMP { get; }

        protected abstract VerticalLayoutGroup BarGroup { get; }

        protected abstract GameObject QuitGroup { get; }

        protected abstract GameObject DownloadingGroup { get; }

        protected abstract GameObject Error { get; }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            // ReSharper disable once InvertIf
            if (firstActivation)
            {
                Vector2 loadingBarSize = new(0, 8);

                // shamelessly stolen from songcore
                _loadingBar = new GameObject("Loading Bar").AddComponent<Image>();
                RectTransform barTransform = (RectTransform)_loadingBar.transform;
                barTransform.SetParent(BarGroup.transform, false);
                barTransform.sizeDelta = loadingBarSize;
                Texture2D? tex = Texture2D.whiteTexture;
                Sprite? sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f, 100, 1);
                _loadingBar.sprite = sprite;
                _loadingBar.type = Image.Type.Filled;
                _loadingBar.fillMethod = Image.FillMethod.Horizontal;
                _loadingBar.color = new Color(1, 1, 1, 0.5f);

                Image loadingBackg = new GameObject("Background").AddComponent<Image>();
                RectTransform loadingBackTransform = (RectTransform)loadingBackg.transform;
                loadingBackTransform.sizeDelta = loadingBarSize;
                loadingBackTransform.SetParent(BarGroup.transform, false);
                loadingBackg.color = new Color(0, 0, 0, 0.2f);
            }
        }

        protected CancellationToken ResetToken()
        {
            _lastProgress = 0;
            DownloadProgress = 0;
            NewView = View.Downloading;
            return _cancellationTokenManager.Reset();
        }

        protected void Cancel()
        {
            _cancellationTokenManager.Cancel();
        }

        protected async Task Download(string url, string unzipPath, Action<float> progress, Action unzipping, Action<float> unzipProgress, CancellationToken token)
        {
            try
            {
                await AsyncExtensions.DownloadAndSave(
                    url,
                    unzipPath,
                    progress,
                    unzipping,
                    unzipProgress,
                    token);
            }
            catch (Exception e)
            {
                LastError = e.Message;
                NewView = View.Error;
                throw;
            }
        }

        [UsedImplicitly]
        [Inject]
        private void Construct(CancellationTokenManager cancellationTokenManager)
        {
            _cancellationTokenManager = cancellationTokenManager;
        }

        private void Update()
        {
            if (_currentView != NewView)
            {
                _currentView = NewView;
                switch (_currentView)
                {
                    case View.Quit:
                        QuitGroup.SetActive(true);
                        DownloadingGroup.SetActive(false);
                        Error.SetActive(false);
                        break;

                    case View.Downloading:
                        QuitGroup.SetActive(false);
                        DownloadingGroup.SetActive(true);
                        Error.SetActive(false);
                        break;

                    case View.Error:
                        QuitGroup.SetActive(false);
                        DownloadingGroup.SetActive(false);
                        Error.SetActive(true);
                        break;
                }
            }

            switch (_currentView)
            {
                case View.Quit:
                    QuitTMP.text = QuitText;
                    break;

                case View.Error:
                    ErrorTMP.text = LastError;
                    break;

                case View.Downloading:
                    _lastProgress = Mathf.Lerp(_lastProgress, DownloadProgress, 20 * Time.deltaTime);
                    _loadingBar.fillAmount = _lastProgress;
                    float percentage = _lastProgress * 100;
                    PercentageTMP.text = $"{(int)percentage}%";
                    DownloadingTMP.text = DownloadText;
                    break;
            }
        }
    }
}
