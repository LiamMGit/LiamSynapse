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

    public string GameVersion { get; init; } = string.Empty;

    public DateTime Time { get; init; }

    public List<Division> Divisions { get; init; } = [];

    public TakeoverInfo Takeover { get; init; } = new();

    public LobbyInfo Lobby { get; init; } = new();

    public List<RequiredMods> RequiredMods { get; init; } = [];
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class TakeoverInfo
{
    public bool DisableDust { get; init; }

    public string CountdownTMP { get; init; } = string.Empty;

    public bool DisableLogo { get; init; }

    public List<BundleInfo> Bundles { get; init; } = [];
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class LobbyInfo
{
    public bool DisableDust { get; init; }

    public bool DisableSmoke { get; init; }

    public int DepthTextureMode { get; init; }

    public List<BundleInfo> Bundles { get; init; } = [];
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class BundleInfo
{
    public string GameVersion { get; init; } = string.Empty;

    public uint Hash { get; init; }

    public string Url { get; init; } = string.Empty;
}

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class Division
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}
