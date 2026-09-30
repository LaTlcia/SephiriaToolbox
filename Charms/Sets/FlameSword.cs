using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterFlameSword()
    {
        CharmMechs["Charm_FlameSwordFall"] = new() { Trig = Trig.Active, Per = 7f, Note = Tr("trigger.active_recalls_about_4") };
        Extra<Charm_FlameSwordFall>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            var f = (Charm_FlameSwordFall)c;
            if (!string.IsNullOrEmpty(f.solisImberStatId)) AddX(s, new XMod { Kind = XKind.DmgIf, Key = key(f.solisImberStatId), A = 1.5 });
        });

        PassiveDefs["Charm_FlameSwordReturn"] = P(new PassiveDef { Kind = PKind.Stat, Key = "FLAMESWORDRETURN", Const = 1 });
    }
}
