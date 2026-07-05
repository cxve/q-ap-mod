using System;
using System.IO;
using TastyTools;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace cxve.qap;

internal class UI
{
    internal static UI Instance { get; private set; }

    internal static void CreateUI() => Instance = new UI();

    bool isInit = false;

    UI() { }

    // prepare all the assets and styles before rendering the first time
    void Init()
    {
        if (isInit) return;
        isInit = true;

        // get the game's font
        var settings = GameObject.FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include);
        settings.gameObject.SetActive(true);
        var go = settings.transform.GetChild(1).transform.GetChild(1);
        var fontHeader = go.transform.GetChild(0).GetComponent<TextMeshProUGUI>().font.sourceFontFile;
        var fontRegular = go.transform.GetChild(2).transform.GetChild(2).transform.GetChild(0).GetComponent<TextMeshProUGUI>().font.sourceFontFile;
        settings.gameObject.SetActive(false);

        // copy that little triangle from the game's UI, this was such a waste of time, but it makes the UI look more consistent
        var tex = go.transform.GetChild(1).GetComponent<Image>().sprite.texture;
        triangle = new Texture2D(tex.width, tex.height);
        RenderTexture rtex = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Graphics.Blit(tex, rtex);
        var pretex = RenderTexture.active;
        RenderTexture.active = rtex;
        triangle.ReadPixels(new Rect(0, 0, rtex.width, rtex.height), 0, 0);
        triangle.Apply();
        var pixels = triangle.GetPixels32();
        for (int i = 0; i < pixels.Length; ++i) pixels[i] = new Color(0x58 / 255f, 0x4F / 255f, 0x45 / 255f, pixels[i].a);
        triangle.SetPixels32(pixels);
        triangle.Apply();
        RenderTexture.active = pretex;
        RenderTexture.ReleaseTemporary(rtex);

        // style for debug elements
        var guiBackgroundDebug = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
        guiBackgroundDebug.SetPixel(0, 0, new Color(0, 0, 0, 0.6f));
        guiBackgroundDebug.Apply();

        guiDebug = new();
        guiDebug.normal.background = guiBackgroundDebug;
        guiDebug.normal.textColor = Color.white;

        // style for persistent ui elements, previously used for tooltips (but i didn't manage to get them work)
        var guiBackgroundTooltip = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
        guiBackgroundTooltip.SetPixel(0, 0, new Color(0, 0, 0, 0.8f));
        guiBackgroundTooltip.Apply();

        guiTooltipText = new();
        guiTooltipText.normal.textColor = Color.white;
        guiTooltipText.font = fontHeader;
        guiTooltipText.alignment = TextAnchor.MiddleCenter;
        guiTooltip = new(guiTooltipText);
        guiTooltip.normal.background = guiBackgroundTooltip;

        // main UI style
        var guiBackgroundMain = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
        //this is what the color is supposed to be, but for some reason the color rendered in game is different
        //guiBackgroundMain.SetPixel(0, 0, new Color(0xE9 / 255f, 0x9D / 255f, 0x00 / 255f));
        // this color code renders as the color code above
        guiBackgroundMain.SetPixel(0, 0, new Color(0xD0 / 255f, 0x56 / 255f, 0x00 / 255f));
        guiBackgroundMain.Apply();

        guiMain = new();
        guiMain.normal.background = guiBackgroundMain;
        // but for some reason, this color is correct
        guiMain.normal.textColor = new Color(0x5A / 255f, 0x51 / 255f, 0x47 / 255f);
        guiMain.fontSize = 48;
        guiMain.font = fontHeader;

        guiMainText = new();
        guiMainText.fontSize = 28;
        guiMainText.font = fontRegular;
        guiMainText.alignment = TextAnchor.MiddleCenter;
        guiMainText.fixedHeight = 50;
        guiMainText.normal.textColor = Color.white;

        guiMainTextRight = new(guiMainText);
        guiMainTextRight.alignment = TextAnchor.MiddleRight;
        guiMainTextRight.contentOffset = new Vector2(-10, 0);

        // text field ui style
        var guiBackgroundTextField = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
        guiBackgroundTextField.SetPixel(0, 0, Color.white);
        guiBackgroundTextField.Apply();

