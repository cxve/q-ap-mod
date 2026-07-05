using Archipelago.MultiClient.Net.Models;
using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TastyTools;
using UnityEngine;

namespace cxve.qap;

internal class Inventory
{
    ManualLogSource Logger { get => Plugin.Logger; }
    Dictionary<long, int> inventory = [];
    internal bool isReadyToReceiveItems = false;
    Dictionary<int, int> checksChallenges = new Dictionary<int, int>()
    {
        { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }
    };

    internal Inventory() { }

    internal void ItemReceived(Archipelago.MultiClient.Net.Helpers.ReceivedItemsHelper helper)
    {
        var item = helper.PeekItem();
        Logger.LogInfo($"Item received: {item.ItemName}");
        GiveItemOnMain(item);
        helper.DequeueItem();
    }

    internal int ChallengeCheck(int tier) => ++checksChallenges[tier];

    // extremely important! this forces the code to run on the main thread, without it the game will crash
    // i heavily preferred this solution over anything i was able to find on the internet
    internal void GiveItemOnMain(ItemInfo item)
    {
        IEnumerator RunOnMain()
        {
            yield return new WaitForFixedUpdate();
            while (!isReadyToReceiveItems) yield return new WaitForSecondsRealtime(1);
            GiveItem(item);
        }
        Simpleton<HackerManager>.i.StartCoroutine(RunOnMain());
    }

    internal bool AddToInventoryAndCheckIfNew(long id)
    {
        Logger.LogInfo($"Check if item with id {id} is already in inventory");
        var data = Client.Instance.SaveData;
        if (!inventory.ContainsKey(id)) inventory[id] = 1;
        else ++inventory[id];
        Logger.LogInfo($"Temp Inventory Count {inventory[id]}");
        if (!data.inventory.ContainsKey(id) || inventory[id] > data.inventory[id])
        {
            Logger.LogInfo($"Item is new, make item persistant");
            data.inventory[id] = inventory[id];
            Client.Instance.SaveData = data;
            return true;
        }
        Logger.LogInfo($"Permanent Inventory Count {data.inventory[id]}");
        return false;
    }

    internal int Count(long id)
    {
        if (!inventory.ContainsKey(id)) return 0;
        return inventory[id];
    }

    internal void GiveItem(ItemInfo item)
    {
        bool isNew = AddToInventoryAndCheckIfNew(item.ItemId);
        string sender = "Somebody";
        if (item.Player.Name != "") sender = item.Player.Name;
        string title = Client.Instance.IsThisMe(sender) ? "You found this!" : $"{sender} found this!";
        if (!isReadyToReceiveItems)
        {
            Logger.LogWarning("The client was given an item, but was not ready to receive items yet. Try again later...");
            return;
        }
        Logger.LogInfo($"Giving item \"{item.ItemName}\"");
        if (Data.GetFeature(item, out var feature)) { if (isNew) GiveFeature(title, feature); }
        else if (item.ItemName == "Crystals") { if (isNew) GiveCrystals(title, inventory[item.ItemId]); }
        else if (item.ItemName == "Corruption Shards") { if (isNew) GiveCorruptionShards(title, inventory[item.ItemId]); }
        else if (item.ItemName == "Gold") { if (isNew) GiveGold(title, inventory[item.ItemId]); }
        else if (item.ItemName == "Upgrade Point") GiveUpgrade(title, isNew);
        else if (isNew) GiveSkill(title, item);
    }

    void GiveSkill(string title, ItemInfo item)
    {
        if (!Data.GetNodeByName(item.ItemName, out var node)) return;
        Logger.LogInfo("Node found!");
        var map = new SaveManager.SerializableSkillMap() { character = node.originalChar, nodes = [node] };
        // determine if there is already a fixed node at the position
        var activeNode = Simpleton<SkillManager>.i.activeMap.GetNodeAtGridPosition(node.gridPosition);
        if (activeNode)
        {
            Logger.LogInfo("Node found at requested position");
            if (activeNode.isMovable) Simpleton<SkillManager>.i.skillCharacterWidget.SetInventoryState(activeNode, true, false);
            else
            {
                Logger.LogInfo("Node not movable, trying to find a new position");
                // let's try to find a valid position for this skill
                for (int i = 0; i < Data.orderToPos.Length; ++i)
                {
                    var tempNode = new SkillNode() { gridPosition = Data.orderToPos[i] };
                    if (!Simpleton<SkillManager>.i.activeMap.GetAdjacent(tempNode).Any(x => !x.isMovable))
                    {
                        Logger.LogInfo("Position found without any fixed nodes nearby");
                        activeNode = Simpleton<SkillManager>.i.activeMap.GetNodeAtGridPosition(tempNode.gridPosition);
                        if (activeNode)
                            if (activeNode.isMovable) Simpleton<SkillManager>.i.skillCharacterWidget.SetInventoryState(activeNode, true, false);
                            else continue; // but there was a fixed node on the position itself, skip
                        node.gridPosition = tempNode.gridPosition;
                        map.nodes = [node];
                        Logger.LogInfo("Position set!");
                        break;
                    }
                }
            }
        }
        Logger.LogInfo("Skillmap created!");
        Simpleton<HackerManager>.i.InitializeHackerNodeFromSerialized(Simpleton<SkillManager>.i.activeMap, map, node);
        Logger.LogInfo("Hacker Node initialized!");
        Client.Instance.SendNotification(title, $"Skill: <b>{item.ItemName}</b>", "levelup");
    }

