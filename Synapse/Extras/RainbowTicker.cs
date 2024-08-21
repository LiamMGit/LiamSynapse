using JetBrains.Annotations;
using UnityEngine;
using Zenject;

namespace Synapse.Extras;

[UsedImplicitly]
internal class RainbowTicker : ITickable
{
    private const float SATURATION = 0.8f;
    private const float SPEED = 0.5f;
    private const float VALUE = 1;

    private float _hue;

    public void Tick()
    {
        _hue = Mathf.Repeat(_hue + (SPEED * Time.deltaTime), 1);
    }

    internal Color ToColor()
    {
        return Color.HSVToRGB(_hue, SATURATION, VALUE);
    }

    internal Color ToColor(float offset)
    {
        return Color.HSVToRGB(Mathf.Repeat(_hue + offset, 1), SATURATION, VALUE);
    }
}
