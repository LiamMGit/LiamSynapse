using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using JetBrains.Annotations;
using SiraUtil.Logging;
using SRT.Controllers;
using SRT.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SRT.Managers
{
    public sealed class MessageManager : IDisposable
    {
        private readonly NetworkManager _networkManager;

        [UsedImplicitly]
        private MessageManager(SiraLog log, NetworkManager networkManager)
        {
            _networkManager = networkManager;
            networkManager.Closed += OnClosed;
            networkManager.Connecting += OnConnecting;
            networkManager.ChatRecieved += OnChatMessageRecieved;
            networkManager.MotdUpdated += OnMotdUpdated;
        }

        public event Action<ChatMessage>? MessageRecieved;

        public void Dispose()
        {
            _networkManager.Closed -= OnClosed;
            _networkManager.Connecting -= OnConnecting;
            _networkManager.ChatRecieved -= OnChatMessageRecieved;
            _networkManager.MotdUpdated -= OnMotdUpdated;
        }

        public void SendMessage(string message)
        {
            _ = SendMessageAsync(message);
        }

        public async Task SendMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            await _networkManager.SendString(message, ServerOpcode.ChatMessage);
        }

        public void RefreshMotd()
        {
            OnMotdUpdated(_networkManager.Status.Motd);
        }

        private void OnClosed(ClosedReason closedReason)
        {
            RelaySystemMessage("Connection closed unexpectedly, reconnecting...");
        }

        private void OnConnecting(Stage stage, int retries)
        {
            switch (stage)
            {
                case Stage.Failed:
                case Stage.Connecting:
                    return;
            }

            string text = stage switch
            {
                ////Stage.Connecting => "Connecting...",
                Stage.Authenticating => "Authenticating...",
                Stage.ReceivingData => "Receiving data...",
                Stage.Timeout => "Connection timed out, retrying...",
                Stage.Refused => "Connection refused, retrying...",
                _ => $"{(SocketError)stage}, retrying..."
            };

            if (retries > 0)
            {
                text += $" ({retries + 1})";
            }

            RelaySystemMessage(text);
        }

        private void OnMotdUpdated(string message)
        {
            OnChatMessageRecieved(new ChatMessage(string.Empty, "Server", message));
        }

        private void OnChatMessageRecieved(ChatMessage message)
        {
            MessageRecieved?.Invoke(message);
        }

        private void RelaySystemMessage(string message)
        {
            message = $"<color=\"yellow\">{message}</color>";
            MessageRecieved?.Invoke(new ChatMessage(string.Empty, "System", message));
        }
    }
}
