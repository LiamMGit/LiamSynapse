using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using JetBrains.Annotations;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace Synapse
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public class Config
    {
        public string Url { get; set; } = "https://event.aeroluna.dev/api/v1/directory";

        public bool ShowEliminated { get; set; } = true;

        public bool? JoinChat { get; set; }

        public bool ProfanityFilter { get; set; } = true;

        public bool MuteMusic { get; set; }
    }
}
