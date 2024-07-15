using System;
using System.Reflection;
using HarmonyLib;
using IPA.Loader;
using JetBrains.Annotations;
using SiraUtil.Logging;
using Synapse.HarmonyPatches;
using Synapse.Models;
using Zenject;

namespace Synapse.Managers;

internal class LevelStartManager
{
    private static readonly Action<string>? _nextLevelIsIsolated = GetNextLevelIsIsolated();

    private readonly Action? _disableCustomPlatform;

    private readonly GameplaySetupViewController _gameplaySetupViewController;
    private readonly LazyInject<HeckIntegrationManager>? _heckIntegrationManager;
    private readonly MenuTransitionsHelper _menuTransitionsHelper;
    private readonly NoEnergyModifier _noEnergyModifier;

    private Ruleset? _ruleset;

    [UsedImplicitly]
    private LevelStartManager(
        SiraLog log,
        DiContainer container,
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

        // to disable custom platforms
        PluginMetadata? customPlatforms = PluginManager.GetPlugin("Custom Platforms");

        // ReSharper disable once InvertIf
        if (customPlatforms != null)
        {
            // cannot use ConnectionManager as it is not bound
            Type? platformManagerType = customPlatforms.Assembly.GetType("CustomFloorPlugin.PlatformManager");
            if (platformManagerType != null)
            {
                object? platformsConnectionManager = container.TryResolve(platformManagerType);
                if (platformsConnectionManager != null)
                {
                    MethodInfo? setPlatform = platformManagerType
                        .GetProperty(
                            "APIRequestedPlatform",
                            AccessTools.all)
                        ?.GetSetMethod(true);
                    MethodInfo? getDefault = platformManagerType
                        .GetProperty(
                            "DefaultPlatform",
                            AccessTools.all)
                        ?.GetGetMethod();
                    if (setPlatform == null)
                    {
                        log.Error("Could not find [CustomFloorPlugin.PlatformManager.APIRequestedPlatform] setter");
                    }
                    else if (getDefault == null)
                    {
                        log.Error("Could not find [CustomFloorPlugin.PlatformManager.DefaultPlatform] getter");
                    }
                    else
                    {
                        object defaultPlatform = getDefault.Invoke(platformsConnectionManager, null);
                        _disableCustomPlatform = () => setPlatform.Invoke(
                            platformsConnectionManager,
                            [defaultPlatform]);
                    }
                }
                else
                {
                    log.Error("Could not resolve [CustomFloorPlugin.PlatformManager] instance");
                }
            }
            else
            {
                log.Error("Could not find [CustomFloorPlugin.ConnectionManager] type");
            }
        }
    }

#pragma warning disable SA1300
    // ReSharper disable InconsistentNaming
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
#pragma warning restore SA1300

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
        BeatmapKey beatmapKey = downloadedMap.BeatmapKey;
        BeatmapLevel beatmapLevel = downloadedMap.BeatmapLevel;
        ColorScheme? beatmapOverrideColorScheme =
            beatmapLevel.GetColorScheme(beatmapKey.beatmapCharacteristic, beatmapKey.difficulty);
#elif !V1_29_1
        ColorScheme? beatmapOverrideColorScheme = null;
        if (downloadedMap is
            {
                BeatmapLevel: CustomBeatmapLevel customBeatmapLevel,
                DifficultyBeatmap: CustomDifficultyBeatmap customDifficultyBeatmap
            })
        {
            beatmapOverrideColorScheme =
                customBeatmapLevel.GetBeatmapLevelColorScheme(customDifficultyBeatmap.beatmapColorSchemeIdx);
        }
#endif

        _disableCustomPlatform?.Invoke();
        _nextLevelIsIsolated?.Invoke("Synapse");
        IntroSkipInstallationPatch.SkipNext = true;

        StartStandardOrHeck(
            "screw yo analytics",
#if LATEST
            beatmapKey,
            beatmapLevel,
#else
            downloadedMap.DifficultyBeatmap,
            downloadedMap.BeatmapLevel,
#endif
            null, // no environment override
            overrideColorScheme,
#if !V1_29_1
            beatmapOverrideColorScheme,
#endif
            modifiers,
            playerSpecificSettings,
            null,
#if LATEST
            null,
#endif
            string.Empty, // doesnt matter, gets reset by animation anyways
            false,
            false,
            null,
#if LATEST
            null,
#endif
            callback,
#if !V1_29_1
            null,
#endif
            null);
    }

    private static Action<string>? GetNextLevelIsIsolated()
    {
        // to disable introskip
        PluginMetadata? bsUtils = PluginManager.GetPlugin("BS_Utils");
        if (bsUtils == null)
        {
            return null;
        }

        Type? type = bsUtils.Assembly.GetType("BS_Utils.Gameplay.Gamemode");
        MethodInfo? method = type?.GetMethod("NextLevelIsIsolated", BindingFlags.Static | BindingFlags.Public);
        if (type == null)
        {
            Plugin.Log.Error("Could not find [BS_Utils.Gameplay.Gamemode] type");
        }
        else if (method == null)
        {
            Plugin.Log.Error("Could not find [BS_Utils.Gameplay.Gamemode.NextLevelIsIsolated] method");
        }
        else
        {
            return (Action<string>?)method.CreateDelegate(typeof(Action<string>), null);
        }

        return null;
    }

    private void OnMapUpdated(int _, Map? map)
    {
        _ruleset = map?.Ruleset;
    }

    // i wish i could use my StartStandardLevelParameters here
    private void StartStandardOrHeck(
        string gameMode,
#if LATEST
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
#if LATEST
        EnvironmentsListModel? environmentsListModel,
#endif
        string backButtonText,
        bool useTestNoteCutSoundEffects,
        bool startPaused,
        Action? beforeSceneSwitchCallback,
#if LATEST
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
        if (_heckIntegrationManager != null)
        {
            _heckIntegrationManager.Value.StartPlayViewInterruptedLevel(
                gameMode,
#if LATEST
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
#if LATEST
                environmentsListModel,
#endif
                backButtonText,
                useTestNoteCutSoundEffects,
                startPaused,
                beforeSceneSwitchCallback,
#if LATEST
                afterSceneSwitchCallback,
#endif
                levelFinishedCallback,
#if !V1_29_1
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
#if LATEST
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
#if LATEST
                environmentsListModel,
#endif
                backButtonText,
                useTestNoteCutSoundEffects,
                startPaused,
                beforeSceneSwitchCallback,
#if LATEST
                afterSceneSwitchCallback,
#endif
                levelFinishedCallback,
#if !V1_29_1
                levelRestartedCallback,
                recordingToolData);
#else
                levelRestartedCallback);
#endif
        }
    }
}
