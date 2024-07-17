using System;

namespace Synapse.Extras;

internal static class TimeSpanExtensions
{
    // a big enough number that we'll never feasibly reach, and won't overflow when doing operations with time spans
    private const int MAX_SECONDS = int.MaxValue;
    private static readonly TimeSpan _maxTimeSpan = TimeSpan.FromSeconds(MAX_SECONDS);

    internal static TimeSpan ToTimeSpan(this float seconds)
    {
        return seconds < MAX_SECONDS ? TimeSpan.FromSeconds(seconds) : _maxTimeSpan;
    }
}
