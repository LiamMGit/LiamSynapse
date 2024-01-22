using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using HMUI;
using IPA.Utilities;
using SRT.Models;
using TMPro;
using Tweening;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Object = UnityEngine.Object;

namespace SRT.Managers
{
    // TODO: Set banner color in listing from pink
    public class PromoManager : IInitializable, ITickable
    {
        private static readonly FieldAccessor<SelectableStateController, TimeTweeningManager>.Accessor
            _tweeningAccessor =
                FieldAccessor<SelectableStateController, TimeTweeningManager>.GetAccessor("_tweeningManager");

        private static readonly Sprite _promoBack = GetEmbeddedResourceSprite("SRT.Resources.promo_back.png");
        private static readonly Sprite _promoPlaceHolder = GetEmbeddedResourceSprite("SRT.Resources.promo_placeholder.png");

        private readonly MainMenuViewController _mainMenuViewController;
        private readonly ListingManager _listingManager;

        private Listing? _listing;

        private ImageViewRainbowController _rainbow = null!;
        private GameObject _promoBanner = null!;
        private ImageView _imageView = null!;
        private TextMeshProUGUI _textMesh = null!;
        private CanvasGroupTransitionSO _transition = null!;

        private Button? _button;

        private PromoManager(MainMenuViewController mainMenuViewController, ListingManager listingManager)
        {
            _mainMenuViewController = mainMenuViewController;
            _listingManager = listingManager;
        }

        public bool Active { get; private set; }

        public Button Button => _button ??= CreateButton();

        public void Initialize()
        {
            _button ??= CreateButton();
        }

        public Button CreateButton()
        {
            Button orignalButton = _mainMenuViewController._musicPackPromoButton;
            GameObject original = orignalButton.gameObject;
            Button newButton = Object.Instantiate(orignalButton, original.transform.parent);
            GameObject newObject = newButton.gameObject;
            Object.Destroy(newObject.GetComponent<MusicPackPromoBanner>());
            _promoBanner = newObject;

            SelectableStateController stateController =
                newObject.GetComponent<NoTransitionButtonSelectableStateController>();
            _tweeningAccessor(ref stateController) =
                original.GetComponent<NoTransitionButtonSelectableStateController>()._tweeningManager;

            newObject.transform.SetSiblingIndex(original.transform.GetSiblingIndex());

            _textMesh = newObject.GetComponentInChildren<TextMeshProUGUI>();
            _textMesh.text = string.Empty;

            _imageView = newObject.GetComponentInChildren<ImageView>();
            _imageView.sprite = _promoPlaceHolder;
            GameObject originalImage = _imageView.gameObject;
            GameObject newImage = Object.Instantiate(originalImage, newObject.transform).gameObject;
            originalImage.transform.localScale = new Vector3(0.96f, 0.99f, 1);
            newImage.transform.SetSiblingIndex(0);
            newImage.GetComponent<ImageView>().sprite = _promoBack;
            _rainbow = newImage.AddComponent<ImageViewRainbowController>();
            _rainbow.enabled = false;
            ////newButton.transform.localScale = new Vector3(1.00f, 1.0f, 1);

            CanvasGroupStateTransition stateTransition = newObject.GetComponent<CanvasGroupStateTransition>();
            _transition = Object.Instantiate(stateTransition._transition);
            _transition._normalAlpha = 0.4f;
            _transition._highlightedAlpha = 0.6f;
            stateTransition._transition = _transition;

            newObject.name = "SRTPromoBanner";

            original.SetActive(false);
            newObject.SetActive(false);

            _listingManager.ListingFound += OnListingFound;
            _listingManager.BannerImageCreated += OnBannerImageCreated;

            _promoBanner = newObject;
            return newButton;
        }

        public void OnListingFound(Listing listing)
        {
            _listing = listing;
            _promoBanner.SetActive(true);
        }

        private void OnBannerImageCreated(Sprite sprite)
        {
            _imageView.sprite = sprite;
        }

        public static Sprite GetSprite(byte[] bytes)
        {
            Texture2D tex = new(2, 2);
            tex.LoadImage(bytes);
            return Sprite.Create(tex, new Rect(0, 0, 200, 500), new Vector2(0.5f, 0.5f));
        }

        private static Sprite GetEmbeddedResourceSprite(string path)
        {
            using Stream stream =
                typeof(PromoManager).Assembly.GetManifestResourceStream(path) ?? throw new InvalidOperationException();
            using MemoryStream memStream = new();
            stream.CopyTo(memStream);
            return GetSprite(memStream.ToArray());
        }

        public void Tick()
        {
            if (Active || _listing == null)
            {
                return;
            }

            TimeSpan span = _listing.TimeSpan;

            if (span.Ticks < 0)
            {
                Active = true;
                _rainbow.enabled = true;
                _transition._normalAlpha = 0.8f;
                _transition._highlightedAlpha = 1;
                NoTransitionButtonSelectableStateController controller =
                    _promoBanner.GetComponent<NoTransitionButtonSelectableStateController>();
                controller.ResolveSelectionState(controller._component.selectionState, false);
                _textMesh.text = $"{_listing.Title}\n<size=120%>LIVE NOW</size>";
            }
            else
            {
                string time = span.Days switch
                {
                    > 1 => $"{span.Days} days",
                    > 0 => $"{span.Days} day",
                    _ => $"{span.Hours:D2}:{span.Minutes:D2}:{span.Seconds:D2}"
                };

                _textMesh.text = $"{_listing.Title}\n<size=120%>{time}</size>";
            }
        }
    }
}
