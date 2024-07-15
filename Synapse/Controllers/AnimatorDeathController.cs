using System;
using UnityEngine;

namespace Synapse.Controllers;

[RequireComponent(typeof(Animator))]
internal class AnimatorDeathController : MonoBehaviour
{
    private Action? _action;
    private Animator _animator = null!;

    private float _timeout;

    internal void ContinueAfterDecay(float timeout, Action action)
    {
        _action = action;
        _timeout = timeout;
        enabled = true;
    }

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Update()
    {
        _timeout -= Time.deltaTime;
        float animatorTime = _animator.GetCurrentAnimatorStateInfo(0).normalizedTime;

        // ReSharper disable once InvertIf
        if (_timeout <= 0 || animatorTime > 1)
        {
            _action?.Invoke();
            enabled = false;
        }
    }
}
