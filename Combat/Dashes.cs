using System;
using UnityEngine;

public partial class SephiriaToolbox
{
    const double DashMinSeconds = 60;

    CharacterDash dashTracker;
    int dashLastUsed, dashPaidCount;
    float dashBattleTime;

    void TrackDashes()
    {
        var a = LocalAvatar();
        if (a == null) return;
        if (dashTracker == null || dashTracker.UnitAvatar != a)
        {
            dashTracker = a.GetComponent<CharacterDash>();
            dashLastUsed = dashTracker != null ? dashTracker.currentDashCount : 0;
        }
        if (dashTracker == null) return;
        int used = dashTracker.currentDashCount;
        if (a.IsInBattle && !a.IsDead)
        {
            dashBattleTime += Time.deltaTime;
            if (used > dashLastUsed) dashPaidCount += used - dashLastUsed;
        }
        dashLastUsed = used;
    }

    void ResetDashes()
    {
        dashPaidCount = 0;
        dashBattleTime = 0;
    }

    bool DashMeasured(out double rate)
    {
        rate = PlayDash;
        if (dashBattleTime < DashMinSeconds) return false;
        rate = Math.Min(1.5, Math.Max(0.02, dashPaidCount / dashBattleTime));
        return true;
    }

    static double DashCooldown(PlayerAvatar a)
    {
        var cd = a != null ? a.GetComponent<CharacterDash>() : null;
        double t = cd != null && cd.cooldownTimer != null && cd.cooldownTimer.time > 0 ? cd.cooldownTimer.time : 0.85;
        return t / Math.Max(0.1, 1 + (a != null ? a.GetCustomStat(ECustomStat.DashRecovery) : 0) / 100.0);
    }
}
