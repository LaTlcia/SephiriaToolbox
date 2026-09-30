using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorayAnalytics;
using Mirror;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static int R(float v) => Mathf.RoundToInt(v);

    static List<SourceRecord> ToSources(Row row) =>
        row.Types.Select(s => new SourceRecord { name = s.Name, damage = R(s.Damage), elem = s.Elem >= 0 ? s.Elem : null }).ToList();

    static List<SourceRecord> ToBySource(Row row) =>
        row.BySource?.Select(s => new SourceRecord { name = s.Name, damage = R(s.Damage), cat = s.Cat >= 0 ? s.Cat : null }).ToList();

    List<PlayerRecord> RecordPlayers()
    {
        var rows = BuildRows(cumulative, runCombatTime, live: false).ToDictionary(r => r.Id);
        var list = new List<PlayerRecord>();
        foreach (var id in playerOrder)
        {
            builds.TryGetValue(id, out var build);
            rows.TryGetValue(id, out var row);
            if (build == null && row == null) continue;
            var p = build != null ? build.Clone() : new PlayerRecord();
            if (names.TryGetValue(id, out var n)) p.name = n;
            if (row != null)
            {
                p.damage = R(row.Damage);
                p.dps = R(row.Dps);
                p.sources = ToSources(row);
                p.bySource = ToBySource(row);
            }
            list.Add(p);
        }
        return list;
    }

    void SaveRunLog()
    {
        if (history.Count == 0 && runResult == null) return;
        try
        {
            Directory.CreateDirectory(LogDir);
            float end = runEnded ? runEndTime : lastActiveTime;
            var rec = new RunRecord
            {
                startedAt = runStartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                endedAt = lastActiveAt.ToString("yyyy-MM-dd HH:mm:ss"),
                durationSeconds = R(Mathf.Max(0f, end - runStartTime)),
                combatSeconds = R(runCombatTime),
                result = runResult,
                hardMode = CaptureHardMode(),
                players = RecordPlayers(),
                floors = history.Select(f => new FloorRecord
                {
                    index = f.Index,
                    title = f.Title,
                    bossAffix = f.BossAffix,
                    defense = f.Defense,
                    durationSeconds = R(f.Duration(f.EndTime)),
                    combatSeconds = R(f.CombatTime),
                    players = BuildRows(f.Final, f.CombatTime, false, f.Names)
                        .Select(r => new DamageRecord { name = r.Name, damage = R(r.Damage), dps = R(r.Dps), sources = ToSources(r), bySource = ToBySource(r) })
                        .ToList()
                }).ToList(),
                official = official
            };
            string path = Path.Combine(LogDir, $"run_{runStartedAt:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(rec, JsonSettings));
            currentRunFile = path;
            recordsDirty = true;
        }
        catch (Exception e) { WarnOnce("保存记录", e); }
    }

    string[] recordFiles = Array.Empty<string>();
    bool recordsDirty = true;
    int recordIndex;
    string loadedPath, loadError;
    RunRecord loadedRecord;

    void RefreshRecords()
    {
        recordsDirty = false;
        loadedPath = null;
        string selected = recordFiles.Length > 0 ? recordFiles[Mathf.Clamp(recordIndex, 0, recordFiles.Length - 1)] : null;
        try
        {
            recordFiles = Directory.Exists(LogDir)
                ? Directory.GetFiles(LogDir, "run_*.json").OrderByDescending(Path.GetFileName, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
        }
        catch { recordFiles = Array.Empty<string>(); }
        int kept = selected != null ? Array.IndexOf(recordFiles, selected) : -1;
        recordIndex = kept >= 0 ? kept : Mathf.Clamp(recordIndex, 0, Mathf.Max(0, recordFiles.Length - 1));
    }

    RunRecord SelectedRecord()
    {
        if (recordFiles.Length == 0) return null;
        string path = recordFiles[Mathf.Clamp(recordIndex, 0, recordFiles.Length - 1)];
        if (path == loadedPath) return loadedRecord;
        loadedPath = path;
        loadedRecord = null;
        loadError = null;
        try
        {
            var rec = JsonConvert.DeserializeObject<RunRecord>(File.ReadAllText(path));
            if (rec.players == null && rec.total != null)
                rec.players = rec.total.Select(d => new PlayerRecord { name = d.name, damage = d.damage, dps = d.dps, sources = d.sources }).ToList();
            rec.players ??= new List<PlayerRecord>();
            rec.floors ??= new List<FloorRecord>();
            loadedRecord = rec;
        }
        catch (Exception e) { loadError = e.Message; }
        return loadedRecord;
    }

    static void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            Application.OpenURL(new Uri(LogDir + Path.DirectorySeparatorChar).AbsoluteUri);
        }
        catch (Exception e) { Debug.LogWarning("[SephiriaToolbox] 打开文件夹失败: " + e.Message); }
    }
}
