using HarmonyLib;
using HMUI;
using UnityEngine;

namespace Synapse.HarmonyPatches;

[HarmonyPatch]
internal class ScrollViewScrollToEnd : MonoBehaviour
{
    private GameObject _target = null!;

    internal void Construct(GameObject target)
    {
        _target = target;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ScrollView), nameof(ScrollView.RefreshButtons))]
    private static void RefreshScrollToEnd(ScrollView __instance)
    {
        __instance
            .GetComponent<ScrollViewScrollToEnd>()
            ?._target.SetActive(
                __instance._destinationPos < __instance.contentSize - __instance.scrollPageSize - 0.001f);
    }
}
