using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Synapse.Extras;

[UsedImplicitly]
internal class PingManager
{
    private readonly Stopwatch _stopwatch = new();
    private CancellationTokenSource _cancellationToken = new();
    private bool _finished;

    internal event Action<string>? Finished;

    internal void Start()
    {
        _cancellationToken.Cancel();
        _finished = false;
        _cancellationToken = new CancellationTokenSource();
        _ = Timeout(_cancellationToken.Token);
        _stopwatch.Restart();
    }

    internal void Stop()
    {
        _cancellationToken.Cancel();
        Finish();
    }

    private void Finish()
    {
        if (_finished)
        {
            return;
        }

        _stopwatch.Stop();
        _finished = true;
        long ms = _stopwatch.ElapsedMilliseconds;
        Finished?.Invoke($"{(ms > 999 ? "999+" : ms)} ms");
    }

    private async Task Timeout(CancellationToken cancellationToken)
    {
        await Task.Delay(1000, cancellationToken);
        Finish();
    }
}
