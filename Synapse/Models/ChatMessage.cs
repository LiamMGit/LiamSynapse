namespace Synapse.Models;

public enum MessageType
{
    System,
    Say,
    WhisperFrom,
    WhisperTo
}

public readonly struct ChatMessage
{
    public ChatMessage(string id, string username, string? color, MessageType type, string message)
    {
        Id = id;
        Username = username;
        Color = color;
        Type = type;
        Message = message;
    }

    public string Id { get; }

    public string Username { get; }

    public string? Color { get; }

    public MessageType Type { get; }

    public string Message { get; }
}
