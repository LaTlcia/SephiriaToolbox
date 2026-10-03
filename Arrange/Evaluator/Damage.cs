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
        double CharmBonus() => d.kCharmDmg >= 0 ? T(d.kCharmDmg) : 0;

        double ModMul(DpsSource s) { int e = DmgElemOf(s); return (modMul[0] * (s.Summon ? 1 : modOwn) * (s.Direct ? modMul[1] : 1) * (e is >= 0 and < 4 ? modMul[2 + e] : 1) - 1) * 100; }
        double ModCrit(DpsSource s) { int e = DmgElemOf(s); return modCrit[0] + (s.Direct ? modCrit[1] : 0) + (e is >= 0 and < 4 ? modCrit[2 + e] : 0); }
        double ModCritDmg(DpsSource s) { int e = DmgElemOf(s); return modCritDmg[0] + (s.Direct ? modCritDmg[1] : 0) + (e is >= 0 and < 4 ? modCritDmg[2 + e] : 0); }

        double CritAddOf(DpsSource s) => (s.MagicCrit ? T(d.kMCrit) / 100.0 : 0) + (s.WeaponCrit ? T(d.kWCrit) / 100.0 : 0);
        double CritDmgAddOf(DpsSource s) => (s.MagicCrit ? T(d.kMCritDmg) : 0) + (s.WeaponCrit ? T(d.kWCritDmg) : 0);

        double CF(double chance, double rate, bool exec) =>
            CritFactor(Single && d.BossCritResist > 0 ? chance * Math.Max(0, 1 - d.BossCritResist / 100.0) : chance, rate, exec);

        int DmgElemOf(DpsSource s)
        {
            if (s.Debuff >= 0 && s.Debuff < d.Debuffs.Count)
            {
                var di = d.Debuffs[s.Debuff];
                if (di.Type is DebuffType.Burn or DebuffType.Plasma && T(di.kBlue) > 0 && elem[2] > elem[1]) return di.Type == DebuffType.Burn ? 2 : s.DmgElem;
            }
            return s.DmgElem;
        }

        double ResistMul(DpsSource s)
        {
            if (!Single) return 1;
            int e = DmgElemOf(s);
            if (e < 0 || e > 3 || d.BossResist[e] <= 0) return 1;
            return 1 - Math.Min(99, d.BossResist[e]) / 100.0;
        }

        double AllFactor(DpsSource s)
        {
            double a = T(d.kAll) + T(d.kElite) * (Single ? 1 : EliteShare) + UnlimitedCombo();
            if (s.Direct) a += T(d.kWdbDash) * T(d.kDashCount);
            if (s.Summon)
            {
                a += T(d.kFollowerDmg);
                if (T(d.kAdvNego) >= 1) a += T(d.kNego);
            }
            double v = Pct(a) * DefenseFactor(s) * d.ConstMul;
            if (T(d.kGoldHand) > 0) v *= 1 + (T(d.kGoldHandUnl) > 0 ? d.GoldHandPctUnl : d.GoldHandPct) / 100.0;
            if (T(d.kDefToAtk) > 0)
            {
                double def = T(d.kDef);
                v *= 1 + (def > 0 ? Math.Log(def / 40.0 + 1) * 0.445 : def / 100.0);
            }
            return v;
        }

        double UnlimitedCombo()
        {
            double per = T(d.kUnlimited);
            if (per <= 0 || d.CatHighest.Length == 0) return 0;
            double sum = 0;
            for (int c = 0; c < d.CatHighest.Length && c < catCount.Length; c++)
                if (d.CatHighest[c] > 0 && catCount[c] > d.CatHighest[c]) sum += catCount[c] - d.CatHighest[c];
            return per * sum;
        }

        double DefenseFactor(DpsSource s)
        {
            double def = Single ? (d.StageDefKnown ? d.StageDef : d.EnemyDef) + d.BossDefBonus : d.EnemyDef;
            if (def == 0) return 1;
            double r = def > 0 ? Math.Min(1, Math.Log(def / 40.0 + 1) * 0.445) : def / 100.0;
            bool flameSpear = s.Kind == SrcKind.WeaponSpecial && d.Weapon.Qs != null && d.Weapon.Qs.FlameSpear;
            double ignore = T(d.kIgnoreDef) + (s.Eco == EcoKind.FlameSword || flameSpear ? T(d.kFsIgnore) : 0);
            if (s.Kind == SrcKind.WeaponBasic && d.Weapon.Eclipse && d.Eco != null && T(d.kFsIgnore) != 0)
            {
                EclipseState(out _, out var share);
                double ig2 = ignore + T(d.kFsIgnore);
                double r1 = ignore > 0 ? r * (1 - ignore / 100.0) : r, r2 = ig2 > 0 ? r * (1 - ig2 / 100.0) : r;
                return Math.Max(0.01, 1 - ((1 - share) * r1 + share * r2));
            }
            if (ignore > 0) r *= 1 - ignore / 100.0;
            return Math.Max(0.01, 1 - r);
        }

        double Toughness(DpsSource s, double v)
        {
            double hits = HitsPerSec(s);
            return hits > 0 ? Math.Max(hits, v - d.BossToughness * hits) : v;
        }

        double HitsPerSec(DpsSource s)
        {
            switch (s.Kind)
            {
                case SrcKind.WeaponBasic: return d.Hits.Swing * SwingAs();
                case SrcKind.WeaponSpecial: return d.Hits.Special * specialScale;
                case SrcKind.WeaponDash: return Math.Max(0, d.Hits.Other - d.Hits.Special);
                case SrcKind.Rider: return WeaponHits();
                case SrcKind.Magic:
                case SrcKind.Proc: return HitRate(s);
            }
            switch (s.Eco)
            {
                case EcoKind.DarkCloud: return d.Eco != null ? CloudStrikes() : 0;
                case EcoKind.FlameSword: return d.Eco != null ? FsSwords() : 0;
                case EcoKind.FlameGround: return d.Eco != null ? GroundHits() : 0;
                case EcoKind.TrueDamage: return WeaponHits();
                case EcoKind.Debuff: return DebuffHits(s);
            }
            return 0;
        }

        double StatVal(Slope sl) => sl.Elem >= 0 ? elem[sl.Elem] : T(sl.Key);

        double Base(DpsSource s, int idx)
        {
            double v = s.Table != null ? SafeAt(s.Table, idx) : 1;
            if (s.Slopes != null)
                foreach (var sl in s.Slopes)
                {
                    if (!sl.Step) v += SafeAt(sl.PerLevel, idx) * (StatVal(sl) - sl.S0);
                    else if ((StatVal(sl) > 0) != sl.BaseOn) v += (sl.BaseOn ? -1 : 1) * SafeAt(sl.PerLevel, idx);
                }
            if (v < 0) v = 0;
            if (s.MpMulKey >= 0 || s.MpFlatKey >= 0) v = ChargeMp(s, v);
            if (s.ElemIdx >= 0) v *= ElemVal(s.ElemIdx);
            if (s.MulKeys != null) foreach (var k in s.MulKeys) v *= Pct(T(k));
            var rate = RateOf(s);
            if (rate != null) v *= SafeAt(rate, idx);
            return v;
        }

        public void ComputeSlopeBases()
        {
            if (d.StatDelta != null) foreach (var kv in d.StatDelta) d.BaseRaw[kv.Key] -= kv.Value;
            Stats();
            foreach (var s in d.Sources)
                if (s.Slopes != null)
                    foreach (var sl in s.Slopes) sl.S0 = StatVal(sl);
            if (d.StatDelta != null) foreach (var kv in d.StatDelta) d.BaseRaw[kv.Key] += kv.Value;
        }

        double F(DpsSource s)
        {
            if (s.GateItems != null)
            {
                bool any = false;
                foreach (var it in s.GateItems) if (ItemOn[it]) { any = true; break; }
                if (!any) return 0;
            }
            double v = FCore(s) * ResistMul(s);
            if (s.Boost != null && v != 0)
            {
                double b = 0;
                foreach (var f in s.Boost) if (!(Single && f.MultiOnly)) b += FeederRate(f);
                v *= 1 + b;
            }
            return v;
        }

        double FCore(DpsSource s)
        {
            if (s.GateCat >= 0 && catCount[s.GateCat] < s.GateCount) return 0;
            double all = AllFactor(s);
            bool exec = T(d.kExec) > 0;
            double crit = T(d.kCrit) / 100.0, critDmg = 50 + T(d.kCritDmg);
            if (s.Summon)
            {
                double contrib = T(d.kFollowerCritContrib);
                crit = T(d.kFollowerCrit) / 100.0 + (contrib > 0 ? crit * contrib / 100.0 : 0);
                critDmg = 50 + (contrib > 0 ? T(d.kCritDmg) * contrib / 100.0 : 0);
                exec = exec && contrib > 0;
            }
            if (s.Eco != EcoKind.None) return EcoF(s, all, crit, critDmg, exec);
            switch (s.Kind)
            {
                case SrcKind.WeaponBasic:
                case SrcKind.WeaponSpecial:
                case SrcKind.WeaponDash:
                {
                    double rate = critDmg + T(d.kWCritDmg) + ModCritDmg(s);
                    double wAmp = T(d.kWCritAmp);
                    if (wAmp > 0) rate += Math.Truncate(rate * wAmp / 100.0);
                    double weaponDmg = WeaponDamage(s, out var eclipse);
                    double chance = crit + T(d.kWCrit) / 100.0 + ModCrit(s) + GsCritAdd(s), cf = CF(chance, rate, exec);
                    if (eclipse > 0 && d.Eco != null)
                        cf = (1 - eclipse) * cf + eclipse * CF(chance + T(d.Eco.kFsCrit), rate + T(d.Eco.kFsCritDmg), exec);
                    double v = ElemFor(s.Formula, s.Element, elem) * Pct(T(d.kWdb)) * Pct(T(d.kFwd)) * all * Pct(ModMul(s)) * cf * weaponDmg;
                    if (s.Kind == SrcKind.WeaponBasic && !s.AsDash)
                    {
                        v *= Pct(T(d.kBad));
                        if (s.FinalShare > 0)
                        {
                            double lb = d.kLastBasic >= 0 ? T(d.kLastBasic) : 0, fd = d.kFinalDmg >= 0 ? T(d.kFinalDmg) : 0, fc = d.kFinalCrit >= 0 ? T(d.kFinalCrit) : 0;
                            if (lb != 0 || fd != 0 || fc != 0)
                            {
                                double cf2 = fc != 0 ? CF(chance + fc, rate, exec) : cf;
                                v *= 1 - s.FinalShare + s.FinalShare * Pct(lb) * Pct(fd) * (cf > 0 ? cf2 / cf : 1);
                            }
                        }
                    }
                    else if (s.Kind == SrcKind.WeaponSpecial) v *= Pct(T(d.kSad)) * SpecialScale();
                    else v *= Pct(T(d.kDad)) * (d.kDashDmg >= 0 ? Pct(T(d.kDashDmg)) : 1);
                    if (s.Kind == SrcKind.WeaponSpecial) v *= SpecialAs(s.SpecialAs);
                    else if (s.UseAttackSpeed) v *= WeaponRate(s);
                    return v;
                }
                case SrcKind.Magic:
                {
                    if (!ItemOn[s.Item]) return 0;
                    int idx = Math.Min(Math.Max(ItemLevel[s.Item], 0), m.Items[s.Item].MaxLevel);
                    double baseDmg = 1, power = 1 + (boost[s.Item] + CharmBonus()) / 100.0;
                    if (s.Default != null || s.Percent != null)
                    {
                        double related = s.RelKey != null ? (Array.IndexOf(ElemKeys, s.RelKey) >= 0 ? StatOf(s.RelKey) : 0) : 0;
                        baseDmg = SafeAt(s.Default, idx) + related * SafeAt(s.Percent, idx) / 100.0 * (s.PowerOnStat ? power : 1);
                    }
                    var rate = RateOf(s);
                    double hits = (rate != null ? SafeAt(rate, idx) : 1) * MultiCast();
                    if (d.Extra[s.Item].BoltMagic) hits *= boltFactor;
                    double casts = manualRate[s.Item] * (1 + MagicCostNow(s, idx) / 10.0 * T(d.kMagicMp) / 100.0)
                                 + autoRate[s.Item] + dupRate[s.Item] + boltRate[s.Item] * Pct(T(d.kBad));
                    return baseDmg * hits * Pct(T(d.kMpSkill)) * power * Pct(T(d.kMdb)) * all * Pct(ModMul(s)) * casts * (1 + d.Weapon.MagicWoundPct / 100.0)
                         * CF(crit + T(d.kMCrit) / 100.0 + ModCrit(s) + SafeAt(s.CritAdd, idx), critDmg + T(d.kMCritDmg) + ModCritDmg(s), exec);
                }
                case SrcKind.Rider when s.SwingScaled:
                {
                    double stat = s.ElemIdx >= 0 ? ElemVal(s.ElemIdx) : s.StatKey >= 0 ? T(s.StatKey) : 0;
                    double pct = s.AddBase + (s.AddKey >= 0 ? T(s.AddKey) : 0), basicBonus = 1;
                    if (s.EclipseAdd > 0 && d.Weapon.Eclipse && d.Eco != null)
                    {
                        EclipseState(out _, out var share);
                        pct += share * s.EclipseAdd;
                        basicBonus = 1 + share * T(d.Eco.kFsDmg) / 100.0;
                    }
                    double rates = s.RideBasic * SwingAs() * Pct(T(s.RideBasicAsDash ? d.kDad : d.kBad)) * basicBonus
                                 + s.RideSpecial * SpecialScale() * Pct(T(d.kSad))
                                 + s.RideDash * Pct(T(d.kDad));
                    return stat * Math.Max(0, pct) / 100.0 * rates * Pct(T(d.kWdb)) * Pct(T(d.kFwd)) * all * Pct(ModMul(s))
                         * (s.NoCrit ? 1 : CF(crit + ModCrit(s), critDmg + ModCritDmg(s), exec));
                }
                case SrcKind.Rider:
                {
                    double stat = s.ElemIdx >= 0 ? ElemVal(s.ElemIdx) : s.StatKey >= 0 ? T(s.StatKey) : 0;
                    double v = stat * Math.Max(0, s.AddBase + (s.AddKey >= 0 ? T(s.AddKey) : 0)) / 100.0
                             * Pct(T(d.kWdb)) * Pct(T(d.kBad)) * Pct(T(d.kFwd)) * all * Pct(ModMul(s))
                             * (s.NoCrit ? 1 : CF(crit + ModCrit(s), critDmg + ModCritDmg(s), exec));
                    if (s.UseAttackSpeed) v *= AsFactor();
                    if (s.PerCrit) v *= GsNeedleFactor(crit + T(d.kWCrit) / 100.0, critDmg + T(d.kWCritDmg));
                    return v;
                }
                case SrcKind.Ability:
                {
                    return Base(s, 0) * RateFactor(s, 0) * all * Pct(ModMul(s)) * (s.NoCrit ? 1 : CF(crit + ModCrit(s) + CritAddOf(s), critDmg + ModCritDmg(s) + CritDmgAddOf(s), exec))
                         * (s.BurnRing ? BurnRingUp(s) : 1);
                }
                case SrcKind.Proc:
                {
                    if (!ItemOn[s.Item]) return 0;
                    int idx = Math.Min(Math.Max(ItemLevel[s.Item], 0), m.Items[s.Item].MaxLevel);
                    double v = Base(s, idx) * RateFactor(s, idx) * (1 + (boost[s.Item] + CharmBonus()) / 100.0) * all * Pct(ModMul(s))
                             * (s.NoCrit ? 1 : CF(crit + ModCrit(s) + CritAddOf(s), (critDmg + ModCritDmg(s) + CritDmgAddOf(s)) * (1 + SafeAt(s.CritDmgAmp, idx) / 100.0), exec)) * FirstUseBonus(s);
                    if (enhanced[s.Item]) v *= 1.5;
                    if (s.Column)
                    {
                        int col = ItemSlot[s.Item] % m.W;
                        double rate = 0, elemSum = 0;
                        for (int y = 0; y < m.H; y++)
                        {
                            int t = ItemAt(col, y);
                            if (t < 0 || !d.Extra[t].Magic) continue;
                            rate += magicRate[t];
                            elemSum += elem[d.Extra[t].MagicElem] / 100.0;
                        }
                        v *= rate * elemSum;
                    }
                    return v;
                }
            }
            return 0;
        }
    }
}
