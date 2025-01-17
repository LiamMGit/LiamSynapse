using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using Zenject;

namespace Synapse.Managers;

[UsedImplicitly]
internal class GlobalDustManager
{
    private readonly HashSet<DustHold> _dustHolds = [];

    private ParticleSystem? _dustParticles;

    internal ParticleSystem? DustParticles => _dustParticles ??=
        Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(n => n.name == "DustPS");

    internal void Refresh()
    {
        if (_dustHolds.Any(n => n.Enabled))
        {
            DustParticles?.Stop();
        }
        else
        {
            DustParticles?.Play();
        }
    }

    internal class DustHold
    {
        private bool _enabled;

        internal event Action? Updated;

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
                Updated?.Invoke();
            }
        }
    }

    internal class DustHoldFactory : IFactory<DustHold>
    {
        private readonly GlobalDustManager _globalDustManager;

        [UsedImplicitly]
        private DustHoldFactory(GlobalDustManager globalDustManager)
        {
            _globalDustManager = globalDustManager;
        }

        public DustHold Create()
        {
            DustHold dustHold = new();
            dustHold.Updated += _globalDustManager.Refresh;
            _globalDustManager._dustHolds.Add(dustHold);
            return dustHold;
        }
    }
}
