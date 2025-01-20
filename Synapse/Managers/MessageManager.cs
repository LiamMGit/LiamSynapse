using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SiraUtil.Logging;
using Synapse.Networking.Models;

namespace Synapse.Managers;

internal sealed class MessageManager : IDisposable
{
    private readonly NetworkManager _networkManager;
    private readonly TimeSyncManager _timeSyncManager;
    private readonly Config _config;

    [UsedImplicitly]
    private MessageManager(SiraLog log, NetworkManager networkManager, TimeSyncManager timeSyncManager, Config config)
    {
        _networkManager = networkManager;
        _timeSyncManager = timeSyncManager;
        _config = config;
        networkManager.Closed += OnClosed;
        networkManager.Connecting += OnConnecting;
        networkManager.ChatReceived += OnChatMessageReceived;
        networkManager.JoinLeaveReceived += OnJoinLeaveReceived;
        networkManager.MotdUpdated += OnMotdUpdated;
    }

    internal event Action<ChatMessage>? MessageReceived;

    public void Dispose()
    {
        _networkManager.Closed -= OnClosed;
        _networkManager.Connecting -= OnConnecting;
        _networkManager.ChatReceived -= OnChatMessageReceived;
        _networkManager.JoinLeaveReceived -= OnJoinLeaveReceived;
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
                            MessageType.PrioritySystem,
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
            if (_config.JoinChat ?? false)
            {
                await _networkManager.Send(ServerOpcode.ChatMessage, message);
            }
            else
            {
                RelaySystemMessage("You must join chat from the settings to send messages!");
            }
        }
    }

    private void OnChatMessageReceived(ChatMessage messages)
    {
        MessageReceived?.Invoke(messages);
    }

    private void OnJoinLeaveReceived(bool join, string username)
    {
        if (!_config.ShowJoinLeaveMessages)
        {
            return;
        }

        string message = $"{username} has {(join ? "joined" : "left")}";
        MessageReceived?.Invoke(new ChatMessage(string.Empty, string.Empty, "yellow", MessageType.System, message));
    }

    private void OnClosed()
    {
        RelaySystemMessage("Connection closed unexpectedly, reconnecting...");
    }

    private void OnConnecting(ConnectingStage connectingStage, int retries)
    {
        string text = connectingStage switch
        {
            ConnectingStage.Connecting => "Connecting...",
            ConnectingStage.Authenticating => "Authenticating...",
            ConnectingStage.ReceivingData => "Receiving data...",
            ConnectingStage.Timeout => "Connection timed out, retrying...",
            ConnectingStage.Refused => "Connection refused, retrying...",
            _ => throw new InvalidOperationException()
        };

        if (retries > 0)
        {
            text += $" ({retries + 1})";
        }

        RelaySystemMessage(text);
    }

    private void OnMotdUpdated(string message)
    {
        MessageReceived?.Invoke(new ChatMessage(string.Empty, string.Empty, null, MessageType.PrioritySystem, message));
    }

    private void RelaySystemMessage(string message)
    {
        ////message = $"<color=\"yellow\">{message}</color>";
        MessageReceived?.Invoke(new ChatMessage(string.Empty, string.Empty, "yellow", MessageType.PrioritySystem, message));
    }
}
