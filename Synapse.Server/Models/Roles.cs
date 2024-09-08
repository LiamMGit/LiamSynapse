using JetBrains.Annotations;

namespace Synapse.Server.Models;

[Flags]
public enum Permission
{
    Coordinator = 0b1,
    Moderator = 0b10
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record SerializedRoleUser : SerializedUser
{
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    public override string ToString()
    {
        return base.ToString() + $" [{string.Join(", ", Roles)}]";
    }
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record Role
{
    public string Name { get; init; } = string.Empty;

    public int Priority { get; init; }

    public string? Color { get; init; }

    public Permission Permission { get; init; }
}
