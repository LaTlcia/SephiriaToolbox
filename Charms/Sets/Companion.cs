using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterCompanion()
    {
        PassiveDefs["Charm_FollowerAttackSpeed"] = P(new PassiveDef { Kind = PKind.Stat, Key = FollowerAsKey, By = "attackSpeedPercentByLevel", CurFunc = (c, a) => 0 });

        CharmMechs["Charm_LeadNPC"] = new() { Trig = Trig.Summon, BonusBy = "levelBonusByLevel", Note = Tr("trigger.companion_attacks") };
        Extra<Charm_LeadNPC>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            AddRevive(s, c, key);
        });

        CharmMechs["Charm_MiniBallista"] = new() { Trig = Trig.Summon, CountBy = "bulletCountByLevel", Note = Tr("trigger.ballista_attacks") };
        Extra<Charm_MiniBallista>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            AddX(s, new XMod { Kind = XKind.RateAddIf, Key = key("ENHANCEDMINIBALLISTA"), A = b.Guard / Math.Max(0.1, PlaySummon) });
            AddX(s, new XMod { Kind = XKind.DmgIf, Key = key("ENHANCEDMINIBALLISTA"), A = 1.3 });
            AddRevive(s, c, key);
        });

        CharmMechs["Charm_SummonUnit"] = new() { Trig = Trig.Summon, Note = Tr("trigger.summon_attacks") };
        Extra<Charm_SummonUnit>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            var su = (Charm_SummonUnit)c;
            if (Num(su, "isJellyfish") > 0)
            {
                AddX(s, new XMod { Kind = XKind.DmgPct, Key = key("JELLYFISHBASICDAMAGE") });
                AddX(s, new XMod { Kind = XKind.DmgIf, Key = key("JELLYFISHDOUBLEATTACK"), A = 2 });
            }
            AddRevive(s, c, key);
        });
    }
}
