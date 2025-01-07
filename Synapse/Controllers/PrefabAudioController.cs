using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;

namespace Synapse.Controllers;

internal abstract class PrefabAudioController : MonoBehaviour
{
    private AudioMixerGroup _audioMixerGroup = null!;

    protected Config? Config { get; private set; }

    protected AudioSource[] AudioSources { get; private set; } = null!;

    protected abstract void OnConfigUpdated();

    [UsedImplicitly]
    [Inject]
    private void Construct(Config config, SongPreviewPlayer songPreviewPlayer)
    {
        _audioMixerGroup = songPreviewPlayer._audioSourcePrefab.outputAudioMixerGroup;
        Config = config;
    }

    private void Awake()
    {
        AudioSources = GetComponentsInChildren<AudioSource>(true);
    }

    private void OnEnable()
    {
        if (Config != null)
        {
            Config.Updated += OnConfigUpdated;
        }
    }

    private void OnDisable()
    {
        if (Config != null)
        {
            Config.Updated -= OnConfigUpdated;
        }
    }

    private void Start()
    {
        foreach (AudioSource audioSource in AudioSources)
        {
            audioSource.outputAudioMixerGroup = _audioMixerGroup;
        }

        OnConfigUpdated();
    }
}
