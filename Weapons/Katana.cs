using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static WeaponMoves KatanaMoves(WeaponSimple_Katana kt)
    {
        var mv = new WeaponMoves();
        var basic = kt.basicComboAttacks ?? new NewWeaponFireData[0];
        int len = basic.Length, fi = kt.finalComboIdx;
        int n = fi >= 0 && fi < len ? fi + 1 : len;
        var combo = Enumerable.Range(0, n).Select(i => At(basic, i)).ToArray();
        mv.BasicFd = combo.FirstOrDefault(f => f != null);
        mv.BasicMult = mv.BasicFd != null ? AvgMult(combo) : 1;
        double total = combo.Where(f => f != null).Sum(HitMult);
        if (total > 0 && fi >= 0 && fi < n && combo[fi] != null) mv.FinalShare = HitMult(combo[fi]) / total;
        mv.DashFd = At(kt.dashAttacks, 0);
        mv.DashMult = HitMult(mv.DashFd);
        mv.NoDash = mv.DashFd == null || kt.isDashAttackAvailable < 1;
        var sp = kt.specialAttacks ?? new NewWeaponFireData[0];
        switch (KatanaSheathKind(kt))
        {
            case 0:
                mv.SpecialFd = At(sp, 0);
                mv.SpecialMult = AvgMult(new[] { At(sp, 0), At(sp, 1) });
                mv.SpecialNote = Tr("weapon.sheath_slash");
                if (KatanaAlwaysSheathed(kt))
                {
                    mv.BasicMult = 0;
                    mv.FinalShare = 0;
                    mv.SpecialNote = Tr("weapon.sheath_slash_always_sheathed");
                }
                break;
            case 1 when KatanaDeflectIce(kt):
                mv.SpecialFd = kt.deflectingIceData;
                mv.SpecialMult = HitMult(kt.deflectingIceData);
                mv.SpecialNote = Tr("weapon.block_ice_spike");
                break;
            case 3:
                mv.SpecialFd = At(sp, 3) ?? At(sp, 0);
                mv.SpecialMult = HitMult(mv.SpecialFd);
                mv.SpecialNote = Tr("weapon.cloud_slash");
                break;
            default:
                mv.NoSpecial = true;
                mv.SpecialNote = KatanaSheathKind(kt) == 4 ? Tr("weapon.quick_draw_counts_as") : "";
                break;
        }
        if (!mv.NoSpecial && mv.SpecialFd == null) mv.NoSpecial = true;
        return mv;
    }

    static bool KatanaAlwaysSheathed(WeaponSimple_Katana kt) => HasAddon<WeaponAddonKatana_DefaultSheath>(kt);

    static bool KatanaDeflectIce(WeaponSimple_Katana kt) => kt.deflectingIceData != null && WeaponAddonStats(kt).GetValueOrDefault("DEFLECTINGICE") > 0;

    static int KatanaSheathKind(WeaponSimple_Katana kt)
    {
        if (HasAddon<WeaponAddonKatana_CloudSlash>(kt) || kt.sheathActionType == WeaponSimple_Katana.ESheathActionType.CloudSlash) return 3;
        if (HasAddon<WeaponAddonKatana_FlameSword_Eclipse>(kt) || kt.sheathActionType == WeaponSimple_Katana.ESheathActionType.Eclipse) return 2;
        if (HasAddon<WeaponAddonKatana_Deflecting>(kt) || kt.sheathActionType == WeaponSimple_Katana.ESheathActionType.Deflecting) return 1;
        if (kt.useQuickDraw || HasAddon<WeaponAddonKatana_QuickDraw>(kt)) return 4;
        return 0;
    }

    void BuildKatanaSweep(PlayerAvatar avatar, DpsModel d, Func<string, int> K, WeaponSimple_Katana kt, Behavior play, ArrModel m)
    {
        int kind = KatanaSheathKind(kt);
        if (play.SpecialMeasured && weaponOverride != null && swingController != null
            && swingController.currentWeapon is WeaponSimple_Katana cur && KatanaSheathKind(cur) != kind)
        {
            play.Special = PlaySpecial;
            play.SpecialMeasured = false;
        }
        bool ice = kind == 1 && KatanaDeflectIce(kt);
        if ((kind is 1 or 2 or 4) && !ice)
        {
            play.Special = 0;
            play.SpecialMeasured = false;
            return;
        }
        if (kind == 3) return;
        bool always = KatanaAlwaysSheathed(kt);
        double mul = always ? 1.8 : 1;
        double interval = ice ? 1 : Math.Max(0.1, kt.sheathAttackTime);
        double asNow = Math.Max(0.1, 1 + mul * avatar.GetCustomStat(ECustomStat.AttackSpeed) / 100.0);
        bool speedSheath = !ice && StatWithWeapon(avatar, "SPEEDSHEATH", kt) > 0;
        if (speedSheath) interval /= asNow;
        double cap = 1 / interval;
        double poolPeriod = play.Single ? d.FightLen / Math.Max(1, BossRefillsPerFight()) : BattleLength();
        double cost0 = always ? 0 : kt.specialAttackCost;
        double cost = Math.Floor(Math.Max(0, cost0 * (1 - avatar.GetCustomStatUnsafe("SPECIALATTACKCOSTREDUCTION") / 100.0)));
        double rate = cost <= 0 || avatar.GetCustomStatUnsafe("INFINITYMP") > 0 ? cap
            : SweepRate(cost, avatar.GetCustomStatUnsafe("MPREGEN"), avatar.GetCustomStatUnsafe("MPRESONANCE"), avatar.GetCustomStat(ECustomStat.MPSteal),
                        SweepDps(avatar), avatar.MaxMp - avatar.reservedMp, poolPeriod, cap: cap);
        if (!play.SpecialMeasured) play.Special = rate;
        if (always && !play.SwingMeasured) play.Swing = 0.05;
        play.SweepCost = cost;
        play.SpecialByMp = cost > 0;
        if (play.Special > 0 && (cost > 0 || speedSheath))
            d.Sweep = new SweepModel
            {
                Mode = 0, Base = play.Special, Measured = play.SpecialMeasured, Cost0 = cost0, Cap = cap, CapAs = speedSheath ? asNow : 0, CapAsMul = mul,
                PoolPeriod = poolPeriod, kCostRed = -1, kSpecCostRed = K("SPECIALATTACKCOSTREDUCTION"), kRegen = K("MPREGEN"),
                kResonance = K("MPRESONANCE"), kSteal = K("MPSTEAL"), Dps = SweepDps(avatar), Reserved = avatar.reservedMp
            };
        string how = play.SpecialMeasured ? Tr("weapon.measured") : cost > 0 ? Tr("weapon.mp_budget") : Tr("weapon.cap");
        m.Notes.Add(ice ? Tr("weapon.silence_everfrost_each_block", play.Special, cost, how)
                  : always ? Tr("weapon.muramasa_attack_button_sheath", play.Special, interval, how)
                  : Tr("weapon.katana_sheath_slash_about", play.Special, cost, interval, how));
    }
}
