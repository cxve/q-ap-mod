using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TastyTools;
using TMPro;
using UnityEngine;

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
                if (rank == Client.Instance.slotData.goal) Client.Instance.SendGoal();
                else Client.Instance.QueueCheck(rank_name);
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
        __instance.challengeReward.text = Util.BuildAPIcon();
    }

    [HarmonyPatch(typeof(ChallengeManager), nameof(ChallengeManager.GetCorruptionShardReward))]
    [HarmonyPostfix]
    public static void RemoveCorruptionReward(ref int __result, Challenge challenge)
    {
        if (challenge.Color == ChallengeColor.Green) __result = 0;
    }

    internal static string challengeQueueLocation;
    internal static Coroutine challengeQueue;
    [HarmonyPatch(typeof(AchievementsManager), nameof(AchievementsManager.OnChallengeComplete))]
    [HarmonyPrefix]
    public static bool SendChallengeCheck(Challenge challenge)
    {
        if (challenge.Color == ChallengeColor.Green)
        {
            challengeQueueLocation = $"Tier {challenge.challengeTier} Challenge {Client.Instance.inventory.ChallengeCheck(challenge.challengeTier)}";
            Client.Instance.QueueCheck(challengeQueueLocation);
            challengeQueue = Client.Instance.QueueSend();
        }
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
        while ((index = __instance.activeChallenges.FindIndex(x => x.Color == ChallengeColor.Green && x.GetRewardMultiplier() > 0)) >= 0)
        {
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

    static void SetTargetBonusText(TextMeshProUGUI targetBonusText) {
        if (Client.Instance.IsRecyclingSetAvailable(Simpleton<PlayerManager>.i.progressData.currentTargetBonus))
            targetBonusText.text = targetBonusText.text.Replace("2x", 
            $"<line-height=0>\n<color=#000000AA><pos=148><voffset=-14><size=62>•</size></color><size=12>{Util.BuildAPIconSmall(160, 4)}</size><pos=173><voffset=0>");
    }

    [HarmonyPatch(typeof(EquipScreenManager), nameof(EquipScreenManager.CheckTargetBonus))]
    [HarmonyPostfix]
    public static void TargetBonusWhenGearIsOpened(TextMeshProUGUI ___targetBonusText) {
        SetTargetBonusText(___targetBonusText);
    }

    [HarmonyPatch(typeof(EquipScreenManager), "SetNewTargetBonus")]
    [HarmonyPostfix]
    public static void TargetBonusAfterRecycle(TextMeshProUGUI ___targetBonusText)
    {
        SetTargetBonusText(___targetBonusText);
    }

    [HarmonyPatch(typeof(EquipScreenManager), nameof(EquipScreenManager.GetBuybackInfo))]
    [HarmonyPostfix]
    public static void SetRewardToOne(ref (float bonus, string text, bool isTargetBonus, int totalSellValue) __result)
    {
        if (__result.isTargetBonus && Client.Instance.IsRecyclingSetAvailable(__result.text))
            __result = (0, __result.text, true, 1);
    }

    static bool ShouldSendRecyclingSetCheck(EquipScreenManager instance, out string set)
    {
        set = "";
        if (!SaveManager.IsFeatureUnlockedLocally(46)) return false;
        var items = (from node in instance.itemNodes where instance.buybackSlots.Contains(node.currentSlot) select node.item).ToList();
        var (_, text, isTargetBonus, _) = instance.GetBuybackInfo(items);
        set = text;
        return isTargetBonus;
    }

    [HarmonyPatch(typeof(EquipScreenManager), "RefreshSellValue")]
    [HarmonyPostfix]
    public static void RefreshSellValue(EquipScreenManager __instance)
    {
        if (!ShouldSendRecyclingSetCheck(__instance, out _)) return;
        __instance.loadBonusText.text = "SET BONUS: Archipelago Item";
        __instance.totalSellValueText.text += "<color=white> + 1</color><size=12>" + Util.BuildAPIconSmall(192, 4);
        SetTargetBonusText(__instance.targetBonusText);
    }

    [HarmonyPatch(typeof(EquipScreenManager), "SellBuybackItemsPressed")]
    [HarmonyPrefix]
    public static void SendRecyclingSetCheck(EquipScreenManager __instance)
    {
        if (!ShouldSendRecyclingSetCheck(__instance, out string set)) return;
        Client.Instance.SendCheck(set + " Set");
    }

    static void UpdateComboMilestoneText(GameObject triggerCountGroup) {
        int nextMilestone = Client.Instance.NextTriggerComboCheck();
        if (nextMilestone < 0)
            triggerCountGroup.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Trigger";
        else
            triggerCountGroup.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text =
            "Trigger<line-height=0>\n</size><voffset=-72><pos=-12><color=black><alpha=#AA><size=98>•</size><alpha=#FF>" +
            Util.BuildAPIconSmall(5, -47) + "<color=white><voffset=-46><pos=26>at " + nextMilestone;
    }

    [HarmonyPatch(typeof(MatchSummaryWidget), nameof(MatchSummaryWidget.ShowTriggerCount))]
    [HarmonyPostfix]
    public static void UpdateComboCheckMilestone(GameObject ___triggerCountGroup)
    {
        UpdateComboMilestoneText(___triggerCountGroup);
    }

    [HarmonyPatch(typeof(MatchSummaryWidget), nameof(MatchSummaryWidget.UpdateTriggerCount))]
    [HarmonyPostfix]
    public static void SendComboCheck(int count, GameObject ___triggerCountGroup)
    {
        int milestone = Client.Instance.NextTriggerComboCheck();
        if (milestone < 1 || count < milestone) return;
        Client.Instance.SendCheck($"{milestone} nodes triggered in one flip");
        UpdateComboMilestoneText(___triggerCountGroup);
        AudioManager.SafePlayOneShot("crystal");
    }
}
