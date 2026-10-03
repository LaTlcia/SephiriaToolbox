using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterMystic()
    {
        PassiveDefs["Charm_IncreaseHP"] = P(new PassiveDef { Kind = PKind.Stat, Key = MaxHpKey, By = "addHPByLevel" });

        PassiveDefs["Charm_FirstAttackBonusDamage"] = P(new PassiveDef { Kind = PKind.Mul, By = "damageBonusByLevel", Uptime = UpFirstHit, Factor = (c, a) => buildSingle ? 0.1 : 1, OwnOnly = true });

        PassiveDefs["Charm_IncreaseAllDamageByHP"] = P(new PassiveDef { Kind = PKind.Stat, Key = "ALLDAMAGEBONUS", By = "damagePercentByLevel", Uptime = UpFullHp, Cur = "enabledValue" });

        PassiveDefs["Charm_MinHPKill"] = P(new PassiveDef
        {
            Kind = PKind.Mul, By = "minHPPercentByLevel", OwnOnly = true,
            Transform = (c, a, v) => v.Select(x => (float)((1 / Math.Max(0.05, 1 - x / 100.0) - 1) * 100 * NormalShare())).ToArray()
        });
    }
}
