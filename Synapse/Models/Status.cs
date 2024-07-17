using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Synapse.Models;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record Status
{
    public string Motd { get; private set; } = string.Empty;

    public int Index { get; private set; } = -1;

    public float? StartTime { get; private set; }

    public PlayerScore? PlayerScore { get; init; }

    public Map? Map { get; init; }
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record PlayerScore
{
    public int Score { get; init; }

    public float Percentage { get; init; }
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record Map
{
    public string Name { get; init; } = string.Empty;

    public string Characteristic { get; init; } = string.Empty;

    public int Difficulty { get; init; }

    public string? AltCoverUrl { get; init; } = string.Empty;

    public Ruleset? Ruleset { get; init; }

    public List<Download> Downloads { get; init; } = [];
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record Ruleset
{
    public bool? AllowOverrideColors { get; init; }

    public string[]? Modifiers { get; init; } = [];

    public bool? AllowLeftHand { get; init; }

    public bool? AllowResubmission { get; init; }
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record Download
{
    public string GameVersion { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string Hash { get; init; } = string.Empty;
}
