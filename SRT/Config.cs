using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using JetBrains.Annotations;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
namespace SRT
{
    [UsedImplicitly]
    public class Config
    {
        public bool AllowDownload { get; set; }

        public string BundleRepository { get; set; } = "https://aeroluna.dev/bundles/";
    }
}
