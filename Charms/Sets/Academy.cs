using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterAcademy()
    {
        PassiveDefs["Charm_ReservedMPBonus"] = P(new PassiveDef { Kind = PKind.Stat, Key = "MAGICDAMAGEBONUS", By = "damageByLevel" });

        CharmMechs["Charm_TuningForks"] = new() { Trig = Trig.MagicCast, Stat = RateStat.MagicCasts, StatCapTimer = "cooldownTimer", StatCap = 1f / 3f, Targets = true, Note = Tr("trigger.casting_magic_builds_stacks") };
        Extra<Charm_TuningForks>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            s.DirectCap = Math.Max(1, ((Charm_TuningForks)c).maxStack);
            s.MagicCrit = true;
        });
    }
}
