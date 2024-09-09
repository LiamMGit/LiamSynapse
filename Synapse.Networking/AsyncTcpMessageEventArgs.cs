using System;

namespace Synapse.Networking;

public enum Message
{
    Connecting,
    ConnectionFailed,
    ConnectionClosed,
    PacketException
}

/// <summary>
///     Provides data for the <see cref="AsyncTcpClient.Message" /> event.
/// </summary>
public class AsyncTcpMessageEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncTcpMessageEventArgs" /> class.
    /// </summary>
    /// <param name="message">The trace message.</param>
    /// <param name="exception">The exception that was thrown, if any.</param>
    /// <param name="reconnectTries">How many retries have occurred.</param>
    public AsyncTcpMessageEventArgs(Message message, Exception? exception = null, int reconnectTries = -1)
    {
        Message = message;
        Exception = exception;
        ReconnectTries = reconnectTries;
    }

    /// <summary>
    ///     Gets the trace message.
    /// </summary>
    public Message Message { get; }

    /// <summary>
    ///     Gets the exception that was thrown, if any.
    /// </summary>
    public Exception? Exception { get; }

    public int ReconnectTries { get; }
}
