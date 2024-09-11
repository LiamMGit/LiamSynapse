using System;

namespace Synapse.Networking;

public class AsyncTcpConnectTimeoutException() : Exception("Connection timed out.");

public class AsyncTcpConnectFailedException(Exception e) : Exception("Connection failed.", e);

public class AsyncTcpFailedAfterRetriesException(int retries, Exception e)
    : Exception($"Connection failed after {retries} retries.", e)
{
    public int ReconnectTries { get; } = retries;
}

public class AsyncTcpSocketException(Exception e) : Exception("Connection closed.", e);

public class AsyncTcpMessageTimeoutException() : Exception("Packet took too long to process.");

public class AsyncTcpMessageException(string message) : Exception(message);

public class AsyncTcpMessageTooLongException(int messageLength) : AsyncTcpMessageException($"Packet exceeded maximum length. ({messageLength})");

public class AsyncTcpMessageZeroLengthException() : AsyncTcpMessageException("Packet had zero length.");
