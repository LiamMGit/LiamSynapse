using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HarmonyLib;
using HMUI;
using IPA.Utilities.Async;
using SiraUtil.Logging;
using SRT.Controllers;
using SRT.Managers;
using SRT.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Random = UnityEngine.Random;

namespace SRT.Views
{
    //[HotReload(RelativePathToLayout = @"../Resources/Lobby.bsml")]
    [ViewDefinition("SRT.Resources.Lobby.bsml")]
    public class EventLobbyViewController : BSMLAutomaticViewController
    {
        private static readonly string[] _randomHeaders =
        {
            "Upcoming",
            "Coming soon",
            "Next up",
            "As seen on TV",
            "Bringing you",
            "Stay tuned for",
            "In the pipeline",
            "Next on the agenda",
            "Prepare for",
            "Launching soon",
            "Coming your way",
            "Next in our lineup",
            "Brace yourself for",
            "Watch out for",
            "Unveiling",
            "Arriving now"
        };

        [UIComponent("chat")]
        private readonly VerticalLayoutGroup _chatObject = null!;

        [UIComponent("scrollview")]
        private readonly ScrollView _scrollView = null!;

        [UIComponent("textbox")]
        private readonly VerticalLayoutGroup _textObject = null!;

        [UIComponent("image")]
        private readonly ImageView _imageView = null!;

        [UIComponent("header")]
        private readonly ImageView _header = null!;

        [UIComponent("headertext")]
        private readonly TextMeshProUGUI _headerText = null!;

        [UIComponent("songtext")]
        private readonly TextMeshProUGUI _songText = null!;

        [UIComponent("artisttext")]
        private readonly TextMeshProUGUI _authorText = null!;

        [UIObject("spinny")]
        private readonly GameObject _loading = null!;

        [UIObject("loading")]
        private readonly GameObject _loadingGroup = null!;

        [UIObject("songinfo")]
        private readonly GameObject _songInfo = null!;

        [UIComponent("countdown")]
        private readonly TextMeshProUGUI _countdown = null!;

        [UIComponent("progress")]
        private readonly TextMeshProUGUI _progress = null!;

        private readonly List<ChatMessage> _messageQueue = new();
        private readonly LinkedList<Tuple<ChatMessage, TextMeshProUGUI>> _messages = new();

        private SiraLog _log = null!;
        private MessageManager _messageManager = null!;
        private NetworkManager _networkManager = null!;
        private CountdownManager _countdownManager = null!;
        private MapDownloadingManager _mapDownloadingManager = null!;
        private IInstantiator _instantiator = null!;

        private ScrollViewScroller _scroller = null!;
        private InputFieldView _input = null!;
        private OkRelay _okRelay = null!;

        private Sprite _placeholderSprite = null!;

        private float _angle;

        [Inject]
        private void Construct(
            SiraLog log,
            MessageManager messageManager,
            NetworkManager networkManager,
            MapDownloadingManager mapDownloadingManager,
            CountdownManager countdownManager,
            IInstantiator instantiator)
        {
            _log = log;
            _messageManager = messageManager;
            _networkManager = networkManager;
            _mapDownloadingManager = mapDownloadingManager;
            _countdownManager = countdownManager;
            _instantiator = instantiator;
        }

        private void OnOkPressed()
        {
            string text = _input.text;
            _input.ClearInput();
            _messageManager.SendMessage(text);
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            if (firstActivation)
            {
                InputFieldView original = Resources.FindObjectsOfTypeAll<InputFieldView>().First(n => n.name == "SearchInputField");
                _input = Instantiate(original, _chatObject.transform);
                _input.name = "EventChatInputField";
                RectTransform rect = (RectTransform)_input.transform;
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(1, 1);
                rect.offsetMin = new Vector2(20, 0);
                rect.offsetMax = new Vector2(-20, -70);
                _input._keyboardPositionOffset = new Vector3(0, 60, 0);
                _input._textLengthLimit = 200;
                _instantiator.InstantiateComponent<KeyboardOpener>(_input.gameObject);
                RectTransform bg = (RectTransform)rect.Find("BG");
                bg.offsetMin = new Vector2(0, -4);
                bg.offsetMax = new Vector2(0, 4);
                Transform placeholderText = rect.Find("PlaceholderText");

                // its in Polyglot and im too lazy to add its reference
                // ReSharper disable once Unity.UnresolvedComponentOrScriptableObject
                Destroy(placeholderText.GetComponent("LocalizedTextMeshProUGUI"));
                placeholderText.GetComponent<CurvedTextMeshPro>().text = "Chat";
                ((RectTransform)placeholderText).offsetMin = new Vector2(4, 0);
                ((RectTransform)rect.Find("Text")).offsetMin = new Vector2(4, -4);
                Destroy(rect.Find("Icon").gameObject);

                _okRelay = _input.gameObject.AddComponent<OkRelay>();
                _okRelay.OkPressed += OnOkPressed;

                _scroller = _scrollView.gameObject.AddComponent<ScrollViewScroller>();
                _scroller.Init(_scrollView);

                _input.gameObject.AddComponent<LayoutElement>().minHeight = 10;
                _imageView.material = Resources.FindObjectsOfTypeAll<Material>().First(n => n.name == "UINoGlowRoundEdge");
                _placeholderSprite = _imageView.sprite;

                _header.color0 = new Color(1, 1, 1, 1);
                _header.color1 = new Color(1, 1, 1, 0);
            }

            if (addedToHierarchy)
            {
                _imageView.sprite = _placeholderSprite;
                _messageManager.MessageRecieved += OnMessageRecieved;
                _messageManager.RefreshMotd();
                _networkManager.UserBanned += OnUserBanned;
                ResetLoading();
                _networkManager.MapUpdated += OnMapUpdated;
                _mapDownloadingManager.MapDownloaded += OnMapDownloaded;
                _mapDownloadingManager.ProgressUpdated += OnProgressUpdated;
                _countdownManager.CountdownUpdated += OnCountdownUpdated;
                _countdownManager.Refresh();

                _progress.text = "Loading...";
            }

            _headerText.text = _randomHeaders[Random.Range(0, _randomHeaders.Length - 1)] + "...";
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);

