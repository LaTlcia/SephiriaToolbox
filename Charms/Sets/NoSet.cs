using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterNoSet()
    {
        CharmMechs["Charm_Golem_Gun"] = new() { Trig = Trig.Periodic, TimerField = "defaultAttackIntervalTimer", Stat = RateStat.AttackSpeed, Note = Tr("trigger.fires_shot_interval_while") };

        ModelHook<Charm_CrossbowDashAmmo>((c, item, d) =>
        {
            var t = LevelTable(c, "ammoByLevel");
            if (t != null) d.Weapon.XbDashAmmo.Add((item, t));
        });

        PassiveDefs["Charm_MiniBossFight"] = P(new PassiveDef
        {
            Kind = PKind.Stat, Key = "ALLDAMAGEBONUS", By = "addDamageByLevel", Cur = "added",
            Factor = (c, a) =>
            {
                var dm = DungeonManager.Instance;
                int kills = dm != null && dm.dungeonEnvironment.TryGetValue("MiniBossKillCount", out var k) ? k : 0;
                try { kills = Math.Min(kills, KeywordDatabase.GetConstValue("miniBossKillCharmCountLimit")); } catch { }
                return kills;
            }
        });

        PassiveDefs["Charm_IncreaseMoveSpeed"] = P(new PassiveDef { Kind = PKind.Stat, Key = MoveSpeedKey, By = "moveSpeedByLevel", Scale = 100 });

        CharmMechs["Charm_NearMagicBullet"] = new() { Trig = Trig.MagicCast, Note = Tr("trigger.fireworks_when_magic_same") };
    }
}
