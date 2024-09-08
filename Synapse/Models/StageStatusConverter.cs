using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Synapse.Networking.Models;

namespace Synapse.Models;

public class StageStatusConverter : JsonConverter<IStageStatus>
{
    public override bool CanWrite => false;

    public override IStageStatus ReadJson(
        JsonReader reader,
        Type objectType,
        IStageStatus? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        JObject obj = JObject.Load(reader);
        string type = (string?)obj["name"] ?? throw new InvalidOperationException("No stage name.");
        IStageStatus result = type switch
        {
            "intro" => new IntroStatus(),
            "play" => new PlayStatus(),
            "finish" => new FinishStatus(),
            _ => throw new ArgumentOutOfRangeException($"{type} out of range.")
        };

        serializer.Populate(obj.CreateReader(), result);

        return result;
    }

    public override void WriteJson(JsonWriter writer, IStageStatus? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
