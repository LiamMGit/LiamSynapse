using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SiraUtil.Logging;
using Synapse.Extras;
using Synapse.Models;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;

namespace Synapse.Managers
{
    internal class ListingManager : IInitializable
    {
        private readonly SiraLog _log;
        private readonly Config _config;
        private readonly CancellationTokenManager _cancellationTokenManager;
        private Sprite? _bannerImage;

        [UsedImplicitly]
        private ListingManager(SiraLog log, Config config, CancellationTokenManager cancellationTokenManager)
        {
            _log = log;
            _config = config;
            _cancellationTokenManager = cancellationTokenManager;
        }

        public event Action<Listing?>? ListingFound
        {
            add
            {
                if (Listing != null)
                {
                    value?.Invoke(Listing);
                }

                _listingFound += value;
            }

            remove => _listingFound -= value;
        }

        public event Action<Sprite?>? BannerImageCreated
        {
            add
            {
                if (_bannerImage != null)
                {
                    value?.Invoke(_bannerImage);
                }

                _bannerImageCreated += value;
            }

            remove => _bannerImageCreated -= value;
        }

        private event Action<Listing?>? _listingFound;

        private event Action<Sprite?>? _bannerImageCreated;

        public Listing? Listing { get; private set; }

        public void Initialize()
        {
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                CancellationToken token = _cancellationTokenManager.Reset();
                string url = _config.Url;
                _log.Debug($"Checking [{url}] for active listing");
                UnityWebRequest www = UnityWebRequest.Get(url);
                await www.SendAndVerify(token);

                string json = www.downloadHandler.text;
                Listing? listing = JsonConvert.DeserializeObject<Listing>(json, JsonSettings.Settings);
                if (listing == null)
                {
                    _log.Error("Error deserializing listing");
                    return;
                }

                _log.Debug($"Found active listing for [{listing.Title}]");

                Listing = listing;
                _listingFound?.Invoke(Listing);

                try
                {
                    _log.Debug($"Fetching banner image from [{listing.BannerImage}]");
                    Sprite bannerImage = await AsyncExtensions.RequestSprite(listing.BannerImage, token);
                    _bannerImage = bannerImage;
                    _bannerImageCreated?.Invoke(_bannerImage);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    _log.Error(e);
                    _bannerImageCreated?.Invoke(null);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _log.Error(e);
                _listingFound?.Invoke(null);
            }
        }
    }
}
