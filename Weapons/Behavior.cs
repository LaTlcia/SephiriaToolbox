using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public partial class SephiriaToolbox
{
    const double PlaySpecial = 0.2, PlayDash = 0.3, PlayParry = 0.12, PlayGuard = 0.12, PlayEvade = 0.1, PlayDamaged = 0.4,
                 PlayKill = 0.35, PlaySummon = 0.8, PlayEnemies = 2.5, PlayUnknown = 0.3, PlayAbility = 0.5, PlayDebuff = 1.0,
                 PlayKillBoss = 0.05;

    static double SwingDefault(EWeaponType t) => t switch
    {
        EWeaponType.SwordAndShield => 2.4,
        EWeaponType.GreatSword => 1.3,
        EWeaponType.Dagger => 3.2,
        EWeaponType.Crossbow => 2.0,
        EWeaponType.StaffMagic => 1.8,
        EWeaponType.Katana => 2.2,
        EWeaponType.Golem => 2.0,
        EWeaponType.Staff => 1.8,
        _ => 2.0
    };

    sealed class Behavior
    {
        public double Swing;
        public int ComboLength;
        public double Guard = PlayGuard, PerfectGuard = PlayParry * 0.5, Parry = PlayParry, Damaged = PlayDamaged;
        public bool DefendMeasured;
        public bool SwingMeasured;
        public double AttackWeight = 0.33;
        public bool Single;
        public double Enemies = PlayEnemies;
        public double HitsPerSwing = 1;
        public double SwingHit = 1;
        public double EnemiesM = PlayEnemies, HitsPerSwingM = 1;
        public double Kill => Single ? PlayKillBoss : PlayKill;
        public double Special = PlaySpecial;
        public bool SpecialMeasured, SpecialByMp;
        public double DashAttack = PlayDash;
        public bool DashAttackMeasured;
        public bool Rapier;
        public double Strike;
        public double Transform;
        public double ParrySwing = -1, FuryMeasured = -1;
        public double SweepCost;
        public EWeaponType Weapon;
        public double MpPerSec;
        public double PhysCritHits;
        public double IceShare;
        public double FrostbiteRate;
    }

    uint swingOwner;
    int swingWeaponId = -1, lastSwingValue = -1, swingCount, dashAttackCount, specialAnimCount, pendingSwing = -1, parryAnimCount, furyAnimCount;
    double swingWeighted;
    float swingCombatStart;
    WeaponControllerSimple swingController;
    readonly Dictionary<string, byte> clipKinds = new(StringComparer.Ordinal);

    void TrackSwings()
    {
        var a = LocalAvatar();
        if (a == null) return;
        if (swingOwner != a.netId || swingController == null)
        {
            swingOwner = a.netId;
            swingController = a.GetComponent<WeaponControllerSimple>();
            ResetSwings(-1);
        }
        var wc = swingController;
        var w = wc != null ? wc.currentWeapon : null;
        if (w == null) return;
        if (w.entityId != swingWeaponId) ResetSwings(w.entityId);
        if (pendingSwing >= 0)
        {
            CountSwing(a, wc, w, pendingSwing);
            pendingSwing = -1;
        }
        int cur = wc.currentWeaponSwing;
        if (cur >= 0 && cur != lastSwingValue) pendingSwing = cur;
        lastSwingValue = cur;
    }

    void CountSwing(PlayerAvatar a, WeaponControllerSimple wc, WeaponSimple w, int fireId)
    {
        switch (SwingKind(wc.animator, w, fireId))
        {
            case 1: dashAttackCount++; break;
            case 2:
                specialAnimCount++;
                if (w is WeaponSimple_Dagger)
                {
                    if (fireId == 10) parryAnimCount++;
                    else if (fireId == 20) furyAnimCount++;
                }
                break;
            default:
                swingCount++;
                swingWeighted += 1.0 / AttackSpeedFactor(a.GetCustomStat(ECustomStat.AttackSpeed), w.attackSpeedAmplify);
                break;
        }
    }

    byte SwingKind(Animator an, WeaponSimple w, int fireId)
    {
        if (w is WeaponSimple_SwordAndShield { isFlameEaterHaetaeEnabled: true } && fireId == 20) return 2;
        if (w is WeaponSimple_Crossbow { isMinigunFiring: true }) return 2;
        if (w is WeaponSimple_Katana { isBladeSheathed: true }) return 2;
        if (an == null) return 0;
        byte kind = 0;
        for (int layer = 0; layer < an.layerCount; layer++)
        {
            foreach (var ci in an.GetCurrentAnimatorClipInfo(layer)) kind = Math.Max(kind, ClipKind(ci.clip));
            if (an.IsInTransition(layer))
                foreach (var ci in an.GetNextAnimatorClipInfo(layer)) kind = Math.Max(kind, ClipKind(ci.clip));
        }
        return kind == 3 ? (byte)1 : kind;
    }

    byte ClipKind(AnimationClip clip)
    {
        if (clip == null) return 0;
        if (clipKinds.TryGetValue(clip.name, out var k)) return k;
        k = 0;
        if (clip.name.IndexOf("DashAttack", StringComparison.OrdinalIgnoreCase) >= 0) k = 3;
        else
            foreach (var ev in clip.events)
                if (ev.functionName is "BeginSpecialAttackAnimation" or "BeginSpecialAttackAnimationFullyManual") { k = 2; break; }
        return clipKinds[clip.name] = k;
    }

    void ResetSwings(int weaponId)
    {
        swingWeaponId = weaponId;
        swingCount = dashAttackCount = specialAnimCount = parryAnimCount = furyAnimCount = 0;
        swingWeighted = 0;
        lastSwingValue = pendingSwing = -1;
        swingCombatStart = runCombatTime;
    }

    static double MoveSetSpeed(WeaponSimple w) => w is WeaponSimple_GreatSword && w.attackMoveSet == 1 ? 1.15 : 1;

    static double AttackSpeedFactor(double attackSpeed, double amplify) => Math.Max(0.1, (attackSpeed * (1 + amplify) + 100) / 100.0);

    Behavior MeasureBehavior(WeaponSimple weapon, bool single)
    {
        var b = new Behavior { Swing = weapon != null ? SwingDefault(weapon.weaponType) : 2.0, Single = single };
        if (EnemiesMeasured(out var enemies)) b.EnemiesM = enemies;
        if (SpecialMeasuredRate(out var sp)) { b.Special = sp; b.SpecialMeasured = true; }
        if (weapon != null)
        {
            b.Weapon = weapon.weaponType;
            b.ComboLength = ComboLengthOf(weapon);
            b.AttackWeight = Math.Max(0.05, weapon.AttackWeightPerSwing);
            b.HitsPerSwingM = weapon.weaponType is EWeaponType.Crossbow or EWeaponType.StaffMagic or EWeaponType.Golem ? 1.0 : Math.Min(2.5, Math.Max(1, 0.6 * b.EnemiesM));
            double seconds = runCombatTime - swingCombatStart;
            bool same = weapon.entityId == swingWeaponId
                        || (weaponOverride != null && swingController != null && swingController.currentWeapon != null
                            && swingController.currentWeapon.entityId == swingWeaponId && swingController.currentWeapon.weaponType == weapon.weaponType);
            if (same && swingCount >= 30 && seconds >= 20)
            {
                b.Swing = Math.Max(0.2, Math.Min(8, swingWeighted / seconds));
                b.SwingMeasured = true;
                var measuredWeapon = swingController != null ? swingController.currentWeapon : null;
                if (weapon.entityId != swingWeaponId && measuredWeapon != null) b.Swing *= MoveSetSpeed(weapon) / MoveSetSpeed(measuredWeapon);
            }
            else b.Swing *= MoveSetSpeed(weapon);
            if (same && seconds >= 30)
            {
                bool rapierNow = swingController != null && swingController.currentWeapon != null && SsRapier(swingController.currentWeapon);
                if (!rapierNow)
                {
                    b.DashAttack = Math.Min(3, dashAttackCount / seconds);
                    b.DashAttackMeasured = true;
                }
                if (!b.SpecialMeasured && specialAnimCount >= 5)
                {
                    b.Special = Math.Max(0.02, Math.Min(3, specialAnimCount / seconds));
                    b.SpecialMeasured = true;
                }
                if (weapon is WeaponSimple_Dagger && swingController != null && swingController.currentWeapon is WeaponSimple_Dagger)
                {
                    b.ParrySwing = Math.Min(3, parryAnimCount / seconds);
                    b.FuryMeasured = Math.Min(3, furyAnimCount / seconds);
                }
            }
            if (SsRapier(weapon))
            {
                b.Rapier = true;
                b.DashAttack = 0;
                b.DashAttackMeasured = false;
            }
        }
        if (DefendMeasured(out var guard, out var perfect, out var parry))
        {
            b.Guard = guard; b.PerfectGuard = perfect; b.Parry = parry; b.DefendMeasured = true;
        }
        if (single && BossHitRate() is var hits && hits >= 0) b.Damaged = hits;
        if (weapon != null)
        {
            var measuredWeapon = swingController != null ? swingController.currentWeapon : null;
            bool had = b.DefendMeasured && HasAddon<WeaponAddon_OppositeAttackGuard>(measuredWeapon);
            double delta = (HasAddon<WeaponAddon_OppositeAttackGuard>(weapon) ? 1 : 0) - (had ? 1 : 0);
            if (delta != 0)
            {
                double g = 0.6 * b.Damaged * delta;
                b.Guard = Math.Max(0, b.Guard + g);
                b.PerfectGuard = Math.Max(0, b.PerfectGuard + 0.5 * g);
            }
        }
        b.Enemies = single ? 1 : b.EnemiesM;
        b.HitsPerSwing = single ? 1 : b.HitsPerSwingM;
        return b;
    }
}
