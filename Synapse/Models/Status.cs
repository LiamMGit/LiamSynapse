using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Synapse.Models;

[JsonConverter(typeof(StageStatusConverter))]
public interface IStageStatus;

public class StageStatusConverter : JsonConverter<IStageStatus>
{
    public override bool CanWrite => false;

    public override IStageStatus ReadJson(
        JsonReader reader,
        Type objectType,
        IStageStatus? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        JObject obj = JObject.Load(reader);
        string type = (string?)obj["name"] ?? throw new InvalidOperationException("No stage name.");
        IStageStatus result = type switch
        {
            "intro" => new IntroStatus(),
            "play" => new PlayStatus(),
            "finish" => new FinishStatus(),
            _ => throw new ArgumentOutOfRangeException($"{type} out of range.")
        };

        serializer.Populate(obj.CreateReader(), result);

        return result;
    }

    public override void WriteJson(JsonWriter writer, IStageStatus? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record Status
{
    public string Motd { get; init; } = string.Empty;

    public IStageStatus Stage { get; init; } = new InvalidStatus();
}

public record InvalidStatus : IStageStatus;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record IntroStatus : IStageStatus
{
    public float StartTime { get; init; } = float.MinValue;
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record PlayStatus : IStageStatus
{
    public int Index { get; init; } = -1;

    public float StartTime { get; init; } = float.MinValue;

    public PlayerScore? PlayerScore { get; init; }

    public Map Map { get; init; } = new();
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record FinishStatus : IStageStatus
{
    public string Url { get; init; } = string.Empty;
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
