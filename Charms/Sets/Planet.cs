using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterPlanet()
    {
        PassiveDefs["Charm_FlamePlanet"] = P(new PassiveDef { Kind = PKind.Stat, Key = "PLANETIGNITEDAMAGE", Const = 1 });

        PassiveDefs["Charm_StatusDebuff"] = P(new PassiveDef { Kind = PKind.Stat, KeyField = "debuffStatId", Const = 1 });

        CharmMechs["Charm_SummonGreenBat"] = new() { Trig = Trig.Summon, Note = Tr("trigger.planet_attacks") };
        Extra<Charm_SummonGreenBat>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            var bat = (Charm_SummonGreenBat)c;
            s.Summon = false;
            var gb = bat.greenbatPrefab != null ? bat.greenbatPrefab.GetComponent<GreenBat>() : null;
            double interval = gb != null && gb.fireIntervalTimer != null && gb.fireIntervalTimer.time > 0 ? gb.fireIntervalTimer.time : 0.5;
            int count = gb != null ? Math.Max(1, gb.fireCount) : 1;
            s.TheoryK = count / interval;
            s.HasTheory = true;
            s.SwingBased = false;
            s.StatRate = RateStat.None;
            s.Note = Tr("trigger.fires_shot_every_s", interval, count);
            var w = b.AttackWeight;
            AddX(s, new XMod { Kind = XKind.Planet, Key = key("PLANETATTACKSPEED"), Key2 = key(nameof(ECustomStat.SUPERPLANET).ToUpperInvariant()), A = interval, B = b.Swing * w * 0.95, C = b.DashAttack * w });
            AddX(s, new XMod { Kind = XKind.DmgPct, Key = key("PLANETDAMAGE") });
        });
    }
}
