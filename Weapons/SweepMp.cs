using System;
using System.Collections.Generic;

public partial class SephiriaToolbox
{
    sealed class MpDrain
    {
        public int Item;
        public double MaxMpShare, PerUse, Cap;
        public bool RelicSwords;
    }

    sealed class MpHeal
    {
        public int Item;
        public float[] Percent;
        public double Cooldown;
    }

    sealed partial class Evaluator
    {
        double OtherMpUse(out double uses)
        {
            double mp = 0;
            uses = 0;
            double multi = MultiCast();
            foreach (var s in d.Sources)
            {
                if (s.Kind != SrcKind.Magic || s.Item < 0 || !ItemOn[s.Item] || (s.MagicCost == null && s.MagicCostBase == null)) continue;
                double casts = manualRate[s.Item] * multi;
                double cost = MagicCostNow(s, IdxOf(s.Item));
                if (cost <= 0) continue;
                mp += casts * cost;
                uses += casts;
            }
            if (d.GuardRate > 0)
            {
                double per = 10 * Math.Max(0, 1 - (d.kGuardResist >= 0 ? T(d.kGuardResist) : 0) / 100.0);
                double perfect = Math.Min(d.GuardRate, Math.Max(0, d.PerfectGuardRate));
                mp += (d.GuardRate - perfect) * Math.Floor(per) + perfect * Math.Floor(per * 0.5);
                uses += d.GuardRate;
            }
            double maxMp = MaxMp();
            foreach (var dr in d.MpDrains)
            {
                if (!ItemOn[dr.Item]) continue;
                double casts = 0;
                if (dr.RelicSwords) casts = Math.Min(RelicFireRate(), dr.Cap);
                else
                    foreach (var s in d.Sources)
                        if (s.Item == dr.Item && s.Kind == SrcKind.Proc) { casts = HitRate(s) / Math.Max(1e-6, s.PerUse); break; }
                mp += casts * (dr.PerUse + dr.MaxMpShare * maxMp);
                uses += casts;
            }
            return mp;
        }

        double MagicCostNow(DpsSource s, int idx)
        {
            if (d.kNoMagicCost >= 0 && T(d.kNoMagicCost) > 0) return 0;
            if (s.MagicCostBase == null) return SafeAt(s.MagicCost, idx);
            double add = s.MagicCostAdd - costRed[s.Item] - (d.kMagicCostReduce >= 0 ? T(d.kMagicCostReduce) : 0);
            if (add <= -100) return 0;
            double b = SafeAt(s.MagicCostBase, idx);
            return Math.Max(0, Math.Round(b + b * add / 100.0));
        }

        double MpHealIncome()
        {
            if (d.MpHeals.Count == 0) return 0;
            double maxMp = MaxMp(), sum = 0, L = FightLen();
            foreach (var h in d.MpHeals)
            {
                if (!ItemOn[h.Item]) continue;
                double period = L > 0 ? Math.Max(L, h.Cooldown) : Math.Max(1, h.Cooldown);
                sum += SafeAt(h.Percent, IdxOf(h.Item)) / 100.0 * maxMp / period;
            }
            return sum;
        }
    }
}
