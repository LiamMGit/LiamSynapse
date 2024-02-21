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
            if (scoreData is EventLeaderboardViewController.EventScoreData { Color: not null } eventScore)
            {
                ((LeaderboardTableCell)__result)._playerNameText.color *= eventScore.Color.Value;
            }
        }
    }
}
