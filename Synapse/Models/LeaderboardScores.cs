using System.Collections.Generic;
using JetBrains.Annotations;

namespace Synapse.Models
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public record LeaderboardScores
    {
        public int Index { get; init; }

        public string Title { get; init; } = string.Empty;

        public int PlayerScoreIndex { get; init; } = -1;

        public List<LeaderboardCell> Scores { get; init; } = new();

        public int ElimPlayerScoreIndex { get; init; } = -1;

        public List<LeaderboardCell> ElimScores { get; init; } = new();
    }

    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public record LeaderboardCell
    {
        public int Rank { get; init; }

        public string PlayerName { get; init; } = string.Empty;

        public int Score { get; init; }

        public float Accuracy { get; init; }

        public string Color { get; init; } = string.Empty;
    }
}
