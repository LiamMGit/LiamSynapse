using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Synapse.Networking.Models;
#if NET
using System.Buffers;
#endif

namespace Synapse.Networking;

public abstract class AsyncTcpClient : IDisposable
{
    public const int MAXIMUM_MESSAGE_LENGTH = 4096;

    private readonly TaskCompletionSource<object?> _closed = new();
    private bool _disposed;
    private bool _active;

    public event EventHandler<AsyncTcpMessageEventArgs>? Message;

    public Func<CancellationToken, Task>? ConnectedCallback { get; set; }

    public Func<byte, BinaryReader, CancellationToken, Task>? ReceivedCallback { get; set; }

    [PublicAPI]
    public abstract Socket Socket { get; }

    [PublicAPI]
    public NetworkStream? Stream { get; protected set; }

    protected virtual CancellationTokenSource Cts { get; } = new();

    public bool IsConnected => Stream is { CanWrite: true };

    protected bool Closing { get; private set; }

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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Cts.Cancel();
            Cts.Dispose();
        }

        _closed.SetResult(null);

        _disposed = true;
    }

#if NET
    public async Task Send(ReadOnlySequence<byte> data, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Could not send packet, not connected.");
        }

        foreach (ReadOnlyMemory<byte> memory in data)
        {
            await Stream!.WriteAsync(memory, cancellationToken);
        }
    }
#else
    public async Task Send(byte[] data, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Could not send packet, not connected.");
        }

        await Stream!.WriteAsync(data, 0, data.Length, cancellationToken);
    }
#endif

    public async Task Disconnect(DisconnectCode? notifyCode)
    {
        Closing = true;

        if (notifyCode != null && IsConnected)
        {
            // disconnect opcode must be same between server/client
            using PacketBuilder packetBuilder = new((byte)ServerOpcode.Disconnect);
            packetBuilder.Write((byte)notifyCode.Value);
            CancellationTokenSource cts = new();
            _ = Send(packetBuilder.ToBytes(), cts.Token);
            await Task.WhenAny(_closed.Task, Task.Delay(1000, cts.Token));
#if NET
            await cts.CancelAsync();
#else
            cts.Cancel();
#endif
        }

        Dispose();
    }

    protected async Task ReadAsync(CancellationToken token)
    {
        if (Stream == null)
        {
            throw new InvalidOperationException("No stream available");
        }

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
#if NET
                    int readLength = await Stream.ReadAsync(lengthBuffer, token);
#else
                    int readLength = await Stream.ReadAsync(lengthBuffer, 0, lengthBuffer.Length, token);
#endif
                    token.ThrowIfCancellationRequested();
                    if (readLength != lengthBuffer.Length)
                    {
                        throw new EndOfStreamException();
                    }

                    ushort messageLength = BitConverter.ToUInt16(lengthBuffer, 0);
                    switch (messageLength)
                    {
                        case > MAXIMUM_MESSAGE_LENGTH:
                            throw new AsyncTcpMessageTooLongException(messageLength);
                        case 0:
                            throw new AsyncTcpMessageZeroLengthException();
                    }

#if NET
                    byte[] messageBuffer = ArrayPool<byte>.Shared.Rent(messageLength);
#else
                    byte[] messageBuffer = new byte[messageLength];
#endif
                    int offset = 0;
                    while (offset < messageLength)
                    {
#if NET
                        int bytesRead = await Stream.ReadAsync(messageBuffer.AsMemory(offset, messageLength - offset), token);
#else
                        int bytesRead = await Stream.ReadAsync(messageBuffer, offset, messageLength - offset, token);
#endif
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
                catch (AsyncTcpMessageException)
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

#if NET
        ArrayPool<byte>.Shared.Return(data);
#endif
    }
}
