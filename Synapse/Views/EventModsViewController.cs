using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using Hive.Versioning;
using IPA.Loader;
using JetBrains.Annotations;
using SiraUtil.Logging;
using Synapse.Extras;
using Synapse.Managers;
using Synapse.Networking.Models;
using TMPro;
using UnityEngine;
using Zenject;

namespace Synapse.Views;

////[HotReload(RelativePathToLayout = @"../Resources/Mods.bsml")]
[ViewDefinition("Synapse.Resources.Mods.bsml")]
internal class EventModsViewController : BSMLAutomaticViewController
{
    private static string? _contentBsml;

    [UsedImplicitly]
    [UIObject("contentObject")]
    private readonly GameObject? _contentObject;

    [UsedImplicitly]
    [UIValue("contents")]
    private readonly List<object> _contents = [];

    [UsedImplicitly]
    [UIComponent("header")]
    private readonly TMP_Text _header = null!;

    private Listing? _listing;

    private SiraLog _log = null!;
    private BSMLParser _bsmlParser = null!;
    private ListingManager _listingManager = null!;
    private NotificationManager _notificationManager = null!;

    internal event Action? Finished;

    public List<ModInfo>? MissingMods { get; private set; }

    private static string ContentBsml
    {
        get
        {
            if (_contentBsml != null)
            {
                return _contentBsml;
            }

            using StreamReader reader = new(
                typeof(EventModsViewController).Assembly.GetManifestResourceStream(
                    "Synapse.Resources.ModsContent.bsml") ??
                throw new InvalidOperationException("Failed to retrieve ModsContent.bsml."));
            _contentBsml = reader.ReadToEnd();

            return _contentBsml;
        }
    }

#if !V1_29_1
    protected override void OnDestroy()
#else
    public override void OnDestroy()
#endif
    {
        base.OnDestroy();
        _listingManager.ListingFound -= OnListingFound;
    }

#pragma warning disable SA1202
    internal List<ModInfo>? Init()
#pragma warning restore SA1202
    {
        _contents.Clear();

        if (_contentObject != null)
        {
            Destroy(_contentObject);
        }

        if (MissingMods == null)
        {
            return null;
        }

        foreach (ModInfo mod in MissingMods)
        {
            _contents.Add(new ListObject(mod.Id, mod.Version));
        }

        _bsmlParser.Parse(ContentBsml, gameObject, this);

        return MissingMods;
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

        if (addedToHierarchy)
        {
            _header.text =
                $"{_listing?.Title ?? "N/A"} requires the following mods, download them now?\n(You will need to manually restart your game.)";
        }
    }

    [UsedImplicitly]
    [Inject]
    private void Construct(
        SiraLog log,
#if !V1_29_1
        BSMLParser bsmlParser,
#endif
        ListingManager listingManager,
        NotificationManager notificationManager)
    {
        _log = log;
#if !V1_29_1
        _bsmlParser = bsmlParser;
#else
        _bsmlParser = BSMLParser.instance;
#endif
        _listingManager = listingManager;
        listingManager.ListingFound += OnListingFound;
        _notificationManager = notificationManager;
    }

    [UsedImplicitly]
    [UIAction("accept-click")]
    private void OnAcceptClick()
    {
        Finished?.Invoke();
    }

    private void OnListingFound(Listing? listing)
    {
        _listing = listing;

        MissingMods = GetMissingMods(listing);
    }

    private List<ModInfo>? GetMissingMods(Listing? listing)
    {
        RequiredMods? versionMods =
            listing?.RequiredMods.FirstOrDefault(n => n.GameVersion.MatchesGameVersion());

        if (versionMods == null)
        {
            return null;
        }

        List<ModInfo> modsToDownload = [];
        PluginMetadata[] plugins =
            PluginManager.EnabledPlugins.ToArray();

        foreach (ModInfo mod in versionMods.Mods)
        {
            try
            {
                // i'll never understand why hive.versioning even exists
                VersionRange range = new(mod.Version);
                PluginMetadata? match = plugins.FirstOrDefault(n => n.Id == mod.Id && range.Matches(n.HVersion));

                if (match != null)
                {
                    continue;
                }

                modsToDownload.Add(mod);
                _log.Debug($"Missing required mod: {mod}");
            }
            catch (Exception e)
            {
                _log.Error($"Error checking mod version for [{mod}]\n{e}");
                _notificationManager.Notify("Unexpected error!", Color.red);
            }
        }

        return modsToDownload.Count == 0 ? null : modsToDownload;
    }

    private readonly struct ListObject
    {
        internal ListObject(string leftText, string rightText)
        {
            LeftText = leftText;
            RightText = rightText;
        }

        [UsedImplicitly]
        [UIValue("lefttext")]
        private string LeftText { get; }

        [UsedImplicitly]
        [UIValue("righttext")]
        private string RightText { get; }
    }
}
