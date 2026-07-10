using Archipelago.MultiClient.Net.Models;
using BepInEx.Logging;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TastyTools;
using UnityEngine;
using static SaveManager;

namespace cxve.qap;

internal class Data
{
    internal static readonly Regex regexSkillName = new("^\\d{1,2}_");
    static ManualLogSource Logger { get => Plugin.Logger; }
    static Dictionary<string, SerializableSkillNode> skillNameToNode;
    static Dictionary<string, FeatureData.Feature> featureMap;
    // this maps progressive upgrades to their respective shop upgrades
    static Dictionary<string, string[]> progressiveMap = new()
    {
        { "PROGRESSIVE_WALLET_SIZE", ["INCREASED_WALLET_SIZE", "EVEN_BIGGER_WALLET", "JUMBO_WALLET", "FULLBODY_WALLET_SUIT", "WORLDS_BIGGEST_WALLET"] },
        { "PROGRESSIVE_ITEM_RECYCLING_SYSTEM", ["ITEM_RECYCLING_SYSTEM", "ENHANCED_ITEM_RECYCLING__SORTING"] },
        { "PROGRESSIVE_ITEM_SLOT", ["ADDITIONAL_ITEM_SLOT_1", "ADDITIONAL_ITEM_SLOT_2", "ADDITIONAL_ITEM_SLOT_3", "ADDITIONAL_ITEM_SLOT_4"] },
        { "PROGRESSIVE_SHARD_SLOT_CAPACITY", ["INCREASED_SHARD_SLOT_CAPACITY", "MAXIMUM_SHARD_SLOT_CAPACITY"] },
        { "PROGRESSIVE_SHOP_SLOT", ["ADDITIONAL_SHOP_SLOT_1", "ADDITIONAL_SHOP_SLOT_2", "ADDITIONAL_SHOP_SLOT_3", "ADDITIONAL_SHOP_SLOT_4", "ADDITIONAL_SHOP_SLOT_5"] },
        { "PROGRESSIVE_QBLOCK_BREAKER", ["QBLOCK_BREAKER_1", "QBLOCK_BREAKER_2", "QBLOCK_BREAKER_3", "QBLOCK_BREAKER_4", "QBLOCK_BREAKER_5", "QBLOCK_BREAKER_6", "QBLOCK_BREAKER_7", "QBLOCK_BREAKER_8", "QBLOCK_BREAKER_9"] },
        { "PROGRESSIVE_CHALLENGES", ["CHALLENGES", "MORE_BETTERED_CHALLENGES"] },
        { "PROGRESSIVE_CHALLENGE_SLOT", ["ADDITIONAL_CHALLENGE_SLOT_1", "ADDITIONAL_CHALLENGE_SLOT_2"] },
        { "PROGRESSIVE_STATS", ["STATS_", "STATS_CHARTS", "ENDGAME_LEADERBOARDS"] },
        { "PROGRESSIVE_SHOP_REROLL", ["SHOP_REROLL", "EXTREMELY_COOL_SHOPS_SOMETIMES"] }
    };

