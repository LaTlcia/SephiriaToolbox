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

        CharmMechs["Charm_ShadowEye"] = new() { Trig = Trig.Crit, Per = 1f / 15f, Targets = true, Note = Tr("trigger.counterattacks_evasion_after_enough") };
    }
}