            if (removedFromHierarchy)
            {
                _messageQueue.Clear();
                _messages.Clear();
                foreach (Transform obj in _textObject.transform)
                {
                    Destroy(obj.gameObject);
                }

                _messageManager.MessageRecieved -= OnMessageRecieved;
                _networkManager.UserBanned -= OnUserBanned;
                _networkManager.MapUpdated -= OnMapUpdated;
                _mapDownloadingManager.MapDownloaded -= OnMapDownloaded;
                _mapDownloadingManager.ProgressUpdated -= OnProgressUpdated;
                _mapDownloadingManager.Cancel();
                _countdownManager.CountdownUpdated -= OnCountdownUpdated;
            }
        }

        private void Update()
        {
            if (_loadingGroup.activeInHierarchy)
            {
                _angle += Time.deltaTime * 200;
                _loading.transform.localEulerAngles = new Vector3(0, 0, _angle);
            }

            if (_messageQueue.Count == 0)
            {
                return;
            }

            try
            {
                float end = _scrollView.contentSize - _scrollView.scrollPageSize;
                bool scrollToEnd = (end < 0) ||
                    (Mathf.Abs(end - _scrollView._destinationPos) < 0.01f);

                ChatMessage[] queue = _messageQueue.ToArray();
                _messageQueue.Clear();
                foreach (ChatMessage message in queue)
                {
                    string content;
                    bool rich;
                    if (string.IsNullOrEmpty(message.Id))
                    {
                        content = message.Message;
                        rich = true;
                    }
                    else
                    {
                        content = $"[{message.Username}] {message.Message}";
                        rich = false;
                    }

                    // TODO: figure out how not to mess with scroll position when shifting chat
                    if (_messages.Count > 100)
                    {
                        LinkedListNode<Tuple<ChatMessage, TextMeshProUGUI>> first = _messages.First;
                        _messages.RemoveFirst();
                        TextMeshProUGUI textMesh = first.Value.Item2;
                        textMesh.richText = rich;
                        textMesh.text = content;
                        textMesh.transform.SetAsLastSibling();
                        first.Value = new Tuple<ChatMessage, TextMeshProUGUI>(message, textMesh);
                        _messages.AddLast(first);
                    }
                    else
                    {
                        TextMeshProUGUI text =
                            BeatSaberMarkupLanguage.BeatSaberUI.CreateText(
                                (RectTransform)_textObject.transform,
                                content,
                                Vector2.zero);
                        text.enableWordWrapping = true;
                        text.richText = rich;
                        text.alignment = TextAlignmentOptions.Left;
                        text.fontSize = 4;
                    }
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_textObject.transform);
                _scrollView.enabled = true;
                if (scrollToEnd)
                {
                    _scroller.enabled = true;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }

        private void OnMapDownloaded((IDifficultyBeatmap Difficulty, IPreviewBeatmapLevel Preview) map)
        {
            _log.Info("show info");
            IPreviewBeatmapLevel preview = map.Preview;
            _ = SetCoverImage(preview);
            _songInfo.SetActive(true);
            _loadingGroup.SetActive(false);
            _songText.text = preview.songName;
            _authorText.text = $"{preview.songAuthorName} [{preview.levelAuthorName}]";
        }

        private async Task SetCoverImage(IPreviewBeatmapLevel preview)
        {
            _imageView.sprite = await preview.GetCoverImageAsync(CancellationToken.None);
        }

        private void OnMessageRecieved(ChatMessage message)
        {
            _messageQueue.Add(message);
        }

        private void OnUserBanned(string id)
        {
            _messageQueue.RemoveAll(n => n.Id == id);
            _messages.Where(n => n.Item1.Id == id).Do(n => n.Item2.text = "<deleted>");
        }

        private void OnMapUpdated(int index, Map _)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(ResetLoading);
        }

        private void ResetLoading()
        {
            _songInfo.SetActive(false);
            _loadingGroup.SetActive(true);
        }

        private void OnCountdownUpdated(string text)
        {
            _countdown.text = text;
        }

        private void OnProgressUpdated(string message)
        {
            _progress.text = message;
        }
    }
}
