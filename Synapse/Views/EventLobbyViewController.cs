using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HarmonyLib;
using HMUI;
using IPA.Utilities.Async;
using JetBrains.Annotations;
using SiraUtil.Logging;
using Synapse.Controllers;
using Synapse.Extras;
using Synapse.HarmonyPatches;
using Synapse.Managers;
using Synapse.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Random = UnityEngine.Random;

namespace Synapse.Views
{
    // oh how i wish i could have sub-viewcontrollers
    ////[HotReload(RelativePathToLayout = @"../Resources/Lobby.bsml")]
    [ViewDefinition("Synapse.Resources.Lobby.bsml")]
    internal class EventLobbyViewController : BSMLAutomaticViewController
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

        private static readonly ProfanityFilter.ProfanityFilter _profanityFilter = new();

        [UIComponent("chat")]
        private readonly VerticalLayoutGroup _chatObject = null!;

        [UIComponent("scrollview")]
        private readonly ScrollView _scrollView = null!;

        [UIComponent("textbox")]
        private readonly VerticalLayoutGroup _textObject = null!;

        [UIComponent("cover")]
        private readonly ImageView _coverImage = null!;

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

        [UIComponent("progress")]
        private readonly TextMeshProUGUI _progress = null!;

        [UIObject("songinfo")]
        private readonly GameObject _songInfo = null!;

        [UIObject("countdownobject")]
        private readonly GameObject _countdownObject = null!;

        [UIComponent("countdown")]
        private readonly TextMeshProUGUI _countdown = null!;

        [UIObject("scoreobject")]
        private readonly GameObject _scoreObject = null!;

        [UIComponent("scoretext")]
        private readonly TextMeshProUGUI _score = null!;

        [UIComponent("accuracytext")]
        private readonly TextMeshProUGUI _accuracy = null!;

        [UIObject("startobject")]
        private readonly GameObject _startObject = null!;

        [UIComponent("startbutton")]
        private readonly TextMeshProUGUI _startButton = null!;

        [UIObject("toend")]
        private readonly GameObject _toEndObject = null!;

        [UIComponent("modal")]
        private readonly ModalView _modal = null!;

        [UIObject("map")]
        private readonly GameObject _map = null!;

        [UIObject("finish")]
        private readonly GameObject _finish = null!;

        [UIComponent("finishimage")]
        private readonly ImageView _finishImage = null!;

        private readonly List<ChatMessage> _messageQueue = new();
        private readonly LinkedList<Tuple<ChatMessage, TextMeshProUGUI>> _messages = new();

        private SiraLog _log = null!;
        private Config _config = null!;
        private MessageManager _messageManager = null!;
        private NetworkManager _networkManager = null!;
        private ListingManager _listingManager = null!;
        private CountdownManager _countdownManager = null!;
        private MapDownloadingManager _mapDownloadingManager = null!;
        private CancellationTokenManager _cancellationTokenManager = null!;
        private IInstantiator _instantiator = null!;

        private InputFieldView _input = null!;
        private OkRelay _okRelay = null!;
        private Sprite _coverPlaceholder = null!;

        private Sprite _finishPlaceholder = null!;

        private string? _altCoverUrl;
        private float _angle;
        private IPreviewBeatmapLevel? _preview;
        private DateTime? _startTime;
        private PlayerScore? _playerScore;

        public event Action? StartLevel;

        [UsedImplicitly]
        [UIValue("joinChat")]
        private bool JoinChat
        {
            get => _config.JoinChat ?? false;
            set
            {
                _config.JoinChat = value;
                _ = _networkManager.SendBool(value, ServerOpcode.SetChatter);
            }
        }

        [UsedImplicitly]
        [UIValue("profanityFilter")]
        private bool ProfanityFilter
        {
            get => _config.ProfanityFilter;
            set => _config.ProfanityFilter = value;
        }

