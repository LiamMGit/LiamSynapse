using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;

namespace Synapse.Controllers
{
    internal class PrefabSyncController : MonoBehaviour
    {
        private AudioSource[] _audioSources = null!;
        private AudioMixerGroup _audioMixerGroup = null!;
        private Config _config = null!;

        private bool _mute;

        [UsedImplicitly]
        [Inject]
        private void Construct(SongPreviewPlayer songPreviewPlayer, Config config)
        {
            _audioMixerGroup = songPreviewPlayer._audioSourcePrefab.outputAudioMixerGroup;
            _config = config;
        }

        private void Awake()
        {
            _audioSources = GetComponentsInChildren<AudioSource>();
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
                // TODO: find a better way to mute audio sources
                audioSource.volume = _mute ? 0 : 1;
            }
        }
    }
}
