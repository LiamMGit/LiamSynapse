using JetBrains.Annotations;
using Synapse.Networking.Models;

namespace Synapse.Server.Models;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record ConfigMap
{
    public string Name { get; init; } = string.Empty;

    public string Characteristic { get; init; } = string.Empty;

    public int Difficulty { get; init; }

    public string? AltCoverUrl { get; init; } = string.Empty;

    public string Motd { get; init; } = string.Empty;

    public TimeSpan Intermission { get; init; }

    public TimeSpan Duration { get; init; }

    public Ruleset? Ruleset { get; init; }

    public List<Key> Keys { get; init; } = [];

    public List<Download> Downloads { get; init; } = [];
}
