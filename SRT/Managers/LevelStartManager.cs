using System;
using SRT.HarmonyPatches;
using SRT.Models;
using Zenject;

namespace SRT.Managers
{
    internal class LevelStartManager
    {
        private readonly GameplaySetupViewController _gameplaySetupViewController;
        private readonly MenuTransitionsHelper _menuTransitionsHelper;
        private readonly NoEnergyModifier _noEnergyModifier;
        private readonly HeckIntegrationManager? _heckIntegrationManager;

        private Ruleset? _ruleset;

        private LevelStartManager(
            GameplaySetupViewController gameplaySetupViewController,
            MenuTransitionsHelper menuTransitionsHelper,
            NetworkManager networkManager,
            NoEnergyModifier noEnergyModifier,
            [InjectOptional] HeckIntegrationManager? heckIntegrationManager)
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
            IDifficultyBeatmap difficultyBeatmap,
            IPreviewBeatmapLevel previewBeatmapLevel,
            Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>? levelFinishedCallback)
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

            StartStandardOrHeck(
                "screw yo analytics",
                difficultyBeatmap,
                previewBeatmapLevel,
                null, // no environment override
                overrideColorScheme,
                modifiers,
                playerSpecificSettings,
                new PracticeSettings(), // TODO: no practice
                string.Empty, // doesnt matter, gets reset by animation anyways
                false,
                false,
                null,
                levelFinishedCallback,
                null);
        }

        private void OnMapUpdated(Map map)
        {
            _ruleset = map.Ruleset;
        }

        // i wish i could use my StartStandardLevelParameters here
        private void StartStandardOrHeck(
            string gameMode,
            IDifficultyBeatmap difficultyBeatmap,
            IPreviewBeatmapLevel previewBeatmapLevel,
            OverrideEnvironmentSettings? overrideEnvironmentSettings,
            ColorScheme? overrideColorScheme,
            GameplayModifiers gameplayModifiers,
            PlayerSpecificSettings playerSpecificSettings,
            PracticeSettings? practiceSettings,
            string backButtonText,
            bool useTestNoteCutSoundEffects,
            bool startPaused,
            Action? beforeSceneSwitchCallback,
            Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>? levelFinishedCallback,
            Action<LevelScenesTransitionSetupDataSO, LevelCompletionResults>? levelRestartedCallback)
        {
            if (_heckIntegrationManager != null)
            {
                _heckIntegrationManager.StartPlayViewInterruptedLevel(
                    gameMode,
                    difficultyBeatmap,
                    previewBeatmapLevel,
                    overrideEnvironmentSettings,
                    overrideColorScheme,
                    gameplayModifiers,
                    playerSpecificSettings,
                    practiceSettings,
                    backButtonText,
                    useTestNoteCutSoundEffects,
                    startPaused,
                    beforeSceneSwitchCallback,
                    levelFinishedCallback,
                    levelRestartedCallback);
            }
            else
            {
                _menuTransitionsHelper.StartStandardLevel(
                    gameMode,
                    difficultyBeatmap,
                    previewBeatmapLevel,
                    overrideEnvironmentSettings,
                    overrideColorScheme,
                    gameplayModifiers,
                    playerSpecificSettings,
                    practiceSettings,
                    backButtonText,
                    useTestNoteCutSoundEffects,
                    startPaused,
                    beforeSceneSwitchCallback,
                    levelFinishedCallback,
                    levelRestartedCallback);
            }
        }
    }
}
