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
using Synapse.Managers;
using Synapse.Models;
using TMPro;
using UnityEngine;
using Zenject;

namespace Synapse.Views
{
    ////[HotReload(RelativePathToLayout = @"../Resources/Mods.bsml")]
    [ViewDefinition("Synapse.Resources.Mods.bsml")]
    internal class EventModsViewController : BSMLAutomaticViewController
    {
        private static string? _contentBSML;

        [UsedImplicitly]
        [UIComponent("header")]
        private readonly TMP_Text _header = null!;

        [UsedImplicitly]
        [UIValue("contents")]
        private readonly List<object> _contents = new();

        [UsedImplicitly]
        [UIObject("contentObject")]
        private readonly GameObject? _contentObject;

        private SiraLog _log = null!;
        private Listing? _listing;
        private NotificationManager _notificationManager = null!;

        internal event Action? Finished;

        private static string ContentBSML
        {
            get
            {
                if (_contentBSML != null)
                {
                    return _contentBSML;
                }

                using StreamReader reader = new(typeof(EventModsViewController).Assembly.GetManifestResourceStream("Synapse.Resources.ModsContent.bsml")
                                                ?? throw new InvalidOperationException("Failed to retrieve ModsContent.bsml."));
                _contentBSML = reader.ReadToEnd();

                return _contentBSML;
            }
        }

        internal List<ModInfo>? Init(List<ModInfo> modInfos)
        {
            if (_listing == null)
            {
                return null;
            }

            List<ModInfo> modsToDownload = new();
            _contents.Clear();
            PluginMetadata[] plugins = PluginManager.EnabledPlugins.Concat(PluginManager.IgnoredPlugins.Select(n => n.Key)).ToArray();
            foreach (ModInfo mod in modInfos)
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
                    _contents.Add(new ListObject(mod.Id, mod.Version));
                    _log.Debug($"Missing required mod: {mod.Id}@{mod.Version}");
                }
                catch (Exception e)
                {
                    _log.Error($"Error checking mod version for [{mod.Id}@{mod.Version}]: {e}");
                    _notificationManager.Notify("Unexpected error, please send your log to Aeroluna!", Color.red);
                }
            }

            if (modsToDownload.Count == 0)
            {
                return null;
            }

            if (_contentObject != null)
            {
                Destroy(_contentObject);
            }

            BSMLParser.instance.Parse(ContentBSML, gameObject, this);
            return modsToDownload;
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
        private void Construct(SiraLog log, ListingManager listingManager, NotificationManager notificationManager)
        {
            _log = log;
            listingManager.ListingFound += n => _listing = n;
            _notificationManager = notificationManager;
        }

        [UsedImplicitly]
        [UIAction("accept-click")]
        private void OnAcceptClick()
        {
            Finished?.Invoke();
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
}
