using System.Text.Json;
using System.Text.Json.Serialization;
using Synapse.Networking.Models;

namespace Synapse.TestClient.Models;

public class StageStatusConverter : JsonConverter<IStageStatus>
{
    public override IStageStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        using JsonDocument jsonDocument = JsonDocument.ParseValue(ref reader);
        string jsonObject = jsonDocument.RootElement.GetRawText();
        string type = jsonDocument.RootElement.GetProperty("name").GetString() ?? throw new InvalidOperationException("No stage name.");
        IStageStatus? result = type switch
        {
            "intro" => JsonSerializer.Deserialize<IntroStatus>(jsonObject, options),
            "play" => JsonSerializer.Deserialize<PlayStatus>(jsonObject, options),
            "finish" => JsonSerializer.Deserialize<FinishStatus>(jsonObject, options),
            _ => throw new ArgumentOutOfRangeException($"{type} out of range.")
        };

        return result ?? throw new InvalidOperationException("Could not deserialize status");
    }

    public override void Write(Utf8JsonWriter writer, IStageStatus value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
