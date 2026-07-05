using BepInEx.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace cxve.qap;

/// <summary>
/// Class to access configuration files.
/// </summary>
static public class Config
{
    static ManualLogSource Logger { get => Plugin.Logger; }

    static string GetPath<T>() => Path.Combine(UnityEngine.Application.persistentDataPath, "AP_config", $"{typeof(T).Name}.json");

    /// <summary>
    /// Get object of specified configuration class.
    /// </summary>
    /// <typeparam name="T">Class representing the config file to load</typeparam>
    /// <param name="_path">Custom path to config file to read</param>
    /// <param name="obj">Provide object to overwrite file</param>
    /// <returns>Object containing the configured values</returns>
    public static T Get<T>(T obj = default, string path = null) where T : new()
    {
        // get path to config file
        path ??= GetPath<T>();

        // create config file if it doesn't exist
        CreateConfig(path, obj);

        // read config file
        string config = File.ReadAllText(path);
        Logger.LogInfo($"loading config file: {path}");
        T result = JsonConvert.DeserializeObject<T>(config, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })!;

        // format / add missing keys to config file
        Logger.LogInfo("adding missing keys");
        string presult = JsonConvert.SerializeObject(result, Formatting.Indented);

        // save changes
        File.WriteAllText(path, presult);
        return result;
    }

    /// <summary>
    /// Create / Overwrite config file.
    /// </summary>
    /// <typeparam name="T">Class representing the config file to create / overwrite</typeparam>
    /// <param name="path">Custom path to config file to write</param>
    /// <param name="obj">Provide object to overwrite file</param>
    public static void CreateConfig<T>(string path, T obj = default) where T : new()
    {
        // if path exists and object is null or default, done here
        if (File.Exists(path) && (obj == null || obj.Equals(default(T)))) return;
        Logger.LogWarning("overwriting / creating config file...");
        if (!Directory.Exists(Path.GetDirectoryName(path)))
        {
            Logger.LogWarning("config directory does not exist yet! creating...");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
        }
        // if object provided, use object values, otherwise use default values
        File.WriteAllText(path, JsonConvert.SerializeObject(obj ?? new T()));
    }

    /// <summary>
    /// Check if config file exists.
    /// </summary>
    /// <typeparam name="T">Class representing the config file to find</typeparam>
    /// <param name="path">Custom path to config file to write</param>
    /// <returns>True if config file exists, otherwise false</returns>
    public static bool Has<T>(string path = null)
    {
        path ??= GetPath<T>();
        return File.Exists(path);
    }

    /// <summary>
    /// Base for config file classes (might be redundant).
    /// </summary>
    public abstract class ConfigFile
    {
        [NonSerialized]
        public readonly string path;

        public ConfigFile() => path = Path.Combine(".", "config", $"{GetType().Name}.json");
    }

    // stores connection details and save file location
    public class Slot
    {
        public string name = "";
        public string host = "archipelago.gg";
        public ushort port = 38281;
        public string slot = "";
        public string pass = "";
        public string file = "";
        public DateTime lastPlayed = DateTime.Now;

        public Slot() { }

        public Slot(string name, string host, ushort port, string slot, string pass, string file)
        {
            this.name = name;
            this.host = host;
            this.port = port;
            this.slot = slot;
            this.pass = pass;
            this.file = file;
        }

        public void UpdateConnectionDetails(Slot slot) => UpdateConnectionDetails(slot.name, slot.host, slot.port, slot.slot, slot.pass);

        public void UpdateConnectionDetails(string name, string host, ushort port, string slot, string pass)
        {
            this.name = name;
            this.host = host;
            this.port = port;
            this.slot = slot;
            this.pass = pass;
        }

        public void UpdateLastPlayed() => lastPlayed = DateTime.Now;
    }

    // stores run data
    public class Data
    {
        public Dictionary<long, int> inventory = [];
        public List<long> locations = [];
    }

    // this is the global save file
    public class Save : ConfigFile
    {
        public List<Slot> slots = [];
        public Dictionary<string, Data> data = [];
    }
}