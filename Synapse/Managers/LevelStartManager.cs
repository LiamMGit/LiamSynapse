using System;
using JetBrains.Annotations;
using Synapse.HarmonyPatches;
using Synapse.Models;
using Zenject;

namespace Synapse.Managers
{
    internal class LevelStartManager
    {
        private readonly GameplaySetupViewController _gameplaySetupViewController;
        private readonly MenuTransitionsHelper _menuTransitionsHelper;
        private readonly NoEnergyModifier _noEnergyModifier;
        private readonly LazyInject<HeckIntegrationManager>? _heckIntegrationManager;

        private Ruleset? _ruleset;

        [UsedImplicitly]
        private LevelStartManager(
            GameplaySetupViewController gameplaySetupViewController,
            MenuTransitionsHelper menuTransitionsHelper,
            NetworkManager networkManager,
            NoEnergyModifier noEnergyModifier,
            [InjectOptional] LazyInject<HeckIntegrationManager>? heckIntegrationManager)
        {
            _gameplaySetupViewController = gameplaySetupViewController;
            _menuTransitionsHelper = menuTransitionsHelper;
            _noEnergyModifier = noEnergyModifier;
            _heckIntegrationManager = heckIntegrationManager;
            networkManager.MapUpdated += OnMapUpdated;
        }

        private enum GameplayModifier
        {
            noFailOn0Energy,
            instaFail,
            failOnSaberClash,
            noBombs,
            fastNotes,
            strictAngles,
            disappearingArrows,
            noArrows,
            ghostNotes,
            proMode,
            zenMode,
            smallCubes,
            noEnergy // custom modifier
        }

        // WARNING: ruleset has lower priority than heck map settings
        public void StartLevel(
            DownloadedMap downloadedMap,
            Action<DownloadedMap, StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>? levelFinishedCallback)
        {
            ColorScheme? overrideColorScheme;
            if (_ruleset?.AllowOverrideColors != null && !_ruleset.AllowOverrideColors.Value)
            {
                overrideColorScheme = null;
            }
            else
            {
                overrideColorScheme = _gameplaySetupViewController.colorSchemesSettings.GetOverrideColorScheme();
            }

            PlayerSpecificSettings playerSpecificSettings;
            if (_ruleset?.AllowLeftHand != null && !_ruleset.AllowLeftHand.Value)
            {
                playerSpecificSettings = _gameplaySetupViewController.playerSettings.CopyWith();
                playerSpecificSettings._leftHanded = false;
            }
            else
            {
                playerSpecificSettings = _gameplaySetupViewController.playerSettings;
            }

            GameplayModifiers modifiers = new();
            if (_ruleset is { Modifiers: not null })
            {
                foreach (string rulesetModifier in _ruleset.Modifiers)
                {
                    if (!Enum.TryParse(rulesetModifier, true, out GameplayModifier modifier))
                    {
                        continue;
                    }

                    switch (modifier)
                    {
                        case GameplayModifier.noFailOn0Energy:
                            modifiers._noFailOn0Energy = true;
                            break;

                        case GameplayModifier.instaFail:
                            modifiers._instaFail = true;
                            break;

                        case GameplayModifier.failOnSaberClash:
                            modifiers._failOnSaberClash = true;
                            break;

                        case GameplayModifier.noBombs:
                            modifiers._noBombs = true;
                            break;

                        case GameplayModifier.fastNotes:
                            modifiers._fastNotes = true;
                            break;

                        case GameplayModifier.strictAngles:
                            modifiers._strictAngles = true;
                            break;

                        case GameplayModifier.disappearingArrows:
                            modifiers._disappearingArrows = true;
                            break;

                        case GameplayModifier.noArrows:
                            modifiers._noArrows = true;
                            break;

                        case GameplayModifier.ghostNotes:
                            modifiers._ghostNotes = true;
                            break;

                        case GameplayModifier.proMode:
                            modifiers._proMode = true;
                            break;

                        case GameplayModifier.zenMode:
                            modifiers._zenMode = true;
                            break;

                        case GameplayModifier.smallCubes:
                            modifiers._smallCubes = true;
                            break;

                        case GameplayModifier.noEnergy:
                            _noEnergyModifier.NoEnergyNextMap = true;
                            break;
                    }
                }
            }

            Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>? callback = null;
            if (levelFinishedCallback != null)
            {
                callback = (a, b) => levelFinishedCallback(downloadedMap, a, b);
            }

#if LATEST
            ColorScheme? beatmapOverrideColorScheme = null;
            if (downloadedMap is { PreviewBeatmapLevel: CustomBeatmapLevel customBeatmapLevel, DifficultyBeatmap: CustomDifficultyBeatmap customDifficultyBeatmap })
            {
                beatmapOverrideColorScheme = customBeatmapLevel.GetBeatmapLevelColorScheme(customDifficultyBeatmap.beatmapColorSchemeIdx);
            }
#endif

            StartStandardOrHeck(
                "screw yo analytics",
                downloadedMap.DifficultyBeatmap,
                downloadedMap.PreviewBeatmapLevel,
                null, // no environment override
                overrideColorScheme,
#if LATEST
                beatmapOverrideColorScheme,
#endif
                modifiers,
                playerSpecificSettings,
                null,
                string.Empty, // doesnt matter, gets reset by animation anyways
                false,
                false,
                null,
                callback,
#if LATEST
                null,
#endif
                null);
        }