    void GiveFeature(string title, FeatureData.Feature feat)
    {
        IEnumerator Wait()
        {
            // receiving honor duels while in a match results in a soft lock
            if (feat.constName == "HONOR_DUELS")
                while (Simpleton<MatchManager>.i.isInMatch) yield return new WaitForSecondsRealtime(1);
            Simpleton<PlayerManager>.i.progressData.unlockedFeatures.Add(feat);
            Simpleton<ShopManager>.i.TryUnlockFeature(feat, true);
            SaveManager.CheckNewInitsAfterFeatureUnlock(feat.id);
            Simpleton<NavManager>.i.RefreshButtonVisibility();
            Client.Instance.SendNotification(title, $"{feat.name}\n\n{feat.description}", "levelup");
        }
        Simpleton<ScreenManager>.i.StartCoroutine(Wait());
    }

    void GiveCrystals(string title, int crystals)
    {
        int amount = 0;
        int efficiency = Client.Instance.slotData.itemPoolEfficiencyCrystals;
        for (int i = 0; i < efficiency; ++i)
        {
            if (crystals * efficiency + i < 36) amount += Convert.ToInt32(Client.Instance.rankBackup[crystals * efficiency + i].crystalEarned);
            else amount += UnityEngine.Mathf.RoundToInt(UnityEngine.Random.value * 50 + 50);
        }
        Simpleton<StatsManager>.i.UpdateStatsAdd("TOTAL_CRYSTAL_EARNED", amount);
        Simpleton<PlayerManager>.i.progressData.EarnCrystal(amount, true);
        Client.Instance.SendNotification(title, $"{amount} Crystals", "crystal");
    }

    void GiveCorruptionShards(string title, int shards)
    {
        int amount = 0;
        int efficiency = Client.Instance.slotData.itemPoolEfficiencyCorruptionShards;
        for (int i = 0; i < efficiency; ++i)
        {
            if (shards * efficiency + i < 20) amount += Convert.ToInt32(Client.Instance.rankBackup[35 + shards * efficiency + i].crystalEarned);
            else amount += UnityEngine.Mathf.RoundToInt(UnityEngine.Random.value * 10);
        }
        Simpleton<StatsManager>.i.UpdateStatsAdd("TOTAL_CORRUPTION_SHARDS_EARNED", amount);
        Simpleton<PlayerManager>.i.progressData.EarnCorruptionShard(amount, isLocal: true);
        Client.Instance.SendNotification(title, $"{amount} Corruption Shards", "crystal");
    }

    void GiveGold(string title, int gold)
    {
        int amount = UnityEngine.Mathf.RoundToInt(UnityEngine.Random.value * 100) * 1000;
        if (gold < 38)
        {
            float _amount = 300 * UnityEngine.Mathf.Pow(gold, 1.6f);
            int magnitude = UnityEngine.Mathf.CeilToInt(UnityEngine.Mathf.Log10(_amount));
            amount = UnityEngine.Mathf.RoundToInt(_amount / UnityEngine.Mathf.Pow(10, magnitude - 2)) * (int)UnityEngine.Mathf.Pow(10, magnitude - 2);
        }
        Simpleton<PlayerManager>.i.progressData.EarnGold(amount, true);
        Client.Instance.SendNotification(title, $"{amount} Gold", "BigGold");
    }

    void GiveUpgrade(string title, bool isNew)
    {
        var sm = Simpleton<SkillManager>.i;
        int efficiency = Client.Instance.slotData.itemPoolEfficiencyUpgradePoints;
        for (int i = 0; i < efficiency; ++i) sm.activeMap.upgradePointLevelsExplicit.Add(0);
        sm.activeMap.RefreshMap();
        Simpleton<PlayerManager>.i.progressData.upgradePoints = sm.activeMap.upgradePointLevelsExplicit.Count - Simpleton<PlayerManager>.i.progressData.GetSpentUpgradePoints();
        if (isNew) Client.Instance.SendNotification(title, $"{efficiency} Upgrade Point{(efficiency != 1 ? "s" : "")}", "hackerLevelUP");
    }
}
