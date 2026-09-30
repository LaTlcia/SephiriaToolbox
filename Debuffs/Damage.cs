using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed partial class Evaluator
    {
        double DebuffHits(DpsSource s)
        {
            if (s.Debuff < 0 || s.Debuff >= d.Debuffs.Count || s.Debuff >= dbR.Length) return 0;
            var di = d.Debuffs[s.Debuff];
            double r = dbR[s.Debuff];
            if (r <= 0) return 0;
            DebuffShape(di, out var max, out var add, out var dur, out _);
            double luck = Math.Min(100, Math.Max(0, T(di.kLuck))) / 100.0, targets = DebuffTargets(di);
            switch (di.Type)
            {
                case DebuffType.Burn: return targets * dbPresence[(int)DebuffType.Burn] * Pct(T(di.kSpeed)) / Math.Max(0.05, di.Tick0);
                case DebuffType.Electric:
                {
                    CycleStacks(r, dur, max, 1, out _, out _, out var cycle);
                    return targets * (1 / cycle + r * luck);
                }
                case DebuffType.Plasma:
                {
                    CycleStacks(r, dur, max, add, out _, out _, out var cycle);
                    return targets * (dur / cycle * Pct(T(di.kSpeed)) / Math.Max(0.05, di.Tick0) + 1 / cycle + r * luck);
                }
                case DebuffType.Frostbite: return s.DebuffPart == 1 ? freezeRate : targets * dbPresence[(int)DebuffType.Frostbite];
                default: return targets * (1 - Math.Exp(-Math.Min(30, r * dur))) / Math.Max(0.05, di.Tick0);
            }
        }

        double DebuffF(DpsSource s, double all)
        {
            if (s.Debuff < 0 || s.Debuff >= d.Debuffs.Count) return 0;
            var di = d.Debuffs[s.Debuff];
            bool plasma = Plasma();
            if (plasma && di.Type is DebuffType.Burn or DebuffType.Electric) return 0;
            if (!plasma && di.Type == DebuffType.Plasma) return 0;
            double targets = DebuffTargets(di);
            double r = s.Debuff < dbR.Length ? dbR[s.Debuff] : DebuffRate(di, targets, true);
            if (r <= 0) return 0;
            DebuffShape(di, out var max, out var add, out var dur, out var d0);
            double fire = elem[1], ice = elem[2], light = elem[3];
            double luck = Math.Min(100, Math.Max(0, T(di.kLuck))) / 100.0;
            double L = FightLen();
            double rampTick = L > 0 ? Math.Max(0, 1 - (1 / r + 0.5 * Math.Min(dur, Math.Max(1, max / Math.Max(1, add)) / r)) / L) : 1;
            double rampPop = L > 0 ? (L >= 1 / r + dur ? Math.Max(0, 1 - 0.5 * (dur + 1 / r) / L) : 0) : 1;
            double rampLuck = L > 0 ? Math.Max(0, 1 - 1 / (r * L)) : 1;
            double perTarget;
            switch (di.Type)
            {
                case DebuffType.Burn:
                {
                    double m = T(di.kEvo) > 0 ? 0.28 : 0.18;
                    double tick = T(di.kBlue) > 0 ? Math.Max(fire, ice) * m + Math.Min(fire, ice) * m * 0.25 : fire * m;
                    tick *= Pct(T(di.kDmg));
                    perTarget = ResetStacks(DebuffType.Burn, r, max, add, DebuffAvgStacks(r, dur, max, add)) * tick / Math.Max(0.05, di.Tick0) * Pct(T(di.kSpeed)) * rampTick;
                    break;
                }
                case DebuffType.Electric:
                {
                    CycleStacks(r, dur, max, 1, out var final, out _, out var cycle);
                    double hit = light * di.StatPct / 100.0 * Pct(T(di.kDmg)) * (dur / d0);
                    perTarget = hit * final / cycle * rampPop + r * luck * hit * rampLuck;
                    break;
                }
                case DebuffType.Plasma:
                {
                    CycleStacks(r, dur, max, add, out var final, out var integral, out var cycle);
                    double m = T(di.kEvo) > 0 ? 0.28 : 0.18;
                    double mul = (100 + T(di.kDmg) + T(di.kDmg2)) / 100.0 * Pct(T(d.kPlasmaDmg));
                    double tick = (T(di.kBlue) > 0 ? (Math.Max(fire, ice) + light) / 2 * m + Math.Min(fire, ice) * m * 0.25 : (fire + light) * m) * mul;
                    double ticks = tick / Math.Max(0.05, di.Tick0) * Pct(T(di.kSpeed)) * integral / cycle;
                    double hit = Math.Floor((fire + light) / 2) * di.StatPct / 100.0 * mul * (dur / d0);
                    perTarget = ticks * rampTick + hit * final / cycle * rampPop + r * luck * hit * rampLuck;
                    break;
                }
                case DebuffType.Frostbite:
                {
                    if (s.DebuffPart == 1)
                    {
                        double perFreeze = max / Math.Max(1, add);
                        double rampFreeze = L > 0 ? Math.Max(0, 1 - 0.5 * (perFreeze / r) / L) : 1;
                        double up = 1 - Math.Exp(-Math.Min(30, r * dur));
                        return targets * r / perFreeze * up * ice * di.FreezeMul * Pct(T(di.kFreezeDmg)) * EffectFactor(s, all) * rampFreeze;
                    }
                    double fbd = T(di.kFrostDmg);
                    if (fbd <= 0) return 0;
                    perTarget = dbStacks[(int)DebuffType.Frostbite] * ice * fbd / 100.0 * rampTick;
                    break;
                }
                default:
                    perTarget = ResetStacks(di.Type, r, max, add, DebuffAvgStacks(r, dur, max, add)) * di.Dmg0 / Math.Max(0.05, di.Tick0) * Pct(T(di.kDmg)) * rampTick;
                    break;
            }
            return targets * perTarget * EffectFactor(s, all);
        }

        double EffectFactor(DpsSource s, double all) => all * Pct(T(d.kDebuff)) * Pct(T(d.kPoison) + PoisonBonus()) * Pct(ModMul(s));

        double PoisonBonus()
        {
            if (d.PoisonIdx < 0 || d.PoisonIdx >= d.Debuffs.Count) return 0;
            var di = d.Debuffs[d.PoisonIdx];
            double targets = DebuffTargets(di), r = d.PoisonIdx < dbR.Length ? dbR[d.PoisonIdx] : DebuffRate(di, targets, true);
            if (r <= 0) return 0;
            DebuffShape(di, out _, out _, out var dur, out _);
            return 10 * targets * (1 - Math.Exp(-Math.Min(30, r * dur)));
        }
    }
}
