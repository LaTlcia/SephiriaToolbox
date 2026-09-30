using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void RegisterFrost()
    {
        CharmMechs["Charm_AirSlash"] = new() { Trig = Trig.Charge, AmpKey = "CHARGINGCHARMAMPLIFY", Note = Tr("trigger.fires_sword_wave_normal") };

        CharmMechs["Charm_Guillotine"] = new() { Trig = Trig.Periodic, Base = 1f / 6f, Per = 2.5f, HasteKey = "CHARGINGCHARMBONUS", AmpKey = "CHARGINGCHARMAMPLIFY", Note = Tr("trigger.summons_blades_every_6") };
        Extra<Charm_Guillotine>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            var g = (Charm_Guillotine)c;
            if (g.shockCooldownReduction > 0 && g.triggerCooldown > 0)
                AddX(s, new XMod { Kind = XKind.ElecCdr, Key = key("CHARGINGCHARMBONUS"), A = g.shockCooldownReduction, B = g.triggerCooldown });
        });

        CharmMechs["Charm_IceBow"] = new() { Trig = Trig.Charge, Per = 6f, HasteKey = "CHARGINGCHARMBONUS", AmpKey = "CHARGINGCHARMAMPLIFY", Note = Tr("trigger.fires_volley_arrows_after") };

        CharmMechs["Charm_IceHammer"] = new() { Trig = Trig.Charge, AmpKey = "CHARGINGCHARMAMPLIFY", Note = Tr("trigger.throws_hammer_when_dashing") };
        Extra<Charm_IceHammer>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            AddX(s, new XMod { Kind = XKind.RateAddIf, Key = key("DASHATTACKICEHAMMER"), A = PlayDash * 0.6 / baseRate });
        });

        CharmMechs["Charm_IceSpear"] = new() { Trig = Trig.Charge, CountBy = "fireCountByLevel", AmpKey = "CHARGINGCHARMAMPLIFY", Note = Tr("trigger.spear_attack_when_fully") };
        Extra<Charm_IceSpear>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            int per = 10;
            try { per = Math.Max(1, KeywordDatabase.GetConstValue("staffAttackToActiveIceSpearCount")); } catch { }
            AddX(s, new XMod { Kind = XKind.RateAddIf, Key = key("ICESPEARWITHWEAPONATTACK"), A = (swingHits + (b.Special + b.DashAttack + b.Strike) * b.HitsPerSwing) / per / baseRate });
        });

        CharmMechs["Charm_IceSword"] = new() { Trig = Trig.Always, Base = 1f, Note = Tr("trigger.frost_relic_activations_create") };
        ModelHook<Charm_IceSword>((c, item, d) => d.MpDrains.Add(new MpDrain
        {
            Item = item, PerUse = c.mpCost, RelicSwords = true, Cap = Math.Max(1, c.swordCount) / Math.Max(0.1, c.swordLifeTime)
        }));
        Extra<Charm_IceSword>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            var isw = (Charm_IceSword)c;
            double hitsPerSword = 1.0;
            try
            {
                if (isw.iceSwordPrefab != null && isw.iceSwordPrefab.attackIntervalTime > 0) hitsPerSword = 0.5 / isw.iceSwordPrefab.attackIntervalTime;
            }
            catch { }
            AddX(s, new XMod { Kind = XKind.RelicSwords, A = Math.Max(0.1, isw.swordLifeTime), B = Math.Max(1, isw.swordCount), C = hitsPerSword });
            double defMp = 50;
            try { defMp = KeywordDatabase.GetConstValue("PLAYERDEFAULTMP"); } catch { }
            if (c.NetworkAvatar != null) AddX(s, new XMod { Kind = XKind.MaxMpScale, Levels = new[] { (float)c.NetworkAvatar.MaxMp, (float)defMp } });
            s.TheoryK = 1;
            s.HasTheory = true;
        });
    }
}
