using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterUnused()
    {
        CharmMechs["Charm_AttackSummon"] = new() { Trig = Trig.Summon, InverseBy = "damageCutByLevel", Note = Tr("trigger.summons_skeleton_once_enough") };

        CharmMechs["Charm_BurnExplosion"] = new() { Trig = Trig.Kill, Per = 0.5f, Targets = true, Note = Tr("trigger.burning_enemies_explode_death") };

        PassiveDefs["Charm_ChangeATKDMGToAPDMG"] = P(
            new PassiveDef { Kind = PKind.Stat, Key = "WEAPONDAMAGEBONUS", By = "bonusByLevel", Scale = -1 },
            new PassiveDef { Kind = PKind.Stat, Key = "MAGICDAMAGEBONUS", By = "bonusByLevel" });

        BuffCharms["Charm_DebuffToBuff"] = new() { Rate = (c, a, b) => b.IceShare > 0.3 ? PlayFreeze : PlayFreeze / 5, Trigger = 0 };

        PassiveDefs["Charm_DecreaseFallingDamage"] = P(new PassiveDef { Kind = PKind.Stat, Key = "ICEDAMAGE", By = "iceDamageBonusByLevel" });

        CharmMechs["Charm_FlameBall"] = new() { Trig = Trig.Swing, Note = Tr("trigger.fires_fireball_every_weapon") };

        PassiveDefs["Charm_FlameGroundKillCrit"] = P(new PassiveDef
        {
            Kind = PKind.Stat, Key = "CRITICAL", By = "critLimitByLevel", Cur = "addedCritical",
            Transform = (c, a, v) => { double kills = Num(c, "killCount"); return v.Select(x => (float)(Math.Min(kills, x) * 100)).ToArray(); }
        });

        PassiveDefs["Charm_GoldIsDamage"] = P(new PassiveDef
        {
            Kind = PKind.Stat, Key = "ALLDAMAGEBONUS", By = "damagePercentByLevel",
            Factor = (c, a) => Math.Floor(a.Money / Math.Max(1.0, Num(c, "perGold"))),
            CurFunc = (c, a) => Num(c, "curGoldPower") * SafeAt(LevelTable(c, "damagePercentByLevel"), c.CurrentLevelToIdx())
        });

        BuffCharms["Charm_GuardBuff"] = new() { AmpBy = "amplificationByLevel", Rate = (c, a, b) => GuardRate(c, b) };

        PassiveDefs["Charm_GuardSmite"] = P(new PassiveDef { Kind = PKind.Stat, Key = "WEAPONDAMAGEBONUS", By = "damageBonusByLevel", Uptime = UpGuardSmite, Cur = "added" });

        PassiveDefs["Charm_IncreaseBasicAttackDamage"] = P(new PassiveDef { Kind = PKind.Stat, Key = "BASICATTACKDAMAGEBONUS", By = "incrementByLevel" });

        PassiveDefs["Charm_IncreaseCooldownRecoverySpeed"] = P(new PassiveDef { Kind = PKind.Stat, Key = "COOLDOWNRECOVERYSPEED", By = "cooldownSpeedByLevel" });

        PassiveDefs["Charm_MagicDamageBySpeed"] = P(new PassiveDef
        {
            Kind = PKind.Stat, Key = "MAGICDAMAGEBONUS", By = "magicDamageByLevel", Cur = "addedDamage",
            Transform = (c, a, v) => { double speed = Math.Max(0, Math.Round((a.moveSpeedMultiplier - 1) * 100)); return v.Select(x => (float)Math.Min(speed, x)).ToArray(); }
        });

        PassiveDefs["Charm_PerfectGuardDamageQuest"] = P(new PassiveDef { Kind = PKind.Stat, Key = "PHYSICALDAMAGE", ConstField = "killCount", Cur = "addedDamage" });

        PassiveDefs["Charm_ShieldDamageBonus"] = P(new PassiveDef { Kind = PKind.Mul, By = "shieldDamageBonusRatioByLevel", Uptime = UpShield, Factor = (c, a) => a.Shield / 100.0 });

        CharmMechs["Charm_VenomSporePouch"] = new() { Trig = Trig.Parry, Note = Tr("trigger.counterattacks_parry") };
    }
}
