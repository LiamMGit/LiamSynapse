using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Synapse.Models
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public record Listing
    {
        public string Guid { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string IpAddress { get; init; } = string.Empty;

        public string BannerImage { get; init; } = string.Empty;

        public string BannerColor { get; init; } = string.Empty;

        public DateTime Time { get; init;  }

        public string LobbyBundle { get; init; } = string.Empty;

        public uint BundleCrc { get; init; }

        public List<RequiredMod> RequiredMods { get; init; } = new(0);

        internal TimeSpan TimeSpan => Time - DateTime.UtcNow;
    }
}