        guiTextField = new(GUI.skin.textField);
        guiTextField.contentOffset = Vector2.zero;
        guiTextField.margin = new(0, 0, 0, 0);
        guiTextField.fontSize = 28;
        guiTextField.font = fontHeader;
        guiTextField.alignment = TextAnchor.MiddleLeft;
        guiTextField.fixedHeight = 50;
        guiTextField.contentOffset = new Vector2(10, 0);
        guiTextField.normal.textColor = guiMain.normal.textColor;
        guiTextField.normal.background = guiBackgroundTextField;

        // button ui style
        var guiBackgroundButton = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
        // again, this is what the color is supposed to be
        // guiBackgroundButton.SetPixel(0, 0, new Color(0x69 / 255f, 0x63 / 255f, 0x56 / 255f));
        // and this is what the color has to be to be somewhat rendered as the color above
        guiBackgroundButton.SetPixel(0, 0, new Color(0x29 / 255f, 0x21 / 255f, 0x17 / 255f));
        guiBackgroundButton.Apply();

        var guiBackgroundButtonHover = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
        guiBackgroundButtonHover.SetPixel(0, 0, new Color(0x39 / 255f, 0x31 / 255f, 0x27 / 255f));
        guiBackgroundButtonHover.Apply();

        var guiBackgroundButtonClick = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
        guiBackgroundButtonClick.SetPixel(0, 0, new Color(0x19 / 255f, 0x11 / 255f, 0x07 / 255f));
        guiBackgroundButtonClick.Apply();

        guiButton = new();
        guiButton.normal.background = guiBackgroundButton;
        guiButton.normal.textColor = guiButton.hover.textColor = guiButton.active.textColor = Color.white;
        guiButton.hover.background = guiBackgroundButtonHover;
        guiButton.active.background = guiBackgroundButtonClick;
        guiButton.alignment = TextAnchor.MiddleCenter;
        guiButton.fontSize = 28;
        guiButton.fixedHeight = 50;
        guiButton.font = fontRegular;

        guiButtonLeft = new GUIStyle(guiButton);
        guiButtonLeft.alignment = TextAnchor.MiddleLeft;

        // below buttons there is some kind of shading, this is how i replicated it
        var guiBackgroundButtonBelow = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
        // again, this is what the color is supposed to be
        // guiBackgroundButtonBelow.SetPixel(0, 0, new Color(0xAB / 255f, 0x72 / 255f, 0x00 / 255f));
        // and this is what the color has to be to be somewhat rendered as the color above
        guiBackgroundButtonBelow.SetPixel(0, 0, new Color(0x5A / 255f, 0x2A / 255f, 0x00 / 255f));
        guiBackgroundButtonBelow.Apply();

