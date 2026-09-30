using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    void DrawRecordsTab()
    {
        if (recordFiles.Length == 0)
        {
            GUILayout.Label(Tr("records.no_records_yet_they"));
            if (GUILayout.Button(Tr("records.open_records_folder"), GUILayout.Width(140))) Defer(OpenLogFolder);
            return;
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Tr("records.newer"), GUILayout.Width(80))) Defer(() => PageRecords(-1));
        GUILayout.Label($"{recordIndex + 1} / {recordFiles.Length}", center);
        if (GUILayout.Button(Tr("records.older"), GUILayout.Width(80))) Defer(() => PageRecords(+1));
        if (GUILayout.Button(Tr("records.open_folder"), GUILayout.Width(100))) Defer(OpenLogFolder);
        GUILayout.EndHorizontal();

        var rec = SelectedRecord();
        if (rec == null)
        {
            GUILayout.Label(Tr("records.failed_read", loadError));
            return;
        }
        var r = rec.result;
        bool live = loadedPath == currentRunFile && !runEnded;
        string state = r != null ? r.endName : live ? Tr("records.progress") : Tr("records.unfinished_left_mid_run");
        GUILayout.Label(Tr("records.time_combat", rec.startedAt, state, Fmt(rec.durationSeconds), Fmt(rec.combatSeconds)));
        if (r != null)
        {
            string hard = r.hardMode > 0 ? Tr("records.hard_mode", r.hardMode) : "";
            string chapter = r.chapter > 0 ? Tr("records.chapter", r.chapter) : "";
            GUILayout.Label(Tr("records.ended_progress_player", chapter, r.stageName, r.progress, hard, r.playerCount), dim);
        }
        if (rec.hardMode != null && rec.hardMode.Count > 0)
            GUILayout.Label(Tr("records.hard_mode_modifiers", string.Join("　", rec.hardMode.Select(h => h.level > 1 ? $"{h.name} {h.level}" : h.name))), wrap);

        int v = GUILayout.Toolbar(recordView, RecordViews);
        if (v != recordView) Defer(() => { recordView = v; scroll = Vector2.zero; });
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(ScrollHeight));
        switch (recordView)
        {
            case 0:
                DrawTable(RecordRows(rec.players), Tr("common.total_damage"), null, showLive: false);
                break;
            case 1:
                if (rec.players.All(p => p.weapon == null && p.inventory.Count == 0))
                    GUILayout.Label(Tr("records.this_record_was_saved"), dim);
                else
                    for (int i = 0; i < rec.players.Count; i++) DrawBuild(rec.players[i], loadedPath + ":" + i, showDamage: true);
                break;
            case 2:
                DrawRecordFloors(rec);
                break;
        }
        GUILayout.EndScrollView();
    }

    void DrawRecordFloors(RunRecord rec)
    {
        if (rec.floors.Count == 0)
        {
            GUILayout.Label(Tr("records.no_areas_combat_were"), dim);
            return;
        }
        int i = Mathf.Clamp(recordFloor, 0, rec.floors.Count - 1);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Tr("common.prev_floor"), GUILayout.Width(90))) Defer(() => recordFloor = Mathf.Max(0, i - 1));
        GUILayout.Label($"{i + 1} / {rec.floors.Count}", center);
        if (GUILayout.Button(Tr("common.next_floor"), GUILayout.Width(90))) Defer(() => recordFloor = Mathf.Min(rec.floors.Count - 1, i + 1));
        GUILayout.EndHorizontal();
        var f = rec.floors[i];
        GUILayout.Label(Tr("common.area", f.index, f.title));
        if (!string.IsNullOrEmpty(f.bossAffix)) GUILayout.Label(Tr("common.boss_modifier", f.bossAffix), dim);
        if (f.defense is int fdef && fdef != 0) GUILayout.Label(Tr("common.enemy_defense_damage_reduction", fdef, DefenseReduction(fdef) * 100), dim);
        GUILayout.Label(Tr("common.time_combat", Fmt(f.durationSeconds), Fmt(f.combatSeconds)), dim);
        DrawTable(RecordRows(f.players), Tr("common.damage"), null, showLive: false);
    }

    static List<Row> RecordRows(IEnumerable<DamageRecord> players) =>
        (players ?? Enumerable.Empty<DamageRecord>())
        .Select((p, i) => new Row
        {
            Id = 0x80000000u + (uint)i,
            Name = p.name,
            Damage = p.damage,
            Dps = p.dps,
            Types = (p.sources ?? new List<SourceRecord>()).Select(s => new Detail { Name = s.name, Damage = s.damage, Elem = s.elem ?? -1, Cat = -1 }).ToList(),
            BySource = p.bySource?.Select(s => new Detail { Name = s.name, Damage = s.damage, Elem = -1, Cat = s.cat ?? -1 }).ToList()
        })
        .OrderByDescending(r => r.Damage)
        .ToList();
}
