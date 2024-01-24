using HarmonyLib;
using SiraUtil.Affinity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SRT.HarmonyPatches
{
    internal class PauseHijack : IAffinity, IInitializable
    {
        private readonly SaberManager _saberManager;
        private readonly PauseMenuManager _pauseMenuManager;

        private GameObject _warningText;
        private bool _warningShown;

        private PauseHijack(SaberManager saberManager, PauseMenuManager pauseMenuManager)
        {
            _saberManager = saberManager;
            _pauseMenuManager = pauseMenuManager;
        }

        public void Initialize()
        {
            const string warning = "<color=\"red\">WARNING\nQuitting will result in disqualification.\nAre you sure you want to quit?</color>";
            TextMeshProUGUI textMesh = BeatSaberMarkupLanguage.BeatSaberUI.CreateText((RectTransform)_pauseMenuManager.transform.Find("Wrapper/MenuWrapper/Canvas"), warning, Vector2.zero);
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.fontSize = 6;
            textMesh.transform.position = new Vector3(0, 2f, 2f);
            _warningText = textMesh.gameObject;
            _warningText.SetActive(false);
        }

        [AffinityPostfix]
        [AffinityPatch(typeof(PauseMenuManager), nameof(PauseMenuManager.Start))]
        private void DisableRestartButton(Button ____restartButton)
        {
            if (____restartButton)
            {
                ____restartButton.gameObject.SetActive(false);
            }
        }

        [AffinityPrefix]
        [AffinityPatch(typeof(PauseMenuManager), nameof(PauseMenuManager.RestartButtonPressed))]
        private bool DisableRestart()
        {
            return false;
        }

        [AffinityPrefix]
        [AffinityPatch(typeof(GamePause), nameof(GamePause.Pause))]
        private bool DisablePause()
        {
            _saberManager.disableSabers = true;
            return false;
        }

        [AffinityPostfix]
        [AffinityPatch(typeof(PauseMenuManager), nameof(PauseMenuManager.ShowMenu))]
        private void ResetWarning(TextMeshProUGUI ____backButtonText)
        {
            _warningShown = false;
            _warningText.SetActive(false);
        }

        [AffinityPrefix]
        [AffinityPatch(typeof(PauseMenuManager), nameof(PauseMenuManager.MenuButtonPressed))]
        private bool ConfirmQuit()
        {
            if (_warningShown)
            {
                return true;
            }

            _warningShown = true;
            _warningText.SetActive(true);
            return false;
        }
    }
}
