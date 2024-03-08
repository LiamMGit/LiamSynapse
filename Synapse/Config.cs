using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using JetBrains.Annotations;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace Synapse
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public class Config
    {
        public string Url { get; set; } = "http://event.aeroluna.dev/api/v1/directory";

        public bool ShowEliminated { get; set; } = true;

        public bool? JoinChat { get; set; }
    }
}
