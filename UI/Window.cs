using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    const int TabCurrent = 0, TabHistory = 1, TabTotal = 2, TabBuild = 3, TabRecords = 4, TabArrange = 5;
    static readonly string[] Tabs = { Tr("window.current"), Tr("window.history"), Tr("window.total"), Tr("window.build"), Tr("window.records"), Tr("window.arrange") };
    static readonly string[] RecordViews = { Tr("common.damage"), Tr("window.build"), Tr("window.floors") };
    const float WindowWidth = 600f;
    const float ScrollHeight = 480f;
    const float Indent = 28f;

    const float MiniWidth = 330f;
    const float MinScale = 0.5f, MaxScale = 2f;

    bool visible = true, expandAll;
    int tab;
    int historyIndex = -1;
    int recordView, recordFloor;
    readonly HashSet<uint> expanded = new();
    readonly HashSet<string> collapsed = new();
    Rect windowRect = new Rect(20, 150, WindowWidth, 0);
    Vector2 scroll;
    List<Row> currentRows = new(), totalRows = new();
    readonly Dictionary<int, string> rarityColors = new();
    bool stylesReady;
    GUIStyle rightAlign, center, dim, rich, wrap, richWrap, playerName, detailName, lockOn;
    static readonly string[] DetailModes = { Tr("window.source"), Tr("window.type") };
    static readonly string[] FrequencyModes = { Tr("window.artifact_values_only"), Tr("window.blend_live_rates") };
    static readonly string[] SceneModes = { Tr("window.single_boss"), Tr("window.multi_target") };

    Action deferred;
    void Defer(Action a) => deferred += a;

    public static SephiriaToolbox Instance { get; private set; }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        try
        {
            if (System.IO.File.Exists(SettingsPath))
                settings = Newtonsoft.Json.JsonConvert.DeserializeObject<UiSettings>(System.IO.File.ReadAllText(SettingsPath)) ?? new UiSettings();
        }
        catch (Exception e) { WarnOnce("读取界面设置", e); }
        settings.scale = Mathf.Clamp(settings.scale, MinScale, MaxScale);
        windowRect.x = settings.x;
        windowRect.y = settings.y;
        tab = Mathf.Clamp(settings.tab, 0, Tabs.Length - 1);
    }

    void ChangeScale(float delta)
    {
        settings.scale = Mathf.Clamp(Mathf.Round((settings.scale + delta) * 10f) / 10f, MinScale, MaxScale);
        SaveSettings();
    }

    void ToggleMinimize()
    {
        settings.minimized = !settings.minimized;
        SaveSettings();
    }

    void ToggleLock()
    {
        settings.locked = !settings.locked;
        SaveSettings();
    }

    void SetTab(int t)
    {
        if (t == tab) return;
        tab = t;
        scroll = Vector2.zero;
        if (t == TabRecords) recordsDirty = true;
        settingsDirty = true;
    }

    void Page(int delta)
    {
        if (tab == TabRecords) PageRecords(delta);
        else PageHistory(delta);
    }

    void PageHistory(int delta)
    {
        var floors = history.Count > 0 ? history : previousRun?.Floors;
        if (floors == null || floors.Count == 0) return;
        int i = historyIndex < 0 ? floors.Count - 1 : historyIndex;
        i = Mathf.Clamp(i + delta, 0, floors.Count - 1);
        historyIndex = i == floors.Count - 1 ? -1 : i;
        SetTab(TabHistory);
    }

    void PageRecords(int delta)
    {
        if (recordFiles.Length == 0) return;
        int i = Mathf.Clamp(recordIndex + delta, 0, recordFiles.Length - 1);
        if (i == recordIndex) return;
        recordIndex = i;
        recordFloor = 0;
        scroll = Vector2.zero;
    }

    static string PageKeysHint => Application.platform == RuntimePlatform.OSXPlayer ? Tr("window.f5_f6_page") : Tr("window.f5_f6_pgup_pgdn");

    void OnGUI()
    {
        if (!visible) return;
        if (!stylesReady)
        {
            stylesReady = true;
            rightAlign = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
            center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            dim = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
            rich = new GUIStyle(GUI.skin.label) { richText = true };
            wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            richWrap = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };
            playerName = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1f, 0.9f, 0.55f) } };
            detailName = new GUIStyle(GUI.skin.label) { clipping = TextClipping.Clip, normal = { textColor = new Color(0.92f, 0.92f, 0.92f) } };
            lockOn = new GUIStyle(GUI.skin.button) { normal = { textColor = new Color(1f, 0.82f, 0.3f) }, hover = { textColor = new Color(1f, 0.9f, 0.5f) } };
        }
        var prevMatrix = GUI.matrix;
        try
        {
            float scale = Mathf.Max(1f, Screen.height / 1080f) * settings.scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            bool mini = settings.minimized;
            float width = mini ? MiniWidth : WindowWidth;
            string main = mini ? Tr("window.dps_f7_expand") : Tr("window.combat_stats_f7_minimize", PageKeysHint);
            string title = settings.locked ? Tr("window.locked", main) : main;
            var r = GUILayout.Window(0x5EF1D95, new Rect(windowRect.x, windowRect.y, width, 0f), DrawWindow, title, GUILayout.Width(width));
            float sw = Screen.width / scale, sh = Screen.height / scale;
            r.x = Mathf.Clamp(r.x, 0f, Mathf.Max(0f, sw - 80f));
            r.y = Mathf.Clamp(r.y, 0f, Mathf.Max(0f, sh - 40f));
            if (r.x != windowRect.x || r.y != windowRect.y)
            {
                settingsDirty = true;
                nextSettingsSave = Time.unscaledTime + 1f;
            }
            windowRect = r;
        }
        finally { GUI.matrix = prevMatrix; }
    }

    void DrawWindow(int id)
    {
        try
        {
            if (settings.minimized) DrawMini();
            else DrawFull();
        }
        catch (ExitGUIException) { throw; }
        catch (Exception e) { WarnOnce("界面", e); }
        if (!settings.locked) GUI.DragWindow();
    }

    void DrawFull()
    {
        GUILayout.BeginHorizontal();
        int t = GUILayout.Toolbar(tab, Tabs, GUILayout.Width(360));
        if (t != tab) Defer(() => SetTab(t));
        GUILayout.FlexibleSpace();
        DrawWindowButtons();
        GUILayout.EndHorizontal();
        switch (tab)
        {
            case TabCurrent: DrawCurrentTab(); break;
            case TabHistory: DrawHistoryTab(); break;
            case TabTotal: DrawTotalTab(); break;
            case TabBuild: DrawBuildTab(); break;
            case TabRecords: DrawRecordsTab(); break;
            case TabArrange: DrawArrangeTab(); break;
        }
    }

    void DrawWindowButtons()
    {
        if (GUILayout.Button("A-", GUILayout.Width(30))) Defer(() => ChangeScale(-0.1f));
        GUILayout.Label($"{Mathf.RoundToInt(settings.scale * 100)}%", center, GUILayout.Width(44));
        if (GUILayout.Button("A+", GUILayout.Width(30))) Defer(() => ChangeScale(+0.1f));
        if (UnityEngine.GUILayout.Button(NextLangLabel(), GUILayout.Width(34))) Defer(CycleUiLanguage);
        if (settings.locked ? GUILayout.Button(Tr("window.unlock"), lockOn, GUILayout.Width(50)) : GUILayout.Button(Tr("window.lock"), GUILayout.Width(50))) Defer(ToggleLock);
        if (GUILayout.Button(settings.minimized ? "□" : "—", GUILayout.Width(28))) Defer(ToggleMinimize);
    }

    void DrawMini()
    {
        bool inFloor = floor != null && !runEnded;
        var rows = inFloor ? currentRows : totalRows;
        GUILayout.BeginHorizontal();
        GUILayout.Label(inFloor ? Tr("window.floor") : Tr("window.run"), dim);
        GUILayout.FlexibleSpace();
        DrawWindowButtons();
        GUILayout.EndHorizontal();
        float team = rows.Sum(r => r.Damage);
        if (team <= 0f)
        {
            GUILayout.Label(Tr("window.no_damage_data_yet"), dim);
            return;
        }
        foreach (var r in rows)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(r.Name, GUILayout.Width(120));
            GUILayout.Label((inFloor ? r.LiveDps : r.Dps).ToString("N0") + "/s", rightAlign, GUILayout.Width(80));
            GUILayout.Label(r.Damage.ToString("N0"), rightAlign, GUILayout.Width(70));
            GUILayout.Label((r.Damage / team).ToString("P0"), rightAlign, GUILayout.Width(40));
            GUILayout.EndHorizontal();
        }
    }
}
