using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SiraUtil.Logging;
using Synapse.Controllers;
using Synapse.Extras;
using Synapse.Models;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;
using Object = UnityEngine.Object;

namespace Synapse.Managers;

internal class MenuPrefabManager : IDisposable
{
    private static readonly int _death = Animator.StringToHash("death");

    private static readonly string _folder =
        (Path.GetDirectoryName(Application.streamingAssetsPath) ?? throw new InvalidOperationException()) +
        $"{Path.DirectorySeparatorChar}Synapse{Path.DirectorySeparatorChar}Bundles";

    private readonly CancellationTokenManager _cancellationTokenManager;
    private readonly IInstantiator _instantiator;
    private readonly ListingManager _listingManager;

    private readonly SiraLog _log;
    private readonly MenuEnvironmentManager _menuEnvironmentManager;
    private readonly SongPreviewPlayer _songPreviewPlayer;

    private bool _active;
    private BundleInfo? _bundleInfo;
    private AnimatorDeathController? _deathController;

    private bool? _didLoadSucceed;
    private ParticleSystem? _dustParticles;
    private string _filePath = string.Empty;

    private GameObject? _prefab;

    [UsedImplicitly]
    private MenuPrefabManager(
        SiraLog log,
        IInstantiator instantiator,
        ListingManager listingManager,
        MenuEnvironmentManager menuEnvironmentManager,
        SongPreviewPlayer songPreviewPlayer,
        CancellationTokenManager cancellationTokenManager)
    {
        _log = log;
        _instantiator = instantiator;
        _listingManager = listingManager;
        _menuEnvironmentManager = menuEnvironmentManager;
        _songPreviewPlayer = songPreviewPlayer;
        _cancellationTokenManager = cancellationTokenManager;
        listingManager.ListingFound += OnListingFound;
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

    internal uint LastHash { get; private set; }

    private ParticleSystem? DustParticles => _dustParticles ??=
        Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(n => n.name == "DustPS");

    public void Dispose()
    {
        _listingManager.ListingFound -= OnListingFound;
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
        if (!_active)
        {
            return;
        }

        _active = false;

        if (_prefab == null)
        {
            return;
        }

        DustParticles?.Play();
        _menuEnvironmentManager.ShowEnvironmentType(MenuEnvironmentManager.MenuEnvironmentType.Default);
        _songPreviewPlayer.CrossfadeToDefault();
        if (Animator == null || _deathController == null)
        {
            _prefab.SetActive(false);
        }
        else
        {
            Animator.SetTrigger(_death);
            _deathController.ContinueAfterDecay(
                10,
                () => { _prefab.SetActive(false); });
        }
    }

    internal void Show()
    {
        if (_active)
        {
            return;
        }

        _active = true;

        if (_prefab == null)
        {
            return;
        }

        if (_deathController != null && _deathController.enabled)
        {
            _prefab.SetActive(false);
            _deathController.enabled = false;
        }

        DustParticles?.Stop();
        _prefab.SetActive(true);
        _menuEnvironmentManager.ShowEnvironmentType(MenuEnvironmentManager.MenuEnvironmentType.None);
        _songPreviewPlayer.FadeOut(1);
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
        _prefab = Object.Instantiate(obj);

        _instantiator.InstantiateComponent<PrefabSyncController>(_prefab);
        Animator = _prefab.GetComponent<Animator>();
        bundle.Unload(false);
        if (Animator == null)
        {
            _log.Error("No animator on prefab");
        }
        else if (Animator.parameters.All(n => n.nameHash != _death))
        {
            _log.Error("No death trigger on animator");
        }
        else
        {
            _deathController = _prefab.AddComponent<AnimatorDeathController>();
        }

        _deathController = null;

        if (_active)
        {
            _active = false;
            Show();
        }
        else
        {
            _prefab.SetActive(false);
        }
    }

    private void OnListingFound(Listing? listing)
    {
        _bundleInfo = listing?.Bundles.FirstOrDefault(b => b.GameVersion.MatchesGameVersion());
        if (_bundleInfo == null)
        {
            _didLoadSucceed = null;
            if (_prefab == null)
            {
                return;
            }

            Object.Destroy(_prefab);
            _prefab = null;
            return;
        }

        if (_bundleInfo.Hash == LastHash)
        {
            return;
        }

        LastHash = _bundleInfo.Hash;

        _didLoadSucceed = null;
        if (_prefab != null)
        {
            Object.Destroy(_prefab);
            _prefab = null;
        }

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
