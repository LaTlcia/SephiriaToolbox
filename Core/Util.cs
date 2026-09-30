using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Mirror;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

public partial class SephiriaToolbox
{
    void WarnOnce(string what, Exception e)
    {
        if (warned.Add(what + ":" + e.GetType().Name + ":" + e.Message)) Debug.LogWarning(ResolveZh($"[SephiriaToolbox] {what}: {e}"));
    }

    PlayerAvatar LocalAvatar()
    {
        var players = PlayerSpawner.MultiplayerList;
        if (players == null) return null;
        foreach (var s in players)
            if (s != null && s.PlayerAvatar != null && (s.isLocalPlayer || s.PlayerAvatar.isLocalPlayer)) return s.PlayerAvatar;
        return null;
    }

    static T SafeAt<T>(T[] a, int i) => a == null || a.Length == 0 ? default : a[Math.Min(Math.Max(i, 0), a.Length - 1)];

    static float[] ReadFloats(object o, string field)
    {
        var f = o.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return f?.GetValue(o) switch
        {
            float[] fa => fa,
            int[] ia => ia.Select(x => (float)x).ToArray(),
            _ => null
        };
    }

    static string ReadString(object o, string field) =>
        o.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(o) as string;

    static object FieldValue(object o, string name) =>
        o == null || string.IsNullOrEmpty(name) ? null : FindField(o.GetType(), name)?.GetValue(o);

    static FieldInfo FindField(Type t, string name)
    {
        for (; t != null && t != typeof(object); t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (f != null) return f;
        }
        return null;
    }

    static float[] LevelTable(object o, string name)
    {
        switch (FieldValue(o, name))
        {
            case float[] fa when fa.Length > 0: return fa;
            case int[] ia when ia.Length > 0: return ia.Select(x => (float)x).ToArray();
        }
        return null;
    }

    static float TimerSeconds(object o, string name) => FieldValue(o, name) is Timer t ? t.time : 0f;

    static void MulLevels(float[] rate, float[] table, Func<float, float> f)
    {
        if (table == null) return;
        for (int l = 0; l < rate.Length; l++) rate[l] *= f(SafeAt(table, l));
    }

    static double Num(object o, string field) => FieldValue(o, field) switch
    {
        int i => i,
        float f => f,
        short sh => sh,
        sbyte sb => sb,
        byte by => by,
        double db => db,
        bool b => b ? 1 : 0,
        _ => 0
    };

    static int ElemIndex(EDamageElementalType e) => e switch
    {
        EDamageElementalType.Physical => 0,
        EDamageElementalType.Fire => 1,
        EDamageElementalType.Ice => 2,
        EDamageElementalType.Lightning => 3,
        _ => -1
    };

    static float[] Per(int levels, Func<int, double> f) => Enumerable.Range(0, levels).Select(l => (float)f(l)).ToArray();

    static double CritFactor(double chancePct, double ratePct, bool execution)
    {
        double r = ratePct / 100.0;
        if (execution)
        {
            double pe = Math.Min(Math.Max(chancePct - 100, 0), 100) / 100.0;
            double pc = Math.Min(Math.Max(chancePct, 0), 100) / 100.0;
            return pe * (1 + 2 * r) + (1 - pe) * (1 + pc * r);
        }
        return 1 + Math.Min(Math.Max(chancePct, 0), 100) / 100.0 * r;
    }

    static double Pct(double v) => Math.Max(0, 1 + v / 100.0);

    static double ElemFor(string formula, EDamageElementalType element, double[] e)
    {
        if (string.IsNullOrEmpty(formula))
        {
            return element switch
            {
                EDamageElementalType.Physical => e[0],
                EDamageElementalType.Fire => e[1],
                EDamageElementalType.Ice => e[2],
                EDamageElementalType.Lightning => e[3],
                _ => 1
            };
        }
        switch (formula)
        {
            case "PHYSICALDAMAGE": return e[0];
            case "FIREDAMAGE": return e[1];
            case "ICEDAMAGE": return e[2];
            case "LIGHTNINGDAMAGE": return e[3];
        }
        var parts = formula.Split('/');
        switch (parts[0])
        {
            case "HIGHEST":
            case "CHAOS_HIGHEST": return Math.Max(Math.Max(e[0], e[1]), Math.Max(e[2], e[3]));
            case "CHAOS_PHYSICAL": return e[0];
            case "CHAOS_FIRE": return e[1];
            case "CHAOS_ICE": return e[2];
            case "CHAOS_LIGHTNING": return e[3];
            case "LOWEST": return Math.Min(Math.Min(e[0], e[1]), Math.Min(e[2], e[3]));
            case "AVERAGEALL": return Math.Truncate((e[0] + e[1] + e[2] + e[3]) / 4);
            case "AVERAGE" when parts.Length > 1:
            {
                var vals = parts[1].Split(',').Select(k => Array.IndexOf(ElemKeys, k)).Where(i => i >= 0).Select(i => e[i]).ToList();
                return vals.Count > 0 ? vals.Average() : 0;
            }
        }
        return 1;
    }
}
