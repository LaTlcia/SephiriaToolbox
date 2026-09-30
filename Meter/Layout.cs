using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    const float SegmentWindow = 240f;

    struct Placed
    {
        public int X, Y, Inst, Rot;
        public bool Key;
    }

    sealed class LayoutSegment
    {
        public float CombatStart, CombatEnd;
        public Dictionary<DamageKey, float> Start, End;
        public Placed[] Layout;
    }

    readonly List<LayoutSegment> segments = new();
    uint segmentOwner;
    int layoutHash;

    static Placed[] ReadLayout(GridInventory inv) =>
        inv.inventoryMatrix
            .Where(kv => kv.Value != null)
            .Select(kv => new Placed
            {
                X = kv.Key.x, Y = kv.Key.y, Inst = kv.Value.InstanceID,
                Rot = kv.Value.StoneTablet != null ? ((kv.Value.StoneTablet.rotation % 4) + 4) % 4 : 0,
                Key = kv.Value.Charm != null || kv.Value.StoneTablet != null
            })
            .OrderBy(p => p.Y).ThenBy(p => p.X)
            .ToArray();

    static int LayoutHash(Placed[] layout)
    {
        unchecked
        {
            int h = 17;
            foreach (var p in layout) h = h * 31 + ((p.X * 64 + p.Y) * 4 + p.Rot) * 1000003 + p.Inst;
            return h;
        }
    }

    void TrackLayout(PlayerAvatar local)
    {
        if (local == null || local.Inventory == null) return;
        if (!cumulative.TryGetValue(local.netId, out var cur)) return;
        if (segmentOwner != local.netId) { segments.Clear(); segmentOwner = local.netId; }
        var layout = ReadLayout(local.Inventory);
        int h = LayoutHash(layout);
        var open = segments.Count > 0 ? segments[segments.Count - 1] : null;
        if (open == null || h != layoutHash)
        {
            if (open != null)
            {
                open.End = new Dictionary<DamageKey, float>(cur);
                open.CombatEnd = runCombatTime;
            }
            segments.Add(new LayoutSegment { CombatStart = runCombatTime, Start = new Dictionary<DamageKey, float>(cur), Layout = layout });
            layoutHash = h;
        }
        while (segments.Count > 40 || (segments.Count > 1 && segments[0].End != null && segments[0].CombatEnd < runCombatTime - SegmentWindow))
            segments.RemoveAt(0);
    }

    void ClearSegments()
    {
        segments.Clear();
        layoutHash = 0;
    }
}
