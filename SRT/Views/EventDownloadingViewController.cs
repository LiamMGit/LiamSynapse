using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using Hive.Versioning;
using IPA.Loader;
using IPA.Utilities.Async;
using JetBrains.Annotations;
using SiraUtil.Logging;
using SRT.Managers;
using SRT.Models;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Zenject;

namespace SRT.Views
{
    // ReSharper disable FieldCanBeMadeReadOnly.
    public abstract class EventDownloadingViewController : BSMLAutomaticViewController
    {
        private SiraLog _log = null!;
        private DownloadingManager _downloadingManager = null!;

        private Image _loadingBar = null!;

        protected float DownloadProgress { get; set; }

        protected string DownloadText { get; set; } = string.Empty;

        protected string QuitText { get; set; } = string.Empty;

        protected View NewView { get; set; } = View.None;

        protected string LastError { get; set; } = string.Empty;

        private float _lastProgress;
        private View _currentView = View.None;

        protected abstract TMP_Text PercentageTMP { get; }

        protected abstract TMP_Text DownloadingTMP { get; }

        protected abstract TMP_Text ErrorTMP { get; }

        protected abstract TMP_Text QuitTMP { get; }

        protected abstract VerticalLayoutGroup BarGroup { get; }

        protected abstract GameObject QuitGroup { get; }

        protected abstract GameObject DownloadingGroup { get; }

        protected abstract GameObject Error { get; }

        protected enum View
        {
            None,
            Quit,
            Downloading,
            Error
        }

        [UsedImplicitly]
        [Inject]
        private void Construct(SiraLog log, DownloadingManager downloadingManager)
        {
            _log = log;
            _downloadingManager = downloadingManager;
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

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

        public void Cancel()
        {
            _downloadingManager.Cancel();
        }

        protected CancellationToken ResetToken()
        {
            _lastProgress = 0;
            DownloadProgress = 0;
            NewView = View.Downloading;
            return _downloadingManager.Reset();
        }

        protected async Task<bool> Download(string url, string unzipPath, Action<float> progress, Action unzipping, Action<float> unzipProgress, CancellationToken token)
        {
            return await _downloadingManager.Download(
                url,
                unzipPath,
                progress,
                unzipping,
                unzipProgress,
                n =>
            {
                LastError = n;
                NewView = View.Error;
            },
                token);
        }
    }
}
