using JetBrains.Annotations;
using Synapse.HarmonyPatches;
using Synapse.Managers;
using Synapse.Views;
using Zenject;

namespace Synapse.Installers
{
    [UsedImplicitly]
    internal class SynapsePlayerInstaller : Installer
    {
        public override void InstallBindings()
        {
            if (!EventFlowCoordinator.IsActive)
            {
                return;
            }

            Container.BindInterfacesTo<PauseHijack>().AsSingle();
            Container.Bind<QuitLevelManager>().AsSingle().NonLazy();
        }
    }
}
