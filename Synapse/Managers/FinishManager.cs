using System;
using System.Threading;
using System.Threading.Tasks;
using IPA.Utilities.Async;
using JetBrains.Annotations;
using SiraUtil.Logging;
using Synapse.Extras;
using UnityEngine;

namespace Synapse.Managers;

internal class FinishManager : IDisposable
{
    private readonly SiraLog _log;
    private readonly NetworkManager _networkManager;
    private Sprite? _finishImage;
    private string? _finishUrl;
    private CancellationTokenSource _cancellationToken = new();

    [UsedImplicitly]
    private FinishManager(SiraLog log, NetworkManager networkManager)
    {
        _log = log;
        _networkManager = networkManager;
        networkManager.FinishUrlUpdated += OnFinishUrlUpdated;
    }

    public event Action<Sprite?>? FinishImageCreated
    {
        add
        {
            if (_finishImage != null)
            {
                value?.Invoke(_finishImage);
            }

            FinishImageCreatedBacking += value;
        }

        remove => FinishImageCreatedBacking -= value;
    }

    private event Action<Sprite?>? FinishImageCreatedBacking;

    public void Dispose()
    {
        _networkManager.FinishUrlUpdated -= OnFinishUrlUpdated;
    }

    private void OnFinishUrlUpdated(string url)
    {
        if (_finishUrl == url)
        {
            return;
        }

        _finishUrl = url;
        _cancellationToken.Cancel();
        _ = UnityMainThreadTaskScheduler.Factory.StartNew(() => GetFinishImage(url, (_cancellationToken = new CancellationTokenSource()).Token));
    }

    private async Task GetFinishImage(string url, CancellationToken token)
    {
        try
        {
            _log.Debug($"Fetching finish image from [{url}]");
            Sprite finishImage = await MediaExtensions.RequestSprite(url, token);
            _finishImage = finishImage;
            FinishImageCreatedBacking?.Invoke(finishImage);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            _log.Error($"Exception while fetching finish image\n{e}");
            FinishImageCreatedBacking?.Invoke(null);
        }
    }
}
