using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterElemental()
    {
        CharmMechs["Charm_FireBulletInRange"] = new() { Trig = Trig.Active, TimerField = "coolDownTimer", CountBy = "countByLevel", Note = Tr("trigger.active_launches_elemental_bomb") };
    }
}
