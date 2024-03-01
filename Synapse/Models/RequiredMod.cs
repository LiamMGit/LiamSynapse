using Newtonsoft.Json;

namespace Synapse.Models
{
    public record RequiredMod
    {
        public string Id { get; init; } = string.Empty;

        public string Version { get; init; } = string.Empty;

        public string Url { get; init; } = string.Empty;
    }
}
