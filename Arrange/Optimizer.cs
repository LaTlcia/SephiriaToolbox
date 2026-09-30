using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

public partial class SephiriaToolbox
{
    static int MovedCount(int[] perm, int[] start)
    {
        int c = 0;
        for (int s = 0; s < perm.Length; s++)
            if (perm[s] >= 0 && perm[s] != start[s]) c++;
        return c;
    }

    static double Objective(ArrModel m, Evaluator ev, int[] perm, int[] rot)
    {
        ev.Levels(perm, rot);
        double s;
        int moved = MovedCount(perm, m.Start), turned = 0;
        for (int t = 0; t < m.TabletItem.Length; t++) if (rot[t] != m.StartRot[t]) turned++;
        int broken = BrokenConstraints(m, ev, perm);
        if (m.Dps == null)
            return ev.LevelScore - 0.5 * moved - 0.25 * turned - HardPenalty * broken;
        s = 10000.0 * ev.Dps() / m.Norm + 2.0 * ev.SumClamp + 3.0 * ev.CompanionsInBadgeRow - HardPenalty * broken;
        if (m.Protect)
            for (int i = 0; i < m.Items.Length; i++)
                if (m.StartOn[i] && !ev.ItemOn[i]) s -= 300;
        return s - 0.05 * moved - 0.02 * turned;
    }

    const double HardPenalty = 1e8;
    const double SeedTempFactor = 0.1;

    static int BrokenConstraints(ArrModel m, Evaluator ev, int[] perm)
    {
        int broken = 0;
        for (int b = m.N; b < perm.Length; b++)
            if (perm[b] >= 0 && perm[b] != m.NewItem)
            {
                for (int s = 0; s < m.N; s++)
                    if (perm[s] < 0) { broken++; break; }
            }
        if (m.KeepSideItem != null)
            for (int k = 0; k < m.KeepSideItem.Length; k++)
            {
                int slot = ev.ItemSlot[m.KeepSideItem[k]];
                if (slot >= 0 && (slot % m.W <= 2) != m.KeepSideLeft[k]) broken++;
            }
        if (m.Patterns != null)
            foreach (var p in m.Patterns)
                if (!PatternPresent(m, perm, p)) broken++;
        return broken;
    }

    static bool PatternPresent(ArrModel m, int[] perm, int[] p)
    {
        for (int s = 0; s < m.N; s++)
        {
            int ox = s % m.W, oy = s / m.W;
            bool ok = true;
            for (int k = 0; k + 2 < p.Length && ok; k += 3)
            {
                int slot = SlotOf(ox + p[k], oy + p[k + 1], m.W, m.N);
                ok = slot >= 0 && perm[slot] >= 0 && m.Items[perm[slot]].EntityId == p[k + 2];
            }
            if (ok) return true;
        }
        return false;
    }

    static void PrepareModel(ArrModel m)
    {
        BuildGeoms(m);
        var ev0 = new Evaluator(m);
        ev0.Levels(m.Start, m.StartRot);
        m.StartOn ??= (bool[])ev0.ItemOn.Clone();
        if (m.Dps != null && !m.Dps.Calibrated)
        {
            var dyn = ev0.DynamicCatCounts();
            m.Dps.CatStatic = new int[m.Dps.CatActual.Length];
            for (int c = 0; c < dyn.Length && c < m.Dps.CatStatic.Length; c++) m.Dps.CatStatic[c] = m.Dps.CatActual[c] - dyn[c];
            ev0.Levels(m.Start, m.StartRot);
            ev0.SubtractStartSpecials();
            ev0.Levels(m.Start, m.StartRot);
            ev0.ComputeSlopeBases();
            ev0.CalibrateWeapon();
            ev0.CalibrateSweep();
            ev0.Single = false;
            ev0.CalibrateDebuffs();
            m.Dps.Calibrated = true;
            Calibrate(m, ev0);
            ev0.Single = m.Dps.Single;
        }
        if (m.Dps != null)
        {
            ev0.Levels(m.Start, m.StartRot);
            m.Norm = ev0.Dps();
            if (m.Norm <= 1e-9) m.Dps = null;
        }
    }

