using System;
using HarmonyLib;
using IPA;
using IPA.Config.Stores;
using JetBrains.Annotations;
using SiraUtil.Zenject;
using Synapse.Installers;
using UnityEngine;
using Logger = IPA.Logging.Logger;

namespace Synapse;

[Plugin(RuntimeOptions.DynamicInit)]
internal class Plugin
{
    private readonly Harmony _harmonyInstance = new("dev.aeroluna.Synapse");

    [UsedImplicitly]
    [Init]
    public Plugin(Logger pluginLogger, IPA.Config.Config conf, Zenjector zenjector)
    {
        Log = pluginLogger;

        zenjector.Install<SynapseAppInstaller>(Location.App, conf.Generated<Config>());
        zenjector.Install<SynapseMenuInstaller>(Location.Menu);
        zenjector.Install<SynapsePlayerInstaller>(Location.Player);
        zenjector.UseLogger(pluginLogger);

        string ver = Application.version;
        GameVersion = ver.Remove(ver.IndexOf("_", StringComparison.Ordinal));
    }

    internal static string GameVersion { get; private set; } = string.Empty;

    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    internal static Logger Log { get; private set; } = null!;

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