        [UsedImplicitly]
        [UIValue("muteMusic")]
        private bool MuteMusic
        {
            get => _config.MuteMusic;
            set => _config.MuteMusic = value;
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
                _input._textView.richText = false;
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

                _scrollView.gameObject.AddComponent<ScrollViewScrollToEnd>().Construct(_toEndObject);
                _toEndObject.SetActive(false);

                _okRelay = _input.gameObject.AddComponent<OkRelay>();
                _okRelay.OkPressed += OnOkPressed;

                _input.gameObject.AddComponent<LayoutElement>().minHeight = 10;
                _coverImage.material = Resources.FindObjectsOfTypeAll<Material>().First(n => n.name == "UINoGlowRoundEdge");
                _coverPlaceholder = _coverImage.sprite;

                _header.color0 = new Color(1, 1, 1, 1);
                _header.color1 = new Color(1, 1, 1, 0);

                _songText.enableAutoSizing = true;
                _authorText.enableAutoSizing = true;
                _songText.enableWordWrapping = false;
                _authorText.enableWordWrapping = false;
                _songText.fontSizeMin = _songText.fontSize / 4;
                _songText.fontSizeMax = _songText.fontSize;
                _authorText.fontSizeMin = _authorText.fontSize / 4;
                _authorText.fontSizeMax = _authorText.fontSize;

                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_songInfo.transform);

                _startObject.SetActive(false);

