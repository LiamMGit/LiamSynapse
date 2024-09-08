using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Synapse.Server.Services;

namespace Synapse.Server.TournamentFormats;

public interface IFormatFactory
{
    public ITournamentFormat Create();
}

public class FormatFactory : IFormatFactory
{
    private readonly string _format;
    private readonly IListenerService _listenerService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMapService _mapService;

    public FormatFactory(
        ILoggerFactory loggerFactory,
        IListenerService listenerService,
        IMapService mapService,
        IConfiguration config)
    {
        _loggerFactory = loggerFactory;
        _listenerService = listenerService;
        _mapService = mapService;
        IConfigurationSection eventSection = config.GetRequiredSection("Event");
        _format = eventSection.GetRequiredSection("Format").Get<string>() ?? throw new InvalidOperationException();
    }

    public ITournamentFormat Create()
    {
        return _format switch
        {
            "Showcase" => new ShowcaseFormat(
                _loggerFactory.CreateLogger<ShowcaseFormat>(),
                _listenerService,
                _mapService),
            _ => throw new InvalidOperationException()
        };
    }
}
