using System;
using JetBrains.Annotations;

namespace Synapse.Models
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public record Status
    {
        public string Motd { get; private set; } = string.Empty;

        public int Index { get; private set; } = -1;

        public DateTime? StartTime { get; private set; }

        public bool HasScore { get; private set; }

        public Map Map { get; private set; } = new();
    }

    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public record Map
    {
        public string Name { get; init; } = string.Empty;

        public string Characteristic { get; init; } = string.Empty;

        public int Difficulty { get; init; }

        public string DownloadUrl { get; init; } = string.Empty;

        public string? AltCoverUrl { get; init; } = string.Empty;

        public Ruleset? Ruleset { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public record Ruleset
    {
        public bool? AllowOverrideColors { get; init; }

        public string[]? Modifiers { get; init; } = Array.Empty<string>();

        public bool? AllowLeftHand { get; init; }

        public bool? AllowResubmission { get; init; }
    }
}
