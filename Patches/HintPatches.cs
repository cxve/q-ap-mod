using System.Collections;
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
        __result = $"You found {Client.Instance.FormatPossessiveName(item.Player.Name).Sanitize()} <b>{item.ItemName.Sanitize()}</b>";
    }

    [HarmonyPatch(typeof(RankUpAnim), nameof(RankUpAnim.Init))]
    [HarmonyPostfix]
    public static void DisplayUnlockInRankUpAnim(RankUpAnim __instance, string newRankName, RankData.Rank newRank)
    {
        if (Client.Instance.slotData.goal == newRank.id) 
            __instance.crystalAmt.text = $"<size=-2>You reached your <b>Goal</b>!";
        else if (Client.Instance.checkRewards.TryGetValue(newRankName, out var item)) 
            __instance.crystalAmt.text = $"<size=-2>You found {Client.Instance.FormatPossessiveName(item.Player.Name).Sanitize()} <b>{item.ItemName.Sanitize()}</b>!";
    }

    static int challengeIndex;

    [HarmonyPatch(typeof(ChallengeManager), nameof(ChallengeManager.OnCompletedChallengeClicked))]
    [HarmonyPrefix]
    public static void RememberChallengeIndex(int challengeIndex) {
        HintPatches.challengeIndex = challengeIndex;
    }

    [HarmonyPatch(typeof(NavManager), nameof(NavManager.QueueNotification))]
    [HarmonyPrefix]
    public static bool ReplaceChallengeCompletedNotification(string header) {
        if (challengeIndex != -1 && header == "Challenge Completed!" && Simpleton<ChallengeManager>.i.activeChallenges[challengeIndex].Color == ChallengeColor.Green) {
            challengeIndex = -1;
            IEnumerator WaitForHint() {
                yield return CheckPatches.challengeQueue;
                if (!Client.Instance.checkRewards.TryGetValue(CheckPatches.challengeQueueLocation, out var item)) yield break;
                string body = $"You found {Client.Instance.FormatPossessiveName(item.Player.Name).Sanitize()} <b>{item.ItemName.Sanitize()}</b>.";
                Simpleton<NavManager>.i.QueueNotification(header, body);
            }
            Simpleton<NavManager>.i.StartCoroutine(WaitForHint());
            return false;
        }
        return true;
    }
}
