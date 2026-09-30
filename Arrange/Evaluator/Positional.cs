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
        void Specials()
        {
            int n = m.Items.Length;
            for (int i = 0; i < n; i++)
            {
                rowCat[i] = -1; boostRoot[i] = -1; wpCatN[i] = 0;
                extraCdr[i] = 0; autoRate[i] = 0; boost[i] = 0; enhanced[i] = false; costRed[i] = 0;
                arrFast[i] = false; arrWide[i] = false;
            }
            companionsInBadgeRow = 0;
            foreach (var sp in d.Specials)
            {
                int s = ItemSlot[sp.Item];
                if (s < 0) continue;
                int x = s % m.W, y = s / m.W;
                if (sp.Kind == SpecialKind.ByRow) rowCat[sp.Item] = sp.RowCats[y % sp.RowCats.Length];
            }
            foreach (var sp in d.Specials)
            {
                if (sp.Kind != SpecialKind.Booster) continue;
                int cur = sp.Item, guard = 0;
                while (guard++ < 16)
                {
                    int s = ItemSlot[cur];
                    var b = BoosterOf(cur);
                    if (s < 0 || b == null) break;
                    int t = ItemAt(s % m.W + b.Dx, s / m.W + b.Dy);
                    if (t < 0 || t == sp.Item) break;
                    if (BoosterOf(t) != null) { cur = t; continue; }
                    if (d.Extra[t].Attackable) boostRoot[sp.Item] = t;
                    break;
                }
                int root = boostRoot[sp.Item];
                if (root >= 0)
                {
                    int idx = IdxOf(sp.Item);
                    double v = SafeAt(sp.A, idx);
                    if (sp.Cond && m.Items[root].Rarity <= sp.MaxRarity) v += SafeAt(sp.B, idx);
                    boost[root] += v;
                }
            }
            foreach (var sp in d.Specials)
            {
                int s = ItemSlot[sp.Item];
                if (s < 0) continue;
                int x = s % m.W, y = s / m.W;
                bool on = ItemOn[sp.Item];
                switch (sp.Kind)
                {
                    case SpecialKind.WhitePaper:
                    {
                        int na = CatsOf(ItemAt(x - 1, y), tmpA), nb = CatsOf(ItemAt(x + 1, y), tmpB);
                        var buf = wpCats[sp.Item];
                        for (int a = 0; a < na; a++)
                        {
                            int hits = 1;
                            for (int b = 0; b < nb; b++) if (tmpB[b] == tmpA[a]) { hits++; break; }
                            if (hits >= sp.Match && wpCatN[sp.Item] < buf.Length) buf[wpCatN[sp.Item]++] = tmpA[a];
                        }
                        break;
                    }
                    case SpecialKind.Hourglass:
                        if (on) { int t = ItemAt(x + 1, y); if (t >= 0 && d.Extra[t].Magic) extraCdr[t] += SafeAt(sp.A, IdxOf(sp.Item)); }
                        break;
                    case SpecialKind.CostLeft:
                        if (on) { int t = ItemAt(x - 1, y); if (t >= 0 && d.Extra[t].Magic) costRed[t] += SafeAt(sp.A, IdxOf(sp.Item)); }
                        break;
                    case SpecialKind.AutoMagic:
                        if (on) { int t = ItemAt(x, y + 1); if (t >= 0 && d.Extra[t].BoltMagic && ItemOn[t]) autoRate[t] += 1.0 / Math.Max(0.1, SafeAt(sp.F, IdxOf(sp.Item))); }
                        break;
                    case SpecialKind.Telescope:
                        if (on)
                            for (int dx = -1; dx <= 1; dx++)
                                for (int dy = -1; dy <= 1; dy++)
                                {
                                    int t = (dx != 0 || dy != 0) ? ItemAt(x + dx, y + dy) : -1;
                                    if (t >= 0 && d.Extra[t].Planet) enhanced[t] = true;
                                }
                        break;
                    case SpecialKind.Badge:
                        if (on)
                            for (int cx = 0; cx < m.W; cx++)
                            {
                                int t = ItemAt(cx, y);
                                if (t >= 0 && d.Extra[t].Companion) companionsInBadgeRow++;
                            }
                        break;
                    case SpecialKind.ArrBonus:
                        ArrangementBonus(sp, x, y);
                        break;
                }
            }
            if (d.CatStatic != null) Array.Copy(d.CatStatic, catCount, d.CatStatic.Length);
            else Array.Clear(catCount, 0, catCount.Length);
            if (d.CatStatic != null)
                for (int i = 0; i < ItemSlot.Length; i++)
                    if (ItemSlot[i] < 0 && d.Extra[i]?.Cats != null)
                        foreach (var c in d.Extra[i].Cats) if (c >= 0 && c < catCount.Length) catCount[c] -= d.Extra[i].CatWeight;
            foreach (var sp in d.Specials)
            {
                switch (sp.Kind)
                {
                    case SpecialKind.ByRow:
                        if (rowCat[sp.Item] >= 0) catCount[rowCat[sp.Item]]++;
                        break;
                    case SpecialKind.Booster:
                        if (boostRoot[sp.Item] >= 0) { int k = CatsOf(boostRoot[sp.Item], tmpA); for (int j = 0; j < k; j++) catCount[tmpA[j]]++; }
                        break;
                    case SpecialKind.WhitePaper:
                        for (int j = 0; j < wpCatN[sp.Item]; j++) catCount[wpCats[sp.Item][j]]++;
                        break;
                }
            }
        }

        void ArrangementBonus(Special sp, int ox, int oy)
        {
            int n = Math.Min(sp.PatIds.Length, patItem.Length);
            for (int j = 0; j < n; j++) patEntry[j] = -1;
            for (int j = 0; j < n; j++)
            {
                int t = ItemAt(ox + sp.PatDx[j], oy + sp.PatDy[j]);
                if (t < 0) return;
                int e = m.Items[t].EntityId, k = -1;
                for (int q = 0; q < n; q++)
                {
                    if (sp.PatIds[q] != e) continue;
                    bool used = false;
                    for (int u = 0; u < j; u++) if (patEntry[u] == q) { used = true; break; }
                    if (!used) { k = q; break; }
                }
                if (k < 0) return;
                patItem[j] = t;
                patEntry[j] = k;
            }
            for (int j = 0; j < n; j++)
            {
                byte f = sp.PatFlag[patEntry[j]];
                if ((f & 1) != 0) arrFast[patItem[j]] = true;
                if ((f & 2) != 0) arrWide[patItem[j]] = true;
            }
        }

        Special BoosterOf(int item)
        {
            foreach (var sp in d.Specials) if (sp.Kind == SpecialKind.Booster && sp.Item == item) return sp;
            return null;
        }

        public int[] DynamicCatCounts()
        {
            var saved = d.CatStatic;
            d.CatStatic = null;
            Specials();
            var r = (int[])catCount.Clone();
            d.CatStatic = saved;
            return r;
        }

        void AddSpecialStats(double[] r, double[] a, ref double h)
        {
            foreach (var sp in d.Specials)
            {
                if (!ItemOn[sp.Item]) continue;
                int s = ItemSlot[sp.Item];
                if (s < 0) continue;
                int x = s % m.W, y = s / m.W, idx = IdxOf(sp.Item);
                switch (sp.Kind)
                {
                    case SpecialKind.NearLevel:
                    {
                        double sum = 0;
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                int t = (dx != 0 || dy != 0) ? ItemAt(x + dx, y + dy) : -1;
                                if (t >= 0 && m.Items[t].Kind >= KindCharm) sum += Math.Min(ItemLevel[t], m.Items[t].MaxLevel);
                            }
                        r[d.kAll] += Math.Floor(SafeAt(sp.F, idx) * sum);
                        break;
                    }
                    case SpecialKind.ByRow:
                        r[sp.RowKeys[y % sp.RowKeys.Length]] += SafeAt(sp.A, idx);
                        break;
                    case SpecialKind.FireIce:
                        if (x <= 2) { r[sp.KeyL] += SafeAt(sp.A, idx); r[sp.KeyR] += SafeAt(sp.B, idx); }
                        else { r[sp.KeyL] += SafeAt(sp.B, idx); r[sp.KeyR] += SafeAt(sp.A, idx); }
                        break;
                    case SpecialKind.QuickRow:
                    {
                        int count = 0;
                        for (int q = 0; q < Math.Min(6, m.N); q++) if (perm[q] >= 0 && m.Items[perm[q]].Kind >= KindCharm) count++;
                        double add = SafeAt(sp.A, idx) * count;
                        r[d.kElem[1]] += add; r[d.kElem[2]] += add; r[d.kElem[3]] += add;
                        break;
                    }
                }
            }
            int active = 0;
            for (int c = 0; c < d.Tiers.Length; c++)
            {
                var tiers = d.Tiers[c];
                if (tiers.Length == 0) continue;
                int count = catCount[c];
                if (count >= tiers[0].Count) active++;
                foreach (var t in tiers)
                {
                    if (t.Count > count) break;
                    foreach (var add in t.Adds)
                    {
                        if (add.Mode == 0) r[add.Key] += add.Values[0];
                        else if (add.Mode == 1) a[add.Key] += add.Values[0];
                        else h += add.Values[0];
                    }
                }
            }
            double comboBonus = d.kComboBonus >= 0 ? T(d.kComboBonus) : d.ComboBonus;
            if (comboBonus != 0) r[d.kAll] += comboBonus * active;
        }

        public void SubtractStartSpecials()
        {
            Stats();
            var r = new double[raw.Length];
            var a = new double[amp.Length];
            double h = 0;
            Specials();
            AddSpecialStats(r, a, ref h);
            for (int k = 0; k < r.Length; k++) { d.BaseRaw[k] -= r[k]; d.BaseAmp[k] -= a[k]; }
            d.BaseHighest -= h;
        }

        public int CompanionsInBadgeRow => companionsInBadgeRow;

        byte KindAt(int x, int y)
        {
            if (x < 0 || x >= m.W || y < 0) return KindEmpty;
            int i = y * m.W + x;
            return i < m.N ? kind[i] : KindEmpty;
        }
    }
}
