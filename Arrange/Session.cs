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
    enum ArrState { Idle, Computing, Ready, Running, Done }

    ArrState arrState;
    ArrModel arrModel;
    ArrResult arrResult;
    Task<ArrResult> arrTask;
    string arrMessage = "", arrCheck = "", arrDpsInfo = "";
    int arrProgress, arrTotal;
    List<string> arrChanges = new();
    double arrBuildMs, arrBudget = 1;
    int engraveTotal, arrGeneration;

    void ApplyGoalWeights(DpsModel d, ArrangeGoal goal)
    {
        foreach (var s in d.Sources)
        {
            s.Weight = goal switch
            {
                ArrangeGoal.Weapon => s.IsWeapon ? 1 : 0.05,
                ArrangeGoal.Magic => s.Kind == SrcKind.Magic ? 1 : 0.05,
                ArrangeGoal.Custom => s.IsWeapon ? settings.wWeapon : s.Kind == SrcKind.Magic ? settings.wMagic : settings.wProc,
                _ => 1
            };
        }
        d.OtherWeight = goal == ArrangeGoal.Total || goal == ArrangeGoal.Custom ? 1 : 0;
    }

    void StartArrange(double budgetSeconds)
    {
        if (arrState == ArrState.Computing || arrState == ArrState.Running) return;
        var avatar = LocalAvatar();
        if (avatar == null) { arrMessage = Tr("arrange.arranging_only_available_during"); arrState = ArrState.Idle; return; }
        var clock = Stopwatch.StartNew();
        ArrModel model;
        Charm_Basic[] charmOf;
        string error;
        try { model = BuildModel(avatar, settings.arrMoveOthers, out charmOf, out error); }
        catch (Exception e) { WarnOnce("整理建模", e); model = null; charmOf = null; error = Tr("arrange.read_inventory_failed", e.Message); }
        if (model == null) { arrMessage = error; arrState = ArrState.Idle; return; }

        var goal = (ArrangeGoal)settings.arrGoal;
        model.Goal = goal;
        model.Protect = settings.arrProtect;
        arrDpsInfo = "";
        if (goal != ArrangeGoal.Levels)
        {
            try
            {
                model.Dps = BuildDpsModel(avatar, model, charmOf, out arrDpsInfo);
                model.Dps.UseMeasured = settings.arrUseMeasured;
                ApplyGoalWeights(model.Dps, goal);
            }
            catch (Exception e)
            {
                WarnOnce("输出建模", e);
                model.Dps = null;
                arrDpsInfo = Tr("arrange.damage_model_failed_arranging", e.Message);
            }
        }
        arrBuildMs = clock.Elapsed.TotalMilliseconds;

        arrModel = model;
        arrResult = null;
        arrCheck = "";
        arrState = ArrState.Computing;
        arrBudget = budgetSeconds;
        arrGeneration++;
        engraveTask = null;
        engraveOptions = null;
        engraveMessage = "";
        arrMessage = Tr("arrange.calculating_background_about_s", budgetSeconds);
        bool rotate = settings.arrRotate;
        int seed = Environment.TickCount;
        arrTask = Task.Run(() => LowPriority(() =>
        {
            var r = Optimize(model, rotate, budgetSeconds, seed);
            if (r != null && model.Dps != null)
            {
                try { r.Values = ComputeCharmValues(model, r.Perm, r.Rot); }
                catch (Exception e) { Debug.LogWarning("[SephiriaToolbox] 神器价值：" + e); }
            }
            return r;
        }));
    }

    void UpdateArrange()
    {
        UpdateForge();
        if (engraveTask != null && engraveTask.IsCompleted)
        {
            if (engraveTask.IsFaulted) WarnOnce("刻印分析", engraveTask.Exception?.GetBaseException() ?? new Exception("未知错误"));
            else engraveOptions = engraveTask.Result;
            engraveTask = null;
        }
        if (arrState != ArrState.Computing || arrTask == null || !arrTask.IsCompleted) return;
        if (arrTask.IsFaulted)
        {
            var ex = arrTask.Exception?.GetBaseException() ?? new Exception(Tr("arrange.unknown_error"));
            WarnOnce("整理计算", ex);
            arrMessage = Tr("arrange.calculation_error", ex.Message);
            arrState = ArrState.Idle;
            arrTask = null;
            return;
        }
        arrResult = arrTask.Result;
        arrTask = null;
        var m = arrModel;
        var r = arrResult;
        if (m.CanEngrave && m.TabletItem.Length > 0)
        {
            bool rotate = settings.arrRotate;
            double per = arrBudget >= 3 ? 1.0 : 0.3;
            int seed = Environment.TickCount, gen = arrGeneration;
            engraveTotal = m.TabletItem.Length;
            engraveProgress = 0;
            engraveTask = Task.Run(() => LowPriority(() =>
            {
                var result = AnalyzeEngraving(m, r, rotate, per, seed, i => { engraveProgress = i; return gen == arrGeneration; });
                return gen == arrGeneration ? result : null;
            }));
        }

        int levelDiff = 0, stateDiff = 0;
        for (int i = 0; i < m.Items.Length; i++)
        {
            var it = m.Items[i];
            if (it.Kind < KindCharm) continue;
            if (r.Before.Level[i] != it.ActualLevel) levelDiff++;
            else if (r.Before.On[i] != it.ActualEnabled) stateDiff++;
        }
        arrCheck = (levelDiff == 0 && stateDiff == 0
                ? Tr("arrange.model_self_check_matches")
                : Tr("arrange.model_self_check_artifact", levelDiff, stateDiff))
            + Tr("arrange.performance_ms_reading_main", arrBuildMs, r.Chains, r.Seconds, r.Evaluations, r.Evaluations / Math.Max(0.01, r.Seconds));
        arrChanges = DescribeChanges(m, r);

        var b = r.Before;
        var a = r.After;
        string levels = Tr("arrange.active_total_effective_levels", b.Enabled, b.Charms, a.Enabled, a.Charms, b.EffectiveLevels, a.EffectiveLevels);
        string dps = m.Dps != null && b.Total > 0
            ? (m.Goal != ArrangeGoal.Total
                ? Tr("arrange.estimated_total_damage_goal", Signed(a.Total / b.Total - 1), Signed(a.Weighted / Math.Max(1e-9, b.Weighted) - 1))
                : Tr("arrange.estimated_total_damage", Signed(a.Total / b.Total - 1)))
            : "";
        if (r.Swaps + r.Rotations == 0)
        {
            arrMessage = Tr("arrange.current_layout_already_best", levels, dps);
            arrState = ArrState.Idle;
            return;
        }
        arrMessage = Tr("arrange.can_be_improved_needs", levels, dps, r.Swaps, r.Rotations);
        arrState = ArrState.Ready;
    }

    List<string> DescribeChanges(ArrModel m, ArrResult r)
    {
        var list = new List<string>();
        for (int i = 0; i < m.Items.Length; i++)
        {
            var it = m.Items[i];
            if (it.Kind < KindCharm) continue;
            int l0 = r.Before.Level[i], l1 = r.After.Level[i];
            bool o0 = r.Before.On[i], o1 = r.After.On[i];
            if (l0 == l1 && o0 == o1) continue;
            string S(int l, bool on) => on ? $"Lv{l}" : Tr("arrange.off_lv", l);
            list.Add(Tr("arrange.level_change", it.Name, S(l0, o0), S(l1, o1)));
        }
        return list;
    }
}
