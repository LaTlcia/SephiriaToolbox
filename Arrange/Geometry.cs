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
    static int SlotOf(int x, int y, int w, int n)
    {
        if (x < 0 || x >= w || y < 0) return -1;
        int i = y * w + x;
        return i < n ? i : -1;
    }

    static Geom BuildGeom(TabletRule rule, int x, int y, int rot, int w, int h, int n)
    {
        var origin = new ItemPosition((sbyte)x, (sbyte)y);
        var conds = StoneTablet.ParseQuery(rule.Condition ?? "", w, h, n, origin, rot, out _);
        var cl = new List<Cond>();
        bool hasPlaced = false, placedHit = false;
        foreach (var md in conds)
        {
            var c = new StoneTablet.AdditionCriteriaData(md);
            switch (c.effectType)
            {
                case StoneTablet.CriteriaType.AnyItem:
                    cl.Add(new Cond { Slot = SlotOf(md.position.x, md.position.y, w, n), Type = CondAnyItem });
                    break;
                case StoneTablet.CriteriaType.OnlyCharm:
                    cl.Add(new Cond { Slot = SlotOf(md.position.x, md.position.y, w, n), Type = CondCharm });
                    break;
                case StoneTablet.CriteriaType.Placed:
                    hasPlaced = true;
                    if (md.position.x == x && md.position.y == y) placedHit = true;
                    break;
                default:
                    placedHit = true;
                    break;
            }
        }
        var el = new List<Eff>();
        foreach (var md in StoneTablet.ParseQuery(rule.Effect ?? "", w, h, n, origin, rot, out _))
        {
            int slot = SlotOf(md.position.x, md.position.y, w, n);
            if (slot < 0) continue;
            var e = new StoneTablet.AdditionEffectData(md);
            byte type = e.effectType switch
            {
                StoneTablet.EffectType.IncreaseConstLevel => EffLevel,
                StoneTablet.EffectType.Disable => EffDisable,
                StoneTablet.EffectType.IgnoreCriteria => EffIgnore,
                StoneTablet.EffectType.MultiplyConstLevel => EffMultiply,
                _ => (byte)0
            };
            if (type != 0) el.Add(new Eff { Slot = slot, Type = type, Param = e.levelParam });
        }
        return new Geom { Blocked = hasPlaced && !placedHit, Conds = cl.ToArray(), Effs = el.ToArray() };
    }

    static void BuildGeoms(ArrModel m)
    {
        if (m.TabletGeom != null) return;
        var geom = new Geom[m.TabletRules.Length][][];
        for (int t = 0; t < m.TabletRules.Length; t++)
        {
            bool rotatable = m.Items[m.TabletItem[t]].Rotatable;
            geom[t] = new Geom[m.N][];
            for (int s = 0; s < m.N; s++)
            {
                geom[t][s] = new Geom[4];
                for (int r = 0; r < 4; r++)
                    if (rotatable || r == m.StartRot[t])
                        geom[t][s][r] = BuildGeom(m.TabletRules[t], s % m.W, s / m.W, r, m.W, m.H, m.N);
            }
        }
        m.Engravings = m.EngravingRules.Select(e => BuildGeom(e, e.X, e.Y, e.Rotation, m.W, m.H, m.N)).ToArray();
        m.TabletGeom = geom;
    }

    static Crit CritOf(Charm_Basic c)
    {
        if (c.criteria == null) return Crit.None;
        return c.criteria.GetType().Name switch
        {
            "CharmActivateCriteria_TopInInventory" => Crit.Top,
            "CharmActivateCriteria_BottomInInventory" => Crit.Bottom,
            "CharmActivateCriteria_SideEnd" => Crit.SideEnd,
            "CharmActivateCriteria_Inside" => Crit.Inside,
            "CharmActivateCriteria_Outlined" => Crit.Outlined,
            "CharmActivateCriteria_BothSideCharm" => Crit.BothSideCharm,
            "CharmActivateCriteria_BothSidesAreEmpty" => Crit.BothSidesEmpty,
            "CharmActivateCriteria_NeighborsAreFull" => Crit.NeighborsFull,
            "CharmActivateCriteria_Near8MagicBook" => Crit.NearMagic,
            _ => Crit.Const
        };
    }
}
