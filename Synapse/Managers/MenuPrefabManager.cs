using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SiraUtil.Logging;
using Synapse.Controllers;
using Synapse.Extras;
using Synapse.Networking.Models;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;
using Object = UnityEngine.Object;

namespace Synapse.Managers;

internal class MenuPrefabManager : IDisposable
{
    private static readonly string _folder =
        (Path.GetDirectoryName(Application.streamingAssetsPath) ?? throw new InvalidOperationException()) +
        $"{Path.DirectorySeparatorChar}Synapse{Path.DirectorySeparatorChar}Bundles";

    private static readonly int _eliminated = Animator.StringToHash("eliminated");

    private readonly CancellationTokenManager _cancellationTokenManager;
    private readonly IInstantiator _instantiator;
    private readonly ListingManager _listingManager;
    private readonly NetworkManager _networkManager;
    private readonly CameraDepthTextureManager _cameraDepthTextureManager;

    private readonly SiraLog _log;
    private readonly MenuEnvironmentManager _menuEnvironmentManager;
    private readonly SongPreviewPlayer _songPreviewPlayer;

    private bool _active;
    private bool _lastActive;
    private uint _lastHash;
    private LobbyInfo? _lobbyInfo;
    private BundleInfo? _bundleInfo;

    private bool? _didLoadSucceed;
    private ParticleSystem? _dustParticles;
    private string _filePath = string.Empty;

    private GameObject? _prefab;

    [UsedImplicitly]
    private MenuPrefabManager(
        SiraLog log,
        IInstantiator instantiator,
        ListingManager listingManager,
        NetworkManager networkManager,
        CameraDepthTextureManager cameraDepthTextureManager,
        MenuEnvironmentManager menuEnvironmentManager,
        SongPreviewPlayer songPreviewPlayer,
        CancellationTokenManager cancellationTokenManager)
    {
        _log = log;
        _instantiator = instantiator;
        _listingManager = listingManager;
        _networkManager = networkManager;
        _cameraDepthTextureManager = cameraDepthTextureManager;
        _menuEnvironmentManager = menuEnvironmentManager;
        _songPreviewPlayer = songPreviewPlayer;
        _cancellationTokenManager = cancellationTokenManager;
        listingManager.ListingFound += OnListingFound;
        networkManager.EliminatedUpdated += Refresh;
    }

    internal event Action<bool>? Loaded
    {
        add
        {
            if (_didLoadSucceed != null)
            {
                value?.Invoke(_didLoadSucceed.Value);
                return;
            }

            LoadedBacking += value;
        }

        remove => LoadedBacking -= value;
    }

    private event Action<bool>? LoadedBacking;

    internal Animator? Animator { get; private set; }

    internal float DownloadProgress { get; private set; }

    internal ParticleSystem? DustParticles => _dustParticles ??=
        Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(n => n.name == "DustPS");

    public void Dispose()
    {
        _listingManager.ListingFound -= OnListingFound;
        _networkManager.EliminatedUpdated -= Refresh;
    }

    internal void Reset(bool clearPrefab)
    {
        _didLoadSucceed = null;

        // ReSharper disable once InvertIf
        if (clearPrefab && _prefab != null)
        {
            Object.Destroy(_prefab);
            _prefab = null;
        }
    }

