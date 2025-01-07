using UnityEngine;

namespace Synapse.Controllers;

internal class LobbyPrefabAudioController : PrefabAudioController
{
    private bool _mute;

    protected override void OnConfigUpdated()
    {
        if (Config == null ||
            Config.DisableLobbyAudio == _mute)
        {
            return;
        }

        _mute = Config.DisableLobbyAudio;
        foreach (AudioSource audioSource in AudioSources)
        {
            audioSource.mute = _mute;
        }
    }
}
