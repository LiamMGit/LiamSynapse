using Newtonsoft.Json;

namespace Synapse.Models
{
    public record RequiredMod
    {
        [JsonProperty("id")]
        public string Id { get; init; } = string.Empty;

        [JsonProperty("version")]
        public string Version { get; init; } = string.Empty;

        [JsonProperty("url")]
        public string Url { get; init; } = string.Empty;
    }
}
