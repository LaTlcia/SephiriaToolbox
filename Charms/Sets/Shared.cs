using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterShared()
    {
        CharmMechs["Charm_FrostiumRing"] = new() { Trig = Trig.DirectOrMagic, CapTimer = "cooldownTimer", Note = Tr("trigger.extra_hit_weapon_magic") };
    }
}
