using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterDarkCloud()
    {
        CharmMechs["Charm_TheTyphoonSheetmusic"] = new() { Trig = Trig.DirectHit, Note = Tr("trigger.extra_lightning_weapon_hits") };
        PassiveDefs["Charm_TheTyphoonSheetmusic"] = P(new PassiveDef { Kind = PKind.Stat, Key = "DARKCLOUDATKSPEEDBONUS", By = "cloudAttackSpeedByLevel" });
    }
}
