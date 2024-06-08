using System;
using Heck.PlayView;
using JetBrains.Annotations;

namespace Synapse.Managers
{
    // TODO: fully remove heck and songcore references
    internal class HeckIntegrationManager
    {
        private readonly PlayViewManager _playViewManager;

        [UsedImplicitly]
        private HeckIntegrationManager(PlayViewManager playViewManager)
        {
            _playViewManager = playViewManager;
        }

        internal void StartPlayViewInterruptedLevel(
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
            StartStandardLevelParameters parameters = new(
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
            _playViewManager.ForceStart(parameters);
        }
    }
}
