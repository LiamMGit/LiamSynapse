using Newtonsoft.Json;

namespace SRT.Models
{
    public record RequiredMod
    {
        [JsonProperty("id")]
        public string Id { get; private set; } = string.Empty;

        [JsonProperty("version")]
        public string Version { get; private set; } = string.Empty;

        [JsonProperty("url")]
        public string Url { get; private set; } = string.Empty;
    }
}
