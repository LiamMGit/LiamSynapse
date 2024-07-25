using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Synapse.Extras;
using Synapse.Models;

namespace Synapse.Managers;

[UsedImplicitly]
internal class TimeSyncManager : IDisposable
{
    private readonly RollingAverage _latency = new(30);
    private readonly NetworkManager _networkManager;
    private readonly RollingAverage _serverOffset = new(30);
    private readonly Stopwatch _stopwatch = new();
    private CancellationTokenSource _cancelPingLoop = new();
    private CancellationTokenSource _cancelPingTimeout = new();

    private bool? _didSyncSucceed;
    private TaskCompletionSource<object?>? _pingTask;
    private TaskCompletionSource<bool>? _synced;

    private TimeSyncManager(NetworkManager networkManager)
    {
        _networkManager = networkManager;
        networkManager.PongReceived += Pong;
        networkManager.Connecting += OnConnecting;
        networkManager.Disconnected += OnDisconnected;
    }

    internal event Action<bool>? Synced
    {
        add
        {
            if (_didSyncSucceed != null)
            {
                value?.Invoke(_didSyncSucceed.Value);
                return;
            }

            SyncedBacking += value;
        }

        remove => SyncedBacking -= value;
    }

    private event Action<bool>? SyncedBacking;

    internal float ElapsedSeconds => _stopwatch.ElapsedMilliseconds * 0.001f;

    internal float Latency => _latency.currentAverage;

    internal float Offset => _serverOffset.currentAverage + Latency;

    internal float SyncTime => ElapsedSeconds + Offset;

    public void Dispose()
    {
        _networkManager.PongReceived -= Pong;
        _networkManager.Connecting -= OnConnecting;
        _networkManager.Disconnected -= OnDisconnected;
    }

    internal void OnConnecting(ConnectingStage connectingStage, int retries)
    {
        if (connectingStage == ConnectingStage.ReceivingData)
        {
            _ = StartSync();
        }
    }

    internal void OnDisconnected(string _)
    {
        _cancelPingLoop.Cancel();
        _cancelPingTimeout.Cancel();
        _stopwatch.Stop();
        _pingTask?.TrySetCanceled();
        _pingTask = null;
        _didSyncSucceed = null;
        _synced = null;
    }

    internal async Task Ping()
    {
        using PacketBuilder packetBuilder = new(ServerOpcode.Ping);
        packetBuilder.Write(ElapsedSeconds);
        _cancelPingTimeout.Cancel();
        _cancelPingTimeout = new CancellationTokenSource();
        _pingTask = new TaskCompletionSource<object?>();
        _ = _networkManager.Send(packetBuilder.ToSegment());
        _ = Timeout(_cancelPingTimeout.Token, 1000);
        await _pingTask.Task;
    }

    internal async Task StartSync()
    {
        _latency.Reset();
        _serverOffset.Reset();
        _stopwatch.Restart();
        _synced ??= new TaskCompletionSource<bool>();

        await Ping();
        _didSyncSucceed = Latency < 1000;
        SyncedBacking?.Invoke(_didSyncSucceed.Value);
        SyncedBacking = null;
        _synced.SetResult(_didSyncSucceed.Value);

        if (_didSyncSucceed.Value)
        {
            _cancelPingLoop.Cancel();
            _cancelPingLoop = new CancellationTokenSource();
            _ = PingLoop(_cancelPingLoop.Token, 10000);
        }
    }

    internal Task<bool> WaitForSync()
    {
        _synced ??= new TaskCompletionSource<bool>();
        return _synced.Task;
    }

    private async Task PingLoop(CancellationToken cancellationToken, int millisecondLoop)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Ping();
            await Task.Delay(millisecondLoop, cancellationToken);
        }
    }

    private void Pong(float sentLocalTime, float serverTime)
    {
        float roundTrip = ElapsedSeconds - sentLocalTime;
        _serverOffset.Update(serverTime - ElapsedSeconds);
        UpdateLatency(roundTrip / 2);
    }

    private async Task Timeout(CancellationToken cancellationToken, int milliseconds)
    {
        await Task.Delay(milliseconds, cancellationToken);
        UpdateLatency(milliseconds / 1000f);
    }

    private void UpdateLatency(float value)
    {
        _cancelPingTimeout.Cancel();
        if (_pingTask == null)
        {
            return;
        }

        _latency.Update(value);
        _pingTask.SetResult(null);
        _pingTask = null;
    }
}
