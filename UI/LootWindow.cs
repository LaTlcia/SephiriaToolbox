using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    const float LootWidth = 460f;
    Rect lootRect = new(-1f, -1f, LootWidth, 0f);

    void DrawLootGui(float scale)
    {
        if (!lootOpen || lootHidden) return;
        float sw = Screen.width / scale, sh = Screen.height / scale;
        if (lootRect.x < 0 || lootRect.y < 0)
        {
            lootRect.x = windowRect.xMax + 10f + LootWidth <= sw ? windowRect.xMax + 10f : Mathf.Max(0f, windowRect.x - LootWidth - 10f);
            lootRect.y = windowRect.y;
        }
        var r = GUILayout.Window(0x5EF1D96, new Rect(lootRect.x, lootRect.y, LootWidth, 0f), DrawLootWindow, Tr("loot.title"), GUILayout.Width(LootWidth));
        r.x = Mathf.Clamp(r.x, 0f, Mathf.Max(0f, sw - 80f));
        r.y = Mathf.Clamp(r.y, 0f, Mathf.Max(0f, sh - 40f));
        if (r.x != lootRect.x || r.y != lootRect.y)
        {
            settings.lootX = r.x;
            settings.lootY = r.y;
            settingsDirty = true;
            nextSettingsSave = Time.unscaledTime + 1f;
        }
        lootRect = r;
    }

    void DrawLootWindow(int id)
    {
        try { DrawLootContent(); }
        catch (ExitGUIException) { throw; }
        catch (Exception e) { WarnOnce("掉落评估界面", e); }
        if (!settings.locked) GUI.DragWindow();
    }

    void DrawLootContent()
    {
        GUILayout.BeginHorizontal();
        var goal = (ArrangeGoal)settings.arrGoal;
        if (goal == ArrangeGoal.Levels) goal = ArrangeGoal.Total;
        GUILayout.Label(Tr("loot.basis", GoalNames[(int)goal]), wrap, GUILayout.Width(LootWidth - 80));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(Tr("loot.close"), GUILayout.Width(56))) Defer(() => lootHidden = true);
        GUILayout.EndHorizontal();
        if (lootOptions == null)
        {
            if (!string.IsNullOrEmpty(lootMessage)) GUILayout.Label(lootMessage, wrap);
            else if (!settings.lootAuto && lootSephirite != null && GUILayout.Button(Tr("loot.evaluate"), GUILayout.Width(160)))
                Defer(() => { if (lootSephirite != null) StartLoot(lootSephirite); });
            return;
        }
        if (!string.IsNullOrEmpty(lootMessage)) GUILayout.Label(lootMessage, wrap);
        else if (lootTask != null || lootBuildNext > -2) GUILayout.Label(Tr("loot.computing", lootProgress, lootTotal), dim);
        else GUILayout.Label(Tr("loot.done", lootSeconds), dim);

        var done = lootOptions.Where(o => o.Done && o.Error == null && o.Note == null).OrderByDescending(o => o.Dps ? o.Gain : o.LevelGain).ToList();
        var best = done.FirstOrDefault(o => o.Taken && (o.Dps ? o.Gain > 0.001 : o.LevelGain > 0));
        var order = done.Concat(lootOptions.Where(o => !done.Contains(o) && o.Note == null && o.Error == null))
                        .Concat(lootOptions.Where(o => o.Note != null || o.Error != null));
        foreach (var o in order) DrawLootOption(o, o == best);
    }

    void DrawLootOption(LootOption o, bool best)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(best ? "<color=#FFD24A>★</color>" : "", rich, GUILayout.Width(16));
        GUILayout.Label($"<color=#{RarityColor(o.Rarity)}>{o.Name}</color>", rich, GUILayout.Width(230));
        string value;
        if (o.Note != null || o.Error != null) value = "";
        else if (!o.Done) value = Tr("loot.pending");
        else if (o.Dps)
        {
            string color = o.Gain > 0.0005 ? "7CFC00" : o.Gain < -0.0005 ? "FF7070" : "BBBBBB";
            value = $"<color=#{color}>{Signed(o.Gain)}</color>";
        }
        else value = Tr("loot.levels_gain", o.LevelGain);
        GUILayout.Label(value, rich, GUILayout.Width(120));
        GUILayout.EndHorizontal();

        void Line(string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(Indent);
            GUILayout.Label(text, richWrap, GUILayout.Width(LootWidth - Indent - 20));
            GUILayout.EndHorizontal();
        }
        if (o.Note != null) { Line($"<color=#999999>{o.Note}</color>"); return; }
        if (o.Error != null) { Line($"<color=#FF7070>{o.Error}</color>"); return; }
        if (!o.Done) return;
        if (!o.Taken) { Line($"<color=#999999>{Tr("loot.not_worth")}</color>"); return; }
        if (o.Merge)
            Line(o.MergeCats > 0 ? Tr("loot.merge_cats", o.MergeLevels, o.MergeCats) : Tr("loot.merge", o.MergeLevels));
        else if (o.Slot >= 0)
        {
            int x = o.Slot % Math.Max(1, o.W) + 1, y = o.Slot / Math.Max(1, o.W) + 1;
            int turns = o.IsTablet && o.Rotatable && lootSephirite != null ? ((o.Rotation - lootSephirite.rotation) % 4 + 4) % 4 : 0;
            Line(turns > 0 ? Tr("loot.place_turn", y, x, turns) : Tr("loot.place", y, x));
        }
        if (o.Drop != null) Line($"<color=#FFB060>{Tr("loot.drop", o.Drop)}</color>");
        if (o.Dps && (ArrangeGoal)settings.arrGoal is not (ArrangeGoal.Total or ArrangeGoal.Levels)) Line(Tr("loot.total_damage", Signed(o.TotalGain)));
        if (o.Rearrange) Line($"<color=#999999>{Tr("loot.rearrange")}</color>");
    }
}
