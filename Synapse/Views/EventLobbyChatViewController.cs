using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HarmonyLib;
using HMUI;
using JetBrains.Annotations;
using SiraUtil.Logging;
using Synapse.Controllers;
using Synapse.HarmonyPatches;
using Synapse.Managers;
using Synapse.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Synapse.Views;

[ViewDefinition("Synapse.Resources.LobbyChat.bsml")]
internal class EventLobbyChatViewController : BSMLAutomaticViewController
{
    private static readonly ProfanityFilter.ProfanityFilter _profanityFilter = new();

    [UIComponent("chat")]
    private readonly VerticalLayoutGroup _chatObject = null!;

    private readonly List<ChatMessage> _messageQueue = [];
    private readonly LinkedList<Tuple<ChatMessage, TextMeshProUGUI>> _messages = [];

    [UIComponent("modal")]
    private readonly ModalView _modal = null!;

    [UIComponent("scrollview")]
    private readonly ScrollView _scrollView = null!;

    [UIComponent("textbox")]
    private readonly VerticalLayoutGroup _textObject = null!;

    [UIObject("toend")]
    private readonly GameObject _toEndObject = null!;

    private Config _config = null!;
    private CountdownManager _countdownManager = null!;

    private InputFieldView _input = null!;
    private IInstantiator _instantiator = null!;

    private SiraLog _log = null!;
    private MessageManager _messageManager = null!;
    private NetworkManager _networkManager = null!;
    private OkRelay _okRelay = null!;

    internal event Action? StartIntro;

    [UsedImplicitly]
    [UIValue("joinChat")]
    private bool JoinChat
    {
        get => _config.JoinChat ?? false;
        set
        {
            _config.JoinChat = value;
            _ = _networkManager.Send(ServerOpcode.SetChatter, value);
        }
    }

    [UsedImplicitly]
    [UIValue("muteMusic")]
    private bool MuteMusic
    {
        get => _config.MuteMusic;
        set => _config.MuteMusic = value;
    }

    [UsedImplicitly]
    [UIValue("profanityFilter")]
    private bool ProfanityFilter
    {
        get => _config.ProfanityFilter;
        set => _config.ProfanityFilter = value;
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

        if (firstActivation)
        {
            rectTransform.sizeDelta = new Vector2(-40, 0);

            InputFieldView original =
                Resources.FindObjectsOfTypeAll<InputFieldView>().First(n => n.name == "SearchInputField");
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
        }

        // ReSharper disable once InvertIf
        if (addedToHierarchy)
        {
            _messageManager.MessageReceived += OnMessageReceived;
            _messageManager.RefreshMotd();
            _networkManager.UserBanned += OnUserBanned;

            _countdownManager.Refresh();
        }
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

            _messageManager.MessageReceived -= OnMessageReceived;
            _networkManager.UserBanned -= OnUserBanned;
        }
    }

    [UsedImplicitly]
    [Inject]
    private void Construct(
        SiraLog log,
        Config config,
        MessageManager messageManager,
        NetworkManager networkManager,
        CountdownManager countdownManager,
        IInstantiator instantiator)
    {
        _log = log;
        _config = config;
        _messageManager = messageManager;
        _networkManager = networkManager;
        _countdownManager = countdownManager;
        _instantiator = instantiator;
    }

    private void OnMessageReceived(ChatMessage message)
    {
        _messageQueue.Add(message);
    }

    private void OnOkPressed()
    {
        string text = _input.text;
        _input.ClearInput();
        _messageManager.SendMessage(text);
    }

    [UsedImplicitly]
    [UIAction("replay-intro")]
    private void OnReplayIntroClick()
    {
        StartIntro?.Invoke();
    }

    [UsedImplicitly]
    [UIAction("toend-click")]
    private void OnToEndClick()
    {
        _scrollView.ScrollToEnd(true);
    }

    private void OnUserBanned(string id)
    {
        _messageQueue.RemoveAll(n => n.Id == id);
        _messages.Where(n => n.Item1.Id == id).Do(n => n.Item2.text = "<deleted>");
    }

    [UsedImplicitly]
    [UIAction("show-modal")]
    private void ShowModal()
    {
        _modal.Show(true);
    }

    private void Update()
    {
        if (_messageQueue.Count == 0)
        {
            return;
        }

        try
        {
            float end = _scrollView.contentSize - _scrollView.scrollPageSize;
            bool scrollToEnd = end < 0 ||
                               Mathf.Abs(end - _scrollView._destinationPos) < 0.01f;
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
                if (message.Type != MessageType.System && _config.ProfanityFilter)
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
                        BeatSaberUI.CreateText(
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
            _log.Error($"Exception while processing message\n{e}");
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
}
