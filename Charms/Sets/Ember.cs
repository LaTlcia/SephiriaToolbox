using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterEmber()
    {
        PassiveDefs["Charm_Burn"] = P(new PassiveDef { Kind = PKind.CritDmg, By = "addCriticalDamageByLevel", ElemField = "targetElementalType" });

        PassiveDefs["Charm_BurnTargetDamageBonus"] = P(new PassiveDef { Kind = PKind.Mul, By = "damageBonusByLevel", DebuffUp = 0, Direct = true });

        CharmMechs["Charm_FireFeather"] = new() { Trig = Trig.Periodic, TimerField = "featherEnableTimer", TimerExtra = 0.5f, TargetsBy = "numberOfTargetByLevel", Note = Tr("trigger.shoots_feathers_every_3") };

        CharmMechs["Charm_FlameGround_Meteor"] = new() { Trig = Trig.Periodic, TimerField = "cooldownTimer", ExtraField = "searchTimer", TimerExtra = 0.75f, CountBy = "countByLevel", Note = Tr("trigger.when_off_cooldown_finds") };
        Extra<Charm_FlameGround_Meteor>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            AddX(s, new XMod
            {
                Kind = XKind.CdSpeed, Key = key("REDSNAKEEYEATKCOOLDOWNBONUS"),
                A = Math.Max(0.1, TimerSeconds(c, "cooldownTimer")), B = TimerSeconds(c, "searchTimer") is var st && st > 0 ? st : 0.75,
                C = b.Swing, D = b.Special + b.DashAttack + b.Strike
            });
        });

        CharmMechs["Charm_FlamePlantRoot"] = new() { Trig = Trig.Special, Cap = 3f, Targets = true, Note = Tr("trigger.area_flames_when_rage") };
        Extra<Charm_FlamePlantRoot>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            double fury = b.Weapon == EWeaponType.Dagger ? (b.FuryMeasured >= 0 ? b.FuryMeasured : b.Parry) : 0;
            double special = Math.Min(Math.Max(b.Special, 1e-6), 3);
            s.TheoryK *= Math.Min(3, fury) / special;
            if (fury <= 0) s.Note = Tr("trigger.needs_rage_attack");
        });
    }
}
