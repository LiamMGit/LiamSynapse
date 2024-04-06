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

namespace Synapse.Managers
{
    internal class PrefabManager
    {
        private static readonly string _folder =
            (Path.GetDirectoryName(Application.streamingAssetsPath) ?? throw new InvalidOperationException()) +
            $"{Path.DirectorySeparatorChar}Synapse{Path.DirectorySeparatorChar}Bundles";

        private static readonly int _death = Animator.StringToHash("death");

        private readonly SiraLog _log;
        private readonly IInstantiator _instantiator;
        private readonly MenuEnvironmentManager _menuEnvironmentManager;
        private readonly CancellationTokenManager _cancellationTokenManager;

        private Listing? _listing;
        private string _filePath = string.Empty;

        private GameObject? _prefab;
        private Animator? _animator;
        private AnimatorDeathController? _deathController;

        private bool? _didLoadSucceed;
        private bool _active;

        [UsedImplicitly]
        private PrefabManager(
            SiraLog log,
            IInstantiator instantiator,
            ListingManager listingManager,
            MenuEnvironmentManager menuEnvironmentManager,
            CancellationTokenManager cancellationTokenManager)
        {
            _log = log;
            _instantiator = instantiator;
            _menuEnvironmentManager = menuEnvironmentManager;
            _cancellationTokenManager = cancellationTokenManager;
            listingManager.ListingFound += n =>
            {
                if (_prefab != null)
                {
                    Object.Destroy(_prefab);
                    _prefab = null;
                }

                _listing = n;
                string listingTitle = n == null ? "undefined" : new string(n.Title.Select(j =>
                {
                    if (char.IsLetter(j) || char.IsNumber(j))
                    {
                        return j;
                    }

                    return '_';
                }).ToArray());
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

                _loaded += value;
            }

            remove => _loaded -= value;
        }

        private event Action<bool>? _loaded;

        internal float DownloadProgress { get; private set; }

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
            }
            else
            {
                _animator.SetTrigger(_death);
                _deathController!.ContinueAfterDecay(10, () =>
                {
                    _prefab.SetActive(false);
                    _menuEnvironmentManager.ShowEnvironmentType(MenuEnvironmentManager.MenuEnvironmentType.Default);
                });
            }
        }

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
                    await LoadBundle();
                    Invoke(true);
                    return;
                }

                string? url = _listing?.LobbyBundle;
                if (string.IsNullOrWhiteSpace(url))
                {
                    _log.Error("No bundle listed");
                    Invoke(true);
                    return;
                }

                _log.Debug($"Downloading lobby bundle from [{url}]");
                UnityWebRequest www = UnityWebRequest.Get(url);
                await www.SendAndVerify(n => DownloadProgress = n, token);
                Directory.CreateDirectory(_folder);
                File.WriteAllBytes(_filePath, www.downloadHandler.data);
                await LoadBundle();
                Invoke(true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _log.Error($"Exception while loading lobby bundle: {e}");
                Invoke(false);
            }

            return;

            void Invoke(bool success)
            {
                _didLoadSucceed = true;
                _loaded?.Invoke(success);
                _loaded = null;
            }
        }

        private async Task LoadBundle()
        {
            DownloadProgress = 1;

            if (_listing == null)
            {
                throw new InvalidOperationException("No listing loaded");
            }

            AssetBundle bundle = await MediaExtensions.LoadFromFileAsync(_filePath, _listing.BundleCrc);

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
}
