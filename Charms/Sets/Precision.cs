using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterPrecision()
    {
        PassiveDefs["Charm_CritAndRanged"] = P(new PassiveDef
        {
            Kind = PKind.Stat, Key = "CRITICALDAMAGEBONUS", ConstField = "critDamageOnApplied",
            Factor = (c, a) => CloseUptime(a, Num(c, "disableRange")),
            CurFunc = (c, a) => Num(c, "applied") > 0 ? Num(c, "critDamageOnApplied") : 0
        });

        PassiveDefs["Charm_CriticalChanceIncreaseWithTablets"] = P(new PassiveDef { Kind = PKind.PerTablet, Key = "CRITICAL", By = "criticalBonusByLevel", Cur = "enabledCriticalBonus" });

        PassiveDefs["Charm_FinalComboCritical"] = P(new PassiveDef { Kind = PKind.Stat, Key = FinalCritKey, By = "criticalBonusPercentByLevel", CurFunc = (c, a) => 0 });

        BuffCharms["Charm_GainBuffOnMPLoss"] = new() { AmpField = "buffAmplify", Rate = (c, a, b) => b.MpPerSec / Math.Max(1, Num(c, "requiredMP")) };

        PassiveDefs["Charm_IncreaseCriticalChance_NormalAttack"] = P(new PassiveDef { Kind = PKind.Stat, Key = "WEAPONCRITICAL", By = "criticalBonusPercentByLevel", Scale = 100, CurFunc = (c, a) => 0 });

        CharmMechs["Charm_Kunai"] = new() { Trig = Trig.Swing, ChanceBy = "throwChanceByLevel", Weight = true, CapTimer = "cooldownTimer", Note = Tr("trigger.chance_throw_kunai_weapon") };

        PassiveDefs["Charm_PointedBat"] = P(new PassiveDef { Kind = PKind.Mul, Factor = (c, a) => -Num(c, "chance") * Num(c, "damageDecreaseRatio") / 100, OwnOnly = true });

        PassiveDefs["Charm_Rapier"] = P(
            new PassiveDef { Kind = PKind.Stat, Key = "BASICATTACKDAMAGEBONUS", By = "basicDamageByLevel", Cur = "bAdded" },
            new PassiveDef { Kind = PKind.Stat, Key = "SPECIALATTACKDAMAGEBONUS", By = "specialDamageByLevel", Cur = "sAdded" },
            new PassiveDef { Kind = PKind.Stat, Key = "DASHATTACKDAMAGEBONUS", By = "dashDamageByLevel", Cur = "dAdded" });

        CharmMechs["Charm_Reddew"] = new() { Trig = Trig.Crit, CapTimer = "coolDownTimer", Targets = true, Note = Tr("trigger.area_damage_crits_cooldown") };
        Extra<Charm_Reddew>((s, c, b, key, levels, baseRate, swingHits) => s.NoCrit = true);

        PassiveDefs["Charm_ScytheOfBerut"] = P(new PassiveDef { Kind = PKind.Stat, Key = "EXECUTION", Const = 1 });

        PassiveDefs["Charm_TooCloseDamage"] = P(new PassiveDef { Kind = PKind.Mul, By = "additionalDamagePercentByLevel", Factor = (c, a) => CloseUptime(a, Num(c, "range")), OwnOnly = true });

        PassiveDefs["Charm_WarmStone"] = P(new PassiveDef { Kind = PKind.Stat, Key = "CRITICALDAMAGEBONUS", By = "criticalDamageByLevel" });
    }

    static float ReddewDamage(Charm_Reddew rd, UnitAvatar avatar, int level)
    {
        int hi = Math.Max(Math.Max(avatar.GetCustomStat(ECustomStat.PhysicalDamage), avatar.GetCustomStat(ECustomStat.FireDamage)),
                          Math.Max(avatar.GetCustomStat(ECustomStat.IceDamage), avatar.GetCustomStat(ECustomStat.LightningDamage)));
        var t = rd.damagePercentByLevel;
        return t == null || t.Length == 0 ? 0f : hi * t[Math.Min(Math.Max(level, 0), t.Length - 1)] / 100f;
    }
}
