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
        public int kDouble = -1;
        public double DoublePct;
        public bool FlameSpear;
    }

    const double SoldierFixedSeconds = 1.5;

    static int StaffSwingsPerCombo(WeaponSimple_QuartterStaff qs, int entries) => qs.attackMoveSet switch
    {
        0 => Math.Min(entries, 4),
        1 => Math.Min(entries, 4),
        _ => entries
    };

    static WeaponMoves QuarterStaffMoves(WeaponSimple_QuartterStaff qs, PlayerAvatar a, Behavior play)
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
            mv.BasicMult *= 1 - StaffSummonShare(qs, a, play, out _, out _);
            double heavy = StaffHeavyUptime(qs, a, play);
            var last = fi >= 0 && fi < n ? At(basic, fi) : null;
            if (heavy > 0 && last != null && mv.BasicMult > 0)
            {
                double mult = (1 - heavy) * mv.BasicMult + heavy * HitMult(last);
                mv.FinalShare = ((1 - heavy) * mv.BasicMult * mv.FinalShare + heavy * HitMult(last)) / mult;
                mv.BasicMult = mult;
            }
        }
        var dashOver = qs.addons?.OfType<WeaponAddonCommon_ChangeWeaponAction>()
                         .FirstOrDefault(x => x != null && x.fireData != null && string.Equals(x.changeWeaponActionName, "DASHATTACK", StringComparison.OrdinalIgnoreCase));
        mv.DashFd = dashOver != null ? dashOver.fireData : At(qs.dashAttacks, 0);
        mv.DashMult = HitMult(mv.DashFd) * CloudBottleCount(mv.DashFd, a);
        mv.NoDash = mv.DashFd == null || qs.isDashAttackAvailable < 1 || qs.isNormalAttackRolling;
        int lastDash = a != null ? StatWithWeapon(a, "SPEARDASHATTACKBONUSBYLASTDASH", qs) : 0;
        if (lastDash > 0 && !mv.NoDash)
        {
            double dashes = Math.Min(Math.Max(1, ConstOr("staffSpearT3DashLimit", 6)), Math.Max(1, buildDash / Math.Max(0.02, play.DashAttack)));
            mv.DashMult *= 1 + lastDash * dashes / 100.0;
        }
        var over = qs.addons?.OfType<WeaponAddonCommon_ChangeWeaponAction>()
                     .FirstOrDefault(x => x != null && x.fireData != null && string.Equals(x.changeWeaponActionName, "SPECIALATTACK", StringComparison.OrdinalIgnoreCase));
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

    static double CloudBottleCount(NewWeaponFireData fd, PlayerAvatar a)
    {
        if (fd is not NewWeaponFireData_SpecialProjectile sp || sp.projectilePrefab == null) return 1;
        var bottle = sp.projectilePrefab.GetComponent<SpecialProjectile_ThrowCloudBottle>();
        if (bottle == null) return 1;
        int n = 0;
        try
        {
            if (a != null && a.Inventory != null)
                foreach (var c in a.Inventory.charms.Values)
                    if (c != null && c.IsEffectEnabled && c.Item != null && c.Item.EntityID == bottle.countTargetEntityItemID) n++;
        }
        catch { }
        return Math.Max(1, n);
    }

    static bool ThrowsCloudBottle(WeaponSimple_QuartterStaff qs) =>
        qs.changedDashAttackParameter == "CLOUDBOTTLE"
        || qs.addons != null && qs.addons.OfType<WeaponAddonCommon_ChangeWeaponAction>()
             .Any(a => a != null && string.Equals(a.changeWeaponActionName, "DASHATTACK", StringComparison.OrdinalIgnoreCase)
                       && (a.newActionParameter ?? "").IndexOf("CLOUDBOTTLE", StringComparison.OrdinalIgnoreCase) >= 0);

    static double StaffHeavyUptime(WeaponSimple_QuartterStaff qs, PlayerAvatar a, Behavior play)
    {
        if (qs.addons == null) return 0;
        foreach (var gb in qs.addons.OfType<WeaponAddonCommon_GuardBuff>())
        {
            if (gb?.buffPrefab == null || !BuffStats(gb.buffPrefab).Any(st => st.Key == "STAFFGUARDEXTEND")) continue;
            double dur = gb.buffPrefab.defaultDuration * (gb.buffPrefab.ignoreDurationBonus || a == null ? 1 : Pct(a.GetCustomStat(ECustomStat.BuffDuration)));
            return BuffAvgStacks(Math.Max(0, play.Guard) * Math.Min(100, Math.Max(0, gb.buffPercent)) / 100.0, dur, 1);
        }
        return 0;
    }

    static double StaffSummonShare(WeaponSimple_QuartterStaff qs, PlayerAvatar a, Behavior play, out double soldiers, out NewWeaponFireData_Summon sm)
    {
        soldiers = 0;
        sm = qs.summonOnBasicAttackFireData as NewWeaponFireData_Summon;
        if (sm == null || sm.summonPrefab == null || !HasAddon<WeaponAddomCommon_BasicAttackWithFollowerDamage>(qs)) return 0;
        double chance = Math.Min(100, Math.Max(0, ConstOr("staffSummonSoldierPercent", 20))) / 100.0;
        double swings = Math.Max(0.05, play.Swing * (a != null ? AttackSpeedFactor(a.GetCustomStat(ECustomStat.AttackSpeed), qs.attackSpeedAmplify) : 1));
        double life = sm.isTemporaryUnit ? Math.Max(1, sm.temporaryUnitLifeTime) : 60;
        soldiers = Math.Min(Math.Max(1, sm.summonLimit), swings * chance * life);
        return Math.Min(chance, soldiers / life / swings);
    }

    void AddStaffSoldierSource(PlayerAvatar avatar, DpsModel d, WeaponSimple weapon, Behavior play, List<DpsSource> sources)
    {
        if (weapon is not WeaponSimple_QuartterStaff qs) return;
        StaffSummonShare(qs, avatar, play, out double soldiers, out var sm);
        if (sm == null || soldiers <= 0) return;
        var ai = sm.summonPrefab.GetComponent<UnitAI_Soldier>();
        var unit = sm.summonPrefab.GetComponent<Unit_Soldier>();
        double wait = ai != null && ai.tryAttackTimer != null && ai.tryAttackTimer.time > 0 ? ai.tryAttackTimer.time * 0.875 : 1.2;
        double fixedSeconds = SoldierFixedSeconds;
        if (SummonCadence(sm.summonPrefab, out double w2, out double f2)) { wait = w2; fixedSeconds = f2; }
        double perSec = 1 / (wait + fixedSeconds);
        double mult = sm.damageMultiplier * (unit != null && unit.fireData != null ? unit.fireData.damageMultiplier : 1);
        var s = new DpsSource
        {
            Kind = SrcKind.Ability, Name = Tr("src.weapon_soldiers"), Preset = true, Summon = true, ElemIdx = 0, DmgElem = 0,
            MulKeys = new[] { d.kWdb, d.kBad, d.kFwd }, Prior = soldiers * perSec * mult, TheoryK = soldiers * perSec * mult, HasTheory = true,
            SummonWait = wait, SummonFixed = fixedSeconds,
            Note = Tr("model.staff_soldiers", soldiers, 1 / perSec, mult)
        };
        if (!string.IsNullOrEmpty(sm.followerDamageId)) s.Ids.Add(sm.followerDamageId);
        sources.Add(s);
    }

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
        var mv = QuarterStaffMoves(qs, avatar, play);
        if (qs.isCrystalExplosion && qs.crystalExplosionFireData != null)
        {
            q.Crystal = true;
            q.CrystalMult = HitMult(qs.crystalExplosionFireData);
            q.SwingMult = Math.Max(0.01, mv.BasicMult);
        }
        double heavy = StaffHeavyUptime(qs, avatar, play);
        if (heavy > 0) m.Notes.Add(Tr("weapon.heavy_staff", play.Guard, heavy));
        int dbl = StatWithWeapon(avatar, "STAFFMULTITHROWINGSPEARDOUBLE", qs);
        if (dbl > 0 && qs.doubleBasicComboAttacks != null && qs.doubleBasicComboAttacks.Length > 0)
        {
            q.DoublePct = dbl / 100.0;
            q.kDouble = K("EVASION");
            m.Notes.Add(Tr("weapon.double_spear", q.DoublePct, Math.Min(100, q.DoublePct * avatar.GetCustomStat(ECustomStat.Evasion) / 100.0)));
        }
        if (qs.addons != null)
            foreach (var da in qs.addons.OfType<WeaponAddonCommon_DebuffAttack>())
                if (da?.debuffPrefab is CharacterDebuff_MagicWound mw)
                {
                    double hits = (play.Swing * play.SwingHit * AttackSpeedFactor(avatar.GetCustomStat(ECustomStat.AttackSpeed), WeaponAsAmp(qs)) + play.Special + play.DashAttack) * play.HitsPerSwing;
                    double stacks = BuffAvgStacks(hits * Math.Min(100, Math.Max(0, da.debuffPercent)) / 100.0, mw.defaultDuration, Math.Max(1, mw.maxStackCount));
                    d.Weapon.MagicWoundPct = mw.magicDamageBonusPerStack * stacks;
                    m.Notes.Add(Tr("weapon.magic_wound", stacks, d.Weapon.MagicWoundPct));
                }
        if (mv.NoSpecial) { play.Special = 0; return; }
        if (mv.SpecialNote == Tr("weapon.flame_spear_consumes_solar"))
        {
            q.FlameSpear = true;
            var fs = avatar.Inventory != null ? avatar.Inventory.FindComboEffect("FLAMESWORD") as ComboEffect_FlameSword : null;
            bool active = fs != null && fs.isEnabled && fs.isFlameSwordEnabled;
            if (!active) { play.Special = 0; play.SpecialMeasured = false; m.Notes.Add(Tr("weapon.flame_spear_needs_combo")); return; }
            if (!play.SpecialMeasured)
            {
                double spearCost = Math.Max(1, ConstOr("staffFlameSpearCost", 3));
                double fired = Math.Min(1 / Math.Max(0.02, fs.minCooldownTime),
                                        play.Swing * play.SwingHit * AttackSpeedFactor(avatar.GetCustomStat(ECustomStat.AttackSpeed), WeaponAsAmp(qs)) * play.HitsPerSwing + play.DashAttack + 0.3);
                int pick = StatWithWeapon(avatar, "FLAMESWORDPICKBONUS", qs);
                play.Special = pick > 0 ? Math.Min(1.1, fired * pick / spearCost) : PlaySpecial;
            }
            m.Notes.Add(Tr("weapon.flame_spear_rate", play.Special, (play.SpecialMeasured ? Tr("weapon.measured") : "")));
            return;
        }
        double cap = 1.1, cost0 = 5;
        double cost = Math.Floor(Math.Max(0, cost0 * (1 - avatar.GetCustomStatUnsafe("SPECIALATTACKCOSTREDUCTION") / 100.0)));
        double poolPeriod = play.Single ? d.FightLen / Math.Max(1, BossRefillsPerFight()) : BattleLength();
        double rate = avatar.GetCustomStatUnsafe("INFINITYMP") > 0 ? cap
            : SweepRate(cost, avatar.GetCustomStatUnsafe("MPREGEN") * MpGain(avatar), avatar.GetCustomStatUnsafe("MPRESONANCE"), avatar.GetCustomStat(ECustomStat.MPSteal) * MpGain(avatar),
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
            if (q.kDouble >= 0) f *= 1 + Math.Min(100, Math.Max(0, q.DoublePct * T(q.kDouble) / 100.0)) / 100.0;
            return f * QsCrystalFactor(s);
        }

        double QsFlameSpearFactor()
        {
            var e = d.Eco;
            if (e == null) return 1;
            return Pct(T(e.kFsDmg)) * (1 + Math.Min(Math.Max(T(e.kFsLuck), 0), 100) / 100.0 * e.FsLuckBonus / 100.0);
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
