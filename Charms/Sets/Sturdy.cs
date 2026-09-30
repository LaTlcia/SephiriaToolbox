using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterSturdy()
    {
        PassiveDefs["Charm_Shieldmate"] = P(new PassiveDef { Kind = PKind.Stat, Key = "SPECIALATTACKCOSTREDUCTION", By = "sweepCostReductionByLevel" });

        BuffCharms["Charm_AddBuffFromBlocking"] = new() { AmpBy = "basicAttackDamageByLevel", Rate = (c, a, b) => GuardRate(c, b) };

        PassiveDefs["Charm_BurnWeaponDamage"] = P(new PassiveDef { Kind = PKind.PerDebuffStack, Key = "FINALWEAPONDAMAGE", By = "addDamageByStackedBurn", Cur = "added" });

        PassiveDefs["Charm_DashAttackStack"] = P(new PassiveDef
        {
            Kind = PKind.Stat, Key = "FINALWEAPONDAMAGE", By = "addByLevel", Cur = "currentBonus",
            Transform = (c, a, v) =>
            {
                var max = LevelTable(c, "maxByLevel");
                double tick = Math.Max(0.1, TimerSeconds(c, "tickTimer")), gap = 1 / buildWeaponHits;
                return v.Select((x, l) => (float)Math.Min(SafeAt(max, l), x * gap / tick)).ToArray();
            }
        });

        CharmMechs["Charm_FireBulletOnHit"] = new() { Trig = Trig.Damaged, Cap = 2f, CountBy = "countByLevel", Note = Tr("trigger.fires_when_you_are") };

        PassiveDefs["Charm_GrowthCrossbow"] = P(
            new PassiveDef { Kind = PKind.Stat, Key = "BASICATTACKDAMAGEBONUS", By = "basicAttackDamageByLevel", Uptime = UpReload, Cur = "addedBasicDamage", Factor = (c, a) => Num(c, "hasReloadBuff") },
            new PassiveDef { Kind = PKind.Stat, Key = "SPECIALATTACKDAMAGEBONUS", By = "specialAttackDamageByLevel", Uptime = UpReload, Cur = "addedSpecialDamage", Factor = (c, a) => Num(c, "hasReloadBuff") });

        CharmMechs["Charm_GrowthParry"] = new() { Trig = Trig.Parry, Note = Tr("trigger.fires_sword_soul_parry") };
        Extra<Charm_GrowthParry>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            var gp = (Charm_GrowthParry)c;
            if (!gp.hasExtraTriggers) return;
            double finalRate = b.ComboLength > 0 ? b.Swing / b.ComboLength : 0;
            double cd = gp.extraTriggerCooldownTimer != null && gp.extraTriggerCooldownTimer.time > 0 ? gp.extraTriggerCooldownTimer.time : 0.32;
            double furyRate = b.Weapon == EWeaponType.Dagger ? Math.Min(b.Special * b.HitsPerSwing, 1 / cd) : 0;
            AddX(s, new XMod { Kind = XKind.ExtraTriggers, A = finalRate / baseRate, B = furyRate / baseRate });
            s.Note = Tr("trigger.fires_sword_soul_parries");
        });

        CharmMechs["Charm_GuardCounter"] = new() { Trig = Trig.Guard, Note = Tr("trigger.counterattacks_successful_block") };
        Extra<Charm_GuardCounter>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            var gc = (Charm_GuardCounter)c;
            if (!string.IsNullOrEmpty(gc.ripostelaserStatId))
                AddX(s, new XMod { Kind = XKind.DmgIf, Key = key(gc.ripostelaserStatId.ToUpperInvariant()), A = gc.ripostelaserDamageRatio });
        });

        PassiveDefs["Charm_IncreaseLastAttackDamage_SwordAndShield"] = P(new PassiveDef { Kind = PKind.Stat, Key = FinalDmgKey, By = "damagePercentByLevel", CurFunc = (c, a) => 0 });

        PassiveDefs["Charm_KatanaEnhancedDashAttackActivator"] = P(new PassiveDef
        {
            Kind = PKind.Stat, Key = DashDmgKey, By = "damagePercentByLevel", CurFunc = (c, a) => 0,
            Factor = (c, a) => buildWeapon is WeaponSimple_Katana
                ? Math.Min(1, 1 / (Math.Max(0.1, TimerSeconds(c, "enhancedAttackTimer")) * Math.Max(0.05, buildDashAttack))) : 0
        });

        PassiveDefs["Charm_SweepRange"] = P(new PassiveDef { Kind = PKind.Stat, Key = "SPECIALATTACKDAMAGEBONUS", By = "sweepDamageByLevel", Cur = "damageAdded" });
    }
}
