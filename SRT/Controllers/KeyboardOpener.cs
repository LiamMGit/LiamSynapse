using HMUI;
using SiraUtil.Logging;
using SRT.Managers;
using UnityEngine;
using Zenject;

namespace SRT.Controllers
{
    [RequireComponent(typeof(InputFieldView))]
    public class KeyboardOpener : MonoBehaviour
    {
        private UIKeyboardManager _uiKeyboardManager = null!;
        private InputFieldView _inputFieldView = null!;

        [Inject]
        private void Construct(UIKeyboardManager uiKeyboardManager)
        {
            _uiKeyboardManager = uiKeyboardManager;
        }

        private void Awake()
        {
            _inputFieldView = GetComponent<InputFieldView>();
        }

        private void OnGUI()
        {
            Event e = Event.current;

            if (!e.isKey || e.type != EventType.KeyDown)
            {
                return;
            }

            if (e.keyCode == KeyCode.Escape)
            {
                if (_uiKeyboardManager.ShouldCloseKeyboard(gameObject))
                {
                    _uiKeyboardManager.CloseKeyboard();
                }
            }
            else if (e.keyCode == KeyCode.Return && !_inputFieldView._hasKeyboardAssigned)
            {
                _uiKeyboardManager.ProcessMousePress(gameObject);
            }
        }
    }
}
