using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    const double ScythePassSeconds = 0.514;

    static void RegisterFrost()
    {
        CharmMechs["Charm_AirSlash"] = new() { Trig = Trig.Charge, AmpKey = "CHARGINGCHARMAMPLIFY", Note = Tr("trigger.fires_sword_wave_normal") };

        CharmMechs["Charm_Guillotine"] = new() { Trig = Trig.Periodic, Base = 1f / 6f, HasteKey = "CHARGINGCHARMBONUS", AmpKey = "CHARGINGCHARMAMPLIFY", Note = Tr("trigger.summons_blades_every_6") };
        Extra<Charm_Guillotine>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            var g = (Charm_Guillotine)c;
            if (g.triggerCooldown > 0) s.TheoryK *= 6 / g.triggerCooldown;
            double hits = Math.Max(1, Math.Min(g.bladeTargetCount, b.Enemies)), hitsM = Math.Max(1, Math.Min(g.bladeTargetCount, b.EnemiesM));
            s.TheoryK *= hits;
            s.MultiScale = hitsM / hits;
            if (g.shockCooldownReduction > 0 && g.triggerCooldown > 0)
                AddX(s, new XMod { Kind = XKind.ElecCdr, Key = key("CHARGINGCHARMBONUS"), A = g.shockCooldownReduction, B = g.triggerCooldown });
        });

        CharmMechs["Charm_IceBow"] = new() { Trig = Trig.Charge, AmpKey = "CHARGINGCHARMAMPLIFY", Note = Tr("trigger.fires_volley_arrows_after") };
        Extra<Charm_IceBow>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            var bow = (Charm_IceBow)c;
            double reload = Math.Max(0.05, bow.arrowReloadTime), gap = Math.Max(0, bow.fireInterval);
            s.TheoryK = 1 / (reload + gap);
            s.MultiScale = 1;
            s.PerUse = Math.Max(1, bow.arrowReloadLimit);
            s.HasteKey = -1;
            s.HasteKey2 = -1;
            AddX(s, new XMod { Kind = XKind.HasteFixed, Key = key("CHARGINGCHARMBONUS"), A = reload, B = gap });
        });

        CharmMechs["Charm_IceHammer"] = new() { Trig = Trig.Charge, AmpKey = "CHARGINGCHARMAMPLIFY", Note = Tr("trigger.throws_hammer_when_dashing") };
        Extra<Charm_IceHammer>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            AddX(s, new XMod { Kind = XKind.RateAddIf, Key = key("DASHATTACKICEHAMMER"), A = buildDash * 0.6 / baseRate });
            var scythe = ((Charm_IceHammer)c).bulletPrefab_Scythe;
            var move = scythe != null ? scythe.GetComponent<BulletMoveModule>() : null;
            double life = move != null && move.destroyOnTime && move.destroyTimer != null ? move.destroyTimer.time : 6;
            AddX(s, new XMod { Kind = XKind.DmgIf, Key = key("ICEHAMMERSCYTHE"), A = Math.Max(1, life / ScythePassSeconds * 0.8) });
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
            var owner = c.NetworkAvatar != null ? c.NetworkAvatar : buildOwner;
            if (owner != null) AddX(s, new XMod { Kind = XKind.MaxMpScale, Levels = new[] { (float)owner.MaxMp, (float)defMp } });
            s.TheoryK = 1;
            s.HasTheory = true;
        });
    }
}
