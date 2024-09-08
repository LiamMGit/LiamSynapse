using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using Synapse.Server.Models;

namespace Synapse.Server.Services;

public interface IMapService
{
    public event Action? IndexChanged;

    public ConfigMap CurrentMap { get; }

    public int MapCount { get; }

    public IReadOnlyList<ConfigMap> Maps { get; }

    public int Index { get; set; }
}

public class MapService : IMapService
{
    private int _index;

    public MapService(IConfiguration config)
    {
        IConfigurationSection eventSection = config.GetRequiredSection("Event");
        Maps = eventSection.GetRequiredSection("Maps").Get<List<ConfigMap>>()?.ToImmutableList() ??
               throw new InvalidOperationException();
        MapCount = Maps.Count;
    }

    public event Action? IndexChanged;

    public ConfigMap CurrentMap => Maps[_index];

    public int MapCount { get; }

    public IReadOnlyList<ConfigMap> Maps { get; }

    public int Index
    {
        get => _index;
        set
        {
            _index = value;
            IndexChanged?.Invoke();
        }
    }
}
