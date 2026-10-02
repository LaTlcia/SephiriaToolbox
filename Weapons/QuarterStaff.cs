using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed class QsModel
    {
        public bool Crystal;
        public bool FollowerBasic;
        public double CrystalMult, SwingMult = 1;
    }

    static int StaffSwingsPerCombo(WeaponSimple_QuartterStaff qs, int entries) => qs.attackMoveSet switch
    {
        0 => Math.Min(entries, 4),
        1 => Math.Min(entries, 4),
        _ => entries
    };

    static WeaponMoves QuarterStaffMoves(WeaponSimple_QuartterStaff qs, Behavior play)
    {
        var mv = new WeaponMoves();
        var basic = qs.basicComboAttacks ?? new NewWeaponFireData[0];
        if (qs.isNormalAttackRolling && qs.rollingFireData != null)
        {
            mv.BasicFd = qs.rollingFireData;
            mv.BasicMult = RandomBoltAttack(qs.rollingFireData) ? 0 : HitMult(qs.rollingFireData);
        }
        else
        {
            int len = basic.Length, fi = qs.finalComboIdx;
            int n = fi >= 0 && fi < len ? fi + 1 : len;
            var combo = Enumerable.Range(0, n).Select(i => At(basic, i)).Where(f => f != null).ToArray();
            mv.BasicFd = combo.FirstOrDefault();
            double total = combo.Sum(HitMult);
            int swings = Math.Max(1, StaffSwingsPerCombo(qs, combo.Length));
            mv.BasicMult = mv.BasicFd != null ? total / swings : 1;
            if (total > 0 && fi >= 0 && fi < n && At(basic, fi) != null) mv.FinalShare = HitMult(At(basic, fi)) / total;
        }
        mv.DashFd = At(qs.dashAttacks, 0);
        mv.DashMult = HitMult(mv.DashFd);
        mv.NoDash = mv.DashFd == null || qs.isDashAttackAvailable < 1;
        var over = qs.addons?.OfType<WeaponAddonCommon_ChangeWeaponAction>()
                     .FirstOrDefault(a => a != null && a.fireData != null && string.Equals(a.changeWeaponActionName, "SPECIALATTACK", StringComparison.OrdinalIgnoreCase));
        var first = over != null ? over.fireData : At(qs.specialAttacks, 0);
        mv.SpecialFd = first;
        if (first == null) mv.NoSpecial = true;
        else if (qs.changedSpecialAttackParameter == "FLAMESPEAR" || HasFlameSpear(qs))
        {
            mv.SpecialMult = HitMult(first);
            mv.SpecialNote = Tr("weapon.flame_spear_consumes_solar");
        }
        else
        {
            double pg = Math.Min(1, Math.Max(0, play.Damaged) * 0.3);
            double second = (1 - pg) * HitMult(qs.secondSpecialAttackFireData) + pg * HitMult(qs.secondSpecialAttackEnhancedFireData ?? qs.secondSpecialAttackFireData);
            mv.SpecialMult = HitMult(first) + second;
            mv.SpecialNote = Tr("weapon.special_attack_two_hits");
        }
        return mv;
    }

    static bool RandomBoltAttack(NewWeaponFireData fd) =>
        fd is NewWeaponFireData_SpecialProjectile sp && sp.projectilePrefab != null && sp.projectilePrefab.GetComponent<SpecialProjectile_RandomBolt>() != null;

    static bool HasFlameSpear(WeaponSimple_QuartterStaff qs) =>
        qs.addons != null && qs.addons.OfType<WeaponAddonCommon_ChangeWeaponAction>()
          .Any(a => a != null && string.Equals(a.changeWeaponActionName, "SPECIALATTACK", StringComparison.OrdinalIgnoreCase)
                    && (a.newActionParameter ?? "").IndexOf("FLAMESPEAR", StringComparison.OrdinalIgnoreCase) >= 0);

    void BuildQuarterStaffModel(PlayerAvatar avatar, DpsModel d, Func<string, int> K, WeaponSimple_QuartterStaff qs, Behavior play, ArrModel m)
    {
        var q = d.Weapon.Qs = new QsModel();
        q.FollowerBasic = HasAddon<WeaponAddomCommon_BasicAttackWithFollowerDamage>(qs);
        if (q.FollowerBasic) m.Notes.Add(Tr("weapon.sacred_jar_normal_attack"));
        if (qs.isNormalAttackRolling && !play.SwingMeasured)
            play.Swing = 1 / Math.Max(0.05, qs.attackWeightPerSwing);
        if (qs.isNormalAttackRolling && RandomBoltAttack(qs.rollingFireData))
        {
            play.SwingHit = 0;
            d.Weapon.BoltSwing = play.Swing;
            m.Notes.Add(Tr("weapon.bolt_barrage", play.Swing * AttackSpeedFactor(avatar.GetCustomStat(ECustomStat.AttackSpeed), 0)));
        }
        var mv = QuarterStaffMoves(qs, play);
        if (qs.isCrystalExplosion && qs.crystalExplosionFireData != null)
        {
            q.Crystal = true;
            q.CrystalMult = HitMult(qs.crystalExplosionFireData);
            q.SwingMult = Math.Max(0.01, mv.BasicMult);
        }
        if (mv.NoSpecial) { play.Special = 0; return; }
        if (mv.SpecialNote == Tr("weapon.flame_spear_consumes_solar")) return;
        double cap = 1.1, cost0 = 5;
        double cost = Math.Floor(Math.Max(0, cost0 * (1 - avatar.GetCustomStatUnsafe("SPECIALATTACKCOSTREDUCTION") / 100.0)));
        double poolPeriod = play.Single ? d.FightLen / Math.Max(1, BossRefillsPerFight()) : BattleLength();
        double rate = avatar.GetCustomStatUnsafe("INFINITYMP") > 0 ? cap
            : SweepRate(cost, avatar.GetCustomStatUnsafe("MPREGEN"), avatar.GetCustomStatUnsafe("MPRESONANCE"), avatar.GetCustomStat(ECustomStat.MPSteal),
                        SweepDps(avatar), avatar.MaxMp - avatar.reservedMp, poolPeriod, cap: cap);
        if (!play.SpecialMeasured) play.Special = rate;
        play.SweepCost = cost;
        play.SpecialByMp = cost > 0;
        if (cost > 0 && play.Special > 0)
            d.Sweep = new SweepModel
            {
                Mode = 0, Base = play.Special, Measured = play.SpecialMeasured, Cost0 = cost0, Cap = cap, PoolPeriod = poolPeriod,
                kCostRed = -1, kSpecCostRed = K("SPECIALATTACKCOSTREDUCTION"), kRegen = K("MPREGEN"),
                kResonance = K("MPRESONANCE"), kSteal = K("MPSTEAL"), Dps = SweepDps(avatar), Reserved = avatar.reservedMp
            };
        m.Notes.Add(Tr("weapon.quarterstaff_special_attack_about", play.Special, cost, (play.SpecialMeasured ? Tr("weapon.measured") : Tr("weapon.mp_budget"))));
    }

    sealed partial class Evaluator
    {
        double QsBasicFactor(DpsSource s)
        {
            var q = d.Weapon.Qs;
            if (q == null) return 1;
            double f = q.FollowerBasic && d.kFollowerDmg >= 0 ? Pct(Math.Max(0, T(d.kFollowerDmg))) : 1;
            return f * QsCrystalFactor(s);
        }

        double QsCrystalFactor(DpsSource s)
        {
            var q = d.Weapon.Qs;
            if (!q.Crystal || d.GuardRate <= 0) return 1;
            double swings = Math.Max(0.05, d.Hits.Swing * WeaponAs());
            double share = Math.Min(1, d.GuardRate / swings);
            double en = Math.Max(1e-6, ElemFor(s.Formula, s.Element, elem));
            double best = Math.Max(Math.Max(elem[0], elem[1]), Math.Max(elem[2], elem[3]));
            return 1 - share + share * best * q.CrystalMult / (en * q.SwingMult);
        }
    }
}
