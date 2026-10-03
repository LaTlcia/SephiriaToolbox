using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed class WeaponMoves
    {
        public NewWeaponFireData BasicFd, SpecialFd, DashFd;
        public double BasicMult = 1, SpecialMult = 1, DashMult = 1;
        public double FinalShare;
        public string Formula;
        public bool BasicIsDash;
        public bool NoSpecial, NoDash;
        public NewWeaponFireData Strike, StrikeExplosion;
        public double AutoDash;
        public string SpecialNote = "";
    }

    static WeaponMoves MovesOf(WeaponSimple w, PlayerAvatar a, Behavior play)
    {
        if (w is WeaponSimple_SwordAndShield ss) return SwordShieldMoves(ss, a, play);
        if (w is WeaponSimple_GreatSword gs) return GreatSwordMoves(gs, a);
        if (w is WeaponSimple_Dagger dg) return DaggerMoves(dg);
        if (w is WeaponSimple_Crossbow xb) return CrossbowMoves(xb);
        if (w is WeaponSimple_Katana kt) return KatanaMoves(kt);
        if (w is WeaponSimple_QuartterStaff qs) return QuarterStaffMoves(qs, a, play);
        return GenericMoves(w);
    }

    static bool RangedWeapon(WeaponSimple w)
    {
        if (w == null) return false;
        if (w.weaponType is EWeaponType.Crossbow or EWeaponType.StaffMagic or EWeaponType.Golem) return true;
        if (w is WeaponSimple_QuartterStaff { isNormalAttackRolling: true } qs)
            return qs.rollingFireData != null && qs.rollingFireData is not NewWeaponFireData_MeleeAttack;
        var first = w.basicComboAttacks?.FirstOrDefault(f => f != null);
        return first != null && first is not NewWeaponFireData_MeleeAttack;
    }

    static double HitMult(NewWeaponFireData f) => f == null ? 0 : f.damageMultiplier * FireHits(f);

    static double AvgMult(IEnumerable<NewWeaponFireData> fds)
    {
        var list = fds.Where(f => f != null).ToList();
        return list.Count > 0 ? list.Average(HitMult) : 0;
    }

    static double FireHits(NewWeaponFireData f)
    {
        switch (f)
        {
            case NewWeaponFireData_MeleeAttack ma when ma.projectilePrefab != null && ma.projectilePrefab.GetComponent<MeleeCollision>() is { } mc:
            {
                int n = Math.Max(1, mc.multiHit);
                if (n > 1 && mc.multiHitIntervalTimer != null && mc.multiHitIntervalTimer.time > 0 && mc.durationTimer != null)
                    n = Math.Min(n, 1 + (int)Math.Floor(mc.durationTimer.time / mc.multiHitIntervalTimer.time + 1e-6));
                return n;
            }
            case NewWeaponFireData_BulletBurst bb:
            {
                int r = Math.Max(1, bb.burstRound);
                if (bb.arcShot && bb.arcShotIntervalAngle > 0) return Math.Min(r, 1 + 2 * Math.Floor(20 / bb.arcShotIntervalAngle + 1e-6));
                return r;
            }
        }
        return 1;
    }

    static WeaponMoves GenericMoves(WeaponSimple w)
    {
        var mv = new WeaponMoves();
        var basic = w.basicComboAttacks ?? new NewWeaponFireData[0];
        mv.BasicFd = basic.FirstOrDefault(f => f != null);
        mv.BasicMult = basic.Any(f => f != null) ? AvgMult(basic) : 1;
        double total = basic.Where(f => f != null).Sum(HitMult);
        int fi = w.finalComboIdx;
        if (total > 0 && fi >= 0 && fi < basic.Length && basic[fi] != null) mv.FinalShare = HitMult(basic[fi]) / total;
        mv.SpecialFd = w.specialAttacks?.FirstOrDefault(f => f != null);
        mv.SpecialMult = mv.SpecialFd != null ? AvgMult(w.specialAttacks) : 0;
        mv.NoSpecial = mv.SpecialFd == null;
        mv.DashFd = w.dashAttacks?.FirstOrDefault(f => f != null);
        mv.DashMult = mv.DashFd != null ? AvgMult(w.dashAttacks) : 0;
        mv.NoDash = mv.DashFd == null;
        return mv;
    }

    double WeaponAsAmp(WeaponSimple w)
    {
        if (w == null) return 0;
        double amp = w.attackSpeedAmplify;
        if (w == weaponOverride && w.addons != null)
            foreach (var ad in w.addons.OfType<WeaponAddonCommon_EnhancedAttackSpeed>())
                if (ad != null) amp += ad.attackSpeedAmplify;
        return amp;
    }

    static int ComboLengthOf(WeaponSimple w)
    {
        if (w == null || w.basicComboAttacks == null) return 0;
        if (w is WeaponSimple_SwordAndShield ss)
        {
            if (SsRapier(ss)) return 0;
            int fi = ss.finalComboIdx;
            if (fi >= 0 && fi < ss.basicComboAttacks.Length) return fi + 1;
        }
        return w.basicComboAttacks.Count(f => f != null);
    }
}
