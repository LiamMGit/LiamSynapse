/*using System;
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
    // ReSharper disable FieldCanBeMadeReadOnly.Local
    ////[HotReload(RelativePathToLayout = @"../Resources/ModsDownloading.bsml")]
    [ViewDefinition("SRT.Resources.ModsDownloading.bsml")]
    public class EventMapDownloadingViewController : EventDownloadingViewController
    {
        private static readonly string _mapFolder =
            (Path.GetDirectoryName(Application.streamingAssetsPath) ?? throw new InvalidOperationException()) +
            $"{Path.DirectorySeparatorChar}SRT";

        private CustomLevelLoader _customLevelLoader = null!;
        private SiraLog _log = null!;

        private Map _map;

#pragma warning disable SA1124
#region i hate bsml
#pragma warning restore SA1124
        [UIComponent("percentage")]
        private TMP_Text _percentageTMP = null!;

        [UIComponent("downloadingtext")]
        private TMP_Text _downloadingTMP = null!;

        [UIComponent("errortext")]
        private TMP_Text _errorTMP = null!;

        [UIComponent("quittext")]
        private TMP_Text _quitTMP = null!;

        [UIComponent("loadingbar")]
        private VerticalLayoutGroup _barGroup = null!;

        [UIObject("quit")]
        private GameObject _quitGroup = null!;

        [UIObject("downloading")]
        private GameObject _downloadingGroup = null!;

        [UIObject("error")]
        private GameObject _error = null!;

        // ReSharper disable ConvertToAutoProperty
        protected override TMP_Text PercentageTMP => _percentageTMP;

        protected override TMP_Text DownloadingTMP => _downloadingTMP;

        protected override TMP_Text ErrorTMP => _errorTMP;

        protected override TMP_Text QuitTMP => _quitTMP;

        protected override VerticalLayoutGroup BarGroup => _barGroup;

        protected override GameObject QuitGroup => _quitGroup;

        protected override GameObject DownloadingGroup => _downloadingGroup;

        protected override GameObject Error => _error;
#endregion

        public event Action<(IDifficultyBeatmap Difficulty, IPreviewBeatmapLevel Preview)>? MapDownloaded;

        public (IDifficultyBeatmap Difficulty, IPreviewBeatmapLevel Preview)? BeatmapLevel;

        public void Init(Map map)
        {
            _map = map;
            BeatmapLevel = null;
            string mapName = Path.GetInvalidFileNameChars().Aggregate(_map.Name, (current, c) => current.Replace(c, '_'));
            string path = $"{_mapFolder}{Path.DirectorySeparatorChar}{mapName}";
            UnityMainThreadTaskScheduler.Factory.StartNew(() => DownloadAndSave(path));
        }

        [UsedImplicitly]
        [Inject]
        private void Construct(
            SiraLog log,
            CustomLevelLoader customLevelLoader,
            NetworkManager networkManager)
        {
            _log = log;
            _customLevelLoader = customLevelLoader;
            PurgeDirectory();
            networkManager.MapUpdated += Init;
        }

        private async Task DownloadAndSave(string path)
        {
            CancellationToken token = Reset();
            string url = _map.DownloadUrl;
            DownloadText = "Downloading map...";

            if (!Directory.Exists(path))
            {
                _log.Debug($"Attempting to download [{_map.Name}] from [{url}]");
                if (!await Download(
                        url,
                        path,
                        n => DownloadProgress = n * 0.45f,
                        () => DownloadText = "Unzipping...",
                        n => DownloadProgress = 0.45f + (n * 0.45f),
                        token))
                {
                    return;
                }
            }
            else
            {
                _log.Debug($"[{path}] already exists");
            }

            try
            {
                DownloadText = "Deserializing...";
                CustomPreviewBeatmapLevel customPreviewBeatmapLevel =
                    await _customLevelLoader.LoadCustomPreviewBeatmapLevelAsync(
                        path,
                        await _customLevelLoader.LoadCustomLevelInfoSaveDataAsync(path, token),
                        token);
                DownloadProgress = 0.95f;

                CustomBeatmapLevel beatmapLevel =
                    await _customLevelLoader.LoadCustomBeatmapLevelAsync(customPreviewBeatmapLevel, token);
                IDifficultyBeatmapSet set = beatmapLevel.beatmapLevelData.GetDifficultyBeatmapSet(_map.Characteristic);
                IDifficultyBeatmap difficultyBeatmap = set.difficultyBeatmaps.First(n => (int)n.difficulty == _map.Difficulty);
                DownloadProgress = 1f;

                _log.Debug($"Successfully downloaded [{_map.Name}]");

                (IDifficultyBeatmap, IPreviewBeatmapLevel) startParameters = (difficultyBeatmap, beatmapLevel);
                BeatmapLevel = startParameters;
                MapDownloaded?.Invoke(startParameters);
            }
            catch (Exception e)
            {
                LastError =
                    $"Error deserializing beatmap data\n({e})";
                _log.Error(LastError);
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            PurgeDirectory();
        }

        private static void PurgeDirectory()
        {
            // cleanup
            if (!Directory.Exists(_mapFolder))
            {
                return;
            }

            DirectoryInfo directory = new(_mapFolder);

            foreach (FileInfo file in directory.GetFiles())
            {
                file.Delete();
            }

            foreach (DirectoryInfo dir in directory.GetDirectories())
            {
                dir.Delete(true);
            }
        }
    }
}
*/
