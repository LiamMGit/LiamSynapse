using System.Diagnostics;
using JetBrains.Annotations;
using Synapse.Networking.Models;

namespace Synapse.TestClient;

[UsedImplicitly]
internal class TimeSyncManager(Client client) : IDisposable
{
    private static readonly Random _random = new();
    private static readonly Stopwatch _stopwatch = new();

    private const int MAX_TIMEOUT = 10000;

    private CancellationTokenSource _cancelPingTimeout = new();
    private CancellationTokenSource _cancelPingLoop = new();
    private TaskCompletionSource<object?>? _pingTask;

    internal static float ElapsedSeconds => _stopwatch.ElapsedMilliseconds * 0.001f;

    internal float Latency { get; private set; }

    public void Dispose()
    {
        _cancelPingTimeout.Cancel();
        _cancelPingLoop.Cancel();
        _pingTask?.TrySetCanceled();
        _pingTask = null;
    }

    internal async Task Ping()
    {
        await _cancelPingTimeout.CancelAsync();
        _cancelPingTimeout = new CancellationTokenSource();
        _pingTask?.SetResult(null);
        _pingTask = new TaskCompletionSource<object?>();
        _ = client.Send(ServerOpcode.Ping, ElapsedSeconds);
        _ = Timeout(MAX_TIMEOUT, _cancelPingTimeout.Token);
        await _pingTask.Task;
    }

    internal async Task StartSync()
    {
        _stopwatch.Restart();
        await _cancelPingLoop.CancelAsync();
        _cancelPingLoop = new CancellationTokenSource();

        await Ping();
        bool success = Latency < MAX_TIMEOUT;

        if (success)
        {
            await Task.Delay(_random.Next(0, 10000));
            _ = PingLoop(10000, _cancelPingLoop.Token);
        }
    }

    private async Task PingLoop(int millisecondLoop, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Ping();
            await Task.Delay(millisecondLoop, cancellationToken);
        }
    }

    internal void Pong(float sentLocalTime, float serverTime)
    {
        float roundTrip = ElapsedSeconds - sentLocalTime;
        UpdateLatency(roundTrip * 0.5f);
    }

    private async Task Timeout(int milliseconds, CancellationToken cancellationToken)
    {
        await Task.Delay(milliseconds, cancellationToken);
        UpdateLatency(milliseconds * 0.001f);
    }

    private void UpdateLatency(float value)
    {
        _cancelPingTimeout.Cancel();
        if (_pingTask == null)
        {
            return;
        }

        Latency = value;
        TaskCompletionSource<object?>? task = _pingTask;
        _pingTask = null;
        task.SetResult(null);
    }
}
