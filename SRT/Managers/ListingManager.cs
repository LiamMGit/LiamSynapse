using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using SiraUtil.Logging;
using SRT.Models;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;

namespace SRT.Managers
{
    // TODO: update listing when entering menu scene
    internal class ListingManager : IInitializable, ITickable
    {
        private readonly SiraLog _log;
        private Sprite? _bannerImage;

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

        public ListingManager(SiraLog log)
        {
            _log = log;
        }

        public Listing? Listing { get; private set; }

        public void Initialize()
        {
            const string url = "http://localhost:5033/api/v1/directory";
            _log.Debug($"Checking [{url}] for active listing");
            UnityWebRequest uwr = UnityWebRequest.Get(url);
            uwr.SendWebRequest().completed += n => Verify(n, OnListingRequestCompleted);
        }

        public void Tick()
        {
            if (Listing != null)
            {
                Listing.TimeSpan = Listing.Time - DateTime.UtcNow;
            }
        }

        private void OnListingRequestCompleted(DownloadHandler download)
        {
            string json = download.text;
            Listing listing = JsonConvert.DeserializeObject<Listing>(json);
            _log.Debug($"Found active listing for [{listing.Title}]");

            Listing = listing;
            _listingFound?.Invoke(Listing);

            _log.Debug($"Fetching banner image from [{listing.BannerImage}]");
            UnityWebRequest uwr = UnityWebRequest.Get(listing.BannerImage);
            uwr.SendWebRequest().completed += n => Verify(n, OnBannerImageRequstCompleted);
        }

        private void OnBannerImageRequstCompleted(DownloadHandler download)
        {
            _bannerImage = PromoManager.GetSprite(download.data);
            _bannerImageCreated?.Invoke(_bannerImage);
        }

        private void Verify(AsyncOperation operation, Action<DownloadHandler> action)
        {
            UnityWebRequest www = ((UnityWebRequestAsyncOperation)operation).webRequest;

            if (www.isHttpError)
            {
                _log.Debug($"The server returned an error response ({www.responseCode})");
                return;
            }

            if (www.isNetworkError)
            {
                _log.Debug($"Network error ({www.error})");
                return;
            }

            action(www.downloadHandler);
        }
    }
}
