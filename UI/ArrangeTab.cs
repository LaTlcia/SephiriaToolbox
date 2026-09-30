using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    GUIStyle cellStyle;
    bool arrNotesOpen;

    void DrawArrangeTab()
    {
        GUILayout.Label(Tr("arrange.simulates_locally_game_rules"), wrap);
        bool busy = arrState == ArrState.Computing || arrState == ArrState.Running;
        GUI.enabled = !busy;

        GUILayout.BeginHorizontal();
        GUILayout.Label(Tr("arrange.goal"), dim, GUILayout.Width(64));
        int goal = GUILayout.Toolbar(settings.arrGoal, GoalNames, GUILayout.Width(WindowWidth - 104));
        GUILayout.EndHorizontal();
        if (goal != settings.arrGoal) Defer(() => { settings.arrGoal = goal; SaveSettings(); });
        string hint = (ArrangeGoal)settings.arrGoal switch
        {
            ArrangeGoal.Levels => Tr("arrange.maximize_total_effective_level"),
            ArrangeGoal.Total => Tr("arrange.maximize_total_damage_computed"),
            ArrangeGoal.Weapon => Tr("arrange.only_weapon_damage_normal"),
            ArrangeGoal.Magic => Tr("arrange.only_damage_from_attack"),
            _ => Tr("arrange.weighted_sum_damage_types")
        };
        GUILayout.Label(hint, dim);
        if ((ArrangeGoal)settings.arrGoal == ArrangeGoal.Custom)
        {
            WeightSlider(Tr("common.weapon"), settings.wWeapon, v => settings.wWeapon = v);
            WeightSlider(Tr("arrange.magic"), settings.wMagic, v => settings.wMagic = v);
            WeightSlider(Tr("arrange.artifacts_other"), settings.wProc, v => settings.wProc = v);
        }

        bool dpsGoal = settings.arrGoal != (int)ArrangeGoal.Levels;
        if (dpsGoal)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tr("arrange.rates"), dim, GUILayout.Width(64));
            int basis = GUILayout.Toolbar(settings.arrUseMeasured ? 1 : 0, FrequencyModes, GUILayout.Width(320));
            GUILayout.EndHorizontal();
            if (basis != (settings.arrUseMeasured ? 1 : 0)) Defer(() => { settings.arrUseMeasured = basis == 1; SaveSettings(); });
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tr("arrange.scenario"), dim, GUILayout.Width(64));
            int scene = GUILayout.Toolbar(settings.arrSingle ? 0 : 1, SceneModes, GUILayout.Width(320));
            GUILayout.EndHorizontal();
            if (scene != (settings.arrSingle ? 0 : 1)) Defer(() => { settings.arrSingle = scene == 0; SaveSettings(); });
            GUILayout.Label(settings.arrSingle
                ? Tr("arrange.evaluated_against_single_boss")
                : Tr("arrange.uses_number_nearby_enemies"), wrap);
            GUILayout.Label(settings.arrUseMeasured
                ? Tr("arrange.damage_computed_from_your")
                : Tr("arrange.damage_computed_from_your_current"), wrap);
        }
        GUILayout.BeginHorizontal();
        bool rot = GUILayout.Toggle(settings.arrRotate, Tr("arrange.allow_rotating_tablets"), GUILayout.Width(170));
        bool others = GUILayout.Toggle(settings.arrMoveOthers, Tr("arrange.other_items_may_move"), GUILayout.Width(160));
        bool protect = dpsGoal ? GUILayout.Toggle(settings.arrProtect, Tr("arrange.keep_active_artifacts_active"), GUILayout.Width(200)) : settings.arrProtect;
        GUILayout.EndHorizontal();
        bool loot = GUILayout.Toggle(settings.lootAuto, Tr("loot.auto"));
        if (loot != settings.lootAuto) Defer(() => { settings.lootAuto = loot; SaveSettings(); });
        if (rot != settings.arrRotate) Defer(() => { settings.arrRotate = rot; SaveSettings(); });
        if (others != settings.arrMoveOthers) Defer(() => { settings.arrMoveOthers = others; SaveSettings(); });
        if (protect != settings.arrProtect) Defer(() => { settings.arrProtect = protect; SaveSettings(); });

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Tr("arrange.quick_1_s"), GUILayout.Width(150))) Defer(() => StartArrange(1.0));
        if (GUILayout.Button(Tr("arrange.deep_5_s"), GUILayout.Width(150))) Defer(() => StartArrange(5.0));
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        if (!string.IsNullOrEmpty(arrDpsInfo)) GUILayout.Label(arrDpsInfo, dim);
        if (arrModel != null && arrModel.Notes.Count > 0)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(arrNotesOpen ? "▼" : "▶", GUILayout.Width(26))) Defer(() => arrNotesOpen = !arrNotesOpen);
            GUILayout.Label(Tr("arrange.model_notes_detected_mechanics", arrModel.Notes.Count), dim);
            GUILayout.EndHorizontal();
            if (arrNotesOpen)
                foreach (var note in arrModel.Notes)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label("· " + note, wrap);
                    GUILayout.EndHorizontal();
                }
        }
        if (!string.IsNullOrEmpty(arrCheck)) GUILayout.Label(arrCheck, dim);
        if (!string.IsNullOrEmpty(arrMessage)) GUILayout.Label(arrMessage, wrap);
        if (arrState == ArrState.Running) GUILayout.Label(Tr("arrange.arranging", arrProgress, arrTotal));
        DrawEngraveSection();
        DrawForgeSection();
        if (arrState != ArrState.Ready || arrResult == null || arrModel == null) return;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Tr("arrange.arrange_now"), GUILayout.Width(120))) Defer(ExecuteArrange);
        if (GUILayout.Button(Tr("arrange.cancel"), GUILayout.Width(80))) Defer(() => { arrState = ArrState.Idle; arrResult = null; arrMessage = ""; });
        GUILayout.EndHorizontal();

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(ScrollHeight));
        DrawSourceTable(arrModel, arrResult);
        DrawCharmValues(arrModel, arrResult);
        GUILayout.Label(Tr("arrange.inventory_after_arranging_moved"), dim);
        DrawArrangeGrid(arrModel, arrResult);
        if (arrChanges.Count > 0)
        {
            GUILayout.Label(Tr("arrange.artifact_level_changes"), dim);
            foreach (var c in arrChanges) GUILayout.Label("　" + c, wrap);
        }
        GUILayout.EndScrollView();
    }

    void DrawEngraveSection()
    {
        if (arrModel == null || !arrModel.CanEngrave || arrModel.TabletItem.Length == 0) return;
        if (engraveTask != null)
        {
            GUILayout.Label(Tr("arrange.analyzing_imprinting_background_doesnt", engraveProgress, engraveTotal), dim);
            return;
        }
        if (engraveOptions == null) return;
        GUILayout.Label(Tr("arrange.imprinting_advice_press_hold"), wrap);
        var worth = engraveOptions.Where(EngraveWorthIt).ToList();
        if (worth.Count == 0)
        {
            GUILayout.Label(Tr("arrange.no_tablet_clearly_worth"), dim);
            return;
        }
        foreach (var o in worth.Take(5))
        {
            int x = o.Slot % arrModel.W + 1, y = o.Slot / arrModel.W + 1;
            int turns = ((o.Rotation - arrModel.StartRot[o.Tablet]) % 4 + 4) % 4;
            string gain = arrModel.Dps != null ? Tr("arrange.over_best_layout_without", Signed(o.Gain)) : Tr("arrange.effective_levels", o.LevelGain);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tr("arrange.imprint_row_column", o.Name, y, x, (turns > 0 ? Tr("arrange.rotated_clockwise", turns) : ""), gain), wrap, GUILayout.Width(WindowWidth - 150));
            bool busy = arrState == ArrState.Computing || arrState == ArrState.Running;
            GUI.enabled = !busy;
            if (GUILayout.Button(Tr("arrange.place_imprint"), GUILayout.Width(110))) Defer(() => StageEngraving(o));
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        if (!string.IsNullOrEmpty(engraveMessage)) GUILayout.Label(engraveMessage, wrap);
        GUILayout.Label(Tr("arrange.place_imprint_only_moves"), dim);
    }

    void WeightSlider(string label, float value, Action<float> set)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(Indent);
        GUILayout.Label(label, GUILayout.Width(110));
        float v = Mathf.Round(GUILayout.HorizontalSlider(value, 0f, 2f, GUILayout.Width(200)) * 10f) / 10f;
        GUILayout.Label(v.ToString("0.0"), GUILayout.Width(40));
        GUILayout.EndHorizontal();
        if (Math.Abs(v - value) > 0.001f) Defer(() => { set(v); settingsDirty = true; });
    }

    void DrawSourceTable(ArrModel m, ArrResult r)
    {
        var d = m.Dps;
        if (d == null || r.Before.Source == null || d.Sources.Length == 0) return;
        double total = r.Before.Total;
        double measuredTotal = d.Sources.Sum(s => s.Measured) + d.Other * d.Realization;
        bool hasMeasured = d.Windows != null && measuredTotal > 0;
        GUILayout.BeginHorizontal();
        GUILayout.Space(Indent);
        GUILayout.Label(Tr("arrange.damage_sources_basis_db"), dim, GUILayout.Width(250));
        GUILayout.Label(Tr("arrange.estimated"), dim, GUILayout.Width(50));
        if (hasMeasured) GUILayout.Label(Tr("common.measured"), dim, GUILayout.Width(50));
        GUILayout.Label(Tr("arrange.after"), dim, GUILayout.Width(70));
        GUILayout.EndHorizontal();
        string[] basisNames = { Tr("arrange.db"), Tr("arrange.live"), Tr("arrange.est") };
        for (int i = 0; i < d.Sources.Length; i++)
        {
            var s = d.Sources[i];
            double b = r.Before.Source[i], a = r.After.Source[i];
            if (b <= 0 && a <= 0 && s.Measured <= 0) continue;
            GUILayout.BeginHorizontal();
            GUILayout.Space(Indent);
            GUILayout.Label(Tr("arrange.basis_source", basisNames[Math.Min(s.Basis, (byte)2)], s.Name), GUILayout.Width(250));
            GUILayout.Label(total > 0 ? (b / total).ToString("P0") : "-", rightAlign, GUILayout.Width(50));
            if (hasMeasured) GUILayout.Label(s.Measured > 0 ? (s.Measured / measuredTotal).ToString("P0") : "-", rightAlign, GUILayout.Width(50));
            string change = b > 0 ? Signed(a / b - 1) : a > 0 ? Tr("arrange.new") : "-";
            string color = a > b * 1.0005 ? "7CFC00" : a < b * 0.9995 ? "FF7070" : "BBBBBB";
            GUILayout.Label($"<color=#{color}>{change}</color>", rich, GUILayout.Width(70));
            if (Math.Abs(s.Weight - 1) > 1e-6) GUILayout.Label(Tr("arrange.weight", s.Weight), dim);
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(s.Note))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(Indent + 24);
                GUILayout.Label(s.Note, dim);
                GUILayout.EndHorizontal();
            }
        }
        if (d.Other > 0)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(Indent);
            GUILayout.Label(Tr("arrange.other_reflect_damage_etc"), dim, GUILayout.Width(250));
            GUILayout.Label(total > 0 ? (d.Other / total).ToString("P0") : "-", rightAlign, GUILayout.Width(50));
            GUILayout.EndHorizontal();
        }
        if (hasMeasured)
            GUILayout.Label(Tr("arrange.estimated_computed_from_your", d.WindowSeconds), dim);
    }

    void DrawArrangeGrid(ArrModel m, ArrResult r)
    {
        cellStyle ??= new GUIStyle(GUI.skin.box) { richText = true, wordWrap = true, fontSize = 11, alignment = TextAnchor.MiddleCenter };
        float cell = Mathf.Floor((WindowWidth - 70f) / m.W);
        for (int y = 0; y < m.H; y++)
        {
            GUILayout.BeginHorizontal();
            for (int x = 0; x < m.W; x++)
            {
                int s = y * m.W + x;
                if (s >= m.N) { GUILayout.Space(cell + 4); continue; }
                int i = r.Perm[s];
                string text = "";
                if (i >= 0)
                {
                    var it = m.Items[i];
                    string name = ClipWidth(Resolve(it.Name), 12);
                    text = (i != m.Start[s] ? "• " : "") + $"<color=#{RarityColor(it.Rarity)}>{name}</color>";
                    if (it.Kind >= KindCharm)
                        text += r.After.On[i] ? $"\n<color=#7CFC00>Lv{r.After.Level[i]}</color>" : Tr("arrange.lv_off", r.After.Level[i]);
                    else if (it.IsTablet)
                    {
                        int turns = ((r.Rot[it.TabletIndex] - m.StartRot[it.TabletIndex]) % 4 + 4) % 4;
                        text += turns > 0 ? Tr("arrange.rotate", turns) : Tr("arrange.tablet");
                    }
                }
                GUILayout.Label(text, cellStyle, GUILayout.Width(cell), GUILayout.Height(42));
            }
            GUILayout.EndHorizontal();
        }
    }
}
