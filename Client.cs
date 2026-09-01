using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using BepInEx.Logging;
using cxve.qap.Patches;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TastyTools;
using UnityEngine;

namespace cxve.qap;

internal class Client
{
    ManualLogSource Logger { get => Plugin.Logger; }

    internal static Client Instance { get; private set; }

    internal static Client CreateClient()
    {
        Instance ??= new Client();

        // force turbo mode enabled
        // might change this to add turbo mode to the item pool
        // or maybe add an option for it
        SaveManager.globalData.permaTurboEnabled = true;
        SaveManager.SaveGlobalData();

        return Instance;
    }

    internal struct SlotData
    {
        internal int champ;
        internal int goal;
        internal int itemPoolEfficiencyUpgradePoints;
        internal int itemPoolEfficiencyCrystals;
        internal int itemPoolEfficiencyCorruptionShards;
        internal string version;
        internal byte[] fixedSkillPos;
    }

    internal struct ImplicitSlotData
    {
        internal int[] sanityNumChallenges;
        internal int sanityNumTriggerCombo;
    }
    internal SlotData slotData;
    internal ImplicitSlotData slotDataImplicit;
    float lastSFX = 0;

    ArchipelagoSession session;

    // this is the mod's save file, similar to the game's globaldata.json
    Config.Save __save;
    internal Config.Save Save
    {
        get => __save ??= Config.Get<Config.Save>(); set
        {
            __save = value;
            Config.Get(__save = value);
        }
    }

    // this is to read and write data for the connected slot
    internal Config.Data SaveData
    {
        get => Save.data[connected_slot.file]; set
        {
            var save = Save;
            save.data[connected_slot.file] = value;
            save.slots.First(x => x.file == connected_slot.file).UpdateLastPlayed();
            Save = save;
        }
    }

    // this is to read all available slots, for the UI to render
    internal List<Config.Slot> Slots
    {
        get => Save.slots; set
        {
            var save = Save;
            save.slots = value;
            Save = save;
        }
    }

    Config.Slot connected_slot;
    internal Inventory inventory;
    bool isClosing = false; // true if the client is closing the connection

    internal bool Connect(Config.Slot slot)
    {
        Logger.LogInfo($"Connecting to {slot.slot}@{slot.host}:{slot.port}{(slot.pass != "" ? " using a password" : "")}...");
        session = ArchipelagoSessionFactory.CreateSession(slot.host, slot.port);
        inventory = new();
        session.Items.ItemReceived += inventory.ItemReceived;
        session.Socket.ErrorReceived += Socket_ErrorReceived; // this is necessary because the socket closed event does not fire when the socket was not gracefully closed
        session.Socket.SocketClosed += Socket_SocketClosed;
        Application.quitting += Application_quitting;
        var result = session.TryConnectAndLogin("Q-UP", slot.slot, ItemsHandlingFlags.AllItems, password: slot.pass != "" ? slot.pass : null);
        if (!result.Successful)
        {
            Logger.LogError("Connection failed!");
            session.Socket.DisconnectAsync();
            session = null;
            foreach (var error in (result as LoginFailure).Errors)
            {
                Logger.LogError(error);
                UI.Instance.SetError(UI.GUI_ERROR.UNKNOWN, error);
            }
            return false;
        }
        else
        {
            Logger.LogInfo("Connection established!");
            var slotData = (result as LoginSuccessful).SlotData;
            var versionWorld = slotData["version"].ToString();
            int majorMod = int.Parse(MyPluginInfo.PLUGIN_VERSION.Split('.')[0]),
                majorWorld = int.Parse(versionWorld.Split('.')[0]);

            if (majorMod != majorWorld)
            {
                Logger.LogFatal($"The mod you are using is not compatible with this world!\n\n" +
                    $"Your mod version: v{MyPluginInfo.PLUGIN_VERSION}\n" +
                    $"World Version: v{versionWorld}\n\n" +
                    $"The first number of both versions must match!\n" +
                    $"Either {(majorMod < majorWorld ? "upgrade" : "downgrade")} your mod from v{majorMod}.x.x to v{majorWorld}.x.x " +
                    $"or {(majorWorld < majorMod ? "upgrade" : "downgrade")} the world from v{majorWorld}.x.x to v{majorMod}.x.x");
                session.Socket.DisconnectAsync();
                session = null;
                UI.Instance.SetError(UI.GUI_ERROR.INCOMPATIBLE, versionWorld);
                return false;
            }
            connected_slot = slot;
            this.slotData = new SlotData()
            {
                champ = Convert.ToInt32(slotData["champ"]),
                goal = Convert.ToInt32(slotData["goal"]),

                itemPoolEfficiencyUpgradePoints = Convert.ToInt32(slotData["itemPoolEfficiencyUpgradePoints"]),
                itemPoolEfficiencyCrystals = Convert.ToInt32(slotData["itemPoolEfficiencyCrystals"]),
                itemPoolEfficiencyCorruptionShards = Convert.ToInt32(slotData["itemPoolEfficiencyCorruptionShards"]),

                version = versionWorld,
                fixedSkillPos = [.. slotData["fixedSkillPos"].ToString().Split(",").Select(i => Convert.ToByte(i))]
            };

            RegisterConsole();

            // this returns true, if the save file has not been created yet
            // in that case, no need to precheck challenge locations
            if (connected_slot.file != "")
            {
                for (int tier = 1; tier < 5; ++tier)
                    for (int i = 0; i < 10; ++i)
                        if (SaveData.locations.Contains(1_000_000 + 300 + (tier - 1) * 10 + i))
                            inventory.ChallengeCheck(tier);
                session.Locations.CompleteLocationChecksAsync(SaveData.locations.ToArray());
            }
            return true;
        }
    }

