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
    Dictionary<int, List<long>> locationsReceived = [];
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

    internal bool AddToInventoryAndCheckIfNew(ItemInfo item)
    {
        var id = item.ItemId;
        Logger.LogInfo($"Check if item {id} sent by {item.Player} from {item.LocationId} is already in inventory");
        var data = Client.Instance.SaveData;

        if (item.LocationId >= 0)
        {
            if (!locationsReceived.ContainsKey(item.Player)) locationsReceived[item.Player] = [item.LocationId];
            else if (!locationsReceived[item.Player].Contains(item.LocationId)) locationsReceived[item.Player].Add(item.LocationId);
            else throw new Exception($"Item {id} sent by {item.Player} from {item.LocationId} was already added to the inventory!");
        }

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
        bool isNew;
        try
        {
            isNew = AddToInventoryAndCheckIfNew(item);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Could not add item to inventory, inventory remains unchanged!\n{ex.Message}");
            return;
        }
        if (!isReadyToReceiveItems)
        {
            Logger.LogWarning("The client was given an item, but was not ready to receive items yet. Try again later...");
            return;
        }
        Logger.LogInfo($"Giving item \"{item.ItemName}\"");
        if (Data.GetFeature(item, out var feature)) { if (isNew) GiveFeature(item, feature); }
        else if (item.ItemName == "Crystals") { if (isNew) GiveCrystals(item, inventory[item.ItemId]); }
        else if (item.ItemName == "Corruption Shards") { if (isNew) GiveCorruptionShards(item, inventory[item.ItemId]); }
        else if (item.ItemName == "Gold") { if (isNew) GiveGold(item, inventory[item.ItemId]); }
        else if (item.ItemName == "Upgrade Point") GiveUpgrade(item, isNew);
        else if (item.ItemName == "Random Gear") { if (isNew) GiveGear(item); }
        else
        {
            switch (item.ItemName)
            {
                case "Jackpot": SaveManager.globalData.noviceGambler = true; break;
                case "Resurrection": SaveManager.globalData.noviceMedic = true; break;
                case "Queen": SaveManager.globalData.novicePro = true; break;
                case "Hoard": SaveManager.globalData.noviceTroll = true; break;
                case "A-List": SaveManager.globalData.noviceStreamer = true; break;
                case "Hypercapitalist": SaveManager.globalData.noviceWhale = true; break;
                case "39_CATCH_FIRE": SaveManager.globalData.noviceRobot = true; break;
                case "Ψ": SaveManager.globalData.noviceWizard = true; break;
            }
            if (isNew) GiveSkill(item);
        }
    }

    void GiveSkill(ItemInfo item)
    {
        if (!Data.GetNodeByName(item.ItemName, out var node)) return;
        Logger.LogInfo("Node found!");
        var activeMap = Simpleton<SkillManager>.i.activeMap;
        // this fixes hypernode unlocks recalling skills at 0, 0, 0
        // i should at some point write a better solution that will choose any unoccupied slot
        if (Data.hypernodes.Contains(node.name))
        {
            node.gridPosition = Data.orderToPos[168];
            Client.Instance.SendMail(item.ToSerializable(), item.ItemDisplayName, $"Apparently that's a so-called hypernode, which can now be found in the item shop! I've heard it's useful in endgame.");
            if (activeMap.nodes.Any(x => x.name == node.name))
            {
                Logger.LogWarning("This hypernode already exists in the active map, will not add another!");
                return;
            }
        }
        var map = new SaveManager.SerializableSkillMap() { character = node.originalChar, nodes = [node] };
        // 
        bool foundPos = false;
        if (!node.isInventory)
        {
            Logger.LogDebug("New Fixed Node!");
            foreach (byte idHex in Client.Instance.slotData.fixedSkillPos)
            {
                var selectedNode = activeMap.GetNodeAtGridPosition(Data.orderToPos[idHex]);
                if (!selectedNode || selectedNode.isMovable) { Logger.LogError("Something went horribly wrong: There is no fixed node at the selected position!"); continue; }
                if (selectedNode.autoBuyLevel != 99) continue; // this node is already unlocked
                UnityEngine.Object.DestroyImmediate(selectedNode.gameObject);
                foreach (var connection in activeMap.connections)
                {
                    if (connection.a == null || connection.b == null)
                    {
                        UnityEngine.Object.DestroyImmediate(connection);
                        Logger.LogDebug("Destroyed a connection...");
                    }
                }
                node.gridPosition = Data.orderToPos[idHex];
                map.nodes = [node];
                foundPos = true;
                break;
            }
            if (!foundPos) Logger.LogWarning("All positions supplied by the world are taken. Did something break or did you just cheat? Falling back to legacy system.");
        }

        // determine if there is already a fixed node at the position
        var activeNode = activeMap.GetNodeAtGridPosition(node.gridPosition);
        if (!foundPos && activeNode)
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
                    if (activeMap.GetAdjacent(tempNode).Any(x => !x.isMovable)) continue;
                        Logger.LogInfo("Position found without any fixed nodes nearby");
                    activeNode = activeMap.GetNodeAtGridPosition(tempNode.gridPosition);
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
        Logger.LogInfo("Skillmap created!");
        Simpleton<HackerManager>.i.InitializeHackerNodeFromSerialized(activeMap, map, node);
        Logger.LogInfo("Hacker Node initialized!");
        if (Simpleton<ScreenManager>.i.GetState() == ScreenManager.ScreenState.LobbySkills) Simpleton<ScreenManager>.i.GoToSkill();
        if (!Data.hypernodes.Contains(node.name)) Client.Instance.SendMail(item.ToSerializable(), item.ItemDisplayName, "It's a skill, hope you can make use of it!");
    }

    void GiveFeature(ItemInfo item, FeatureData.Feature feat)
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
            Client.Instance.SendMail(item.ToSerializable(), feat.name, $"I found this description on the internet: {feat.description}");
        }
        Simpleton<ScreenManager>.i.StartCoroutine(Wait());
    }

    void GiveCrystals(ItemInfo item, int crystals)
    {
        crystals -= 1;
        int amount = 0;
        int efficiency = Client.Instance.slotData.itemPoolEfficiencyCrystals;
        for (int i = 1; i < efficiency + 1; ++i)
        {
            if (crystals * efficiency + i < 36) amount += Convert.ToInt32(Client.Instance.rankBackup[crystals * efficiency + i].crystalEarned);
            else amount += UnityEngine.Mathf.RoundToInt(UnityEngine.Random.value * 75 + 75);
        }
        Simpleton<StatsManager>.i.UpdateStatsAdd("TOTAL_CRYSTAL_EARNED", amount);
        Simpleton<PlayerManager>.i.progressData.EarnCrystal(amount, true);
        Client.Instance.SendMail(item.ToSerializable(), $"{amount} Crystals", "Use it to unlock some stuff for your friends, like me ;)");
        if (Simpleton<ScreenManager>.i.GetState() == ScreenManager.ScreenState.LobbyShop) Simpleton<ScreenManager>.i.GoToShop();
    }

    void GiveCorruptionShards(ItemInfo item, int shards)
    {
        int amount = 0;
        int efficiency = Client.Instance.slotData.itemPoolEfficiencyCorruptionShards;
        for (int i = 0; i < efficiency; ++i)
        {
            if (shards * efficiency + i < 20) amount += Convert.ToInt32(Client.Instance.rankBackup[35 + shards * efficiency + i].crystalEarned);
            else amount += UnityEngine.Mathf.RoundToInt(UnityEngine.Random.value * 10 + 5);
        }
        Simpleton<StatsManager>.i.UpdateStatsAdd("TOTAL_CORRUPTION_SHARDS_EARNED", amount);
        Simpleton<PlayerManager>.i.progressData.EarnCorruptionShard(amount, isLocal: true);
        Client.Instance.SendMail(item.ToSerializable(), $"{amount} Corruption Shards", "This will probably come in handy during the second half of your game.");
    }

    void GiveGold(ItemInfo item, int gold)
    {
        int amount = Mathf.RoundToInt(UnityEngine.Random.value * 100) * 1000 + 200_000;
        if (gold < 15)
        {
            float _amount = 300 * Mathf.Pow(gold, 2.5f);
            int magnitude = Mathf.CeilToInt(Mathf.Log10(_amount));
            amount = Mathf.RoundToInt(_amount / Mathf.Pow(10, magnitude - 2)) * (int)Mathf.Pow(10, magnitude - 2);
        }
        Simpleton<PlayerManager>.i.progressData.EarnGold(amount, true);
        Client.Instance.SendMail(item.ToSerializable(), $"{amount} Gold", "Buy yourself something nice in the item shop, if you have it unlocked!");
    }

    void GiveGear(ItemInfo item)
    {
        var gear = Simpleton<ShopManager>.i.shopV2Picks.GetRandomItem(10);
        Simpleton<ItemManager>.i.CreateItemInstanceFromBlueprint(gear.item);
        Client.Instance.SendMail(item.ToSerializable(), $"{gear.item.displayName}", $"Actually, I bought it from the item shop. It reminded me of you, so I just had to get you one!");
    }

    void GiveUpgrade(ItemInfo item, bool isNew)
    {
        var sm = Simpleton<SkillManager>.i;
        int efficiency = Client.Instance.slotData.itemPoolEfficiencyUpgradePoints;
        for (int i = 0; i < efficiency; ++i) sm.activeMap.upgradePointLevelsExplicit.Add(0);
        sm.activeMap.RefreshMap();
        Simpleton<PlayerManager>.i.progressData.upgradePoints = sm.activeMap.upgradePointLevelsExplicit.Count - Simpleton<PlayerManager>.i.progressData.GetSpentUpgradePoints();
        if (isNew) Client.Instance.SendMail(item.ToSerializable(), $"{efficiency} Upgrade Point{(efficiency != 1 ? "s" : "")}", "You can use it to upgrade your fixed skills, if you have any...");
    }
}
