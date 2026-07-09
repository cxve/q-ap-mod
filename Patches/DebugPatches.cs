using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace cxve.qap.Patches;

[HarmonyPatch]
internal class DebugPatches
{
    [HarmonyPatch(typeof(SkillMap), nameof(SkillMap.InitializeLocal))]
    [HarmonyPrefix]
    public static void Log() => Plugin.Logger.LogDebug("If you are seeing this while respecing: The respec transpiler patch did not work!");
}
