using HarmonyLib;
using TastyTools;

namespace cxve.qap.Patches;

[HarmonyPatch]
internal class HintPatches
{
    [HarmonyPatch(typeof(ScreenManager), nameof(ScreenManager.GoToShop))]
    [HarmonyPostfix]
    public static void CreateHintsWhenEnteringShop()
    {
        Client.Instance.CreateHintsForShop();
    }

    [HarmonyPatch(typeof(ShopManager), nameof(ShopManager.RefreshUnlockableFeatures))]
    [HarmonyPostfix]
    public static void CreateHintsWhenShopRefreshes()
    {
        if (Simpleton<ScreenManager>.i.GetState() != ScreenManager.ScreenState.LobbyShop) return;
        Client.Instance.CreateHintsForShop();
    }

    static string level;

    [HarmonyPatch(typeof(LevelUpAnim), nameof(LevelUpAnim.InternalInit))]
    [HarmonyPrefix]
    public static void DisplayUnlockInLevelUpAnim(string level)
    {
        HintPatches.level = level;
    }

    [HarmonyPatch(typeof(LevelUpAnim), "FormatUnlockedSkills")]
    [HarmonyPostfix]
    public static void DisplayUnlockInLevelUpAnim(ref string __result)
    {
        if (!Client.Instance.checkRewards.TryGetValue($"Level {level}", out var item)) return;
        __result = $"You found {Client.Instance.FormatPossessiveName(item.Player.Name)} <b>{item.ItemName}</b>";
    }

    [HarmonyPatch(typeof(RankUpAnim), nameof(RankUpAnim.Init))]
    [HarmonyPostfix]
    public static void DisplayUnlockInRankUpAnim(RankUpAnim __instance, string newRankName)
    {
        if (!Client.Instance.checkRewards.TryGetValue(newRankName, out var item)) return;
        __instance.crystalAmt.text = $"<size=-2>You found {Client.Instance.FormatPossessiveName(item.Player.Name)} <b>{item.ItemName}</b>!";
    }
}
