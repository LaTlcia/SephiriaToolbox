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
    const double PriorSeconds = 30;

    static void Calibrate(ArrModel m, Evaluator ev)
    {
        var d = m.Dps;
        int n = d.Sources.Length;
        var f0 = new double[n];
        var P = new double[n];
        var M = new double[n];
        var tmp = new double[n];
        ev.Levels(m.Start, m.StartRot);
        ev.RawAll(f0);
        double other = 0, seconds = 0;
        if (d.Windows != null)
            foreach (var w in d.Windows)
            {
                ev.Levels(w.Perm ?? m.Start, w.Rot ?? m.StartRot);
                ev.RawAll(tmp);
                for (int i = 0; i < n; i++) { P[i] += tmp[i] * w.Seconds; M[i] += w.Measured[i]; }
                other += w.Other;
                seconds += w.Seconds;
            }
        static double Kth(DpsSource s) => s.TheoryK > 0 ? s.TheoryK : s.Prior;
        static double KthM(DpsSource s) => Kth(s) * s.MultiScale;
        double rho = 1;
        int basicIdx = Array.FindIndex(d.Sources, x => x.Kind == SrcKind.WeaponBasic);
        if (basicIdx >= 0 && M[basicIdx] > 0 && P[basicIdx] > 0 && KthM(d.Sources[basicIdx]) > 0)
            rho = M[basicIdx] / (KthM(d.Sources[basicIdx]) * P[basicIdx]);
        else
        {
            var ratios = new List<double>();
            for (int i = 0; i < n; i++)
                if (d.Sources[i].HasTheory && M[i] > 0 && P[i] > 0 && KthM(d.Sources[i]) > 0) ratios.Add(M[i] / (KthM(d.Sources[i]) * P[i]));
            if (ratios.Count > 0)
            {
                ratios.Sort();
                rho = ratios.Count % 2 == 1 ? ratios[ratios.Count / 2] : Math.Sqrt(ratios[ratios.Count / 2 - 1] * ratios[ratios.Count / 2]);
            }
        }
        rho = Math.Min(Math.Max(rho, 0.05), 20);
        d.Realization = rho;
        var fe = f0;
        if (d.Single && !ev.Single)
        {
            fe = new double[n];
            ev.Single = true;
            ev.Levels(m.Start, m.StartRot);
            ev.RawAll(fe);
            ev.Single = false;
        }
        for (int i = 0; i < n; i++)
        {
            var s = d.Sources[i];
            double kth = Kth(s), kthM = KthM(s), scale = Math.Max(1e-9, s.MultiScale);
            double measured = M[i] > 0 && P[i] > 0 ? M[i] / P[i] / rho : -1;
            if (d.UseMeasured)
            {
                double prior = PriorSeconds * f0[i];
                double kM = P[i] + prior > 0 ? (M[i] / rho + prior * kthM) / (P[i] + prior) : kthM;
                s.K = kM / scale;
                s.Basis = (byte)(measured >= 0 && P[i] >= prior ? 1 : s.HasTheory ? 0 : 2);
            }
            else if (s.HasTheory) { s.K = kth; s.Basis = 0; }
            else if (measured >= 0) { s.K = measured / scale; s.Basis = 1; }
            else { s.K = kth; s.Basis = 2; }
            s.Estimated = s.Basis == 2;
            s.Measured = seconds > 0 ? M[i] / seconds : 0;
            s.TheoryShare = kth * fe[i];
        }
        d.Other = seconds > 0 && !d.NoOther ? other / seconds / rho : 0;
    }
}
