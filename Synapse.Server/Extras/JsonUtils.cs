using System.Text.Json;
using Microsoft.Extensions.Logging;
using Synapse.Server.Models;

namespace Synapse.Server.Extras;

public static class JsonUtils
{
    public static JsonSerializerOptions PrettySettings { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static JsonSerializerOptions Settings { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new StageStatusConverter()
        }
    };

    public static async Task<TResult?> LoadJson<TValue, TResult>(
        ILogger log,
        string path,
        Func<TValue, TResult> transform,
        bool verbatim)
    {
        if (!File.Exists(path))
        {
            if (verbatim)
            {
                log.LogWarning("Could not find [{Path}]", path);
            }

            return default;
        }

        using StreamReader reader = new(path);
        TValue? deserialized = await JsonSerializer.DeserializeAsync<TValue>(reader.BaseStream, Settings);
        if (deserialized != null)
        {
            return transform(deserialized);
        }

        log.LogError("Could not load [{Path}]", path);

        return default;
    }

    public static async Task SaveJson<TSource>(TSource list, string path)
    {
        await using StreamWriter output = new(path);
        await JsonSerializer.SerializeAsync(output.BaseStream, list, PrettySettings);
    }
}
