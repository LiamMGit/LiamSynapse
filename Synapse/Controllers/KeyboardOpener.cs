using HMUI;
using JetBrains.Annotations;
using UnityEngine;
using Zenject;

namespace Synapse.Controllers;

[RequireComponent(typeof(InputFieldView))]
public class KeyboardOpener : MonoBehaviour
{
    private InputFieldView _inputFieldView = null!;
    private UIKeyboardManager _uiKeyboardManager = null!;

    internal void Close()
    {
        if (_uiKeyboardManager.ShouldCloseKeyboard(gameObject))
        {
            _uiKeyboardManager.CloseKeyboard();
        }
    }

    private void Awake()
    {
        _inputFieldView = GetComponent<InputFieldView>();
    }

    [UsedImplicitly]
    [Inject]
    private void Construct(UIKeyboardManager uiKeyboardManager)
    {
        _uiKeyboardManager = uiKeyboardManager;
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
                Close();
                break;

            case KeyCode.Slash when !_inputFieldView._hasKeyboardAssigned:
            case KeyCode.Return when !_inputFieldView._hasKeyboardAssigned:
                _uiKeyboardManager.ProcessMousePress(gameObject);
                break;
        }
    }
}
