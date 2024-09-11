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

        int count = _rateLimit.AddOrUpdate(index, 1, (_, n) => n + 1);
        if (count > max)
        {
            return true;
        }

        _ = Task
            .Delay(milliseconds)
            .ContinueWith(_ => { _rateLimit.TryRemove(index, out int _); });
        return false;
    }

    public static void Timeout(Action action, int milliseconds, [CallerMemberName] string memberName = "")
    {
        if (_timeoutFinished.AddOrUpdate(memberName, false, (_, _) => true))
        {
            return;
        }

        action();
        _ = TimeoutTask(memberName, action, milliseconds);
    }

    private static async Task TimeoutTask(string index, Action action, int milliseconds)
    {
        while (true)
        {
            await Task.Delay(milliseconds);
            if (_timeoutFinished.TryUpdate(index, false, true))
            {
                action();
                continue;
            }

            _timeoutFinished.TryRemove(index, out bool _);
            break;
        }
    }
}
