using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mirror;
using UnityEngine;

public partial class SephiriaToolbox
{
    const double MinWindowSeconds = 15;

    List<DpsWindow> BuildWindows(PlayerAvatar avatar, ArrModel m, DpsModel d, Dictionary<string, int> byId, out double seconds)
    {
        seconds = 0;
        var list = new List<DpsWindow>();
        if (segmentOwner != avatar.netId || !cumulative.TryGetValue(avatar.netId, out var cur)) return list;
        var itemOf = new Dictionary<int, int>();
        for (int i = 0; i < m.Items.Length; i++) itemOf[m.Items[i].InstanceId] = i;
        float now = runCombatTime;
        foreach (var seg in segments)
        {
            float end = seg.End != null ? seg.CombatEnd : now;
            double secs = end - seg.CombatStart;
            if (end < now - SegmentWindow || secs < 0.5) continue;
            if (!MapLayout(m, seg.Layout, itemOf, out var perm, out var rot)) continue;
            var w = new DpsWindow { Seconds = secs, Measured = new double[d.Sources.Length], Perm = perm, Rot = rot };
            foreach (var kv in seg.End ?? cur)
            {
                double delta = kv.Value - (seg.Start.TryGetValue(kv.Key, out var b) ? b : 0f);
                if (delta <= 0) continue;
                if (byId.TryGetValue(kv.Key.Id, out var s) && s >= 0) w.Measured[s] += delta;
                else w.Other += delta;
            }
            list.Add(w);
            seconds += secs;
        }
        return list;
    }

    static bool MapLayout(ArrModel m, Placed[] layout, Dictionary<int, int> itemOf, out int[] perm, out int[] rot)
    {
        perm = Enumerable.Repeat(-1, m.N).ToArray();
        rot = (int[])m.StartRot.Clone();
        foreach (var p in layout)
        {
            if (!itemOf.TryGetValue(p.Inst, out var i))
            {
                if (p.Key) return false;
                continue;
            }
            int slot = SlotOf(p.X, p.Y, m.W, m.N);
            if (slot < 0) return false;
            perm[slot] = i;
            var it = m.Items[i];
            if (it.IsTablet) rot[it.TabletIndex] = p.Rot;
        }
        return true;
    }
}
