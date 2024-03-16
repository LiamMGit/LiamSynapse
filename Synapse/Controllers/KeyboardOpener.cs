using HMUI;
using JetBrains.Annotations;
using UnityEngine;
using Zenject;

namespace Synapse.Controllers
{
    [RequireComponent(typeof(InputFieldView))]
    public class KeyboardOpener : MonoBehaviour
    {
        private UIKeyboardManager _uiKeyboardManager = null!;
        private InputFieldView _inputFieldView = null!;

        [UsedImplicitly]
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

            switch (e.keyCode)
            {
                case KeyCode.Escape:
                    if (_uiKeyboardManager.ShouldCloseKeyboard(gameObject))
                    {
                        _uiKeyboardManager.CloseKeyboard();
                    }

                    break;

                case KeyCode.Slash when !_inputFieldView._hasKeyboardAssigned:
                case KeyCode.Return when !_inputFieldView._hasKeyboardAssigned:
                    _uiKeyboardManager.ProcessMousePress(gameObject);
                    break;
            }
        }
    }
}
