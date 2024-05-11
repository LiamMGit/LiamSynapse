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

                ListingFound_Backing += value;
            }

            remove => ListingFound_Backing -= value;
        }

        public event Action<Sprite?>? BannerImageCreated
        {
            add
            {
                if (_bannerImage != null)
                {
                    value?.Invoke(_bannerImage);
                }

                BannerImageCreated_Backing += value;
            }

            remove => BannerImageCreated_Backing -= value;
        }

        public event Action<Sprite?>? FinishImageCreated
        {
            add
            {
                if (_finishImage != null)
                {
                    value?.Invoke(_finishImage);
                }

                FinishImageCreated_Backing += value;
            }

            remove => FinishImageCreated_Backing -= value;
        }

        private event Action<Listing?>? ListingFound_Backing;

        private event Action<Sprite?>? BannerImageCreated_Backing;

        private event Action<Sprite?>? FinishImageCreated_Backing;

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
                ListingFound_Backing?.Invoke(Listing);

                _ = GetBannerImage(listing, token);
                _ = GetFinishImage(listing, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _log.Warn(e);
                _lastListing = null;
                ListingFound_Backing?.Invoke(null);
                BannerImageCreated_Backing?.Invoke(null);
                FinishImageCreated_Backing?.Invoke(null);
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
                    BannerImageCreated_Backing?.Invoke(bannerImage);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    _log.Error(e);
                    BannerImageCreated_Backing?.Invoke(null);
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
                    FinishImageCreated_Backing?.Invoke(finishImage);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    _log.Error(e);
                    FinishImageCreated_Backing?.Invoke(null);
                }
            }
        }
    }
}
