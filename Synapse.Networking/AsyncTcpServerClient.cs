using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Synapse.Networking;

public class AsyncTcpServerClient : AsyncTcpClient
{
    private readonly NetworkStream _stream;

    public AsyncTcpServerClient(Socket socket, CancellationToken token)
    {
        Socket = socket;
        _stream = new NetworkStream(socket);
        Stream = _stream;
        Cts = CancellationTokenSource.CreateLinkedTokenSource(token);
    }

    public override Socket Socket { get; }

    protected override CancellationTokenSource Cts { get; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override async Task ConnectAsync(CancellationToken token)
    {
        try
        {
            await ReadAsync(token);
        }
        finally
        {
            SendMessage(new AsyncTcpMessageEventArgs(Networking.Message.ConnectionClosed));
        }
    }
}