                _finishPlaceholder = _finishImage.sprite;
                _listingManager.FinishImageCreated += OnFinishImageCreated;
            }

            if (addedToHierarchy)
            {
                _messageManager.MessageRecieved += OnMessageRecieved;
                _messageManager.RefreshMotd();
                _networkManager.UserBanned += OnUserBanned;
                _startTime = _networkManager.Status.StartTime;
                _playerScore = _networkManager.Status.PlayerScore;
                RefreshMap(_networkManager.Status.Map);
                _networkManager.PlayerScoreUpdated += OnPlayerScoreUpdate;
                _networkManager.StartTimeUpdated += OnStartTimeUpdated;
                _networkManager.MapUpdated += OnMapUpdated;
                _mapDownloadingManager.MapDownloaded += OnMapDownloaded;
                _mapDownloadingManager.ProgressUpdated += OnProgressUpdated;
                _countdownManager.CountdownUpdated += OnCountdownUpdated;
                _countdownManager.Refresh();
            }

            _headerText.text = _randomHeaders[Random.Range(0, _randomHeaders.Length - 1)] + "...";
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);

            // ReSharper disable once InvertIf
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
                _networkManager.PlayerScoreUpdated -= OnPlayerScoreUpdate;
                _networkManager.StartTimeUpdated -= OnStartTimeUpdated;
                _networkManager.MapUpdated -= OnMapUpdated;
                _mapDownloadingManager.MapDownloaded -= OnMapDownloaded;
                _mapDownloadingManager.ProgressUpdated -= OnProgressUpdated;
                _mapDownloadingManager.Cancel();
                _countdownManager.CountdownUpdated -= OnCountdownUpdated;
            }
        }

        [UsedImplicitly]
        [Inject]
        private void Construct(
            SiraLog log,
            Config config,
            MessageManager messageManager,
            NetworkManager networkManager,
            ListingManager listingManager,
            MapDownloadingManager mapDownloadingManager,
            CancellationTokenManager cancellationTokenManager,
            CountdownManager countdownManager,
            IInstantiator instantiator)
        {
            _log = log;
            _config = config;
            _messageManager = messageManager;
            _networkManager = networkManager;
            _listingManager = listingManager;
            _mapDownloadingManager = mapDownloadingManager;
            _cancellationTokenManager = cancellationTokenManager;
            _countdownManager = countdownManager;
            _instantiator = instantiator;
        }

        private new void OnDestroy()
        {
            base.OnDestroy();
            _listingManager.FinishImageCreated -= OnFinishImageCreated;
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
                float heightLost = 0;

                ChatMessage[] queue = _messageQueue.ToArray();
                _messageQueue.Clear();
                foreach (ChatMessage message in queue)
                {
                    string content;
                    Color color;
                    string messageString = message.Message;
                    string usernameString = message.Username;
                    string? colorString = message.Color;
                    if (ProfanityFilter)
                    {
                        messageString = _profanityFilter.CensorString(messageString);
                        usernameString = _profanityFilter.CensorString(usernameString);
                    }

                    switch (message.Type)
                    {
                        case MessageType.System:
                            content = Colorize(messageString, message.Color);
                            color = Color.white;
                            break;

                        case MessageType.WhisperFrom:
                            content = $"[From {Colorize(NoParse(usernameString), colorString)}] {NoParse(messageString)}";
                            color = Color.magenta;
                            break;

                        case MessageType.WhisperTo:
                            content = $"[To {Colorize(NoParse(usernameString), colorString)}] {NoParse(messageString)}";
                            color = Color.magenta;
                            break;

                        case MessageType.Say:
                        default:
                            content = $"[{Colorize(NoParse(usernameString), colorString)}] {NoParse(messageString)}";
                            color = Color.white;
                            break;
                    }

                    if (_messages.Count > 100)
                    {
                        LinkedListNode<Tuple<ChatMessage, TextMeshProUGUI>> first = _messages.First;
                        _messages.RemoveFirst();
                        TextMeshProUGUI text = first.Value.Item2;
                        float height = text.rectTransform.rect.height;
                        text.color = color;
                        text.text = content;
                        text.transform.SetAsLastSibling();
                        first.Value = new Tuple<ChatMessage, TextMeshProUGUI>(message, text);
                        _messages.AddLast(first);
                        heightLost += height;
                    }
                    else
                    {
                        TextMeshProUGUI text =
                            BeatSaberMarkupLanguage.BeatSaberUI.CreateText(
                                (RectTransform)_textObject.transform,
                                content,
                                Vector2.zero);
                        text.enableWordWrapping = true;
                        text.richText = true;
                        text.color = color;
                        text.alignment = TextAlignmentOptions.Left;
                        text.fontSize = 4;
                        _messages.AddLast(new Tuple<ChatMessage, TextMeshProUGUI>(message, text));
                    }
                }

                Canvas.ForceUpdateCanvases();
                RectTransform contentTransform = _scrollView._contentRectTransform;
                _scrollView.SetContentSize(contentTransform.rect.height);
                float heightDiff = heightLost;
                if (scrollToEnd)
                {
                    heightDiff = _scrollView.contentSize - _scrollView.scrollPageSize - _scrollView._destinationPos;
                }
                else if (heightLost > 0)
                {
                    heightDiff = -heightLost;
                }
                else
                {
                    return;
                }

                _scrollView._destinationPos = Mathf.Max(_scrollView._destinationPos + heightDiff, 0);
                _scrollView.RefreshButtons();
                float newY = Mathf.Max(contentTransform.anchoredPosition.y + heightDiff, 0);
                contentTransform.anchoredPosition = new Vector2(0, newY);
                _scrollView.UpdateVerticalScrollIndicator(Mathf.Abs(newY));
            }
            catch (Exception e)
            {
                _log.Error(e);
            }

            return;

            static string NoParse(string message)
            {
                StringBuilder stringBuilder = new();
                foreach (char c in message)
                {
                    if (c is '<' or '>')
                    {
                        stringBuilder.Append($"<noparse>{c}</noparse>");
                    }
                    else
                    {
                        stringBuilder.Append(c);
                    }
                }

                return stringBuilder.ToString();
            }

            static string Colorize(string message, string? color)
            {
                if (color == null)
                {
                    return message;
                }

                return color[0] != '#' ? $"<color=\"{color}\">{message}</color>" : $"<color={color}>{message}</color>";
            }
        }

        private void RefreshSongInfo()
        {
            CancellationToken token = _cancellationTokenManager.Reset();
            _coverImage.sprite = _coverPlaceholder;
            if (_altCoverUrl != null && _playerScore == null)
            {
                _ = SetCoverImage(MediaExtensions.RequestSprite(_altCoverUrl, token));
                _songText.text = "???";
                _authorText.text = "??? [???]";
            }
            else if (_preview != null)
            {
                _ = SetCoverImage(_preview.GetCoverImageAsync(token));
                _songText.text = _preview.songName;
                _authorText.text = $"{_preview.songAuthorName} [{_preview.levelAuthorName}]";
            }
            else
            {
                _songText.text = "???";
                _authorText.text = "??? [???]";
            }

            if (_playerScore != null)
            {
                if (_networkManager.Status.Map?.Ruleset?.AllowResubmission ?? false)
                {
                    _startButton.text = "rescore";
                    _startObject.SetActive(true);
                    _scoreObject.SetActive(false);
                    _countdownObject.SetActive(false);
                }
                else
                {
                    _score.text = $"{ScoreFormatter.Format(_playerScore.Score)}";
                    _accuracy.text = $"{EventLeaderboardVisuals.FormatAccuracy(_playerScore.Accuracy)}";
                    _startObject.SetActive(false);
                    _scoreObject.SetActive(true);
                    _countdownObject.SetActive(false);
                }
            }
            else
            {
                if (_startTime == null || _startTime > DateTime.UtcNow)
                {
                    _startObject.SetActive(false);
                    _scoreObject.SetActive(false);
                    _countdownObject.SetActive(true);
                }
                else
                {
                    _startButton.text = "play";
                    _startObject.SetActive(true);
                    _scoreObject.SetActive(false);
                    _countdownObject.SetActive(false);
                }
            }

            return;

            async Task SetCoverImage(Task<Sprite> spriteTask)
            {
                _coverImage.sprite = await spriteTask;
            }
        }

        private void OnMapDownloaded(DownloadedMap map)
        {
            _altCoverUrl = string.IsNullOrWhiteSpace(map.Map.AltCoverUrl) ? null : map.Map.AltCoverUrl;
            _preview = map.PreviewBeatmapLevel;
            _songInfo.SetActive(true);
            _loadingGroup.SetActive(false);
            RefreshSongInfo();
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

        private void OnPlayerScoreUpdate(PlayerScore? playerScore)
        {
            _playerScore = playerScore;
            UnityMainThreadTaskScheduler.Factory.StartNew(RefreshSongInfo);
        }

        private void OnStartTimeUpdated(DateTime? startTime)
        {
            _startTime = startTime;
            UnityMainThreadTaskScheduler.Factory.StartNew(RefreshSongInfo);
        }

        private void OnMapUpdated(int _, Map? map)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() => RefreshMap(map));
        }

        private void RefreshMap(Map? map)
        {
            if (map != null)
            {
                _map.SetActive(true);
                _finish.SetActive(false);
                _progress.text = "Loading...";
                _altCoverUrl = null;
                _preview = null;
                _songInfo.SetActive(false);
                _loadingGroup.SetActive(true);
                RefreshSongInfo();
            }
            else
            {
                _map.SetActive(false);
                _finish.SetActive(true);
            }
        }

        private void OnCountdownUpdated(string text)
        {
            _countdown.text = text;
        }

        private void OnProgressUpdated(string message)
        {
            _progress.text = message;
        }

        private void OnFinishImageCreated(Sprite? image)
        {
            _finishImage.sprite = image != null ? image : _finishPlaceholder;
        }

        private void OnOkPressed()
        {
            string text = _input.text;
            _input.ClearInput();
            _messageManager.SendMessage(text);
        }

        [UsedImplicitly]
        [UIAction("show-modal")]
        private void ShowModal()
        {
            _modal.Show(true);
        }

        [UsedImplicitly]
        [UIAction("toend-click")]
        private void OnToEndClick()
        {
            _scrollView.ScrollToEnd(true);
        }

        [UsedImplicitly]
        [UIAction("start-click")]
        private void OnStartClick()
        {
            StartLevel?.Invoke();
        }
    }
}
