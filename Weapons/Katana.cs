using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed class KtModel
    {
        public double BasicMult = 1;
        public double Tempo = 0.456;
        public double DrawMult, DrawEvery = 10, DrawSheath = 0.375, DrawTime = 0.55, DrawSkip = 0.333;
        public string DrawFormula;
        public EDamageElementalType DrawElem;
        public double NanmuTime, NanmuFill = 10, NanmuRatio = 1;
        public double HeatPerSec, HeatMult, HeatSpeed = 3, HeatTempo = 1;
        public string HeatFormula;
        public EDamageElementalType HeatElem;
        public double HardenTime, HardenCount, HardenDef, StuckMult, StuckUnit = 2, StuckPct = 4, StuckSeconds = 0.79, StuckOverhead = 1.5;
        public bool Issen;
        public double IssenSpeed = 3, IssenCost = 2, IssenRatio = 1, SheathCap = 1 / 0.75;
        public int kElecStack = -1;
        public double[] CloudStage, CloudStageEx;
        public double CloudPerStage = 5;
        public int kCloudLuck = -1;
        public double EclipseBasic = 1;
        public int kEnhDash = -1;
        public double EnhDashMult = 1;
    }

    const string EnhDashKey = "__KATANAENHANCEDDASH";

    static NewWeaponFireData KatanaGreatDraw(WeaponSimple_Katana kt, out double every)
    {
        every = kt.attackToFillGaugeValue > 0 ? Math.Ceiling(1 / kt.attackToFillGaugeValue - 1e-4) : 10;
        return KatanaSheathKind(kt) == 4 && kt.hasKatanaGauge && kt.useAttackToFillGauge && !kt.useNanmu ? kt.greatDrawFireData : null;
    }

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
                mv.SpecialFd = At(kt.cloudSlashFireDatas, 0) ?? At(sp, 3) ?? At(sp, 0);
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

    void BuildKatanaModel(PlayerAvatar avatar, DpsModel d, Func<string, int> K, WeaponSimple_Katana kt, Behavior play, ArrModel m)
    {
        var q = d.Weapon.Kt = new KtModel();
        var mv = KatanaMoves(kt);
        var basic = kt.basicComboAttacks ?? new NewWeaponFireData[0];
        q.BasicMult = Math.Max(0.01, mv.BasicMult);
        int kind = KatanaSheathKind(kt);
        double c1 = ClipSeconds("Katana_Attack1", 0.333), c2 = ClipSeconds("Katana_Attack2", 0.3), c3 = ClipSeconds("Katana_Attack3", 0.4),
               c4 = ClipSeconds("Katana_Attack4", 0.5), e3 = ClipSeconds("Katana_Attack3_End", 0.333), e4 = ClipSeconds("Katana_Attack4_End", 0.25),
               sheathing = ClipSeconds("Katana_Sheathing", 0.375);
        q.Tempo = (c1 + c2 + c3 + e3) / 3;
        var great = KatanaGreatDraw(kt, out double every);
        if (great != null && mv.BasicFd != null)
        {
            q.DrawMult = HitMult(great);
            q.DrawEvery = every;
            q.DrawFormula = great.relatedStatFormula;
            q.DrawElem = great.damageElementalType;
            q.DrawSheath = sheathing;
            q.DrawTime = ClipSeconds("Katana_Drawing", 0.55);
            q.DrawSkip = e3;
            m.Notes.Add(Tr("weapon.great_draw", every, q.DrawMult, q.DrawSheath + q.DrawTime));
        }
        if (kind == 4 && kt.useNanmu && kt.hasKatanaGauge && kt.useAttackToFillGauge && kt.nanmuEndTimer != null && kt.nanmuEndTimer.time > 0)
        {
            var combo4 = Enumerable.Range(0, Math.Min(basic.Length, 4)).Select(i => At(basic, i)).ToArray();
            double extra = kt.nanmuExtraSwingFireDatas != null && kt.nanmuExtraSwingFireDatas.Any(f => f != null) ? AvgMult(kt.nanmuExtraSwingFireDatas) : 0;
            q.NanmuTime = kt.nanmuEndTimer.time;
            q.NanmuFill = every;
            q.NanmuRatio = (AvgMult(combo4) + extra) / q.BasicMult * q.Tempo / ((c1 + c2 + c3 + c4 + e4) / 4);
            m.Notes.Add(Tr("weapon.nanmu", every, q.NanmuTime, q.NanmuRatio));
        }
        if (StatWithWeapon(avatar, "KATANADASHSTACK", kt) > 0 && kt.basicAttack_Overheat_FireDatas != null)
        {
            var heat = kt.basicAttack_Overheat_FireDatas.Take(kt.dashStackAttackMoveSet > 0 ? Math.Max(1, kt.dashStackFinalComboIdx + 1) : 99).Where(f => f != null).ToArray();
            if (heat.Length > 0)
            {
                q.HeatPerSec = buildDash;
                q.HeatMult = AvgMult(heat);
                q.HeatSpeed = Math.Max(0.1, kt.dashStackFixedAttackSpeed / 100.0);
                q.HeatTempo = q.Tempo / ((c1 + c2) / 2);
                q.HeatFormula = heat[0].relatedStatFormula;
                q.HeatElem = heat[0].damageElementalType;
                m.Notes.Add(Tr("weapon.overheat", q.HeatPerSec, q.HeatMult));
            }
        }
        if (kt.useSheathHardening && kt.stuckBladeAttack_FireDatas != null && kt.stuckBladeAttack_FireDatas.Any(f => f != null))
        {
            q.HardenCount = Math.Max(1, ConstOr("katanaSheathHardeningMaxCount", 10));
            q.HardenTime = q.HardenCount * ConstOr("katanaSheathHardeningTimer_div10", 8) * 0.1;
            q.HardenDef = q.HardenCount * ConstOr("katanaSheathHardeningDefenseBonus", 1);
            q.StuckMult = kt.stuckBladeAttack_FireDatas.Where(f => f != null).Average(f => (double)f.damageMultiplier);
            q.StuckUnit = Math.Max(1, ConstOr("katanaSheathHardeningAttackStateDamageBonusStatSplitUnit", 2));
            q.StuckPct = ConstOr("katanaSheathHardeningAttackStateDamageBonus", 4);
            q.StuckSeconds = (ClipSeconds("Katana_SheathAttack1", 0.667) + ClipSeconds("Katana_SheathAttack2", 0.667) + ClipSeconds("Katana_SheathAttack2_End", 0.25)) / 2;
            q.StuckOverhead = 2 * sheathing + 0.75;
            m.Notes.Add(Tr("weapon.hardening", q.HardenTime, q.HardenCount, q.StuckMult, q.StuckPct, q.StuckUnit));
        }
        if (kind == 0 && StatWithWeapon(avatar, "KATANAELECTRICCHARGE", kt) > 0 && kt.electricChargeIssenFireData != null && mv.SpecialMult > 0)
        {
            q.Issen = true;
            q.IssenSpeed = Math.Max(1, kt.electricChargeIssenAttackSpeedMultiplier);
            q.IssenCost = Math.Max(1, kt.electricChargeIssenCost);
            q.IssenRatio = HitMult(kt.electricChargeIssenFireData) / mv.SpecialMult;
            q.SheathCap = 1 / Math.Max(0.1, kt.sheathAttackTime);
            q.kElecStack = K("ELECTRICSTACK");
            m.Notes.Add(Tr("weapon.issen", q.IssenCost, q.IssenSpeed));
        }
        if (kind == 3 && kt.cloudSlashFireDatas != null && kt.cloudSlashFireDatas.Length >= 8 && kt.cloudSlashFireDatas.Take(8).All(f => f != null))
        {
            double m0 = Math.Max(0.01, HitMult(kt.cloudSlashFireDatas[0]));
            q.CloudStage = Enumerable.Range(0, 4).Select(i => HitMult(kt.cloudSlashFireDatas[i]) / m0).ToArray();
            q.CloudStageEx = Enumerable.Range(4, 4).Select(i => HitMult(kt.cloudSlashFireDatas[i]) / m0).ToArray();
            q.CloudPerStage = Math.Max(1, kt.residualLightningStackPerStage);
            q.kCloudLuck = K("DARKCLOUDLUCK");
        }
        if (kind == 2 && kt.eclipseBasicAttackFireDatas != null && kt.eclipseBasicAttackFireDatas.Any(f => f != null))
        {
            double b14 = ClipSeconds("Katana_BigFireAttack1", 0.283) + ClipSeconds("Katana_BigFireAttack2", 0.283) + ClipSeconds("Katana_BigFireAttack3", 0.283)
                       + ClipSeconds("Katana_BigFireAttack4", 0.283);
            double tempo = (b14 + ClipSeconds("Katana_BigFireAttack5", 0.417) + ClipSeconds("Katana_BigFireAttack5_End", 0.25)) / 5;
            q.EclipseBasic = AvgMult(kt.eclipseBasicAttackFireDatas) / q.BasicMult * q.Tempo / tempo;
            m.Notes.Add(Tr("weapon.eclipse_combo", q.EclipseBasic));
        }
        q.kEnhDash = K(EnhDashKey);
        if (mv.DashMult > 0 && kt.enhancedDashAttackFireData_Physical != null)
            q.EnhDashMult = HitMult(kt.enhancedDashAttackFireData_Physical) / mv.DashMult;
        if (kt.useMagicDamageEnhancement && kt.hasKatanaGauge && kt.useAutoFillOnSheath && kt.magicDamageEnhancementValue != 0)
        {
            double add = kt.magicDamageEnhancementValue * SwordAuraShare;
            d.ExtraBase[d.kMdb] = d.ExtraBase.GetValueOrDefault(d.kMdb) + add;
            m.Notes.Add(Tr("weapon.sword_aura", kt.magicDamageEnhancementValue, SwordAuraShare));
        }
        if (kt.addons != null)
            foreach (var ga in kt.addons.OfType<WeaponAddonKatana_SummonGhost>())
            {
                var ghost = ga?.ghostPrefab != null ? ga.ghostPrefab.GetComponent<KatanaGhost>() : null;
                if (ghost == null || ghost.dashAttackIntervalTimer == null) continue;
                int fixedDash = avatar.GetCustomStatUnsafe("FIXEDDASH");
                double dashes = fixedDash > 0 ? fixedDash : avatar.GetCustomStat(ECustomStat.DashCount);
                double cd = Math.Max(0.05, ghost.dashAttackIntervalTimer.time - (ghost.reduceIntervalByDashCount ? ghost.intervalReducePerDash * dashes : 0));
                double near = buildCloseCdf != null ? CloseShare(buildCloseCdf, ghost.dashAttackCheckRadius) : 0.6;
                double attacks = Math.Max(1, ghost.dashAttackCount) * Math.Min(buildDash, 1 / cd) * near;
                play.DashAttack += attacks;
                m.Notes.Add(Tr("weapon.ghost", attacks, cd));
            }
    }

    const double SwordAuraShare = 0.8;

    void KatanaLiveState(PlayerAvatar avatar, DpsModel d, Func<string, int> K, Dictionary<int, double> curRaw)
    {
        if (avatar.GetComponent<WeaponControllerSimple>()?.currentWeapon is not WeaponSimple_Katana live) return;
        void Sub(int k, double v) { if (v != 0) curRaw[k] = curRaw.GetValueOrDefault(k) + v; }
        if (live.dashStackCounter > 0 && avatar.GetCustomStatUnsafe("FIXEDATTACKSPEED") >= live.dashStackFixedAttackSpeed) Sub(d.kFixedAs, live.dashStackFixedAttackSpeed);
        if (live.isEclipseBuffActivated && FieldValue(live, "consumedSwordCount") is int swords && swords > 0)
            Sub(d.kFwd, swords * ConstOr("katanaEclipseDamageBonusByConsumeSwordCount", 10));
        if (FieldValue(live, "isMagicDamageEnhancementActive") is bool on && on) Sub(d.kMdb, live.magicDamageEnhancementValue);
        if (live.useSheathHardening && FieldValue(live, "hardeningCount") is int layers && layers > 0)
            Sub(d.kDef, layers * ConstOr("katanaSheathHardeningDefenseBonus", 1));
    }

    void AddKatanaSources(PlayerAvatar avatar, DpsModel d, Func<string, int> K, WeaponSimple weapon, List<DpsSource> sources)
    {
        if (weapon is not WeaponSimple_Katana kt) return;
        if (kt.useMagicBlade)
        {
            double final = Pct(avatar.GetCustomStat(ECustomStat.FinalMP));
            double missing = avatar.GetCustomStatUnsafe("INFINITYMP") > 0 ? 0 : avatar.reservedMp + Math.Max(0, avatar.MaxMp - avatar.reservedMp) * 0.5;
            var s = new DpsSource
            {
                Kind = SrcKind.Ability, Name = Tr("src.weapon_magic_blade"), Preset = true, Table = new[] { (float)(kt.magicBladeDamge + missing * 0.7) },
                Slopes = missing > 0 ? new[] { new Slope { Key = d.kMaxMp, PerLevel = new[] { (float)(0.5 * 0.7 * final) } } } : null,
                MulKeys = new[] { d.kMdb }, StatRate = RateStat.MagicCasts, MagicCrit = true, Prior = 1, TheoryK = 1, HasTheory = true, DmgElem = 0,
                Note = Tr("model.magic_blade", kt.magicBladeDamge, missing)
            };
            s.Ids.Add(kt.magicBladeDamageId);
            sources.Add(s);
        }
        if (kt.addons != null)
            foreach (var ns in kt.addons.OfType<WeaponAddonKatana_Nastrond>())
            {
                if (ns == null) continue;
                double ice = avatar.GetCustomStat(ECustomStat.IceDamage);
                var cc = ns.chargingCharm;
                double charge = cc != null ? Math.Max(0.1, cc.defaultChargeTimer) : 4;
                double per = Math.Max(1, ConstOr("chargingCharmRetriggerByAttackSpeed", 100));
                var s = new DpsSource
                {
                    Kind = SrcKind.Ability, Name = Tr("src.weapon", TrimPrefix(DamageIdName(ns.damageId), "武器")), Preset = true,
                    Table = new[] { (float)(ns.defaultDamage + ice * ns.damagePercent / 100.0) },
                    Slopes = new[] { new Slope { Key = d.kElem[2], Elem = 2, PerLevel = new[] { ns.damagePercent / 100f } } },
                    MulKeys = new[] { K("FROSTRELICDAMAGE") }, HasteKey = cc != null && cc.ignoreCooldownBonus ? -1 : K("CHARGINGCHARMBONUS"),
                    AmpKey = K("CHARGINGCHARMAMPLIFY"), MpMulKey = K("FROSTRELICMPDAMAGE"), MpFlatKey = K("FROSTRELICMPMAXMPDAMAGE"), Relic = true,
                    X = new List<XMod> { new XMod { Kind = XKind.Retrigger, Key = K("CHARGINGCHARMRETRIGGERBYATTACKSPEED"), A = per } },
                    Prior = 1 / (charge + 0.5), TheoryK = 1 / (charge + 0.5), HasTheory = true, DmgElem = 2,
                    Note = Tr("model.nastrond", charge, ns.defaultDamage, ns.damagePercent)
                };
                s.Ids.Add(ns.damageId);
                sources.Add(s);
            }
    }

    void BuildKatanaSweep(PlayerAvatar avatar, DpsModel d, Func<string, int> K, WeaponSimple_Katana kt, Behavior play, ArrModel m)
    {
        try { BuildKatanaModel(avatar, d, K, kt, play, m); } catch (Exception e) { WarnOnce("太刀的专属机制", e); d.Weapon.Kt = null; }
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
            : SweepRate(cost, avatar.GetCustomStatUnsafe("MPREGEN") * MpGain(avatar), avatar.GetCustomStatUnsafe("MPRESONANCE"), avatar.GetCustomStat(ECustomStat.MPSteal) * MpGain(avatar),
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

    sealed partial class Evaluator
    {
        bool SpecialPaysMp()
        {
            var sw = d.Sweep;
            if (sw == null || sw.Mode != 0) return true;
            return SweepCost(sw.Cost0, T(sw.kCostRed), T(sw.kSpecCostRed)) > 0;
        }

        double KtBasicFactor(DpsSource s)
        {
            var q = d.Weapon.Kt;
            if (q == null) return 1;
            double f = 1, eBasic = Math.Max(1e-6, ElemFor(s.Formula, s.Element, elem));
            double n0 = Math.Max(0.05, d.Hits.Swing), a = SwingAs(), n = n0 * a;
            if (q.DrawMult > 0)
            {
                double lb = d.kLastBasic >= 0 ? T(d.kLastBasic) : 0, fd = d.kFinalDmg >= 0 ? T(d.kFinalDmg) : 0, last = Pct(lb) * Pct(fd);
                double g = q.DrawMult * ElemFor(q.DrawFormula, q.DrawElem, elem) / eBasic * last / (1 - s.FinalShare + s.FinalShare * last);
                double time = Math.Max(0, q.DrawSheath * a + q.DrawTime - q.DrawSkip) * n0 / q.DrawEvery;
                f *= Math.Max(1, (1 + g / (q.DrawEvery * q.BasicMult)) / (1 + time));
            }
            if (q.NanmuTime > 0)
            {
                double dur = q.NanmuTime * Pct(T(d.kBuffDur)), up = dur / (dur + q.NanmuFill / n);
                f *= 1 - up + up * q.NanmuRatio;
            }
            if (q.HeatPerSec > 0)
            {
                double fast = q.HeatSpeed * q.HeatTempo * n0, heat = Math.Min(q.HeatPerSec, fast);
                f *= 1 - heat / fast + heat * q.HeatMult * ElemFor(q.HeatFormula, q.HeatElem, elem) / (eBasic * n * q.BasicMult);
            }
            if (q.HardenCount > 0)
            {
                var w = d.Weapon;
                double def = Math.Max(0, T(d.kDef)), defStuck = def + q.HardenDef;
                bool defKatana = T(w.kDefKatana) > 0;
                double normalAdd = defKatana ? Math.Floor(def / w.DefKatanaUnit) * w.DefKatanaPct : 0;
                double stuckAdd = (defKatana ? Math.Floor(defStuck / w.DefKatanaUnit) * w.DefKatanaPct : 0) + Math.Floor(defStuck / q.StuckUnit) * q.StuckPct;
                double stuck = q.StuckMult * Pct(stuckAdd) / Pct(normalAdd);
                double busy = Math.Min(1, Math.Max(0.05, n0 * q.Tempo));
                double phase = q.HardenCount * q.StuckSeconds / busy + q.StuckOverhead, normal = n * q.BasicMult;
                f *= Math.Max(1, (q.HardenTime * normal + q.HardenCount * stuck) / ((q.HardenTime + phase) * normal));
            }
            return f;
        }

        double KtDashFactor(DpsSource s)
        {
            var q = d.Weapon.Kt;
            if (q == null || q.kEnhDash < 0) return 1;
            double share = Math.Min(1, Math.Max(0, T(q.kEnhDash)) / 100.0);
            if (share <= 0) return 1;
            double e0 = Math.Max(1e-6, ElemFor(s.Formula, s.Element, elem)), best = Math.Max(Math.Max(elem[0], elem[1]), Math.Max(elem[2], elem[3]));
            double bonus = d.kDashDmg >= 0 ? Math.Max(0, T(d.kDashDmg)) / 100.0 : 0;
            return (1 - share + (share + bonus) * q.EnhDashMult * best / e0) / (1 + bonus);
        }

        double KtSpecialFactor()
        {
            var q = d.Weapon.Kt;
            if (q == null) return 1;
            double f = 1;
            if (q.CloudStage != null && d.Eco != null)
            {
                double stacks = CloudSlashStacks();
                double stage = stacks >= d.Weapon.CloudSlashMax - 1e-6 ? 3 : Math.Min(3, Math.Max(0, (stacks - (q.CloudPerStage - 1) / 2) / q.CloudPerStage));
                int lo = (int)Math.Floor(stage), hi = Math.Min(3, lo + 1);
                double t = stage - lo, luck = Math.Min(100, Math.Max(0, T(q.kCloudLuck))) / 100.0;
                double normal = q.CloudStage[lo] * (1 - t) + q.CloudStage[hi] * t, ex = q.CloudStageEx[lo] * (1 - t) + q.CloudStageEx[hi] * t;
                f *= (1 - luck) * normal + luck * ex;
            }
            if (q.Issen)
            {
                double r0 = Math.Max(1e-6, d.Hits.Special * specialScale), fast = q.IssenSpeed * q.SheathCap;
                double issen = Math.Min(elecPops / q.IssenCost, fast), normal = Math.Min(r0, (1 - issen / fast) * q.SheathCap);
                double ratio = q.IssenRatio * (1 + (2 + Math.Max(0, T(q.kElecStack))) * 0.5) / (SpecialPaysMp() ? Pct(T(d.kMpSkill)) : 1);
                f *= (normal + issen * ratio) / r0;
            }
            return f;
        }
    }
}
