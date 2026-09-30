using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static double DebuffAvgStacks(double r, double dur, double max, double add)
    {
        if (r <= 0 || dur <= 0 || max <= 0) return 0;
        double x = Math.Min(r * dur, 30);
        double up = 1 - Math.Exp(-x);
        double run = Math.Exp(x);
        double k = Math.Max(1, max / Math.Max(1, add));
        double avg = run <= k ? (run + 1) / 2 : k - k * (k - 1) / (2 * run);
        return up * Math.Min(max, Math.Max(1, avg) * Math.Max(1, add));
    }

    static void CycleStacks(double r, double T, double M, double a, out double final, out double integral, out double cycle)
    {
        final = integral = 0;
        cycle = double.PositiveInfinity;
        if (r <= 0 || T <= 0 || M <= 0) return;
        a = Math.Max(1, a);
        cycle = T + 1 / r;
        double lam = Math.Min(r * T, 600);
        int cap = Math.Max(0, (int)Math.Ceiling(M / a - 1 - 1e-9));
        double p = Math.Exp(-lam), cdf = 0, used = 0;
        for (int n = 0; n < cap; n++)
        {
            double sn = a * (1 + n);
            final += p * sn;
            cdf += p;
            double at = Math.Max(0, 1 - cdf) / r;
            integral += sn * at;
            used += at;
            p *= lam / (n + 1);
        }
        final += Math.Max(0, 1 - cdf) * M;
        integral += M * Math.Max(0, T - used);
    }

    static double InvertStacks(double stacks, double dur, double max, double add)
    {
        if (stacks <= 0) return 0;
        double lo = 0, hi = 50;
        for (int i = 0; i < 60; i++)
        {
            double mid = (lo + hi) / 2;
            if (DebuffAvgStacks(mid, dur, max, add) < stacks) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }

    static double InvertCycleStacks(double stacks, double dur, double max, double add)
    {
        if (stacks <= 0 || dur <= 0) return 0;
        double lo = 1e-4, hi = 100;
        for (int i = 0; i < 60; i++)
        {
            double mid = Math.Sqrt(lo * hi);
            CycleStacks(mid, dur, max, add, out _, out var integral, out _);
            if (integral / dur < stacks) lo = mid; else hi = mid;
        }
        return Math.Sqrt(lo * hi);
    }
}
