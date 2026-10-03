using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public partial class SephiriaToolbox
{
    const double SweepCap = 1.25;

    sealed class SweepModel
    {
        public byte Mode;
        public double Base;
        public double BaseEval;
        public bool Measured;
        public bool Absolute;
        public bool Limited = true;
        public double Cap = SweepCap;
        public double CapAs;
        public double CapAsMul = 1;
        public double Cost0, PoolPeriod, Dps, Reserved;
        public int kCostRed = -1, kSpecCostRed = -1, kRegen = -1, kResonance = -1, kSteal = -1;
        public double GuardRed, PerfectRed;
    }

    double SweepDps(PlayerAvatar a)
    {
        try { if (runCombatTime >= 20) return a.dealsStatistics.Values.Sum() / runCombatTime; } catch { }
        return 0;
    }

    static double MpGain(UnitAvatar a) => a == null ? 1 : Math.Max(0, 1 + a.GetCustomStatUnsafe("MPREGENMULTIPLE") / 100.0);

    static double SweepRate(double cost, double regenStat, double resonance, double stealPermille, double dps, double pool, double poolPeriod,
                            double net = 0, double otherUses = 0, double cap = SweepCap)
    {
        if (cost <= 0) return cap;
        double regen = Math.Max(0, regenStat) * 0.1;
        double pause = 0.85 / Math.Max(0.1, 1 + resonance / 100.0);
        double income = Math.Max(0, stealPermille) / 1000.0 * dps + (poolPeriod > 0 ? Math.Max(0, pool) / poolPeriod : 0) - net;
        double s = Math.Min(cap, Math.Max(0, regen * Math.Max(0, 1 - pause * otherUses) + income) / cost);
        for (int i = 0; i < 30; i++) s = Math.Min(cap, Math.Max(0, regen * Math.Max(0, 1 - pause * (s + otherUses)) + income) / cost);
        return Math.Max(0.02, s);
    }

    static double SweepCost(double cost0, double sweepRed, double specRed) =>
        Math.Floor(Math.Floor(Math.Max(0, cost0 * (1 - sweepRed / 100.0))) * Math.Max(0, 1 - specRed / 100.0));

    static double SweepBuffReduction(CharacterBuff prefab)
    {
        if (prefab == null) return 0;
        double v = 0;
        foreach (var (key, mode, value) in BuffStats(prefab)) if (mode == 0 && key == "SWEEPCOSTREDUCTION") v += value;
        return v;
    }

    static double ActiveSweepBuff(PlayerAvatar a, CharacterBuff prefab)
    {
        if (prefab == null || string.IsNullOrEmpty(prefab.ID)) return 0;
        if (BuffsField?.GetValue(a) is not Dictionary<string, CharacterBuff> buffs || !buffs.TryGetValue(prefab.ID, out var active) || active == null) return 0;
        return SweepBuffReduction(prefab) * active.Amplified * active.CurrentStack;
    }

    double SweepRateByMp(PlayerAvatar a, WeaponSimple_SwordAndShield ss, double poolPeriod, out double cost)
    {
        var live = a.GetComponent<WeaponControllerSimple>()?.currentWeapon as WeaponSimple_SwordAndShield;
        double buffNow = live != null ? ActiveSweepBuff(a, live.buffPrefab) + ActiveSweepBuff(a, live.perfectGuardBuffPrefab) : 0;
        cost = SweepCost(ss.sweepMpCost, a.GetCustomStat(ECustomStat.SweepCostReduction) - buffNow, a.GetCustomStatUnsafe("SPECIALATTACKCOSTREDUCTION"));
        if (a.GetCustomStatUnsafe("INFINITYMP") > 0) return SweepCap;
        return SweepRate(cost, a.GetCustomStatUnsafe("MPREGEN") * MpGain(a), a.GetCustomStatUnsafe("MPRESONANCE"), a.GetCustomStat(ECustomStat.MPSteal) * MpGain(a),
                         SweepDps(a), a.MaxMp - a.reservedMp, poolPeriod);
    }

    WeaponControllerSimple specialController;
    Action<int> specialHandler;
    int specialCount;
    float specialCombatStart;

    void TrackSpecials()
    {
        var a = LocalAvatar();
        var wc = a != null ? a.GetComponent<WeaponControllerSimple>() : null;
        if (wc == specialController) return;
        UntrackSpecials();
        if (wc == null || !Mirror.NetworkServer.active) return;
        specialController = wc;
        specialHandler = _ => { if (runCombatTime - lastSpecialCombat > 0.05f || specialCount == 0) specialCount++; lastSpecialCombat = runCombatTime; };
        wc.OnSpecialAttackSwing = (Action<int>)Delegate.Combine(wc.OnSpecialAttackSwing, specialHandler);
        specialCount = 0;
        specialCombatStart = runCombatTime;
    }

    float lastSpecialCombat = -1f;

    void UntrackSpecials()
    {
        if (specialController != null && specialHandler != null)
        {
            try { specialController.OnSpecialAttackSwing = (Action<int>)Delegate.Remove(specialController.OnSpecialAttackSwing, specialHandler); } catch { }
        }
        specialController = null;
        specialHandler = null;
    }

    bool SpecialMeasuredRate(out double rate)
    {
        rate = PlaySpecial;
        double seconds = runCombatTime - specialCombatStart;
        if (specialController == null || seconds < 30 || specialCount < 5) return false;
        rate = Math.Max(0.02, Math.Min(3, specialCount / seconds));
        return true;
    }
}
