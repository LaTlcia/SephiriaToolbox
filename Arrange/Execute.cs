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
    struct ArrStep
    {
        public bool Swap;
        public int A, B;
        public int[] Expect;
        public int Tablet, ExpectRotation;
    }

    static List<ArrStep> PlanSteps(ArrModel m, int[] target, int[] targetRot)
    {
        var steps = new List<ArrStep>();
        var cur = (int[])m.Start.Clone();
        for (int s = 0; s < m.N; s++)
        {
            int want = target[s];
            if (want < 0 || cur[s] == want) continue;
            int j = Array.IndexOf(cur, want);
            (cur[s], cur[j]) = (cur[j], cur[s]);
            steps.Add(new ArrStep { Swap = true, A = s, B = j, Expect = (int[])cur.Clone() });
        }
        for (int t = 0; t < m.TabletItem.Length; t++)
        {
            int clicks = ((targetRot[t] - m.StartRot[t]) % 4 + 4) % 4;
            int slot = Array.IndexOf(cur, m.TabletItem[t]);
            if (slot < 0) continue;
            for (int k = 1; k <= clicks; k++)
                steps.Add(new ArrStep { Swap = false, A = slot, Tablet = t, ExpectRotation = (m.StartRot[t] + k) % 4, Expect = cur });
        }
        return steps;
    }

    void ExecuteArrange()
    {
        if (arrState != ArrState.Ready || arrResult == null) return;
        StartCoroutine(RunArrange(arrModel, arrResult));
    }

    static bool MatchesState(GridInventory inv, ArrModel m, int[] expect, int tablet = -1, int rotation = -1)
    {
        for (int s = 0; s < m.N; s++)
        {
            var v = inv.FindItem(inv.IdxToPos(s));
            int want = expect[s];
            if (want < 0 ? v != null : v == null || v.InstanceID != m.Items[want].InstanceId) return false;
        }
        if (tablet >= 0)
        {
            var v = inv.FindItemByInstanceID(m.Items[m.TabletItem[tablet]].InstanceId);
            if (v == null || v.StoneTablet == null || ((v.StoneTablet.rotation % 4) + 4) % 4 != rotation) return false;
        }
        return true;
    }

    static bool ItemPickedUp()
    {
        try
        {
            var picker = UIManager.Instance.GetElement<UI_NewItemPicker_Controller>();
            return picker != null && (picker.CurrentPickedUp != null || picker.CurrentDraggable != null);
        }
        catch { return false; }
    }

    IEnumerator RunArrange(ArrModel m, ArrResult r, int stageTabletInstance = -1, string stageTabletName = null)
    {
        var avatar = LocalAvatar();
        var inv = avatar != null ? avatar.Inventory : null;
        if (inv == null) { arrMessage = Tr("arrange.inventory_not_found"); arrState = ArrState.Idle; yield break; }
        if (ItemPickedUp()) { arrMessage = Tr("arrange.please_put_down_item"); yield break; }
        if (!MatchesState(inv, m, m.Start)) { arrMessage = Tr("arrange.inventory_changed_after_calculation"); arrState = ArrState.Idle; yield break; }

        var steps = PlanSteps(m, r.Perm, r.Rot);
        arrState = ArrState.Running;
        arrTotal = steps.Count;
        for (int k = 0; k < steps.Count; k++)
        {
            arrProgress = k;
            var st = steps[k];
            var a = inv.IdxToPos(st.A);
            if (st.Swap)
            {
                var b = inv.IdxToPos(st.B);
                inv.Swap(a.x, a.y, b.x, b.y);
            }
            else inv.DoClickAction(a);

            float deadline = Time.unscaledTime + 3f;
            while (!MatchesState(inv, m, st.Expect, st.Swap ? -1 : st.Tablet, st.ExpectRotation))
            {
                if (Time.unscaledTime > deadline)
                {
                    arrMessage = Tr("arrange.step_had_no_effect", k + 1);
                    arrState = ArrState.Idle;
                    yield break;
                }
                yield return null;
            }
            yield return null;
        }
        arrProgress = steps.Count;

        yield return new WaitForSecondsRealtime(0.5f);
        int enabled = 0, levels = 0, charms = 0;
        foreach (var c in inv.charms.Values)
        {
            if (c == null) continue;
            charms++;
            if (c.IsEffectEnabled) { enabled++; levels += Mathf.Clamp(c.DisplayedLevel, 0, c.maxLevel); }
        }
        if (stageTabletInstance >= 0)
        {
            var v = inv.FindItemByInstanceID(stageTabletInstance);
            bool applied = v != null && v.StoneTablet != null && v.StoneTablet.IsApplied;
            arrMessage = applied
                ? Tr("arrange.suggested_imprint_position_condition", stageTabletName)
                : Tr("arrange.place_but_game_shows", stageTabletName);
            arrState = ArrState.Done;
            yield break;
        }
        arrMessage = Tr("arrange.arranged_active_total_effective", r.Before.Enabled, enabled, charms, r.Before.EffectiveLevels, levels, r.After.EffectiveLevels);
        if (m.Dps != null && r.Before.Total > 0)
            arrMessage += Tr("arrange.estimated_total_damage_upcoming", Signed(r.After.Total / r.Before.Total - 1));
        arrState = ArrState.Done;
    }
}
