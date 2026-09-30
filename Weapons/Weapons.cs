using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed class WeaponModel
    {
        public double AsNow = 1;
        public bool Crossbow;
        public double XbInterval = 0.08, XbReload = 2, XbCap = 6, XbMags = 1, XbMoveExcess, XbRateNow = 1;
        public bool XbCompression, XbFrostRelic;
        public int kXbAmmo = -1, kXbMag = -1, kXbReload = -1, kXbFixedAmmo = -1, kXbDashReload = -1, kXbCritAmmo = -1, kXbGrenade = -1,
                   kXbMg = -1, kXbMoveAs = -1, kXbIceSlow = -1, kXbDashDmg = -1, kXbIceBuff = -1, kXbFrostRelicStat = -1, kFrostRelicDmg = -1;
        public bool Katana, Eclipse, CloudSlash;
        public int kDefKatana = -1, kSpeedSheath = -1, kEvaSheath = -1, kEvasion = -1;
        public double EclipsePerSword = 10, EclipseMaxSwords = 10, EclipseTime = 10, CloudSlashPerStack = 5, CloudSlashMax = 20,
                      DefKatanaUnit = 2, DefKatanaPct = 1, SheathRangePer = 1;
        public bool Dagger;
        public int kBloodyFury = -1, kCloudParry = -1;
        public double BloodyFuryPct;
        public bool CloudBottle;
        public double CloudBottlePct = 70;
        public bool SpecialMp, BasicMp;
        public bool Melee;
        public int kRange = -1;
        public double RangeNow = 1, HitsCap = 1, HitsCapM = 1;
        public double SpecialRate = PlaySpecial;
        public double DashAttackRate = PlayDash;
        public SsModel Ss;
        public GsModel Gs;
        public DgModel Dg;
        public XbModel Xb;
        public QsModel Qs;
        public readonly List<(int Item, float[] Ammo)> XbDashAmmo = new();
        public readonly List<CondDamage> CondDmg = new();
        public double BurnRingTime = 10;
    }

    static double RangeHits(double rangePercent) => Math.Max(0.2, 1 + 0.5 * rangePercent / 100.0);

    static int ConstOr(string key, int fallback)
    {
        try { return KeywordDatabase.GetConstValue(key); } catch { return fallback; }
    }

    void BuildWeaponModel(PlayerAvatar avatar, DpsModel d, Func<string, int> K, WeaponSimple weapon, Behavior play)
    {
        var w = d.Weapon;
        if (weapon == null) return;
        w.AsNow = AttackSpeedFactor(avatar.GetCustomStat(ECustomStat.AttackSpeed), WeaponAsAmp(weapon));
        w.SpecialMp = weapon is WeaponSimple_SwordAndShield or WeaponSimple_GreatSword or WeaponSimple_Dagger or WeaponSimple_Katana
                      or WeaponSimple_Katana_New or WeaponSimple_Crossbow or WeaponSimple_QuartterStaff;
        w.BasicMp = weapon is WeaponSimple_GreatSword gs && gs.useMPBasicAttack;
        w.Melee = !(weapon.weaponType is EWeaponType.Crossbow or EWeaponType.StaffMagic or EWeaponType.Golem or EWeaponType.Staff);
        w.kRange = K("WEAPONRANGE");
        w.RangeNow = RangeHits(avatar.GetCustomStat(ECustomStat.WeaponRange));
        w.HitsCap = Math.Max(1, play.Enemies / Math.Max(1, play.HitsPerSwing));
        w.HitsCapM = Math.Max(1, play.EnemiesM / Math.Max(1, play.HitsPerSwingM));
        w.SpecialRate = play.Special;
        w.DashAttackRate = play.DashAttack;
        switch (weapon)
        {
            case WeaponSimple_SwordAndShield ss:
                BuildSwordShieldModel(avatar, d, K, ss, play);
                break;
            case WeaponSimple_GreatSword great:
                BuildGreatSwordModel(avatar, d, K, great, play);
                break;
            case WeaponSimple_Crossbow xb:
            {
                w.Crossbow = true;
                if (xb.fireIntervalTimer != null && xb.fireIntervalTimer.time > 0) w.XbInterval = xb.fireIntervalTimer.time;
                if (xb.reloadTime > 0) w.XbReload = xb.reloadTime;
                w.XbCap = Math.Max(1, xb.defaultMagazineCapacity);
                w.XbMags = Math.Max(1, xb.defaultMagazineCount);
                w.XbCompression = xb.specialAttackType == WeaponSimple_Crossbow.ESpecialAttackType.AmmoCompression;
                w.XbFrostRelic = xb.specialAttackType == WeaponSimple_Crossbow.ESpecialAttackType.IceBuff;
                w.XbMoveExcess = Math.Max(0, avatar.moveSpeedMultiplier - 1);
                w.kXbAmmo = K("CROSSBOWAMMO"); w.kXbMag = K("CROSSBOWADDITIONALMAGAZINE"); w.kXbReload = K("CROSSBOWRELOADSPEED");
                w.kXbFixedAmmo = K("FIXEDAMMO"); w.kXbDashReload = K("DASHRELOAD"); w.kXbCritAmmo = K("ADDAMMOWHENCRITICAL");
                w.kXbGrenade = K("GRENADEATTACK"); w.kXbMg = K("CROSSBOWMG"); w.kXbMoveAs = K("MOVESPEEDTOATTACKSPEED");
                w.kXbIceSlow = K("ICEBUFF_DECREASEATTACKSPEED"); w.kXbDashDmg = K("DASHCOUNTDAMAGE");
                w.kXbIceBuff = K("ICECROSSBOWBUFF"); w.kXbFrostRelicStat = K("ICECROSSBOWFROSTRELIC"); w.kFrostRelicDmg = K("FROSTRELICDAMAGE");
                break;
            }
            case WeaponSimple_Katana kt:
            {
                w.Katana = true;
                w.Eclipse = kt.sheathActionType == WeaponSimple_Katana.ESheathActionType.Eclipse;
                w.CloudSlash = kt.sheathActionType == WeaponSimple_Katana.ESheathActionType.CloudSlash;
                w.EclipsePerSword = ConstOr("katanaEclipseDamageBonusByConsumeSwordCount", 10);
                w.EclipseMaxSwords = ConstOr("katanaConsumeSwordCountLimit", 10);
                w.EclipseTime = ConstOr("katanaEclipseBuffTime", 10);
                w.CloudSlashPerStack = kt.cloudSlashDamagePercentPerStack;
                w.CloudSlashMax = Math.Max(1, kt.residualLightningStackMax);
                w.DefKatanaUnit = Math.Max(1, ConstOr("defenseKatanaDamageBonusStatSplitUnit", 2));
                w.DefKatanaPct = ConstOr("defenseKatanaDamageBonusPercent", 1);
                w.SheathRangePer = Math.Max(1, ConstOr("katanaSheathRangePer", 1));
                w.kDefKatana = K("DEFENSEKATANA"); w.kSpeedSheath = K("SPEEDSHEATH"); w.kEvaSheath = K("EVASIONSHEATH"); w.kEvasion = K("EVASION");
                break;
            }
            case WeaponSimple_Dagger dagger:
                w.Dagger = true;
                w.kBloodyFury = K("BLOODYFURY");
                w.kCloudParry = K("DARKCLOUDPARRY");
                w.BloodyFuryPct = Math.Floor(avatar.MaxHp / Math.Max(1, ConstOr("daggerBloodyFuryHP", 2)));
                BuildDaggerModel(avatar, d, K, dagger, play);
                break;
            case WeaponSimple_QuartterStaff qs:
                w.CloudBottle = qs.changedDashAttackParameter == "CLOUDBOTTLE";
                w.CloudBottlePct = ConstOr("throwCloudBottleDamagePercent", 70);
                break;
        }
    }

    sealed partial class Evaluator
    {
        double CrossbowRate()
        {
            var w = d.Weapon;
            double spd;
            if (T(w.kXbGrenade) > 0) spd = 1;
            else if (T(d.kFixedAs) > 0) spd = T(d.kFixedAs) / 100.0;
            else
            {
                spd = 1 + T(d.kAs) / 100.0 - Math.Max(0, T(w.kXbIceSlow)) / 100.0;
                if (T(w.kXbMoveAs) > 0) spd += w.XbMoveExcess;
                if (T(w.kXbMg) > 0) spd += 0.3;
            }
            double fire = Math.Max(0.05, spd) / Math.Max(0.01, w.XbInterval);
            double mags = T(w.kXbFixedAmmo) > 0 ? 1 : Math.Max(1, w.XbMags + T(w.kXbMag));
            double n = T(w.kXbFixedAmmo) > 0 ? T(w.kXbFixedAmmo) : Math.Max(1, w.XbCap + T(w.kXbAmmo)) * mags;
            double refund = T(w.kXbCritAmmo);
            if (refund > 0)
            {
                double p = Math.Min(1, Math.Max(0, (T(d.kCrit) + T(d.kWCrit)) / 10000.0)) * refund;
                n = p >= 0.95 ? n * 20 : n / (1 - p);
            }
            double reload = w.XbReload / Math.Max(0.1, 1 + T(w.kXbReload) / 100.0);
            if (w.Xb != null && w.Xb.FastShare > 0) reload *= 1 - w.Xb.FastShare + w.Xb.FastShare / w.Xb.FastSpeed;
            if (T(w.kXbDashReload) > 0) reload = (1 - Math.Exp(-PlayDash * reload)) / PlayDash;
            double inflow = 0;
            foreach (var (item, ammo) in w.XbDashAmmo) if (ItemOn[item]) inflow += SafeAt(ammo, IdxOf(item)) * PlayDash;
            if (inflow > 0)
            {
                if (inflow >= fire * 0.999) return fire;
                double ts = n / (fire - inflow);
                return fire * ts / (ts + reload);
            }
            double s = w.Xb != null ? w.Xb.CompShare : 0;
            if (s > 0)
            {
                double nm = n / mags, rm = reload / mags;
                return ((1 - s) * nm + s) / ((1 - s) * (nm / fire + rm) + s * (1 / fire + rm));
            }
            return n / (n / fire + reload);
        }

        bool ComboOn(EcoKind kind)
        {
            foreach (var s in d.Sources)
                if (s.Eco == kind) return s.GateCat < 0 || s.GateCat >= catCount.Length || catCount[s.GateCat] >= s.GateCount;
            return false;
        }

        public void CalibrateWeapon()
        {
            if (!d.Weapon.Crossbow) return;
            Stats();
            d.Weapon.XbRateNow = Math.Max(1e-6, CrossbowRate());
        }

        double WeaponRate(DpsSource s)
        {
            return s.Kind == SrcKind.WeaponBasic ? SwingAs() : WeaponAs();
        }

        void EclipseState(out double fwd, out double share)
        {
            fwd = share = 0;
            var w = d.Weapon;
            if (!w.Eclipse || d.Eco == null || !ComboOn(EcoKind.FlameSword)) return;
            var e = d.Eco;
            double dur = w.EclipseTime * Pct(T(d.kBuffDur));
            double t = Math.Max(5, e.Battle);
            double first = Math.Min(w.EclipseMaxSwords, Math.Max(1, e.FsMax0 + T(e.kFsMax)));
            double auto = 0;
            foreach (var f in e.FsGen) auto += FeederRate(f);
            double later = Math.Min(w.EclipseMaxSwords, auto * dur);
            double firstTime = Math.Min(dur, t), rest = Math.Max(0, t - dur);
            fwd = (first * w.EclipsePerSword * firstTime + (later > 0 ? later * w.EclipsePerSword * rest : 0)) / t;
            share = (firstTime + (later > 0 ? rest : 0)) / t;
        }

        double CloudSlashStacks()
        {
            var w = d.Weapon;
            if (!w.CloudSlash || d.Eco == null || !ComboOn(EcoKind.DarkCloud)) return 0;
            double keep = Math.Min(Math.Max(T(d.Eco.kCloudKeep), 0), 99) / 100.0;
            return Math.Min(w.CloudSlashMax, CloudStrikes() / Math.Max(0.05, w.SpecialRate) / (1 - keep));
        }

        double WeaponDamage(DpsSource s, out double eclipseShare)
        {
            eclipseShare = 0;
            var w = d.Weapon;
            double v = 1;
            if (s.Kind == SrcKind.WeaponSpecial && w.SpecialMp || s.Kind == SrcKind.WeaponBasic && w.BasicMp) v *= Pct(T(d.kMpSkill));
            if (w.Ss != null)
            {
                if (s.Kind == SrcKind.WeaponSpecial) v *= SsSpecialFactor();
                else if (s.Kind == SrcKind.WeaponBasic && !s.AsDash) v *= SsBasicFactor();
            }
            if (w.Gs != null)
            {
                if (s.Kind == SrcKind.WeaponSpecial) v *= GsSpecialFactor();
                else if (s.Kind == SrcKind.WeaponBasic)
                    v *= GsBasicFactor(ElemFor(s.Formula, s.Element, elem) * Pct(T(d.kWdb)) * Pct(T(d.kBad)) * Pct(T(d.kFwd)));
            }
            if (w.Melee && w.kRange >= 0) v *= Math.Min(Single ? w.HitsCap : w.HitsCapM, RangeHits(T(w.kRange)) / w.RangeNow);
            if (w.Crossbow && s.Kind == SrcKind.WeaponBasic)
            {
                double add = T(w.kXbDashDmg) * T(d.kDashCount);
                v *= Pct(add);
                v *= XbShotFactor(s);
                if (T(w.kXbGrenade) > 0) v *= Pct(T(d.kAs));
            }
            if (w.Katana)
            {
                if (s.Kind == SrcKind.WeaponBasic && T(w.kDefKatana) > 0)
                    v *= Pct(Math.Floor(Math.Max(0, T(d.kDef)) / w.DefKatanaUnit) * w.DefKatanaPct);
                if (s.Kind is SrcKind.WeaponBasic or SrcKind.WeaponDash && w.Eclipse)
                {
                    EclipseState(out var fwd, out var share);
                    eclipseShare = share;
                    if (share > 0)
                    {
                        v *= Pct(T(d.kFwd) + fwd) / Pct(T(d.kFwd));
                        v *= 1 + share * T(d.Eco.kFsDmg) / 100.0;
                    }
                }
                if (s.Kind == SrcKind.WeaponSpecial)
                {
                    if (T(w.kEvaSheath) > 0) v *= Pct(Math.Floor(Math.Max(0, T(w.kEvasion)) / (100.0 * w.SheathRangePer)));
                    if (w.CloudSlash && d.Eco != null) v *= Pct(w.CloudSlashPerStack * CloudSlashStacks()) * Pct(T(d.Eco.kCloudDmg));
                }
            }
            if (w.Qs != null && s.Kind == SrcKind.WeaponBasic) v *= QsBasicFactor(s);
            if (w.Xb != null && s.Kind == SrcKind.WeaponSpecial)
                v *= w.Xb.Type == 4 ? XbShotFactor(s) : XbSpecialFactor();
            if (w.Dg != null)
            {
                if (s.Kind == SrcKind.WeaponSpecial) v *= DgSpecialFactor();
                else if (s.Kind == SrcKind.WeaponDash) v *= DgDashFactor();
            }
            return v;
        }
    }
}
