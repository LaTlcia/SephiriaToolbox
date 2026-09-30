using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    void DrawBuildTab()
    {
        if (builds.Count == 0)
        {
            GUILayout.Label(Tr("build.no_player_data_yet"));
            return;
        }
        GUILayout.Label(runEnded ? Tr("build.this_run_has_ended") : Tr("build.current_builds_whole_party"), dim);
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(ScrollHeight));
        foreach (var id in playerOrder)
            if (builds.TryGetValue(id, out var p)) DrawBuild(p, "live:" + id, showDamage: false);
        GUILayout.EndScrollView();
    }

    void DrawBuild(PlayerRecord p, string key, bool showDamage)
    {
        bool open = !collapsed.Contains(key);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(open ? "−" : "+", GUILayout.Width(22)))
            Defer(() => { if (!collapsed.Remove(key)) collapsed.Add(key); });
        GUILayout.Label(p.name ?? "?", playerName, GUILayout.Width(150));
        if (p.level > 0) GUILayout.Label($"Lv.{p.level}", GUILayout.Width(55));
        GUILayout.Label(p.costumeName ?? p.costume ?? "", dim);
        GUILayout.FlexibleSpace();
        if (showDamage && p.damage > 0) GUILayout.Label(Tr("build.damage_dps", p.damage, p.dps), dim);
        GUILayout.EndHorizontal();
        if (!open) return;

        Field(Tr("common.weapon"), p.weapon?.name ?? "—");
        Field(Tr("build.miracle"), p.miracles.Count > 0 ? string.Join(Tr("common.sep_list"), p.miracles.Select(m => m.name)) : "—");
        if (p.talents != null)
        {
            Field(Tr("build.talents"), p.talents.Count > 0 ? string.Join("　", p.talents.Select(t => $"{t.name} {t.level}")) : "—");
            var perks = p.talents.Where(t => t.perks != null).SelectMany(t => t.perks).Where(x => !string.IsNullOrEmpty(x)).ToList();
            if (perks.Count > 0) Field(Tr("build.talent_effects"), string.Join(Tr("common.sep_semicolon"), perks));
        }
        if (p.sets.Count > 0)
            Field(Tr("build.sets"), string.Join("　", p.sets.OrderByDescending(s => s.count).Select(s => $"{s.name}×{s.count}")));
        if (p.fruitSkewer.Count > 0)
            Field(Tr("build.fruit_skewer"), string.Join("　", p.fruitSkewer.Select(f => f.weight > 1 ? $"{f.name}×{f.weight}" : f.name)));

        foreach (var g in p.inventory.GroupBy(i => string.IsNullOrEmpty(i.typeName) ? i.type : i.typeName).OrderBy(g => TypeOrder(g.First().type)))
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(Indent);
            GUILayout.Label(Tr("common.name_paren", g.Key, g.Count()), dim);
            GUILayout.EndHorizontal();
            var items = g.OrderByDescending(i => i.rarity).ThenByDescending(i => i.level).ThenBy(i => i.name).ToList();
            for (int i = 0; i < items.Count; i += 2)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(Indent + 12);
                ItemLabel(items[i]);
                if (i + 1 < items.Count) ItemLabel(items[i + 1]);
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.Space(8);
    }

    void Field(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(Indent);
        GUILayout.Label(label, dim, GUILayout.Width(90));
        GUILayout.Label(value, wrap, GUILayout.Width(WindowWidth - Indent - 90 - 60));
        GUILayout.EndHorizontal();
    }

    void ItemLabel(ItemRecord i)
    {
        string text = $"<color=#{RarityColor(i.rarity)}>{i.name}</color>";
        if (i.maxLevel > 0) text += $"  Lv{i.level}";
        if (i.quantity > 1) text += $"  ×{i.quantity}";
        GUILayout.Label(text, rich, GUILayout.Width(235));
    }

    string RarityColor(int rarity)
    {
        if (rarityColors.TryGetValue(rarity, out var c)) return c;
        try { c = ColorUtility.ToHtmlStringRGB(ItemDatabase.GetColorViaItemRarity((EItemRarity)rarity)); }
        catch { c = "FFFFFF"; }
        return rarityColors[rarity] = c;
    }

    static int TypeOrder(string type) => type switch { "Charm" => 0, "StoneTablet" => 1, _ => 2 };
}
