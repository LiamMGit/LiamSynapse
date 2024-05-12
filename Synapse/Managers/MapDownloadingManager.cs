using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IPA.Utilities.Async;
using JetBrains.Annotations;
using SiraUtil.Logging;
using Synapse.Extras;
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

        private readonly SiraLog _log;
        private readonly CustomLevelLoader _customLevelLoader;
        private readonly CancellationTokenManager _cancellationTokenManager;
        private readonly DirectoryInfo _directory;

        private readonly bool _songCoreActive;

        private string _lastSent = string.Empty;
        private string? _error;
        private float _downloadProgress;
        private float _lastProgress;

        private DownloadedMap? _beatmapLevel;

        [UsedImplicitly]
        [Inject]
        private MapDownloadingManager(
            SiraLog log,
            CustomLevelLoader customLevelLoader,
            NetworkManager networkManager,
            CancellationTokenManager cancellationTokenManager)
        {
            _log = log;
            _customLevelLoader = customLevelLoader;
            _cancellationTokenManager = cancellationTokenManager;
            _directory = new DirectoryInfo(_mapFolder);
            _directory.Purge();
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

                MapDownloaded_Backing += value;
            }

            remove => MapDownloaded_Backing -= value;
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

                MapDownloadedOnce_Backing += value;
            }

            remove => MapDownloadedOnce_Backing -= value;
        }

        private event Action<DownloadedMap>? MapDownloadedOnce_Backing;

        private event Action<DownloadedMap>? MapDownloaded_Backing;

        public void Dispose()
        {
            _directory.Purge();
        }

        public void Tick()
        {
            string text;
            if (_error != null)
            {
                text = _error;
            }
            else
            {
                _lastProgress = Mathf.Lerp(_lastProgress, _downloadProgress, 20 * Time.deltaTime);
                text = $"{_lastProgress:0%}";
            }

            if (text == _lastSent)
            {
                return;
            }

            _lastSent = text;
            ProgressUpdated?.Invoke(text);
        }

        internal void Cancel()
        {
            _cancellationTokenManager.Cancel();
        }

        // needed for BeatLeader
        private static CustomPreviewBeatmapLevel SongCoreLoad(
            StandardLevelInfoSaveData standardLevelInfoSaveData,
            string songPath)
        {
            return SongCore.Loader.LoadSong(standardLevelInfoSaveData, songPath, out _) ??
                   throw new InvalidOperationException();
        }

        private void Init(int index, Map? map)
        {
            MapDownloadedOnce_Backing = null;
            _beatmapLevel = null;

            if (map == null)
            {
                return;
            }

            string mapName = Path.GetInvalidFileNameChars().Aggregate(map.Name, (current, c) => current.Replace(c, '_'));
            string path = $"{_mapFolder}{Path.DirectorySeparatorChar}{mapName}";
            UnityMainThreadTaskScheduler.Factory.StartNew(async () =>
            {
                int i = 0;
                while (true)
                {
                    try
                    {
                        await Download(index, map, path);
                        return;
                    }
                    catch
                    {
                        await Task.Delay(++i * 1000);
                    }
                }
            });
        }

        private async Task Download(int index, Map map, string path)
        {
            _error = null;
            CancellationToken token = _cancellationTokenManager.Reset();
            _lastProgress = 0;
            _downloadProgress = 0;
            try
            {
                Download download =
                    map.Downloads.FirstOrDefault(n => n.GameVersion.Split(',').Any(v => v == Plugin.GameVersion)) ??
                    throw new InvalidOperationException($"No download found for game version [{Plugin.GameVersion}].");
                string url = download.Url;

                DirectoryInfo directory = new(path);
                if (directory.Exists)
                {
                    directory.Delete(true);
                }

                _log.Debug($"Attempting to download [{map.Name}] from [{url}]");
                await MediaExtensions.DownloadAndSave(
                    url,
                    download.Hash,
                    path,
                    n => _downloadProgress = n * 0.8f,
                    null,
                    n => _downloadProgress = 0.8f + (n * 0.15f),
                    token);

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
                IDifficultyBeatmapSet set =
                    beatmapLevel.beatmapLevelData.GetDifficultyBeatmapSet(map.Characteristic);
                IDifficultyBeatmap difficultyBeatmap =
                    set.difficultyBeatmaps.First(n => (int)n.difficulty == map.Difficulty);

                _log.Debug($"Successfully downloaded [{map.Name}] as [{customPreviewBeatmapLevel.levelID}]");
                _downloadProgress = 1;

                DownloadedMap downloadedMap = new(index, map, difficultyBeatmap, customPreviewBeatmapLevel);
                _beatmapLevel = downloadedMap;
                MapDownloaded_Backing?.Invoke(downloadedMap);
                MapDownloadedOnce_Backing?.Invoke(downloadedMap);
                MapDownloadedOnce_Backing = null;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _log.Error($"Error downloading map [{map.Name}]\n{e}");
                _error = "ERROR!";
                _directory.Purge();
                throw;
            }
        }
    }
}
