using Synapse.Networking.Models;
using Synapse.Server.Clients;

namespace Synapse.Server.Stages;

public abstract class Stage
{
    public event Action? Finished;

    public event Action<bool>? StatusUpdateRequested;

    public bool Active { get; internal set; }

    public virtual Status AdjustStatus(Status status, IClient client)
    {
        return status;
    }

    public virtual void Enter()
    {
    }

    public abstract Status GetStatus();

    public virtual Task Prepare()
    {
        return Task.CompletedTask;
    }

    public abstract void PrintStatus(IClient client);

    protected void Exit()
    {
        Finished?.Invoke();
    }

    protected void UpdateStatus(bool resetMotd = false)
    {
        StatusUpdateRequested?.Invoke(resetMotd);
    }
}
