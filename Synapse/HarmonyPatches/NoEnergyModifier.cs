using SiraUtil.Affinity;

namespace Synapse.HarmonyPatches;

internal class NoEnergyModifier : IAffinity
{
    public bool NoEnergyNextMap { get; set; }

    [AffinityPrefix]
    [AffinityPatch(typeof(GameEnergyCounter), nameof(GameEnergyCounter.Start))]
    private void InitDataSetter(ref GameEnergyCounter.InitData ____initData)
    {
        bool nofail = NoEnergyNextMap || ____initData.noFail;
        NoEnergyNextMap = false;
        ____initData = new GameEnergyCounter.InitData(
            ____initData.energyType,
            nofail,
            ____initData.instaFail,
            ____initData.failOnSaberClash);
    }
}
