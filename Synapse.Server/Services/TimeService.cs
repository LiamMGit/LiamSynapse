using System.Diagnostics;

namespace Synapse.Server.Services;

public interface ITimeService
{
    public float Time { get; }
}

public class TimeService : ITimeService
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public float Time => _stopwatch.ElapsedMilliseconds * 0.001f;
}
