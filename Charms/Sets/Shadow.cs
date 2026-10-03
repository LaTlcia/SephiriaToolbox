using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterShadow()
    {
        ModelHook<Charm_BrokenSapphire>((c, item, d) => d.EvadeBarriers.Add((item, LevelTable(c, "timeByLevel"))));

        CharmMechs["Charm_GreenGi"] = new() { Trig = Trig.Evade, Note = Tr("trigger.punches_evasion") };

        CharmMechs["Charm_ShadowEye"] = new() { Trig = Trig.Crit, Per = 1f / 16f, Targets = true, Note = Tr("trigger.counterattacks_evasion_after_enough") };
        Extra<Charm_ShadowEye>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            double crit = buildOwner != null ? Math.Min(1, Math.Max(0, (buildOwner.GetCustomStat(ECustomStat.Critical) + buildOwner.GetCustomStatUnsafe("WEAPONCRITICAL")) / 10000.0)) : 0.3;
            double crits = Math.Min(1 / 0.15, Math.Max(0.01, buildWeaponHits * crit));
            double fill = 16 / crits, wait = 1 / Math.Max(0.05, b.Damaged);
            s.TheoryK *= fill / (fill + wait);
        });
    }
}
