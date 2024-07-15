using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Synapse.HarmonyPatches;

// realistically this should be calculated server side... but this is easier
[HarmonyPatch]
internal static class ScorePercentageHelper
{
    private static readonly MethodInfo _getPercentage = AccessTools.Method(
        typeof(ScorePercentageHelper),
        nameof(GetPercentage));

    internal static float ScorePercentage { get; private set; }

    private static void GetPercentage(int modifiedScore, int maxModifiedScore)
    {
        ScorePercentage = (float)modifiedScore / maxModifiedScore;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(LevelCompletionResultsHelper), nameof(LevelCompletionResultsHelper.Create))]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(
                true,
                new CodeMatch(
                    n => n.opcode == OpCodes.Callvirt &&
                         ((MethodInfo)n.operand).Name == "MaxModifiedScoreForMaxMultipliedScore"),
                new CodeMatch(OpCodes.Stloc_S))
            .Advance(1)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_S, 5),
                new CodeInstruction(OpCodes.Ldloc_S, 12),
                new CodeInstruction(OpCodes.Call, _getPercentage))
            .InstructionEnumeration();
    }
}
