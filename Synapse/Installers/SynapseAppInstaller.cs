using JetBrains.Annotations;
using Synapse.Extras;
using Synapse.HarmonyPatches;
using Synapse.Managers;
using Zenject;

namespace Synapse.Installers
{
    [UsedImplicitly]
    internal class SynapseAppInstaller : Installer
    {
        private readonly Config _config;

        private SynapseAppInstaller(Config config)
        {
            _config = config;
        }

        public override void InstallBindings()
        {
            Container.BindInstance(_config);
            Container.BindInterfacesAndSelfTo<CancellationTokenManager>().AsTransient();
            Container.BindInterfacesAndSelfTo<NetworkManager>().AsSingle();
            Container.BindInterfacesAndSelfTo<ListingManager>().AsSingle();
            Container.BindInterfacesAndSelfTo<MessageManager>().AsSingle();
            Container.BindInterfacesAndSelfTo<PingManager>().AsSingle();

            Container.BindInterfacesAndSelfTo<NoEnergyModifier>().AsSingle();

#if DEBUG
            Container.BindInterfacesTo<TestScoreManager>().AsSingle();
            Container.BindInterfacesTo<TestMessageManager>().AsSingle();
#endif
        }
    }
}
