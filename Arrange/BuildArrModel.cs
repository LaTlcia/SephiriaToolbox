using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

public partial class SephiriaToolbox
{
    ArrModel BuildModel(PlayerAvatar avatar, bool moveOtherItems, out Charm_Basic[] charmOf, out string error)
    {
        error = null;
        charmOf = null;
        var inv = avatar.Inventory;
        if (inv == null) { error = Tr("arrange.inventory_not_found"); return null; }
        if (inv.globalActiveValue <= 0) { error = Tr("arrange.inventory_globally_disabled_right"); return null; }
        int w = inv.Width, n = inv.CurrentInventoryStorage, h = inv.Height;
        if (w <= 0 || n <= 1) { error = Tr("arrange.inventory_too_small"); return null; }

        var dm = DungeonManager.Instance;
        var wc = avatar.GetComponent<WeaponControllerSimple>();
        var weaponType = wc != null && wc.currentWeapon != null ? wc.currentWeapon.weaponType : (EWeaponType?)null;

        var items = new List<ArrItem>();
        var charms = new List<Charm_Basic>();
        var tablets = new List<StoneTablet>();
        var start = Enumerable.Repeat(-1, n).ToArray();
        var uniqueGroups = new Dictionary<int, int>();
        for (int s = 0; s < n; s++)
        {
            var pos = inv.IdxToPos(s);
            var v = inv.FindItem(pos);
            if (v == null) continue;
            var e = ItemDatabase.FindItemById(v.EntityID);
            var it = new ArrItem
            {
                InstanceId = v.InstanceID,
                EntityId = v.EntityID,
                Name = ItemName(v.EntityID),
                Rarity = e != null ? (int)e.rarity : 0,
                Kind = KindItem
            };
            Charm_Basic charm = null;
            if (e != null && e.type == EItemType.Charm)
            {
                var c = v.Charm;
                if (c == null) { error = Tr("arrange.cannot_read_artifact_data", it.Name); return null; }
                charm = c;
                it.Kind = c is Charm_Magic ? KindMagic : KindCharm;
                it.MaxLevel = c.maxLevel;
                it.Crit = CritOf(c);
                if (it.Crit == Crit.Const)
                {
                    try { it.CritConst = c.criteria.GetCriteria(c); } catch { it.CritConst = c.IsEffectEnabled; }
                }
                it.WeaponOk = !c.isWeaponRelatedCharm || (weaponType.HasValue && weaponType.Value == c.relatedWeapon);
                if (dm != null && int.TryParse(dm.GetGlobalItemStatValue(v.InstanceID, "Enchant"), out var ench)) it.Enchant = ench;
                if (c.isUniqueEffect)
                {
                    if (!uniqueGroups.TryGetValue(v.EntityID, out var g)) uniqueGroups[v.EntityID] = g = uniqueGroups.Count;
                    it.UniqueGroup = g;
                }
                it.ActualLevel = c.DisplayedLevel;
                it.ActualEnabled = c.IsEffectEnabled;
            }
            else if (v.StoneTablet != null)
            {
                it.IsTablet = true;
                it.TabletIndex = tablets.Count;
                it.StartRotation = ((v.StoneTablet.rotation % 4) + 4) % 4;
                it.Rotatable = DungeonManager.IsTabletRotatable(v.StoneTablet);
                tablets.Add(v.StoneTablet);
            }
            start[s] = items.Count;
            items.Add(it);
            charms.Add(charm);
        }
        if (!items.Any(i => i.Kind >= KindCharm)) { error = Tr("arrange.no_artifacts_inventory"); return null; }

        var engravings = inv.engravings.Where(t => t != null).ToList();
        var m = new ArrModel
        {
            W = w, H = h, N = n,
            Items = items.ToArray(),
            Start = start,
            StartRot = items.Where(i => i.IsTablet).Select(i => i.StartRotation).Concat(Enumerable.Repeat(-1, tablets.Count)).ToArray(),
            CanEngrave = inv.tabletEngravingCount > 0,
            TabletItem = items.Select((it, idx) => (it, idx)).Where(p => p.it.IsTablet).Select(p => p.idx).ToArray(),
            UniqueGroups = uniqueGroups.Count,
            Movable = new bool[n],
            TabletRules = tablets.Select(t => new TabletRule { Condition = t.GetConditionQuery(t.instanceID), Effect = t.GetQuery(t.instanceID) }).ToArray(),
            EngravingRules = engravings.Select(t => new TabletRule
            {
                Condition = t.GetConditionQuery(t.instanceID), Effect = t.GetQuery(t.instanceID),
                X = t.xIdx, Y = t.yIdx, Rotation = ((t.rotation % 4) + 4) % 4
            }).ToArray(),
            BaseLevel = new int[n], BaseMult = new int[n], BaseDisable = new int[n], BaseIgnore = new int[n]
        };
        for (int s = 0; s < n; s++)
            m.Movable[s] = moveOtherItems || start[s] < 0 || m.Items[start[s]].Kind >= KindCharm || m.Items[start[s]].IsTablet;

        var tabInc = new int[n]; var tabMul = new int[n]; var tabDis = new int[n]; var tabIgn = new int[n];
        foreach (var t in tablets.Concat(engravings))
        {
            if (!t.IsApplied) continue;
            foreach (var e in t.EffectRange)
            {
                int slot = SlotOf(e.position.x, e.position.y, w, n);
                if (slot < 0) continue;
                switch (e.effectType)
                {
                    case StoneTablet.EffectType.IncreaseConstLevel: tabInc[slot] += e.levelParam; break;
                    case StoneTablet.EffectType.Disable: tabDis[slot]++; break;
                    case StoneTablet.EffectType.IgnoreCriteria: tabIgn[slot]++; break;
                    case StoneTablet.EffectType.MultiplyConstLevel: tabMul[slot] += e.levelParam; break;
                }
            }
        }
        for (int s = 0; s < n; s++)
        {
            var pos = inv.IdxToPos(s);
            int mult = inv.multiplyLevelMatrix.TryGetValue(pos, out var mm) ? mm : 0;
            bool hasLevel = inv.levelMatrix.TryGetValue(pos, out var lv);
            int pre = hasLevel && mult != 0 ? lv / mult : (hasLevel ? lv : 0);
            int ench = start[s] >= 0 && m.Items[start[s]].Kind >= KindCharm ? m.Items[start[s]].Enchant : 0;
            m.BaseLevel[s] = pre - ench - tabInc[s];
            m.BaseMult[s] = mult - tabMul[s];
            m.BaseDisable[s] = (inv.disableMatrix.TryGetValue(pos, out var d) ? d : 0) - tabDis[s];
            m.BaseIgnore[s] = (inv.ignoreCriteriaMatrix.TryGetValue(pos, out var ig) ? ig : 0) - tabIgn[s];
        }
        charmOf = charms.ToArray();

        var keep = Enumerable.Range(0, m.Items.Length).Where(i => charms[i] is Charm_FireIceWeapon).ToArray();
        if (keep.Length > 0)
        {
            m.KeepSideItem = keep;
            m.KeepSideLeft = keep.Select(i => Array.IndexOf(start, i) % w <= 2).ToArray();
            m.Notes.Add(Tr("arrange.artifact_whose_effect_depends", keep.Length));
        }
        try
        {
            if (avatar.GetCustomStatUnsafe("ARRANGEMENTBONUS") > 0)
            {
                var patterns = new List<int[]>();
                foreach (var bonus in ItemDatabase.GetAllArrangementBonuses())
                {
                    if (bonus == null || !bonus.isEnabled || bonus.arrangements == null || bonus.arrangements.Length == 0) continue;
                    if (bonus.arrangements.Any(a => a == null || a.item == null)) continue;
                    if (bonus.arrangements.All(a => a.activeBonusID is "" or "FAST" or "WIDE") && bonus.arrangements.Any(a => a.activeBonusID is "FAST" or "WIDE")) continue;
                    var o = bonus.arrangements[0].offset;
                    var p = bonus.arrangements.SelectMany(a => new[] { a.offset.x - o.x, a.offset.y - o.y, a.item.id }).ToArray();
                    if (PatternPresent(m, start, p)) patterns.Add(p);
                }
                if (patterns.Count > 0)
                {
                    m.Patterns = patterns.ToArray();
                    m.Notes.Add(Tr("arrange.active_arrangement_bonus_pattern", patterns.Count));
                }
            }
        }
        catch (Exception e) { WarnOnce("配置奖励", e); }
        return m;
    }
}
