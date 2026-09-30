using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    readonly Dictionary<string, double> debuffTargetSum = new(), debuffStackSum = new();
    int debuffSamples;
    double targetDebuffSum;
    double targetOtherDebuffSum;
    int targetDebuffSamples;

    void SampleDebuffs(PlayerAvatar a)
    {
        debuffSamples++;
        CharacterDebuff[] all;
        try { all = UnityEngine.Object.FindObjectsByType<CharacterDebuff>(FindObjectsSortMode.None); }
        catch { return; }
        HashSet<UnitAvatar> seen = null;
        foreach (var db in all)
        {
            if (db == null || db.IsEndBuff || db.NetworkAttacker != a) continue;
            string id = db.ID;
            if (string.IsNullOrEmpty(id)) continue;
            debuffTargetSum[id] = debuffTargetSum.GetValueOrDefault(id) + 1;
            debuffStackSum[id] = debuffStackSum.GetValueOrDefault(id) + Math.Max(1, (int)db.CurrentStack);
            var t = db.NetworkTarget;
            if (t == null || t.IsDead) continue;
            seen ??= new HashSet<UnitAvatar>();
            if (!seen.Add(t)) continue;
            int n = t.isInStun ? 1 : 0, other = n;
            foreach (var x in t.Debuffs)
            {
                if (x == null || x.IsEndBuff) continue;
                n++;
                if (x.NetworkAttacker != a) other++;
            }
            targetDebuffSum += n;
            targetOtherDebuffSum += other;
            targetDebuffSamples++;
        }
    }

    void ResetDebuffSamples()
    {
        debuffTargetSum.Clear();
        debuffStackSum.Clear();
        debuffSamples = 0;
        targetDebuffSum = 0;
        targetOtherDebuffSum = 0;
        targetDebuffSamples = 0;
    }

    double OtherDebuffsOnTarget() => targetDebuffSamples >= 20 ? targetOtherDebuffSum / targetDebuffSamples : 0;

    bool DebuffMeasured(string id, out double targets, out double stacks)
    {
        targets = stacks = 0;
        if (debuffSamples < 20 || !debuffTargetSum.TryGetValue(id, out var t) || t < 10) return false;
        targets = t / debuffSamples;
        stacks = debuffStackSum.GetValueOrDefault(id) / t;
        return true;
    }
}
