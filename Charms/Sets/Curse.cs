using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterCurse()
    {
        PassiveDefs["Charm_DebuffDamage"] = P(new PassiveDef { Kind = PKind.Mul, By = "additionalDamage", PerDebuffCount = true, OwnOnly = true });
    }
}
