using HarmonyLib;
using System;

namespace cxve.qap.Patches;

// used to expose private methods
[HarmonyPatch]
internal class ReversePatches
{
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(ScreenManager), "HideAll")]
    public static void HideAll(object instance) =>
        throw new NotImplementedException("If you are seeing this, the mod failed to patch the game. This is probably caused by an error upstream. If you report this issue, please include your log file!");

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(Challenge), nameof(Challenge.Color), MethodType.Setter)]
    public static void SetColor(object instance, ChallengeColor color) =>
        throw new NotImplementedException("If you are seeing this, the mod failed to patch the game. This is probably caused by an error upstream. If you report this issue, please include your log file!");

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(SettingsManager), "GoToCharacterSelect")]
    public static void GoToMainMenu(object instance, bool force = false) =>
        throw new NotImplementedException("If you are seeing this, the mod failed to patch the game. This is probably caused by an error upstream. If you report this issue, please include your log file!");
}
