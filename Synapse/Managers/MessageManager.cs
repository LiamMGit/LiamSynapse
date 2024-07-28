using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SiraUtil.Logging;
using Synapse.Models;

namespace Synapse.Managers;

internal sealed class MessageManager : IDisposable
{
    private readonly NetworkManager _networkManager;
    private readonly TimeSyncManager _timeSyncManager;

    [UsedImplicitly]
    private MessageManager(SiraLog log, NetworkManager networkManager, TimeSyncManager timeSyncManager)
    {
        _networkManager = networkManager;
        _timeSyncManager = timeSyncManager;
        networkManager.Closed += OnClosed;
        networkManager.Connecting += OnConnecting;
        networkManager.ChatReceived += OnChatMessageReceived;
        networkManager.MotdUpdated += OnMotdUpdated;
    }

    internal event Action<ChatMessage>? MessageReceived;

    public void Dispose()
    {
        _networkManager.Closed -= OnClosed;
        _networkManager.Connecting -= OnConnecting;
        _networkManager.ChatReceived -= OnChatMessageReceived;
        _networkManager.MotdUpdated -= OnMotdUpdated;
    }

    internal void RefreshMotd()
    {
        OnMotdUpdated(_networkManager.Status.Motd);
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
            if (message.Length > 1)
            {
                message = message.Substring(1);
                if (message == "ping")
                {
                    float latency = _timeSyncManager.Latency * 1000;
                    MessageReceived?.Invoke(
                        new ChatMessage(
                            string.Empty,
                            string.Empty,
                            "yellow",
                            MessageType.System,
                            $"{(latency > 999 ? "999+" : latency.ToString("F0"))} ms"));
                }
                else
                {
                    await _networkManager.Send(ServerOpcode.Command, message);
                }
            }
        }
        else
        {
            await _networkManager.Send(ServerOpcode.ChatMessage, message);
        }
    }

    private void OnChatMessageReceived(ChatMessage messages)
    {
        MessageReceived?.Invoke(messages);
    }

    private void OnClosed(ClosedReason closedReason)
    {
        RelaySystemMessage("Connection closed unexpectedly, reconnecting...");
    }

    private void OnConnecting(ConnectingStage connectingStage, int retries)
    {
        switch (connectingStage)
        {
            case ConnectingStage.Failed:
            case ConnectingStage.Connecting:
                return;
        }

        string text = connectingStage switch
        {
            ////Stage.Connecting => "Connecting...",
            ConnectingStage.Authenticating => "Authenticating...",
            ConnectingStage.ReceivingData => "Receiving data...",
            ConnectingStage.Timeout => "Connection timed out, retrying...",
            ConnectingStage.Refused => "Connection refused, retrying...",
            _ => $"{(SocketError)connectingStage}, retrying..."
        };

        if (retries > 0)
        {
            text += $" ({retries + 1})";
        }

        RelaySystemMessage(text);
    }

    private void OnMotdUpdated(string message)
    {
        MessageReceived?.Invoke(new ChatMessage(string.Empty, string.Empty, null, MessageType.System, message));
    }

    private void RelaySystemMessage(string message)
    {
        ////message = $"<color=\"yellow\">{message}</color>";
        MessageReceived?.Invoke(new ChatMessage(string.Empty, string.Empty, "yellow", MessageType.System, message));
    }
}
