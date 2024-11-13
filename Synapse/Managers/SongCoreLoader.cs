using System;
using System.Reflection;
using HarmonyLib;
using IPA.Loader;
using JetBrains.Annotations;
using Zenject;
#if PRE_V1_37_1
using System.Threading;
#else
using System.Collections.Concurrent;
#endif

namespace Synapse.Managers;

[UsedImplicitly]
internal class SongCoreLoader : IInitializable
{
    private object _loader = null!;

#if PRE_V1_37_1
    private MethodInfo _loadSongAndAddToDictionaries = null!;
    private MethodInfo _loadCustomLevelSongData = null!;
#else
#pragma warning disable SA1214
    private readonly CustomLevelLoader _customLevelLoader;
#pragma warning restore SA1214
    private MethodInfo _loadCustomLevel = null!;
    private ConcurrentDictionary<string, CustomLevelLoader.LoadedSaveData> _loadedBeatmapSaveData = null!;

    private SongCoreLoader(CustomLevelLoader customLevelLoader)
    {
        _customLevelLoader = customLevelLoader;
    }
#endif

    // could do zenject di in 1.35+ versions, but i dont wanna set up that up
    public void Initialize()
    {
        Assembly assembly = PluginManager.GetPlugin("SongCore").Assembly;
        Type loaderType = assembly.GetType("SongCore.Loader");
        PropertyInfo? instanceProperty = loaderType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
        if (instanceProperty == null)
        {
            throw new InvalidOperationException("Failed to get SongCore.Loader.Instance property");
        }

        _loader = instanceProperty.GetValue(null);

#if PRE_V1_37_1
        _loadSongAndAddToDictionaries = AccessTools.Method(loaderType, "LoadSongAndAddToDictionaries");
        _loadCustomLevelSongData = AccessTools.Method(loaderType, "LoadCustomLevelSongData");
#else
        _loadCustomLevel = AccessTools.Method(loaderType, "LoadCustomLevel");
        PropertyInfo? loadedBeatmapSaveDataProperty = loaderType.GetProperty("LoadedBeatmapSaveData", BindingFlags.Static | BindingFlags.NonPublic);
        if (loadedBeatmapSaveDataProperty == null)
        {
            throw new InvalidOperationException("Failed to get SongCore.Loader.Instance property");
        }

        _loadedBeatmapSaveData = (ConcurrentDictionary<string, CustomLevelLoader.LoadedSaveData>)loadedBeatmapSaveDataProperty.GetValue(null);
#endif
    }

#if PRE_V1_37_1
    // needed for other mods like BeatTogether/Camera2
    internal CustomPreviewBeatmapLevel Load(string songPath)
    {
        object? songData = _loadCustomLevelSongData.Invoke(_loader, [songPath]);
        if (songData == null)
        {
            throw new InvalidOperationException("SongCore error: invalid song data.");
        }

        object? result = _loadSongAndAddToDictionaries.Invoke(
            _loader,
            [CancellationToken.None, songData, songPath, null]);
        if (result == null)
        {
            throw new InvalidOperationException("SongCore error: failed to load.");
        }

        return (CustomPreviewBeatmapLevel)result;
    }
#else
    internal BeatmapLevel Load(string songPath)
    {
        (string, BeatmapLevel) customLevel = ((string, BeatmapLevel)?)_loadCustomLevel.Invoke(_loader, [songPath]) ??
                                             throw new InvalidOperationException("SongCore error: failed to load.");
        string levelId = customLevel.Item2.levelID;
        if (!_loadedBeatmapSaveData.TryGetValue(
                levelId,
                out CustomLevelLoader.LoadedSaveData loadedSaveData))
        {
            throw new InvalidOperationException("SongCore error: failed to get loadedSaveData.");
        }

        _customLevelLoader._loadedBeatmapSaveData[levelId] = loadedSaveData;
        return customLevel.Item2;
    }
#endif
}
