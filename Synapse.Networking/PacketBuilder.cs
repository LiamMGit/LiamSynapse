using System;
using System.IO;

namespace Synapse.Networking;

public sealed class PacketBuilder : IDisposable
{
    private readonly object _lock = new();

    private readonly MemoryStream _stream;
    private readonly BinaryWriter _writer;

    public PacketBuilder(byte opcode)
    {
        // slower than recyclablememorystream, but we'll find out if its a problem
        _stream = new MemoryStream();
        _writer = new BinaryWriter(_stream);
        _writer.Write(opcode);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _stream.Dispose();
            _writer.Dispose();
        }
    }

    public byte[] ToArray()
    {
        byte[] bytes = _stream.GetBuffer();
        ushort ushortLength = checked((ushort)bytes.Length);
        byte[] newValues = new byte[bytes.Length + 2];
        newValues[0] = (byte)ushortLength;
        newValues[1] = (byte)((uint)ushortLength >> 8);
        Array.Copy(bytes, 0, newValues, 2, bytes.Length);
        return newValues;
    }

    public void Write(byte value)
    {
        lock (_lock)
        {
            _writer.Write(value);
        }
    }

    public void Write(bool value)
    {
        lock (_lock)
        {
            _writer.Write(value);
        }
    }

    public void Write(string value)
    {
        lock (_lock)
        {
            _writer.Write(value);
        }
    }

    public void Write(float value)
    {
        lock (_lock)
        {
            _writer.Write(value);
        }
    }

    public void Write(int value)
    {
        lock (_lock)
        {
            _writer.Write(value);
        }
    }
}
