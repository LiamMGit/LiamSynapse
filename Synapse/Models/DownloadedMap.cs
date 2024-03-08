namespace Synapse.Models
{
    public readonly struct DownloadedMap
    {
        public DownloadedMap(int index, Map map, IDifficultyBeatmap difficultyBeatmap, IPreviewBeatmapLevel previewBeatmapLevel)
        {
            Index = index;
            Map = map;
            DifficultyBeatmap = difficultyBeatmap;
            PreviewBeatmapLevel = previewBeatmapLevel;
        }

        public int Index { get; }

        public Map Map { get; }

        public IDifficultyBeatmap DifficultyBeatmap { get; }

        public IPreviewBeatmapLevel PreviewBeatmapLevel { get; }
    }
}
