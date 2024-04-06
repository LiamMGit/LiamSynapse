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
        private string? _bannerUrl;
        private Sprite? _finishImage;
        private string? _finishUrl;
        private string? _lastListing;

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

        public event Action<Sprite?>? FinishImageCreated
        {
            add
            {
                if (_finishImage != null)
                {
                    value?.Invoke(_finishImage);
                }

                _finishImageCreated += value;
            }

            remove => _finishImageCreated -= value;
        }

        private event Action<Listing?>? _listingFound;

        private event Action<Sprite?>? _bannerImageCreated;

        private event Action<Sprite?>? _finishImageCreated;

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

                if (listing.Guid == _lastListing)
                {
                    return;
                }

                _lastListing = listing.Guid;
                _log.Debug($"Found active listing for [{listing.Title}]");

                Listing = listing;
                _listingFound?.Invoke(Listing);

                _ = GetBannerImage(listing, token);
                _ = GetFinishImage(listing, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _log.Error(e);
                _lastListing = null;
                _listingFound?.Invoke(null);
                _bannerImageCreated?.Invoke(null);
                _finishImageCreated?.Invoke(null);
            }
        }

        private async Task GetBannerImage(Listing listing, CancellationToken token)
        {
            if (_bannerUrl != listing.BannerImage)
            {
                _bannerUrl = listing.BannerImage;

                try
                {
                    _log.Debug($"Fetching banner image from [{listing.BannerImage}]");
                    Sprite bannerImage = await MediaExtensions.RequestSprite(listing.BannerImage, token);
                    _bannerImage = bannerImage;
                    _bannerImageCreated?.Invoke(bannerImage);
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
        }

        private async Task GetFinishImage(Listing listing, CancellationToken token)
        {
            if (_finishUrl != listing.FinishImage)
            {
                _finishUrl = listing.FinishImage;

                try
                {
                    _log.Debug($"Fetching finish image from [{listing.FinishImage}]");
                    Sprite finishImage = await MediaExtensions.RequestSprite(listing.FinishImage, token);
                    _finishImage = finishImage;
                    _finishImageCreated?.Invoke(finishImage);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    _log.Error(e);
                    _finishImageCreated?.Invoke(null);
                }
            }
        }
    }
}
