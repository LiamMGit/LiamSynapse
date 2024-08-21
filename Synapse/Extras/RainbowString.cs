using System;
using JetBrains.Annotations;
using UnityEngine;
using Zenject;

namespace Synapse.Extras;

[UsedImplicitly]
internal class RainbowString : ITickable
{
    private const string COLOR_PREFIX = "<color=#RRGGBB>";
    private static readonly char[] _prefixArray = COLOR_PREFIX.ToCharArray();
    private static readonly uint[] _lookup32 = CreateLookup32();

    private readonly RainbowTicker _rainbowTicker;

    private char[] _buffer = [];
    private string? _last;
    private int _length;

    private RainbowString(RainbowTicker rainbowTicker, TickableManager tickableManager)
    {
        _rainbowTicker = rainbowTicker;
        tickableManager.Add(this);
    }

    public void Tick()
    {
        uint[] lookup32 = _lookup32;
        char[] buffer = _buffer;
        int length = _length;
        for (int i = 0; i < length; i++)
        {
            int index = i * 16;
            Color color = _rainbowTicker.ToColor(0.02f * i);
            uint r = lookup32[(byte)(color.r * byte.MaxValue)];
            buffer[index + 8] = (char)r;
            buffer[index + 9] = (char)(r >> 16);
            uint g = lookup32[(byte)(color.g * byte.MaxValue)];
            buffer[index + 10] = (char)g;
            buffer[index + 11] = (char)(g >> 16);
            uint b = lookup32[(byte)(color.b * byte.MaxValue)];
            buffer[index + 12] = (char)b;
            buffer[index + 13] = (char)(b >> 16);
        }
    }

    public char[] ToCharArray()
    {
        return _buffer;
    }

    public override string ToString()
    {
        return new string(_buffer);
    }

    internal void SetString(string newString)
    {
        if (newString == _last)
        {
            return;
        }

        _last = newString;
        if (_length == newString.Length)
        {
            int length = _length;
            char[] buffer = _buffer;
            for (int i = 0; i < length; i++)
            {
                int index = i * 16;
                buffer[index + 15] = newString[i];
            }
        }
        else
        {
            int length = _length = newString.Length;
            char[] buffer = _buffer = new char[length * 16];
            for (int i = 0; i < length; i++)
            {
                int index = i * 16;
                Array.Copy(_prefixArray, 0, buffer, index, 15);
                buffer[index + 15] = newString[i];
            }
        }
    }

    private static uint[] CreateLookup32()
    {
        uint[] result = new uint[256];
        for (int i = 0; i < 256; i++)
        {
            string s = i.ToString("X2");
            result[i] = s[0] + ((uint)s[1] << 16);
        }

        return result;
    }
}
