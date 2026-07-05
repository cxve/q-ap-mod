using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TastyTools;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace cxve.qap;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static bool isDebug { get; private set; }

    internal static new ManualLogSource Logger;
    internal static TMP_SpriteAsset APIcon { get; private set; }

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        // disable the mod entirely, should result in a vanilla experience
        if (Environment.GetCommandLineArgs().Contains("--cxve_ap_disable"))
        {
            Logger.LogWarning("--cxve_ap_disable argument found, disabling Q-AP mod...");
            return;
        }

        // automatically reset save files on startup
        if (Environment.GetCommandLineArgs().Contains("--cxve_ap_reset")) Debug.DeleteSavesAP();

        // enable the debug menu and change some game behavior
        if (Environment.GetCommandLineArgs().Contains("--cxve_ap_debug")) isDebug = true;

        Harmony harmony = new(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        StartWaitForInit();
    }

    // wait for the game's system to be initialized
    // before creating the client and UI
    static IEnumerator WaitForInit()
    {
        while (Singleton<SceneLoadManager>.i.isLoading || SceneManager.GetActiveScene().name != "main" || !Simpleton<DiscordManager>.i.isInitialized) yield return new WaitForSecondsRealtime(1);
        Client.CreateClient();
        UI.CreateUI();
    }

    internal static void StartWaitForInit() => Singleton<SceneLoadManager>.i.StartCoroutine(WaitForInit());

    void OnGUI()
    {
        if (UI.Instance == null) return;
        UI.Instance.OnGUI();
    }
}
