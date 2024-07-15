using JetBrains.Annotations;

namespace Synapse.Models;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record ScoreSubmission
{
    public int Index { get; init; }

    public int Score { get; init; }

    public float Percentage { get; init; }
}
