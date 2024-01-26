using System.Collections.Generic;

namespace SRT.Models
{
    public record LeaderboardScores
    {
        public int Index { get; set; }

        public int PlayerScoreIndex { get; set; } = -1;

        public List<LeaderboardScore> Scores { get; set; } = new();
    }

    public record LeaderboardScore
    {
        public int Rank { get; set; }

        public string PlayerName { get; set; } = string.Empty;

        public int Score { get; set; }

        public bool FullCombo { get; set; }
    }

}
