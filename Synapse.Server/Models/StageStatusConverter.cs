using System.Text.Json;
using System.Text.Json.Serialization;
using Synapse.Networking.Models;

namespace Synapse.Server.Models;

public class StageStatusConverter : JsonConverter<IStageStatus>
{
    public override IStageStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, IStageStatus value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
