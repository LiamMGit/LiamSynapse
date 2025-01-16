using JetBrains.Annotations;
using Synapse.Managers;
using UnityEngine;
using Zenject;

namespace Synapse.Controllers;

[RequireComponent(typeof(Camera))]
internal class CameraDepthTextureController : MonoBehaviour
{
    private Camera _camera = null!;
    private CameraDepthTextureManager? _depthTextureManager;

    private DepthTextureMode? _cachedDepthTextureMode;

    [Inject]
    [UsedImplicitly]
    private void Construct(CameraDepthTextureManager depthTextureManager)
    {
        _depthTextureManager = depthTextureManager;
        depthTextureManager.Refresh += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_depthTextureManager != null)
        {
            _depthTextureManager.Refresh -= Refresh;
        }
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void OnEnable() => Refresh();

    private void Refresh()
    {
        if (_depthTextureManager == null)
        {
            return;
        }

        if (_depthTextureManager.Enabled)
        {
            _cachedDepthTextureMode ??= _camera.depthTextureMode;
            _camera.depthTextureMode = _depthTextureManager.DepthTextureMode;
        }
        else
        {
            if (!_cachedDepthTextureMode.HasValue)
            {
                return;
            }

            _camera.depthTextureMode = _cachedDepthTextureMode.Value;
            _cachedDepthTextureMode = null;
        }
    }
}
