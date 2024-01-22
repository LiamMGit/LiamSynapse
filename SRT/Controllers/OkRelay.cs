using System;
using UnityEngine;

namespace SRT.Controllers
{
    internal class OkRelay : MonoBehaviour
    {
        internal event Action? OkPressed;

        internal void KeyboardOkPressed() => OkPressed?.Invoke();

        private void Start()
        {
            // Destroy self when other losers try to duplicate this
            if (name != "EventChatInputField")
            {
                Destroy(this);
            }
        }
    }
}
