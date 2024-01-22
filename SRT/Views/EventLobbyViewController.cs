using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HarmonyLib;
using HMUI;
using SiraUtil.Logging;
using SRT.Controllers;
using SRT.Managers;
using SRT.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SRT.Views
{
    //[HotReload(RelativePathToLayout = @"../Resources/Lobby.bsml")]
    [ViewDefinition("SRT.Resources.Lobby.bsml")]
    public class EventLobbyViewController : BSMLAutomaticViewController
    {
        [UIComponent("scrollview")]
        private readonly ScrollView _scrollView = null!;

        [UIComponent("textbox")]
        private readonly VerticalLayoutGroup _textObject = null!;

        private readonly List<ChatMessage> _messageQueue = new();
        private readonly LinkedList<Tuple<ChatMessage, TextMeshProUGUI>> _messages = new();

        private SiraLog _log = null!;
        private MessageManager _messageManager = null!;
        private NetworkManager _networkManager = null!;
        private IInstantiator _instantiator = null!;

        private ScrollViewScroller _scroller = null!;
        private InputFieldView _input = null!;
        private OkRelay _okRelay = null!;

        [Inject]
        private void Construct(SiraLog log, MessageManager messageManager, NetworkManager networkManager, IInstantiator instantiator)
        {
            _log = log;
            _messageManager = messageManager;
            _networkManager = networkManager;
            _instantiator = instantiator;
        }

        private void Start()
        {
            InputFieldView original = Resources.FindObjectsOfTypeAll<InputFieldView>().First(n => n.name == "SearchInputField");
            _input = Instantiate(original, transform);
            _input.name = "EventChatInputField";
            RectTransform rect = (RectTransform)_input.transform;
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(20, 6.83f);
            rect.offsetMax = new Vector2(-20, -65.17f);
            _input._keyboardPositionOffset = new Vector3(0, 60, 0);
            _input._textLengthLimit = 100;
            _instantiator.InstantiateComponent<KeyboardOpener>(_input.gameObject);
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

            if (addedToHierarchy)
            {
                _messageManager.MessageRecieved += OnMessageRecieved;
                _messageManager.RefreshMotd();
                _networkManager.UserBanned += OnUserBanned;
            }
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
            }
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
                bool scrollToEnd = (end < 0) ||
                    (Mathf.Abs(end - _scrollView._destinationPos) < 0.01f);

                foreach (ChatMessage message in _messageQueue)
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

            _messageQueue.Clear();
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
    }
}
