using System.Text.Json;
using Synapse.TestClient.Models;

namespace Synapse.TestClient.Extras;

public static class JsonSettings
{
    public static JsonSerializerOptions Settings { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new StageStatusConverter()
        }
    };
}
