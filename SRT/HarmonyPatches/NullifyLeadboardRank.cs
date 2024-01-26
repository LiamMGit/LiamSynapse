using HarmonyLib;
using JetBrains.Annotations;
using SiraUtil.Affinity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SRT.HarmonyPatches
{
    [HarmonyPatch(typeof(LeaderboardTableCell), nameof(LeaderboardTableCell.rank), MethodType.Setter)]
    internal class NullifyLeaderboardRank
    {
        [UsedImplicitly]
        [HarmonyPrefix]
        private static bool NullifyRank(int value, TextMeshProUGUI ____rankText)
        {
            ____rankText.text = value > 0 ? value.ToString() : string.Empty;
            return false;
        }
    }
}