        guiButtonBelow = new(guiButton);
        guiButtonBelow.hover = new();
        guiButtonBelow.active = new();
        guiButtonBelow.normal.background = guiBackgroundButtonBelow;
        guiButtonBelow.fixedHeight = 4;
    }

    GUIStyle guiDebug;
    GUIStyle guiTooltip;
    GUIStyle guiTooltipText;
    GUIStyle guiMain;
    GUIStyle guiMainText;
    GUIStyle guiMainTextRight;
    GUIStyle guiTextField;
    GUIStyle guiButton;
    GUIStyle guiButtonLeft;
    GUIStyle guiButtonBelow;

    Texture2D triangle;

    Config.Slot slot;
    string inPort = "38281";

    enum GUI_STATE { INIT, START_RUN, CONTINUE_RUN, CONTINUE_RUN_CONNECT, CONNECTED }
    GUI_STATE gui_state = GUI_STATE.INIT;
    int delete = -1;
    int page = 0;
    int entriesPerPage = 5;

    // used to render a window in the game's ui style
    void BeginWindow(int width, int height)
    {
        int startX = (Screen.width - width) / 2, startY = (Screen.height - height) / 2;

        // draw border
        GUILayout.BeginArea(new Rect(startX - 9, startY - 9, width + 18, 4), guiMain);
        GUILayout.EndArea();
        GUILayout.BeginArea(new Rect(startX - 9, startY - 9, 4, height + 18), guiMain);
        GUILayout.EndArea();
        GUILayout.BeginArea(new Rect(startX + width + 5, startY - 9, 4, height + 18), guiMain);
        GUILayout.EndArea();
        GUILayout.BeginArea(new Rect(startX - 9, startY + height + 5, width + 18, 4), guiMain);
        GUILayout.EndArea();

        const int paddingX = 68;
        const int paddingY = 46;
        GUILayout.BeginArea(new Rect(startX, startY, width, height), guiMain);
        GUILayout.BeginArea(new Rect(4, height - 34 - 3, 33, 34));
        GUILayout.Box(triangle, new GUIStyle());
        GUILayout.EndArea();
        GUILayout.BeginArea(new Rect(paddingX / 2, paddingY / 2, width - paddingX, height - paddingY), guiMain);
    }

    // use at the end of the window
    void EndWindow()
    {
        GUILayout.EndArea();
        GUILayout.EndArea();
    }

    // q-ap main menu screen
    void GUI_Init(int width, int height)
    {
        if (Simpleton<SettingsManager>.i.isActiveAndEnabled) return; // hide when settings are opened

        BeginWindow(width, height);

        GUILayout.Label($"Q-UP ARCHIPELAGO v{MyPluginInfo.PLUGIN_VERSION}", guiMain);
        GUILayout.Space(10);
        if (GUILayout.Button("Start New Run", guiButton)) gui_state = GUI_STATE.START_RUN;
        GUILayout.Button("", guiButtonBelow);
        GUILayout.Space(28);

        var slots = Client.Instance.Slots;
        slots.Sort((a, b) => DateTime.Compare(a.lastPlayed, b.lastPlayed));
        slots.Reverse(); // show last played slots first

        // paginate slots
        for (int i = 0; i + (page * entriesPerPage) < slots.Count && i < entriesPerPage; ++i)
        {
            var slot = slots[i + (page * entriesPerPage)];
            void Delete()
            {
                delete = -1;
                File.Delete(Path.Join(Application.persistentDataPath, "AP_saves", slot.file));
                var save = Client.Instance.Save;
                save.data.Remove(slot.file);
                save.slots.Remove(slot);
                Client.Instance.Save = save;
            }

            // if no save file was found for a slot, delete the slot
            if (!File.Exists(Path.Combine(Application.persistentDataPath, "AP_saves", slot.file)))
            {
                Delete();
                continue;
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            var content = new GUIContent($"   Continue \"{slot.name}\"", $"{slot.lastPlayed}");
            if (GUILayout.Button(content, guiButtonLeft))
            {
                this.slot = slot;
                inPort = this.slot.port.ToString();
                gui_state = GUI_STATE.CONTINUE_RUN_CONNECT;
            }
            GUILayout.Button("", guiButtonBelow);
            GUILayout.EndVertical();
            GUILayout.Space(10);
            GUILayout.BeginVertical(GUILayout.MaxWidth(200));
            // click delete twice to delete slot
            if (delete != i)
            {
                if (GUILayout.Button($"Delete?", guiButton)) delete = i;
            }
            else if (GUILayout.Button($"You Sure?", guiButton)) Delete();
            GUILayout.Button("", guiButtonBelow);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        // render paginator
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.MinWidth(100));
        if (GUILayout.Button("<", guiButton) && page > 0) --page;
        GUILayout.Button("", guiButtonBelow);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.Label($"{page + 1}/{Mathf.Max(Mathf.CeilToInt(slots.Count / (float)entriesPerPage), 1)}", guiMainText);
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical(GUILayout.MinWidth(100));
        if (GUILayout.Button(">", guiButton) && page + 1 < Mathf.CeilToInt(slots.Count / (float)entriesPerPage)) ++page;
        GUILayout.Button("", guiButtonBelow);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.Space(12);

        EndWindow();
    }

    // connection detail screen, used for both new runs as well as continued runs
    void GUI_Login(string title)
    {
        BeginWindow(900, 600);

        GUILayout.Label(title, guiMain);
        GUILayout.FlexibleSpace();

        GUILayout.BeginVertical();
        GUILayout.Label("Give your run a memorable name:", guiMainText);
        slot.name = GUILayout.TextField(slot.name, guiTextField);
        GUILayout.Button("", guiButtonBelow);
        if (slot.name.Length > 32) slot.name = slot.name[..32];
        GUILayout.EndVertical();

        GUILayout.Space(20);

        void TextField(string name, ref string field, Action verify = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{name}:", guiMainTextRight, GUILayout.Width(100));
            GUILayout.BeginVertical();
            field = GUILayout.TextField(field, guiTextField);
            verify?.Invoke();
            GUILayout.Button("", guiButtonBelow);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        TextField("Host", ref slot.host);
        TextField("Port", ref inPort, () =>
        {
            if (inPort != "")
                if (ushort.TryParse(inPort, out var port)) slot.port = port;
                else inPort = slot.port.ToString();
        });
        TextField("Slot", ref slot.slot);
        TextField("Pass", ref slot.pass);

        // this is broken for some reason
        /*GUILayout.BeginHorizontal();
        GUILayout.Label("Pass: ", guiMainTextRight, GUILayout.Width(100));
        GUILayout.BeginVertical();
        slot.pass = GUILayout.PasswordField(slot.pass, '*', guiTextField);
        GUILayout.Button("", guiButtonBelow);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();*/

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        GUI.enabled = slot.host != "" && slot.name != "" && slot.slot != "";
        if (GUILayout.Button("Connect", guiButton) && Client.Instance.Connect(slot)) gui_state = GUI_STATE.CONNECTED; // signal state change
        GUILayout.Button("", guiButtonBelow);
        GUI.enabled = true;
        GUILayout.EndVertical();
        GUILayout.Space(10);
        GUILayout.BeginVertical();
        if (GUILayout.Button("Cancel", guiButton) && gui_state != GUI_STATE.CONNECTED) gui_state = GUI_STATE.INIT; // return to main menu
        GUILayout.Button("", guiButtonBelow);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.Space(12);

        EndWindow();
    }

    void GUI_Start_Run()
    {
        slot ??= new Config.Slot();
        GUI_Login("Start New Run");
        // this is true, when the connect button in GUI_Login was clicked
        if (gui_state == GUI_STATE.CONNECTED) Client.Instance.StartRun();
    }


    void GUI_Continue_Connect()
    {
        GUI_Login("Continue Run");
        // this is true, when the connect button in GUI_Login was clicked
        if (gui_state == GUI_STATE.CONNECTED) Client.Instance.ContinueRun(slot);
    }

    // used to show debug methods in UI
    internal void DebugUI()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 200 - 10, Screen.height - 150 - 10, 200, 150), guiDebug);

        GUILayout.BeginVertical();
        if (GUILayout.Button("DELETE AP SAVES")) Debug.DeleteSavesAP();
        if (GUILayout.Button("DUMP ACTIVE NODES")) Debug.DumpActiveMapNodes();
        //if (GUILayout.Button("DUMP PROGRESSDATA MATCH DATA")) Debug.DumpMatchData();
        //if (GUILayout.Button("DUMP SHOP FEATURES")) Debug.DumpShopFeatures();
        //if (GUILayout.Button("DUMP SKILL DATA")) Debug.DumpSkillData();
        //if (GUILayout.Button("DUMP RANKS SO")) Debug.DumpRankSO();
        //if (GUILayout.Button("DUMP SKILL MAP PREFABS")) Debug.DumpSkillMapNodes();
        GUILayout.EndVertical();

        GUILayout.EndArea();
    }

    // displays Q-AP version on the bottom right corner while connected
    void GUI_Connected()
    {
        string text = $"Connected to {slot.slot}@{slot.host}:{slot.port}";
        var size = guiTooltip.CalcSize(new(text));
        if (Plugin.isDebug)
        {
            GUILayout.BeginArea(new Rect(10, Screen.height - size.y - 24, size.x + 30, size.y + 12), guiTooltip);
            GUILayout.FlexibleSpace();
            GUILayout.Label(text, guiTooltipText);
            GUILayout.Space(2);
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        text = $"Q-AP v{MyPluginInfo.PLUGIN_VERSION}";
        size = guiTooltip.CalcSize(new(text));
        GUILayout.BeginArea(new Rect(Screen.width - size.x - 40, Screen.height - size.y - 24, size.x + 30, size.y + 12), guiTooltip);
        GUILayout.FlexibleSpace();
        GUILayout.Label(text, guiTooltipText);
        GUILayout.Space(2);
        GUILayout.FlexibleSpace();
        GUILayout.EndArea();
    }

    internal void OnGUI()
    {
        // wait for dependencies to init
        if (Singleton<SceneLoadManager>.i.isLoading || SceneManager.GetActiveScene().name != "main" || Simpleton<SplashManager>.i.currentState != SplashManager.SplashMovieState.Ended) return;

        Init(); // init once after dependencies are init

        // show relevant ui
        switch (gui_state)
        {
            case GUI_STATE.INIT: GUI_Init(900, 600); break;
            case GUI_STATE.START_RUN: GUI_Start_Run(); break;
            case GUI_STATE.CONTINUE_RUN_CONNECT: GUI_Continue_Connect(); break;
            case GUI_STATE.CONNECTED: GUI_Connected(); break;
        }

        if (Plugin.isDebug) DebugUI();
    }
}
