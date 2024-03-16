using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SiraUtil.Logging;
using Synapse.Models;

namespace Synapse.Managers
{
    internal sealed class MessageManager : IDisposable
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

        internal event Action<ChatMessage>? MessageRecieved;

        public void Dispose()
        {
            _networkManager.Closed -= OnClosed;
            _networkManager.Connecting -= OnConnecting;
            _networkManager.ChatRecieved -= OnChatMessageRecieved;
            _networkManager.MotdUpdated -= OnMotdUpdated;
        }

        internal void SendMessage(string message)
        {
            _ = SendMessageAsync(message);
        }

        internal async Task SendMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (message.StartsWith("/"))
            {
                await _networkManager.SendString(message, ServerOpcode.Command);
            }
            else
            {
                await _networkManager.SendString(message, ServerOpcode.ChatMessage);
            }
        }

        internal void RefreshMotd()
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
            MessageRecieved?.Invoke(new ChatMessage(string.Empty, "Server", MessageType.System, message));
        }

        private void OnChatMessageRecieved(ChatMessage messages)
        {
            MessageRecieved?.Invoke(messages);
        }

        private void RelaySystemMessage(string message)
        {
            ////message = $"<color=\"yellow\">{message}</color>";
            MessageRecieved?.Invoke(new ChatMessage(string.Empty, "System", MessageType.System, message));
        }
    }
}
