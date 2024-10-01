using JetBrains.Annotations;

namespace Synapse.Networking.Models;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record ScoreSubmission
{
    public int Division { get; init; }

    public int Index { get; init; }

    public int Score { get; init; }

    public float Percentage { get; init; }
}
