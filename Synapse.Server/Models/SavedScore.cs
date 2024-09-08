using JetBrains.Annotations;

namespace Synapse.Server.Models;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public readonly struct SavedScore : IComparable<SavedScore>
{
    internal SavedScore(int score, float percentage, string id, string username)
    {
        Score = score;
        Percentage = percentage;
        Id = id;
        Username = username;
    }

    public int Score { get; init; }

    public float Percentage { get; init; }

    public string Id { get; init; }

    public string Username { get; init; }

    public int CompareTo(SavedScore other)
    {
        return other.Score.CompareTo(Score);
    }

    public override string ToString()
    {
        return $"({Id}) {Username}: {Score}";
    }
}
