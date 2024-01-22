using JetBrains.Annotations;
using SRT.HarmonyPatches;
using SRT.Views;
using Zenject;

namespace SRT.Installers
{
    [UsedImplicitly]
    internal class SRTPlayerInstaller : Installer
    {
        public override void InstallBindings()
        {
            if (!EventFlowCoordinator.IsActive)
            {
                return;
            }

            Container.BindInterfacesTo<PauseHijack>().AsSingle();
        }
    }
}
