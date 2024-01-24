using System;

namespace SRT.Models
{
    public record Status
    {
        public int PlayStatus { get; set; } = -1;

        public string Motd { get; set; } = string.Empty;

        public int Index { get; set; } = -1;

        public Map Map { get; set; } = new();
    }

    public record Map
    {
        public string Name { get; set; } = string.Empty;

        public string Characteristic { get; set; } = string.Empty;

        public int Difficulty { get; set; }

        public string DownloadUrl { get; set; } = string.Empty;

        public Ruleset? Ruleset { get; set; }
    }

    public record Ruleset
    {
        public bool? AllowOverrideColors { get; set; }

        public string[]? Modifiers { get; set; } = Array.Empty<string>();

        public bool? AllowLeftHand { get; set; }
    }
}
