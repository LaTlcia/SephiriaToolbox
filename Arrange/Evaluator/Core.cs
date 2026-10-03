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
    sealed partial class Evaluator
    {
        readonly ArrModel m;
        readonly DpsModel d;
        readonly int[] lvl, mul, dis, ign;
        readonly byte[] kind;
        readonly int[] uniqWinner, uniqClamp;
        public readonly int[] ItemLevel;
        public readonly bool[] ItemOn;
        public int Enabled, Disabled, SumClamp;
        public double LevelScore;
        readonly double[] raw, amp;
        readonly double[] elem = new double[4], elem0 = new double[4], stat = new double[4], bonus = new double[4], selPct = new double[4];
        readonly double[] modMul = new double[6], modCrit = new double[6], modCritDmg = new double[6];
        double modOwn = 1;
        double boltFactor = 1;
        readonly int[] sel = new int[4];
        double highest;
        public readonly int[] ItemSlot;
        int[] perm;
        readonly int[] catCount, rowCat, boostRoot;
        readonly int[][] wpCats;
        readonly int[] wpCatN;
        readonly double[] extraCdr, autoRate, boost, magicRate, costRed;
        readonly double[] manualRate, boltRate, dupRate;
        readonly bool[] boltSet;
        readonly bool[] enhanced;
        readonly bool[] arrFast, arrWide;
        readonly int[] patItem = new int[8], patEntry = new int[8];
        int companionsInBadgeRow;
        public bool Single;

        public Evaluator(ArrModel model)
        {
            m = model;
            d = model.Dps;
            Single = d != null && d.Single;
            lvl = new int[m.N]; mul = new int[m.N]; dis = new int[m.N]; ign = new int[m.N];
            kind = new byte[m.N];
            uniqWinner = new int[Math.Max(1, m.UniqueGroups)];
            uniqClamp = new int[Math.Max(1, m.UniqueGroups)];
            ItemLevel = new int[m.Items.Length];
            ItemOn = new bool[m.Items.Length];
            ItemSlot = new int[m.Items.Length];
            if (d != null)
            {
                int n = m.Items.Length;
                raw = new double[d.Keys.Length]; amp = new double[d.Keys.Length];
                catCount = new int[Math.Max(1, d.CatNames.Length)];
                rowCat = new int[n]; boostRoot = new int[n];
                wpCats = new int[n][]; wpCatN = new int[n];
                foreach (var sp in d.Specials) if (sp.Kind == SpecialKind.WhitePaper) wpCats[sp.Item] = new int[8];
                extraCdr = new double[n]; autoRate = new double[n]; boost = new double[n]; magicRate = new double[n]; costRed = new double[n];
                manualRate = new double[n]; boltRate = new double[n]; boltSet = new bool[n]; dupRate = new double[n];
                enhanced = new bool[n];
                arrFast = new bool[n]; arrWide = new bool[n];
            }
        }

        int ItemAt(int x, int y)
        {
            int s = SlotOf(x, y, m.W, m.N);
            return s < 0 ? -1 : perm[s];
        }

        int IdxOf(int item) => ItemOn[item] ? Math.Min(Math.Max(ItemLevel[item], 0), m.Items[item].MaxLevel) : 0;

        int CatsOf(int item, int[] buf)
        {
            if (item < 0) return 0;
            var ex = d.Extra[item];
            if (ex.Cats != null)
            {
                int k = 0;
                foreach (var c in ex.Cats) if (k < buf.Length) buf[k++] = c;
                return k;
            }
            if (rowCat[item] >= 0) { buf[0] = rowCat[item]; return 1; }
            if (boostRoot[item] >= 0) return CatsOf(boostRoot[item], buf);
            return 0;
        }

        readonly int[] tmpA = new int[8], tmpB = new int[8];

        void Apply(Geom g, bool ignoreConds = false)
        {
            if (g == null || g.Blocked) return;
            if (!ignoreConds)
            foreach (var c in g.Conds)
            {
                if (c.Slot < 0) return;
                byte k = kind[c.Slot];
                if (c.Type == CondAnyItem ? k == KindEmpty : k < KindCharm) return;
            }
            foreach (var e in g.Effs)
            {
                switch (e.Type)
                {
                    case EffLevel: lvl[e.Slot] += e.Param; break;
                    case EffDisable: dis[e.Slot]++; break;
                    case EffIgnore: ign[e.Slot]++; break;
                    case EffMultiply: mul[e.Slot] += e.Param; break;
                }
            }
        }

        bool CritOk(ArrItem it, int s)
        {
            int x = s % m.W, y = s / m.W;
            switch (it.Crit)
            {
                case Crit.None: return true;
                case Crit.Const: return it.CritConst;
                case Crit.Top: return y == 0;
                case Crit.Bottom: return s >= m.N - 6;
                case Crit.SideEnd: return x == 0 || x == 5;
                case Crit.Inside: return !(x <= 0 || y <= 0 || x >= m.W - 1) && s + 7 <= m.N - 1;
                case Crit.Outlined: return x <= 0 || y <= 0 || x >= m.W - 1 || s >= m.N - 6;
                case Crit.BothSideCharm:
                    return x > 0 && x < m.W - 1 && KindAt(x - 1, y) >= KindCharm && KindAt(x + 1, y) >= KindCharm;
                case Crit.BothSidesEmpty:
                {
                    int rem = m.N % m.W;
                    return x > 0 && x < m.W - 1 && (rem == 0 || y < m.H - 1 || x < rem - 1)
                        && KindAt(x - 1, y) == KindEmpty && KindAt(x + 1, y) == KindEmpty;
                }
                case Crit.NeighborsFull:
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if ((dx != 0 || dy != 0) && KindAt(x + dx, y + dy) == KindEmpty) return false;
                    return true;
                case Crit.NearMagic:
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if ((dx != 0 || dy != 0) && KindAt(x + dx, y + dy) == KindMagic) return true;
                    return false;
            }
            return true;
        }

        public void Levels(int[] perm, int[] rot)
        {
            this.perm = perm;
            for (int i = 0; i < ItemSlot.Length; i++) { ItemSlot[i] = -1; ItemOn[i] = false; ItemLevel[i] = 0; }
            for (int s = 0; s < perm.Length && s < m.N; s++) if (perm[s] >= 0) ItemSlot[perm[s]] = s;
            int n = m.N;
            Array.Copy(m.BaseLevel, lvl, n);
            Array.Copy(m.BaseMult, mul, n);
            Array.Copy(m.BaseDisable, dis, n);
            Array.Copy(m.BaseIgnore, ign, n);
            for (int s = 0; s < n; s++) kind[s] = perm[s] < 0 ? KindEmpty : m.Items[perm[s]].Kind;
            for (int s = 0; s < n; s++)
            {
                int i = perm[s];
                if (i < 0) continue;
                var it = m.Items[i];
                if (it.IsTablet) Apply(m.TabletGeom[it.TabletIndex][s][rot[it.TabletIndex]]);
            }
            foreach (var g in m.Engravings) Apply(g);
            int tabletCount = m.TabletItem.Length;
            if (rot.Length >= 2 * tabletCount)
                for (int t = 0; t < tabletCount; t++)
                {
                    int stamp = rot[tabletCount + t];
                    if (stamp >= 0) Apply(m.TabletGeom[t][stamp][rot[t]], ignoreConds: true);
                }

            int enabled = 0, disabled = 0, sumClamp = 0, sumLevel = 0, overflow = 0, negative = 0;
            for (int g = 0; g < m.UniqueGroups; g++) uniqWinner[g] = -1;
            for (int s = 0; s < n; s++)
            {
                int i = perm[s];
                if (i < 0) continue;
                var it = m.Items[i];
                if (it.Kind < KindCharm) continue;
                int level = lvl[s] + it.Enchant;
                if (mul[s] != 0) level *= mul[s];
                bool on = dis[s] <= 0 && level >= 0 && it.WeaponOk && (ign[s] > 0 || CritOk(it, s));
                int clamp = Mathf.Clamp(level, 0, it.MaxLevel);
                if (on && it.UniqueGroup >= 0)
                {
                    int g = it.UniqueGroup, prev = uniqWinner[g];
                    if (prev < 0) { uniqWinner[g] = i; uniqClamp[g] = clamp; }
                    else if (clamp > uniqClamp[g])
                    {
                        enabled--; disabled++; sumClamp -= uniqClamp[g];
                        ItemOn[prev] = false;
                        uniqWinner[g] = i; uniqClamp[g] = clamp;
                    }
                    else on = false;
                }
                sumLevel += level;
                if (level < 0) negative -= level;
                if (level > it.MaxLevel) overflow += level - it.MaxLevel;
                if (on) { enabled++; sumClamp += clamp; }
                else disabled++;
                ItemLevel[i] = level;
                ItemOn[i] = on;
            }
            Enabled = enabled;
            Disabled = disabled;
            SumClamp = sumClamp;
            LevelScore = sumClamp * 10000.0 + enabled * 1000.0 + sumLevel * 10.0 + overflow - disabled * 750.0 - negative * 250.0;
        }

        double T(int k)
        {
            if (k < 0) return 0;
            double r = raw[k];
            return r == 0 ? 0 : Math.Truncate(r * (100 + amp[k]) / 100.0);
        }

        void ElemPass(bool withBonus, double[] outE)
        {
            for (int e = 0; e < 4; e++)
            {
                int k = d.kElem[e];
                double r = raw[k] + (withBonus ? bonus[e] : 0);
                stat[e] = r == 0 ? 0 : Math.Truncate(r * (100 + amp[k]) / 100.0);
            }
            for (int e = 0; e < 4; e++)
            {
                sel[e] = -1;
                selPct[e] = 0;
                for (int t = 0; t < 4; t++)
                {
                    if (t == e) continue;
                    double p = T(d.kConv[e, t]);
                    if (p > selPct[e]) { selPct[e] = p; sel[e] = t; }
                }
            }
            for (int e = 0; e < 4; e++)
            {
                double v = stat[e];
                if (v > 20 && sel[e] >= 0) v = 20;
                for (int i = 0; i < 4; i++)
                {
                    if (i == e || sel[i] != e) continue;
                    double x = stat[i] - 20;
                    if (x > 0) v += Math.Truncate(x * selPct[i] / 100.0);
                }
                outE[e] = v;
            }
        }

        int TabletsInBag()
        {
            int n = 0;
            foreach (int t in m.TabletItem) if (t >= 0 && t < ItemSlot.Length && ItemSlot[t] >= 0) n++;
            return n;
        }

        void Stats()
        {
            Array.Copy(d.BaseRaw, raw, raw.Length);
            Array.Copy(d.BaseAmp, amp, amp.Length);
            highest = d.BaseHighest;
            for (int i = 0; i < m.Items.Length; i++)
            {
                var adds = d.Adds[i];
                if (adds == null || !ItemOn[i]) continue;
                int idx = Math.Min(Math.Max(ItemLevel[i], 0), m.Items[i].MaxLevel);
                foreach (var a in adds)
                {
                    if (a.Mode is 3 or 5) continue;
                    int v = a.Values[Math.Min(idx, a.Values.Length - 1)];
                    if (a.Mode == 6) { raw[a.Key] += v * TabletsInBag(); continue; }
                    if (a.Mode == 0) raw[a.Key] += v;
                    else if (a.Mode == 1) amp[a.Key] += v;
                    else highest += v;
                }
            }
            Specials();
            AddSpecialStats(raw, amp, ref highest);
            for (int i = 0; i < m.Items.Length; i++)
            {
                var adds = d.Adds[i];
                if (adds == null || !ItemOn[i]) continue;
                int idx = Math.Min(Math.Max(ItemLevel[i], 0), m.Items[i].MaxLevel);
                foreach (var a in adds)
                    if (a.Mode == 3 && a.Div != null)
                        raw[a.Key] += SafeAt(a.Values, idx) * Math.Floor(Math.Max(0, T(a.Src)) / SafeAt(a.Div, idx));
            }
            UpdateMagicRates();
            MagicBuffStats();
            specialScale = SpecialScaleNow();
            DebuffState();
            DebuffLinkedStats();
            for (int i = 0; i < 6; i++) modMul[i] = 1;
            modOwn = 1;
            Array.Clear(modCrit, 0, 6); Array.Clear(modCritDmg, 0, 6);
            boltFactor = 1;
            foreach (var md in d.Mods)
            {
                if (!ItemOn[md.Item]) continue;
                int idx = Math.Min(Math.Max(ItemLevel[md.Item], 0), m.Items[md.Item].MaxLevel);
                int scope = md.Elem >= 0 ? 2 + md.Elem : md.Direct ? 1 : 0;
                double v = SafeAt(md.Values, idx);
                if (md.WideMul != 1 && arrWide[md.Item]) v *= md.WideMul;
                if (md.DebuffUp >= 0)
                {
                    double own = DebuffPresence(md.DebuffUp), other = Single ? d.OtherDebuff[md.DebuffUp] : 0;
                    v *= 1 - (1 - own) * (1 - other);
                }
                if (md.PerDebuffCount) v *= ownDebuffObjects + d.OtherDebuffObjects;
                if (md.Kind == 0 && md.OwnOnly && scope == 0) modOwn *= Math.Max(0, 1 + v / 100.0);
                else if (md.Kind == 0) modMul[scope] *= Math.Max(0, 1 + v / 100.0);
                else if (md.Kind == 1) modCrit[scope] += v;
                else if (md.Kind == 2) modCritDmg[scope] += v;
                else
                {
                    double side = !Single || (d.kBoltHoming >= 0 && T(d.kBoltHoming) > 0) ? 1 : d.BoltSideShare;
                    boltFactor *= Math.Max(0, v) * (1 + 2 * side);
                }
            }
            ApplyCondDamage();
            ElemPass(false, elem0);
            if (highest >= 0)
            {
                double mx = Math.Max(Math.Max(elem0[0], elem0[1]), Math.Max(elem0[2], elem0[3]));
                for (int e = 0; e < 4; e++) bonus[e] = elem0[e] == mx && elem0[e] > 0 ? highest : 0;
                ElemPass(true, elem);
            }
            else Array.Copy(elem0, elem, 4);
        }

        double StatOf(string key)
        {
            int e = Array.IndexOf(ElemKeys, key);
            if (e >= 0) return elem[e];
            return 0;
        }

        double ElemVal(int i) => i < 4 ? elem[i] : Math.Max(Math.Max(elem[0], elem[1]), Math.Max(elem[2], elem[3]));

        public double Dps(ArrEval detail = null)
        {
            Stats();
            UpdateMagicRates();
            double weighted = d.Other * d.OtherWeight, total = d.Other;
            for (int i = 0; i < d.Sources.Length; i++)
            {
                var s = d.Sources[i];
                double v = s.K * F(s);
                if (Single && d.BossToughness > 0 && v > 0) v = Toughness(s, v);
                weighted += s.Weight * v;
                total += v;
                if (detail != null) detail.Source[i] = v;
            }
            if (detail != null) { detail.Total = total; detail.Weighted = weighted; }
            return weighted;
        }

        public void RawAll(double[] f)
        {
            Stats();
            UpdateMagicRates();
            for (int i = 0; i < d.Sources.Length; i++) f[i] = F(d.Sources[i]);
        }

        public ArrEval Detail(int[] perm, int[] rot)
        {
            Levels(perm, rot);
            var r = new ArrEval
            {
                Level = (int[])ItemLevel.Clone(),
                On = (bool[])ItemOn.Clone(),
                Enabled = Enabled,
                Charms = Enabled + Disabled,
                EffectiveLevels = SumClamp,
                Score = LevelScore
            };
            if (d != null)
            {
                r.Source = new double[d.Sources.Length];
                Dps(r);
            }
            return r;
        }
    }
}
