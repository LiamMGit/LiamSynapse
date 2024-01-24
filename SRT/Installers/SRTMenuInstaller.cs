using System;
using HarmonyLib;
using JetBrains.Annotations;
using SRT.HarmonyPatches;
using SRT.Managers;
using SRT.Views;
using Zenject;

namespace SRT.Installers
{
    [UsedImplicitly]
    internal class SRTMenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            if (IPA.Loader.PluginManager.GetPlugin("Heck") != null)
            {
                Container.Bind<HeckIntegrationManager>().AsSingle();
            }

            Container.BindInterfacesTo<AddEventFlowCoordinator>().AsSingle();
            Container.BindInterfacesTo<AddMainMenuEventButton>().AsSingle();

            Container.Bind<EventFlowCoordinator>().FromFactory<EventFlowCoordinator.EventFlowCoordinatorFactory>();
            Container.Bind<EventModsViewController>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<EventModsDownloadingViewController>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<EventLoadingViewController>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<EventLobbyViewController>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<EventMapDownloadingViewController>().FromNewComponentAsViewController().AsSingle();

            Container.Bind<LevelStartManager>().AsSingle();

            Container.BindInterfacesAndSelfTo<PromoManager>().AsSingle();
            Container.Bind<NotificationManager>().FromFactory<NotificationManager.NotificationManagerFactory>().NonLazy();
        }
    }
}
