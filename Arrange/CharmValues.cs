using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed class CharmValue
    {
        public int Item;
        public int Level, MaxLevel;
        public bool On;
        public double PlusOne = double.NaN;
        public double Remove;
    }

    static List<CharmValue> ComputeCharmValues(ArrModel m, int[] perm, int[] rot)
    {
        if (m.Dps == null || perm == null || rot == null) return null;
        var ev = new Evaluator(m);
        ev.Levels(perm, rot);
        double baseDps = ev.Dps();
        if (!(baseDps > 0)) return null;
        var slotOf = Enumerable.Repeat(-1, m.Items.Length).ToArray();
        for (int s = 0; s < perm.Length; s++) if (perm[s] >= 0) slotOf[perm[s]] = s;
        var list = new List<CharmValue>();
        for (int i = 0; i < m.Items.Length; i++)
        {
            var it = m.Items[i];
            if (it.IsTablet || (it.Kind != KindCharm && it.Kind != KindMagic) || slotOf[i] < 0) continue;
            ev.Levels(perm, rot);
            var v = new CharmValue { Item = i, Level = ev.ItemLevel[i], MaxLevel = it.MaxLevel, On = ev.ItemOn[i] };
            if (v.On && v.Level < it.MaxLevel)
            {
                ev.ItemLevel[i]++;
                v.PlusOne = ev.Dps() / baseDps - 1;
            }
            var without = (int[])perm.Clone();
            without[slotOf[i]] = -1;
            ev.Levels(without, rot);
            v.Remove = ev.Dps() / baseDps - 1;
            list.Add(v);
        }
        return list.OrderByDescending(v => double.IsNaN(v.PlusOne) ? double.NegativeInfinity : v.PlusOne)
                   .ThenBy(v => v.Remove).ToList();
    }

    void DrawCharmValues(ArrModel m, ArrResult r)
    {
        if (r.Values == null || r.Values.Count == 0) return;
        GUILayout.Label(Tr("values.artifact_value_under_suggested"), wrap);
        GUILayout.BeginHorizontal();
        GUILayout.Space(Indent);
        GUILayout.Label(Tr("values.artifact"), dim, GUILayout.Width(200));
        GUILayout.Label(Tr("values.level"), dim, GUILayout.Width(60));
        GUILayout.Label(Tr("values.1_lv"), dim, GUILayout.Width(70));
        GUILayout.Label(Tr("values.remove"), dim, GUILayout.Width(70));
        GUILayout.EndHorizontal();
        foreach (var v in r.Values)
        {
            var it = m.Items[v.Item];
            GUILayout.BeginHorizontal();
            GUILayout.Space(Indent);
            GUILayout.Label(v.On ? it.Name : Tr("values.inactive", it.Name), rich, GUILayout.Width(200));
            GUILayout.Label(v.Level >= it.MaxLevel ? Tr("values.lv_max", v.Level, it.MaxLevel) : $"Lv{v.Level}/{it.MaxLevel}", GUILayout.Width(60));
            string plus = double.IsNaN(v.PlusOne) ? "-" : Signed(v.PlusOne);
            GUILayout.Label(plus, rightAlign, GUILayout.Width(70));
            string color = v.Remove < -0.0005 ? "FF7070" : v.Remove > 0.0005 ? "7CFC00" : "BBBBBB";
            GUILayout.Label($"<color=#{color}>{Signed(v.Remove)}</color>", rich, GUILayout.Width(70));
            GUILayout.EndHorizontal();
        }
    }
}
