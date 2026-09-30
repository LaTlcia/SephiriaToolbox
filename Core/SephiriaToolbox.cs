using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class SephiriaToolboxMod : HorayModBase
{
    protected override void OnModLoaded()
    {
        if (SephiriaToolbox.Instance != null) return;
        var go = new GameObject("SephiriaToolbox");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<SephiriaToolbox>();
    }
}

public partial class SephiriaToolbox : MonoBehaviour
{
    const float SampleInterval = 0.25f;
    const float LiveWindow = 5f;
    const float CombatGrace = 2f;
    const float BuildInterval = 2f;
    const int TopDetails = 10;

    class Segment
    {
        public string Title = "";
        public string BossAffix;
        public int? Defense;
        public int Index;
        public float StartTime, EndTime = -1f, CombatTime;
        public readonly Dictionary<uint, Dictionary<DamageKey, float>> Baseline = new();
        public readonly Dictionary<uint, Dictionary<DamageKey, float>> Final = new();
        public readonly Dictionary<uint, string> Names = new();
        public float Duration(float now) => (EndTime >= 0f ? EndTime : now) - StartTime;
    }

    class RunData
    {
        public List<Segment> Floors;
        public Dictionary<uint, Dictionary<DamageKey, float>> Totals;
        public Dictionary<uint, string> Names;
        public float CombatTime, Duration;
    }

    struct Detail
    {
        public string Name;
        public float Damage;
        public int Elem, Cat;
    }

    class Row
    {
        public uint Id;
        public string Name;
        public float Damage, Dps, LiveDps;
        public List<Detail> Types, BySource;
    }

    readonly Dictionary<uint, Dictionary<DamageKey, float>> cumulative = new();
    readonly Dictionary<uint, string> names = new();
    readonly Dictionary<uint, Queue<(float t, float total)>> liveWindow = new();
    readonly List<Segment> history = new();
    readonly Dictionary<DamageKey, string> sourceNameCache = new();
    readonly Dictionary<uint, PlayerRecord> builds = new();
    readonly List<uint> playerOrder = new();
    readonly HashSet<string> warned = new();
    Segment floor;
    RunData previousRun;
    RunResult runResult;
    JObject official;
    bool runEnded;
    float runStartTime, runEndTime, runCombatTime, lastDamageTime = -999f, lastSampleTime, lastTeamTotal, lastPlayedTime, lastActiveTime;
    DateTime runStartedAt = DateTime.Now, lastActiveAt = DateTime.Now;
    string lastFloorGuid;
    int floorCounter;
    float nextSample, nextBuildRefresh;

    UI_GameOverLabel gameOverLabel;
    float nextLabelSearch;
    bool gameOverWasOpen;

