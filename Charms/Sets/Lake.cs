using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterLake()
    {
        PassiveDefs["Charm_IncreaseMPRegen"] = P(new PassiveDef { Kind = PKind.Stat, Key = "MPREGEN", By = "addMPRegenByLevel" });

        PassiveDefs["Charm_IncreaseMpRegenOnGuard"] = P(new PassiveDef
        {
            Kind = PKind.Stat, Key = "MPREGEN", By = "addMapRegenByLevel",
            Factor = (c, a) => buildWeapon is WeaponSimple_SwordAndShield ? buildGuardHold : 0,
            CurFunc = (c, a) => Num(c, "onApplied") > 0 ? SafeAt(LevelTable(c, "addMapRegenByLevel"), c.CurrentLevelToIdx()) : 0
        });

        ModelHook<Charm_MpHeal>((c, item, d) => d.MpHeals.Add(new MpHeal
        {
            Item = item, Percent = c.mpHealAmountByLevel.Select(v => (float)v).ToArray(),
            Cooldown = c.coolDownTimer != null && c.coolDownTimer.time > 0 ? c.coolDownTimer.time : 30
        }));

        PassiveDefs["Charm_CarrotCharm"] = P(new PassiveDef { Kind = PKind.Stat, Key = MaxMpKey, By = "addMpByLevel" });

        PassiveDefs["Charm_IncreaseMP"] = P(new PassiveDef { Kind = PKind.Stat, Key = MaxMpKey, By = "addMPByLevel" });

        CharmMechs["Charm_LakeSpirit"] = new() { Trig = Trig.Active, TimerField = "coolDownTimer", Note = Tr("trigger.active_water_spirit_cast") };
        Extra<Charm_LakeSpirit>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            var ls = (Charm_LakeSpirit)c;
            AddX(s, new XMod { Kind = XKind.MaxMpMul });
            if (ls.critDamageAmpByLevel != null) s.CritDmgAmp = ls.critDamageAmpByLevel.Select(v => (float)v).ToArray();
        });
        ModelHook<Charm_LakeSpirit>((c, item, d) => d.MpDrains.Add(new MpDrain { Item = item, MaxMpShare = c.mpPercent / 100.0 }));

        PassiveDefs["Charm_WaterBag"] = P(new PassiveDef
        {
            Kind = PKind.Stat, Key = MaxMpKey, By = "mpStackByLevel",
            Transform = (c, a, v) => { double st = Num(c, "currentStack"); return v.Select(x => (float)Math.Min(st, x)).ToArray(); }
        });
    }
}
