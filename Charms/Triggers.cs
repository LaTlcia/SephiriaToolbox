using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public partial class SephiriaToolbox
{
    enum Trig : byte
    {
        Unknown,
        Swing,
        BasicHit,
        DirectHit,
        DirectOrMagic,
        Special,
        Dash,
        Parry, Guard, Evade, Damaged, Kill,
        Crit,
        Periodic,
        Charge,
        Active,
        Summon,
        MagicCast,
        Orbit,
        Always
    }

    enum RateStat : byte { None, AttackSpeed, Crit, Luck, MagicCasts, DarkCloud }

    sealed class Mech
    {
        public Trig Trig;
        public float Per = 1;
        public float Cap;
        public string CapTimer;
        public string StatCapTimer;
        public string DashCdField;
        public float Base;
        public string TimerField;
        public float TimerExtra;
        public string ExtraField;
        public string CountBy;
        public string TargetsBy;
        public string ChanceBy;
        public string IntervalBy;
        public string InverseBy;
        public string BonusBy;
        public bool Targets;
        public bool Weight;
        public RateStat Stat;
        public float StatCap;
        public string HasteKey;
        public string AmpKey;
        public string Note;
    }

    static Mech FindMech(Charm_Basic c)
    {
        for (var t = c?.GetType(); t != null && t != typeof(Charm_Basic); t = t.BaseType)
            if (CharmMechs.TryGetValue(t.Name, out var mech)) return mech;
        return null;
    }

    static void ApplyMech(DpsSource s, Charm_Basic c, Mech mech, Behavior b, Func<string, int> key)
    {
        int levels = Math.Max(1, c.maxLevel + 1);
        var rate = Enumerable.Repeat(1f, levels).ToArray();
        MulLevels(rate, LevelTable(c, mech.CountBy), v => Math.Max(0f, v));
        MulLevels(rate, LevelTable(c, mech.ChanceBy), v => Math.Max(0f, v) / 100f);
        MulLevels(rate, LevelTable(c, mech.IntervalBy), v => v > 0 ? 1f / v : 0f);
        MulLevels(rate, LevelTable(c, mech.BonusBy), v => Math.Max(0f, 100f + v) / 100f);
        var inverse = LevelTable(c, mech.InverseBy);
        if (inverse != null && inverse[0] > 0) MulLevels(rate, inverse, v => v > 0 ? inverse[0] / v : 0f);
        if (s.Rate != null) for (int l = 0; l < levels; l++) rate[l] *= SafeAt(s.Rate, l);
        var rateM = (float[])rate.Clone();
        var targets = LevelTable(c, mech.TargetsBy);
        if (targets != null)
        {
            MulLevels(rate, targets, v => (float)Math.Min(Math.Max(v, 0f), b.Enemies));
            MulLevels(rateM, targets, v => (float)Math.Min(Math.Max(v, 0f), b.EnemiesM));
        }
        s.Rate = rate.Any(v => Math.Abs(v - 1f) > 1e-6f) ? rate : null;
        s.RateM = targets != null && rateM.Zip(rate, (x, y) => Math.Abs(x - y) > 1e-6f).Any(z => z) ? rateM : null;

        double swing = b.Swing + b.Strike, basicSwing = b.Rapier ? 0 : swing;
        double BaseRate(double hps, double kill) => mech.Trig switch
        {
            Trig.Swing => basicSwing,
            Trig.BasicHit => basicSwing * b.SwingHit * hps,
            Trig.DirectHit => (swing * b.SwingHit + b.Special + b.DashAttack) * hps,
            Trig.DirectOrMagic => (swing + b.Special + b.DashAttack) * hps + 0.3,
            Trig.Special => b.Special,
            Trig.Dash => buildDash,
            Trig.Parry => b.Parry,
            Trig.Guard => b.Guard,
            Trig.Evade => 1,
            Trig.Damaged => b.Damaged,
            Trig.Kill => kill,
            Trig.Crit => swing * hps,
            Trig.Summon => PlaySummon,
            Trig.MagicCast => 1,
            Trig.Orbit => mech.Base,
            Trig.Always => mech.Base,
            _ => PlayUnknown
        };
        double baseRate = BaseRate(b.HitsPerSwing, b.Kill);
        double baseRateM = BaseRate(b.HitsPerSwingM, PlayKill);
        if (mech.Trig is Trig.Periodic or Trig.Active)
        {
            float t = TimerSeconds(c, mech.TimerField);
            if (t <= 0 && c is Charm_Active act && s.Rate == null && mech.Base <= 0)
            {
                try { t = act.GetCooldownTime(out _, c.CurrentLevelToIdx(), 0); } catch { t = 0; }
            }
            float extra = mech.ExtraField != null && TimerSeconds(c, mech.ExtraField) > 0 ? TimerSeconds(c, mech.ExtraField) : mech.TimerExtra;
            baseRate = t > 0 ? 1.0 / (t + extra) : mech.Base > 0 ? mech.Base : 1;
            if (t > 0 && mech.DashCdField != null) baseRate *= 1 + Num(c, mech.DashCdField) * buildDash;
            if (mech.Trig == Trig.Active && s.Rate != null && TimerSeconds(c, mech.TimerField) <= 0) baseRate = 1;
            baseRateM = baseRate;
        }
        if (mech.Trig == Trig.Charge)
        {
            float charge = 10f;
            if (FieldValue(c, "chargingCharm") is ChargingCharm cc)
            {
                charge = Math.Max(0.1f, cc.defaultChargeTimer);
                if (mech.HasteKey == null && !cc.ignoreCooldownBonus)
                    s.HasteKey = cc.airSlashCooldownBonus ? key("AIRSLASHHASTE") : cc.voluspaCooldownBonus ? key("VOLUSPAHASTE") : -1;
            }
            baseRate = 1.0 / charge;
            baseRateM = baseRate;
            if (FieldValue(c, "chargingCharm") is ChargingCharm && mech.HasteKey != "CHARGINGCHARMBONUS") s.HasteKey2 = key("CHARGINGCHARMBONUS");
        }
        double cap = mech.Cap;
        if (mech.CapTimer != null && TimerSeconds(c, mech.CapTimer) > 0) cap = 1.0 / TimerSeconds(c, mech.CapTimer);
        if (cap > 0)
        {
            if (c is Charm_FrostiumRing) (s.X ??= new List<XMod>()).Add(new XMod { Kind = XKind.ArrFast, A = baseRate, B = cap });
            if (mech.Trig == Trig.Crit || mech.ChanceBy != null || mech.Stat is RateStat.Luck or RateStat.Crit)
            {
                s.CdSeconds = 1 / cap;
                s.CdBase = baseRate;
                s.CdBaseM = baseRateM;
                s.CdChance = mech.Weight ? b.AttackWeight : 1;
            }
            else
            {
                baseRate = Math.Min(baseRate, cap);
                baseRateM = Math.Min(baseRateM, cap);
            }
        }
        double per = mech.Per;
        if (mech.Weight) per *= b.AttackWeight;
        double perM = per;
        if (mech.Targets) { per *= b.Enemies; perM *= b.EnemiesM; }
        s.TheoryK = baseRate * per;
        s.MultiScale = s.TheoryK > 1e-9 ? baseRateM * perM / s.TheoryK : 1;
        s.HasTheory = true;
        if (mech.Trig is Trig.Active or Trig.Charge) { s.FirstFree = true; s.PerUse = Math.Max(1e-6, per); }
        if (FieldValue(c, "chargingCharm") is ChargingCharm) s.Relic = true;
        s.StatRate = mech.Stat;
        s.StatCap = mech.StatCapTimer != null && TimerSeconds(c, mech.StatCapTimer) > 0 ? 1f / TimerSeconds(c, mech.StatCapTimer) : mech.StatCap;
        if (mech.HasteKey != null) s.HasteKey = key(mech.HasteKey);
        if (mech.AmpKey != null) s.AmpKey = key(mech.AmpKey);
        if (mech.Stat == RateStat.Luck && c is Charm_PallasCard pc)
        {
            s.ChanceBase = pc.defaultChance;
            s.ChanceTable = pc.throwChanceByLevel;
            s.StatKey = key("LUCK");
        }
        if (mech.Stat is RateStat.AttackSpeed or RateStat.Crit || mech.Trig is Trig.Swing or Trig.BasicHit or Trig.DirectHit or Trig.DirectOrMagic or Trig.Crit)
            s.SwingBased = true;
        if (mech.Trig == Trig.Crit) s.StatRate = RateStat.Crit;
        if (mech.Trig == Trig.Evade) (s.X ??= new List<XMod>()).Add(new XMod { Kind = XKind.EvadeRate });
        if (mech.Trig == Trig.Summon) s.Summon = true;
        s.Note = mech.Note;
    }
}
