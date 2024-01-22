using System.Text;
using HMUI;
using JetBrains.Annotations;
using SRT.Models;
using SRT.Views;
using TMPro;
using UnityEngine;
using Zenject;

namespace SRT.Managers
{
    public class NotificationManager : MonoBehaviour
    {
        private ListingManager _listingManager = null!;
        private NetworkManager _networkManager = null!;

        private TextMeshProUGUI _textMesh = null!;
        private string _text = string.Empty;

        private float _hue;
        private float _timer;
        private bool _active;
        private Listing? _listing;

        [Inject]
        private void Construct(ListingManager listingManager, NetworkManager networkManager, TextMeshProUGUI textMesh)
        {
            _textMesh = textMesh;
            _listingManager = listingManager;
            listingManager.ListingFound += OnListingFound;
            _networkManager = networkManager;
            networkManager.Connecting += OnConnecting;
        }

        private void OnDestroy()
        {
            _listingManager.ListingFound -= OnListingFound;
            _networkManager.Connecting -= OnConnecting;
        }

        // Dont need notification if we are already connecting
        private void OnConnecting(Stage stage, int _)
        {
            gameObject.SetActive(false);
            _timer = 0;
        }

        private void OnListingFound(Listing listing)
        {
            _listing = listing;
            if (_listing.TimeSpan.Ticks < 0)
            {
                OnStarted();
            }
            else if (_listing.TimeSpan.TotalMinutes < 30)
            {
                Notify($"{_listing.Title} is starting soon!");
            }
        }

        private void Notify(string text)
        {
            _timer = 15;
            gameObject.SetActive(true);
            _text = text;
        }

        private void OnStarted()
        {
            _active = true;
            Notify($"{_listing!.Title} is now live!");
        }

        private void Update()
        {
            if (!_active && _listing is { TimeSpan: { Ticks: < 0 } })
            {
                OnStarted();
            }

            if (_timer > 0)
            {
                _timer -= Time.deltaTime;
            }
            else
            {
                gameObject.SetActive(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(_text))
            {
                return;
            }

            _hue = Mathf.Repeat(_hue + (0.5f * Time.deltaTime), 1);

            StringBuilder builder = new(_text.Length);
            for (int i = 0; i < _text.Length; i++)
            {
                char c = _text[i];
                if (char.IsWhiteSpace(c))
                {
                    builder.Append(c);
                    continue;
                }

                Color col = Color.HSVToRGB(Mathf.Repeat(_hue + (0.02f * i), 1), 0.8f, 1);
                builder.Append($"<color=#{ColorUtility.ToHtmlStringRGB(col)}>{c}");
            }

            _textMesh.SetText(builder);
        }

        internal class NotificationManagerFactory : IFactory<NotificationManager>
        {
            private readonly IInstantiator _instantiator;

            [UsedImplicitly]
            private NotificationManagerFactory(IInstantiator instantiator)
            {
                _instantiator = instantiator;
            }

            public NotificationManager Create()
            {
                GameObject gameObject = new("NotificationText")
                {
                    layer = 5
                };
                DontDestroyOnLoad(gameObject);

                Canvas canvas = gameObject.AddComponent<Canvas>();
                CurvedCanvasSettings curvedCanvasSettings = gameObject.AddComponent<CurvedCanvasSettings>();
                curvedCanvasSettings.SetRadius(800);
                canvas.renderMode = RenderMode.WorldSpace;
                RectTransform rectTransform = (RectTransform)canvas.transform;
                rectTransform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                TextMeshProUGUI textMesh = BeatSaberMarkupLanguage.BeatSaberUI.CreateText(rectTransform, string.Empty, Vector2.zero);
                textMesh.gameObject.layer = 5;
                textMesh.alignment = TextAlignmentOptions.Center;
                textMesh.fontSize = 15;
                gameObject.transform.position = new Vector3(0, 3f, 4f);

                return _instantiator.InstantiateComponent<NotificationManager>(gameObject, new object[] { textMesh });
            }
        }
    }
}
