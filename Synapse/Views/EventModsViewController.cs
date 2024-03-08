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

        private readonly List<RequiredMod> _requiredMods = new(0);

        private SiraLog _log = null!;
        private Listing _listing = null!;

        internal event Action<List<RequiredMod>>? didAcceptEvent;

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

        internal bool Init()
        {
            _requiredMods.Clear();
            _contents.Clear();
            bool result = false;
            PluginMetadata[] plugins = PluginManager.EnabledPlugins.Concat(PluginManager.IgnoredPlugins.Select(n => n.Key)).ToArray();
            foreach (RequiredMod mod in _listing.RequiredMods)
            {
                // i'll never understand why hive.versioning even exists
                VersionRange range = new(mod.Version);
                PluginMetadata? match = plugins.FirstOrDefault(n => n.Id == mod.Id && range.Matches(n.HVersion));

                if (match != null)
                {
                    continue;
                }

                _requiredMods.Add(mod);
                _contents.Add(new ListObject(mod.Id, mod.Version));
                _log.Debug($"Missing required mod: {mod.Id}@{mod.Version}");
                result = true;
            }

            if (!result)
            {
                return false;
            }

            if (_contentObject != null)
            {
                Destroy(_contentObject);
            }

            BSMLParser.instance.Parse(ContentBSML, gameObject, this);
            return true;
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            if (addedToHierarchy)
            {
                _header.text =
                    $"{_listing.Title} requires the following mods, download them now?\n(You will need to manually restart your game.)";
            }
        }

        [UsedImplicitly]
        [Inject]
        private void Construct(SiraLog log, ListingManager listingManager)
        {
            _log = log;
            listingManager.ListingFound += n => _listing = n;
        }

        [UsedImplicitly]
        [UIAction("accept-click")]
        private void OnAcceptClick()
        {
            didAcceptEvent?.Invoke(_requiredMods);
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
