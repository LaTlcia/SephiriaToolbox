using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterGuardian()
    {
        PassiveDefs["Charm_AddStatByDefense"] = P(
            new PassiveDef { Kind = PKind.PerStat, Key = "FIREDAMAGE", By = "addByLevel", PerKey = "DAMAGEREDUCTION", Div = 10, Cur = "addedValue" },
            new PassiveDef { Kind = PKind.PerStat, Key = "ICEDAMAGE", By = "addByLevel", PerKey = "DAMAGEREDUCTION", Div = 10, Cur = "addedValue" },
            new PassiveDef { Kind = PKind.PerStat, Key = "LIGHTNINGDAMAGE", By = "addByLevel", PerKey = "DAMAGEREDUCTION", Div = 10, Cur = "addedValue" });

        BuffCharms["Charm_Endure"] = new() { Field = "buff", Rate = (c, a, b) => b.PhysCritHits };

        CharmMechs["Charm_RockElephant"] = new() { Trig = Trig.Active, TimerField = "coolDownTimer", DashCdField = "coolDownReductionOnDash", Per = 3f, Note = Tr("trigger.active_rock_spear_paid") };
        Extra<Charm_RockElephant>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            double def = Num(c, "defaultStoneBulletCount"), per = Num(c, "additionalStoneBulletCountByATKSpeed");
            if (def > 0 && per > 0) AddX(s, new XMod { Kind = XKind.PerByStat, Key = key("ATTACKSPEED"), A = def, B = per, C = 3 });
        });
    }
}
