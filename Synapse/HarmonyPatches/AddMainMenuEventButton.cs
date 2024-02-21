using SiraUtil.Affinity;
using Synapse.Managers;

namespace Synapse.HarmonyPatches
{
    internal class AddMainMenuEventButton : IAffinity
    {
        private readonly PromoManager _promoManager;

        private AddMainMenuEventButton(PromoManager promoManager)
        {
            _promoManager = promoManager;
        }

        [AffinityPrefix]
        [AffinityPatch(typeof(MainMenuViewController), nameof(MainMenuViewController.DidActivate))]
        private void DidActivate(MainMenuViewController __instance, bool firstActivation)
        {
            if (firstActivation)
            {
                __instance.buttonBinder.AddBinding(_promoManager.Button, () => __instance.HandleMenuButton((MainMenuViewController.MenuButton)13));
            }
        }
    }
}
