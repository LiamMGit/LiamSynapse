using SiraUtil.Affinity;
using Synapse.Managers;

namespace Synapse.HarmonyPatches;

internal class AddMainMenuEventButton : IAffinity
{
    private readonly ListingManager _listingManager;
    private readonly PromoManager _promoManager;

    private AddMainMenuEventButton(PromoManager promoManager, ListingManager listingManager)
    {
        _promoManager = promoManager;
        _listingManager = listingManager;
    }

    [AffinityPrefix]
    [AffinityPatch(typeof(MainMenuViewController), nameof(MainMenuViewController.DidActivate))]
    private void DidActivate(MainMenuViewController __instance, bool firstActivation)
    {
        if (firstActivation)
        {
            __instance.buttonBinder.AddBinding(
                _promoManager.Button,
                () => __instance.HandleMenuButton((MainMenuViewController.MenuButton)13));
        }

        _listingManager.Initialize();
    }
}
