using HMUI;
using JetBrains.Annotations;
using Synapse.Extras;
using UnityEngine;
using Zenject;

namespace Synapse.Controllers;

[RequireComponent(typeof(ImageView))]
public class ImageViewRainbowController : MonoBehaviour
{
    private ImageView _imageView = null!;
    private RainbowTicker _rainbowTicker = null!;

    private void Awake()
    {
        _imageView = GetComponent<ImageView>();
    }

    [Inject]
    [UsedImplicitly]
    private void Construct(RainbowTicker rainbowTicker)
    {
        _rainbowTicker = rainbowTicker;
    }

    private void OnDisable()
    {
        _imageView.gradient = false;
    }

    private void Update()
    {
        _imageView._gradientDirection = ImageView.GradientDirection.Vertical;
        _imageView.gradient = true;
        _imageView.color0 = _rainbowTicker.ToColor();
        _imageView.color1 = _rainbowTicker.ToColor(0.2f);
    }
}