    void RegisterConsole()
    {
        ConsoleCommandsRepository instance = ConsoleCommandsRepository.Instance;
        string SessionRelay(string cmd, string[] args, bool skipCommand = false)
        {
            session.Say($"{(skipCommand ? "" : $"{cmd} ")}{string.Join(' ', args)}");
            return "";
        }
        void RegisterCommand(string cmd, string desc, string syntax = "", bool skipCommand = false) => instance.RegisterCommand(cmd, (args) => SessionRelay(cmd, args, skipCommand), desc, syntax == "" ? cmd : syntax);

        string Help(string[] args)
        {
            string result = "To send a message or command to the Archipelago server, use <color=yellow>/say <message>.</color>\n" +
                "For example: <color=yellow>/say !hint ITEM_SHOP</color>\n" +
                "The following commands can be used directly, without using <color=yellow>/say</color>:\n";
            foreach (var command in instance.GetCommands().Where(x => x.name.StartsWith("!")))
                result += $"{command.name} ";
            return result;
        }

        RegisterCommand("!help", "Returns the help listing");
        RegisterCommand("!license", "Returns the license information");
        RegisterCommand("!options", "List all current options. Warning: lists password.");
        RegisterCommand("!admin", "Allow remote administration of the multiworld server, for further help, use !help", "!admin [command]");
        RegisterCommand("!players", "Get information about connected and missing players.");
        RegisterCommand("!status", "Get status information abour your team. Optionally mention a Tag name and get information on who has that Tag. For example: DeathLink or EnergyLink", "!status [tag]");
        RegisterCommand("!release", "Sends remaining items in your world to their recipients.");
        RegisterCommand("!collect", "Send your remaining items to yourself");
        RegisterCommand("!countdown", "Start a countdown in seconds", "!countdown seconds=10");
        RegisterCommand("!remaining", "List remaining items in your game, but not their location or recipient");
        RegisterCommand("!missing", "List all missing location checks from the server's perspective. Can be given text, which will be used as filter.", "!missing [filter_text]");
        RegisterCommand("!checked", "List all done location checks from the server's perspective. Can be given text, which will be used as filter.", "!checked [filter_text]");
        RegisterCommand("!alias", "Set your alias to the passed name.", "!alias [alias_name]");
        RegisterCommand("!getitem", "Cheat in an item, if it is enabled on this server", "!getitem item_name");
        RegisterCommand("!hint", "Use !hint {item_name}, for example !hint ITEM_SHOP to get a spoiler peek for that item. If hint costs are on, this will only give you one new result, you can rerun the command to get more in that case.", "!hint [item_name]");
        RegisterCommand("!hint_location", "Use !hint_location {location_name}, for example \"!hint_location Tier 4 Challenge 1\" to get a spoiler peek for that location.", "!hint_location [location]");
        RegisterCommand("/say", "Use this to send any text to the archipelago server, including commands!", "/say <content>", true);
        instance.RegisterCommand("/help", Help, "Explains how to send messages and commands to the Archipelago server.");

        void MessageReceived(LogMessage message)
        {
            string output = "";
            foreach (var part in message.Parts)
            {
                var color = part.Color;
                output += $"<color=#{color.R.ToString("X").PadLeft(2, '0')}{color.G.ToString("X").PadLeft(2, '0')}{color.B.ToString("X").PadLeft(2, '0')}>{part.Text}</color>";
            }
            ConsoleLog.Instance.Log(output);
        }

        session.MessageLog.OnMessageReceived += MessageReceived;
    }

