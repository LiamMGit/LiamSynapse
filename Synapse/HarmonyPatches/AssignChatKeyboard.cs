using HarmonyLib;
using HMUI;
using Synapse.Controllers;
using UnityEngine;

namespace Synapse.HarmonyPatches
{
    [HarmonyPatch(typeof(InputFieldView))]
    internal static class AssignChatKeyboard
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

            InputKiller.Active = true;
            keyboard.okButtonWasPressedEvent += okRelay.KeyboardOkPressed;
            keyboard.gameObject.AddComponent<KeyboardKeyer>();
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

            InputKiller.Active = false;
            keyboard.okButtonWasPressedEvent -= okRelay.KeyboardOkPressed;
            Object.Destroy(keyboard.GetComponent<KeyboardKeyer>());
        }
    }
}
