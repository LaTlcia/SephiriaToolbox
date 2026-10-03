using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed class DgModel
    {
        public double ParryMult, FuryMult;
        public bool ParryFury = true, CanFury = true;
        public bool CritFury, EvasionFury, EvasionNext, EvadeFury;
        public double CritFuryPct = 30, EvasionFuryTime = 2.8, DoubleFury;
        public int kTrance = -1, kEvasion = -1;
        public double PassFury;
        public double ParrySwing, Parry, FuryBase = -1;
        public double TranceBuild;
        public bool FuryMp;
        public double FuryMpPer = 8, FuryMpMin = 5, FuryMpBonus = 5, FuryMpPct = 100;
        public bool ParryUsesMp = true, FuryUsesMp;
        public bool DupMagic;
        public int kFsFury = -1;
        public bool BladeZone;
        public double ZoneMult, ZoneAsPer = 30, ZoneStep = 7, DashMultNormal = 1;
    }

    static WeaponMoves DaggerMoves(WeaponSimple_Dagger dg)
    {
        var mv = new WeaponMoves();
        var basic = dg.basicComboAttacks ?? new NewWeaponFireData[0];
        int len = basic.Length, fi = dg.finalComboIdx;
        int n = fi >= 0 && fi < len ? fi + 1 : len;
        var combo = Enumerable.Range(0, n).Select(i => At(basic, i)).ToArray();
        mv.BasicFd = combo.FirstOrDefault(f => f != null);
        mv.BasicMult = mv.BasicFd != null ? AvgMult(combo) : 1;
        double total = combo.Where(f => f != null).Sum(HitMult);
        if (total > 0 && fi >= 0 && fi < n && combo[fi] != null) mv.FinalShare = HitMult(combo[fi]) / total;
        mv.DashFd = At(dg.dashAttacks, 0);
        mv.DashMult = HitMult(mv.DashFd);
        mv.NoDash = mv.DashFd == null || dg.isDashAttackAvailable < 1;
        mv.SpecialFd = At(dg.specialAttacks, 0) ?? At(dg.specialAttacks, 1);
        mv.SpecialMult = 1;
        mv.NoSpecial = mv.SpecialFd == null;
        mv.SpecialNote = Tr("weapon.parry_counter_rage_attack");
        return mv;
    }

    void BuildDaggerModel(PlayerAvatar avatar, DpsModel d, Func<string, int> K, WeaponSimple_Dagger dg, Behavior play)
    {
        var w = d.Weapon;
        var g = w.Dg = new DgModel();
        var sp = dg.specialAttacks ?? new NewWeaponFireData[0];
        bool throwDagger = dg.throwDagger || HasAddon<WeaponAddonCommon_SpecialAttackDebuff>(dg) && At(sp, 1) is NewWeaponFireData_BulletBurst;
        bool wide = dg.wideParry || HasAddon<WeaponAddonDagger_WideParry>(dg), easy = dg.easyParry || HasAddon<WeaponAddonDagger_EasyParry>(dg);
        bool heal = dg.healParry || HasAddon<WeaponAddonDagger_ParryHeal>(dg);
        int parryIdx = throwDagger ? 1 : easy ? 2 : heal ? 3 : wide ? 2 : 1;
        g.ParryMult = HitMult(At(sp, parryIdx) ?? At(sp, 1));
        bool lightning = dg.lightningFury;
        var over = dg.addons?.OfType<WeaponAddonCommon_ChangeWeaponAction>()
                     .FirstOrDefault(x => x != null && string.Equals(x.changeWeaponActionName, "FURY", StringComparison.OrdinalIgnoreCase));
        var furyFd = over != null && over.fireData != null ? over.fireData : At(sp, lightning ? 4 : 0) ?? At(sp, 0);
        g.FuryMult = HitMult(furyFd);
        if (furyFd is NewWeaponFireData_SpecialProjectile dsp && dsp.projectilePrefab != null && dsp.projectilePrefab.GetComponent<SpecialProjectile_DuplicateMagic>() != null)
        {
            g.DupMagic = true;
            g.FuryMult = 0;
        }
        g.kFsFury = K("FLAMESWORDFURY");
        w.SpecialMp = false;
        g.ParryUsesMp = !(dg.freeParry || HasAddon<WeaponAddonDagger_FreeParry>(dg));
        g.FuryUsesMp = over != null && (over.newActionParameter ?? "").IndexOf("MP=", StringComparison.Ordinal) >= 0;
        if (furyFd is NewWeaponFireData_SpecialProjectile fsp && fsp.projectilePrefab != null && fsp.projectilePrefab.GetComponent<SpecialProjectile_FuryMP>() != null)
        {
            g.FuryMp = true;
            g.FuryMpPer = Math.Max(1, ConstOr("furyMPProjectilePerMP", 8));
            g.FuryMpMin = Math.Max(1, ConstOr("furyMPMinimumProjectileCount", 5));
            g.FuryMpBonus = ConstOr("furyMPProjectileDamageBonusBy50MP", 5);
            g.FuryMpPct = ConstOr("furyMPProjectileDamagePercent", 100);
        }
        bool final = dg.basicAttackFinal || HasAddon<WeaponAddonDagger_BasicAttackFinal>(dg);
        g.CritFury = dg.critFury || HasAddon<WeaponAddonDagger_CritFury>(dg);
        g.CritFuryPct = ConstOr("critFuryPercent", 30);
        g.EvasionFury = StatWithWeapon(avatar, "EVASIONFURY", dg) > 0;
        g.EvasionNext = StatWithWeapon(avatar, "EVASIONFURY_NEXT", dg) > 0;
        g.EvadeFury = dg.evadeFury || HasAddon<WeaponAddonDagger_EvadeFury>(dg);
        g.DoubleFury = dg.doubleFury || HasAddon<WeaponAddonDagger_DoubleFury>(dg) ? 1 : 0;
        g.CanFury = !final;
        g.ParryFury = !final && !g.CritFury && !g.EvasionFury;
        bool canParry = !g.CritFury && !g.EvasionFury;
        g.kTrance = K("DAGGERTRANCEBONUS");
        g.kEvasion = K("EVASION");
        g.TranceBuild = avatar.GetCustomStatUnsafe("DAGGERTRANCEBONUS");
        g.PassFury = dg.passFuryDamageBonus;
        if (dg == weaponOverride)
            foreach (var pf in dg.addons?.OfType<WeaponAddonDagger_PassFury>() ?? Enumerable.Empty<WeaponAddonDagger_PassFury>()) g.PassFury += pf.specialAttackDamage;
        g.Parry = canParry ? play.Parry : 0;
        g.ParrySwing = !canParry ? 0 : play.ParrySwing >= 0 ? play.ParrySwing : Math.Max(g.Parry, PlayParry * 1.5);
        g.FuryBase = g.CanFury && play.FuryMeasured >= 0 ? play.FuryMeasured : -1;
        if ((dg.enhancedDashAttack || HasAddon<WeaponAddonDagger_EnhancedDashAttack>(dg)) && dg.dashAttackFireData_BladeZone is NewWeaponFireData_SpecialProjectile bz
            && bz.projectilePrefab != null && bz.projectilePrefab.GetComponent<SpecialProjectile_BladeZone>() is { } zone)
        {
            g.BladeZone = true;
            g.ZoneMult = bz.damageMultiplier;
            g.ZoneAsPer = Math.Max(1, zone.addAttackCountPerAttackSpeed);
            g.ZoneStep = zone.addDamagePercentPerAttack;
            g.DashMultNormal = Math.Max(0.01, HitMult(At(dg.dashAttacks, 0)));
        }
    }

    void DaggerDashBuff(PlayerAvatar avatar, ArrModel m, DpsModel d, Func<string, int> K, WeaponSimple weapon, Behavior play, Dictionary<int, double> curRaw)
    {
        var live = avatar.GetComponent<WeaponControllerSimple>()?.currentWeapon as WeaponSimple_Dagger;
        if (live != null && live.basicAttackFinalBuffPrefab != null && BuffsField?.GetValue(avatar) is Dictionary<string, CharacterBuff> buffs
            && !string.IsNullOrEmpty(live.basicAttackFinalBuffPrefab.ID) && buffs.TryGetValue(live.basicAttackFinalBuffPrefab.ID, out var active) && active != null)
            foreach (var (key, mode, v) in BuffStats(live.basicAttackFinalBuffPrefab))
                if (mode == 0) curRaw[K(key)] = curRaw.GetValueOrDefault(K(key)) + v * active.Amplified * active.CurrentStack;
        if (weapon is not WeaponSimple_Dagger dg || dg.basicAttackFinalBuffPrefab == null) return;
        if (!(dg.basicAttackFinal || HasAddon<WeaponAddonDagger_BasicAttackFinal>(dg))) return;
        var prefab = dg.basicAttackFinalBuffPrefab;
        double dur = prefab.defaultDuration * (prefab.ignoreDurationBonus ? 1 : Pct(avatar.GetCustomStat(ECustomStat.BuffDuration)));
        double up = BuffAvgStacks(play.DashAttack, dur, prefab.MaxStackCount);
        foreach (var (key, mode, v) in BuffStats(prefab))
            if (mode == 0) d.ExtraBase[K(key)] = d.ExtraBase.GetValueOrDefault(K(key)) + v * up;
        m.Notes.Add(Tr("weapon.post_dash_attack_buff", play.DashAttack, up));
    }

    sealed partial class Evaluator
    {
        double DgFuryRate()
        {
            var g = d.Weapon.Dg;
            if (g == null || !g.CanFury) return 0;
            double n = 1 + g.DoubleFury + Math.Max(0, T(g.kTrance));
            if (g.FuryBase >= 0)
            {
                double n0 = 1 + g.DoubleFury + Math.Max(0, g.TranceBuild);
                return Math.Min(2, g.FuryBase * n / Math.Max(1, n0));
            }
            double rate = 0;
            if (g.ParryFury) rate += g.Parry * n;
            if (g.CritFury) rate += Math.Min(WeaponHits(), 20) * g.CritFuryPct / 100.0 * n;
            if (g.EvadeFury) rate += EvadeRate() * n;
            if (g.EvasionFury)
            {
                double ev = Math.Max(0, T(g.kEvasion)) * 0.0001;
                ev = g.EvasionNext ? ev * 1.25 : Math.Min(ev, 0.33);
                rate += n / (g.EvasionFuryTime / (1 + ev) + 0.5 * n);
            }
            return Math.Min(2, rate);
        }

        double DgSpecialFactor()
        {
            var g = d.Weapon.Dg;
            if (g == null) return 1;
            var w = d.Weapon;
            double add = g.PassFury;
            if (w.kBloodyFury >= 0 && T(w.kBloodyFury) > 0) add += w.BloodyFuryPct;
            if (w.kCloudParry >= 0 && T(w.kCloudParry) > 0 && d.Eco != null && d.Sources.Any(x => x.Eco == EcoKind.DarkCloud))
            {
                var e = d.Eco;
                double max = Math.Max(0, T(e.kCloudMin)) * (1 + Math.Max(0, T(e.kCloudScale))) + e.CloudDefault;
                add += Math.Min(70, max * 0.5 * 2);
            }
            double fury = g.FuryMult;
            if (g.FuryMp)
            {
                double mp = Math.Max(0, MaxMp()) * 0.5;
                double count = Math.Max(g.FuryMpMin, Math.Floor(mp / g.FuryMpPer));
                fury = g.FuryMult * g.FuryMpPct / 100.0 * Math.Max(1, count / 6.0) * (1 + Math.Floor(mp / 50) * g.FuryMpBonus / 100.0);
            }
            double mpSkill = Pct(T(d.kMpSkill));
            return g.ParrySwing * g.ParryMult * (g.ParryUsesMp ? mpSkill : 1) + DgFuryRate() * fury * Pct(add) * (g.FuryUsesMp ? mpSkill : 1);
        }

        double DgDashFactor()
        {
            var g = d.Weapon.Dg;
            if (g == null || !g.BladeZone) return 1;
            double n = 1 + Math.Floor(Math.Max(0, T(d.kAs)) / g.ZoneAsPer);
            return g.ZoneMult * (n + g.ZoneStep / 100.0 * n * (n - 1) / 2) / g.DashMultNormal;
        }
    }
}
