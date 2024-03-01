namespace Synapse.Models
{
    public readonly struct DownloadedMap
    {
        public DownloadedMap(Map map, IDifficultyBeatmap difficultyBeatmap, IPreviewBeatmapLevel previewBeatmapLevel)
        {
            Map = map;
            DifficultyBeatmap = difficultyBeatmap;
            PreviewBeatmapLevel = previewBeatmapLevel;
        }

        public Map Map { get; }

        public IDifficultyBeatmap DifficultyBeatmap { get; }

        public IPreviewBeatmapLevel PreviewBeatmapLevel { get; }
    }
}
