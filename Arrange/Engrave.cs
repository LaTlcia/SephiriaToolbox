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
    Task<List<EngraveOption>> engraveTask;
    List<EngraveOption> engraveOptions;
    volatile int engraveProgress;
    string engraveMessage = "";

    sealed class EngraveOption
    {
        public int Tablet;
        public string Name;
        public int Slot, Rotation;
        public double Gain;
        public int LevelGain;
        public ArrModel Model;
        public ArrResult Result;
    }

    static List<EngraveOption> AnalyzeEngraving(ArrModel main, ArrResult baseResult, bool allowRotate, double budget, int seed, Func<int, bool> progress)
    {
        var list = new List<EngraveOption>();
        bool dps = main.Dps != null;
        double baseValue = dps ? baseResult.After.Weighted : baseResult.After.Score;
        int tabletCount = main.TabletItem.Length;
        for (int t = 0; t < tabletCount; t++)
        {
            if (!progress(t)) return null;
            if (Array.IndexOf(main.Start, main.TabletItem[t]) < 0) continue;
            var m2 = main.CloneForEngrave(t);
            var r2 = Optimize(m2, allowRotate, budget, seed + 101 * t);
            var o = new EngraveOption
            {
                Tablet = t,
                Name = main.Items[main.TabletItem[t]].Name,
                Slot = r2.Rot[tabletCount + t],
                Rotation = r2.Rot[t],
                Model = m2,
                Result = r2,
                LevelGain = r2.After.EffectiveLevels - baseResult.After.EffectiveLevels
            };
            if (dps && m2.Dps != null) o.Gain = baseValue > 0 ? r2.After.Weighted / baseValue - 1 : 0;
            else o.Gain = (r2.After.Score - baseValue) / 10000.0;
            list.Add(o);
        }
        if (!progress(tabletCount)) return null;
        return list.OrderByDescending(o => o.Gain).ToList();
    }

    bool EngraveWorthIt(EngraveOption o) => arrModel != null && arrModel.Dps != null ? o.Gain >= 0.005 : o.LevelGain >= 1;

    void StageEngraving(EngraveOption o)
    {
        if (arrState == ArrState.Computing || arrState == ArrState.Running) return;
        var main = arrModel;
        var avatar = LocalAvatar();
        if (main == null || avatar == null) { engraveMessage = Tr("arrange.only_available_during_run"); return; }
        int tabletCount = main.TabletItem.Length, tabletItem = main.TabletItem[o.Tablet];
        var stage = (int[])o.Result.Perm.Clone();
        int slot = o.Slot;
        var g = slot >= 0 ? main.TabletGeom[o.Tablet][slot][o.Rotation] : null;
        if (g == null || g.Blocked) { engraveMessage = Tr("arrange.this_tablet_cant_be"); return; }
        var condCells = new HashSet<int>(g.Conds.Select(c => c.Slot));
        if (condCells.Contains(-1)) { engraveMessage = Tr("arrange.part_this_tablet_condition"); return; }
        if (stage[slot] >= 0)
        {
            int empty = Enumerable.Range(0, main.N).Where(e => e != slot && stage[e] < 0).OrderBy(e => condCells.Contains(e) ? 1 : 0).DefaultIfEmpty(-1).First();
            if (empty < 0) { engraveMessage = Tr("arrange.no_free_slot_inventory"); return; }
            stage[empty] = stage[slot];
        }
        stage[slot] = tabletItem;
        foreach (var c in g.Conds)
        {
            bool Suits(int item) => item >= 0 && (c.Type == CondAnyItem || main.Items[item].Kind >= KindCharm);
            if (Suits(stage[c.Slot])) continue;
            int donor = Enumerable.Range(0, main.N).Where(d => d != slot && !condCells.Contains(d) && Suits(stage[d])).DefaultIfEmpty(-1).First();
            if (donor < 0) { engraveMessage = Tr("arrange.not_enough_items_inventory"); return; }
            (stage[c.Slot], stage[donor]) = (stage[donor], stage[c.Slot]);
        }
        var rot = (int[])o.Result.Rot.Clone();
        rot[o.Tablet] = o.Rotation;
        for (int t = 0; t < tabletCount; t++) rot[tabletCount + t] = -1;

        ArrModel fresh;
        try { fresh = BuildModel(avatar, true, out _, out var error); if (fresh == null) { engraveMessage = error; return; } }
        catch (Exception e) { WarnOnce("刻印摆放建模", e); engraveMessage = Tr("arrange.read_inventory_failed", e.Message); return; }
        var byInstance = new Dictionary<int, int>();
        for (int i = 0; i < fresh.Items.Length; i++) byInstance[fresh.Items[i].InstanceId] = i;
        var perm = Enumerable.Repeat(-1, fresh.N).ToArray();
        var used = new HashSet<int>();
        for (int s2 = 0; s2 < main.N && s2 < fresh.N; s2++)
        {
            if (stage[s2] < 0) continue;
            if (!byInstance.TryGetValue(main.Items[stage[s2]].InstanceId, out var fi) || !used.Add(fi)) { engraveMessage = Tr("arrange.inventory_changed_since_calculation"); return; }
            perm[s2] = fi;
        }
        if (used.Count != fresh.Items.Length || main.N != fresh.N) { engraveMessage = Tr("arrange.inventory_changed_since_calculation"); return; }
        int freshTablets = fresh.TabletItem.Length;
        var freshRot = (int[])fresh.StartRot.Clone();
        for (int t = 0; t < freshTablets; t++)
        {
            int inst = fresh.Items[fresh.TabletItem[t]].InstanceId;
            int mt = Array.FindIndex(main.TabletItem, ti => main.Items[ti].InstanceId == inst);
            if (mt >= 0) freshRot[t] = rot[mt];
        }
        var res = new ArrResult { Perm = perm, Rot = freshRot };
        engraveMessage = "";
        arrMessage = Tr("arrange.moving_imprint_position_will", o.Name);
        StartCoroutine(RunArrange(fresh, res, main.Items[tabletItem].InstanceId, o.Name));
    }
}
