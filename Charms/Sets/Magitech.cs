using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterMagitech()
    {
        CharmMechs["Charm_ElectricEarring"] = new() { Trig = Trig.Periodic, TimerField = "cooldownTimer", ExtraField = "searchTimer", TimerExtra = 0.5f, CountBy = "countByLevel", Note = Tr("trigger.when_off_cooldown_finds_enemies") };
        Extra<Charm_ElectricEarring>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            var count = LevelTable(c, "countByLevel");
            if (count != null) AddX(s, new XMod { Kind = XKind.CountAdd, Key = key("ELECTRICEARRINGCOUNT"), Levels = count });
            double search = TimerSeconds(c, "searchTimer");
            AddX(s, new XMod { Kind = XKind.Sweep, Key = key("ELECTRICEARRINGSWEEP"), A = Math.Max(0.1, TimerSeconds(c, "cooldownTimer")), B = search > 0 ? search : 0.5, C = b.Special });
        });

        CharmMechs["Charm_FireChakram"] = new() { Trig = Trig.Orbit, Base = 0.83f, CountBy = "bulletCountByLevel", Note = Tr("trigger.orbiting_chakrams") };
        Extra<Charm_FireChakram>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            AddX(s, new XMod { Kind = XKind.RatePctIf, Key = key("ATKSPDCHAKRAM"), Key2 = key("ATTACKSPEED") });
        });

        PassiveDefs["Charm_FireFly"] = P(new PassiveDef
        {
            Kind = PKind.Stat, Key = "LIGHTNINGDAMAGE", By = "lightningDamageByLevel",
            Factor = (c, a) => NoHitShare(TimerSeconds(c, "fireFlyReturnTimer")),
            CurFunc = (c, a) => Num(c, "onApplied") > 0 ? SafeAt(LevelTable(c, "lightningDamageByLevel"), c.CurrentLevelToIdx()) : 0
        });

        PassiveDefs["Charm_KirinHorn"] = P(new PassiveDef { Kind = PKind.Crit, By = "addCriticalByLevel", Elem = 3 });
    }
}
