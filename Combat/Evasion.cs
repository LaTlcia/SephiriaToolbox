using System;
using System.Collections.Generic;

public partial class SephiriaToolbox
{
    static double EvasionChance(double evasion) =>
        evasion <= 0 ? 0 : Math.Max(0, Math.Log(Math.Min(evasion, 10000) / 6200.0 + 1) * 0.8);

    sealed partial class Evaluator
    {
        double EvadeRate()
        {
            double ignore = Single ? Math.Min(100, Math.Max(0, d.BossIgnoreEvasion)) : 0;
            double chance = EvasionChance(d.kEvasion >= 0 ? T(d.kEvasion) : 0) * (1 - ignore / 100.0);
            double attempts = Math.Max(0, d.EvadeAttempts), evades = attempts * chance;
            foreach (var (item, seconds) in d.EvadeBarriers)
            {
                if (!ItemOn[item]) continue;
                double t = SafeAt(seconds, IdxOf(item));
                if (t > 0 && attempts > 0) evades += 1 / (t + 1 / attempts);
            }
            return Math.Min(attempts, evades);
        }
    }
}