    // required to prevent crashes
    private void Application_quitting() => isClosing = true;

    // disconnect and reset when the socket was closed
    private void Socket_ErrorReceived(Exception e, string message)
    {
        if (session == null || isClosing) return;
        Logger.LogError(message);
        Logger.LogError(e);
        ReversePatches.GoToMainMenu(Simpleton<SettingsManager>.i, true);
        DisconnectAndReset();
    }

    private void Socket_SocketClosed(string reason)
    {
        if (session == null || isClosing) return;
        Logger.LogError("Connection to archipelago lost, goodbye!");
        ReversePatches.GoToMainMenu(Simpleton<SettingsManager>.i, true);
        DisconnectAndReset();
    }

    internal List<RankData.Rank> rankBackup;

    internal void StartRun()
    {
        // AP world decides champion
        Logger.LogInfo($"Champion: {slotData.champ}");
        var champ = (ChampionType)Enum.Parse(typeof(ChampionType), slotData.champ.ToString());
        SaveManager.StartNewGame(champ);
        // force turbo speed, as it's currently not in the item pool anyway
        Simpleton<SettingsManager>.i.gameSpeedDropdown.value = 2;
        Simpleton<SettingsManager>.i.gameSpeedModifier = Simpleton<SettingsManager>.i.GetGameSpeedModifier(2);

        // create slot data for mod save file
        connected_slot.file = Simpleton<PlayerManager>.i.progressData.filename;
        var save = Save;
        save.slots.Add(connected_slot);
        save.data[connected_slot.file] = new();
        Save = save;

        PrepareRun();
    }

    internal void ContinueRun(Config.Slot slot)
    {
        // load game save
        var save = Save;
        var progressDatas = SaveManager.GetSavedProgressDatas();
        var progressData = progressDatas.First(x => x.filename == slot.file);
        // if player changed connection details before loading save, update them!
        save.slots.First(x => x.file == progressData.filename).UpdateConnectionDetails(slot);
        Save = save;
        SaveManager.LoadProgressData(progressData);
        PrepareRun();
    }

    // for example, turns "Cave" into "Cave's" or "James" into "James'"
    // if player name is connected player's name, turns it into "your" instead
    internal string FormatPossessiveName(string name) => $"{(name == connected_slot.slot ? "your" : $"{name}'{(name.ToLower().Last() == 's' ? "" : "s")}")}";

