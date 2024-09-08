using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Synapse.Networking;

public abstract class AsyncTcpClient : IDisposable
{
    private bool _active;
    private NetworkStream? _stream;

    public event EventHandler<AsyncTcpMessageEventArgs>? Message;

    public Func<CancellationToken, Task>? ConnectedCallback { get; set; }

    public Func<byte, BinaryReader, CancellationToken, Task>? ReceivedCallback { get; set; }

    public abstract Socket Socket { get; }

    protected virtual CancellationTokenSource Cts { get; } = new();

    public bool IsConnected => _stream is { CanWrite: true };

    protected abstract Task ConnectAsync(CancellationToken token);

    public async Task RunAsync()
    {
        if (_active)
        {
            throw new InvalidOperationException("Already active.");
        }

        _active = true;
        CancellationToken token = Cts.Token;
        token.ThrowIfCancellationRequested();
        await ConnectAsync(token);
    }

    public void Dispose()
    {
        Cts.Cancel();
        Cts.Dispose();
    }

    public async Task Send(byte[] data, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Could not send packet, not connected.");
        }

        await _stream!.WriteAsync(data, 0, data.Length, cancellationToken);
    }

    protected async Task ReadAsync(Socket socket, CancellationToken token)
    {
        using NetworkStream stream = new(socket);
        _stream = stream;

        if (ConnectedCallback != null)
        {
            await ConnectedCallback(token);
        }

        if (ReceivedCallback != null)
        {
            byte[] lengthBuffer = new byte[2];
            while (true)
            {
                try
                {
                    int readLength = await stream.ReadAsync(lengthBuffer, 0, lengthBuffer.Length, token);
                    token.ThrowIfCancellationRequested();
                    if (readLength != lengthBuffer.Length)
                    {
                        throw new EndOfStreamException();
                    }

                    ushort messageLength = BitConverter.ToUInt16(lengthBuffer, 0);
                    byte[] messageBuffer = new byte[messageLength];
                    int offset = 0;
                    while (offset < messageLength)
                    {
                        int bytesRead = await stream.ReadAsync(messageBuffer, offset, messageLength - offset, token);
                        token.ThrowIfCancellationRequested();
                        if (bytesRead == 0)
                        {
                            throw new EndOfStreamException();
                        }

                        offset += bytesRead;
                    }

                    _ = ProcessPacket(ReceivedCallback, messageBuffer, token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw new AsyncTcpSocketException(e);
                }
            }
        }
    }

    protected void SendMessage(AsyncTcpMessageEventArgs args)
    {
        Message?.Invoke(this, args);
    }

    private async Task ProcessPacket(Func<byte, BinaryReader, CancellationToken, Task> func, byte[] data, CancellationToken token)
    {
        try
        {
            using MemoryStream stream = new(data);
            using BinaryReader reader = new(stream);
            byte opcode = reader.ReadByte();
            await func(opcode, reader, token);
        }
        catch (Exception e)
        {
            SendMessage(new AsyncTcpMessageEventArgs(Networking.Message.PacketException, e));
        }
    }
}
