using HMUI;
using UnityEngine;

namespace Synapse.Controllers
{
    [RequireComponent(typeof(ImageView))]
    public class ImageViewRainbowController : MonoBehaviour
    {
        private ImageView _imageView = null!;

        private float _hue;

        private void Awake()
        {
            _imageView = GetComponent<ImageView>();
        }

        private void OnDisable()
        {
            _imageView.gradient = false;
        }

        private void Update()
        {
            _hue = Mathf.Repeat(_hue + (0.5f * Time.deltaTime), 1);
            _imageView._gradientDirection = ImageView.GradientDirection.Vertical;
            _imageView.gradient = true;
            _imageView.color0 = Color.HSVToRGB(_hue, 0.6f, 1);
            _imageView.color1 = Color.HSVToRGB(Mathf.Repeat(_hue + 0.2f, 1), 0.6f, 1);
        }
    }
}
