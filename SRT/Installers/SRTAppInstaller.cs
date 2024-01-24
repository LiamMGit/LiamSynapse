using System;
using HarmonyLib;
using JetBrains.Annotations;
using SRT.HarmonyPatches;
using SRT.Managers;
using Zenject;

namespace SRT.Installers
{
    [UsedImplicitly]
    internal class SRTAppInstaller : Installer
    {
        private readonly Config _config;

        private SRTAppInstaller(Config config)
        {
            _config = config;
        }

        public override void InstallBindings()
        {
            Container.BindInstance(_config);
            Container.BindInterfacesAndSelfTo<NetworkManager>().AsSingle();
            Container.BindInterfacesAndSelfTo<ListingManager>().AsSingle();
            Container.BindInterfacesAndSelfTo<MessageManager>().AsSingle();

            Container.BindInterfacesAndSelfTo<NoEnergyModifier>().AsSingle();

#if DEBUG
            Container.BindInterfacesTo<TestScoreManager>().AsSingle();
            Container.BindInterfacesTo<TestMessageManager>().AsSingle();
#endif
        }
    }
}