        private void OnMapUpdated(int _, Map? map)
        {
            _ruleset = map?.Ruleset;
        }

        // i wish i could use my StartStandardLevelParameters here
        private void StartStandardOrHeck(
            string gameMode,
            IDifficultyBeatmap difficultyBeatmap,
            IPreviewBeatmapLevel previewBeatmapLevel,
            OverrideEnvironmentSettings? overrideEnvironmentSettings,
            ColorScheme? overrideColorScheme,
#if LATEST
            ColorScheme? beatmapOverrideColorScheme,
#endif
            GameplayModifiers gameplayModifiers,
            PlayerSpecificSettings playerSpecificSettings,
            PracticeSettings? practiceSettings,
            string backButtonText,
            bool useTestNoteCutSoundEffects,
            bool startPaused,
            Action? beforeSceneSwitchCallback,
            Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>? levelFinishedCallback,
#if LATEST
            Action<LevelScenesTransitionSetupDataSO, LevelCompletionResults>? levelRestartedCallback,
            RecordingToolManager.SetupData? recordingToolData)
#else
            Action<LevelScenesTransitionSetupDataSO, LevelCompletionResults>? levelRestartedCallback)
#endif
        {
            if (_heckIntegrationManager != null)
            {
                _heckIntegrationManager.Value.StartPlayViewInterruptedLevel(
                    gameMode,
                    difficultyBeatmap,
                    previewBeatmapLevel,
                    overrideEnvironmentSettings,
                    overrideColorScheme,
#if LATEST
                    beatmapOverrideColorScheme,
#endif
                    gameplayModifiers,
                    playerSpecificSettings,
                    practiceSettings,
                    backButtonText,
                    useTestNoteCutSoundEffects,
                    startPaused,
                    beforeSceneSwitchCallback,
                    levelFinishedCallback,
#if LATEST
                    levelRestartedCallback,
                    recordingToolData);
#else
                    levelRestartedCallback);
#endif
            }
            else
            {
                _menuTransitionsHelper.StartStandardLevel(
                    gameMode,
                    difficultyBeatmap,
                    previewBeatmapLevel,
                    overrideEnvironmentSettings,
                    overrideColorScheme,
#if LATEST
                    beatmapOverrideColorScheme,
#endif
                    gameplayModifiers,
                    playerSpecificSettings,
                    practiceSettings,
                    backButtonText,
                    useTestNoteCutSoundEffects,
                    startPaused,
                    beforeSceneSwitchCallback,
                    levelFinishedCallback,
#if LATEST
                    levelRestartedCallback,
                    recordingToolData);
#else
                    levelRestartedCallback);
#endif
            }
        }
    }
}
