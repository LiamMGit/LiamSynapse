using System;

namespace SRT.Models
{
    public record Status
    {
        public int PlayStatus { get; private set; } = -1;

        public string Motd { get; private set; } = string.Empty;

        public int Index { get; private set; } = -1;

        public Map Map { get; private set; } = new();
    }

    public record Map
    {
        public string Name { get; private set; } = string.Empty;

        public string Characteristic { get; private set; } = string.Empty;

        public int Difficulty { get; private set; }

        public string DownloadUrl { get; private set; } = string.Empty;

        public Ruleset? Ruleset { get; private set; }
    }

    public record Ruleset
    {
        public bool? AllowOverrideColors { get; private set; }

        public string[] Modifiers { get; private set; } = Array.Empty<string>();

        public bool? AllowLeftHand { get; private set; }
    }
}
