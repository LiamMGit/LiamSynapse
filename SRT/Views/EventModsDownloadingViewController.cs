using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using Hive.Versioning;
using IPA.Loader;
using JetBrains.Annotations;
using SiraUtil.Logging;
using SRT.Managers;
using SRT.Models;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Zenject;

namespace SRT.Views
{
    // TODO: make this less ugly

    // ReSharper disable FieldCanBeMadeReadOnly.Local
    ////[HotReload(RelativePathToLayout = @"../Resources/ModsDownloading.bsml")]
    [ViewDefinition("SRT.Resources.ModsDownloading.bsml")]
    public class EventModsDownloadingViewController : EventDownloadingViewController
    {
        private SiraLog _log = null!;

        private List<RequiredMod> _requiredMods = null!;

#pragma warning disable SA1124
        #region i hate bsml
#pragma warning restore SA1124
        [UIComponent("percentage")]
        private readonly TMP_Text _percentageTMP = null!;

        [UIComponent("downloadingtext")]
        private readonly TMP_Text _downloadingTMP = null!;

        [UIComponent("errortext")]
        private readonly TMP_Text _errorTMP = null!;

        [UIComponent("quittext")]
        private readonly TMP_Text _quitTMP = null!;

        [UIComponent("loadingbar")]
        private readonly VerticalLayoutGroup _barGroup = null!;

        [UIObject("quit")]
        private readonly GameObject _quitGroup = null!;

        [UIObject("downloading")]
        private readonly GameObject _downloadingGroup = null!;

        [UIObject("error")]
        private readonly GameObject _error = null!;

        // ReSharper disable ConvertToAutoProperty
        protected override TMP_Text PercentageTMP => _percentageTMP;

        protected override TMP_Text DownloadingTMP => _downloadingTMP;

        protected override TMP_Text ErrorTMP => _errorTMP;

        protected override TMP_Text QuitTMP => _quitTMP;

        protected override VerticalLayoutGroup BarGroup => _barGroup;

        protected override GameObject QuitGroup => _quitGroup;

        protected override GameObject DownloadingGroup => _downloadingGroup;

        protected override GameObject Error => _error;
        #endregion

        internal bool DownloadFinished { get; private set; }

        [UsedImplicitly]
        [Inject]
        private void Construct(SiraLog log)
        {
            _log = log;
        }

        public void Init(List<RequiredMod> requiredMods)
        {
            DownloadFinished = false;
            _requiredMods = requiredMods;
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            if (!addedToHierarchy)
            {
                return;
            }

            if (!DownloadFinished)
            {
                _ = DownloadAndSave();
            }
            else
            {
                NewView = View.Quit;
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
            if (removedFromHierarchy)
            {
                Cancel();
            }
        }

        private async Task DownloadAndSave()
        {
            CancellationToken token = ResetToken();
            int count = _requiredMods.Count;
            string unzipPath = Directory.CreateDirectory(
                (Path.GetDirectoryName(Application.dataPath) ?? throw new InvalidOperationException())
                + $"{Path.DirectorySeparatorChar}IPA{Path.DirectorySeparatorChar}Pending").FullName;
            for (int i = 0; i < count; i++)
            {
                RequiredMod requiredMod = _requiredMods[i];
                string url = requiredMod.Url;
                DownloadText = $"Downloading {requiredMod.Id}... ({i + 1}/{count})";
                _log.Debug($"Attempting to download [{requiredMod.Id}] from [{url}]");
                int iteration = i;
                if (!await Download(
                        url,
                        unzipPath,
                        n => DownloadProgress = (iteration + (n * 0.5f)) / count,
                        () => DownloadText = $"Unzipping {requiredMod.Id}... ({iteration + 1}/{count})",
                        n => DownloadProgress = (iteration + 0.5f + (n * 0.5f)) / count,
                        token))
                {
                    return;
                }

                _log.Debug($"Successfully downloaded [{requiredMod.Id}]");
            }

            QuitText = $"{count} mod(s) successfully downloaded.\nQuit and restart to complete installation.";
            NewView = View.Quit;
            DownloadFinished = true;
        }

        [UsedImplicitly]
        [UIAction("accept-click")]
        private void OnAcceptClick()
        {
            Application.Quit();
        }
    }
}
