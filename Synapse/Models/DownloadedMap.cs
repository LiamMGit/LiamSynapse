namespace Synapse.Models;

public readonly struct DownloadedMap
{
    public DownloadedMap(
        int index,
        Map map,
#if LATEST
        in BeatmapKey beatmapKey,
        BeatmapLevel beatmapLevel)
#else
        IDifficultyBeatmap difficultyBeatmap,
        IPreviewBeatmapLevel beatmapLevel)
#endif
    {
        Index = index;
        Map = map;
#if LATEST
        BeatmapKey = beatmapKey;
#else
        DifficultyBeatmap = difficultyBeatmap;
#endif
        BeatmapLevel = beatmapLevel;
    }

    public int Index { get; }

    public Map Map { get; }

#if LATEST
    public BeatmapKey BeatmapKey { get; }

    public BeatmapLevel BeatmapLevel { get; }
#else
    public IDifficultyBeatmap DifficultyBeatmap { get; }

    public IPreviewBeatmapLevel BeatmapLevel { get; }
#endif
}
