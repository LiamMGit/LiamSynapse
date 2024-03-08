using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Synapse.HarmonyPatches
{
    [HarmonyPatch]
    internal static class AccuracyHelper
    {
        internal static float Accuracy { get; private set; }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LevelCompletionResultsHelper), nameof(LevelCompletionResultsHelper.Create))]
        private static void CalculateAccuracy(IReadonlyBeatmapData beatmapData, BeatmapObjectExecutionRating[] beatmapObjectExecutionRatings)
        {
            IEnumerable<NoteData> noteDatas = beatmapData.GetBeatmapDataItems<NoteData>(0);
            IEnumerable<SliderData> sliderDatas = beatmapData.GetBeatmapDataItems<SliderData>(0);
            List<ScoreModel.NoteScoreDefinition> list = new(1000);
            list.AddRange(from n in noteDatas
                where n.scoringType != NoteData.ScoringType.Ignore &&
                      n.scoringType != NoteData.ScoringType.NoScore
                select ScoreModel.GetNoteScoreDefinition(n.scoringType));
            int totalMaxScore = list.Sum(scoreDefinition => scoreDefinition.maxCutScore);

            int burstSliderScore = ScoreModel.GetNoteScoreDefinition(NoteData.ScoringType.BurstSliderElement).maxCutScore;
            int slices = sliderDatas.Where(n => n.sliderType != SliderData.Type.Burst).Sum(n => n.sliceCount - 1);
            totalMaxScore += burstSliderScore * slices;

            int totalScore = 0;
            foreach (BeatmapObjectExecutionRating beatmapObjectExecutionRating in beatmapObjectExecutionRatings)
            {
                if (beatmapObjectExecutionRating is NoteExecutionRating noteExecutionRating)
                {
                    totalScore += noteExecutionRating.cutScore;
                }
            }

            Accuracy = (float)totalScore / totalMaxScore;
        }
    }
}
