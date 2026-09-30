using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterWindSong()
    {
        PassiveDefs["Charm_DashAttackDamage"] = P(new PassiveDef { Kind = PKind.Stat, Key = "DASHATTACKDAMAGEBONUS", By = "bonusByLevel" });

        CharmMechs["Charm_DashDamage"] = new() { Trig = Trig.Dash, Per = 1.5f, Targets = true, Note = Tr("trigger.hits_enemies_you_dash") };

        PassiveDefs["Charm_IncreaseAttackSpeed"] = P(new PassiveDef { Kind = PKind.Stat, Key = "ATTACKSPEED", By = "atkSpeedByLevel" });

        PassiveDefs["Charm_IncreaseMeleeAttackRange"] = P(new PassiveDef { Kind = PKind.Stat, Key = "WEAPONRANGE", By = "rangeByLevel" });

        CharmMechs["Charm_PallasCard"] = new() { Trig = Trig.Swing, Stat = RateStat.Luck, Weight = true, Per = 1.2f, CapTimer = "throwIntervalTimer", Note = Tr("trigger.chance_throw_card_weapon") };

        PassiveDefs["Charm_SpeedRun"] = P(new PassiveDef
        {
            Kind = PKind.Stat, Key = "ATTACKSPEED", By = "attackSpeedByLevel", Uptime = (float)UpFloorStart,
            CurFunc = (c, a) => Num(c, "buffEnabled") > 0 ? SafeAt(LevelTable(c, "attackSpeedByLevel"), c.CurrentLevelToIdx()) : 0
        });

        PassiveDefs["Charm_TheFlagOfCheer"] = P(new PassiveDef
        {
            Kind = PKind.Stat, Key = "ATTACKSPEED", By = "speedByLevel", CurFunc = (c, a) => 0,
            Factor = (c, a) => Math.Min(1, Num(c, "flagTime") / Math.Max(1, TimerSeconds(c, "coolDownTimer"))) * FlagInRange
        });

        PassiveDefs["Charm_Wings"] = P(new PassiveDef { Kind = PKind.PerStat, Key = "ALLDAMAGEBONUS", ConstField = "statMultiplier", PerKey = "ATTACKSPEED", DivBy = "attackSpeedUnitByLevel", Cur = "addedStatValue" });
    }
}
