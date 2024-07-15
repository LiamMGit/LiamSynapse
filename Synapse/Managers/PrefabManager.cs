using System;
using System.IO;
using System.Linq;
using System.Threading;
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

internal class PrefabManager
{
    private static readonly int _death = Animator.StringToHash("death");

    private static readonly string _folder =
        (Path.GetDirectoryName(Application.streamingAssetsPath) ?? throw new InvalidOperationException()) +
        $"{Path.DirectorySeparatorChar}Synapse{Path.DirectorySeparatorChar}Bundles";

    private readonly CancellationTokenManager _cancellationTokenManager;
    private readonly IInstantiator _instantiator;

    private readonly SiraLog _log;
    private readonly MenuEnvironmentManager _menuEnvironmentManager;
    private readonly SongPreviewPlayer _songPreviewPlayer;
    private bool _active;
    private Animator? _animator;

    private BundleInfo? _bundleInfo;
    private AnimatorDeathController? _deathController;

    private bool? _didLoadSucceed;
    private string _filePath = string.Empty;

    private GameObject? _prefab;

    [UsedImplicitly]
    private PrefabManager(
        SiraLog log,
        IInstantiator instantiator,
        ListingManager listingManager,
        MenuEnvironmentManager menuEnvironmentManager,
        SongPreviewPlayer songPreviewPlayer,
        CancellationTokenManager cancellationTokenManager)
    {
        _log = log;
        _instantiator = instantiator;
        _menuEnvironmentManager = menuEnvironmentManager;
        _songPreviewPlayer = songPreviewPlayer;
        _cancellationTokenManager = cancellationTokenManager;
        listingManager.ListingFound += n =>
        {
            if (_prefab != null)
            {
                Object.Destroy(_prefab);
                _prefab = null;
            }

            _bundleInfo = n?.Bundles.FirstOrDefault(b => b.GameVersion.MatchesGameVersion());
            string listingTitle = n == null
                ? "undefined"
                : new string(
                    n
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
        };
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

    internal float DownloadProgress { get; private set; }

    internal async Task Download()
    {
        if (_prefab != null)
        {
            return;
        }

        CancellationToken token = _cancellationTokenManager.Reset();

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
            await www.SendAndVerify(n => DownloadProgress = n * 0.98f, token);
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
            _didLoadSucceed = true;
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

        if (_animator == null || _deathController == null)
        {
            _prefab.SetActive(false);
            _menuEnvironmentManager.ShowEnvironmentType(MenuEnvironmentManager.MenuEnvironmentType.Default);
            _songPreviewPlayer.CrossfadeToDefault();
        }
        else
        {
            _animator.SetTrigger(_death);
            _deathController.ContinueAfterDecay(
                10,
                () =>
                {
                    _prefab.SetActive(false);
                    _menuEnvironmentManager.ShowEnvironmentType(MenuEnvironmentManager.MenuEnvironmentType.Default);
                    _songPreviewPlayer.CrossfadeToDefault();
                });
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
        if (prefabNames.Length > 0)
        {
            _log.Warn($"More than one asset found in assetbundle, using first [{prefabNames[0]}]");
        }

        GameObject obj = await bundle.LoadAssetAsyncTask<GameObject>(prefabNames[0]);
        _prefab = Object.Instantiate(obj);
        if (_active)
        {
            _active = false;
            Show();
        }
        else
        {
            _prefab.SetActive(false);
        }

        _instantiator.InstantiateComponent<PrefabSyncController>(_prefab);
        _animator = _prefab.GetComponent<Animator>();
        bundle.Unload(false);
        if (_animator == null)
        {
            _log.Error("No animator on prefab");
        }
        else if (_animator.parameters.All(n => n.nameHash != _death))
        {
            _log.Error("No death trigger on animator");
        }
        else
        {
            _deathController = _prefab.AddComponent<AnimatorDeathController>();
            return;
        }

        _deathController = null;
    }
}
