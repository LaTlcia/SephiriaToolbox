using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed partial class Evaluator
    {
        double FeederRate(Feeder f)
        {
            if (!ItemOn[f.Item]) return 0;
            int idx = IdxOf(f.Item);
            if (f.Kind == 0) return SafeAt(f.PerSec, idx) * f.Mul;
            double p = (f.Hits * AsFactor() + f.HitsFixed) * SafeAt(f.Chance, idx);
            return p <= 0 ? 0 : f.Mul / (SafeAt(f.Cd, idx) + 1 / p);
        }

        double MultiCast()
        {
            double n = d.MultiBase;
            if (d.MultiCast.Count > 0)
            {
                double maxMp = d.kMaxMp >= 0 ? T(d.kMaxMp) * Pct(d.kFinalMp >= 0 ? T(d.kFinalMp) : 0) : 0;
                foreach (var f in d.MultiCast)
                    if (ItemOn[f.Item] && maxMp >= SafeAt(f.Chance, IdxOf(f.Item))) n += f.Mul;
            }
            return Math.Max(1, n);
        }

        double CloudStrikes()
        {
            CloudBudget(out var demand, out var supply);
            return Math.Min(demand, supply);
        }

        void CloudBudget(out double demand, out double supply)
        {
            var e = d.Eco;
            double scale = 1 + Math.Max(0, T(e.kCloudScale));
            double max = Math.Max(0, T(e.kCloudMin)) * scale + e.CloudDefault;
            double speed = e.CloudSpeed0 * Math.Max(0, 1 + T(e.kCloudSpeed) / 100.0 + (T(e.kCloudAsBonus) > 0 ? T(d.kAs) * T(e.kCloudAsBonus) / 10000.0 : 0));
            double multi = Math.Max(1, T(e.kCloudMulti));
            demand = speed / Math.Max(0.1, e.CloudInterval) * multi;
            foreach (var f in e.CloudUse) demand += FeederRate(f) * multi;
            if (d.Weapon.CloudBottle) demand += d.Weapon.DashAttackRate * d.Weapon.CloudBottlePct / 100.0;
            double keep = Math.Min(Math.Max(T(e.kCloudKeep), 0), 100) / 100.0;
            if (keep >= 0.999) { supply = double.PositiveInfinity; return; }
            double regen = Math.Max(1, Math.Floor(max * e.CloudRestorePct / 100.0)) * Math.Max(0, 1 + T(e.kCloudRestore) / 100.0) / Math.Max(0.1, e.CloudRestoreSec);
            double gen = e.CloudOther + e.CloudOtherAs * AsFactor();
            foreach (var f in e.CloudGen) gen += FeederRate(f);
            supply = (max / Math.Max(5, e.Battle) + regen + gen * scale) / (1 - keep);
        }

        double CloudDamage()
        {
            var e = d.Eco;
            double l = elem[3], ice = elem[2];
            double v = T(e.kCloudIce) > 0 ? Math.Max(l, ice) * e.CloudPct / 100 + Math.Min(l, ice) * e.CloudPctIce / 100 : l * e.CloudPct / 100;
            v *= 1 + Math.Min(Math.Max(T(e.kCloudLuck), 0), 100) / 100.0;
            return v * Pct(T(e.kCloudDmg));
        }

        double FsSwords()
        {
            var e = d.Eco;
            double direct = e.FsHitsSwing * AsFactor() + e.FsHitsOther, magic = 0;
            foreach (var o in d.Sources)
                if (o.Kind == SrcKind.Magic && ItemOn[o.Item])
                    magic += magicRate[o.Item] * Math.Min(3, o.Rate != null ? SafeAt(o.Rate, IdxOf(o.Item)) : 1) * MultiCast();
            double relic = 0;
            if (e.kFsRelic >= 0 && T(e.kFsRelic) > 0)
                foreach (var o in d.Sources)
                    if (o.Relic && o.Kind == SrcKind.Proc) relic += HitRate(o);
            double all = direct + magic + relic;
            double trig = Math.Min(all, 1 / e.FsMinCd);
            if (trig <= 0) return 0;
            double per = 1 + Math.Max(0, T(e.kFsAdd)) + direct / all * Math.Max(0, T(e.kFsAddW)) + magic / all * Math.Max(0, T(e.kFsAddM));
            double demand = trig * per;
            if (T(e.kFsPick) > 0) return demand;
            double max = Math.Max(1, e.FsMax0 + T(e.kFsMax));
            double auto = 0;
            foreach (var f in e.FsGen) auto += FeederRate(f);
            double tau = 0.25 + e.FsLife / Pct(T(e.kFsFall)) + (T(e.kFsReturn) > 0 ? 0 : e.FsWalk);
            if (d.Weapon.Eclipse) max = 0;
            double supply = (max + auto * e.Battle / 2) / Math.Min(tau, Math.Max(5, e.Battle));
            return Math.Min(demand, supply);
        }

        double FsDamage(DpsSource s, double crit, double critDmg, bool exec)
        {
            var e = d.Eco;
            double v = (T(e.kFsFrost) > 0 ? elem[2] : elem[1]) * Pct(T(e.kFsDmg)) * e.FsPct / 100;
            v *= 1 + Math.Min(Math.Max(T(e.kFsLuck), 0), 100) / 100.0 * e.FsLuckBonus / 100.0;
            if (T(e.kFsMagic) > 0) v *= Pct(T(d.kMdb));
            return v * CF(crit + T(e.kFsCrit) + ModCrit(s), critDmg + T(e.kFsCritDmg) + ModCritDmg(s), exec);
        }

        double GroundHits()
        {
            var e = d.Eco;
            bool any = false;
            foreach (var it in e.GroundGate) if (ItemOn[it]) { any = true; break; }
            if (!any) return 0;
            double ticks = GroundTicksPerArea(T(e.kGroundDur));
            double range = Math.Max(0.1, T(e.kGroundRange) / 100.0);
            double hits = e.GroundOther;
            foreach (var f in e.GroundGen)
            {
                if (!ItemOn[f.Item]) continue;
                int idx = IdxOf(f.Item);
                hits += SafeAt(f.PerSec, idx) * ticks * GroundCover(SafeAt(f.Radius, idx) * range, f.AtPlayer, d.Melee, Enemies());
            }
            return Math.Min(4 * Enemies(), hits);
        }

        double Enemies() => Single ? 1 : d.Eco != null ? d.Eco.Enemies : PlayEnemies;

        double EcoF(DpsSource s, double all, double crit, double critDmg, bool exec)
        {
            if (d.Eco == null) return 0;
            var e2 = d.Eco;
            double mm = Pct(ModMul(s));
            switch (s.Eco)
            {
                case EcoKind.DarkCloud: return CloudStrikes() * CloudDamage() * all * mm * CF(crit + ModCrit(s), critDmg + ModCritDmg(s), exec);
                case EcoKind.FlameSword: return FsSwords() * FsDamage(s, crit, critDmg, exec) * all * mm;
                case EcoKind.FlameGround: return GroundHits() * (2 + 0.5 * elem[1]) * Pct(T(d.kDebuff)) * Pct(T(d.kPoison)) * all * mm * CF(crit + ModCrit(s), critDmg + ModCritDmg(s), exec);
                case EcoKind.TrueDamage: return Math.Max(0, T(d.kTrue)) * (e2.SwingHits * SwingAs() + e2.OtherHits);
                case EcoKind.Debuff: return DebuffF(s, all) * CF(crit + ModCrit(s), critDmg + ModCritDmg(s), exec);
            }
            return 0;
        }
    }
}
