using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mirror;
using UnityEngine;

public partial class SephiriaToolbox
{
    void BuildSpecials(PlayerAvatar avatar, ArrModel m, Charm_Basic[] charmOf, DpsModel d, Func<string, int> key)
    {
        var cats = new List<string>();
        var catIndex = new Dictionary<string, int>();
        int Cat(string c)
        {
            if (string.IsNullOrEmpty(c)) return -1;
            if (!catIndex.TryGetValue(c, out var i)) { i = cats.Count; cats.Add(c); catIndex[c] = i; }
            return i;
        }
        var specials = new List<Special>();
        for (int i = 0; i < m.Items.Length; i++)
        {
            var c = charmOf[i];
            if (c == null) continue;
            try
            {
                bool dynamicCats = false;
                switch (c)
                {
                    case Charm_NearLevelDamage nl:
                        specials.Add(new Special { Kind = SpecialKind.NearLevel, Item = i, F = nl.allDamageBonusByLevel });
                        break;
                    case Charm_ReduceMPCost rc:
                        specials.Add(new Special { Kind = SpecialKind.CostLeft, Item = i, A = rc.reducePercentByLevel });
                        break;
                    case Charm_RightSpellCooldownHelper hg:
                        specials.Add(new Special { Kind = SpecialKind.Hourglass, Item = i, A = hg.cooldownRecoveryByLevel });
                        break;
                    case Charm_UpCharmDamage up:
                        specials.Add(new Special
                        {
                            Kind = SpecialKind.Booster, Item = i, A = up.damageBonusByLevel, B = up.dependencyDamageBonusByLevel,
                            Dx = up.xOffset, Dy = up.yOffset, Cond = up.hasDependencyCondition, MaxRarity = (int)up.maxRarity
                        });
                        dynamicCats = true;
                        break;
                    case Charm_PlanetModule:
                        specials.Add(new Special { Kind = SpecialKind.Telescope, Item = i });
                        break;
                    case Charm_AutoMagic am:
                        specials.Add(new Special { Kind = SpecialKind.AutoMagic, Item = i, F = am.cooldownByLevel });
                        break;
                    case Charm_3Elemental_ByRow br:
                    {
                        var rowCats = br.lineCategory.Select(Cat).ToArray();
                        var rowKeys = br.lineCategory.Select(lc => key(lc switch
                        {
                            "EMBER" => "FIREDAMAGE",
                            "GLACIER" => "ICEDAMAGE",
                            "MAGITECH" => "LIGHTNINGDAMAGE",
                            _ => "PHYSICALDAMAGE"
                        })).ToArray();
                        specials.Add(new Special { Kind = SpecialKind.ByRow, Item = i, A = br.addElementalStatByLevel, RowCats = rowCats, RowKeys = rowKeys });
                        dynamicCats = true;
                        break;
                    }
                    case Charm_FireIce fi:
                        specials.Add(new Special
                        {
                            Kind = SpecialKind.FireIce, Item = i, A = fi.mainStat, B = fi.oppositeStat,
                            KeyL = key(fi.leftStatName.ToString().ToUpperInvariant()), KeyR = key(fi.rightStatName.ToString().ToUpperInvariant())
                        });
                        break;
                    case Charm_WhitePaper wp:
                        specials.Add(new Special { Kind = SpecialKind.WhitePaper, Item = i, Match = Math.Max(1, wp.match) });
                        dynamicCats = true;
                        break;
                    case Charm_CompanionChaos:
                        specials.Add(new Special { Kind = SpecialKind.Badge, Item = i });
                        break;
                    case Charm_WoodenBox wb:
                        specials.Add(new Special { Kind = SpecialKind.QuickRow, Item = i, A = wb.apPerQuickSlotCharmByLevel });
                        break;
                }
                if (!dynamicCats)
                {
                    var ent = ItemDatabase.FindItemById(m.Items[i].EntityId);
                    d.Extra[i].Cats = ent != null && ent.categories != null ? ent.categories.Select(Cat).Where(x => x >= 0).ToArray() : new int[0];
                    if (avatar.Inventory.uniquePairAddedComboCount.TryGetValue(m.Items[i].InstanceId, out var up) && up > 0) d.Extra[i].CatWeight = 1 + up;
                }
            }
            catch (Exception e) { WarnOnce("联动识别 " + m.Items[i].Name, e); }
        }
        try
        {
            if (avatar.GetCustomStatUnsafe("ARRANGEMENTBONUS") > 0)
                foreach (var ab in ItemDatabase.GetAllArrangementBonuses())
                {
                    var arr = ab != null && ab.isEnabled ? ab.arrangements : null;
                    if (arr == null || arr.Length == 0 || arr.Any(a => a == null || a.item == null)) continue;
                    if (arr.Select(a => (a.offset.x, a.offset.y)).Distinct().Count() != arr.Length) continue;
                    var ids = arr.Select(a => a.item.id).ToArray();
                    var flags = arr.Select(a => (byte)(a.activeBonusID switch { "FAST" => 1, "WIDE" => 2, "BLACKHOLE" => 4, _ => 0 })).ToArray();
                    int added = 0;
                    for (int i = 0; i < m.Items.Length; i++)
                        if (ids.Contains(m.Items[i].EntityId))
                        {
                            specials.Add(new Special
                            {
                                Kind = SpecialKind.ArrBonus, Item = i, PatIds = ids, PatFlag = flags,
                                PatDx = arr.Select(a => a.offset.x - arr[0].offset.x).ToArray(),
                                PatDy = arr.Select(a => a.offset.y - arr[0].offset.y).ToArray()
                            });
                            added++;
                        }
                    if (added > 0 && flags.Any(f => (f & 3) != 0))
                        m.Notes.Add(Tr("model.arrangement_bonus_side_side", Clean(ab.aName.ToString()), string.Join(" + ", ids.Select(x => ItemName(x)))));
                }
        }
        catch (Exception e) { WarnOnce("排列加成", e); }
        d.Specials = specials.ToArray();

        foreach (var kv in avatar.Inventory.currentSetEffectCount) Cat(kv.Key);
        d.CatNames = cats.ToArray();
        d.CatActual = new int[cats.Count];
        d.Tiers = new ComboTier[cats.Count][];
        d.CatHighest = new int[cats.Count];
        for (int c = 0; c < cats.Count; c++)
        {
            d.CatActual[c] = avatar.Inventory.currentSetEffectCount.TryGetValue(cats[c], out var n) ? n : 0;
            var tiers = new List<ComboTier>();
            try
            {
                var entity = ItemDatabase.FindItemCategory(cats[c]);
                var combo = entity != null && entity.comboEffectPrefab != null ? entity.comboEffectPrefab.GetComponent<ComboEffectBase>() : null;
                if (combo != null) d.CatHighest[c] = combo.GetHighestComboCount();
                if (combo != null && combo.addStatByCombo != null)
                    foreach (var tier in combo.addStatByCombo)
                    {
                        if (tier == null || tier.status == null) continue;
                        var adds = new List<StatAdd>();
                        foreach (var st in tier.status)
                        {
                            var parts = (st ?? "").Split('/');
                            if (parts.Length < 2 || !int.TryParse(parts[1], out var v)) continue;
                            if (!MapStatus(parts[0], out var k, out var mode)) continue;
                            adds.Add(new StatAdd { Key = mode == 2 ? -1 : key(k), Mode = mode, Values = new[] { v } });
                        }
                        tiers.Add(new ComboTier { Count = tier.comboCount, Adds = adds.ToArray() });
                    }
            }
            catch (Exception e) { WarnOnce("连击数据 " + cats[c], e); }
            d.Tiers[c] = tiers.OrderBy(t => t.Count).ToArray();
        }
        d.ComboBonus = avatar.GetCustomStatUnsafe("COMBOBONUSDAMAGE");
        d.kComboBonus = key("COMBOBONUSDAMAGE");

        string[] kindNames = { Tr("model.crystal_harmony"), Tr("model.glowing_hourglass"), Tr("model.amplifier_points_adjacent_artifact"), Tr("model.giant_telescope"), Tr("model.auto_cast"), Tr("model.element_row"), Tr("model.left_right_half_stats"), Tr("model.white_paper"), Tr("model.devotion_insignia"), Tr("model.wooden_box_quick_slot") };
        foreach (var g in d.Specials.GroupBy(sp => sp.Kind)) m.Notes.Add($"{kindNames[(int)g.Key]} ×{g.Count()}");
        int planets = d.Extra.Count(e => e.Planet);
        if (planets > 0) m.Notes.Add(Tr("model.planets", planets));
        if (d.Specials.Any(sp => sp.Kind is SpecialKind.WhitePaper or SpecialKind.Booster or SpecialKind.ByRow))
            m.Notes.Add(d.ComboBonus != 0 ? Tr("model.combo_tiers_bonus", d.Tiers.Count(t => t.Length > 0), d.ComboBonus)
                                          : Tr("model.combo_tiers", d.Tiers.Count(t => t.Length > 0)));
    }
}