    // i don't know how to calculate hex positions
    // but this is probably better in terms of performance anyway
    internal static Vector3Int[] orderToPos = [
        new(0, 0, 0),
        new(0, 1, -1),
        new(1, 0, -1),
        new(1, -1, 0),
        new(0, -1, 1),
        new(-1, 0, 1),
        new(-1, 1, 0),
        new(0, 2, -2),
        new(1, 1, -2),
        new(2, 0, -2),
        new(2, -1, -1),
        new(2, -2, 0),
        new(1, -2, 1),
        new(0, -2, 2),
        new(-1, -1, 2),
        new(-2, 0, 2),
        new(-2, 1, 1),
        new(-2, 2, 0),
        new(-1, 2, -1),
        new(0, 3, -3),
        new(1, 2, -3),
        new(2, 1, -3),
        new(3, 0, -3),
        new(3, -1, -2),
        new(3, -2, -1),
        new(3, -3, 0),
        new(2, -3, 1),
        new(1, -3, 2),
        new(0, -3, 3),
        new(-1, -2, 3),
        new(-2, -1, 3),
        new(-3, 0, 3),
        new(-3, 1, 2),
        new(-3, 2, 1),
        new(-3, 3, 0),
        new(-2, 3, -1),
        new(-1, 3, -2),
        new(0, 4, -4),
        new(1, 3, -4),
        new(2, 2, -4),
        new(3, 1, -4),
        new(4, 0, -4),
        new(4, -1, -3),
        new(4, -2, -2),
        new(4, -3, -1),
        new(4, -4, 0),
        new(3, -4, 1),
        new(2, -4, 2),
        new(1, -4, 3),
        new(0, -4, 4),
        new(-1, -3, 4),
        new(-2, -2, 4),
        new(-3, -1, 4),
        new(-4, 0, 4),
        new(-4, 1, 3),
        new(-4, 2, 2),
        new(-4, 3, 1),
        new(-4, 4, 0),
        new(-3, 4, -1),
        new(-2, 4, -2),
        new(-1, 4, -3),
        new(0, 5, -5),
        new(1, 4, -5),
        new(2, 3, -5),
        new(3, 2, -5),
        new(4, 1, -5),
        new(5, 0, -5),
        new(5, -1, -4),
        new(5, -2, -3),
        new(5, -3, -2),
        new(5, -4, -1),
        new(5, -5, 0),
        new(4, -5, 1),
        new(3, -5, 2),
        new(2, -5, 3),
        new(1, -5, 4),
        new(0, -5, 5),
        new(-1, -4, 5),
        new(-2, -3, 5),
        new(-3, -2, 5),
        new(-4, -1, 5),
        new(-5, 0, 5),
        new(-5, 1, 4),
        new(-5, 2, 3),
        new(-5, 3, 2),
        new(-5, 4, 1),
        new(-5, 5, 0),
        new(-4, 5, -1),
        new(-3, 5, -2),
        new(-2, 5, -3),
        new(-1, 5, -4),
        new(0, 6, -6),
        new(1, 5, -6),
        new(2, 4, -6),
        new(3, 3, -6),
        new(4, 2, -6),
        new(5, 1, -6),
        new(6, 0, -6),
        new(6, -1, -5),
        new(6, -2, -4),
        new(6, -3, -3),
        new(6, -4, -2),
        new(6, -5, -1),
        new(6, -6, 0),
        new(5, -6, 1),
        new(4, -6, 2),
        new(3, -6, 3),
        new(2, -6, 4),
        new(1, -6, 5),
        new(0, -6, 6),
        new(-1, -5, 6),
        new(-2, -4, 6),
        new(-3, -3, 6),
        new(-4, -2, 6),
        new(-5, -1, 6),
        new(-6, 0, 6),
        new(-6, 1, 5),
        new(-6, 2, 4),
        new(-6, 3, 3),
        new(-6, 4, 2),
        new(-6, 5, 1),
        new(-6, 6, 0),
        new(-5, 6, -1),
        new(-4, 6, -2),
        new(3, 6, -9),
        new(-2, 6, -4),
        new(-1, 6, -5),
        new(0, 7, -7),
        new(1, 6, -7),
        new(2, 5, -7),
        new(3, 4, -7),
        new(4, 3, -7),
        new(5, 2, -7),
        new(6, 1, -7),
        new(7, 0, -7),
        new(7, -1, -6),
        new(7, -2, -5),
        new(7, -3, -4),
        new(7, -4, -3),
        new(7, -5, -2),
        new(7, -6, -1),
        new(7, -7, 0),
        new(6, -7, 1),
        new(5, -7, 2),
        new(4, -7, 3),
        new(3, -7, 4),
        new(2, -7, 5),
        new(1, -7, 6),
        new(0, -7, 7),
        new(-1, -6, 7),
        new(-2, -5, 7),
        new(-3, -4, 7),
        new(-4, -3, 7),
        new(-5, -2, 7),
        new(-6, -1, 7),
        new(-7, 0, 7),
        new(-7, 1, 6),
        new(-7, 2, 5),
        new(-7, 3, 4),
        new(-7, 4, 3),
        new(-7, 5, 2),
        new(-7, 6, 1),
        new(-7, 7, 0),
        new(-6, 7, -1),
        new(-5, 7, -2),
        new(-4, 7, -3),
        new(-3, 7, -4),
        new(-2, 7, -5),
        new(-1, 7, -6)
    ];

    // map skill names to their serialized node data, used below
    static void GenerateDictionary()
    {
        skillNameToNode = [];
        foreach (var _map in Simpleton<SkillManager>.i.skillMapPrefabs)
        {
            var map = _map.GetComponent<SkillMap>();
            foreach (var node in map.nodes)
            {
                var name = regexSkillName.Replace(node.name, "");
                skillNameToNode[name] = node.Serialize();
            }
        }
    }

    static internal bool GetNodeByName(string name, out SerializableSkillNode node)
    {
        if (skillNameToNode == null) GenerateDictionary();
        if (skillNameToNode.TryGetValue(name, out node)) return true;
        string err = $"Unable to find skill node \"{name}\".";
        Logger.LogFatal(err);
        return false;
    }

    // maps feat names to their shop upgrade features, used below
    static internal void GenerateFeatureMap()
    {
        featureMap = [];
        foreach (var feature in Simpleton<DataManager>.i.features)
        {
            var feat = feature;
            feat.cost = 0;
            featureMap[feature.constName] = feat;
        }
    }

    static internal bool GetFeature(ItemInfo item, out FeatureData.Feature feature)
    {
        feature = default;
        var name = item.ItemName;
        if (featureMap == null) GenerateFeatureMap();
        if (name.StartsWith("PROGRESSIVE")) if (progressiveMap.TryGetValue(name, out var names))
        {
            int count = Client.Instance.inventory.Count(item.ItemId) - 1;
            if (count < 0) return false;
            if (count >= names.Length) count = names.Length - 1;
            name = names[count];
        }
        if (featureMap.TryGetValue(name, out feature)) return true;
        return false;
    }

    // used to prescout the shop
    static internal long[] GetAllShopLocations(Archipelago.MultiClient.Net.ArchipelagoSession session)
    {
        if (featureMap == null) GenerateFeatureMap();
        List<long> result = [];
        foreach (var key in featureMap.Keys)
            result.Add(session.Locations.GetLocationIdFromName("Q-UP", key));
        return [.. result];
    }
}
