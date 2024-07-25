// cool idea, kinda looks like ass
/*using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using Zenject;

namespace Synapse.Managers;

internal class MenuFadeManager : ITickable
{
    private readonly MenuEnvironmentManager _menuEnvironmentManager;
    private readonly GameObject _wrapper;

    private int _direction = -1;
    private float _transition;

    [UsedImplicitly]
    private MenuFadeManager(MenuEnvironmentManager menuEnvironmentManager)
    {
        _menuEnvironmentManager = menuEnvironmentManager;
        _wrapper = menuEnvironmentManager._data
            .First(n => n.menuEnvironmentType == MenuEnvironmentManager.MenuEnvironmentType.Default)
            .wrapper;
    }

    public void Tick()
    {
        if ((_direction == -1 &&
            _transition < 0) ||
            (_direction == 1 &&
             _transition > 1))
        {
            return;
        }

        _transition += Time.deltaTime * _direction;
        Transform transform = _wrapper.transform;
        Vector3 pos = transform.localPosition;
        pos.y = Mathf.Lerp(0, -100, EaseInQuint(_transition));
        transform.localPosition = pos;

        if (_direction == 1 &&
            _transition > 1)
        {
            _menuEnvironmentManager.ShowEnvironmentType(MenuEnvironmentManager.MenuEnvironmentType.None);
        }
    }

    internal void FadeOut()
    {
        _direction = 1;
        _menuEnvironmentManager.ShowEnvironmentType(MenuEnvironmentManager.MenuEnvironmentType.Default);
    }

    internal void FadeIn()
    {
        _direction = -1;
        _menuEnvironmentManager.ShowEnvironmentType(MenuEnvironmentManager.MenuEnvironmentType.Default);
    }

    /// <summary>
    /// Modeled after the quint y = x^5
    /// </summary>
    private static float EaseInQuint(float p)
    {
        return p * p * p * p * p;
    }
}
*/


