using JetBrains.Annotations;

namespace Synapse.Server.Models;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record SerializedBannedUser : SerializedUser
{
    public string Reason { get; init; } = string.Empty;

    public DateTime? BanTime { get; init; }

    public override string ToString()
    {
        string result = $"{base.ToString()} [{BanTime?.ToString() ?? "permanently"}] [{Reason}]";
        return result;
    }
}