    static ArrResult Optimize(ArrModel m, bool allowRotate, double budgetSeconds, int seed, int[] seedPerm = null, int[] seedRot = null, int maxChains = 0)
    {
        var clock = Stopwatch.StartNew();
        PrepareModel(m);
        bool dpsGoal = m.Dps != null;
        double T0 = dpsGoal ? 300 : 15000, T1 = dpsGoal ? 0.3 : 5;

        var slots = Enumerable.Range(0, m.N + m.Bench).Where(s => m.Movable[s]).ToArray();
        var rotTablets = Enumerable.Range(0, m.TabletItem.Length)
            .Where(t => m.Items[m.TabletItem[t]].Rotatable && (allowRotate || t == m.FreeRotTablet)).ToArray();
        var rotatableItem = new bool[m.Items.Length];
        foreach (var t in rotTablets) rotatableItem[m.TabletItem[t]] = true;
        int tabletCount = m.TabletItem.Length;
        var stamps = Enumerable.Range(0, tabletCount).Where(t => m.StartRot.Length >= 2 * tabletCount && m.StartRot[tabletCount + t] >= 0).ToArray();
        var stampRotatable = stamps.Select(t => allowRotate && m.Items[m.TabletItem[t]].Rotatable).ToArray();

        long evals = 0;
        int chains = maxChains > 0 ? maxChains : Math.Max(1, Math.Min(4, Environment.ProcessorCount / 2));
        var bestPerms = new int[chains][];
        var bestRots = new int[chains][];
        var bestScores = new double[chains];
        double budget = Math.Max(0.2, budgetSeconds - clock.Elapsed.TotalSeconds);

        Parallel.For(0, chains, c =>
        {
            var ev = new Evaluator(m);
            var rng = new System.Random(seed + c * 7919);
            bool fromSeed = c == 0 && seedPerm != null && seedRot != null;
            var perm = (int[])(fromSeed ? seedPerm : m.Start).Clone();
            var rot = (int[])(fromSeed ? seedRot : m.StartRot).Clone();
            if (c >= 2)
            {
                for (int i = slots.Length - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (perm[slots[i]], perm[slots[j]]) = (perm[slots[j]], perm[slots[i]]);
                }
                foreach (var t in rotTablets) rot[t] = rng.Next(4);
                foreach (var t in stamps) rot[tabletCount + t] = rng.Next(m.N);
            }
            double cur = Objective(m, ev, perm, rot);
            var bp = (int[])perm.Clone();
            var br = (int[])rot.Clone();
            double best = cur;
            long local = 1;
            var sw = Stopwatch.StartNew();
            double t0 = fromSeed ? T0 * SeedTempFactor : T0;
            double temp = t0;
            int step = 0;
            if (slots.Length >= 2)
            {
                while (true)
                {
                    if ((++step & 255) == 0)
                    {
                        double frac = sw.Elapsed.TotalSeconds / budget;
                        if (frac >= 1) break;
                        temp = t0 * Math.Pow(T1 / t0, frac);
                    }
                    double r = rng.NextDouble();
                    if (stamps.Length > 0 && r < 0.2)
                    {
                        int k = rng.Next(stamps.Length), t = stamps[k];
                        int oldSlot = rot[tabletCount + t], oldRot = rot[t];
                        if (rng.NextDouble() < 0.7 || !stampRotatable[k]) rot[tabletCount + t] = rng.Next(m.N);
                        if (stampRotatable[k] && rng.NextDouble() < 0.5) rot[t] = rng.Next(4);
                        double sc = Objective(m, ev, perm, rot); local++;
                        if (sc >= cur || rng.NextDouble() < Math.Exp((sc - cur) / temp)) cur = sc;
                        else { rot[tabletCount + t] = oldSlot; rot[t] = oldRot; }
                    }
                    else if (rotTablets.Length > 0 && r < 0.3)
                    {
                        int t = rotTablets[rng.Next(rotTablets.Length)];
                        int old = rot[t];
                        rot[t] = (old + 1 + rng.Next(3)) % 4;
                        double sc = Objective(m, ev, perm, rot); local++;
                        if (sc >= cur || rng.NextDouble() < Math.Exp((sc - cur) / temp)) cur = sc;
                        else rot[t] = old;
                    }
                    else
                    {
                        int a = slots[rng.Next(slots.Length)], b = slots[rng.Next(slots.Length)];
                        if (a == b || (perm[a] < 0 && perm[b] < 0)) continue;
                        (perm[a], perm[b]) = (perm[b], perm[a]);
                        int turnT = -1, oldRot = 0;
                        int moved = perm[a] >= 0 && rotatableItem[perm[a]] ? perm[a] : perm[b] >= 0 && rotatableItem[perm[b]] ? perm[b] : -1;
                        if (moved >= 0 && rng.NextDouble() < 0.5)
                        {
                            turnT = m.Items[moved].TabletIndex;
                            oldRot = rot[turnT];
                            rot[turnT] = rng.Next(4);
                        }
                        double sc = Objective(m, ev, perm, rot); local++;
                        if (sc >= cur || rng.NextDouble() < Math.Exp((sc - cur) / temp)) cur = sc;
                        else
                        {
                            (perm[a], perm[b]) = (perm[b], perm[a]);
                            if (turnT >= 0) rot[turnT] = oldRot;
                        }
                    }
                    if (cur > best + 1e-9)
                    {
                        best = cur;
                        Array.Copy(perm, bp, perm.Length);
                        Array.Copy(rot, br, rot.Length);
                    }
                }
            }
            bestPerms[c] = bp;
            bestRots[c] = br;
            bestScores[c] = best;
            Interlocked.Add(ref evals, local);
        });

        int win = Array.IndexOf(bestScores, bestScores.Max());
        var bestPerm = bestPerms[win];
        var bestRot = bestRots[win];
        var evp = new Evaluator(m);
        double bestScore = Objective(m, evp, bestPerm, bestRot);

        bool improved = true;
        while (improved && slots.Length >= 2)
        {
            improved = false;
            for (int i = 0; i < slots.Length; i++)
                for (int j = i + 1; j < slots.Length; j++)
                {
                    int a = slots[i], b = slots[j];
                    if (bestPerm[a] < 0 && bestPerm[b] < 0) continue;
                    (bestPerm[a], bestPerm[b]) = (bestPerm[b], bestPerm[a]);
                    double sc = Objective(m, evp, bestPerm, bestRot); evals++;
                    if (sc > bestScore + 1e-9) { bestScore = sc; improved = true; }
                    else (bestPerm[a], bestPerm[b]) = (bestPerm[b], bestPerm[a]);
                }
            foreach (var t in rotTablets)
                for (int r = 0; r < 4; r++)
                {
                    int old = bestRot[t];
                    if (r == old) continue;
                    bestRot[t] = r;
                    double sc = Objective(m, evp, bestPerm, bestRot); evals++;
                    if (sc > bestScore + 1e-9) { bestScore = sc; improved = true; }
                    else bestRot[t] = old;
                }
            for (int k = 0; k < stamps.Length; k++)
            {
                int t = stamps[k];
                for (int slot = 0; slot < m.N; slot++)
                    for (int r = 0; r < 4; r++)
                    {
                        if (!stampRotatable[k] && r != bestRot[t]) continue;
                        int oldSlot = bestRot[tabletCount + t], oldRot = bestRot[t];
                        if (slot == oldSlot && r == oldRot) continue;
                        bestRot[tabletCount + t] = slot;
                        bestRot[t] = r;
                        double sc = Objective(m, evp, bestPerm, bestRot); evals++;
                        if (sc > bestScore + 1e-9) { bestScore = sc; improved = true; }
                        else { bestRot[tabletCount + t] = oldSlot; bestRot[t] = oldRot; }
                    }
            }
        }

        var result = new ArrResult
        {
            Perm = bestPerm,
            Rot = bestRot,
            Before = evp.Detail(m.Start, m.StartRot),
            After = evp.Detail(bestPerm, bestRot),
            Evaluations = evals,
            Chains = chains,
            Seconds = clock.Elapsed.TotalSeconds
        };
        if (Objective(m, evp, m.Start, m.StartRot) >= bestScore - 1e-9)
        {
            result.Perm = (int[])m.Start.Clone();
            result.Rot = (int[])m.StartRot.Clone();
            result.After = result.Before;
        }
        if (m.Bench == 0)
        {
            var plan = PlanSteps(m, result.Perm, result.Rot);
            result.Swaps = plan.Count(p => p.Swap);
            result.Rotations = plan.Count(p => !p.Swap);
        }
        return result;
    }
}
