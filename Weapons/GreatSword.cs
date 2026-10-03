using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed class GsModel
    {
        public double ChargeTime = 1.2, AnimTime = 0.6;
        public bool Rapid;
        public double RapidCrit = 100;
        public bool Outside;
        public double OutsideBonus = 0.5, OutsideShare = 0.75;
        public bool Money;
        public bool Wound;
        public double WoundDmg = 120;
        public bool Amethyst;
        public double AmethystShots = 16, AmethystInterval = 0.5, AmethystLimit = 3;
        public bool Transform;
        public double TransformTime = 8, TransMult = 1, ComboMult = 1, TransAs, TransFlat, TransDefRatio;
        public int kTransDef = -1;
        public bool Blood;
        public double BloodAllDmg, BloodBasicPct;
        public bool Needle;
        public double NeedleCount = 3, NeedleRatio = 0.26, NeedleHit = 0.5;
    }

    static WeaponMoves GreatSwordMoves(WeaponSimple_GreatSword gs, PlayerAvatar a)
    {
        var mv = new WeaponMoves();
        var basic = gs.basicComboAttacks ?? new NewWeaponFireData[0];
        int len = basic.Length, fi = gs.finalComboIdx;
        int n = fi >= 0 && fi < len ? fi + 1 : len;
        var combo = Enumerable.Range(0, n).Select(i => At(basic, i)).ToList();
        bool triple = gs.tripleCombo || HasAddon<WeaponAddonGreatsword_TripleCombo>(gs);
        if (triple && fi >= 0 && fi < n) { combo.Add(combo[fi]); combo.Add(combo[fi]); }
        mv.BasicFd = combo.FirstOrDefault(f => f != null);
        mv.BasicMult = mv.BasicFd != null ? AvgMult(combo) : 1;
        double total = combo.Where(f => f != null).Sum(HitMult);
        if (total > 0 && fi >= 0 && fi < n && combo[fi] != null) mv.FinalShare = (triple ? 3 : 1) * HitMult(combo[fi]) / total;

        mv.DashFd = gs.dashAttacks?.FirstOrDefault(f => f != null);
        mv.DashMult = mv.DashFd != null ? AvgMult(gs.dashAttacks) : 0;
        mv.NoDash = mv.DashFd == null || gs.blockDashAttack || gs.isDashAttackAvailable < 1;

        var special = gs.specialAttacks ?? new NewWeaponFireData[0];
        if (gs.specialAttackToTransform)
        {
            mv.NoSpecial = true;
            mv.SpecialNote = Tr("weapon.reassemble_transform");
        }
        else if (gs.doubleWhirlwind || HasAddon<WeaponAddonGreatsword_DoubleWhirlwind>(gs))
        {
            mv.SpecialFd = At(special, 2) ?? At(special, 0);
            mv.SpecialMult = HitMult(At(special, 2)) + HitMult(At(special, 3));
            mv.SpecialNote = Tr("weapon.double_whirlwind");
        }
        else if (!gs.moneyWhirlwind && StatWithWeapon(a, "WOUNDEXPLOSION", gs) <= 0 && (gs.outsideBonus || HasAddon<WeaponAddonGreatsword_OutsideBonus>(gs)) && At(special, 1) != null)
        {
            mv.SpecialFd = At(special, 1);
            mv.SpecialNote = Tr("weapon.whirlwind_outer_ring_bonus");
        }
        else
        {
            mv.SpecialFd = At(special, 0);
            mv.SpecialNote = gs.moneyWhirlwind ? Tr("weapon.gold_whirlwind") : StatWithWeapon(a, "WOUNDEXPLOSION", gs) > 0 ? Tr("weapon.wound_detonation")
                           : mv.SpecialFd is NewWeaponFireData_Summon ? Tr("weapon.summon_amethyst") : Tr("weapon.whirlwind");
        }
        if (!mv.NoSpecial)
        {
            if (mv.SpecialFd == null) mv.NoSpecial = true;
            else if (mv.SpecialNote != Tr("weapon.double_whirlwind")) mv.SpecialMult = HitMult(mv.SpecialFd);
        }
        return mv;
    }

    void BuildGreatSwordModel(PlayerAvatar avatar, DpsModel d, Func<string, int> K, WeaponSimple_GreatSword gs, Behavior play)
    {
        var w = d.Weapon;
        var g = w.Gs = new GsModel();
        var mv = GreatSwordMoves(gs, avatar);
        bool prefab = gs == weaponOverride;
        g.ChargeTime = GsChargeTime(gs, prefab);
        w.SpecialMp = !gs.moneyWhirlwind && !gs.specialAttackToTransform;
        w.BasicMp = gs.useMPBasicAttack || HasAddon<WeaponAddonGreatsword_UseMPBasicAttack>(gs);
        g.Rapid = gs.rapidWhirlwind || HasAddon<WeaponAddonGreatsword_RapidWhirlwind>(gs);
        g.RapidCrit = ConstOr("greatswordRapidWhirlwindCriticalChancePercent", 100);
        if (mv.SpecialNote == Tr("weapon.whirlwind_outer_ring_bonus"))
        {
            g.Outside = true;
            if (mv.SpecialFd is NewWeaponFireData_MeleeAttack ma && ma.projectilePrefab != null
                && ma.projectilePrefab.GetComponent<MeleeCollision_Circle_Distance>() is { } cd)
                g.OutsideBonus = cd.damageBonus;
        }
        g.Money = gs.moneyWhirlwind;
        if (mv.SpecialNote == Tr("weapon.wound_detonation"))
        {
            g.Wound = true;
            g.WoundDmg = ConstOr("woundExplosionDamage", 120);
        }
        if (mv.SpecialFd is NewWeaponFireData_Summon sm)
        {
            g.Amethyst = true;
            g.AmethystLimit = Math.Max(1, sm.summonLimit);
            var unit = sm.summonPrefab != null ? sm.summonPrefab.GetComponent<UnitAvatar>() : null;
            if (unit != null && unit.maxHp > 0) g.AmethystShots = Math.Max(1, Math.Ceiling(unit.maxHp / 5.0));
            var ai = sm.summonPrefab != null ? sm.summonPrefab.GetComponent<UnitAI_Amethyst>() : null;
            if (ai != null && ai.laserAttackTimer != null && ai.laserAttackTimer.time > 0) g.AmethystInterval = ai.laserAttackTimer.time;
        }
        if (gs.specialAttackToTransform)
        {
            g.Transform = true;
            w.SpecialRate = play.Transform;
            g.TransformTime = gs.transformResetTimer != null && gs.transformResetTimer.time > 0 ? gs.transformResetTimer.time : 10;
            var basic = gs.basicComboAttacks ?? new NewWeaponFireData[0];
            var trans = new[] { At(basic, 3), At(basic, 4) };
            g.TransMult = trans.Any(f => f != null) ? AvgMult(trans) : mv.BasicMult;
            g.ComboMult = Math.Max(0.01, mv.BasicMult);
            var fast = gs.addons?.OfType<WeaponAddonGreatsword_BasicAttackFaster>().FirstOrDefault(x => x != null);
            if (fast != null) g.TransAs = fast.maxAddableAttackSpeed * 0.9;
            var tb = gs.addons?.OfType<WeaponAddonCommon_TransformAttackBonus>().FirstOrDefault(x => x != null);
            if (tb != null)
            {
                g.TransFlat = ConstOr(tb.transformAttackBonusDamageConstKey, 15);
                g.TransDefRatio = ConstOr(tb.relatedStatBonusConstKey, 50) / 100.0;
                g.kTransDef = K((tb.relatedStatUnsafe ?? "DAMAGEREDUCTION").ToUpperInvariant());
            }
            var bb = gs.addons?.OfType<WeaponAddonGreatsword_BoneBlood>().FirstOrDefault(x => x != null);
            if (bb != null)
            {
                double maxHp = Math.Max(0, avatar.MaxHp);
                g.Blood = true;
                g.BloodAllDmg = Math.Max(0, Math.Floor(maxHp * 0.9) - bb.transformedMaxHp) * bb.damagePerBloodStack;
                g.BloodBasicPct = bb.maxHpPerBonusAmount > 0 ? Math.Floor(Math.Max(0, maxHp - bb.transformedMaxHp) / bb.maxHpPerBonusAmount) * bb.basicAttackBonusPerAmount : 0;
                double share = Math.Min(1, play.Transform * g.TransformTime * Pct(avatar.GetCustomStat(ECustomStat.BuffDuration)));
                int kAll = K("ALLDAMAGEBONUS");
                d.ExtraBase[kAll] = d.ExtraBase.GetValueOrDefault(kAll) + g.BloodAllDmg * share;
            }
        }
        var needle = gs.addons?.OfType<WeaponAddonGreatsword_FireNeedleBullet>().FirstOrDefault(x => x != null);
        if (needle != null)
        {
            g.Needle = true;
            g.NeedleCount = Math.Max(1, needle.needleBulletCount);
            g.NeedleRatio = needle.needleBulletDamageRatio;
        }
    }

    static double GsChargeTime(WeaponSimple_GreatSword gs, bool prefab)
    {
        double t = gs.sweepTimer != null && gs.sweepTimer.time > 0 ? gs.sweepTimer.time : 0.7;
        if (prefab) foreach (var _ in gs.addons?.OfType<WeaponAddonGreatsword_BasicAttack>() ?? Enumerable.Empty<WeaponAddonGreatsword_BasicAttack>()) t *= 2;
        bool superQuick = gs.superQuickSweep || HasAddon<WeaponAddonGreatsword_SuperQuick>(gs);
        bool quick = gs.quickSweep || HasAddon<WeaponAddonGreatsword_Quick>(gs);
        bool longCharge = gs.longCharge || HasAddon<WeaponAddonGreatsword_LongCharge>(gs);
        if (longCharge) t /= 0.6;
        else if (superQuick) t /= 100;
        else if (quick) t /= 1.5;
        return t;
    }

    void BuildGreatSwordSweep(PlayerAvatar avatar, DpsModel d, Func<string, int> K, WeaponSimple_GreatSword gs, Behavior play, ArrModel m)
    {
        string Kind(WeaponSimple_GreatSword x) => x.specialAttackToTransform ? "T" : x.moneyWhirlwind ? "M" : (x.specialAttacks?.FirstOrDefault(f => f != null) is NewWeaponFireData_Summon ? "S" : "W");
        if (play.SpecialMeasured && weaponOverride != null && swingController != null
            && swingController.currentWeapon is WeaponSimple_GreatSword cur && Kind(cur) != Kind(gs))
        {
            play.Special = PlaySpecial;
            play.SpecialMeasured = false;
        }
        double charge = GsChargeTime(gs, gs == weaponOverride);
        double specAs = Math.Max(0.1, (avatar.GetCustomStat(ECustomStat.SpecialAttackSpeed) + 100) / 100.0);
        double cap = 1 / Math.Max(0.2, charge + 0.6 / specAs);
        if (gs.moneyWhirlwind)
        {
            if (!play.SpecialMeasured) play.Special = cap;
            m.Notes.Add(Tr("weapon.gold_whirlwind_30_gold", play.Special));
            return;
        }
        double poolPeriod = play.Single ? d.FightLen / Math.Max(1, BossRefillsPerFight()) : BattleLength();
        double cost0 = Math.Max(0, gs.sweepCost - gs.sweepCost * gs.sweepCostBonus / 100.0);
        double cost = Math.Floor(cost0 * Math.Max(0, 1 - avatar.GetCustomStatUnsafe("SPECIALATTACKCOSTREDUCTION") / 100.0));
        double rate = avatar.GetCustomStatUnsafe("INFINITYMP") > 0 ? cap
            : SweepRate(cost, avatar.GetCustomStatUnsafe("MPREGEN") * MpGain(avatar), avatar.GetCustomStatUnsafe("MPRESONANCE"), avatar.GetCustomStat(ECustomStat.MPSteal) * MpGain(avatar),
                        SweepDps(avatar), avatar.MaxMp - avatar.reservedMp, poolPeriod, cap: cap);
        d.SweepCost = cost;
        play.SweepCost = cost;
        play.SpecialByMp = cost > 0;
        if (!play.SpecialMeasured && rate > 0) play.Special = rate;
        if (cost > 0 && play.Special > 0)
            d.Sweep = new SweepModel
            {
                Mode = 0, Base = play.Special, Measured = play.SpecialMeasured, Cost0 = cost0, PoolPeriod = poolPeriod, Cap = cap,
                kCostRed = -1, kSpecCostRed = K("SPECIALATTACKCOSTREDUCTION"), kRegen = K("MPREGEN"),
                kResonance = K("MPRESONANCE"), kSteal = K("MPSTEAL"), Dps = SweepDps(avatar), Reserved = avatar.reservedMp
            };
        string what = gs.specialAttackToTransform ? Tr("weapon.transform") : Tr("weapon.whirlwind");
        m.Notes.Add(play.SpecialMeasured
            ? Tr("weapon.great_sword_measured_s", what, play.Special, cost)
            : Tr("weapon.great_sword_mp_budget", what, play.Special, cost, charge));
        if (gs.specialAttackToTransform)
        {
            play.Transform = play.Special;
            play.Special = 0;
            play.SpecialMeasured = false;
        }
    }

    void GreatSwordLiveState(PlayerAvatar avatar, Func<string, int> K, Dictionary<int, double> curRaw)
    {
        if (avatar.GetComponent<WeaponControllerSimple>()?.currentWeapon is not WeaponSimple_GreatSword live || live.addons == null) return;
        foreach (var ad in live.addons)
            switch (ad)
            {
                case WeaponAddonGreatsword_BasicAttackFaster fast when fast.addedAttackSpeed != 0:
                {
                    int k = K("ATTACKSPEED");
                    curRaw[k] = curRaw.GetValueOrDefault(k) + fast.addedAttackSpeed;
                    break;
                }
                case WeaponAddonGreatsword_BoneBlood bb when FieldValue(bb, "appliedDamageBonus") is int applied && applied != 0:
                {
                    int k = K("ALLDAMAGEBONUS");
                    curRaw[k] = curRaw.GetValueOrDefault(k) + applied;
                    break;
                }
            }
    }

    void WeaponSpecialBuffs(PlayerAvatar avatar, ArrModel m, DpsModel d, Func<string, int> K, WeaponSimple weapon, Behavior play, Dictionary<int, double> curRaw)
    {
        var live = avatar.GetComponent<WeaponControllerSimple>()?.currentWeapon;
        if (live != null && live.addons != null && BuffsField?.GetValue(avatar) is Dictionary<string, CharacterBuff> buffs)
            foreach (var ao in live.addons.OfType<WeaponAddonCommon_AddDarkCloudOnAttack>())
                if (ao?.specialAttackBuffPrefab != null && !string.IsNullOrEmpty(ao.specialAttackBuffPrefab.ID)
                    && buffs.TryGetValue(ao.specialAttackBuffPrefab.ID, out var active) && active != null)
                    foreach (var (key, mode, v) in BuffStats(ao.specialAttackBuffPrefab))
                        if (mode == 0) curRaw[K(key)] = curRaw.GetValueOrDefault(K(key)) + v * active.Amplified * active.CurrentStack;
        if (weapon == null || weapon.addons == null || play.Special <= 0) return;
        foreach (var ao in weapon.addons.OfType<WeaponAddonCommon_AddDarkCloudOnAttack>())
        {
            var prefab = ao?.specialAttackBuffPrefab;
            if (prefab == null) continue;
            double dur = prefab.defaultDuration * (prefab.ignoreDurationBonus ? 1 : Pct(avatar.GetCustomStat(ECustomStat.BuffDuration)));
            double avg = BuffAvgStacks(play.Special, dur, prefab.MaxStackCount);
            foreach (var (key, mode, v) in BuffStats(prefab))
                if (mode == 0) d.ExtraBase[K(key)] = d.ExtraBase.GetValueOrDefault(K(key)) + v * avg;
            m.Notes.Add(Tr("weapon.special_attack_buff_special", play.Special, avg));
        }
    }

    sealed partial class Evaluator
    {
        double GsSpecialFactor()
        {
            var g = d.Weapon.Gs;
            if (g == null) return 1;
            double f = 1;
            if (g.Outside) f *= 1 + g.OutsideBonus * g.OutsideShare;
            if (g.Money) f *= Pct(T(d.kBad));
            if (g.Wound) f *= g.WoundDmg * GsWoundStacks();
            if (g.Amethyst) f *= GsAmethystShots();
            return f;
        }

        double GsSpecialRate() => Math.Max(0, d.Weapon.SpecialRate * SpecialScale());

        double GsWoundStacks()
        {
            double q = Math.Max(0.05, GsSpecialRate()), sum = 0;
            bool plasma = Plasma();
            for (int i = 0; i < d.Debuffs.Count && i < dbR.Length; i++)
            {
                var di = d.Debuffs[i];
                if (plasma && di.Type is DebuffType.Burn or DebuffType.Electric) continue;
                if (!plasma && di.Type == DebuffType.Plasma) continue;
                double r = dbR[i];
                if (r <= 0) continue;
                DebuffShape(di, out var max, out var add, out _, out _);
                sum += Math.Min(max, r / q * add);
            }
            return sum;
        }

        double GsAmethystShots()
        {
            var g = d.Weapon.Gs;
            double q = GsSpecialRate();
            if (q <= 0) return g.AmethystShots;
            double maxPerSec = g.AmethystLimit / g.AmethystInterval * Pct(T(d.kAs));
            return Math.Min(g.AmethystShots, maxPerSec / q);
        }

        double GsTransformShare()
        {
            var g = d.Weapon.Gs;
            if (g == null || !g.Transform) return 0;
            return Math.Min(1, GsSpecialRate() * g.TransformTime * Pct(T(d.kBuffDur)));
        }

        double GsBasicFactor(double hitDamage)
        {
            var g = d.Weapon.Gs;
            if (g == null || !g.Transform) return 1;
            double share = GsTransformShare();
            if (share <= 0) return 1;
            double asNow = WeaponAs(), asTrans = Math.Max(0.1, ((T(d.kAs) + g.TransAs) * (1 + d.AsAmp) + 100) / 100.0);
            double trans = g.TransMult / g.ComboMult * (asTrans / asNow) * Pct(T(d.kMpSkill));
            if (g.TransFlat > 0 && hitDamage > 0)
                trans *= 1 + (g.TransFlat + Math.Max(0, T(g.kTransDef)) * g.TransDefRatio) / (hitDamage * g.TransMult);
            if (g.Blood) trans *= Pct(g.BloodBasicPct);
            return (1 - share) + share * trans;
        }

        double GsCritAdd(DpsSource s)
        {
            var g = d.Weapon.Gs;
            return g != null && g.Rapid && s.Kind == SrcKind.WeaponSpecial ? g.RapidCrit : 0;
        }

        double GsNeedleFactor(double critPct, double critRate)
        {
            var g = d.Weapon.Gs;
            if (g == null || !g.Needle) return 0;
            double extra = Math.Max(0, Math.Floor((critRate - 50) / 20.0));
            return Math.Min(1, Math.Max(0, critPct / 100.0)) * (g.NeedleCount + extra) / g.NeedleCount;
        }
    }
}
