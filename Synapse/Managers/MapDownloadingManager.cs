using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IPA.Loader;
using IPA.Utilities.Async;
using JetBrains.Annotations;
using SiraUtil.Logging;
using SongCore;
using Synapse.Extras;
using Synapse.Models;
using UnityEngine;
using Zenject;
#if !LATEST
using System.Reflection;
using HarmonyLib;
using SongCore.Data;
#endif

namespace Synapse.Managers;

internal sealed class MapDownloadingManager : IDisposable, ITickable
{
    private static readonly string _mapFolder =
        (Path.GetDirectoryName(Application.streamingAssetsPath) ?? throw new InvalidOperationException()) +
        $"{Path.DirectorySeparatorChar}Synapse{Path.DirectorySeparatorChar}Levels";

    private readonly SiraLog _log;
    private readonly CustomLevelLoader _customLevelLoader;
    private readonly NetworkManager _networkManager;
    private readonly CancellationTokenManager _cancellationTokenManager;
    private readonly DirectoryInfo _directory;

#if LATEST
    private readonly bool _doSongCoreLoad;
#else
    private readonly MethodInfo? _songCoreLoad;
#endif

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
        _networkManager = networkManager;
        _cancellationTokenManager = cancellationTokenManager;
        _directory = new DirectoryInfo(_mapFolder);
        _directory.Purge();
        networkManager.MapUpdated += OnMapUpdated;
        networkManager.Closed += OnClosed;

        if (PluginManager.GetPlugin("SongCore") != null)
        {
#if LATEST
            _doSongCoreLoad = true;
#else
            _songCoreLoad = GetSongCoreLoadMethod();
#endif
        }
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

            MapDownloadedBacking += value;
        }

        remove => MapDownloadedBacking -= value;
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

            MapDownloadedOnceBacking += value;
        }

        remove => MapDownloadedOnceBacking -= value;
    }

    private event Action<DownloadedMap>? MapDownloadedOnceBacking;

    private event Action<DownloadedMap>? MapDownloadedBacking;

    public void Dispose()
    {
        _networkManager.MapUpdated -= OnMapUpdated;
        _networkManager.Closed -= OnClosed;
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

#if LATEST
    private static BeatmapLevel SongCoreLoad(string songPath)
    {
        (string, BeatmapLevel)? customLevel = Loader.LoadCustomLevel(songPath);
        if (customLevel == null)
        {
            throw new InvalidOperationException("SongCore error: failed to load.");
        }

        return customLevel.Value.Item2;
    }
#else
    // idk why its private
    private static MethodInfo? GetSongCoreLoadMethod()
    {
        return AccessTools.Method(typeof(Loader), "LoadSongAndAddToDictionaries");
    }

    // needed for other mods like BeatTogether/Camera2
    private CustomPreviewBeatmapLevel SongCoreLoad(string songPath)
    {
        SongData? songData = Loader.Instance.LoadCustomLevelSongData(songPath);
        if (songData == null)
        {
            throw new InvalidOperationException("SongCore error: invalid song data.");
        }

        object? result = _songCoreLoad!.Invoke(
            Loader.Instance,
            [CancellationToken.None, songData, songPath, null]);
        if (result == null)
        {
            throw new InvalidOperationException("SongCore error: failed to load.");
        }

        return (CustomPreviewBeatmapLevel)result;
    }
#endif

    private void OnMapUpdated(int index, Map? map)
    {
        MapDownloadedOnceBacking = null;
        _beatmapLevel = null;

        if (map == null)
        {
            return;
        }

        string mapName = Path.GetInvalidFileNameChars().Aggregate(map.Name, (current, c) => current.Replace(c, '_'));
        string path = $"{_mapFolder}{Path.DirectorySeparatorChar}{mapName}";
        UnityMainThreadTaskScheduler.Factory.StartNew(
            async () =>
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

    private void OnClosed(ClosedReason _)
    {
        _cancellationTokenManager.Cancel();
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
                map.Downloads.FirstOrDefault(n => n.GameVersion.MatchesGameVersion()) ??
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
#if LATEST
            BeatmapLevel beatmapLevel;
            if (_doSongCoreLoad)
            {
                beatmapLevel = SongCoreLoad(path);
            }
            else
            {
                CustomLevelFolderInfo? customLevelFolderInfo =
                    await FileSystemCustomLevelProvider.LoadCustomLevelFolderInfoAsync(path, token);
                if (customLevelFolderInfo == null)
                {
                    throw new InvalidOperationException("Failed to get CustomLevelFolderInfo.");
                }

                (BeatmapLevel, CustomLevelLoader.LoadedSaveData)? tuple =
                    await _customLevelLoader.LoadBeatmapLevelAsync(customLevelFolderInfo.Value, token);
                if (tuple == null)
                {
                    throw new InvalidOperationException("Failed to get BeatmapLevel.");
                }

                beatmapLevel = tuple.Value.Item1;
            }

            BeatmapCharacteristicSO characteristic =
                _customLevelLoader._beatmapCharacteristicCollection.GetBeatmapCharacteristicBySerializedName(path);
            BeatmapKey beatmapKey = new(beatmapLevel.levelID, characteristic, (BeatmapDifficulty)map.Difficulty);
#else
            CustomPreviewBeatmapLevel beatmapLevel;
            if (_songCoreLoad != null)
            {
                beatmapLevel = SongCoreLoad(path);
            }
            else
            {
                StandardLevelInfoSaveData infoSaveData =
                    await _customLevelLoader.LoadCustomLevelInfoSaveDataAsync(path, token);
                beatmapLevel = await _customLevelLoader.LoadCustomPreviewBeatmapLevelAsync(
                    path,
                    infoSaveData,
                    token);
            }

            _downloadProgress = 0.99f;
            CustomBeatmapLevel customBeatmapLevel =
                await _customLevelLoader.LoadCustomBeatmapLevelAsync(beatmapLevel, token);
            IDifficultyBeatmapSet set =
                customBeatmapLevel.beatmapLevelData.GetDifficultyBeatmapSet(map.Characteristic);
            IDifficultyBeatmap difficultyBeatmap =
                set.difficultyBeatmaps.First(n => (int)n.difficulty == map.Difficulty);
#endif

            _log.Debug($"Successfully downloaded [{map.Name}] as [{beatmapLevel.levelID}]");
            _downloadProgress = 1;

            DownloadedMap downloadedMap = new(
                index,
                map,
#if LATEST
                beatmapKey,
                beatmapLevel);
#else
                difficultyBeatmap,
                beatmapLevel);
#endif
            _beatmapLevel = downloadedMap;
            MapDownloadedBacking?.Invoke(downloadedMap);
            MapDownloadedOnceBacking?.Invoke(downloadedMap);
            MapDownloadedOnceBacking = null;
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
