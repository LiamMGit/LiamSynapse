using HarmonyLib;
using HMUI;
using IPA.Loader;
using JetBrains.Annotations;
using Synapse.Controllers;

namespace Synapse.KeyboardKeyer;

[HarmonyPatch(typeof(InputFieldView))]
internal static class AssignChatKeyboard
{
    [UsedImplicitly]
    [HarmonyPrepare]
    private static bool CheckKeyboardKeyer()
    {
        return PluginManager.GetPlugin("KeyboardKeyer") == null;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(InputFieldView.ActivateKeyboard))]
    private static void ActivateKeyboard(InputFieldView __instance, UIKeyboard keyboard)
    {
        if (__instance._hasKeyboardAssigned)
        {
            return;
        }

        if (__instance.GetComponent<OkRelay>() == null)
        {
            return;
        }

        InputKiller.Active = true;
        KeyboardKeyer keyboardKeyer = keyboard.gameObject.GetComponent<KeyboardKeyer>();
        if (keyboardKeyer != null)
        {
            keyboardKeyer.enabled = true;
        }
        else
        {
            keyboard.gameObject.AddComponent<KeyboardKeyer>();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(InputFieldView.DeactivateKeyboard))]
    private static void DeactivateKeyboard(InputFieldView __instance, UIKeyboard keyboard)
    {
        if (!__instance._hasKeyboardAssigned)
        {
            return;
        }

        InputKiller.Active = false;
        KeyboardKeyer keyboardKeyer = keyboard.gameObject.GetComponent<KeyboardKeyer>();
        if (keyboardKeyer != null)
        {
            keyboardKeyer.enabled = false;
        }
    }
}
