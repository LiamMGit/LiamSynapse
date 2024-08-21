using BeatSaberMarkupLanguage;
using HMUI;
using JetBrains.Annotations;
using Synapse.Extras;
using Synapse.Models;
using TMPro;
using UnityEngine;
using Zenject;

namespace Synapse.Managers;

internal class NotificationManager : MonoBehaviour
{
    private bool _active;
    private Color? _color;

    private Listing? _listing;
    private ListingManager _listingManager = null!;
    private NetworkManager _networkManager = null!;
    private RainbowString _rainbowString = null!;
    private string _text = string.Empty;
    private TextMeshProUGUI _textMesh = null!;
    private float _timer;

    public void Notify(string text, Color? color = null)
    {
        _timer = 15;
        gameObject.SetActive(true);
        _text = text;
        _color = color;

        if (color == null)
        {
            _rainbowString.SetString(text);
        }
    }

    [UsedImplicitly]
    [Inject]
    private void Construct(
        RainbowString rainbowString,
        ListingManager listingManager,
        NetworkManager networkManager,
        TextMeshProUGUI textMesh)
    {
        _rainbowString = rainbowString;
        _textMesh = textMesh;
        _listingManager = listingManager;
        listingManager.ListingFound += OnListingFound;
        _networkManager = networkManager;
        networkManager.Connecting += OnConnecting;
    }

    // Dont need notification if we are already connecting
    private void OnConnecting(ConnectingStage connectingStage, int _)
    {
        _timer = 0;
    }

    private void OnDestroy()
    {
        _listingManager.ListingFound -= OnListingFound;
        _networkManager.Connecting -= OnConnecting;
    }

    private void OnListingFound(Listing? listing)
    {
        _listing = listing;
        if (_listing == null)
        {
            _timer = 0;
            return;
        }

        if (_listing.TimeSpan.Ticks < 0)
        {
            OnStarted();
        }
        else if (_listing.TimeSpan.TotalMinutes < 30)
        {
            Notify($"{_listing.Title} is starting soon!");
        }
    }

    private void OnStarted()
    {
        _active = true;
        Notify($"{_listing!.Title} is now live!");
    }

    private void Update()
    {
        if (!_active && _listing is { TimeSpan.Ticks: < 0 })
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

        if (_color == null)
        {
            _textMesh.SetCharArray(_rainbowString.ToCharArray());
        }
        else
        {
            _textMesh.color = _color.Value;
            _textMesh.text = _text;
        }
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
            TextMeshProUGUI textMesh = BeatSaberUI.CreateText(rectTransform, string.Empty, Vector2.zero);
            textMesh.gameObject.layer = 5;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.fontSize = 15;
            gameObject.transform.position = new Vector3(0, 3f, 4f);

            return _instantiator.InstantiateComponent<NotificationManager>(gameObject, new object[] { textMesh });
        }
    }
}
