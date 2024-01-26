using HarmonyLib;
using IPA;
using IPA.Config.Stores;
using JetBrains.Annotations;
using SiraUtil.Zenject;
using SRT.Installers;
using SRT.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = IPA.Logging.Logger;

namespace SRT
{
    [Plugin(RuntimeOptions.DynamicInit)]
    internal class Plugin
    {
        private readonly Harmony _harmonyInstance = new("dev.aeroluna.SRT");

        internal static Logger Log { get; private set; } = null!;

        [UsedImplicitly]
        [Init]
        public Plugin(Logger pluginLogger, IPA.Config.Config conf, Zenjector zenjector)
        {
            Log = pluginLogger;

            zenjector.Install<SRTAppInstaller>(Location.App, conf.Generated<Config>());
            zenjector.Install<SRTMenuInstaller>(Location.Menu);
            zenjector.Install<SRTPlayerInstaller>(Location.Player);
            zenjector.UseLogger(pluginLogger);
        }

#pragma warning disable CA1822
        [UsedImplicitly]
        [OnEnable]
        public void OnEnable()
        {
            _harmonyInstance.PatchAll(typeof(Plugin).Assembly);
        }

        [UsedImplicitly]
        [OnDisable]
        public void OnDisable()
        {
            _harmonyInstance.UnpatchSelf();
        }
#pragma warning restore CA1822
    }
}
