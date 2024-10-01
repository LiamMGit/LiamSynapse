using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using JetBrains.Annotations;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace Synapse;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class Config
{
    public bool? JoinChat { get; set; }

    public EventInfo LastEvent { get; set; } = new();

    public bool MuteMusic { get; set; }

    public bool ProfanityFilter { get; set; } = true;

    public bool ShowEliminated { get; set; } = true;

    public string Url { get; set; } = "https://event.aeroluna.dev/api/v1/directory";
}

public class EventInfo
{
    public string Title { get; set; } = string.Empty;

    public bool SeenIntro { get; set; }

    public int? Division { get; set; }
}
