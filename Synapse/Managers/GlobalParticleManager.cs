using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using Zenject;

namespace Synapse.Managers;

[UsedImplicitly]
internal class GlobalParticleManager
{
    private readonly HashSet<ParticleHold> _particleHolds = [];

    private ParticleSystem? _dust;
    private ParticleSystem? _smoke;

    internal ParticleSystem? Dust => _dust ??=
        Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(n => n.name == "DustPS");

    internal ParticleSystem? Smoke => _smoke ??=
        Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(n => n.name == "BigSmokePS");

    internal void Refresh()
    {
        RefreshDust();
        RefreshSmoke();
    }

    internal void RefreshDust()
    {
        if (_particleHolds.Any(n => n.DustDisabled))
        {
            Dust?.Stop();
        }
        else
        {
            Dust?.Play();
        }
    }

    internal void RefreshSmoke()
    {
        if (_particleHolds.Any(n => n.SmokeDisabled))
        {
            Smoke?.Stop();
            Smoke?.Clear();
        }
        else
        {
            Smoke?.Play();
        }
    }

    internal class ParticleHold
    {
        private bool _dustDisabled;
        private bool _smokeDisabled;

        internal event Action? DustUpdated;

        internal event Action? SmokeUpdated;

        internal bool DustDisabled
        {
            get => _dustDisabled;
            set
            {
                if (_dustDisabled == value)
                {
                    return;
                }

                _dustDisabled = value;
                DustUpdated?.Invoke();
            }
        }

        internal bool SmokeDisabled
        {
            get => _smokeDisabled;
            set
            {
                if (_smokeDisabled == value)
                {
                    return;
                }

                _smokeDisabled = value;
                SmokeUpdated?.Invoke();
            }
        }
    }

    internal class DustHoldFactory : IFactory<ParticleHold>
    {
        private readonly GlobalParticleManager _globalParticleManager;

        [UsedImplicitly]
        private DustHoldFactory(GlobalParticleManager globalParticleManager)
        {
            _globalParticleManager = globalParticleManager;
        }

        public ParticleHold Create()
        {
            ParticleHold particleHold = new();
            particleHold.DustUpdated += _globalParticleManager.RefreshDust;
            particleHold.SmokeUpdated += _globalParticleManager.RefreshSmoke;
            _globalParticleManager._particleHolds.Add(particleHold);
            return particleHold;
        }
    }
}
