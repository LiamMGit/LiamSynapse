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