    internal void PrepareRun()
    {
        SaveManager.globalData.noviceGambler = SaveManager.globalData.noviceMedic = SaveManager.globalData.novicePro = SaveManager.globalData.noviceRobot =
            SaveManager.globalData.noviceStreamer = SaveManager.globalData.noviceTroll = SaveManager.globalData.noviceWhale = SaveManager.globalData.noviceWizard = false;

        Simpleton<ScreenManager>.i.InitializeAfterSaveSelected();

        // prevent unlocks
        var activeMap = Simpleton<SkillManager>.i.activeMap;
        activeMap.upgradePointLevelsExplicit = [];
        activeMap.upgradePointLevelsFiller = [];

        // reserve fixed skill positions
        foreach (byte idHex in slotData.fixedSkillPos)
        {
            var selectedNode = activeMap.GetNodeAtGridPosition(Data.orderToPos[idHex]);
            if (selectedNode) continue;
            if (!Data.GetNodeByName("Epic", out var node)) return;
            node.autoBuyLevel = 99;
            node.gridPosition = Data.orderToPos[idHex];
            var map = new SaveManager.SerializableSkillMap() { character = node.originalChar, nodes = [node] };
            Simpleton<HackerManager>.i.InitializeHackerNodeFromSerialized(activeMap, map, node);
        }

        // i am prescouting all shop items after establishing a connection
        // because it is easier to prefill the data than to change shop data retroactively
        // it is only stored in memory
        Data.GenerateFeatureMap();
        var task = session.Locations.ScoutLocationsAsync(HintCreationPolicy.None, Data.GetAllShopLocations(session));
        task.Wait();
        for (int i = 0; i < Simpleton<DataManager>.i.features.Count; ++i)
        {
            FeatureData.Feature feat = Simpleton<DataManager>.i.features[i];
            // change upgrade ids to make them recognizable by the mod and prevent unlocks by the game
            feat.id += 100;
            for (int j = 0; j < feat.prereqs.Count; ++j) feat.prereqs[j] += 100;
            var locid = session.Locations.GetLocationIdFromName("Q-UP", feat.constName);
            if (locid < 0 || !task.Result.ContainsKey(locid)) continue;
            var item = task.Result[locid];
            var player = item.Player.Name;
            feat.name = $"{FormatPossessiveName(player).Sanitize()} {item.ItemName.Sanitize()}";
            // create a fun description according to the item flags
            switch (item.Flags)
            {
                case ItemFlags.None: feat.description = "You have seen like ten of these already, looks boring."; break;
                case ItemFlags.Advancement: feat.description = "It's like a car, it takes you places."; break;
                case ItemFlags.NeverExclude: feat.description = "This looks interesting, maybe you should check it out."; break;
                case ItemFlags.Advancement | ItemFlags.NeverExclude: feat.description = "This is a true work of art, a miracle, you should definitely take it."; break;
                case ItemFlags.Trap: feat.description = "It looks like trash, smells like trash, it's probably trash."; break;
            }
            Simpleton<DataManager>.i.features[i] = feat;
        }

        // set crystal rewards to zero
        rankBackup = [.. Simpleton<DataManager>.i.ranksSO.ranks];
        for (int i = 0; i < Simpleton<DataManager>.i.ranksSO.ranks.Count; ++i)
        {
            var r = Simpleton<DataManager>.i.ranksSO.ranks[i];
            r.crystalEarned = 0;
            Simpleton<DataManager>.i.ranksSO.ranks[i] = r;
        }

        inventory.isReadyToReceiveItems = true;

        // send all unsent locations
        foreach (var location in SaveData.locations) QueueCheck(location);
        QueueSend();

        // get all locations already sent
        var saveData = SaveData;
        saveData.locations.AddRange(session.Locations.AllLocationsChecked);
        SaveData = saveData;

        // imply slot data from locations
        int[] numChallenges = [0, 0, 0, 0];
        for (int tier = 0; tier < 4; ++tier) numChallenges[tier] = session.Locations.AllLocations.Count(x => x >= 1_000_300 + tier * 10 && x < 1_000_300 + (tier + 1) * 10);
        slotDataImplicit = new()
        {
            sanityNumChallenges = numChallenges,
            sanityNumTriggerCombo = session.Locations.AllLocations.Count(x => x >= 1_001_000 && x < 1_001_300)
        };

        // resume base game logic
        Simpleton<AudioManager>.i.CrossfadeToMenu(0.5f);
        Simpleton<ScreenManager>.i.GoToLobby();
        //Simpleton<NavManager>.i.navButtons[2].requiresLevel2 = false;
        Simpleton<NavManager>.i.RefreshButtonVisibility();

        Logger.LogInfo("Finished preparing run, have fun!");
    }

