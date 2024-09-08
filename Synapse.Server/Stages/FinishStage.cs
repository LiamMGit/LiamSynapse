using Microsoft.Extensions.Configuration;
using Synapse.Networking.Models;
using Synapse.Server.Clients;
using Synapse.Server.Services;

namespace Synapse.Server.Stages;

public class FinishStage : Stage
{
    private readonly IMapService _mapService;
    private readonly string _finalMotd;
    private readonly string _url;

    public FinishStage(IConfiguration config, IMapService mapService)
    {
        _mapService = mapService;
        IConfigurationSection finishSection = config.GetRequiredSection("Event").GetRequiredSection("Finish");
        _finalMotd = finishSection.GetRequiredSection("Motd").Get<string>() ?? string.Empty;
        _url = finishSection.GetRequiredSection("Url").Get<string>() ?? string.Empty;
    }

    public override Status GetStatus()
    {
        return new Status
        {
            Motd = _finalMotd,
            Stage = new FinishStatus
            {
                Url = _url,
                MapCount = _mapService.MapCount
            }
        };
    }

    public override void PrintStatus(IClient client)
    {
        client.SendServerMessage("Finished with event!");
    }
}
