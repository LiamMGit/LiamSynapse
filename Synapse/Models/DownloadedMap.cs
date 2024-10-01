using System.Collections.Generic;
using Synapse.Networking.Models;

namespace Synapse.Models;

public readonly struct DownloadedMap
{
    public DownloadedMap(
        int index,
        Map map,
#if LATEST
        List<BeatmapKey> beatmapKeys,
        BeatmapLevel beatmapLevel)
#else
        List<IDifficultyBeatmap> difficultyBeatmaps,
        IPreviewBeatmapLevel beatmapLevel)
#endif
    {
        Index = index;
        Map = map;
#if LATEST
        BeatmapKeys = beatmapKeys;
#else
        DifficultyBeatmaps = difficultyBeatmaps;
#endif
        BeatmapLevel = beatmapLevel;
    }

    public int Index { get; }

    public Map Map { get; }

#if LATEST
    public List<BeatmapKey> BeatmapKeys { get; }

    public BeatmapLevel BeatmapLevel { get; }
#else
    public List<IDifficultyBeatmap> DifficultyBeatmaps { get; }

    public IPreviewBeatmapLevel BeatmapLevel { get; }
#endif
}
