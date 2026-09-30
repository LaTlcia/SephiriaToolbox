using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mirror;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void ReadBossAffix(PlayerAvatar avatar, DpsModel d)
    {
        var dm = DungeonManager.Instance;
        if (dm == null || dm.generatedFloors == null || avatar == null || string.IsNullOrEmpty(avatar.currentFloorGuid)) return;
        if (!dm.generatedFloors.TryGetValue(avatar.currentFloorGuid, out var cur) || cur == null) return;
        FloorData next = null;
        foreach (var f in dm.generatedFloors.Values)
        {
            if (f == null || string.IsNullOrEmpty(f.bossHard) || f.stageName != cur.stageName || f.nodeProgress < cur.nodeProgress) continue;
            if (next == null || f.nodeProgress < next.nodeProgress) next = f;
        }
        if (next == null) return;
        var ent = UnitDatabase.GetBossHardEntityByKey(next.bossHard);
        if (ent == null || ent.effects == null) return;
        foreach (var eff in ent.effects)
        {
            var parts = (eff ?? "").Split('/');
            if (parts.Length < 2 || !int.TryParse(parts[1], out var v)) continue;
            string key = parts[0].Replace("_", "").ToUpperInvariant();
            switch (key)
            {
                case "PHYSICALDEFENSE": d.BossResist[0] += v; break;
                case "FIREDEFENSE": d.BossResist[1] += v; break;
                case "ICEDEFENSE": d.BossResist[2] += v; break;
                case "LIGHTNINGDEFENSE": d.BossResist[3] += v; break;
                case "CRITICALRESIST": d.BossCritResist += v; break;
                case "TOUGHNESS": d.BossToughness += v; break;
                case "DEFENSE": d.BossDefBonus += v; break;
                case "IGNOREEVASION": d.BossIgnoreEvasion += v; break;
            }
        }
        d.BossAffix = Clean(ent.aName.ToString());
    }

    static int StageDefense(DungeonManager dm, string stageName, out int plate)
    {
        plate = 0;
        if (string.IsNullOrEmpty(stageName)) return 0;
        var st = dm.FindStage(stageName);
        int def = st != null ? st.additionalDEF : 0;
        int progress = dm.sortedStages.IndexOf(stageName);
        if (progress >= 0 && dm.hardModeEnvironment.TryGetValue("PLATEARMOR", out var v)) plate = v * (progress - 1);
        return def + plate;
    }

    static double DefenseReduction(double def) => def > 0 ? Math.Min(1, Math.Log(def / 40.0 + 1) * 0.445) : def / 100.0;
}