    // this is different from the prescouting above
    // this rescouts items once they are visible in shop, actually creating a hint for the sending and receiving player
    internal void CreateHintsForShop()
    {
        FeatureWidgetEntry[] componentsInChildren = Simpleton<ShopManager>.i.unlockableFeaturesParent.GetComponentsInChildren<FeatureWidgetEntry>();
        List<long> ids = [];
        foreach (var location in componentsInChildren.Select(c => c.feature.constName))
        {
            var id = session.Locations.GetLocationIdFromName("Q-UP", location);
            if (ids.Contains(id))
            {
                Logger.LogWarning($"Tried to add duplicate location \"{location}\", does this cause any issues?");
                continue;
            }
            ids.Add(id);
        }
        session.Locations.ScoutLocationsAsync(HintCreationPolicy.CreateAndAnnounceOnce, [.. ids]);
    }

    // makes use of the game's notification system
    // notifications are capped at 9 to prevent massive spam when creating a new save file on an existing world
    internal void SendNotification(string title, string body, string sound = "")
    {
        Simpleton<NavManager>.i.QueueNotification(title, body);
        if (Simpleton<NavManager>.i.notificationQueue.Count > 9) Simpleton<NavManager>.i.notificationQueue.RemoveAt(0);
        if (sound != "" && Simpleton<ScreenManager>.i.GetState() != ScreenManager.ScreenState.Match && Time.time - lastSFX > 1)
        {
            lastSFX = Time.time;
            AudioManager.SafePlayOneShot(sound);
        }
        Simpleton<NavManager>.i.RefreshButtonVisibility();
    }

    int cheatedItemCounter = 0;

    internal EmailData BuildMailData(Config.MailData data) => BuildMailData(data.item, data.itemName, data.context);
    internal EmailData BuildMailData(SerializableItemInfo item, string itemName, string context)
    {
        long idLocation = item.LocationId;
        if (idLocation < 0) idLocation = cheatedItemCounter++;
        return new()
        {
            Filename = $"Q-AP_{item.Player.Slot}_{idLocation}",
            Sender = $"{regexMail.Replace(item.Player.Alias, "")}@{regexMail.Replace(item.LocationGame, "")}.ap",
            Subject = $"{itemName.Sanitize()} found!",
            BodyContent = [ new() {
                Text = $"[[size=24]]Hey there {connected_slot.slot}!",
                ParagraphType = "p"
            }, new() {
                Text = $"[[size=24]]I found your [[b]]{itemName.Sanitize()}[[/b]] in [[b]]{item.LocationGame.Sanitize()}[[/b]] at [[b]]{item.LocationDisplayName.Sanitize()}[[/b]].",
                ParagraphType = "p"
            }, new() {
                Text = $"[[size=24]]{context.Sanitize()}",
                ParagraphType = "p"
            }, new() {
                Text = "You are welcome! :)",
                ParagraphType = "p"
            }, new() {
                Text = $"[[size=16]][[b]]{item.Player.Alias.Sanitize()}\r\n{item.LocationGame.Sanitize()} Player[[/b]] [[/size]]",
                ParagraphType = "p"
            }]
        };
    }

    static readonly Regex regexMail = new("[^\\w\\d_\\-]");
    int saveAttempt = 0;
    internal void SendMail(SerializableItemInfo item, string itemName, string context)
    {
        var data = BuildMailData(item, itemName, context);

        var saveData = SaveData;
        saveData.mail[data.Filename] = new(item, itemName, context);
        SaveData = saveData;

        Simpleton<EmailParser>.i.GetFieldValue<Dictionary<string, EmailData>>("emailDatabase").Add(data.Filename, data);
        SimpleScreen<MailManager>.i.triggeredEmails.Add(data.Filename);
        Simpleton<PlayerManager>.i.progressData.triggeredEmails = [.. SimpleScreen<MailManager>.i.triggeredEmails];
        SimpleScreen<MailManager>.i.AddMailToInbox(data);
        IEnumerator WaitThenSave()
        {
            int _saveAttempt = ++saveAttempt;
            yield return new WaitForSecondsRealtime(1);
            if (saveAttempt > _saveAttempt) yield break;
            SaveManager.SaveProgressData();
        }
        SimpleScreen<MailManager>.i.StartCoroutine(WaitThenSave());
    }

