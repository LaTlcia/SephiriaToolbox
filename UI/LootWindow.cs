using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    const float LootWidth = 460f;
    Rect lootRect = new(-1f, -1f, LootWidth, 0f);

    sealed class LootRow
    {
        public string Head, Value;
        public bool Best;
        public readonly List<string> Lines = new();
    }

    List<LootRow> lootRows = new();
    string lootTitle = "", lootBasis = "", lootStatus = "";
    bool lootButton;
    float nextLootRows;

    void RefreshLootRows()
    {
        if (!lootOpen || lootHidden || Time.unscaledTime < nextLootRows) return;
        nextLootRows = Time.unscaledTime + 0.1f;
        var goal = (ArrangeGoal)settings.arrGoal;
        if (goal == ArrangeGoal.Levels) goal = ArrangeGoal.Total;
        string goalName = GoalNames[(int)goal];
        (lootTitle, lootBasis) = lootKind switch
        {
            LootKind.Shop => (Tr("loot.title_shop"), Tr("loot.basis_shop", goalName)),
            LootKind.Enchant => (Tr("loot.title_enchant"), Tr("loot.basis_enchant", goalName)),
            _ => (Tr("loot.title"), Tr("loot.basis", goalName))
        };
        var opts = lootOptions ?? lootWaiting;
        lootButton = lootOptions == null && lootWaiting != null;
        if (!string.IsNullOrEmpty(lootMessage)) lootStatus = lootMessage;
        else if (lootOptions == null) lootStatus = "";
        else if (lootTask != null || opts.Any(o => o.Note == null && !o.Done)) lootStatus = Tr("loot.computing", lootProgress, lootTotal);
        else lootStatus = Tr("loot.done", lootSeconds);
        var rows = new List<LootRow>();
        if (opts != null)
        {
            var done = opts.Where(o => o.Done && o.Error == null && o.Note == null).OrderByDescending(o => o.Dps ? o.Gain : o.LevelGain).ToList();
            var best = done.FirstOrDefault(o => o.Taken && (o.Dps ? o.Gain > 0.001 : o.LevelGain > 0));
            var order = done.Concat(opts.Where(o => !done.Contains(o) && o.Note == null && o.Error == null))
                            .Concat(opts.Where(o => o.Note != null || o.Error != null));
            int money = LocalAvatar() is PlayerAvatar a ? a.Money : int.MaxValue;
            foreach (var o in order) rows.Add(LootRowOf(o, o == best, money));
        }
        lootRows = rows;
    }

    LootRow LootRowOf(LootOption o, bool best, int money)
    {
        var row = new LootRow { Best = best };
        string head = $"<color=#{RarityColor(o.Rarity)}>{o.Name}</color>";
        if (o.Price >= 0) head += $"  <color=#{(o.Price <= money ? "FFD24A" : "FF7070")}>{Tr("loot.price", o.Price)}</color>";
        row.Head = head;
        if (o.Note != null) { row.Value = ""; row.Lines.Add($"<color=#999999>{o.Note}</color>"); return row; }
        if (o.Error != null) { row.Value = ""; row.Lines.Add($"<color=#FF7070>{o.Error}</color>"); return row; }
        if (!o.Done) { row.Value = lootOptions == null ? "" : Tr("loot.pending"); return row; }
        if (o.Dps)
        {
            string color = o.Gain > 0.0005 ? "7CFC00" : o.Gain < -0.0005 ? "FF7070" : "BBBBBB";
            row.Value = $"<color=#{color}>{Signed(o.Gain)}</color>";
        }
        else row.Value = Tr("loot.levels_gain", o.LevelGain);
        if (o.Price > money) row.Lines.Add($"<color=#FF7070>{Tr("loot.not_enough_gold")}</color>");
        if (!o.Taken) { row.Lines.Add($"<color=#999999>{(lootKind == LootKind.Shop ? Tr("loot.not_worth_shop") : Tr("loot.not_worth"))}</color>"); return row; }
        if (o.Merge && lootKind != LootKind.Enchant)
            row.Lines.Add(o.MergeCats > 0 ? Tr("loot.merge_cats", o.MergeLevels, o.MergeCats) : Tr("loot.merge", o.MergeLevels));
        else if (o.Slot >= 0)
        {
            int x = o.Slot % Math.Max(1, o.W) + 1, y = o.Slot / Math.Max(1, o.W) + 1;
            int enter = o.EnterRot >= 0 ? o.EnterRot : lootSephirite != null ? lootSephirite.rotation : 0;
            int turns = o.IsTablet && o.Rotatable ? ((o.Rotation - enter) % 4 + 4) % 4 : 0;
            row.Lines.Add(turns > 0 ? Tr("loot.place_turn", y, x, turns) : Tr("loot.place", y, x));
        }
        if (o.Drop != null) row.Lines.Add($"<color=#FFB060>{Tr("loot.drop", o.Drop)}</color>");
        if (o.Dps && (ArrangeGoal)settings.arrGoal is not (ArrangeGoal.Total or ArrangeGoal.Levels)) row.Lines.Add(Tr("loot.total_damage", Signed(o.TotalGain)));
        if (o.Rearrange) row.Lines.Add($"<color=#999999>{Tr("loot.rearrange")}</color>");
        return row;
    }

    void DrawLootGui(float scale)
    {
        if (!lootOpen || lootHidden) return;
        float sw = Screen.width / scale, sh = Screen.height / scale;
        if (lootRect.x < 0 || lootRect.y < 0)
        {
            lootRect.x = windowRect.xMax + 10f + LootWidth <= sw ? windowRect.xMax + 10f : Mathf.Max(0f, windowRect.x - LootWidth - 10f);
            lootRect.y = windowRect.y;
        }
        var r = GUILayout.Window(0x5EF1D96, new Rect(lootRect.x, lootRect.y, LootWidth, 0f), DrawLootWindow, lootTitle, GUILayout.Width(LootWidth));
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
        GUILayout.Label(lootBasis, wrap, GUILayout.Width(LootWidth - 80));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(Tr("loot.close"), GUILayout.Width(56))) Defer(() => lootHidden = true);
        GUILayout.EndHorizontal();
        if (lootStatus.Length > 0) GUILayout.Label(lootStatus, dim);
        if (lootButton && GUILayout.Button(Tr("loot.evaluate"), GUILayout.Width(160))) Defer(StartLootManually);
        foreach (var row in lootRows)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(row.Best ? "<color=#FFD24A>★</color>" : "", rich, GUILayout.Width(16));
            GUILayout.Label(row.Head, rich, GUILayout.Width(300));
            GUILayout.Label(row.Value, rich, GUILayout.Width(110));
            GUILayout.EndHorizontal();
            foreach (var line in row.Lines)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(Indent);
                GUILayout.Label(line, richWrap, GUILayout.Width(LootWidth - Indent - 20));
                GUILayout.EndHorizontal();
            }
        }
    }
}
