using System;
using System.Threading;
using System.Threading.Tasks;
using IPA.Utilities.Async;
using JetBrains.Annotations;
using SiraUtil.Logging;
using Synapse.Extras;
using UnityEngine;

namespace Synapse.Managers;

internal class InfoSpriteManager : IDisposable
{
    private readonly SiraLog _log;
    private readonly NetworkManager _networkManager;
    private Sprite? _introImage;
    private string? _introUrl;
    private Sprite? _finishImage;
    private string? _finishUrl;
    private CancellationTokenSource _cancellationToken = new();

    [UsedImplicitly]
    private InfoSpriteManager(SiraLog log, NetworkManager networkManager)
    {
        _log = log;
        _networkManager = networkManager;
        networkManager.IntroUrlUpdated += OnIntroUrlUpdated;
        networkManager.FinishUrlUpdated += OnFinishUrlUpdated;
    }

    public event Action<Sprite?>? IntroImageCreated
    {
        add
        {
            if (_introImage != null)
            {
                value?.Invoke(_introImage);
            }

            IntroImageCreatedBacking += value;
        }

        remove => IntroImageCreatedBacking -= value;
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

    private event Action<Sprite?>? IntroImageCreatedBacking;

    private event Action<Sprite?>? FinishImageCreatedBacking;

    public void Dispose()
    {
        _networkManager.IntroUrlUpdated -= OnIntroUrlUpdated;
        _networkManager.FinishUrlUpdated -= OnFinishUrlUpdated;
    }

    private void OnIntroUrlUpdated(string url)
    {
        if (_introUrl == url)
        {
            return;
        }

        _introUrl = url;
        _cancellationToken.Cancel();
        _ = UnityMainThreadTaskScheduler.Factory.StartNew(() => GetIntroImage(url, (_cancellationToken = new CancellationTokenSource()).Token));
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

    private async Task GetIntroImage(string url, CancellationToken token)
    {
        try
        {
            _log.Debug($"Fetching intro image from [{url}]");
            Sprite introImage = await MediaExtensions.RequestSprite(url, token);
            _introImage = introImage;
            IntroImageCreatedBacking?.Invoke(introImage);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            _log.Error($"Exception while fetching intro image\n{e}");
            IntroImageCreatedBacking?.Invoke(null);
        }
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