    internal bool IsThisMe(string sender) => sender == connected_slot.slot;

    // cache rewards for checks
    internal Dictionary<string, ScoutedItemInfo> checkRewards = [];

    // count the number of completed challenge checks present in the save file for a specific tier
    internal int CountChallengeChecks(int tier) => SaveData.locations.Count(x => x >= 1_000_300 + (tier - 1) * 10 && x < 1_000_300 + tier * 10);

    // generally count how many of a specific item was received
    internal int CountItemInSession(string name) => session.Items.AllItemsReceived.Count(x => x.ItemName == name);

    // this generates the number of challenge checks expected on this slot and compares them to the number of completed checks for a specific tier
    // the result of the generation could be cached, but this is probably not devastating in terms of performance
    internal bool IsChallengeAvailable(int tier) => CountChallengeChecks(tier) < slotDataImplicit.sanityNumChallenges[tier - 1];

    internal bool IsRecyclingSetAvailable(string set)
    {
        var id = Array.IndexOf(Data.recycling_sets, set);
        return !SaveData.locations.Contains(1_000_400 + id);
    }

    internal int NextTriggerComboCheck()
    {
        int count = SaveData.locations.Count(x => x >= 1_001_000 && x < 1_001_300);
        if (count >= slotDataImplicit.sanityNumTriggerCombo) return -1;
        return (count + 1) * 10;
    }

    // put upcoming checks into a queue. this was made to improve scouting reliability by only sending one scout request instead of 5+
    Dictionary<long, string> checkQ = new();
    internal void QueueCheck(string check) => QueueCheck(session.Locations.GetLocationIdFromName("Q-UP", check), check);

    internal void QueueCheck(long id, string check = null)
    {
        if (id < 0) Logger.LogError($"Cannot queue check \"{check ?? id.ToString()}\", check not found!");
        else
        {
            // save check in advance, checks get resent on reconnect if anything fails
            var data = SaveData;
            if (!data.locations.Contains(id))
            {
                data.locations.Add(id);
                SaveData = data;
            }
            checkQ[id] = check;
            Logger.LogDebug($"Successfully added \"{check ?? id.ToString()}\" to the queue.");
        }
    }

    internal Coroutine QueueSend()
    {
        session.Locations.CompleteLocationChecks([.. checkQ.Keys]);
        Logger.LogInfo($"Queue was submitted with {checkQ.Keys.Count} checks!");
        var task = session.Locations.ScoutLocationsAsync([.. checkQ.Keys]);
        IEnumerator Wait()
        {
            while (!task.IsCompleted) yield return new WaitForFixedUpdate();
            foreach (var result in task.Result)
            {
                // cache check rewards for later
                if (checkQ.TryGetValue(result.Key, out string name) && name != null)
                    checkRewards[name] = result.Value;
            }
            checkQ.Clear();
        }
        return Simpleton<ScreenManager>.i.StartCoroutine(Wait());
    }

    // send checks without any scouting!
    internal void SendCheck(string check)
    {
        Logger.LogInfo($"Send check \"{check}\"");
        long id = session.Locations.GetLocationIdFromName("Q-UP", check);
        if (id <= 0)
        {
            Logger.LogFatal($"Unable to send check \"{check}\", check not found!");
            return;
        }
        var data = SaveData;
        if (!data.locations.Contains(id))
        {
            data.locations.Add(id);
            SaveData = data;
        }
        session.Locations.CompleteLocationChecks(id);
    }

    internal void SendGoal()
    {
        session.SetGoalAchieved();
        Logger.LogInfo("Goal was sent!");
    }

    internal void ResetSaveCache() => __save = null;

    // the state should be returned to what it was before connecting
    internal void DisconnectAndReset()
    {
        isClosing = true;
        Logger.LogInfo("Disconnecting...");
        session.Socket.DisconnectAsync();
        Logger.LogInfo("Disconnected!");
        session = null;
        ResetSaveCache();
        cheatedItemCounter = 0;
        checkQ = new();
        // reset ui
        Plugin.StartWaitForInit();
        isClosing = false;
    }
}
