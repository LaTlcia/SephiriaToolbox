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
        double SwingAs() => d.Weapon.Crossbow ? d.Weapon.AsNow * CrossbowRate() / Math.Max(1e-6, d.Weapon.XbRateNow) : WeaponAs();
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
                    f *= s.StatCap > 0 ? Math.Min(casts, s.StatCap) : casts;
                    break;
                }
                case RateStat.DarkCloud:
                    f *= Math.Max(0, 1 + (s.RateKeyA >= 0 ? T(s.RateKeyA) : 0) / 100.0 + (s.RateKeyB >= 0 ? T(d.kAs) * T(s.RateKeyB) / 10000.0 : 0));
                    break;
            }
            if (s.Summon) f *= Pct(T(d.kFollowerAs) + T(d.kFollowerAs2));
            if (s.HasteKey >= 0) f *= Pct(T(s.HasteKey));
            if (s.HasteKey2 >= 0) f *= Pct(T(s.HasteKey2));
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
            if (others) net = OtherMpUse(out uses) - MpHealIncome();
            return SweepRate(cost, T(sw.kRegen), T(sw.kResonance), T(sw.kSteal), sw.Dps, MaxMp() - sw.Reserved, sw.PoolPeriod, net, uses, cap);
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
            foreach (var s in d.Sources)
                if (s.Kind == SrcKind.Magic)
                {
                    double manual = blocked ? 0 : CdRate(Pct(cdr + extraCdr[s.Item]) / s.Cooldown, k) + (L > 0 ? 1 / L : 0);
                    magicRate[s.Item] = ItemOn[s.Item] ? manual + autoRate[s.Item] : 0;
                }
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
            double uses = HitRate(s) / Math.Max(1e-9, s.PerUse);
            return uses > 1e-9 ? 1 + 1 / (L * uses) : 1;
        }
    }
}
