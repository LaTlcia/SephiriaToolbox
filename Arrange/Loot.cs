using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public partial class SephiriaToolbox
{
    const double LootBaseBudget = 3, LootCharmBudget = 1, LootTabletBudget = 2.5;

    sealed class LootOption
    {
        public int EntityId, InstanceId, Rarity;
        public string Name;
        public bool IsTablet, Merge, Rotatable;
        public int MergeInstance, MergeLevels, MergeCats;
        public string Note;
        public ArrModel Model;
        public volatile bool Done;
        public string Error;
        public Exception Failure;
        public bool Dps;
        public double Gain, TotalGain;
        public int LevelGain;
        public bool Taken;
        public int Slot = -1, Rotation;
        public string Drop;
        public bool Rearrange;
        public int W;
    }

    List<LootOption> lootOptions;
    ArrModel lootBase;
    Sephirite lootSephirite;
    string lootKey, lootMessage = "";
    int lootBuildNext = -2;
    int lootGeneration, lootTotal, lootRestarts;
    int lootProgress;
    volatile bool lootStale;
    Task lootTask;
    Stopwatch lootClock;
    double lootSeconds;
    bool lootOpen, lootHidden;
    float nextLootCheck;

    static Sephirite OpenSephirite()
    {
        try
        {
            var ui = UIManager.Instance;
            var panel = ui != null ? ui.GetElement<UI_SephiriteRewardPanel>() : null;
            var sep = panel != null && panel.IsOpened ? panel.sephirite : null;
            return sep != null && sep.isGenerated && sep.Rewards.Count > 0 ? sep : null;
        }
        catch { return null; }
    }

    void UpdateLoot()
    {
        if (lootBuildNext > -2) { BuildNextLootModel(); return; }
        if (lootTask != null && lootTask.IsCompleted) FinishLootTask();
        if (Time.unscaledTime < nextLootCheck) return;
        nextLootCheck = Time.unscaledTime + 0.25f;
        var sep = LocalAvatar() != null ? OpenSephirite() : null;
        if (sep == null)
        {
            if (lootOpen) StopLoot();
            return;
        }
        if (!lootOpen) { lootHidden = false; lootRestarts = 0; }
        lootOpen = true;
        lootSephirite = sep;
        string key = LootKey(sep);
        if (lootStale && lootTask == null)
        {
            lootStale = false;
            if (lootRestarts++ < 2) lootKey = null;
        }
        if (key == lootKey) return;
        if (lootKey != null) lootRestarts = 0;
        lootKey = key;
        if (settings.lootAuto) StartLoot(sep);
        else
        {
            lootGeneration++;
            lootBuildNext = -2;
            lootOptions = null;
            lootMessage = "";
        }
    }

    static string LootKey(Sephirite sep) => sep.netId + ":" + string.Join(",", sep.Rewards.Select(r => r.instanceID));

    void StopLoot()
    {
        lootOpen = false;
        lootGeneration++;
        lootBuildNext = -2;
        lootOptions = null;
        lootBase = null;
        lootKey = null;
        lootMessage = "";
        lootSephirite = null;
    }

    void StartLoot(Sephirite sep)
    {
        lootGeneration++;
        lootOptions = new List<LootOption>();
        lootBase = null;
        lootMessage = "";
        lootSeconds = 0;
        lootStale = false;
        var avatar = LocalAvatar();
        var inv = avatar != null ? avatar.Inventory : null;
        if (inv == null) { lootMessage = Tr("arrange.only_available_during_run"); lootBuildNext = -2; return; }
        var dm = DungeonManager.Instance;
        int Enchant(int instance) => dm != null && int.TryParse(dm.GetGlobalItemStatValue(instance, "Enchant"), out var v) ? v : 0;
        foreach (var r in sep.Rewards)
        {
            var e = ItemDatabase.FindItemById(r.entityID);
            var o = new LootOption
            {
                EntityId = r.entityID, InstanceId = r.instanceID,
                Name = e != null ? ItemName(r.entityID) : r.entityID.ToString(),
                Rarity = e != null ? (int)e.rarity : 0
            };
            lootOptions.Add(o);
            if (e == null || (e.type != EItemType.Charm && e.type != EItemType.StoneTablet)) { o.Note = Tr("loot.not_artifact_or_tablet"); continue; }
            o.IsTablet = e.type == EItemType.StoneTablet;
            var charm = !o.IsTablet && e.resourcePrefab != null ? e.resourcePrefab.GetComponent<Charm_Basic>() : null;
            try
            {
                if (charm != null && charm.isUniqueEffect && charm.connectedUniqueItems != null)
                    foreach (var cu in charm.connectedUniqueItems)
                        if (cu != null && inv.HasItem(cu, out _, out _, out _)) { o.Note = Tr("loot.conflicts_with_owned", ItemName(cu.id)); break; }
                if (o.Note != null) continue;
                if (charm != null && inv.uniquePairCount > 0 && inv.HasItem(e, out var hx, out var hy, out _))
                {
                    var owned = inv.FindItem(hx, hy);
                    if (owned == null || owned.Charm == null || owned.Charm.maxLevel <= 0 || Enchant(owned.InstanceID) >= owned.Charm.maxLevel)
                    {
                        o.Note = Tr("loot.cannot_merge_max_level");
                        continue;
                    }
                    o.Merge = true;
                    o.MergeInstance = owned.InstanceID;
                    o.MergeLevels = 1 + Enchant(r.instanceID);
                    int mode = 2, up = inv.uniquePairAddedComboCount.TryGetValue(owned.InstanceID, out var u) ? u : 0;
                    try { mode = KeywordDatabase.GetConstValue("allowUniquePairIncreaseCombo"); } catch { }
                    o.MergeCats = mode == 1 ? Math.Max(0, Math.Min(o.MergeLevels, owned.Charm.maxLevel - up))
                                : mode == 2 ? (up > 0 ? 0 : 1) : 0;
                    continue;
                }
                if (charm != null && (inv.TryGetUniqueEffect(e, out _) || (charm.isUniqueEffect && inv.HasItem(e, out _, out _, out _))))
                    o.Note = Tr("loot.already_have_unique");
            }
            catch (Exception ex) { WarnOnce("掉落评估：拿取规则", ex); }
        }
        lootTotal = 1 + lootOptions.Count(o => o.Note == null);
        lootProgress = 0;
        lootClock = Stopwatch.StartNew();
        lootBuildNext = -1;
    }

    void BuildNextLootModel()
    {
        var avatar = LocalAvatar();
        if (avatar == null || lootOptions == null) { lootBuildNext = -2; return; }
        try
        {
            if (lootBuildNext == -1)
            {
                lootBase = BuildLootModel(avatar, null, out var err);
                if (lootBase == null) { lootMessage = err ?? Tr("loot.model_failed", ""); lootOptions = null; lootBuildNext = -2; return; }
            }
            else
            {
                var o = lootOptions[lootBuildNext];
                if (o.Note == null)
                {
                    o.Model = BuildLootModel(avatar, o, out var err);
                    if (o.Model == null) { o.Error = err ?? Tr("loot.model_failed", ""); o.Done = true; }
                }
            }
        }
        catch (Exception e)
        {
            WarnOnce("掉落评估建模", e);
            if (lootBuildNext == -1) { lootMessage = Tr("loot.model_failed", e.Message); lootOptions = null; lootBuildNext = -2; return; }
            var o = lootOptions[lootBuildNext];
            o.Error = Tr("loot.model_failed", e.Message);
            o.Done = true;
        }
        lootBuildNext++;
        if (lootBuildNext < lootOptions.Count) return;
        lootBuildNext = -2;
        StartLootTask();
    }

    ArrModel BuildLootModel(PlayerAvatar avatar, LootOption o, out string error)
    {
        lootPick = o != null && !o.Merge ? new LootPick { EntityId = o.EntityId, InstanceId = o.InstanceId, Rotation = lootSephirite != null ? lootSephirite.rotation : 0 } : null;
        try
        {
            var m = BuildModel(avatar, settings.arrMoveOthers, out var charmOf, out error);
            if (m == null) return null;
            var goal = (ArrangeGoal)settings.arrGoal;
            if (goal == ArrangeGoal.Levels) goal = ArrangeGoal.Total;
            m.Goal = goal;
            m.Protect = false;
            try
            {
                m.Dps = BuildDpsModel(avatar, m, charmOf, out _);
                m.Dps.UseMeasured = settings.arrUseMeasured;
                ApplyGoalWeights(m.Dps, goal);
                var ex = m.NewItem >= 0 ? m.Dps.Extra[m.NewItem] : null;
                if (ex?.Cats != null)
                    foreach (var c in ex.Cats)
                        if (c >= 0 && c < m.Dps.CatActual.Length) m.Dps.CatActual[c] += ex.CatWeight;
            }
            catch (Exception e)
            {
                WarnOnce("掉落评估：输出建模", e);
                m.Dps = null;
            }
            return m;
        }
        finally { lootPick = null; }
    }

    void StartLootTask()
    {
        var baseModel = lootBase;
        var opts = lootOptions;
        if (baseModel == null || opts == null) return;
        int gen = lootGeneration, seed = Environment.TickCount;
        bool rotate = settings.arrRotate;
        var todo = opts.Where(o => o.Model != null).ToList();
        lootTotal = 1 + todo.Count;
        lootTask = Task.Run(() =>
        {
            var baseResult = Optimize(baseModel, rotate, LootBaseBudget, seed);
            if (gen != lootGeneration) return;
            lootProgress = 1;
            void Run(LootOption o, int k, double budget, int chains)
            {
                try { EvaluateLootOption(baseModel, baseResult, o, rotate, seed + 7919 * (k + 1), budget, chains); }
                catch (Exception e) { o.Failure = e; o.Error = Tr("loot.calculation_error", e.Message); }
                o.Model = null;
                o.Done = true;
                Interlocked.Increment(ref lootProgress);
            }
            var charms = todo.Where(o => !o.IsTablet).ToList();
            int degree = Math.Max(1, Math.Min(charms.Count, Environment.ProcessorCount / 2));
            Parallel.For(0, charms.Count, new ParallelOptions { MaxDegreeOfParallelism = degree }, (k, state) =>
            {
                if (gen != lootGeneration) { state.Stop(); return; }
                Run(charms[k], k, LootCharmBudget, 1);
            });
            var tablets = todo.Where(o => o.IsTablet).ToList();
            for (int k = 0; k < tablets.Count && gen == lootGeneration; k++)
                Run(tablets[k], charms.Count + k, LootTabletBudget, 0);
        });
    }

    void EvaluateLootOption(ArrModel b, ArrResult br, LootOption o, bool rotate, int seed, double budget, int chains)
    {
        var h = o.Model;
        PrepareModel(h);
        if (!SeedFor(b, br, h, out var sp, out var sr)) { lootStale = true; o.Error = Tr("loot.inventory_changed"); return; }
        var w0 = new Evaluator(h).Detail(sp, sr);
        int k = -1;
        var ex = (ItemExtra)null;
        void Merge(int sign)
        {
            h.Items[k].Enchant += sign * o.MergeLevels;
            if (ex?.Cats == null || o.MergeCats <= 0 || h.Dps?.CatStatic == null) return;
            ex.CatWeight += sign * o.MergeCats;
            foreach (var c in ex.Cats) if (c >= 0 && c < h.Dps.CatStatic.Length) h.Dps.CatStatic[c] += sign * o.MergeCats;
        }
        if (o.Merge)
        {
            k = Array.FindIndex(h.Items, it => it.InstanceId == o.MergeInstance);
            if (k < 0) { lootStale = true; o.Error = Tr("loot.inventory_changed"); return; }
            ex = h.Dps?.Extra[k];
            Merge(+1);
        }
        BestInsertion(h, sp, sr);
        var r = Optimize(h, rotate, budget, seed, sp, sr, chains);
        var w1 = r.After;
        bool dps = h.Dps != null;
        bool Better(ArrEval x, ArrEval y) => dps ? x.Weighted > y.Weighted : x.Score > y.Score;
        int slot = h.NewItem >= 0 ? Array.IndexOf(r.Perm, h.NewItem) : -1;
        int dropped = -1;
        for (int s = h.N; s < r.Perm.Length; s++)
            if (r.Perm[s] >= 0 && r.Perm[s] != h.NewItem) dropped = r.Perm[s];
        if (o.Merge)
        {
            Merge(-1);
            var alt = new Evaluator(h).Detail(r.Perm, r.Rot);
            Merge(+1);
            if (Better(alt, w0)) w0 = alt;
        }
        else if (slot >= 0 && slot < h.N)
        {
            var altPerm = (int[])r.Perm.Clone();
            altPerm[slot] = dropped;
            for (int s = h.N; s < altPerm.Length; s++) altPerm[s] = -1;
            altPerm[h.N] = h.NewItem;
            var alt = new Evaluator(h).Detail(altPerm, r.Rot);
            if (Better(alt, w0)) w0 = alt;
        }
        o.Dps = dps;
        o.W = h.W;
        o.Taken = o.Merge || (slot >= 0 && slot < h.N);
        if (!o.Taken) w1 = w0;
        if (o.Dps)
        {
            o.Gain = w0.Weighted > 0 ? w1.Weighted / w0.Weighted - 1 : 0;
            o.TotalGain = w0.Total > 0 ? w1.Total / w0.Total - 1 : 0;
            if (!o.Merge && o.Gain < 0) { o.Gain = 0; o.TotalGain = 0; o.Taken = false; w1 = w0; }
        }
        o.LevelGain = w1.EffectiveLevels - w0.EffectiveLevels;
        if (!o.Taken) return;
        if (slot >= 0 && slot < h.N)
        {
            o.Slot = slot;
            if (h.FreeRotTablet >= 0)
            {
                o.Rotation = r.Rot[h.FreeRotTablet];
                o.Rotatable = h.Items[h.TabletItem[h.FreeRotTablet]].Rotatable;
            }
        }
        if (dropped >= 0) o.Drop = h.Items[dropped].Name;
        for (int s = 0; s < h.N && !o.Rearrange; s++)
            if (r.Perm[s] != h.Start[s] && r.Perm[s] != h.NewItem && !(h.Start[s] == dropped && r.Perm[s] < 0))
                o.Rearrange = true;
        for (int t = 0; t < h.TabletItem.Length && !o.Rearrange; t++)
            if (t != h.FreeRotTablet && r.Rot[t] != h.StartRot[t]) o.Rearrange = true;
    }

    static void BestInsertion(ArrModel h, int[] perm, int[] rot)
    {
        if (h.NewItem < 0 || h.Bench == 0 || perm.Length <= h.N || perm[h.N] != h.NewItem) return;
        var ev = new Evaluator(h);
        var it = h.Items[h.NewItem];
        int t = it.IsTablet ? it.TabletIndex : -1, turns = t >= 0 && it.Rotatable ? 4 : 1, bench = h.N;
        int orig = t >= 0 ? rot[t] : 0;
        bool full = true;
        for (int s = 0; s < h.N; s++) if (perm[s] < 0) { full = false; break; }
        var slots = Enumerable.Range(0, h.N).Where(s => h.Movable[s]).ToArray();
        var cands = new List<(double Score, int[] Perm, int Rot)> { (Objective(h, ev, perm, rot), (int[])perm.Clone(), orig) };
        void Try()
        {
            for (int r = 0; r < turns; r++)
            {
                if (turns > 1) rot[t] = r;
                cands.Add((Objective(h, ev, perm, rot), (int[])perm.Clone(), t >= 0 ? rot[t] : 0));
            }
        }
        foreach (var s in slots)
        {
            (perm[bench], perm[s]) = (perm[s], perm[bench]);
            Try();
            if (full && perm[bench] >= 0)
                foreach (var u in slots)
                {
                    if (u == s) continue;
                    (perm[bench], perm[u]) = (perm[u], perm[bench]);
                    Try();
                    (perm[bench], perm[u]) = (perm[u], perm[bench]);
                }
            (perm[bench], perm[s]) = (perm[s], perm[bench]);
        }
        if (t >= 0) rot[t] = orig;
        var all = slots.Append(bench).ToArray();
        double best = double.NegativeInfinity;
        int[] bestPerm = null;
        int bestRot = orig;
        foreach (var c in cands.OrderByDescending(c => c.Score).Take(6))
        {
            var p = (int[])c.Perm.Clone();
            var ro = (int[])rot.Clone();
            if (t >= 0) ro[t] = c.Rot;
            double sc = LocalImprove(h, ev, p, ro, all, turns > 1 ? t : -1);
            if (sc > best) { best = sc; bestPerm = p; bestRot = t >= 0 ? ro[t] : 0; }
        }
        Array.Copy(bestPerm, perm, perm.Length);
        if (t >= 0) rot[t] = bestRot;
    }

    static double LocalImprove(ArrModel h, Evaluator ev, int[] perm, int[] rot, int[] slots, int turnTablet)
    {
        double cur = Objective(h, ev, perm, rot);
        for (int pass = 0; pass < 5; pass++)
        {
            bool improved = false;
            for (int i = 0; i < slots.Length; i++)
                for (int j = i + 1; j < slots.Length; j++)
                {
                    int a = slots[i], b = slots[j];
                    if (perm[a] < 0 && perm[b] < 0) continue;
                    (perm[a], perm[b]) = (perm[b], perm[a]);
                    double sc = Objective(h, ev, perm, rot);
                    if (sc > cur + 1e-9) { cur = sc; improved = true; }
                    else (perm[a], perm[b]) = (perm[b], perm[a]);
                }
            if (turnTablet >= 0)
                for (int r = 0; r < 4; r++)
                {
                    int old = rot[turnTablet];
                    if (r == old) continue;
                    rot[turnTablet] = r;
                    double sc = Objective(h, ev, perm, rot);
                    if (sc > cur + 1e-9) { cur = sc; improved = true; }
                    else rot[turnTablet] = old;
                }
            if (!improved) break;
        }
        return cur;
    }

    static bool SeedFor(ArrModel b, ArrResult br, ArrModel h, out int[] perm, out int[] rot)
    {
        perm = rot = null;
        if (b.N != h.N || b.W != h.W || br?.Perm == null) return false;
        if (h.Items.Length - (h.NewItem >= 0 ? 1 : 0) != b.Items.Length) return false;
        var index = new Dictionary<int, int>();
        bool unique = true;
        for (int i = 0; i < h.Items.Length; i++)
            if (i != h.NewItem && !index.TryAdd(h.Items[i].InstanceId, i)) unique = false;
        var map = new int[b.Items.Length];
        for (int i = 0; i < b.Items.Length; i++)
        {
            if (unique && index.TryGetValue(b.Items[i].InstanceId, out var j)) map[i] = j;
            else if (!unique && h.Items[i].InstanceId == b.Items[i].InstanceId && h.Items[i].EntityId == b.Items[i].EntityId) map[i] = i;
            else return false;
        }
        perm = Enumerable.Repeat(-1, h.N + h.Bench).ToArray();
        for (int s = 0; s < b.N; s++)
            if (br.Perm[s] >= 0) perm[s] = map[br.Perm[s]];
        if (h.NewItem >= 0) perm[h.N] = h.NewItem;
        rot = (int[])h.StartRot.Clone();
        for (int t = 0; t < b.TabletItem.Length; t++)
        {
            int ti = h.Items[map[b.TabletItem[t]]].TabletIndex;
            if (ti < 0) return false;
            rot[ti] = br.Rot[t];
        }
        return true;
    }

    void FinishLootTask()
    {
        if (lootTask.IsFaulted)
        {
            WarnOnce("掉落评估", lootTask.Exception?.GetBaseException() ?? new Exception("未知错误"));
            lootMessage = Tr("loot.calculation_error", lootTask.Exception?.GetBaseException().Message ?? "");
        }
        if (lootOptions != null)
            foreach (var o in lootOptions)
                if (o.Failure != null) { WarnOnce("掉落评估 " + o.Name, o.Failure); o.Failure = null; }
        lootTask = null;
        lootSeconds = lootClock != null ? lootClock.Elapsed.TotalSeconds : 0;
    }
}
