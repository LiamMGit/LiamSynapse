using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using JetBrains.Annotations;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace Synapse
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public class Config
    {
        public bool ShowEliminated { get; set; }
    }
}
