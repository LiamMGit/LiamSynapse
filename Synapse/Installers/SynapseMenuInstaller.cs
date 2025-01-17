using IPA.Loader;
using JetBrains.Annotations;
using Synapse.HarmonyPatches;
using Synapse.Managers;
using Synapse.Settings;
using Synapse.Views;
using Zenject;

namespace Synapse.Installers;

[UsedImplicitly]
internal class SynapseMenuInstaller : Installer
{
    public override void InstallBindings()
    {
        if (PluginManager.GetPlugin("Heck") != null)
        {
            Container.Bind<HeckIntegrationManager>().AsSingle();
        }

        if (PluginManager.GetPlugin("SongCore") != null)
        {
            Container.BindInterfacesAndSelfTo<SongCoreLoader>().AsSingle();
        }

        Container.BindInterfacesTo<AddEventFlowCoordinator>().AsSingle();
        Container.BindInterfacesTo<AddMainMenuEventButton>().AsSingle();

        Container.Bind<EventFlowCoordinator>().FromFactory<EventFlowCoordinator.EventFlowCoordinatorFactory>();
        Container.Bind<EventDivisionSelectViewController>().FromNewComponentAsViewController().AsSingle();
        Container.Bind<EventIntroViewController>().FromNewComponentAsViewController().AsSingle();
        Container.Bind<EventModsViewController>().FromNewComponentAsViewController().AsSingle();
        Container.Bind<EventModsDownloadingViewController>().FromNewComponentAsViewController().AsSingle();
        Container.Bind<EventLoadingViewController>().FromNewComponentAsViewController().AsSingle();
        Container.Bind<EventLobbyNavigationViewController>().FromNewComponentAsViewController().AsSingle();
        Container.Bind<EventLobbyChatViewController>().FromNewComponentAsViewController().AsSingle();
        Container.Bind<EventLobbySongInfoViewController>().FromNewComponentAsViewController().AsSingle();
        Container.Bind<EventLeaderboardViewController>().FromNewComponentAsViewController().AsSingle();
        ////Container.Bind<EventMapDownloadingViewController>().FromNewComponentAsViewController().AsSingle();

        Container.BindInterfacesTo<SettingsMenu>().AsSingle();

        Container.BindInterfacesTo<OverrideMenuManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<MenuTakeoverManager>().AsSingle();

        Container.BindInterfacesAndSelfTo<MenuPrefabManager>().AsSingle();

        Container.BindInterfacesAndSelfTo<MapDownloadingManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<CountdownManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<LevelStartManager>().AsSingle();

        Container.BindInterfacesAndSelfTo<PromoManager>().AsSingle();
        Container.Bind<NotificationManager>().FromFactory<NotificationManager.NotificationManagerFactory>().NonLazy();

        Container.Bind<GlobalDustManager>().AsSingle();
        Container.Bind<GlobalDustManager.DustHold>().FromFactory<GlobalDustManager.DustHoldFactory>().AsTransient();
    }
}
