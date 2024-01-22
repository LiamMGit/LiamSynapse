using HMUI;
using SRT.Views;
using UnityEngine;

namespace SRT.Controllers
{
    public class ScrollViewScroller : MonoBehaviour
    {
        private ScrollView _scrollView = null!;

        internal void Init(ScrollView scrollView)
        {
            _scrollView = scrollView;
        }

        private void Update()
        {
            enabled = false;
            _scrollView.ScrollToEnd(false);
        }
    }
}
