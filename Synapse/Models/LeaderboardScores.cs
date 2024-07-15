using System.Collections.Generic;
using JetBrains.Annotations;

namespace Synapse.Models;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record LeaderboardScores
{
    public int Index { get; init; }

    public string Title { get; init; } = string.Empty;

    public int PlayerScoreIndex { get; init; } = -1;

    public List<LeaderboardCell> Scores { get; init; } = [];

    public int ElimPlayerScoreIndex { get; init; } = -1;

    public List<LeaderboardCell> ElimScores { get; init; } = [];
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record LeaderboardCell
{
    public int Rank { get; init; }

    public string PlayerName { get; init; } = string.Empty;

    public int Score { get; init; }

    public float Percentage { get; init; }

    public string Color { get; init; } = string.Empty;
}
