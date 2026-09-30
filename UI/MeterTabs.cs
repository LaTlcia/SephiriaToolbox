using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    void DrawCurrentTab()
    {
        if (floor == null)
        {
            GUILayout.Label(runEnded ? Tr("meter.this_run_has_ended") : Tr("meter.waiting_combat"));
            return;
        }
        GUILayout.Label(Tr("common.area", floor.Index, floor.Title));
        GUILayout.Label(Tr("common.time_combat", Fmt(floor.Duration(Time.unscaledTime)), Fmt(floor.CombatTime)), dim);
        DrawTable(currentRows, Tr("meter.floor_damage"), Tr("meter.live_dps"), showLive: true);
    }

    void DrawHistoryTab()
    {
        var floors = history.Count > 0 ? history : previousRun?.Floors;
        if (floors == null || floors.Count == 0)
        {
            GUILayout.Label(Tr("meter.no_finished_areas_yet"));
            return;
        }
        if (history.Count == 0) GUILayout.Label(Tr("meter.previous_run"), dim);
        int i = historyIndex < 0 ? floors.Count - 1 : Mathf.Clamp(historyIndex, 0, floors.Count - 1);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Tr("common.prev_floor"), GUILayout.Width(90))) Defer(() => PageHistory(-1));
        GUILayout.Label($"{i + 1} / {floors.Count}", center);
        if (GUILayout.Button(Tr("common.next_floor"), GUILayout.Width(90))) Defer(() => PageHistory(+1));
        GUILayout.EndHorizontal();
        var seg = floors[i];
        GUILayout.Label(Tr("common.area", seg.Index, seg.Title));
        if (!string.IsNullOrEmpty(seg.BossAffix)) GUILayout.Label(Tr("common.boss_modifier", seg.BossAffix), dim);
        if (seg.Defense is int sd && sd != 0) GUILayout.Label(Tr("common.enemy_defense_damage_reduction", sd, DefenseReduction(sd) * 100), dim);
        GUILayout.Label(Tr("common.time_combat", Fmt(seg.Duration(seg.EndTime)), Fmt(seg.CombatTime)), dim);
        DrawTable(BuildRows(seg.Final, seg.CombatTime, false, seg.Names), Tr("common.damage"), null, showLive: false);
    }

    void DrawTotalTab()
    {
        if (totalRows.Sum(r => r.Damage) <= 0f && previousRun != null)
        {
            GUILayout.Label(Tr("meter.previous_run_areas_combat", previousRun.Floors.Count, Fmt(previousRun.CombatTime)));
            GUILayout.Label(Tr("meter.total_time", Fmt(previousRun.Duration)), dim);
            DrawTable(BuildRows(previousRun.Totals, previousRun.CombatTime, false, previousRun.Names), Tr("common.total_damage"), null, showLive: false);
            return;
        }
        float elapsed = runEnded ? runEndTime - runStartTime : floorCounter > 0 ? Time.unscaledTime - runStartTime : 0f;
        GUILayout.Label(Tr("meter.this_run_areas_recorded", history.Count, Fmt(runCombatTime)));
        GUILayout.Label(Tr("meter.total_time", Fmt(elapsed)), dim);
        DrawTable(totalRows, Tr("common.total_damage"), null, showLive: false);
    }

    void DrawTable(List<Row> rows, string damageTitle, string liveTitle, bool showLive)
    {
        float team = rows.Sum(r => r.Damage);
        GUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(22));
        GUILayout.Label(Tr("meter.player"), GUILayout.Width(130));
        if (showLive) GUILayout.Label(liveTitle, rightAlign, GUILayout.Width(80));
        GUILayout.Label(Tr("meter.avg_dps"), rightAlign, GUILayout.Width(80));
        GUILayout.Label(damageTitle, rightAlign, GUILayout.Width(100));
        GUILayout.Label(Tr("meter.share"), rightAlign, GUILayout.Width(55));
        GUILayout.EndHorizontal();

        foreach (var r in rows)
        {
            bool open = expandAll || expanded.Contains(r.Id);
            GUILayout.BeginHorizontal();
            uint rowId = r.Id;
            if (GUILayout.Button(open ? "−" : "+", GUILayout.Width(22)))
                Defer(() => { if (!expanded.Remove(rowId)) expanded.Add(rowId); });
            GUILayout.Label(r.Name, GUILayout.Width(130));
            if (showLive) GUILayout.Label(r.LiveDps.ToString("N0"), rightAlign, GUILayout.Width(80));
            GUILayout.Label(r.Dps.ToString("N0"), rightAlign, GUILayout.Width(80));
            GUILayout.Label(r.Damage.ToString("N0"), rightAlign, GUILayout.Width(100));
            GUILayout.Label(team > 0 ? (r.Damage / team).ToString("P0") : "-", rightAlign, GUILayout.Width(55));
            GUILayout.EndHorizontal();

            if (!open) continue;
            var details = settings.bySource && r.BySource != null ? r.BySource : r.Types ?? new List<Detail>();
            float perSecond = r.Damage > 0 && r.Dps > 0 ? r.Dps / r.Damage : 0f;
            foreach (var s in details.Take(TopDetails)) DrawDetailLine(s, r.Damage, perSecond);
            if (details.Count > TopDetails) GUILayout.Label(Tr("meter.more", details.Count - TopDetails), dim);
        }
        GUILayout.BeginHorizontal();
        GUILayout.Label(Tr("meter.party_total_click_details", team), dim);
        GUILayout.FlexibleSpace();
        int mode = GUILayout.Toolbar(settings.bySource ? 0 : 1, DetailModes, GUILayout.Width(140));
        if (mode != (settings.bySource ? 0 : 1)) Defer(() => { settings.bySource = mode == 0; SaveSettings(); });
        GUILayout.EndHorizontal();
    }

    void DrawDetailLine(Detail s, float total, float perSecond)
    {
        var rect = GUILayoutUtility.GetRect(1f, 21f, GUILayout.ExpandWidth(true));
        float share = total > 0 ? s.Damage / total : 0f;
        float x = rect.x + 26f;
        if (Event.current.type == EventType.Repaint)
        {
            var prev = GUI.color;
            GUI.color = s.Cat >= 0 && s.Cat < CatColors.Length ? CatColors[s.Cat] : s.Elem >= 0 ? ElementColor((EDamageElementalType)s.Elem) : CatColors[CatColors.Length - 1];
            GUI.DrawTexture(new Rect(x, rect.y + 2f, Mathf.Max(0f, rect.width - 26f) * Mathf.Clamp01(share), rect.height - 4f), Texture2D.whiteTexture);
            GUI.color = prev;
        }
        GUI.Label(new Rect(x + 4f, rect.y, 262f, rect.height), Resolve(s.Name), detailName);
        if (perSecond > 0f) GUI.Label(new Rect(x + 266f, rect.y, 84f, rect.height), (s.Damage * perSecond).ToString("N0") + "/s", rightAlign);
        GUI.Label(new Rect(x + 350f, rect.y, 100f, rect.height), s.Damage.ToString("N0"), rightAlign);
        GUI.Label(new Rect(x + 450f, rect.y, 60f, rect.height), share.ToString("P1"), rightAlign);
    }
}
