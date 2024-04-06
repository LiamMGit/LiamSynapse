using System.Collections.Generic;
using JetBrains.Annotations;

namespace Synapse.Models
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public class RequiredMods
    {
        public string GameVersion { get; init; } = string.Empty;

        public List<ModInfo> Mods { get; init; } = new();
    }

    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public class ModInfo
    {
        public string Id { get; init; } = string.Empty;

        public string Version { get; init; } = string.Empty;

        public string Url { get; init; } = string.Empty;
    }
}
