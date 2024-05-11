using SiraUtil.Affinity;
using Synapse.Managers;
using Synapse.Views;

namespace Synapse.HarmonyPatches
{
    internal class AddEventFlowCoordinator : IAffinity
    {
        private readonly MainFlowCoordinator _mainFlowCoordinator;
        private readonly EventFlowCoordinator _eventFlowCoordinator;
        private readonly PromoManager _promoManager;

        private AddEventFlowCoordinator(MainFlowCoordinator mainFlowCoordinator, EventFlowCoordinator eventFlowCoordinator, PromoManager promoManager)
        {
            _mainFlowCoordinator = mainFlowCoordinator;
            _eventFlowCoordinator = eventFlowCoordinator;
            _promoManager = promoManager;
        }

        [AffinityPrefix]
        [AffinityPatch(typeof(MainFlowCoordinator), nameof(MainFlowCoordinator.DidActivate))]
        private void DidActivate(bool addedToHierarchy)
        {
            if (addedToHierarchy)
            {
                _eventFlowCoordinator.Finished += HandleEventFlowCoordinatorDidFinish;
            }
        }

        [AffinityPrefix]
        [AffinityPatch(typeof(MainFlowCoordinator), nameof(MainFlowCoordinator.DidDeactivate))]
        private void DidDeactivate(bool removedFromHierarchy)
        {
            if (removedFromHierarchy)
            {
                _eventFlowCoordinator.Finished -= HandleEventFlowCoordinatorDidFinish;
            }
        }

        [AffinityPrefix]
        [AffinityPatch(typeof(MainFlowCoordinator), nameof(MainFlowCoordinator.HandleMainMenuViewControllerDidFinish))]
        private void HandleMainMenuViewControllerDidFinish(MainFlowCoordinator __instance, MainMenuViewController.MenuButton subMenuType)
        {
            if ((int)subMenuType == 13 && _promoManager.Active)
            {
                __instance.PresentFlowCoordinator(_eventFlowCoordinator);
            }
        }

        private void HandleEventFlowCoordinatorDidFinish(EventFlowCoordinator flowCoordinator)
        {
            _mainFlowCoordinator.DismissFlowCoordinator(flowCoordinator);
        }
    }
}
