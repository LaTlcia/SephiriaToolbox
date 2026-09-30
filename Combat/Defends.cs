using System;
using UnityEngine;

public partial class SephiriaToolbox
{
    const double DefendMinSeconds = 60;

    UnitAvatar defendOwner;
    Action<DamageInstance, bool> guardHandler;
    Action<DamageInstance> parryHandler;
    int guardCount, perfectGuardCount, parryCount;
    float defendCombatStart;

    void TrackDefends()
    {
        var a = LocalAvatar();
        if (a == defendOwner) return;
        UntrackDefends();
        if (a == null || !Mirror.NetworkServer.active) return;
        defendOwner = a;
        guardHandler = (damage, perfect) => { guardCount++; if (perfect) perfectGuardCount++; };
        parryHandler = damage => parryCount++;
        a.OnGuardSucceeded += guardHandler;
        a.OnParry += parryHandler;
        guardCount = perfectGuardCount = parryCount = 0;
        defendCombatStart = runCombatTime;
    }

    void UntrackDefends()
    {
        if (defendOwner != null)
        {
            try
            {
                defendOwner.OnGuardSucceeded -= guardHandler;
                defendOwner.OnParry -= parryHandler;
            }
            catch { }
        }
        defendOwner = null;
        guardHandler = null;
        parryHandler = null;
    }

    float guardHoldTime, guardHoldBattle;

    void TrackGuardHold()
    {
        var a = LocalAvatar();
        if (a == null || !a.IsInBattle || a.IsDead) return;
        var wc = a.GetComponent<WeaponControllerSimple>();
        if (wc == null || !(wc.currentWeapon is WeaponSimple_SwordAndShield ss)) return;
        float dt = Time.deltaTime;
        guardHoldBattle += dt;
        if (ss.isGuardAnimationTurnedOn) guardHoldTime += dt;
    }

    double GuardHoldUptime() => guardHoldBattle >= 20 ? guardHoldTime / guardHoldBattle : 0.1;

    bool DefendMeasured(out double guard, out double perfectGuard, out double parry)
    {
        guard = PlayGuard;
        perfectGuard = PlayParry * 0.5;
        parry = PlayParry;
        double seconds = runCombatTime - defendCombatStart;
        if (defendOwner == null || seconds < DefendMinSeconds) return false;
        guard = Math.Min(3, guardCount / seconds);
        perfectGuard = Math.Min(3, perfectGuardCount / seconds);
        parry = Math.Min(3, parryCount / seconds);
        return true;
    }
}
