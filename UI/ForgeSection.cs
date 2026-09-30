using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed class ForgeGroup
    {
        public ForgeOption First;
        public readonly List<ForgeOption> Next = new();
        public ForgeOption BestOption;
        public double Best = -1;
        public double NextBest = -1;
    }

    readonly HashSet<int> forgeOpen = new();
    bool forgeHidden;
    Vector2 forgeScroll;
    const int ForgeVisibleRows = 9;

    List<ForgeGroup> ForgeGroups()
    {
        var groups = new List<ForgeGroup>();
        if (forgeOptions == null) return groups;
        foreach (var o in forgeOptions)
        {
            if (o.Step == 1 || groups.Count == 0) groups.Add(new ForgeGroup { First = o });
            else groups[groups.Count - 1].Next.Add(o);
        }
        foreach (var g in groups)
        {
            g.Next.Sort((a, b) => b.Dps.CompareTo(a.Dps));
            g.NextBest = g.Next.Count > 0 ? g.Next[0].Dps : -1;
            g.BestOption = g.NextBest > g.First.Dps ? g.Next[0] : g.First;
            g.Best = g.BestOption.Dps;
        }
        groups.Sort((a, b) => b.Best.CompareTo(a.Best));
        return groups;
    }

    void OpenBestForgeGroup()
    {
        forgeOpen.Clear();
        var groups = ForgeGroups();
        if (groups.Count > 0 && groups[0].Next.Count > 0) forgeOpen.Add(groups[0].First.WeaponId);
        forgeHidden = false;
        forgeScroll = Vector2.zero;
    }

    string ForgeGain(ForgeOption o)
    {
        if (o.Dps <= 0) return o.Error ?? Tr("forge.not_available");
        double ratio = o.Dps / forgeBase - 1;
        string color = ratio > 0.0005 ? "7CFC00" : ratio < -0.0005 ? "FF7070" : "BBBBBB";
        return Tr("forge.gain_dps", color, Signed(ratio), o.Dps);
    }

    string ForgeText(ForgeOption o) => Tr("forge.name_gain", o.Name, ForgeGain(o));

    void DrawForgeSection()
    {
        bool hasResult = forgeTask == null && forgeOptions != null && forgeBase > 0;
        GUILayout.BeginHorizontal();
        bool busy = forgeTask != null || arrState == ArrState.Computing || arrState == ArrState.Running;
        GUI.enabled = !busy;
        if (GUILayout.Button(Tr("forge.enhance_advice"), GUILayout.Width(110))) Defer(StartForgeAdvice);
        GUI.enabled = true;
        if (hasResult && GUILayout.Button(forgeHidden ? Tr("forge.expand") : Tr("forge.collapse"), GUILayout.Width(70))) Defer(() => forgeHidden = !forgeHidden);
        GUILayout.Label(Tr("forge.predicts_damage_each_enhancement"), wrap);
        GUILayout.EndHorizontal();
        if (forgeTask != null)
        {
            GUILayout.Label(Tr("forge.analyzing_enhancements_background_doesnt", forgeProgress, forgeTotal), dim);
            return;
        }
        if (!string.IsNullOrEmpty(forgeMessage)) GUILayout.Label(forgeMessage, wrap);
        if (!hasResult || forgeHidden) return;

        var groups = ForgeGroups();
        GUILayout.Label(Tr("forge.now_s_best_layout", forgeCurrentName, forgeBase), dim);
        var top = groups.Count > 0 ? groups[0] : null;
        if (top != null && top.Best > 0)
        {
            string path = top.BestOption == top.First ? top.First.Name : top.First.Name + " → " + top.BestOption.Name;
            GUILayout.Label(Tr("forge.best_path", path, ForgeGain(top.BestOption)), richWrap);
        }

        int rows = groups.Count + groups.Where(g => forgeOpen.Contains(g.First.WeaponId)).Sum(g => g.Next.Count);
        bool useScroll = rows > ForgeVisibleRows;
        if (useScroll) forgeScroll = GUILayout.BeginScrollView(forgeScroll, GUILayout.Height(ForgeVisibleRows * 24));
        foreach (var g in groups)
        {
            bool open = forgeOpen.Contains(g.First.WeaponId);
            GUILayout.BeginHorizontal();
            GUILayout.Space(12);
            if (g.Next.Count > 0)
            {
                int id = g.First.WeaponId;
                if (GUILayout.Button(open ? "▼" : "▶", GUILayout.Width(26))) Defer(() => { if (!forgeOpen.Remove(id)) forgeOpen.Add(id); });
            }
            else GUILayout.Space(30);
            string more = g.Next.Count > 0 && !open && g.NextBest > 0
                ? Tr("forge.further_enhancement_best", g.Next.Count, Signed(g.NextBest / forgeBase - 1)) : "";
            GUILayout.Label(Tr("forge.enhance", ForgeText(g.First), more), richWrap);
            GUILayout.EndHorizontal();
            if (!open) continue;
            foreach (var n in g.Next)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(12 + 30 + 20);
                GUILayout.Label(Tr("forge.then_enhance", ForgeText(n)), richWrap);
                GUILayout.EndHorizontal();
            }
        }
        if (useScroll) GUILayout.EndScrollView();
        GUILayout.Label(Tr("forge.moves_multipliers_add_effects"), dim);
    }
}
