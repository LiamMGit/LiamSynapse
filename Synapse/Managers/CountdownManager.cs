using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SiraUtil.Logging;
using Synapse.Extras;
using Synapse.Networking.Models;
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
    private readonly RainbowString _rainbowString;
    private readonly SongPreviewPlayer _songPreviewPlayer;
    private readonly TimeSyncManager _timeSyncManager;
    private AudioClip[] _audioClips = null!;
    private AudioSource _countAudioSource = null!;
    private AudioSource _gongAudioSource = null!;

    private bool _gongPlayed;

    private int _lastPlayed;
    private string _lastSent = string.Empty;
    private bool _levelStarted;

    private float _startTime;

    private CountdownManager(
        SiraLog log,
        NetworkManager networkManager,
        TimeSyncManager timeSyncManager,
        AudioClipAsyncLoader audioClipAsyncLoader,
        SongPreviewPlayer songPreviewPlayer,
        RainbowString rainbowString)
    {
        _log = log;
        _networkManager = networkManager;
        _timeSyncManager = timeSyncManager;
        _songPreviewPlayer = songPreviewPlayer;
        _rainbowString = rainbowString;
        networkManager.StartTimeUpdated += OnStartTimeUpdated;
        networkManager.Closed += OnClosed;

        _ = LoadAudio(audioClipAsyncLoader);
    }

    internal event Action<string>? CountdownUpdated;

    internal event Action? LevelStarted;

    private float StartTime
    {
        get => _startTime;
        set
        {
            _gongPlayed = false;
            _lastPlayed = -1;
            _startTime = value;
            _levelStarted = false;
        }
    }

    public void Dispose()
    {
        _networkManager.StartTimeUpdated -= OnStartTimeUpdated;
        _networkManager.Closed -= OnClosed;
    }

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

    public void Tick()
    {
        if (_networkManager.Status.Stage is not PlayStatus playStatus)
        {
            return;
        }

        if (_levelStarted)
        {
            Send($"<size=160%>{_rainbowString}");
            return;
        }

        TimeSpan diff = (StartTime - _timeSyncManager.SyncTime).ToTimeSpan();
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

            _rainbowString.SetString(alteredDiff.Seconds.ToString());
            Send($"<size=160%>{_rainbowString}");
        }
        else
        {
            _levelStarted = true;
            if (playStatus.PlayerScore == null ||
                (playStatus.Map.Ruleset?.AllowResubmission ?? false))
            {
                LevelStarted?.Invoke();
            }

            _rainbowString.SetString("Now");
            Send($"<size=160%>{_rainbowString}");
        }
    }

    internal void ManualStart()
    {
        _levelStarted = true;
        LevelStarted?.Invoke();
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

    private void OnClosed()
    {
        StartTime = float.MaxValue;
    }

    private void OnStartTimeUpdated(float startTime)
    {
        StartTime = startTime;
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
}
