namespace Synapse.Models;

public enum MessageType
{
    System,
    Say,
    WhisperFrom,
    WhisperTo
}

public readonly struct ChatMessage(string id, string username, string? color, MessageType type, string message)
{
    public string Id { get; } = id;

    public string Username { get; } = username;

    public string? Color { get; } = color;

    public MessageType Type { get; } = type;

    public string Message { get; } = message;
}
