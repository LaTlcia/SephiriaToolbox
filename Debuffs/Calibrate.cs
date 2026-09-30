using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed partial class Evaluator
    {
        public void CalibrateDebuffs()
        {
            if (d.Eco == null) return;
            Stats();
            UpdateMagicRates();
            foreach (var di in d.Debuffs)
            {
                di.Kappa = 1;
                di.Residual = 0;
            }
            foreach (var di in d.Debuffs)
            {
                if (di.Stacks <= 0 || di.Targets <= 0) continue;
                if (di.Type == DebuffType.Frostbite) continue;
                double model = DebuffRate(di, di.Targets, false);
                DebuffShape(di, out var max, out var add, out var dur, out _);
                double stacks = Math.Min(di.Stacks, max * 0.999);
                double real = di.Renew ? InvertStacks(stacks, dur, max, add) : InvertCycleStacks(stacks, dur, max, add);
                if (real <= 0) continue;
                if (real > model) di.Residual = real - model;
                else if (model > 1e-6) di.Kappa = Math.Max(0.2, real / model);
            }
        }
    }
}
