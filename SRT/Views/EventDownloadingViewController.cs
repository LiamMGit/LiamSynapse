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
        protected CancellationTokenSource _tokenSource = new();

        private SiraLog _log = null!;

        private CustomLevelLoader _customLevelLoader = null!;
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
        private void Construct(SiraLog log)
        {
            _log = log;
        }

        private void Start()
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
                        ErrorTMP.text = LastError;
                        break;
                }
            }

            if (_currentView != View.Downloading)
            {
                return;
            }

            _lastProgress = Mathf.Lerp(_lastProgress, DownloadProgress, 20 * Time.deltaTime);
            _loadingBar.fillAmount = _lastProgress;
            float percentage = _lastProgress * 100;
            PercentageTMP.text = $"{(int)percentage}%";
            DownloadingTMP.text = DownloadText;
            QuitTMP.text = QuitText;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            _tokenSource.Dispose();
        }

        public void Cancel()
        {
            _tokenSource.Cancel();
        }

        protected CancellationToken Reset()
        {
            _tokenSource.Dispose();
            _tokenSource = new CancellationTokenSource();
            _lastProgress = 0;
            DownloadProgress = 0;
            NewView = View.Downloading;
            return _tokenSource.Token;
        }

        protected async Task<bool> Download(string url, string unzipPath, Action<float> progress, Action unzipping, Action<float> unzipProgress, CancellationToken token)
        {
            try
            {
                using UnityWebRequest www = UnityWebRequest.Get(url);
                www.SendWebRequest();
                while (!www.isDone)
                {
                    if (!token.IsCancellationRequested)
                    {
                        progress(www.downloadProgress);
                        await Task.Delay(150, token);
                        continue;
                    }

                    www.Abort();
                    _log.Debug("Download cancelled");
                    return false;
                }

#pragma warning disable CS0618
                if (www.isNetworkError || www.isHttpError)
                {
                    if (www.isNetworkError)
                    {
                        LastError = $"Network error while downloading\n{www.error}";
                        _log.Error(LastError);
                    }
                    else if (www.isHttpError)
                    {
                        LastError =
                            $"Server sent error response code while downloading\n({www.responseCode})";
                        _log.Error(LastError);
                    }

                    NewView = View.Error;
                    return false;
                }
#pragma warning restore CS0618

                unzipping();

                using MemoryStream zipStream = new(www.downloadHandler.data);
                using ZipArchive zip = new(zipStream, ZipArchiveMode.Read, false);
                await Task.Run(
                    () =>
                    {
                        ZipArchiveEntry[] entries = zip.Entries.ToArray();
                        for (int j = 0; j < entries.Length; j++)
                        {
                            unzipProgress((float)j / entries.Length);
                            ZipArchiveEntry entry = entries[j];
                            string fullPath = Path.GetFullPath(Path.Combine(unzipPath, entry.FullName));
                            if (Path.GetFileName(fullPath).Length == 0)
                            {
                                Directory.CreateDirectory(fullPath);
                            }
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                                entry.ExtractToFile(fullPath, false);
                            }
                        }
                    },
                    token);
            }
            catch (Exception e)
            {
                LastError =
                    $"Error downloading\n({e})";
                _log.Error(LastError);
                return false;
            }

            return true;
        }
    }
}
