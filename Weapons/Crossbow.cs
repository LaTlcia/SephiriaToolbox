using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed class XbModel
    {
        public byte Type;
        public double Cost = 10;
        public int kLightning = -1;
        public double LightMult, NormalMult = 1;
        public double IceUp, IceMult;
        public bool IceRelic;
        public int kRelicAmp = -1, kRelicRetrig = -1, kRelicHaste = -1;
        public double RetrigPer = 100;
        public bool Continue;
        public double ContinueStep = 10, ContinueMax = 5;
        public double FastShare, FastSpeed = 5;
        public double CompShare, CompBonus = 25, CompMult, EnhMult, EnhThreshold;
        public bool Drone;
        public double DronePerMp = 0.9;
        public bool C4;
        public double C4Max = 5, C4Flat = 10, C4PhysPct = 120, C4PerDef = 2;
        public double FinalCdr, FinalEvery = 2;
    }

    static WeaponMoves CrossbowMoves(WeaponSimple_Crossbow xb)
    {
        var mv = GenericMoves(xb);
        var basic = xb.basicComboAttacks ?? new NewWeaponFireData[0];
        mv.BasicMult = basic.Any(f => f != null) ? AvgMult(basic) : 1;
        switch (xb.specialAttackType)
        {
            case WeaponSimple_Crossbow.ESpecialAttackType.FireBullet:
                mv.SpecialFd = At(xb.specialAttacks, 0);
                mv.SpecialMult = HitMult(mv.SpecialFd);
                mv.NoSpecial = mv.SpecialFd == null;
                mv.SpecialNote = xb.useMiniDrone ? Tr("weapon.drone") : Tr("weapon.special_bolt");
                break;
            case WeaponSimple_Crossbow.ESpecialAttackType.Minigun:
                mv.SpecialFd = At(basic, 0);
                mv.SpecialMult = HitMult(mv.SpecialFd);
                mv.NoSpecial = mv.SpecialFd == null;
                mv.SpecialNote = Tr("weapon.minigun");
                break;
            default:
                mv.NoSpecial = true;
                break;
        }
        return mv;
    }

    void BuildCrossbowModel(PlayerAvatar avatar, DpsModel d, Func<string, int> K, WeaponSimple_Crossbow xb, Behavior play, ArrModel m)
    {
        var w = d.Weapon;
        var x = w.Xb = new XbModel { Type = (byte)xb.specialAttackType };
        var basic = xb.basicComboAttacks ?? new NewWeaponFireData[0];
        x.NormalMult = Math.Max(0.01, basic.Any(f => f != null) ? AvgMult(basic) : 1);
        if (xb.lightningArrow != null) { x.kLightning = K("LIGHTNINGCROSSBOW"); x.LightMult = HitMult(xb.lightningArrow); }
        x.Drone = xb.useMiniDrone;
        x.DronePerMp = xb.miniDroneAddDamagePercentPerMP;
        x.Continue = xb.continueBonus && xb.specialAttackType == WeaponSimple_Crossbow.ESpecialAttackType.FireBullet;
        if (x.Continue) m.Notes.Add(Tr("weapon.continue_bonus", x.ContinueStep, x.ContinueStep * x.ContinueMax));
        if (HasAddon<WeaponAddonCommon_C4Bomb>(xb))
        {
            x.C4 = true;
            x.C4Max = Math.Max(1, ConstOr("crossbowC4BombMaxCountPerTarget", 5));
            x.C4Flat = ConstOr("crossbowC4BombExplosionDefaultDamage", 10);
            x.C4PhysPct = ConstOr("crossbowC4BombExplosionPhysicalPercent", 120);
            x.C4PerDef = Math.Max(1, ConstOr("crossbowC4BombExplosionPerDefense", 2));
            m.Notes.Add(Tr("weapon.barnacle_hits_attach_c4", x.C4Max, x.C4Flat, x.C4PhysPct, x.C4PerDef));
        }
        var fc = xb.addons?.OfType<WeaponAddonCrossbow_FinalComboDecreaseCooldown>().FirstOrDefault(a => a != null);
        if (fc != null && fc.lastAttackDecreaseCooldownRatio > 0)
        {
            x.FinalCdr = Math.Min(0.95, fc.lastAttackDecreaseCooldownRatio);
            x.FinalEvery = Math.Max(1, xb.finalComboIdx + 1);
            m.Notes.Add(Tr("weapon.xra_9_every_shots", x.FinalEvery, x.FinalCdr));
        }
        x.Cost = Math.Max(0, (x.Drone ? 3 : xb.specialAttackCost) * (1 - avatar.GetCustomStatUnsafe("SPECIALATTACKCOSTREDUCTION") / 100.0));
        double poolPeriod = play.Single ? d.FightLen / Math.Max(1, BossRefillsPerFight()) : BattleLength();
        double Casts(double cost, double cap) => cost <= 0 || avatar.GetCustomStatUnsafe("INFINITYMP") > 0 ? cap
            : SweepRate(cost, avatar.GetCustomStatUnsafe("MPREGEN") * MpGain(avatar), avatar.GetCustomStatUnsafe("MPRESONANCE"), avatar.GetCustomStat(ECustomStat.MPSteal) * MpGain(avatar),
                        SweepDps(avatar), avatar.MaxMp - avatar.reservedMp, poolPeriod, cap: cap);
        double reloadSpeed = avatar.GetCustomStatUnsafe("CROSSBOWRELOADSPEED") / 100.0;
        double reload = xb.reloadTime / Math.Max(0.1, 1 + reloadSpeed);
        double ammo = Math.Max(1, avatar.GetCustomStatUnsafe("FIXEDAMMO") > 0 ? avatar.GetCustomStatUnsafe("FIXEDAMMO")
                                  : xb.defaultMagazineCapacity + avatar.GetCustomStatUnsafe("CROSSBOWAMMO"));
        double interval = xb.fireIntervalTimer != null && xb.fireIntervalTimer.time > 0 ? xb.fireIntervalTimer.time : 0.3;
        double spd = Math.Max(0.1, 1 + avatar.GetCustomStat(ECustomStat.AttackSpeed) / 100.0);
        double cycle = ammo * interval / spd + reload, cycles = 1 / Math.Max(0.1, cycle);
        double magazines = avatar.GetCustomStatUnsafe("FIXEDAMMO") > 0 ? 1 : Math.Max(1, xb.defaultMagazineCount + avatar.GetCustomStatUnsafe("CROSSBOWADDITIONALMAGAZINE"));
        buildReloadCycle = magazines * ammo * interval / spd + reload;
        switch (xb.specialAttackType)
        {
            case WeaponSimple_Crossbow.ESpecialAttackType.FireBullet:
            case WeaponSimple_Crossbow.ESpecialAttackType.Minigun:
            {
                bool minigun = xb.specialAttackType == WeaponSimple_Crossbow.ESpecialAttackType.Minigun;
                double cap = minigun ? 1 / Math.Max(0.02, xb.minigunFireIntervalTimer != null ? xb.minigunFireIntervalTimer.time : 0.08) : 1.5;
                double cost0 = minigun ? xb.specialAttackCost : x.Drone ? 3 : xb.specialAttackCost;
                double rate = Casts(x.Cost, cap);
                if (!play.SpecialMeasured) play.Special = rate;
                play.SweepCost = x.Cost;
                play.SpecialByMp = x.Cost > 0;
                if (x.Cost > 0 && play.Special > 0)
                    d.Sweep = new SweepModel
                    {
                        Mode = 0, Base = play.Special, Measured = play.SpecialMeasured, Cost0 = cost0, Cap = cap,
                        PoolPeriod = poolPeriod, kCostRed = -1, kSpecCostRed = K("SPECIALATTACKCOSTREDUCTION"), kRegen = K("MPREGEN"),
                        kResonance = K("MPRESONANCE"), kSteal = K("MPSTEAL"), Dps = SweepDps(avatar), Reserved = avatar.reservedMp
                    };
                m.Notes.Add(Tr("weapon.crossbow_about_s_mp", (minigun ? Tr("weapon.minigun") : x.Drone ? Tr("weapon.drone") : Tr("weapon.special_bolt")), play.Special, x.Cost, (play.SpecialMeasured ? Tr("weapon.measured") : Tr("weapon.mp_budget"))));
                break;
            }
            case WeaponSimple_Crossbow.ESpecialAttackType.FastReload:
            {
                double casts = Casts(x.Cost, cycles);
                x.FastShare = Math.Min(1, casts / Math.Max(1e-6, cycles));
                x.FastSpeed = Math.Max(1, xb.fastReloadSpeed);
                play.Special = 0;
                m.Notes.Add(Tr("weapon.fast_reload_about_reloads", x.FastShare, x.FastSpeed));
                break;
            }
            case WeaponSimple_Crossbow.ESpecialAttackType.IceBuff:
            {
                var buff = xb.iceBuffPrefab;
                double dur = buff != null ? buff.defaultDuration * (buff.ignoreDurationBonus ? 1 : Pct(avatar.GetCustomStat(ECustomStat.BuffDuration))) : 10;
                double cd = xb.iceBuffCoolDownTimer != null && xb.iceBuffCoolDownTimer.time > 0 ? xb.iceBuffCoolDownTimer.time : 2;
                double casts = Casts(x.Cost, 1 / cd);
                x.IceUp = Math.Min(1, casts * dur);
                x.IceMult = HitMult(xb.iceArrow);
                x.IceRelic = buff is CharacterBuff_StatusUnsafe su && su.add != null && su.add.Any(a => a != null && a.id == "ICECROSSBOWFROSTRELIC");
                if (x.IceRelic)
                {
                    x.kRelicAmp = K("CHARGINGCHARMAMPLIFY"); x.kRelicRetrig = K("CHARGINGCHARMRETRIGGERBYATTACKSPEED"); x.kRelicHaste = K("CHARGINGCHARMBONUS");
                    x.RetrigPer = Math.Max(1, ConstOr("chargingCharmRetriggerByAttackSpeed", 100));
                }
                play.Special = 0;
                if (buff != null)
                    foreach (var (key, mode, v) in BuffStats(buff))
                        if (mode == 0 && key != "ICECROSSBOWBUFF" && key != "ICECROSSBOWFROSTRELIC")
                            d.ExtraBase[K(key)] = d.ExtraBase.GetValueOrDefault(K(key)) + v * x.IceUp;
                m.Notes.Add(Tr("weapon.frost_veil_ice_bolts", x.IceUp));
                break;
            }
            case WeaponSimple_Crossbow.ESpecialAttackType.AmmoCompression:
            {
                double casts = Casts(x.Cost, cycles);
                x.CompShare = Math.Min(1, casts / Math.Max(1e-6, cycles));
                x.CompBonus = ConstOr("ammoCompressionDamageBonus", 25);
                x.CompMult = xb.compressedAmmoAttacks != null && xb.compressedAmmoAttacks.Any(f => f != null) ? AvgMult(xb.compressedAmmoAttacks) : x.NormalMult;
                x.EnhMult = xb.enhancedCompressedAmmoAttacks != null && xb.enhancedCompressedAmmoAttacks.Any(f => f != null) ? AvgMult(xb.enhancedCompressedAmmoAttacks) : 0;
                x.EnhThreshold = xb.enhancedCompressedAmmoThreshold;
                play.Special = 0;
                m.Notes.Add(Tr("weapon.ammo_compression_about_magazines", x.CompShare));
                break;
            }
        }
        if (!play.SwingMeasured)
        {
            double mags = avatar.GetCustomStatUnsafe("FIXEDAMMO") > 0 ? 1 : Math.Max(1, xb.defaultMagazineCount + avatar.GetCustomStatUnsafe("CROSSBOWADDITIONALMAGAZINE"));
            double r = (1 - x.FastShare) * reload + x.FastShare * xb.reloadTime / Math.Max(0.1, x.FastSpeed + reloadSpeed);
            if (avatar.GetCustomStatUnsafe("DASHRELOAD") > 0) r = Math.Min(r, Math.Max(w.XbDashTime, w.XbDashEvery - ammo * interval / spd));
            double t = interval / spd, rm = r / mags, s = x.CompShare;
            double shots = ((1 - s) * ammo + s) / Math.Max(1e-6, (1 - s) * (ammo * t + rm) + s * (t + rm));
            play.Swing = Math.Max(0.2, shots / Math.Max(0.1, AttackSpeedFactor(avatar.GetCustomStat(ECustomStat.AttackSpeed), WeaponAsAmp(xb))));
        }
    }

    void CrossbowLiveState(PlayerAvatar avatar, Func<string, int> K, Dictionary<int, double> curRaw)
    {
        if (avatar.GetComponent<WeaponControllerSimple>()?.currentWeapon is not WeaponSimple_Crossbow live || live.iceBuffPrefab == null) return;
        if (BuffsField?.GetValue(avatar) is not Dictionary<string, CharacterBuff> buffs || string.IsNullOrEmpty(live.iceBuffPrefab.ID)
            || !buffs.TryGetValue(live.iceBuffPrefab.ID, out var active) || active == null) return;
        foreach (var (key, mode, v) in BuffStats(live.iceBuffPrefab))
            if (mode == 0) curRaw[K(key)] = curRaw.GetValueOrDefault(K(key)) + v * active.Amplified * active.CurrentStack;
    }

    sealed partial class Evaluator
    {
        double XbShotFactor(DpsSource s)
        {
            var x = d.Weapon.Xb;
            if (x == null) return 1;
            double en = Math.Max(1e-6, ElemFor(s.Formula, s.Element, elem));
            double f = 1;
            if (x.IceUp > 0 && x.IceMult > 0)
            {
                double ice = elem[2] * x.IceMult / (en * x.NormalMult) * (x.IceRelic ? Pct(T(d.Weapon.kFrostRelicDmg)) * XbRelicShots() : 1);
                f = (1 - x.IceUp) + x.IceUp * ice;
            }
            if (x.kLightning >= 0 && x.LightMult > 0)
            {
                double p = Math.Min(100, Math.Max(0, T(x.kLightning))) / 100.0 * (1 - x.IceUp);
                f += p * (elem[3] * x.LightMult / (en * x.NormalMult) - 1);
            }
            if (x.CompShare > 0 && s.Kind == SrcKind.WeaponBasic) f *= XbCompressionFactor();
            return f;
        }

        double XbCompressionFactor()
        {
            var x = d.Weapon.Xb;
            var w = d.Weapon;
            double n = Math.Max(1, w.XbCap + T(w.kXbAmmo));
            double mc = x.EnhMult > 0 && n - 1 >= x.EnhThreshold ? x.EnhMult : x.CompMult;
            double comp = mc * (1 + x.CompBonus / 100.0 * (n - 1)) / Math.Max(1e-9, x.NormalMult);
            double s = x.CompShare;
            return ((1 - s) * n + s * comp) / Math.Max(1e-9, (1 - s) * n + s);
        }

        double XbRelicShots()
        {
            var x = d.Weapon.Xb;
            double extra = Math.Max(0, T(x.kRelicAmp)), re = T(x.kRelicRetrig), a = T(d.kAs);
            if (re > 0 && a > 0) extra += Math.Min(100, a / x.RetrigPer * re) / 100.0;
            return 1 + extra;
        }

        double XbRelicFires()
        {
            var x = d.Weapon.Xb;
            return x != null && x.IceRelic ? d.Hits.Swing * SwingAs() * x.IceUp : 0;
        }

        double XbSpecialFactor()
        {
            var x = d.Weapon.Xb;
            if (x == null) return 1;
            if (x.C4) return XbC4Factor();
            double add = 0;
            if (x.Drone) add += Math.Max(0, MaxMp() - 50) * x.DronePerMp;
            if (x.Continue) add += XbContinueBonus();
            return Pct(add);
        }

        double XbContinueBonus()
        {
            var x = d.Weapon.Xb;
            var sw = d.Sweep;
            double top = x.ContinueStep * x.ContinueMax;
            if (sw == null) return 0;
            if (d.kInfMp >= 0 && T(d.kInfMp) > 0) return top;
            double cost = SweepCost(sw.Cost0, T(sw.kCostRed), T(sw.kSpecCostRed));
            if (cost <= 0) return top;
            double n = Math.Max(1, Math.Floor(Math.Max(0, MaxMp() - sw.Reserved) / cost)), m = Math.Min(n, x.ContinueMax + 1);
            return x.ContinueStep * (m * (m - 1) / 2 + (n - m) * x.ContinueMax) / n;
        }

        double XbC4Factor()
        {
            var x = d.Weapon.Xb;
            double spec = Math.Max(1e-6, d.Hits.Special * specialScale);
            double attach = d.Hits.Swing * SwingAs() + Math.Max(0, d.Hits.Other - d.Hits.Special);
            double bombs = Math.Min(x.C4Max, attach / spec);
            double def = Math.Floor(Math.Max(0, T(d.kDef)) / x.C4PerDef);
            return bombs * (elem[0] * x.C4PhysPct / 100.0 + x.C4Flat) * (1 + def / 100.0);
        }
    }
}