    internal async Task Download()
    {
        if (_prefab != null)
        {
            Invoke(true);
            return;
        }

        try
        {
            if (File.Exists(_filePath))
            {
                DownloadProgress = 0.99f;
                await LoadBundle();
                DownloadProgress = 1;
                Invoke(true);
                return;
            }

            string? url = _bundleInfo?.Url;
            if (string.IsNullOrWhiteSpace(url))
            {
                _log.Error("No bundle listed");
                Invoke(true);
                return;
            }

            _log.Debug($"Downloading lobby bundle from [{url}]");
            UnityWebRequest www = UnityWebRequest.Get(url);
            await www.SendAndVerify(n => DownloadProgress = n * 0.98f, _cancellationTokenManager.Reset());
            Directory.CreateDirectory(_folder);
            File.WriteAllBytes(_filePath, www.downloadHandler.data);
            DownloadProgress = 0.99f;
            await LoadBundle();
            DownloadProgress = 1;
            Invoke(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            _log.Error($"Exception while loading lobby bundle\n{e}");
            Invoke(false);
        }

        return;

        void Invoke(bool success)
        {
            _didLoadSucceed = success;
            LoadedBacking?.Invoke(success);
            LoadedBacking = null;
        }
    }

    internal void Hide()
    {
        _active = false;
        Refresh();
    }

    internal void HideParticles()
    {
        DustParticles?.Stop();
    }

    internal void Show()
    {
        _active = true;
        Refresh();
    }

    private async Task LoadBundle()
    {
        if (_bundleInfo == null)
        {
            throw new InvalidOperationException("No bundle info found.");
        }

        AssetBundle? bundle = await MediaExtensions.LoadFromFileAsync(_filePath, _bundleInfo.Hash);
        if (bundle == null)
        {
            FileInfo file = new(_filePath);
            if (file.Exists)
            {
                file.Delete();
            }

            throw new InvalidOperationException("Failed to load bundle.");
        }

        string[] prefabNames = bundle.GetAllAssetNames();
        if (prefabNames.Length > 1)
        {
            _log.Warn($"More than one asset found in assetbundle, using first [{prefabNames[0]}]");
        }

        GameObject obj = await bundle.LoadAssetAsyncTask<GameObject>(prefabNames[0]);
        _prefab = _instantiator.InstantiatePrefab(obj);
        _instantiator.InstantiateComponent<LobbyPrefabAudioController>(_prefab);
        Animator = _prefab.GetComponent<Animator>();
        bundle.Unload(false);
        if (Animator == null)
        {
            _log.Error("No animator on prefab");
        }

        _prefab.SetActive(_active);
    }

    private void Refresh()
    {
        if (_lastActive == _active)
        {
            return;
        }

        _lastActive = _active;
        _cameraDepthTextureManager.Enabled = _active;
        if (_active)
        {
            SetPrefabActive(false);
            SetPrefabActive(true);

            // None is actually used on 1.40 for some reason?
            _menuEnvironmentManager.ShowEnvironmentType((MenuEnvironmentManager.MenuEnvironmentType)99);
            if (_lobbyInfo?.DisableDust ?? false)
            {
                DustParticles?.Stop();
            }

            _songPreviewPlayer.FadeOut(1);
        }
        else
        {
            DustParticles?.Play();
            _menuEnvironmentManager.ShowEnvironmentType(MenuEnvironmentManager.MenuEnvironmentType.Default);
            _songPreviewPlayer.CrossfadeToDefault();
            SetPrefabActive(false);
        }

        if (Animator != null)
        {
            Animator.SetBool(_eliminated, _networkManager.Status.Stage is PlayStatus { Eliminated: true });
        }

        return;

        void SetPrefabActive(bool value)
        {
            if (_prefab != null)
            {
                _prefab.SetActive(value);
            }
        }
    }

    private void OnListingFound(Listing? listing)
    {
        _lobbyInfo = listing?.Lobby;
        _bundleInfo = _lobbyInfo?.Bundles.FirstOrDefault(b => b.GameVersion.MatchesGameVersion());
        if (_bundleInfo == null)
        {
            Reset(true);
            return;
        }

        if (_bundleInfo.Hash == _lastHash)
        {
            return;
        }

        _lastHash = _bundleInfo.Hash;

        _cameraDepthTextureManager.DepthTextureMode = (DepthTextureMode)_lobbyInfo!.DepthTextureMode;

        Reset(true);

        string listingTitle = listing == null
            ? "undefined"
            : new string(
                listing
                    .Title.Select(
                        j =>
                        {
                            if (char.IsLetter(j) || char.IsNumber(j))
                            {
                                return j;
                            }

                            return '_';
                        })
                    .ToArray());
        _filePath = Path.Combine(_folder, listingTitle);
    }
}
