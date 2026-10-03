using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterGlacier()
    {
        CharmMechs["Charm_CreateBulletOnSweep"] = new() { Trig = Trig.Special, Cap = 1.25f, Note = Tr("trigger.fires_special_attack_swings") };
        Extra<Charm_CreateBulletOnSweep>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            string stat = (FieldValue(c, "orbitStatId") as string) ?? "ICICLEVINEORBIT";
            AddX(s, new XMod { Kind = XKind.DmgIf, Key = key(stat.ToUpperInvariant()), A = 1.9 });
        });

        CharmMechs["Charm_EchoOfTheGlacier"] = new() { Trig = Trig.Periodic, IntervalBy = "frostbiteTimeByLevel", Targets = true, Note = Tr("trigger.inflicts_frostbite_nearby_enemies") };
        Extra<Charm_EchoOfTheGlacier>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            AddX(s, new XMod { Kind = XKind.RateAddIf, Key = key("ECHOOFTHEGLACIERPARRY"), A = b.Parry / baseRate });
        });

        CharmMechs["Charm_FreezeNormalSlash"] = new() { Trig = Trig.BasicHit, ChanceBy = "slashChanceByLevel", Weight = true, Cap = 2.5f, Note = Tr("trigger.chance_slash_normal_attack") };
        Extra<Charm_FreezeNormalSlash>((s, c, b, key, levels, baseRate, swingHits) => s.WeaponCrit = true);

        PassiveDefs["Charm_FrozenEgg"] = P(new PassiveDef { Kind = PKind.Crit, By = "criticalChanceByLevel", Elem = 2 });

        BuffCharms["Charm_FrozenMemory"] = new() { Field = "frostTouchBuffPrefab", StackBy = "stackByLevel", Rate = (c, a, b) => b.FrostbiteRate, Trigger = 1 };

        CharmMechs["Charm_IceBat"] = new() { Trig = Trig.Periodic, TimerField = "attackTimer", ChanceBy = "attackSpeedBonusByLevel", Stat = RateStat.AttackSpeed, Note = Tr("trigger.attacks_frostbitten_enemies_every") };
        Extra<Charm_IceBat>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            AddX(s, new XMod { Kind = XKind.NeedDebuff, A = (double)DebuffType.Frostbite });
        });

        PassiveDefs["Charm_WarmGlove"] = P(new PassiveDef { Kind = PKind.Mul, By = "damageBonusByLevel", DebuffUp = 2, Elem = 2 });
    }
}
