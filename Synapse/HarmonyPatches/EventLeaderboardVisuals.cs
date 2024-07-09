using System.Collections.Generic;
using HarmonyLib;
using HMUI;
using Synapse.Views;
using TMPro;
#if !V1_29_1
using UnityEngine;
#endif

namespace Synapse.HarmonyPatches
{
    [HarmonyPatch]
    internal static class EventLeaderboardVisuals
    {
        internal static string FormatPercentage(float percentage)
        {
            float percent = percentage * 100;
            return $"<color=#FEA959>{percent:F2}<size=60%>%</size></color>";
        }

#if !V1_29_1
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LeaderboardTableCell), nameof(LeaderboardTableCell.score), MethodType.Setter)]
        private static void ExpandScore(TextMeshProUGUI ____scoreText)
        {
            Vector2 old = ____scoreText.rectTransform.sizeDelta;
            ____scoreText.rectTransform.sizeDelta = new Vector2(100, old.y);
        }
#endif

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
            if (eventScore.Percentage >= 0)
            {
                cell._scoreText.text = $"{FormatPercentage(eventScore.Percentage)}    {cell._scoreText.text}";
                cell._scoreText.color = cell._normalColor;
            }
        }
    }
}
