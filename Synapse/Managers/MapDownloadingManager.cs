using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IPA.Utilities.Async;
using JetBrains.Annotations;
using SiraUtil.Logging;
using Synapse.Models;
using UnityEngine;
using Zenject;

namespace Synapse.Managers
{
    internal sealed class MapDownloadingManager : IDisposable, ITickable
    {
        private static readonly string _mapFolder =
            (Path.GetDirectoryName(Application.streamingAssetsPath) ?? throw new InvalidOperationException()) +
            $"{Path.DirectorySeparatorChar}Synapse{Path.DirectorySeparatorChar}Levels";

        private readonly CustomLevelLoader _customLevelLoader;
        private readonly DownloadingManager _downloadingManager;
        private readonly SiraLog _log;

        private readonly bool _songCoreActive;

        private string _lastSent = string.Empty;
        private float _downloadProgress;
        private float _lastProgress;

        private DownloadedMap? _beatmapLevel;

        [UsedImplicitly]
        [Inject]
        private MapDownloadingManager(
            SiraLog log,
            CustomLevelLoader customLevelLoader,
            NetworkManager networkManager,
            DownloadingManager downloadingManager)
        {
            _log = log;
            _customLevelLoader = customLevelLoader;
            _downloadingManager = downloadingManager;
            PurgeDirectory();
            networkManager.MapUpdated += Init;

            _songCoreActive = IPA.Loader.PluginManager.GetPlugin("SongCore") != null;
        }

        public event Action<string>? ProgressUpdated;

        public event Action<DownloadedMap>? MapDownloaded
        {
            add
            {
                if (_beatmapLevel.HasValue)
                {
                    value?.Invoke(_beatmapLevel.Value);
                }

                _mapDownloaded += value;
            }

            remove => _mapDownloaded -= value;
        }

        public event Action<DownloadedMap>? MapDownloadedOnce
        {
            add
            {
                if (_beatmapLevel.HasValue)
                {
                    value?.Invoke(_beatmapLevel.Value);
                    return;
                }

                _mapDownloadedOnce += value;
            }

            remove => _mapDownloadedOnce -= value;
        }

        private event Action<DownloadedMap>? _mapDownloadedOnce;

        private event Action<DownloadedMap>? _mapDownloaded;

        public void Dispose()
        {
            PurgeDirectory();
        }

        public void Tick()
        {
            _lastProgress = Mathf.Lerp(_lastProgress, _downloadProgress, 20 * Time.deltaTime);
            string text = $"{_lastProgress:0%}";
            if (text == _lastSent)
            {
                return;
            }

            _lastSent = text;
            ProgressUpdated?.Invoke(text);
        }

        internal void Cancel()
        {
            _downloadingManager.Cancel();
        }

        // needed for BeatLeader
        private static CustomPreviewBeatmapLevel SongCoreLoad(
            StandardLevelInfoSaveData standardLevelInfoSaveData,
            string songPath)
        {
            return SongCore.Loader.LoadSong(standardLevelInfoSaveData, songPath, out _) ??
                   throw new InvalidOperationException();
        }

        // TODO: decide if i should keep this
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

        private void Init(int _, Map map)
        {
            _beatmapLevel = null;
            string mapName = Path.GetInvalidFileNameChars().Aggregate(map.Name, (current, c) => current.Replace(c, '_'));
            string path = $"{_mapFolder}{Path.DirectorySeparatorChar}{mapName}";
            UnityMainThreadTaskScheduler.Factory.StartNew(() => DownloadAndSave(map, path));
        }

        private async Task DownloadAndSave(Map map, string path)
        {
            CancellationToken token = _downloadingManager.Reset();
            _lastProgress = 0;
            _downloadProgress = 0;
            string url = map.DownloadUrl;
            if (!Directory.Exists(path))
            {
                _log.Debug($"Attempting to download [{map.Name}] from [{url}]");
                if (!await _downloadingManager.Download(
                        url,
                        path,
                        n => _downloadProgress = n * 0.8f,
                        null,
                        n => _downloadProgress = 0.8f + (n * 0.15f),
                        null,
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
                _downloadProgress = 0.98f;
                StandardLevelInfoSaveData infoSaveData =
                    await _customLevelLoader.LoadCustomLevelInfoSaveDataAsync(path, token);
                CustomPreviewBeatmapLevel customPreviewBeatmapLevel = _songCoreActive
                    ? SongCoreLoad(infoSaveData, path)
                    : await _customLevelLoader.LoadCustomPreviewBeatmapLevelAsync(
                        path,
                        infoSaveData,
                        token);

                _downloadProgress = 0.99f;
                CustomBeatmapLevel beatmapLevel =
                    await _customLevelLoader.LoadCustomBeatmapLevelAsync(customPreviewBeatmapLevel, token);
                IDifficultyBeatmapSet set = beatmapLevel.beatmapLevelData.GetDifficultyBeatmapSet(map.Characteristic);
                IDifficultyBeatmap difficultyBeatmap = set.difficultyBeatmaps.First(n => (int)n.difficulty == map.Difficulty);

                _log.Debug($"Successfully downloaded [{map.Name}] as [{customPreviewBeatmapLevel.levelID}]");

                DownloadedMap downloadedMap = new(map, difficultyBeatmap, customPreviewBeatmapLevel);
                _beatmapLevel = downloadedMap;
                _mapDownloaded?.Invoke(downloadedMap);
                _mapDownloadedOnce?.Invoke(downloadedMap);
                _mapDownloadedOnce = null;
            }
            catch (Exception e)
            {
                _log.Error($"Error deserializing beatmap data\n({e})");
            }
        }
    }
}
