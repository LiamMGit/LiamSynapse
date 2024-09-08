using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Synapse.Networking.Models;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public record Listing
{
    public string Guid { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string IpAddress { get; init; } = string.Empty;

    public string BannerImage { get; init; } = string.Empty;

    public string BannerColor { get; init; } = string.Empty;

    public DateTime Time { get; init; }

    public List<BundleInfo> Bundles { get; init; } = [];

    public List<RequiredMods> RequiredMods { get; init; } = [];
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class BundleInfo
{
    public string GameVersion { get; init; } = string.Empty;

    public uint Hash { get; init; }

    public string Url { get; init; } = string.Empty;
}
