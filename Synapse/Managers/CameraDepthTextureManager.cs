using System;
using JetBrains.Annotations;
using UnityEngine;

namespace Synapse.Managers;

[UsedImplicitly]
internal class CameraDepthTextureManager
{
    private bool _enabled;
    private DepthTextureMode _depthTextureMode;

    internal event Action? Refresh;

    internal DepthTextureMode DepthTextureMode
    {
        get => _depthTextureMode;
        set
        {
            if (_depthTextureMode == value)
            {
                return;
            }

            _depthTextureMode = value;
            Refresh?.Invoke();
        }
    }

    internal bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            Refresh?.Invoke();
        }
    }
}