    void Update()
    {
        var pending = deferred;
        deferred = null;
        pending?.Invoke();

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.f7Key.wasPressedThisFrame) ToggleMinimize();
            if (kb.f8Key.wasPressedThisFrame) visible = !visible;
            if (kb.f9Key.wasPressedThisFrame) SetTab((tab + 1) % Tabs.Length);
            if (kb.f10Key.wasPressedThisFrame) expandAll = !expandAll;
            if (kb.f5Key.wasPressedThisFrame || kb.pageUpKey.wasPressedThisFrame) Page(-1);
            if (kb.f6Key.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame) Page(+1);
        }
        if (visible && tab == TabRecords && recordsDirty) RefreshRecords();
        SaveSettingsIfDirty();
        UpdateUiLanguage();
        UpdateArrange();
        try { TrackSwings(); } catch (Exception e) { WarnOnce("出手速度", e); }
        try { TrackSpecials(); } catch (Exception e) { WarnOnce("特殊攻击次数", e); }
        try { TrackDefends(); TrackGuardHold(); } catch (Exception e) { WarnOnce("格挡 / 弹反", e); }
        try { TrackBosses(); } catch (Exception e) { WarnOnce("Boss 战", e); }
        try { TrackBattles(); } catch (Exception e) { WarnOnce("战斗长度", e); }
        try { TrackEnemies(); } catch (Exception e) { WarnOnce("身边敌人数", e); }

        if (Time.unscaledTime < nextSample) return;
        nextSample = Time.unscaledTime + SampleInterval;
        try { Sample(); } catch (Exception e) { WarnOnce("采样", e); }
    }

    void OnApplicationQuit()
    {
        SaveSettings();
        if (runEnded) return;
        FinishFloor(lastActiveTime > 0f ? lastActiveTime : Time.unscaledTime);
        SaveRunLog();
    }

    void Sample()
    {
        float now = Time.unscaledTime;
        var players = PlayerSpawner.MultiplayerList;
        if (players == null || players.Count == 0) return;

        var dm = DungeonManager.Instance;
        float played = dm ? dm.playedRealtimeClientside : 0f;
        bool newRun = played + 1f < lastPlayedTime;
        lastPlayedTime = played;

        PlayerAvatar local = null;
        var fresh = new Dictionary<uint, Dictionary<DamageKey, float>>();
        foreach (var spawner in players)
        {
            var avatar = spawner ? spawner.PlayerAvatar : null;
            if (!avatar) continue;
            uint id = avatar.netId;
            var snap = new Dictionary<DamageKey, float>();
            foreach (var kv in avatar.dealsStatistics) snap[kv.Key] = kv.Value;
            fresh[id] = snap;
            if (cumulative.TryGetValue(id, out var prev) && snap.Values.Sum() + 1f < prev.Values.Sum()) newRun = true;
            names[id] = string.IsNullOrEmpty(avatar.Name) ? "Player " + id : avatar.Name;
            if (spawner.isLocalPlayer || avatar.isLocalPlayer) local = avatar;
        }
        if (local == null) local = players.Where(s => s && s.PlayerAvatar).Select(s => s.PlayerAvatar).FirstOrDefault();

        if (newRun)
        {
            StartNewRun();
            liveWindow.Clear();
        }
        foreach (var id in fresh.Keys)
            if (!playerOrder.Contains(id)) playerOrder.Add(id);
        cumulative.Clear();
        float teamTotal = 0f;
        foreach (var kv in fresh)
        {
            cumulative[kv.Key] = kv.Value;
            float total = kv.Value.Values.Sum();
            teamTotal += total;
            if (!liveWindow.TryGetValue(kv.Key, out var q)) liveWindow[kv.Key] = q = new Queue<(float, float)>();
            q.Enqueue((now, total));
            while (q.Count > 1 && now - q.Peek().t > LiveWindow) q.Dequeue();
        }
        if (newRun) lastTeamTotal = teamTotal;
        lastActiveTime = now;
        lastActiveAt = DateTime.Now;

        if (runEnded)
        {
            totalRows = BuildRows(cumulative, runCombatTime, live: false);
            return;
        }

        if (now >= nextBuildRefresh)
        {
            nextBuildRefresh = now + BuildInterval;
            RefreshBuilds(players);
        }
        if (GameOverOpened())
        {
            EndRun(now, players, local);
            totalRows = BuildRows(cumulative, runCombatTime, live: false);
            return;
        }

        float dt = lastSampleTime > 0f ? now - lastSampleTime : 0f;
        lastSampleTime = now;
        if (teamTotal > lastTeamTotal + 0.5f) lastDamageTime = now;
        lastTeamTotal = teamTotal;
        if (now - lastDamageTime <= CombatGrace && floor != null)
        {
            floor.CombatTime += dt;
            runCombatTime += dt;
        }
        try { TrackLayout(local); } catch (Exception e) { WarnOnce("排列分段", e); }

        string guid = local ? local.currentFloorGuid : null;
        if (floor == null || guid != lastFloorGuid)
        {
            if (FinishFloor(now)) SaveRunLog();
            StartFloor(now, guid);
            lastFloorGuid = guid;
        }

        foreach (var id in cumulative.Keys)
            if (!floor.Baseline.ContainsKey(id)) floor.Baseline[id] = new Dictionary<DamageKey, float>(cumulative[id]);

        currentRows = BuildRows(FloorDamage(floor), floor.CombatTime, live: true);
        totalRows = BuildRows(cumulative, runCombatTime, live: false);
    }

    bool GameOverOpened()
    {
        if (!gameOverLabel && Time.unscaledTime >= nextLabelSearch)
        {
            nextLabelSearch = Time.unscaledTime + 1f;
            gameOverLabel = FindAnyObjectByType<UI_GameOverLabel>(FindObjectsInactive.Include);
        }
        bool open = gameOverLabel && gameOverLabel.IsOpened;
        bool opened = open && !gameOverWasOpen;
        gameOverWasOpen = open;
        return opened;
    }

    void EndRun(float now, List<PlayerSpawner> players, PlayerAvatar local)
    {
        RefreshBuilds(players);
        try { runResult = CaptureResult(local, players.Count); } catch (Exception e) { WarnOnce("结算信息", e); }
        try { if (runResult != null) official = BuildOfficial(players, runResult); } catch (Exception e) { WarnOnce("官方格式记录", e); }
        FinishFloor(now);
        runEndTime = now;
        runEnded = true;
        SaveRunLog();
    }

    void StartNewRun()
    {
        if (!runEnded)
        {
            float end = lastActiveTime > 0f ? lastActiveTime : Time.unscaledTime;
            FinishFloor(end);
            runEndTime = end;
            SaveRunLog();
        }
        if (history.Count > 0)
            previousRun = new RunData
            {
                Floors = new List<Segment>(history),
                Totals = cumulative.ToDictionary(kv => kv.Key, kv => new Dictionary<DamageKey, float>(kv.Value)),
                Names = new Dictionary<uint, string>(names),
                CombatTime = runCombatTime,
                Duration = runEndTime - runStartTime
            };
        history.Clear();
        historyIndex = -1;
        floor = null;
        floorCounter = 0;
        runCombatTime = 0f;
        lastFloorGuid = null;
        builds.Clear();
        playerOrder.Clear();
        runResult = null;
        official = null;
        runEnded = false;
        ClearSegments();
        ResetSwings(-1);
        ResetBattles();
        ResetEnemies();
        currentRunFile = null;
        nextBuildRefresh = 0f;
    }

    void StartFloor(float now, string guid)
    {
        if (floorCounter == 0)
        {
            runStartTime = now;
            runStartedAt = DateTime.Now;
            hardModeCache = null;
        }
        floor = new Segment { StartTime = now, Index = ++floorCounter, Title = FloorTitle(guid), BossAffix = FloorBossAffix(guid), Defense = FloorDefense(guid) };
        CaptureHardMode();
        foreach (var kv in cumulative) floor.Baseline[kv.Key] = new Dictionary<DamageKey, float>(kv.Value);
    }

    bool FinishFloor(float now)
    {
        if (floor == null) return false;
        floor.EndTime = now;
        foreach (var kv in FloorDamage(floor)) floor.Final[kv.Key] = kv.Value;
        foreach (var kv in names) floor.Names[kv.Key] = kv.Value;
        bool kept = floor.Final.Values.Sum(d => d.Values.Sum()) > 0f;
        if (kept) history.Add(floor);
        floor = null;
        return kept;
    }

    Dictionary<uint, Dictionary<DamageKey, float>> FloorDamage(Segment seg)
    {
        var result = new Dictionary<uint, Dictionary<DamageKey, float>>();
        foreach (var kv in cumulative)
        {
            seg.Baseline.TryGetValue(kv.Key, out var baseMap);
            var d = new Dictionary<DamageKey, float>();
            foreach (var s in kv.Value)
            {
                float b = baseMap != null && baseMap.TryGetValue(s.Key, out var v) ? v : 0f;
                if (s.Value - b > 0.01f) d[s.Key] = s.Value - b;
            }
            result[kv.Key] = d;
        }
        return result;
    }

    List<Row> BuildRows(Dictionary<uint, Dictionary<DamageKey, float>> damage, float combatTime, bool live, Dictionary<uint, string> nameMap = null)
    {
        nameMap ??= names;
        var rows = new List<Row>();
        foreach (var kv in damage)
        {
            float dmg = kv.Value.Values.Sum();
            float liveDps = 0f;
            if (live && liveWindow.TryGetValue(kv.Key, out var q) && q.Count > 1)
            {
                var first = q.Peek();
                float span = Time.unscaledTime - first.t;
                if (span > 0.5f) liveDps = (q.Last().total - first.total) / span;
            }
            rows.Add(new Row
            {
                Id = kv.Key,
                Name = nameMap.TryGetValue(kv.Key, out var n) ? n : "Player " + kv.Key,
                Damage = dmg,
                Dps = dmg / Mathf.Max(combatTime, 1f),
                LiveDps = liveDps,
                Types = kv.Value.OrderByDescending(s => s.Value)
                    .Select(s => new Detail { Name = SourceName(s.Key), Damage = s.Value, Elem = (int)s.Key.ElementalType, Cat = -1 }).ToList(),
                BySource = GroupBySource(kv.Key, kv.Value)
            });
        }
        return rows.OrderByDescending(r => r.Damage).ToList();
    }

    List<Detail> GroupBySource(uint player, Dictionary<DamageKey, float> damage)
    {
        var groups = new Dictionary<string, (SourceInfo info, float dmg)>();
        foreach (var s in damage)
        {
            var info = SourceFor(player, s.Key.Id);
            groups[info.Key] = groups.TryGetValue(info.Key, out var g) ? (g.info, g.dmg + s.Value) : (info, s.Value);
        }
        return groups.Values.OrderByDescending(g => g.dmg)
            .Select(g => new Detail { Name = g.info.Name, Damage = g.dmg, Elem = -1, Cat = (int)g.info.Cat }).ToList();
    }
}
