using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;

namespace Synapse.Controllers;

internal class PrefabSyncController : MonoBehaviour
{
    private AudioMixerGroup _audioMixerGroup = null!;
    private AudioSource[] _audioSources = null!;
    private Config _config = null!;

    private bool _mute;

    private void Awake()
    {
        _audioSources = GetComponentsInChildren<AudioSource>(true);
    }

    [UsedImplicitly]
    [Inject]
    private void Construct(SongPreviewPlayer songPreviewPlayer, Config config)
    {
        _audioMixerGroup = songPreviewPlayer._audioSourcePrefab.outputAudioMixerGroup;
        _config = config;
    }

    private void Start()
    {
        foreach (AudioSource audioSource in _audioSources)
        {
            audioSource.outputAudioMixerGroup = _audioMixerGroup;
        }
    }

    private void Update()
    {
        if (_config.MuteMusic == _mute)
        {
            return;
        }

        _mute = _config.MuteMusic;
        foreach (AudioSource audioSource in _audioSources)
        {
            audioSource.mute = _mute;
        }
    }
}
