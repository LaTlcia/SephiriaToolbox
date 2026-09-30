using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mirror;
using UnityEngine;

public partial class SephiriaToolbox
{
    static GameObject probeObject;
    static UnitAvatar probeAvatar;
    static readonly FieldInfo SyncObjectsField = typeof(SyncIDictionary<string, int>).GetField("objects", BindingFlags.Instance | BindingFlags.NonPublic);

    static IDictionary<string, int> Backing(SyncDictionary<string, int> dict) => (IDictionary<string, int>)SyncObjectsField.GetValue(dict);

    static UnitAvatar Probe(UnitAvatar real)
    {
        if (SyncObjectsField == null) return null;
        if (probeAvatar == null)
        {
            probeObject = new GameObject("SephiriaToolbox.StatProbe") { hideFlags = HideFlags.HideAndDontSave };
            probeObject.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(probeObject);
            probeAvatar = probeObject.AddComponent<UnitAvatar>();
        }
        void Copy(SyncDictionary<string, int> from, SyncDictionary<string, int> to)
        {
            var t = Backing(to);
            t.Clear();
            foreach (var kv in from) t[kv.Key] = kv.Value;
        }
        Copy(real.customStats, probeAvatar.customStats);
        Copy(real.calculatedBonusStats, probeAvatar.calculatedBonusStats);
        Copy(real.customStatsAmp, probeAvatar.customStatsAmp);
        probeAvatar.highestElementalBonus = real.highestElementalBonus;
        return probeAvatar;
    }

    static double SlopeOf(UnitAvatar p, string key, Func<UnitAvatar, float> g, float g0) => Measure(p, key, g, g0, out _, out _);

    static double Measure(UnitAvatar p, string key, Func<UnitAvatar, float> g, float g0, out bool step, out bool baseOn)
    {
        step = false;
        baseOn = false;
        var raw = Backing(p.customStats);
        bool had = raw.TryGetValue(key, out var old);
        int bonus = p.calculatedBonusStats.TryGetValue(key, out var b) ? b : 0;
        int delta = Math.Max(10, Math.Abs(old + bonus) / 10);
        int f0 = p.GetCustomStatUnsafe(key);
        try
        {
            if (f0 <= 0)
            {
                raw[key] = old + (1 - f0);
                int f1 = p.GetCustomStatUnsafe(key);
                double g1 = g(p);
                raw[key] = old + (1 - f0) + delta;
                int f2 = p.GetCustomStatUnsafe(key);
                double g2 = g(p);
                if (f1 > 0 && f2 > f1 && Math.Abs(g1 - g0) > 1e-4 * Math.Max(1, Math.Abs(g0)) && Math.Abs(g2 - g1) <= 0.02 * Math.Abs(g1 - g0))
                {
                    step = true;
                    return double.IsNaN(g1 - g0) ? 0 : g1 - g0;
                }
                if (f2 == f0) return 0;
                double r = (g2 - g0) / (f2 - f0);
                return double.IsNaN(r) || double.IsInfinity(r) ? 0 : r;
            }
            else
            {
                raw[key] = old - f0;
                double gOff = g(p);
                int fOff = p.GetCustomStatUnsafe(key);
                raw[key] = old + delta;
                int f1 = p.GetCustomStatUnsafe(key);
                double gUp = g(p);
                if (fOff <= 0 && Math.Abs(g0 - gOff) > 1e-4 * Math.Max(1, Math.Abs(g0)) && Math.Abs(gUp - g0) <= 0.02 * Math.Abs(g0 - gOff))
                {
                    step = true;
                    baseOn = true;
                    return double.IsNaN(g0 - gOff) ? 0 : g0 - gOff;
                }
                if (f1 == f0) return 0;
                double r = (gUp - g0) / (f1 - f0);
                return double.IsNaN(r) || double.IsInfinity(r) ? 0 : r;
            }
        }
        finally
        {
            if (had) raw[key] = old;
            else raw.Remove(key);
        }
    }

    static bool SameDamage(float a, float b) => !float.IsNaN(b) && Math.Abs(a - b) <= 1e-3f * Math.Max(1f, Math.Abs(a));

    static bool ProbeCharm(DpsSource s, Charm_Basic c, IAttackableCharm ac, UnitAvatar real, UnitAvatar probe, List<(int key, string name, int elem)> keys)
    {
        int saved = c.limitedEffectEnabledLevel;
        float G(UnitAvatar a) => ac.GetDamage(a);
        try
        {
            if (!SameDamage(G(real), G(probe))) return false;
            int levels = s.Table.Length;
            var relevant = new List<(int key, string name, int elem)>();
            foreach (int l in new[] { levels - 1, Math.Min(Math.Max(saved, 0), levels - 1) }.Distinct())
            {
                c.limitedEffectEnabledLevel = l;
                float g0 = G(probe);
                foreach (var k in keys)
                    if (!relevant.Contains(k) && Math.Abs(SlopeOf(probe, k.name, G, g0)) > 1e-7) relevant.Add(k);
            }
            var slopes = new List<Slope>();
            foreach (var k in relevant)
            {
                var per = new float[levels];
                bool step = false, baseOn = false;
                for (int l = 0; l < levels; l++)
                {
                    c.limitedEffectEnabledLevel = l;
                    per[l] = (float)Measure(probe, k.name, G, G(probe), out var st, out var on);
                    step |= st;
                    baseOn |= on;
                }
                slopes.Add(new Slope { Key = k.key, Elem = k.elem, PerLevel = per, Step = step, BaseOn = baseOn });
            }
            if (slopes.Any(sl => !sl.Step && sl.PerLevel.Any(v => v < -1e-6f))) return false;
            s.Slopes = slopes.ToArray();
            return true;
        }
        finally { c.limitedEffectEnabledLevel = saved; }
    }

    static bool ProbeFunction(DpsSource s, Func<UnitAvatar, float> g, UnitAvatar real, UnitAvatar probe, List<(int key, string name, int elem)> keys)
    {
        float g0 = g(probe);
        if (!SameDamage(g(real), g0)) return false;
        s.Table = new[] { g0 };
        var slopes = new List<Slope>();
        foreach (var k in keys)
        {
            double sl = Measure(probe, k.name, g, g0, out var step, out var on);
            if (Math.Abs(sl) > 1e-7) slopes.Add(new Slope { Key = k.key, Elem = k.elem, PerLevel = new[] { (float)sl }, Step = step, BaseOn = on });
        }
        if (slopes.Any(x => !x.Step && x.PerLevel[0] < -1e-6f)) return false;
        s.Slopes = slopes.ToArray();
        return true;
    }
}
