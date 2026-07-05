using HarmonyLib;
using TastyTools;
using UnityEngine;

namespace cxve.qap.Patches;

[HarmonyPatch]
internal class GeneralPatches
{
    // separate saves for this mod
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Initialize))]
    [HarmonyPrefix]
    public static void ChangeSavePaths(ref string ___saveDirectory, ref string ___globalDataFilename)
    {
        ___saveDirectory = "AP_saves";
        ___globalDataFilename = "AP_globaldata.json";
    }

    // prevent achievements while using this mod
    // TODO: test this!
    [HarmonyPatch(typeof(AchievementsManager), nameof(AchievementsManager.SetAchievement))]
    [HarmonyPrefix]
    public static bool BlockAchievements()
    {
        return false;
    }

    // we are always offline, we don't want this
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.CheckForChestAndKeyGeneration))]
    [HarmonyPrefix]
    public static bool BlockChestAndKeyGeneration()
    {
        Plugin.Logger.LogDebug("Successfully blocked chest and key generation!");
        return false;
    }

    // force offline mode for now (we don't want to crash vanilla players with this mod)
    [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.OnQUpPressed))]
    [HarmonyPrefix]
    public static void ForceOfflineMode()
    {
        SaveManager.globalData.offlineSetting = true;
    }

    [HarmonyPatch(typeof(ScreenManager), nameof(ScreenManager.GoToCharSelect))]
    [HarmonyPatch(typeof(ScreenManager), nameof(ScreenManager.GotoFirstTimeFlow))]
    [HarmonyPrefix]
    public static bool BlockCharSelect(ScreenManager __instance)
    {
        Simpleton<WidescreenBackgroundManager>.i.SetClouds();
        ReversePatches.HideAll(__instance);
        return false;
    }

    [HarmonyPatch(typeof(SettingsManager), "GoToCharacterSelect")]
    [HarmonyPostfix]
    public static void DisconnectAndReset()
    {
        Client.Instance.DisconnectAndReset();
    }

    [HarmonyPatch(typeof(ScreenManager), nameof(ScreenManager.GoToLobby))]
    [HarmonyPostfix]
    public static void HideOfflineNotification()
    {
        Simpleton<NavManager>.i.offlineNotification.transform.localScale = Vector3.zero;
    }

    [HarmonyPatch(typeof(SettingsManager), nameof(SettingsManager.Show))]
    [HarmonyPostfix]
    public static void HideOfflineSetting()
    {
        Simpleton<SettingsManager>.i.offlineToggle.gameObject.SetActive(false);
    }

    [HarmonyPatch(typeof(ProgressData), nameof(ProgressData.GetTotalUpgradePointsForXPLevel))]
    [HarmonyPostfix]
    public static void OverwriteUpgradePoints(ref int __result)
    {
        __result = Client.Instance.CountItemInSession("Upgrade Point") * Client.Instance.slotData.itemPoolEfficiencyUpgradePoints;
    }

    [HarmonyPatch(typeof(SkillMap), nameof(SkillMap.InitializeFromProgressData))]
    [HarmonyPrefix]
    public static void FixMatchSkillMap(SkillMap __instance, ref SaveManager.SerializableSkillMap serializedMap, bool isLocal)
    {
        if (__instance.owner.IsLocal || isLocal)
        {
            Plugin.Logger.LogInfo($"Upgrade Point Level Filler: {__instance.upgradePointLevelsFiller.Count}");
            if (__instance.upgradePointLevelsFiller.Count > 0)
            {
                foreach (var node in __instance.nodes) if (node != null) Object.DestroyImmediate(node.gameObject);
                foreach (var node in __instance.connections)
                    if (node != null && node.gameObject != null)
                        Object.DestroyImmediate(node.gameObject);
                __instance.nodes = [];
                __instance.connections = [];
            }
            __instance.upgradePointLevelsFiller = [];
            __instance.upgradePointLevelsExplicit = Simpleton<SkillManager>.i.activeMap.upgradePointLevelsExplicit;
            __instance.character = ChampionType.Hacker;
        }
    }

    [HarmonyPatch(typeof(SkillMap), nameof(SkillMap.InitializeFromProgressData))]
    [HarmonyPostfix]
    public static void ResetChamp(SkillMap __instance)
    {
        __instance.character = (ChampionType)Client.Instance.slotData.champ;
    }
}
