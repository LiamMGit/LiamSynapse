using JetBrains.Annotations;

namespace Synapse.Server.Models;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record SerializedUser
{
    public string Id { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public override string ToString()
    {
        return $"{Username} ({Id})";
    }
}
