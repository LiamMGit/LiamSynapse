using System;
using SiraUtil.Affinity;

namespace Synapse.HarmonyPatches;

internal class NoEnergyModifier : IAffinity
{
    public bool NoEnergyNextMap { get; set; }

    [AffinityPrefix]
    [AffinityPatch(typeof(GameEnergyCounter), nameof(GameEnergyCounter.Start))]
    private void InitDataSetter(ref GameEnergyCounter.InitData ____initData)
    {
        bool noFail = NoEnergyNextMap || ____initData.noFail;
        NoEnergyNextMap = false;
        ____initData = new GameEnergyCounter.InitData(
            ____initData.energyType,
            noFail,
            ____initData.instaFail,
            ____initData.failOnSaberClash);
    }

    [AffinityPostfix]
    [AffinityPatch(typeof(GameEnergyCounter), nameof(GameEnergyCounter.ProcessEnergyChange))]
    private void ProcessEnergyOnNoFail(
        GameEnergyCounter __instance,
        Action<float>? ___gameEnergyDidChangeEvent,
        float energyChange)
    {
        if (!__instance.noFail)
        {
            return;
        }

        __instance.energy = Math.Min(Math.Max(__instance.energy + energyChange, 0f), 1f);

        ___gameEnergyDidChangeEvent?.Invoke(__instance.energy);
    }
}
