using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SRT.Models
{
    public class Listing
    {
        [JsonProperty("title")]
        public string Title { get; private set; } = string.Empty;

        [JsonProperty("ipAddress")]
        public string IpAddress { get; private set; } = string.Empty;

        [JsonProperty("bannerImage")]
        public string BannerImage { get; private set; } = string.Empty;

        [JsonProperty("time")]
        public DateTime Time { get; private set;  }

        [JsonProperty("requiredMods")]
        public List<RequiredMod> RequiredMods { get; private set; } = new(0);

        public TimeSpan TimeSpan { get; internal set; }
    }
}
