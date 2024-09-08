using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Synapse.Server.Extras;

public static class RateLimiter
{
    public static readonly ConcurrentDictionary<string, int> _rateLimit = new();
    public static readonly ConcurrentDictionary<string, bool> _timeoutFinished = new();

    public static bool RateLimit(object id, int max, int milliseconds, [CallerMemberName] string memberName = "")
    {
        string index = $"{id.GetHashCode()}/{memberName}";
        if (_rateLimit.TryGetValue(index, out int count))
        {
            count++;
            if (count > max)
            {
                return true;
            }

            _rateLimit[index] = count;
            return false;
        }

        _rateLimit[index] = 1;
        _ = Task
            .Delay(TimeSpan.FromMilliseconds(milliseconds))
            .ContinueWith(_ => { _rateLimit.TryRemove(index, out int _); });
        return false;
    }

    public static void Timeout(Action action, int milliseconds, [CallerMemberName] string memberName = "")
    {
        string index = memberName;
        if (_timeoutFinished.ContainsKey(index))
        {
            _timeoutFinished[index] = true;
            return;
        }

        action();
        _timeoutFinished[index] = false;
        _ = Task
            .Delay(TimeSpan.FromMilliseconds(milliseconds))
            .ContinueWith(
                _ =>
                {
                    if (_timeoutFinished[index])
                    {
                        action();
                    }

                    _timeoutFinished.TryRemove(index, out bool _);
                });
    }
}
