using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Synapse.Networking;

public class AsyncTcpServerClient(Socket socket, CancellationToken token) : AsyncTcpClient
{
    public override Socket Socket { get; } = socket;

    protected override CancellationTokenSource Cts { get; } = CancellationTokenSource.CreateLinkedTokenSource(token);

    protected override async Task ConnectAsync(CancellationToken token)
    {
        try
        {
            await ReadAsync(Socket, token);
        }
        finally
        {
            SendMessage(new AsyncTcpMessageEventArgs(Networking.Message.ConnectionClosed));
        }
    }
}
