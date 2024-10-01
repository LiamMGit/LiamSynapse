using System.Collections.Generic;
using Synapse.Networking.Models;

namespace Synapse.Models;

public readonly struct DownloadedMap
{
    public DownloadedMap(
        int index,
        Map map,
#if !PRE_V1_37_1
        List<BeatmapKey> beatmapKeys,
        BeatmapLevel beatmapLevel)
#else
        List<IDifficultyBeatmap> difficultyBeatmaps,
        IPreviewBeatmapLevel beatmapLevel)
#endif
    {
        Index = index;
        Map = map;
#if !PRE_V1_37_1
        BeatmapKeys = beatmapKeys;
#else
        DifficultyBeatmaps = difficultyBeatmaps;
#endif
        BeatmapLevel = beatmapLevel;
    }

    public int Index { get; }

    public Map Map { get; }

#if !PRE_V1_37_1
    public List<BeatmapKey> BeatmapKeys { get; }

    public BeatmapLevel BeatmapLevel { get; }
#else
    public List<IDifficultyBeatmap> DifficultyBeatmaps { get; }

    public IPreviewBeatmapLevel BeatmapLevel { get; }
#endif
}
