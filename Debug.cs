using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TastyTools;
using UnityEngine;
using static SaveManager;

namespace cxve.qap;

// this class includes useful methods for debugging the game
static internal class Debug
{
    static internal void DumpActiveMap() => File.WriteAllText("DEBUG_ACTIVE_MAP.JSON", JsonUtility.ToJson(Simpleton<SkillManager>.i.activeMap));

    static internal void DumpActiveMapNodes() => File.WriteAllText("DEBUG_ACTIVE_MAP_NODES.JSON", JsonUtility.ToJson(Simpleton<SkillManager>.i.activeMap.nodes));

    static internal void DumpShopFeatures() => File.WriteAllText("DEBUG_FEATURES.JSON", JsonConvert.SerializeObject(Simpleton<DataManager>.i.features));

    static internal void DumpMatchData() => File.WriteAllText("DEBUG_PROGRESSDATA_MATCH_DATA.JSON", JsonConvert.SerializeObject(Simpleton<MatchManager>.i.matchContext.localPlayer.ProgressData.data));

    static readonly string[] hypernodes = ["Jackpot", "Resurrection", "Queen", "Hoard", "A-List", "Hypercapitalist", "39_CATCH_FIRE", "Ψ"];

    static internal string DumpSkillData()
    {
        List<List<Dictionary<string, object>>> characters = new();
        foreach (var _map in Simpleton<SkillManager>.i.skillMapPrefabs)
        {
            List<Dictionary<string, object>> nodes = new();
            var map = _map.GetComponent<SkillMap>();
            foreach (var node in map.nodes.Where(x => !x.name.StartsWith("[Upgrade] ") && !hypernodes.Contains(x.name)))
            {
                nodes.Add(new()
                {
                    { "name", new Regex("^\\d{1,2}_").Replace(node.name, "") },
                    { "originalChar", node.map.character },
                    { "guid", node.GUID },
                    { "desc", node.GetDescription() },
                    { "autoBuyLevel", node.autoBuyLevel },
                    { "isInventory", node.isInventory }
                });
            }
            characters.Add(nodes);
        }
        File.WriteAllText("DEBUG_SERIALIZED_CHAMP_NODES.JSON", JsonConvert.SerializeObject(characters));
        return "Data dump complete!";
    }

    static internal void DumpRankSO()
    {
        List<string> dump = [];
        foreach (var rank in Simpleton<DataManager>.i.ranks) dump.Add(JsonUtility.ToJson(rank));
        File.WriteAllText("DEBUG_RANKS_SO.JSON", $"[{string.Join(",", dump)}]");
    }

    static internal void DumpSkillMapNodes()
    {
        List<string> dump = [];
        foreach (var prefab in Simpleton<SkillManager>.i.skillMapPrefabs.Select(x => x.GetComponent<SkillMap>())) dump.Add(JsonUtility.ToJson(prefab));
        File.WriteAllText("DEBUG_SKILL_MAP_PREFABS.JSON", $"[{string.Join(",", dump)}]");
    }

    static internal void TryAddSkillNode()
    {
        // test addition
        var snode = Simpleton<SkillManager>.i.skillMapPrefabs.Select(go => go.GetComponent<SkillMap>()).First(map => map.character == ChampionType.Medic).nodes.First(node => node.GUID == "cac308e32054449ac9fbb553956f9ed1").Serialize();
        var map = new SerializableSkillMap() { character = snode.originalChar, nodes = [snode] };
        Simpleton<HackerManager>.i.InitializeHackerNodeFromSerialized(Simpleton<SkillManager>.i.activeMap, map, snode);
    }

    static internal void DumpMails() => File.WriteAllText("DEBUG_MAILS.JSON", JsonConvert.SerializeObject(Simpleton<EmailParser>.i.GetAllEmails()));

    static internal void DumpUnlockedFeatures() => File.WriteAllText("DEBUG_UNLOCKED_FEATURES.JSON", JsonUtility.ToJson(Simpleton<PlayerManager>.i.progressData.unlockedFeatures));

    static internal void DeleteSavesAP()
    {
        Plugin.Logger.LogWarning("--cxve_ap_reset argument found, deleting AP files...");
        DeleteDirectory("AP_config");
        DeleteDirectory("AP_saves");
        //File.Delete(Path.Join(Application.persistentDataPath, "AP_globaldata.json"));
        //File.Delete(Path.Join(Application.persistentDataPath, "AP_globaldata.json.bak"));
    }

    static void DeleteDirectory(string path)
    {
        var fullpath = Path.Join(Application.persistentDataPath, path);
        if (Directory.Exists(fullpath)) Directory.Delete(fullpath, true);
    }

    static internal void RegisterCommands()
    {
        var instance = ConsoleCommandsRepository.Instance;
        instance.RegisterCommand("dump_skill_data", _ => DumpSkillData(), "Dump skill data to file...");
    }
}
