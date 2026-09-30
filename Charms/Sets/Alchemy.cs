using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterAlchemy()
    {
        BuffCharms["Charm_AlchemyFlask"] = new() { AmpBy = "amplifyValuesByLevel", Rate = (c, a, b) => PlayPotion };
    }
}
