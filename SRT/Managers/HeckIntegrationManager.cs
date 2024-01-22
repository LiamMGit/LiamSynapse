using System;
using Heck.PlayView;

namespace SRT.Managers
{
    public class HeckIntegrationManager
    {
        private readonly PlayViewManager _playViewManager;

        public HeckIntegrationManager(PlayViewManager playViewManager)
        {
            _playViewManager = playViewManager;
        }

        public void StartPlayViewInterruptedLevel(
            string gameMode,
            IDifficultyBeatmap difficultyBeatmap,
            IPreviewBeatmapLevel previewBeatmapLevel,
            OverrideEnvironmentSettings? overrideEnvironmentSettings,
            ColorScheme overrideColorScheme,
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
            StartStandardLevelParameters parameters = new(
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
            _playViewManager.ForceStart(parameters);
        }
    }
}
