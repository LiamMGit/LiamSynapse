using System.Collections.Generic;
using HarmonyLib;
using HMUI;
using Synapse.Views;
using TMPro;

namespace Synapse.HarmonyPatches
{
    [HarmonyPatch]
    internal static class EventLeaderboardVisuals
    {
        internal static string FormatAccuracy(float accuracy)
        {
            float percent = accuracy * 100;
            return $"<color=#FEA959>{percent:F2}<size=60%>%</size></color>";
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LeaderboardTableCell), nameof(LeaderboardTableCell.rank), MethodType.Setter)]
        private static bool NullifyRank(int value, TextMeshProUGUI ____rankText)
        {
            ____rankText.text = value > 0 ? value.ToString() : string.Empty;
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LeaderboardTableView), nameof(LeaderboardTableView.CellForIdx))]
        private static void Colorize(
            TableCell __result,
            int row,
            List<LeaderboardTableView.ScoreData> ____scores)
        {
            LeaderboardTableView.ScoreData scoreData = ____scores[row];
            if (scoreData is not EventLeaderboardViewController.EventScoreData eventScore)
            {
                return;
            }

            LeaderboardTableCell cell = (LeaderboardTableCell)__result;
            if (eventScore.Color != null)
            {
                cell._playerNameText.color = eventScore.Color.Value;
            }

            // ReSharper disable once InvertIf
            if (eventScore.Accuracy >= 0)
            {
                cell._scoreText.text = $"{FormatAccuracy(eventScore.Accuracy)}    {cell._scoreText.text}";
                cell._scoreText.color = cell._normalColor;
            }
        }
    }
}
