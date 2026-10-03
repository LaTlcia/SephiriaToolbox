using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mirror;
using UnityEngine;

public partial class SephiriaToolbox
{
    static readonly Dictionary<string, string> StatusKeys = new()
    {
        ["StatusInstance_AP"] = "AP",
        ["StatusInstance_ChargingCharmBonus"] = "CHARGINGCHARMBONUS",
        ["StatusInstance_AttackSpeed"] = "ATTACKSPEED",
        ["StatusInstance_BasicAttackDamage"] = "BASICATTACKDAMAGEBONUS",
        ["StatusInstance_BuffDuration"] = "BUFFDURATION",
        ["StatusInstance_CooldownRecoverySpeed"] = "COOLDOWNRECOVERYSPEED",
        ["StatusInstance_Critical"] = "CRITICAL",
        ["StatusInstance_CriticalDamageRate"] = "CRITICALDAMAGEBONUS",
        ["StatusInstance_DashAttackDamage"] = "DASHATTACKDAMAGEBONUS",
        ["StatusInstance_DashCount"] = "DASHCOUNT",
        ["StatusInstance_DashRecoverySpeed"] = "DASHRECOVERY",
        ["StatusInstance_DashSpeed"] = "DASHSPEEDBONUSPERCENT",
        ["StatusInstance_DebuffDuration"] = "DEBUFFDURATION",
        ["StatusInstance_Defense"] = "DAMAGEREDUCTION",
        ["StatusInstance_EXPDrop"] = "EXPDROP",
        ["StatusInstance_Evasion"] = "EVASION",
        ["StatusInstance_FinalAP"] = "FINALAP",
        ["StatusInstance_FinalDamage"] = "ALLDAMAGEBONUS",
        ["StatusInstance_FinalMP"] = "FINALMP",
        ["StatusInstance_FinalWeaponDamage"] = "FINALWEAPONDAMAGE",
        ["StatusInstance_FireDamage"] = "FIREDAMAGE",
        ["StatusInstance_HPPotionBonus"] = "HPPOTIONBONUS",
        ["StatusInstance_HPRegen"] = "HPREGEN",
        ["StatusInstance_HPSteal"] = "HPSTEAL",
        ["StatusInstance_IceDamage"] = "ICEDAMAGE",
        ["StatusInstance_LeafDrop"] = "MONEYDROP",
        ["StatusInstance_LightningDamage"] = "LIGHTNINGDAMAGE",
        ["StatusInstance_Luck"] = "LUCK",
        ["StatusInstance_MPPotionBonus"] = "MPPOTIONBONUS",
        ["StatusInstance_MPRegen"] = "MPREGEN",
        ["StatusInstance_MPSteal"] = "MPSTEAL",
        ["StatusInstance_MagicCritical"] = "MAGICCRITICAL",
        ["StatusInstance_MagicCriticalDamageRate"] = "MAGICCRITICALDAMAGEBONUS",
        ["StatusInstance_MinDarkCloud"] = "MINDARKCLOUD",
        ["StatusInstance_Negotiation"] = "NEGOTIATION",
        ["StatusInstance_PhysicalDamage"] = "PHYSICALDAMAGE",
        ["StatusInstance_SpecialAttackDamage"] = "SPECIALATTACKDAMAGEBONUS",
        ["StatusInstance_SweepCostReduction"] = "SWEEPCOSTREDUCTION",
        ["StatusInstance_Thorns"] = "THORNS",
        ["StatusInstance_TrueDamage"] = "TRUEDAMAGE",
        ["StatusInstance_MaxMP"] = MaxMpKey,
        ["StatusInstance_MaxHP"] = MaxHpKey,
        ["StatusInstance_MaxHPNoRatio"] = MaxHpKey,
        ["StatusInstance_FinalHP"] = FinalHpKey,
        ["StatusInstance_FlameGround_Duration"] = GroundDurKey,
        ["StatusInstance_FlameGround_Range"] = GroundRangeKey,
        ["StatusInstance_MoveSpeed"] = MoveSpeedKey,
    };

    static bool MapStatus(string statusID, out string key, out byte mode)
    {
        key = null;
        mode = 0;
        var e = StatusDatabase.GetStatusEntity(statusID);
        if (e == null || string.IsNullOrEmpty(e.className)) return false;
        var parts = e.className.Split('/');
        switch (parts[0])
        {
            case "StatusInstance_Custom" when parts.Length > 1:
                key = parts[1].ToUpperInvariant();
                return true;
            case "StatusInstance_CustomAmp" when parts.Length > 1:
                key = parts[1].ToUpperInvariant();
                mode = 1;
                return true;
            case "StatusInstance_HighestElementalDamage":
                mode = 2;
                return true;
        }
        return StatusKeys.TryGetValue(parts[0], out key);
    }
}
