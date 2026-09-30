using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed partial class Evaluator
    {
        double WeaponHits()
        {
            var hm = d.Hits;
            double other = Single ? hm.Other : hm.OtherM, special = Single ? hm.Special : hm.SpecialM;
            return (Single ? hm.Swing : hm.SwingM) * SwingAs() + other + special * (SpecialScale() - 1);
        }

        double HitRate(DpsSource s)
        {
            switch (s.Kind)
            {
                case SrcKind.Proc:
                {
                    if (s.Item < 0 || !ItemOn[s.Item] || s.Summon) return 0;
                    int idx = IdxOf(s.Item);
                    double k = (s.K > 0 ? s.K : s.TheoryK > 0 ? s.TheoryK : s.Prior) * (Single ? 1 : s.MultiScale);
                    var rate = RateOf(s);
                    return k * (rate != null ? SafeAt(rate, idx) : 1) * RateFactor(s, idx, rateOnly: true);
                }
                case SrcKind.Magic:
                {
                    if (s.Item < 0 || !ItemOn[s.Item]) return 0;
                    var rate = RateOf(s);
                    double hits = (rate != null ? SafeAt(rate, IdxOf(s.Item)) : 1) * MultiCast();
                    if (d.Extra[s.Item].BoltMagic) hits *= boltFactor;
                    return magicRate[s.Item] * hits;
                }
                case SrcKind.Ability when s.Eco == EcoKind.DarkCloud:
                    return d.Eco != null ? CloudStrikes() : 0;
                case SrcKind.Ability when s.Eco == EcoKind.FlameSword:
                    return d.Eco != null ? FsSwords() : 0;
            }
            return 0;
        }

        readonly double[] elemHits = new double[5];
        double ElemHits(int elem) => elem is >= 0 and < 4 ? elemHits[elem] : elemHits[4];

        void CacheElemHits()
        {
            Array.Clear(elemHits, 0, 5);
            foreach (var s in d.Sources)
            {
                if (s.IsWeapon || s.Eco == EcoKind.Debuff) continue;
                double h = HitRate(s);
                if (h <= 0) continue;
                if (s.DmgElem is >= 0 and < 4) elemHits[s.DmgElem] += h;
                elemHits[4] += h;
            }
        }

        readonly double[] dbPresence = new double[6], dbStacks = new double[6], dbApply = new double[6];
        double[] dbR = new double[0];
        double freezeRate, debuffStackTotal;
        double elecPops;
        double ownDebuffObjects;

        double DebuffPresence(int type) => type is >= 0 and < 6 ? dbPresence[type] : 0;

        void DebuffState()
        {
            Array.Clear(dbPresence, 0, 6); Array.Clear(dbStacks, 0, 6); Array.Clear(dbApply, 0, 6);
            freezeRate = 0;
            debuffStackTotal = 0;
            elecPops = 0;
            ownDebuffObjects = 0;
            if (d.Debuffs.Count == 0) return;
            CacheElemHits();
            if (dbR.Length != d.Debuffs.Count) dbR = new double[d.Debuffs.Count];
            bool plasma = Plasma();
            for (int i = 0; i < d.Debuffs.Count; i++)
            {
                var di = d.Debuffs[i];
                double targets = DebuffTargets(di);
                double r = DebuffRate(di, targets, true);
                dbR[i] = r;
                if (r <= 0) continue;
                if (plasma && di.Type is DebuffType.Burn or DebuffType.Electric) continue;
                if (!plasma && di.Type == DebuffType.Plasma) continue;
                DebuffShape(di, out var max, out var add, out var dur, out _);
                double pres, stacks;
                switch (di.Type)
                {
                    case DebuffType.Electric:
                    case DebuffType.Plasma:
                    {
                        CycleStacks(r, dur, max, add, out _, out var integral, out var cycle);
                        pres = dur / cycle;
                        stacks = integral / cycle;
                        break;
                    }
                    case DebuffType.Frostbite:
                    {
                        double up = 1 - Math.Exp(-Math.Min(30, r * dur));
                        pres = up * Math.Max(0.01, 1 - Math.Min(0.99, add / max));
                        stacks = up * Math.Max(0, (max - 1) / 2.0);
                        freezeRate += targets * r * add / max * up;
                        break;
                    }
                    default:
                        pres = 1 - Math.Exp(-Math.Min(30, r * dur));
                        stacks = DebuffAvgStacks(r, dur, max, add);
                        if (DebuffResetRate(di.Type) is var q && q > 0)
                        {
                            stacks = ResetStacks(di.Type, r, max, add, stacks);
                            double x = r / q;
                            pres = Math.Min(pres, Math.Max(0, 1 - (1 - Math.Exp(-Math.Min(30, x))) / x));
                        }
                        break;
                }
                int t = (int)di.Type;
                dbPresence[t] = Math.Max(dbPresence[t], pres);
                dbStacks[t] = stacks;
                dbApply[t] = r * targets;
                debuffStackTotal += stacks * targets;
                ownDebuffObjects += pres;
                if (di.Type is DebuffType.Electric or DebuffType.Plasma)
                {
                    CycleStacks(r, dur, max, add, out _, out _, out var popCycle);
                    elecPops += targets * (1 / popCycle + r * Math.Min(100, Math.Max(0, T(di.kLuck))) / 100.0);
                }
            }
            dbPresence[(int)DebuffType.Burn] = Math.Max(dbPresence[(int)DebuffType.Burn], dbPresence[(int)DebuffType.Plasma]);
        }

        void DebuffLinkedStats()
        {
            if (debuffStackTotal > 0)
                for (int i = 0; i < m.Items.Length; i++)
                {
                    var adds = d.Adds[i];
                    if (adds == null || !ItemOn[i]) continue;
                    int idx = IdxOf(i);
                    foreach (var a in adds)
                        if (a.Mode == 5) raw[a.Key] += SafeAt(a.Values, idx) * debuffStackTotal;
                }
            foreach (var b in d.DynBuffs)
            {
                if (!ItemOn[b.Item]) continue;
                int idx = IdxOf(b.Item);
                double rate = b.Trigger == 0 ? freezeRate : dbApply[(int)DebuffType.Frostbite];
                if (rate <= 0) continue;
                double v = SafeAt(b.PerStack, idx) * BuffAvgStacks(rate, b.Dur, Math.Max(1, (int)SafeAt(b.MaxStack, idx)));
                if (b.Mode == 1) amp[b.Key] += v; else raw[b.Key] += v;
            }
        }

        double DebuffRate(DebuffInfo di, double targets, bool kappa)
        {
            double r;
            if (di.Type == DebuffType.Plasma)
            {
                r = di.Residual;
                if (d.BurnIdx >= 0) r += RawRate(d.Debuffs[d.BurnIdx], targets);
                if (d.ElecIdx >= 0) r += RawRate(d.Debuffs[d.ElecIdx], targets);
            }
            else r = RawRate(di, targets);
            return r * (kappa ? di.Kappa : 1);
        }

        double RawRate(DebuffInfo di, double targets)
        {
            double r = di.Residual, tg = Single ? 1 : Math.Max(0.5, targets);
            var hm = d.Hits;
            foreach (var a in di.Appliers)
            {
                switch (a.Kind)
                {
                    case 0:
                    {
                        if (a.Cat < 0 || a.Cat >= catCount.Length || catCount[a.Cat] < a.Activate) break;
                        double cd = a.Cooldown / (catCount[a.Cat] >= a.HasteCount ? Pct(a.Haste) : 1);
                        double hits = (a.Elem == hm.WeaponElem ? WeaponHits() : 0) + (a.Elem is >= 0 and < 4 ? ElemHits(a.Elem) : 0);
                        double h = hits / tg;
                        if (h > 0) r += 1 / (cd + 1 / h);
                        break;
                    }
                    case 1:
                        r += Math.Min(100, Math.Max(0, T(a.Key))) / 100.0 * (WeaponHits() + ElemHits(-1)) / tg;
                        break;
                    case 2:
                    {
                        if (a.Item >= 0 && !ItemOn[a.Item]) break;
                        if (a.Key >= 0 && T(a.Key) <= 0) break;
                        int idx = a.Item >= 0 ? IdxOf(a.Item) : 0;
                        double per = a.Src >= 0 && a.Src < d.Sources.Length
                            ? HitRate(d.Sources[a.Src]) * (a.ChanceTable != null ? SafeAt(a.ChanceTable, idx) : 1)
                            : SafeAt(a.PerLevel, idx);
                        r += a.PerTarget ? per : per / tg;
                        break;
                    }
                    case 3:
                        if (a.Item >= 0 && ItemOn[a.Item] && d.Eco != null && d.Sources.Any(x => x.Eco == EcoKind.DarkCloud)) r += CloudStrikes() / tg;
                        break;
                    case 4:
                        r += a.Chance * WeaponHits() / tg;
                        break;
                    case 5:
                        r += a.Chance * (Single ? hm.Special : hm.SpecialM) * SpecialScale() / tg;
                        break;
                }
            }
            return r;
        }

        double DebuffTargets(DebuffInfo di) => Single ? 1 : di.Targets > 0 ? di.Targets : DotTargetShare * (d.Eco != null ? d.Eco.Enemies : PlayEnemies);

        bool Plasma() => T(d.kPlasma) > 0;

        void DebuffShape(DebuffInfo di, out double max, out double add, out double dur, out double d0)
        {
            double bonus = T(d.kDebuffDur);
            add = 1;
            d0 = di.Duration0;
            switch (di.Type)
            {
                case DebuffType.Burn:
                    max = 2 + T(di.kStack);
                    add = 1 + Math.Max(0, T(di.kAdd));
                    dur = di.Duration0 * Pct(bonus + T(di.kDur));
                    break;
                case DebuffType.Electric:
                    max = 2 + T(di.kStack);
                    d0 = Math.Max(0.1, di.Duration0 - T(di.kQuick) / 10.0);
                    dur = d0 * Pct(bonus);
                    break;
                case DebuffType.Plasma:
                    max = 2 + T(di.kStack) + T(di.kStack2);
                    add = 1 + Math.Max(0, T(di.kAdd));
                    d0 = Math.Max(0.1, di.Duration0 - T(di.kQuick) / 10.0);
                    dur = d0 * Pct(bonus + T(di.kDur));
                    break;
                case DebuffType.Frostbite:
                    max = Math.Max(1, 5 - T(di.kFreezeTh));
                    dur = di.Duration0 * Pct(bonus);
                    foreach (var (item, pct) in di.ExtraStack)
                        if (ItemOn[item]) add += Math.Min(100, Math.Max(0, SafeAt(pct, IdxOf(item)))) / 100.0;
                    break;
                case DebuffType.Poison:
                    max = 1 + T(di.kStack);
                    dur = di.Duration0 * Pct(bonus);
                    break;
                default:
                    max = 4 + T(di.kStack);
                    dur = di.Duration0 * Pct(bonus);
                    break;
            }
            max = Math.Max(1, max);
            dur = Math.Max(0.05, dur);
        }
    }
}
