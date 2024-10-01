using System;
using Heck.PlayView;
using JetBrains.Annotations;
#if !PRE_V1_37_1
using Zenject;
#endif

namespace Synapse.Managers;

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
#if !PRE_V1_37_1
        in BeatmapKey beatmapKey,
        BeatmapLevel beatmapLevel,
#else
        IDifficultyBeatmap difficultyBeatmap,
        IPreviewBeatmapLevel previewBeatmapLevel,
#endif
        OverrideEnvironmentSettings? overrideEnvironmentSettings,
        ColorScheme? overrideColorScheme,
#if !V1_29_1
        ColorScheme? beatmapOverrideColorScheme,
#endif
        GameplayModifiers gameplayModifiers,
        PlayerSpecificSettings playerSpecificSettings,
        PracticeSettings? practiceSettings,
#if !PRE_V1_37_1
        EnvironmentsListModel environmentsListModel,
#endif
        string backButtonText,
        bool useTestNoteCutSoundEffects,
        bool startPaused,
        Action? beforeSceneSwitchCallback,
#if !PRE_V1_37_1
        Action<DiContainer>? afterSceneSwitchCallback,
#endif
        Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>? levelFinishedCallback,
#if !V1_29_1
        Action<LevelScenesTransitionSetupDataSO, LevelCompletionResults>? levelRestartedCallback,
        RecordingToolManager.SetupData? recordingToolData)
#else
        Action<LevelScenesTransitionSetupDataSO, LevelCompletionResults>? levelRestartedCallback)
#endif
    {
        StartStandardLevelParameters parameters = new(
            gameMode,
#if !PRE_V1_37_1
            beatmapKey,
            beatmapLevel,
#else
            difficultyBeatmap,
            previewBeatmapLevel,
#endif
            overrideEnvironmentSettings,
            overrideColorScheme,
#if !V1_29_1
            beatmapOverrideColorScheme,
#endif
            gameplayModifiers,
            playerSpecificSettings,
            practiceSettings,
#if !PRE_V1_37_1
            environmentsListModel,
#endif
            backButtonText,
            useTestNoteCutSoundEffects,
            startPaused,
            beforeSceneSwitchCallback,
#if !PRE_V1_37_1
            afterSceneSwitchCallback,
#endif
            levelFinishedCallback,
#if !V1_29_1
            levelRestartedCallback,
            recordingToolData);
#else
            levelRestartedCallback);
#endif
        _playViewManager.ForceStart(parameters);
    }
}
