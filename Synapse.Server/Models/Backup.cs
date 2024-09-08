using JetBrains.Annotations;

namespace Synapse.Server.Models;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public readonly struct Backup
{
    public int Index { get; init; }

    public IReadOnlyList<SavedScore> Scores { get; init; }

    public IReadOnlyList<string>? ActivePlayers { get; init; }
}
