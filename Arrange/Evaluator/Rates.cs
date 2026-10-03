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
        double AsFactor() => SwingAs();
        double SwingAs() => d.Weapon.Crossbow ? d.Weapon.AsNow * CrossbowRate() / Math.Max(1e-6, d.Weapon.XbRateNow) : SwingCurve(WeaponAs(), d.Weapon.SwingFixed);
        double WeaponAs()
        {
            double fixedAs = T(d.kFixedAs);
            if (fixedAs > 0) return fixedAs / 100.0;
            return Math.Max(0.1, (T(d.kAs) * (1 + d.AsAmp) + 100) / 100.0);
        }
        double StatAs() => Math.Max(0.1, (T(d.kAs) + 100) / 100.0);
        double SpecialAs(bool related) => Math.Max(0.1, (T(d.kSpecAs) + (related ? T(d.kAs) : 0) + 100) / 100.0);

        double RateFactor(DpsSource s, int idx, bool rateOnly = false)
        {
            double f = s.SwingBased ? AsFactor() : 1;
            double fHits = f;
            switch (s.StatRate)
            {
                case RateStat.AttackSpeed:
                    if (!s.SwingBased) f *= StatAs();
                    break;
                case RateStat.Crit:
                    f *= Math.Min(Math.Max((T(d.kCrit) + T(d.kWCrit)) / 10000.0, 0), 1);
                    break;
                case RateStat.Luck:
                    f *= Math.Max(0, s.ChanceBase + SafeAt(s.ChanceTable, idx) * Math.Max(0, T(s.StatKey))) / 100.0;
                    break;
                case RateStat.MagicCasts:
                {
                    double casts = 0;
                    foreach (var o in d.Sources) if (o.Kind == SrcKind.Magic) casts += magicRate[o.Item];
                    foreach (var mb in d.MagicBuffs) casts += magicRate[mb.Item];
                    if (s.StatCap > 0) casts = Math.Min(casts, s.StatCap);
                    if (s.DirectCap > 0) casts = Math.Min(casts, s.DirectCap * WeaponHits());
                    f *= casts;
                    break;
                }
                case RateStat.DarkCloud:
                    f *= Math.Max(0, 1 + (s.RateKeyA >= 0 ? T(s.RateKeyA) : 0) / 100.0 + (s.RateKeyB >= 0 ? T(d.kAs) * T(s.RateKeyB) / 10000.0 : 0));
                    break;
            }
            if (s.Summon)
            {
                double asf = Pct(T(d.kFollowerAs) + T(d.kFollowerAs2));
                f *= s.SummonWait > 0 ? (s.SummonWait + s.SummonFixed) / (s.SummonWait / Math.Max(0.1, asf) + s.SummonFixed) : asf;
            }
            if (s.HasteKey >= 0) f *= Pct(T(s.HasteKey));
            if (s.HasteKey2 >= 0) f *= Pct(T(s.HasteKey2));
            if (s.CdSeconds > 0)
            {
                var lv = RateOf(s);
                double h = (Single ? s.CdBase : s.CdBaseM) * fHits;
                double p = s.CdChance * (lv != null ? SafeAt(lv, idx) : 1) * (fHits > 1e-9 ? f / fHits : 1);
                if (p > 1) { f /= p; p = 1; }
                f /= 1 + Math.Max(0, p) * Math.Max(0, s.CdSeconds * h - 0.5);
            }
            if (s.X != null) f *= XFactor(s, idx, rateOnly);
            if (s.AmpKey >= 0) f *= 1 + Math.Max(0, T(s.AmpKey));
            return f;
        }

        float[] RateOf(DpsSource s) => !Single && s.RateM != null ? s.RateM : s.Rate;

        double specialScale = 1;
        double SpecialScale() => specialScale;

        double SweepRateNow(bool others = true)
        {
            var sw = d.Sweep;
            double cap = sw.CapAs > 0 ? sw.Cap * Math.Max(0.1, 1 + sw.CapAsMul * T(d.kAs) / 100.0) / sw.CapAs : sw.Cap;
            if (d.kInfMp >= 0 && T(d.kInfMp) > 0) return cap;
            double cost = SweepCost(sw.Cost0, T(sw.kCostRed), T(sw.kSpecCostRed));
            double net = 0, uses = 0;
            if (others)
            {
                net = OtherMpUse(out uses) - MpHealIncome() * d.MpGain;
                if ((sw.GuardRed > 0 || sw.PerfectRed > 0) && d.GuardRate > 0)
                {
                    double perfect = Math.Min(d.GuardRate, Math.Max(0, d.PerfectGuardRate)), normal = d.GuardRate - perfect;
                    double red = T(sw.kCostRed), spec = T(sw.kSpecCostRed);
                    net -= normal * (cost - SweepCost(sw.Cost0, red + sw.GuardRed, spec)) + perfect * (cost - SweepCost(sw.Cost0, red + sw.PerfectRed, spec));
                }
            }
            return SweepRate(cost, T(sw.kRegen) * d.MpGain, T(sw.kResonance), T(sw.kSteal) * d.MpGain, sw.Dps, MaxMp() - sw.Reserved, sw.PoolPeriod, net, uses, cap);
        }

        double SweepNow(bool others = true) => d.Sweep.Mode == 1 ? SsCloudSweepRate() : SweepRateNow(others);

        double SpecialScaleNow()
        {
            var sw = d.Sweep;
            if (sw == null || Single != d.Single) return 1;
            double now = SweepNow();
            if (sw.Absolute) return sw.Base > 0 ? now / sw.Base : 1;
            if (sw.BaseEval <= 0) return 1;
            if (sw.Measured && !sw.Limited) return Math.Min(1, now / Math.Max(1e-6, sw.Base));
            return now / sw.BaseEval;
        }

        public void CalibrateSweep()
        {
            if (d.Sweep == null) return;
            Stats();
            UpdateMagicRates();
            var sw = d.Sweep;
            sw.BaseEval = SweepNow(others: sw.Measured);
            sw.Limited = !sw.Measured || sw.Base >= 0.8 * sw.BaseEval;
            specialScale = 1;
        }

        double FightLen() => Single && d.FightLen > 0 ? d.FightLen : 0;

        void UpdateMagicRates()
        {
            double cdr = T(d.kCdr), L = FightLen();
            bool blocked = d.kBlockMagic >= 0 && T(d.kBlockMagic) > 0;
            double k = FinalComboCdr();
            double swings = d.Weapon.BoltSwing > 0 ? d.Weapon.BoltSwing * StatAs() : 0, supply = 0;
            double evCd = d.kEvCd >= 0 ? T(d.kEvCd) : 0, evade = evCd > 0 ? EvadeRate() * evCd / 100.0 : 0;
            int books = 0;
            foreach (var s in d.Sources)
                if (s.Kind == SrcKind.Magic)
                {
                    int i = s.Item;
                    double regen = CdRate(Pct(cdr + extraCdr[i]) / s.Cooldown + evade, k);
                    bool bolt = swings > 0 && d.Extra[i].BoltMagic && ItemOn[i];
                    double opening = L > 0 ? s.Ammo / L : 0;
                    manualRate[i] = blocked || bolt || !ItemOn[i] ? 0 : regen + opening;
                    boltRate[i] = bolt ? regen + opening : 0;
                    boltSet[i] = !bolt;
                    if (bolt) { supply += boltRate[i]; books++; }
                }
            foreach (var mb in d.MagicBuffs)
            {
                int i = mb.Item;
                manualRate[i] = blocked || !ItemOn[i] ? 0 : CdRate(Pct(cdr + extraCdr[i]) / mb.Cooldown + evade, k) + (L > 0 ? mb.Ammo / L : 0);
                boltRate[i] = 0;
                magicRate[i] = manualRate[i];
            }
            if (books > 0 && swings < supply)
            {
                double left = swings;
                bool changed = true;
                while (changed && books > 0)
                {
                    changed = false;
                    double share = left / books;
                    foreach (var s in d.Sources)
                    {
                        if (s.Kind != SrcKind.Magic || boltSet[s.Item] || boltRate[s.Item] > share) continue;
                        boltSet[s.Item] = true;
                        left -= boltRate[s.Item];
                        books--;
                        changed = true;
                    }
                }
                double even = books > 0 ? Math.Max(0, left) / books : 0;
                foreach (var s in d.Sources)
                    if (s.Kind == SrcKind.Magic && !boltSet[s.Item]) boltRate[s.Item] = even;
            }
            DpsSource dupBook = null;
            double dup = 0;
            if (d.Weapon.Dg != null && d.Weapon.Dg.DupMagic)
            {
                double manual = 0, bestValue = 0;
                foreach (var s in d.Sources)
                {
                    if (s.Kind != SrcKind.Magic || !ItemOn[s.Item] || manualRate[s.Item] <= 0) continue;
                    manual += manualRate[s.Item];
                    double v = MagicCastValue(s);
                    if (dupBook == null || v > bestValue) { dupBook = s; bestValue = v; }
                }
                dup = Math.Min(DgFuryRate(), manual);
            }
            foreach (var s in d.Sources)
                if (s.Kind == SrcKind.Magic)
                {
                    dupRate[s.Item] = s == dupBook ? dup : 0;
                    magicRate[s.Item] = ItemOn[s.Item] ? manualRate[s.Item] + autoRate[s.Item] + boltRate[s.Item] + dupRate[s.Item] : 0;
                }
        }

        double MagicCastValue(DpsSource s)
        {
            int idx = Math.Min(Math.Max(ItemLevel[s.Item], 0), m.Items[s.Item].MaxLevel);
            double baseDmg = 1;
            if (s.Default != null || s.Percent != null)
                baseDmg = SafeAt(s.Default, idx) + (s.RelKey != null ? StatOf(s.RelKey) : 0) * SafeAt(s.Percent, idx) / 100.0;
            var rate = RateOf(s);
            return baseDmg * (rate != null ? SafeAt(rate, idx) : 1) * (1 + boost[s.Item] / 100.0) * (s.K > 0 ? s.K : 1);
        }

        static double CdRate(double r0, double k) => k > 1e-9 && r0 > 1e-9 ? k / Math.Log(1 + k / r0) : r0;

        double FinalComboCdr()
        {
            var x = d.Weapon.Xb;
            if (x == null || x.FinalCdr <= 0) return 0;
            return d.Hits.Swing * SwingAs() / x.FinalEvery * -Math.Log(1 - x.FinalCdr);
        }

        double FirstUseBonus(DpsSource s)
        {
            double L = FightLen();
            if (L <= 0 || !s.FirstFree) return 1;
            double uses = UseRate(s);
            return uses > 1e-9 ? 1 + 1 / (L * uses) : 1;
        }
    }
}
