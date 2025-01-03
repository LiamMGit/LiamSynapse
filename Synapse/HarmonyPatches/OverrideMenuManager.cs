using SiraUtil.Affinity;
using Synapse.Managers;

namespace Synapse.HarmonyPatches;

internal class OverrideMenuManager : IAffinity
{
    private readonly MenuTakeoverManager _menuTakeoverManager;

    private OverrideMenuManager(MenuTakeoverManager menuTakeoverManager)
    {
        _menuTakeoverManager = menuTakeoverManager;
    }

    [AffinityPrefix]
    [AffinityPatch(typeof(MenuEnvironmentManager), nameof(MenuEnvironmentManager.ShowEnvironmentType))]
    private void EnableMenuTakeover(MenuEnvironmentManager.MenuEnvironmentType menuEnvironmentType)
    {
        _menuTakeoverManager.Enabled = menuEnvironmentType == MenuEnvironmentManager.MenuEnvironmentType.Default;
    }
}
