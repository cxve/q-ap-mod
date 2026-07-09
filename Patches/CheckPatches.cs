using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TastyTools;
using TMPro;

namespace cxve.qap.Patches;

// these patches change the game in order to support checks
[HarmonyPatch]
internal class CheckPatches
{
    [HarmonyPatch(typeof(ProgressData), nameof(ProgressData.EarnQ))]
    [HarmonyPrefix]
    public static void SendRankCheck(ProgressData __instance, long earnedQ, bool isLocal)
    {
        long q = __instance.q;
        int rank_old = Simpleton<DataManager>.i.GetRankForQ(q).id;
        if (rank_old < 55 && isLocal)
        {
            q += earnedQ;
            int rank_new = Simpleton<DataManager>.i.GetRankForQ(q).id;
            for (int rank = rank_old + 1; rank <= rank_new; ++rank)
            {
                var rank_name = Simpleton<DataManager>.i.GetRankById(rank).name;
                Client.Instance.QueueCheck(rank_name);
                if (rank == 55) Client.Instance.SendGoal();
            }
        }
    }

    [HarmonyPatch(typeof(ProgressData), nameof(ProgressData.EarnXP))]
    [HarmonyPrefix]
    public static void SendLevelCheck(ProgressData __instance, long earnedXP, bool isLocal)
    {
        long xp = __instance.xp;
        int level_old = Simpleton<DataManager>.i.GetXPLevel(xp);
        if (isLocal)
        {
            if (level_old < 50)
            {
                xp += earnedXP;
                int level_new = Simpleton<DataManager>.i.GetXPLevel(xp);
                for (int level = level_old + 1; level <= level_new; ++level)
                {
                    var level_name = $"Level {level}";
                    Client.Instance.QueueCheck(level_name);
                }
            }
            Client.Instance.QueueSend();
        }
    }

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.UnlockFeature))]
    [HarmonyPostfix]
    public static void SendShopUpgradeCheck(int id)
    {
        Plugin.Logger.LogInfo($"Sending check for {id}");
        if (id < 99)
        {
            Plugin.Logger.LogInfo("This is probably a shop upgrade received from another player, no check will be sent...");
            return;
        }
        Client.Instance.SendCheck(Simpleton<PlayerManager>.i.progressData.unlockedFeatures.First(x => x.id == id).constName);
    }

    [HarmonyPatch(typeof(ChallengeFactory), nameof(ChallengeFactory.CreateRandomChallenge))]
    [HarmonyPostfix]
    public static void CreateTier1ChallengeAP(ref Challenge __result)
    {
        if (!Client.Instance.IsChallengeAvailable(1) || Simpleton<ChallengeManager>.i.activeChallenges.Any(x => x.Color == ChallengeColor.Green && x.challengeTier == 1)) return;
        var challenge = new Challenge(ChallengeColor.Green, __result.Condition);
        challenge.SetRewardMultiplier(0);
        __result = challenge;
    }

    [HarmonyPatch(typeof(ChallengeUIElement), nameof(ChallengeUIElement.UpdateChallenge))]
    [HarmonyPostfix]
    public static void UpdateRewardText(ChallengeUIElement __instance)
    {
        if (__instance.challengeReward.text != "0") return;
        __instance.challengeReward.spriteAsset = Plugin.APIcon;
        __instance.challengeReward.font = TMP_Settings.defaultFontAsset;
        __instance.challengeReward.text = "<color=#a0c4ff><voffset=-4><pos=-7>•<color=#fdffb6><voffset=4><pos=-7>•<color=#ffadad><voffset=8><pos=0>•<voffset=4><color=#caffbf><pos=7>•<voffset=-4><pos=7><color=#bdb2ff>•<voffset=-8><color=#ffd6a5><pos=0>•";
    }

    [HarmonyPatch(typeof(ChallengeManager), nameof(ChallengeManager.GetCorruptionShardReward))]
    [HarmonyPostfix]
    public static void RemoveCorruptionReward(ref int __result, Challenge challenge)
    {
        if (challenge.Color == ChallengeColor.Green) __result = 0;
    }

    [HarmonyPatch(typeof(AchievementsManager), nameof(AchievementsManager.OnChallengeComplete))]
    [HarmonyPrefix]
    public static bool SendChallengeCheck(Challenge challenge)
    {
        if (challenge.Color == ChallengeColor.Green)
            Client.Instance.SendCheck($"Tier {challenge.challengeTier} Challenge {Client.Instance.inventory.ChallengeCheck(challenge.challengeTier)}");
        return false;
    }

    [HarmonyPatch(typeof(ChallengeManager), nameof(ChallengeManager.ShowMergeScreen))]
    [HarmonyPrefix]
    public static void TurnLowXPMergeIntoCheck(ChallengeManager __instance, ref List<Challenge> group)
    {
        int tier = group[0].challengeTier + 1;
        if (!Client.Instance.IsChallengeAvailable(tier) || Simpleton<ChallengeManager>.i.activeChallenges.Any(x => x.Color == ChallengeColor.Green && x.challengeTier == tier)) return;
        var min = group.Min(x => x.GetRewardMultiplier());
        var index = group.FindIndex(x => x.GetRewardMultiplier() <= min);
        group[index].SetRewardMultiplier(0); // this also sets the challenge color to green, which is required in the next step!
    }

    [HarmonyPatch(typeof(ChallengeManager), nameof(ChallengeManager.ShowMergeScreen))]
    [HarmonyPostfix]
    public static void RemoveMergeXP(ChallengeManager __instance, ref List<Challenge> group)
    {
        var index = group.FindIndex(x => x.Color == ChallengeColor.Green);
        if (index < 0) return;
        group[index].SetRewardMultiplier(0);
        __instance.challengeDiscoveryUIElements[index].UpdateChallenge(group[index], index, group[index].challengeTier + 1);
    }

    [HarmonyPatch(typeof(Challenge), nameof(Challenge.SetRewardMultiplier))]
    [HarmonyPostfix]
    public static void FixMergeButtonClick(Challenge __instance, float multiplier)
    {
        if (multiplier == 0) ReversePatches.SetColor(__instance, ChallengeColor.Green);
    }

    [HarmonyPatch(typeof(ChallengeManager), nameof(ChallengeManager.OnMergeButtonClick))]
    [HarmonyPostfix]
    public static void FixChallengeRewardAgain(ChallengeManager __instance)
    {
        int index;
        while ((index = __instance.activeChallenges.FindIndex(x => x.Color == ChallengeColor.Green && x.GetRewardMultiplier() > 0)) >= 0) {
            __instance.activeChallenges[index].SetRewardMultiplier(0);
            __instance.challengeUIElements[index].UpdateChallenge(__instance.activeChallenges[index], index);
        }
    }

    // this is just to fix a visual bug which occurs when clicking on a completed challenge causes a merge
    // i am fairly sure this is a vanilla bug, but it stands out due to AP challenges being visually distinct
    [HarmonyPatch(typeof(ChallengeManager), nameof(ChallengeManager.CheckForMerges))]
    [HarmonyPrefix]
    public static void CheckForMerges(ChallengeManager __instance, ref bool __state)
    {
        __instance.UpdateChallengeUI();
    }

    [HarmonyPatch(typeof(ChallengeManager), nameof(ChallengeManager.UpdateChallengeUI))]
    [HarmonyPrefix]
    public static bool UpdateChallengeUI(List<Challenge> ___mergeChallenges)
    {
        if (___mergeChallenges.Count > 0) return false;
        return true;
    }
}
