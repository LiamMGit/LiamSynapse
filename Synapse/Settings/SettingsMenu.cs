using System;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Settings;
using JetBrains.Annotations;
using Zenject;

namespace Synapse.Settings;

internal class SettingsMenu : IInitializable, IDisposable
{
    private readonly Config _config;
    private readonly BSMLSettings _bsmlSettings;

    // i wish nico backported the bsml updates :(
    private SettingsMenu(
#if !V1_29_1
        BSMLSettings bsmlSettings,
#endif
        Config config)
    {
        _config = config;
#if !V1_29_1
        _bsmlSettings = bsmlSettings;
#else
        _bsmlSettings = BSMLSettings.instance;
#endif
    }

    public void Initialize()
    {
        _bsmlSettings.AddSettingsMenu("Synapse", "Synapse.Resources.Settings.bsml", this);
    }

    public void Dispose()
    {
        _bsmlSettings.RemoveSettingsMenu(this);
    }

#pragma warning disable CA1822
    [UsedImplicitly]
    [UIValue("menu-takeover")]
#pragma warning disable SA1201
    public bool TechnicolorEnabled
#pragma warning restore SA1201
    {
        get => _config.DisableMenuTakeover;
        set => _config.DisableMenuTakeover = value;
    }
#pragma warning restore CA1822
}
