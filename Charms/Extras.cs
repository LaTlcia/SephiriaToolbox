using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    enum XKind : byte
    {
        RatePct,
        RatePctIf,
        RateAddIf,
        CountAdd,
        DmgPct,
        DmgIf,
        PerByStat,
        CdSpeed,
        Revive,
        Retrigger,
        Sweep,
        Planet,
        ArrFast,
        RelicSwords,
        MaxMpScale,
        NeedDebuff,
        ElecCdr,
        ExtraTriggers,
        EvadeRate,
        MaxMpMul
    }

    sealed class XMod
    {
        public XKind Kind;
        public int Key = -1, Key2 = -1;
        public double A, B, C, D;
        public float[] Levels;
    }

    const double SummonAliveSeconds = 40;

    static void AddX(DpsSource s, XMod x) => (s.X ??= new List<XMod>()).Add(x);

    static void ApplyExtras(DpsSource s, Charm_Basic c, Behavior b, Func<string, int> key)
    {
        int levels = Math.Max(1, c.maxLevel + 1);
        double baseRate = s.TheoryK > 0 ? s.TheoryK : 1;
        double swingHits = b.Swing * b.HitsPerSwing;
        for (var t = c.GetType(); t != null && t != typeof(Charm_Basic); t = t.BaseType)
            if (CharmExtras.TryGetValue(t, out var fn))
            {
                fn(s, c, b, key, levels, baseRate, swingHits);
                break;
            }
        if (FieldValue(c, "chargingCharm") is ChargingCharm)
        {
            double per = 100;
            try { per = Math.Max(1, KeywordDatabase.GetConstValue("chargingCharmRetriggerByAttackSpeed")); } catch { }
            AddX(s, new XMod { Kind = XKind.Retrigger, Key = key("CHARGINGCHARMRETRIGGERBYATTACKSPEED"), A = per });
            s.MpMulKey = key("FROSTRELICMPDAMAGE");
            s.MpFlatKey = key("FROSTRELICMPMAXMPDAMAGE");
        }
    }

    static void AddRevive(DpsSource s, Charm_Basic c, Func<string, int> key)
    {
        float revive = TimerSeconds(c, "defaultReviveTimer");
        if (revive > 0) AddX(s, new XMod { Kind = XKind.Revive, Key = key("FOLLOWERREVIVE"), A = SummonAliveSeconds, B = revive });
    }

    sealed partial class Evaluator
    {
        double XFactor(DpsSource s, int idx, bool rateOnly = false)
        {
            double f = 1;
            foreach (var x in s.X)
            {
                if (rateOnly && x.Kind is XKind.DmgPct or XKind.DmgIf) continue;
                switch (x.Kind)
                {
                    case XKind.RatePct:
                    case XKind.DmgPct:
                        f *= Pct(T(x.Key));
                        break;
                    case XKind.RatePctIf:
                        if (T(x.Key) > 0) f *= Pct(T(x.Key2));
                        break;
                    case XKind.RateAddIf:
                        if (T(x.Key) > 0) f *= 1 + x.A;
                        break;
                    case XKind.DmgIf:
                        if (T(x.Key) > 0) f *= x.A;
                        break;
                    case XKind.CountAdd:
                    {
                        double n = SafeAt(x.Levels, idx);
                        if (n > 0) f *= Math.Max(0, n + T(x.Key)) / n;
                        break;
                    }
                    case XKind.PerByStat:
                        f *= (x.A + Math.Floor(Math.Max(0, T(x.Key)) / x.B)) / x.C;
                        break;
                    case XKind.CdSpeed:
                    {
                        double v = T(x.Key);
                        if (v <= 0) break;
                        double speed = 1 + (x.C * WeaponAs() + x.D) * v / 10.0;
                        f *= (x.A + x.B) / (x.A / speed + x.B);
                        break;
                    }
                    case XKind.Revive:
                    {
                        double up0 = x.A / (x.A + x.B), up = x.A / (x.A + x.B / Pct(T(x.Key)));
                        f *= up / up0;
                        break;
                    }
                    case XKind.Retrigger:
                    {
                        double v = T(x.Key), a = T(d.kAs);
                        if (v > 0 && a > 0) f *= 1 + Math.Min(100, Math.Floor(a / x.A) * v) / 100.0;
                        break;
                    }
                    case XKind.Sweep:
                    {
                        if (T(x.Key) <= 0) break;
                        double lambda = Math.Max(1e-6, x.C * SpecialScale()), miss = Math.Exp(-lambda * x.A);
                        f *= (x.A + x.B) / ((1 - miss) / lambda + miss * x.B);
                        break;
                    }
                    case XKind.Planet:
                        f *= Pct(T(x.Key)) + (T(x.Key2) > 0 ? (x.B * WeaponAs() + x.C) * x.A : 0);
                        break;
                    case XKind.ArrFast:
                        if (s.Item >= 0 && arrFast[s.Item] && x.B > 0) f *= Math.Min(x.A, 3 * x.B) / Math.Max(1e-9, Math.Min(x.A, x.B));
                        break;
                    case XKind.RelicSwords:
                        f *= Math.Min(x.B, x.A * RelicFireRate()) * x.C;
                        break;
                    case XKind.MaxMpScale:
                    {
                        if (rateOnly || x.Levels == null || x.Levels.Length < 2) break;
                        double def = x.Levels[1], was = x.Levels[0] - def;
                        if (was > 1e-6) f *= Math.Max(0, MaxMp() - def) / was;
                        break;
                    }
                    case XKind.NeedDebuff:
                        f *= DebuffPresence((int)x.A);
                        break;
                    case XKind.MaxMpMul:
                        if (!rateOnly) f *= Math.Max(0, MaxMp());
                        break;
                    case XKind.EvadeRate:
                        f *= EvadeRate();
                        break;
                    case XKind.ExtraTriggers:
                        f *= 1 + x.A * WeaponAs() + x.B;
                        break;
                    case XKind.ElecCdr:
                    {
                        double p = elecPops * x.A;
                        if (p <= 0) break;
                        double h = Math.Max(0.01, Pct(T(x.Key)));
                        f *= (h + p) / h * x.B / (x.B + x.A / 2 * p / (h + p));
                        break;
                    }
                }
            }
            return f;
        }

        double RelicFireRate()
        {
            double sum = 0;
            foreach (var s in d.Sources)
                if (s.Relic && s.Kind == SrcKind.Proc && s.X?.Any(x => x.Kind == XKind.RelicSwords) != true)
                    sum += HitRate(s) / Math.Max(1e-6, s.PerUse);
            return sum;
        }

        double ChargeMp(DpsSource s, double v)
        {
            double skill = Pct(T(d.kMpSkill));
            if (T(s.MpMulKey) > 0) v *= Pct(T(s.MpMulKey)) * skill;
            if (T(s.MpFlatKey) > 0) v += Math.Max(0, MaxMp() - d.DefaultMp) * skill;
            return v;
        }

        double MaxMp() => d.kMaxMp >= 0 ? T(d.kMaxMp) * Pct(d.kFinalMp >= 0 ? T(d.kFinalMp) : 0) : 0;
    }
}
