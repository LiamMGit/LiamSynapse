using HarmonyLib;
using HMUI;
using Synapse.Controllers;

namespace Synapse.HarmonyPatches;

[HarmonyPatch(typeof(InputFieldView))]
internal static class AssignOkRelay
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(InputFieldView.ActivateKeyboard))]
    private static void ActivateKeyboard(InputFieldView __instance, HMUI.UIKeyboard keyboard)
    {
        if (__instance._hasKeyboardAssigned)
        {
            return;
        }

        OkRelay? okRelay = __instance.GetComponent<OkRelay>();
        if (okRelay == null)
        {
            return;
        }

        keyboard.okButtonWasPressedEvent += okRelay.KeyboardOkPressed;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(InputFieldView.DeactivateKeyboard))]
    private static void DeactivateKeyboard(InputFieldView __instance, HMUI.UIKeyboard keyboard)
    {
        if (!__instance._hasKeyboardAssigned)
        {
            return;
        }

        OkRelay? okRelay = __instance.GetComponent<OkRelay>();
        if (okRelay == null)
        {
            return;
        }

        keyboard.okButtonWasPressedEvent -= okRelay.KeyboardOkPressed;
    }
}
