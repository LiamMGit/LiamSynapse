using System;
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
    // TODO: update listing when entering menu scene
    internal class ListingManager : IInitializable
    {
        private readonly SiraLog _log;
        private readonly Config _config;
        private Sprite? _bannerImage;

        [UsedImplicitly]
        private ListingManager(SiraLog log, Config config)
        {
            _log = log;
            _config = config;
        }

        public event Action<Listing>? ListingFound
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

        public event Action<Sprite>? BannerImageCreated
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

        private event Action<Listing>? _listingFound;

        private event Action<Sprite>? _bannerImageCreated;

        public Listing? Listing { get; private set; }

        public void Initialize()
        {
            string url = _config.Url;
            _log.Debug($"Checking [{url}] for active listing");
            UnityWebRequest.Get(url).SendAndVerify(OnListingRequestCompleted);
        }

        private void OnListingRequestCompleted(UnityWebRequest download)
        {
            string json = download.downloadHandler.text;
            Listing? listing = JsonConvert.DeserializeObject<Listing>(json, JsonSettings.Settings);
            if (listing == null)
            {
                _log.Error("Error deserializing listing");
                return;
            }

            _log.Debug($"Found active listing for [{listing.Title}]");

            Listing = listing;
            _listingFound?.Invoke(Listing);

            _log.Debug($"Fetching banner image from [{listing.BannerImage}]");
            WebRequestExtensions.RequestSprite(listing.BannerImage, OnBannerImageRequstCompleted);
        }

        private void OnBannerImageRequstCompleted(Sprite sprite)
        {
            _bannerImage = sprite;
            _bannerImageCreated?.Invoke(_bannerImage);
        }
    }
}
