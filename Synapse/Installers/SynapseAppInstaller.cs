using JetBrains.Annotations;
using Synapse.Extras;
using Synapse.HarmonyPatches;
using Synapse.Managers;
using Zenject;

namespace Synapse.Installers;

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
        Container.BindInterfacesAndSelfTo<DynamicDisposableManager>().AsSingle();
        Container
            .Bind<CancellationTokenManager>()
            .AsTransient()
            .OnInstantiated<CancellationTokenManager>(
                (context, obj) => context.Container.Resolve<DynamicDisposableManager>().Add(obj));

        Container.BindInstance(_config);
        Container.BindInterfacesAndSelfTo<RainbowTicker>().AsSingle();
        Container.Bind<RainbowString>().AsTransient();
        Container.BindInterfacesAndSelfTo<NetworkManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<ListingManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<MessageManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<FinishManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<TimeSyncManager>().AsSingle();

        Container.BindInterfacesAndSelfTo<NoEnergyModifier>().AsSingle();

#if DEBUG
        Container.BindInterfacesTo<TestScoreManager>().AsSingle();
        Container.BindInterfacesTo<TestMessageManager>().AsSingle();
#endif
    }
}
