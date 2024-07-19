using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SiraUtil.Logging;
using Synapse.Extras;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;
using Object = UnityEngine.Object;

namespace Synapse.Managers;

internal class CountdownManager : ITickable, IInitializable, IDisposable
{
    private static readonly string _folder =
        Directory.CreateDirectory(
                (Path.GetDirectoryName(Application.streamingAssetsPath) ?? throw new InvalidOperationException()) +
                $"{Path.DirectorySeparatorChar}Synapse{Path.DirectorySeparatorChar}Countdown")
            .FullName;

    private readonly SiraLog _log;
    private readonly NetworkManager _networkManager;
    private readonly TimeSyncManager _timeSyncManager;
    private readonly SongPreviewPlayer _songPreviewPlayer;
    private AudioClip[] _audioClips = null!;
    private AudioSource _countAudioSource = null!;

    private AudioSource _gongAudioSource = null!;
    private bool _gongPlayed;
    private float _hue;

    private int _lastPlayed;
    private string _lastSent = string.Empty;

    // in local time
    private float _startTime;

    private CountdownManager(
        SiraLog log,
        NetworkManager networkManager,
        TimeSyncManager timeSyncManager,
        AudioClipAsyncLoader audioClipAsyncLoader,
        SongPreviewPlayer songPreviewPlayer)
    {
        _log = log;
        _networkManager = networkManager;
        _timeSyncManager = timeSyncManager;
        _songPreviewPlayer = songPreviewPlayer;
        networkManager.StartTimeUpdated += OnStartTimeUpdated;
        networkManager.Closed += OnClosed;

        _ = LoadAudio(audioClipAsyncLoader);
    }

    internal event Action<string>? CountdownUpdated;

    public void Initialize()
    {
        AudioMixerGroup outputAudioMixerGroup = _songPreviewPlayer._audioSourcePrefab.outputAudioMixerGroup;
        GameObject gameObject = new("CountdownAudio");
        Object.DontDestroyOnLoad(gameObject);
        AudioSource gong = gameObject.AddComponent<AudioSource>();
        gong.outputAudioMixerGroup = outputAudioMixerGroup;
        gong.volume = 0.4f;
        gong.playOnAwake = false;
        gong.bypassEffects = true;
        gong.clip = Resources.FindObjectsOfTypeAll<CountdownController>().First()._audioSource.clip;
        _gongAudioSource = gong;
        AudioSource count = gameObject.AddComponent<AudioSource>();
        count.outputAudioMixerGroup = outputAudioMixerGroup;
        count.volume = 0.4f;
        count.playOnAwake = false;
        count.bypassEffects = true;
        _countAudioSource = count;
    }

    public void Dispose()
    {
        _networkManager.StartTimeUpdated -= OnStartTimeUpdated;
        _networkManager.Closed -= OnClosed;
    }

    public void Tick()
    {
        TimeSpan diff = (_startTime - _timeSyncManager.ElapsedSeconds).ToTimeSpan();
        TimeSpan alteredDiff = diff + TimeSpan.FromSeconds(1);
        if ((int)alteredDiff.TotalHours > 0)
        {
            Send("Soon™");
        }
        else if ((int)alteredDiff.TotalMinutes > 0)
        {
            Send($"{alteredDiff.Minutes}:{alteredDiff.Seconds:D2}");
        }
        else if ((int)alteredDiff.TotalSeconds > 10)
        {
            Send($"{alteredDiff.Seconds}");
        }
        else if (diff.TotalSeconds > 0)
        {
            if (!_gongPlayed)
            {
                _gongAudioSource.Play();
                _gongPlayed = true;
            }

            int count = alteredDiff.Seconds - 1;
            if (count < _audioClips.Length && _lastPlayed != count)
            {
                _lastPlayed = count;
                _countAudioSource.clip = _audioClips[count];
                _countAudioSource.Play();
            }

            _hue = Mathf.Repeat(_hue + (0.6f * Time.deltaTime), 1);
            Color col = Color.HSVToRGB(Mathf.Repeat(_hue, 1), 0.8f, 1);

            Send($"<size=160%><color=#{ColorUtility.ToHtmlStringRGB(col)}>{alteredDiff.Seconds}");
        }
        else
        {
            _hue = Mathf.Repeat(_hue + (0.6f * Time.deltaTime), 1);
            Color col = Color.HSVToRGB(Mathf.Repeat(_hue, 1), 0.8f, 1);

            Send($"<size=160%><color=#{ColorUtility.ToHtmlStringRGB(col)}>Now");
        }
    }

    internal void Refresh()
    {
        _lastSent = string.Empty;
    }

    private async Task LoadAudio(AudioClipAsyncLoader audioClipAsyncLoader)
    {
        try
        {
            // idk how to read these from memory
            const string prefix = "Synapse.Resources.Countdown.";
            const int count = 5;
            Assembly assembly = typeof(CountdownManager).Assembly;
            _audioClips = new AudioClip[count];
            for (int i = 0; i < count; i++)
            {
                string fileName = $"{i + 1}.ogg";
                string audio = $"{prefix}{fileName}";
                string path = Path.Combine(_folder, fileName);
                if (!File.Exists(path))
                {
                    using Stream resource = assembly.GetManifestResourceStream(audio) ??
                                            throw new InvalidOperationException();
                    using FileStream file = new(path, FileMode.Create, FileAccess.Write);
                    await resource.CopyToAsync(file);
                }

                _audioClips[i] = await audioClipAsyncLoader.Load(path);
            }
        }
        catch (Exception e)
        {
            _log.Error($"Exception while loading countdown audio\n{e}");
        }
    }

    private void Send(string text)
    {
        if (text == _lastSent)
        {
            return;
        }

        _lastSent = text;
        CountdownUpdated?.Invoke(text);
    }

    private void OnStartTimeUpdated(float startTime)
    {
        _gongPlayed = false;
        _startTime = startTime - _timeSyncManager.Offset;
    }

    private void OnClosed(ClosedReason reason)
    {
        _startTime = float.MaxValue;
        _gongPlayed = false;
        _lastPlayed = -1;
    }
}
