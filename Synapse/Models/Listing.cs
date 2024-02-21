using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Synapse.Models
{
    public record Listing
    {
        [JsonProperty("title")]
        public string Title { get; init; } = string.Empty;

        [JsonProperty("ipAddress")]
        public string IpAddress { get; init; } = string.Empty;

        [JsonProperty("bannerImage")]
        public string BannerImage { get; init; } = string.Empty;

        [JsonProperty("bannerColor")]
        public string BannerColor { get; init; } = string.Empty;

        [JsonProperty("time")]
        public DateTime Time { get; init;  }

        [JsonProperty("requiredMods")]
        public List<RequiredMod> RequiredMods { get; init; } = new(0);

        internal TimeSpan TimeSpan => Time - DateTime.UtcNow;
    }
}
