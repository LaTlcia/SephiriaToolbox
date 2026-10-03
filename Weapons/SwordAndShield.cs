using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed class SsModel
    {
        public byte Mode;
        public bool Charged;
        public double ChargedTime = 3, ChargedMax = 10, ChargedPct = 30, ChargedAsEff = 200;
        public int kDefStat = -1;
        public double DefUnit = 2, DefPct = 1;
        public bool Throw;
        public double ThrowBase = 2, ThrowPer = 20;
        public bool Ring, RingScaled;
        public double RingBase = 6, RingThreshold = 20, RingPer = 20, RingMax;
        public double SpearEx = 2;
        public double CloudCost = 4;
        public int kCloudLuck = -1, kCloudDmg = -1, kSweepRed = -1;
        public double StrikeRate, StrikeMult, ExplMult = 1, ExplRatio = 50, ExplRatioHi = 100, ExplHiStack = 4, SwingBase, ComboMult = 1;
    }

    static bool HasAddon<T>(WeaponSimple w) where T : WeaponAddon => w != null && w.addons != null && w.addons.Any(a => a is T);

    static bool SsRapier(WeaponSimple w) => w is WeaponSimple_SwordAndShield && HasAddon<WeaponAddon_Rapier>(w);

    static int StatWithWeapon(PlayerAvatar a, string key, WeaponSimple w)
    {
        var live = a.GetComponent<WeaponControllerSimple>()?.currentWeapon;
        int v = a.GetCustomStatUnsafe(key);
        if (w == null || w == live) return v;
        return v - (live != null ? WeaponAddonStats(live).GetValueOrDefault(key) : 0) + WeaponAddonStats(w).GetValueOrDefault(key);
    }

    static byte SsSweepMode(WeaponSimple_SwordAndShield ss, PlayerAvatar a)
    {
        if (!ss.isSweepAvailable || HasAddon<WeaponAddon_BasicAttack>(ss)) return 3;
        if (ss.isFlameEaterHaetaeEnabled) return 2;
        if (StatWithWeapon(a, "DARKCLOUDSWEEP", ss) > 0) return 1;
        return 0;
    }

    static WeaponSimple_SwordAndShield.AllElementalFireDataSet SsElementalSet(WeaponSimple_SwordAndShield ss, PlayerAvatar a)
    {
        if (!ss.isAllElementalEnabled || ss.allElementalSets == null) return null;
        int p = a.GetCustomStatUnsafe("PHYSICALDAMAGE"), ice = a.GetCustomStatUnsafe("ICEDAMAGE"),
            fire = a.GetCustomStatUnsafe("FIREDAMAGE"), light = a.GetCustomStatUnsafe("LIGHTNINGDAMAGE");
        int max = Math.Max(Math.Max(p, ice), Math.Max(fire, light)), n = 0, state = 4;
        if (p == max) { n++; state = 0; }
        if (ice == max) { n++; state = 1; }
        if (fire == max) { n++; state = 2; }
        if (light == max) { n++; state = 3; }
        if (n >= 2) state = 4;
        return state < ss.allElementalSets.Length ? ss.allElementalSets[state] : null;
    }

    static NewWeaponFireData At(NewWeaponFireData[] arr, int i) => arr != null && i >= 0 && i < arr.Length ? arr[i] : null;

    static WeaponMoves SwordShieldMoves(WeaponSimple_SwordAndShield ss, PlayerAvatar a, Behavior play)
    {
        var mv = new WeaponMoves();
        var set = SsElementalSet(ss, a);
        NewWeaponFireData Basic(int i) => At(set?.basicComboAttacks, i) ?? At(ss.basicComboAttacks, i);
        NewWeaponFireData Special(int i) => At(set?.specialAttacks, i) ?? At(ss.specialAttacks, i);
        NewWeaponFireData Dash(int i) => At(set?.dashAttacks, i) ?? At(ss.dashAttacks, i);
        if (set != null) mv.Formula = "HIGHEST";

        if (SsRapier(ss))
        {
            var thrusts = new[] { Dash(0), Dash(1) };
            mv.BasicIsDash = mv.NoDash = true;
            mv.BasicFd = thrusts.FirstOrDefault(f => f != null);
            mv.BasicMult = mv.BasicFd != null ? AvgMult(thrusts) : 1;
        }
        else
        {
            int len = ss.basicComboAttacks?.Length ?? 0, fi = ss.finalComboIdx;
            int n = fi >= 0 && fi < len ? fi + 1 : len;
            var combo = Enumerable.Range(0, n).Select(Basic).ToArray();
            mv.BasicFd = combo.FirstOrDefault(f => f != null);
            mv.BasicMult = mv.BasicFd != null ? AvgMult(combo) : 1;
            double total = combo.Where(f => f != null).Sum(HitMult);
            if (total > 0 && fi >= 0 && fi < n && combo[fi] != null) mv.FinalShare = HitMult(combo[fi]) / total;
            mv.DashFd = Dash(0);
            mv.DashMult = HitMult(mv.DashFd);
            mv.NoDash = mv.DashFd == null;
        }
        var auto = ss.addons?.OfType<WeaponAddon_DashAttack>().FirstOrDefault(x => x != null);
        if (auto != null && !mv.NoDash)
        {
            mv.AutoDash = HitMult(Dash(auto.dashAttackIdx) ?? Dash(0));
        }

        var over = SsOverride(ss);
        double guardMult = 0;
        switch (SsSweepMode(ss, a))
        {
            case 3:
                mv.NoSpecial = true;
                mv.SpecialNote = Tr("weapon.no_cleave");
                break;
            case 2:
                mv.NoSpecial = true;
                mv.Strike = ss.haetaeStrikeAttackFireData;
                mv.StrikeExplosion = ss.haetaeStrikeAttackExplosionFireData;
                mv.SpecialNote = Tr("weapon.cleave_button_performs_flame");
                break;
            case 1:
                mv.SpecialFd = Special(0);
                mv.SpecialNote = Tr("weapon.lightning_spear_uses_storm");
                break;
            default:
                if (over != null && over.fireData != null) { mv.SpecialFd = over.fireData; mv.SpecialNote = Tr("weapon.shield_bash_defense_bonus"); }
                else if (ss.shieldThrowing || HasAddon<WeaponAddon_ShieldThrowing>(ss)) { mv.SpecialFd = Special(4); mv.SpecialNote = Tr("weapon.shield_throw"); }
                else if (ss.chargedSweep_New) { mv.SpecialFd = ss.chargedSweepFireData_New ?? Special(0); mv.SpecialNote = Tr("weapon.cleave_stacking_damage"); }
                else if ((ss.guardSweep || HasAddon<WeaponAddon_SweepDamage>(ss)) && Special(1) != null && Special(0) != null)
                {
                    double share = 1 - Math.Exp(-Math.Max(0, play.Guard) * 5);
                    mv.SpecialFd = Special(0);
                    guardMult = (1 - share) * HitMult(Special(0)) + share * HitMult(Special(1));
                    mv.SpecialNote = Tr("weapon.cleave_empowered_5_s", share);
                }
                else mv.SpecialFd = Special(0);
                break;
        }
        if (!mv.NoSpecial)
        {
            if (mv.SpecialFd == null) mv.NoSpecial = true;
            else mv.SpecialMult = guardMult > 0 ? guardMult : HitMult(mv.SpecialFd);
        }
        return mv;
    }

    static WeaponAddonCommon_ChangeWeaponAction SsOverride(WeaponSimple ss) =>
        ss.addons?.OfType<WeaponAddonCommon_ChangeWeaponAction>()
          .FirstOrDefault(x => x != null && string.Equals(x.changeWeaponActionName, "SWEEP", StringComparison.OrdinalIgnoreCase));

    void BuildSwordShieldModel(PlayerAvatar avatar, DpsModel d, Func<string, int> K, WeaponSimple_SwordAndShield ss, Behavior play)
    {
        var w = d.Weapon;
        var s = w.Ss = new SsModel { Mode = SsSweepMode(ss, avatar) };
        w.SpecialMp = s.Mode == 0;
        var mv = SwordShieldMoves(ss, avatar, play);
        if (ss.chargedSweep_New && s.Mode == 0)
        {
            s.Charged = true;
            s.ChargedTime = Math.Max(0.1, ConstOr("chargedSweepTime", 3));
            s.ChargedMax = Math.Max(0, ConstOr("chargedSweepMaxStack", 10));
            s.ChargedPct = ConstOr("chargedSweepDamageBonusPercent", 30);
            s.ChargedAsEff = ConstOr("chargedSweepAttackSpeedEfficiency", 200);
        }
        var over = SsOverride(ss);
        if (s.Mode == 0 && over != null && !string.IsNullOrEmpty(over.newActionParameter))
            foreach (var part in over.newActionParameter.Split(','))
            {
                var kv = part.Split('=');
                if (kv.Length != 2 || kv[0].Trim() != "DAMAGEBONUSBYSTAT") continue;
                var p = kv[1].Split('/');
                if (p.Length != 3) continue;
                string stat = p[0].Trim().ToUpperInvariant();
                s.kDefStat = K(stat == "DEFENSE" ? "DAMAGEREDUCTION" : stat);
                double Num(string t)
                {
                    t = t.Trim();
                    if (t.StartsWith("[") && t.EndsWith("]")) return ConstOr(t.Substring(1, t.Length - 2), 1);
                    return int.TryParse(t, out var v) ? v : 1;
                }
                s.DefUnit = Math.Max(1, Num(p[1]));
                s.DefPct = Num(p[2]);
            }
        if (s.Mode == 0 && mv.SpecialNote == Tr("weapon.shield_throw"))
        {
            s.Throw = true;
            if (mv.SpecialFd is NewWeaponFireData_SpecialProjectile sp && sp.projectilePrefab != null
                && sp.projectilePrefab.GetComponent<SpecialProjectile_ThrowingShield>() is { } ts)
                s.ThrowBase = Math.Max(1, ts.defaultBulletCount);
            s.ThrowPer = Math.Max(1, ConstOr("throwingShieldBulletCountBonusByBasicAttackDamage", 20));
        }
        if (mv.SpecialFd is NewWeaponFireData_BulletSpread bs && bs.betweenAngleDegrees > 0)
        {
            s.Ring = true;
            s.RingBase = Math.Max(1, Math.Ceiling(bs.spreadAngle / bs.betweenAngleDegrees - 1e-6));
            if (bs is NewWeaponFireData_BulletSpread_ElementalScaled es && es.statPerExtraBullet > 0)
            {
                s.RingScaled = true;
                s.RingThreshold = es.baseStatThreshold;
                s.RingPer = es.statPerExtraBullet;
                s.RingMax = es.maxExtraBullets;
            }
        }
        if (s.Mode == 1)
        {
            var normal = At(ss.specialAttacks, 0);
            var ex = At(ss.specialAttacks, 1);
            s.SpearEx = normal != null && ex != null && normal.damageMultiplier > 0 ? ex.damageMultiplier / normal.damageMultiplier : 1;
            s.kCloudLuck = K("DARKCLOUDLUCK");
            s.kCloudDmg = K("DARKCLOUDDAMAGE");
            s.kSweepRed = K("SWEEPCOSTREDUCTION");
        }
        if (s.Mode == 2 && mv.Strike != null)
        {
            s.StrikeMult = mv.Strike.damageMultiplier;
            s.ExplMult = mv.StrikeExplosion != null ? mv.StrikeExplosion.damageMultiplier : 0;
            s.ExplRatio = ConstOr("flameEaterStackDamageRatio", 50);
            s.ExplRatioHi = ConstOr("flameEaterStackEnhancedDamageRatio", 100);
            s.ExplHiStack = Math.Max(1, ConstOr("flameEaterStackEnhancedDamageStack", 4));
            s.StrikeRate = play.Strike;
            s.SwingBase = play.Swing;
            s.ComboMult = Math.Max(0.01, mv.BasicMult);
        }
    }

    void BuildSweep(PlayerAvatar avatar, DpsModel d, Func<string, int> K, WeaponSimple_SwordAndShield ss, Behavior play, ArrModel m)
    {
        byte mode = SsSweepMode(ss, avatar);
        if (play.SpecialMeasured && weaponOverride != null && swingController != null
            && swingController.currentWeapon is WeaponSimple_SwordAndShield cur && SsSweepMode(cur, avatar) != mode)
        {
            play.Special = PlaySpecial;
            play.SpecialMeasured = false;
        }
        double poolPeriod = play.Single ? d.FightLen / Math.Max(1, BossRefillsPerFight()) : BattleLength();
        switch (mode)
        {
            case 3:
                play.Special = 0;
                play.SpecialMeasured = false;
                m.Notes.Add(Tr("weapon.this_sword_shield_cant"));
                return;
            case 2:
            {
                bool measured = play.SpecialMeasured;
                play.Strike = measured ? play.Special : SweepCap;
                play.Special = 0;
                play.SpecialMeasured = false;
                m.Notes.Add(Tr("weapon.flame_eater_haetae_cleave", play.Strike, (measured ? Tr("common.measured") : Tr("weapon.no_mp_cap"))));
                return;
            }
            case 1:
            {
                bool measured = play.SpecialMeasured;
                if (!measured) play.Special = PlaySpecial;
                d.Sweep = new SweepModel { Mode = 1, Base = play.Special, Measured = measured, Absolute = !measured };
                m.Notes.Add(measured ? Tr("weapon.superconductor_cleave_uses_storm", play.Special) : Tr("weapon.superconductor_cleave_uses_storm_clouds"));
                return;
            }
        }
        double rate = SweepRateByMp(avatar, ss, poolPeriod, out d.SweepCost);
        play.SweepCost = d.SweepCost;
        play.SpecialByMp = d.SweepCost > 0;
        if (!play.SpecialMeasured && rate > 0) play.Special = rate;
        if (d.SweepCost > 0 && play.Special > 0)
            d.Sweep = new SweepModel
            {
                Mode = 0, Base = play.Special, Measured = play.SpecialMeasured, Cost0 = ss.sweepMpCost, PoolPeriod = poolPeriod,
                kCostRed = K("SWEEPCOSTREDUCTION"), kSpecCostRed = K("SPECIALATTACKCOSTREDUCTION"), kRegen = K("MPREGEN"),
                kResonance = K("MPRESONANCE"), kSteal = K("MPSTEAL"), Dps = SweepDps(avatar), Reserved = avatar.reservedMp,
                GuardRed = SweepBuffReduction(ss.buffPrefab), PerfectRed = SweepBuffReduction(ss.perfectGuardBuffPrefab)
            };
        if (play.SpecialByMp)
            m.Notes.Add(play.SpecialMeasured
                ? Tr("weapon.sword_shield_cleave_measured", play.Special, play.SweepCost)
                : Tr("weapon.sword_shield_cleave_mp", play.Special, play.SweepCost));
    }

    void WeaponGuardBuffs(PlayerAvatar avatar, ArrModel m, DpsModel d, Func<string, int> K, WeaponSimple weapon, Behavior play, Dictionary<int, double> curRaw)
    {
        var live = avatar.GetComponent<WeaponControllerSimple>()?.currentWeapon;
        if (live is WeaponSimple_SwordAndShield liveSs)
        {
            double now = ActiveSweepBuff(avatar, liveSs.buffPrefab) + ActiveSweepBuff(avatar, liveSs.perfectGuardBuffPrefab);
            if (now != 0) curRaw[K("SWEEPCOSTREDUCTION")] = curRaw.GetValueOrDefault(K("SWEEPCOSTREDUCTION")) + now;
        }
        if (live != null && live.addons != null && BuffsField?.GetValue(avatar) is Dictionary<string, CharacterBuff> buffs)
            foreach (var pg in live.addons.OfType<WeaponAddon_PerfectGuardBuff>())
                if (pg?.buffPrefab != null && !string.IsNullOrEmpty(pg.buffPrefab.ID) && buffs.TryGetValue(pg.buffPrefab.ID, out var active) && active != null)
                    foreach (var (key, mode, v) in BuffStats(pg.buffPrefab))
                        if (mode == 0) curRaw[K(key)] = curRaw.GetValueOrDefault(K(key)) + v * active.Amplified * active.CurrentStack;
        if (weapon == null || weapon.addons == null) return;
        foreach (var pg in weapon.addons.OfType<WeaponAddon_PerfectGuardBuff>())
        {
            var prefab = pg?.buffPrefab;
            if (prefab == null) continue;
            double dur = prefab.defaultDuration * (prefab.ignoreDurationBonus ? 1 : Pct(avatar.GetCustomStat(ECustomStat.BuffDuration)));
            double up = BuffAvgStacks(play.PerfectGuard, dur, 1);
            foreach (var (key, mode, v) in BuffStats(prefab))
                if (mode == 0) d.ExtraBase[K(key)] = d.ExtraBase.GetValueOrDefault(K(key)) + v * up;
            m.Notes.Add(Tr("weapon.frozen_fury_perfect_block", play.PerfectGuard, up));
        }
    }

    sealed partial class Evaluator
    {
        double SsSweepsPerSec() => d.Weapon.SpecialRate * SpecialScale();

        double SsSpecialFactor()
        {
            var ss = d.Weapon.Ss;
            if (ss == null) return 1;
            double f = 1;
            if (ss.Charged) f *= 1 + ss.ChargedPct / 100.0 * SsChargedStacks();
            if (ss.kDefStat >= 0) f *= Pct(Math.Truncate(T(ss.kDefStat) / ss.DefUnit) * ss.DefPct);
            if (ss.Throw) f *= SsThrowHits();
            if (ss.Ring) f *= SsRingHits();
            if (ss.Mode == 1)
            {
                double p = Math.Min(100, Math.Max(0, T(ss.kCloudLuck))) / 100.0;
                f *= (1 + p * (ss.SpearEx - 1)) * Pct(T(ss.kCloudDmg));
            }
            return f;
        }

        double SsChargedStacks()
        {
            var ss = d.Weapon.Ss;
            double rate = Math.Max(0.02, SsSweepsPerSec());
            double speed = 1 + Math.Max(0, T(d.kAs)) / 100.0 * ss.ChargedAsEff / 100.0;
            double avg = Math.Min(ss.ChargedMax, Math.Max(0, speed / rate / ss.ChargedTime - 0.5));
            double L = FightLen() > 0 ? FightLen() : d.Eco != null ? d.Eco.Battle : DefaultBattle;
            double first = 1 / Math.Max(1, rate * L);
            return first * ss.ChargedMax + (1 - first) * avg;
        }

        double SsThrowHits()
        {
            var ss = d.Weapon.Ss;
            double n = ss.ThrowBase + (T(d.kBad) > 0 ? Math.Floor(T(d.kBad) / ss.ThrowPer) : 0);
            return n <= 3 ? n : 3 + 0.5 * (n - 3);
        }

        double SsRingHits()
        {
            var ss = d.Weapon.Ss;
            double n = ss.RingBase;
            if (ss.RingScaled)
            {
                double over = 0;
                for (int e = 0; e < 4; e++) over += Math.Max(0, Math.Truncate(elem[e]) - ss.RingThreshold);
                double extra = Math.Floor(over / ss.RingPer);
                if (ss.RingMax > 0) extra = Math.Min(extra, ss.RingMax);
                n += extra;
            }
            return Math.Max(1, n / 6.0);
        }

        double SsCloudSweepRate()
        {
            var ss = d.Weapon.Ss;
            int cost = (int)(ss.CloudCost - ss.CloudCost * T(ss.kSweepRed) / 100.0);
            if (cost <= 0) return SweepCap;
            if (d.Eco == null || !ComboOn(EcoKind.DarkCloud)) return 0;
            CloudBudget(out var demand, out var supply);
            if (double.IsInfinity(supply)) return SweepCap;
            return Math.Min(SweepCap, Math.Max(0, supply - demand) / cost);
        }

        double SsBasicFactor()
        {
            var ss = d.Weapon.Ss;
            if (ss == null || ss.Mode != 2 || ss.StrikeRate <= 0) return 1;
            double swing = Math.Max(0.05, ss.SwingBase * SwingAs());
            double stacks = SsHaetaeStacks(), ratio = stacks >= ss.ExplHiStack ? ss.ExplRatioHi : ss.ExplRatio;
            double perStrike = ss.StrikeMult + ss.ExplMult * ratio / 100.0 * stacks;
            return 1 + ss.StrikeRate * perStrike / (swing * ss.ComboMult);
        }

        double SsHaetaeStacks()
        {
            if (d.BurnIdx < 0 || d.BurnIdx >= d.Debuffs.Count || Plasma()) return 0;
            var di = d.Debuffs[d.BurnIdx];
            double r = d.BurnIdx < dbR.Length ? dbR[d.BurnIdx] : 0;
            if (r <= 0) return 0;
            DebuffShape(di, out var max, out var add, out _, out _);
            double q = Math.Max(0.05, d.Weapon.Ss.StrikeRate);
            return Math.Min(max, r / q * add);
        }

        double DebuffResetRate(DebuffType t)
        {
            var ss = d.Weapon.Ss;
            if (ss != null && ss.Mode == 2 && ss.StrikeRate > 0 && t == DebuffType.Burn) return ss.StrikeRate;
            var g = d.Weapon.Gs;
            if (g != null && g.Wound && t is DebuffType.Burn or DebuffType.Poison or DebuffType.Wound) return GsSpecialRate();
            return 0;
        }

        double ResetStacks(DebuffType t, double r, double max, double add, double normal)
        {
            double q = DebuffResetRate(t);
            if (q <= 0 || r <= 0) return normal;
            double full = add * r / q;
            double saw = full <= max ? full / 2 : max - max * max / (2 * full);
            return Math.Min(normal, saw);
        }
    }
}
