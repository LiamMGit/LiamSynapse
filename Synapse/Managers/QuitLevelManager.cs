using System;
using IPA.Utilities.Async;
using JetBrains.Annotations;

namespace Synapse.Managers;

internal class QuitLevelManager : IDisposable
{
    private readonly NetworkManager _networkManager;
    private readonly PrepareLevelCompletionResults _prepareLevelCompletionResults;
    private readonly StandardLevelScenesTransitionSetupDataSO _standardLevelScenesTransitionSetupDataSo;

    [UsedImplicitly]
    internal QuitLevelManager(
        NetworkManager networkManager,
        PrepareLevelCompletionResults prepareLevelCompletionResults,
        StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupDataSo)
    {
        _networkManager = networkManager;
        _prepareLevelCompletionResults = prepareLevelCompletionResults;
        _standardLevelScenesTransitionSetupDataSo = standardLevelScenesTransitionSetupDataSo;
        networkManager.StopLevelReceived += OnStopLevelReceived;
        networkManager.Disconnected += OnDisconnected;
    }

    public void Dispose()
    {
        _networkManager.StopLevelReceived -= OnStopLevelReceived;
    }

    private void OnStopLevelReceived() => StopLevel();

    private void OnDisconnected(string _) => StopLevel();

    private void StopLevel()
    {
        LevelCompletionResults levelCompletionResults = _prepareLevelCompletionResults.FillLevelCompletionResults(
            LevelCompletionResults.LevelEndStateType.Incomplete,
            LevelCompletionResults.LevelEndAction.None);
        UnityMainThreadTaskScheduler.Factory.StartNew(
            () => { _standardLevelScenesTransitionSetupDataSo.Finish(levelCompletionResults); });
    }
}
