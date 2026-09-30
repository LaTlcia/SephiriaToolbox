using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public partial class SephiriaToolbox
{
    void BuildEconomy(PlayerAvatar avatar, ArrModel m, Charm_Basic[] charmOf, DpsModel d, Func<string, int> K, Behavior play,
                      List<DpsSource> sources, Dictionary<string, Dictionary<EDamageElementalType, double>> measuredIds)
    {
        var e = d.Eco = new EcoModel { Battle = play.Single && d.FightLen > 0 ? d.FightLen : BattleLength(), Enemies = play.EnemiesM };
        var weapon = ModelWeapon(avatar);
        bool melee = weapon == null || !(weapon.weaponType is EWeaponType.Crossbow or EWeaponType.StaffMagic or EWeaponType.Golem);
        double swingHits = play.Swing * play.HitsPerSwing;
        double otherHits = (play.Special + play.DashAttack + play.Strike) * play.HitsPerSwing;
        double asNow = AttackSpeedFactor(avatar.GetCustomStat(ECustomStat.AttackSpeed), WeaponAsAmp(weapon));
        double combatSec = runCombatTime;
        double MeasuredDps(params string[] ids)
        {
            if (combatSec < 20) return -1;
            double sum = 0;
            foreach (var id in ids) if (measuredIds.TryGetValue(id, out var byElem)) sum += byElem.Values.Sum();
            return sum / combatSec;
        }
        static int Levels(Charm_Basic c) => Math.Max(1, c.maxLevel + 1);

        var dcs = sources.FirstOrDefault(x => x.Ids.Contains("Ability_DarkCloud"));
        if (dcs != null)
        {
            var dc = avatar.Inventory != null ? avatar.Inventory.FindComboEffect("DARKCLOUD") as ComboEffect_DarkCloud : null;
            MakeEco(dcs, EcoKind.DarkCloud);
            dcs.DmgElem = 3;
            e.kCloudMin = K("MINDARKCLOUD"); e.kCloudScale = K("DARKCLOUDSCALE"); e.kCloudRestore = K("DARKCLOUDRESTOREDURINGBATTLE");
            e.kCloudKeep = K("DARKCLOUDKEEP"); e.kCloudMulti = K("DARKCLOUDMULTISHOT"); e.kCloudLuck = K("DARKCLOUDLUCK");
            e.kCloudDmg = K("DARKCLOUDDAMAGE"); e.kCloudSpeed = K("DARKCLOUDSPEED"); e.kCloudAsBonus = K("DARKCLOUDATKSPEEDBONUS"); e.kCloudIce = K("DARKCLOUDICE");
            try { e.CloudPct = KeywordDatabase.GetConstValue("darkCloudDamagePercent"); } catch { }
            try { e.CloudPctIce = KeywordDatabase.GetConstValue("darkCloudDamagePercentIce"); } catch { }
            if (dc != null)
            {
                if (dc.lightningIntervalSpeed > 0) e.CloudSpeed0 = dc.lightningIntervalSpeed;
                if (dc.cloudTimer != null && dc.cloudTimer.time > 0) e.CloudInterval = dc.cloudTimer.time;
                e.CloudDefault = dc.defaultDarkCloud;
                e.CloudRestorePct = dc.defaultRestorePercent;
                float rs = TimerSeconds(dc, "darkCloudGainDuringCombat");
                if (rs > 0) e.CloudRestoreSec = rs;
            }
            double lightningFollowers = sources.Count(x => x.Summon && MainElement(x.Ids, measuredIds) == 3) * PlaySummon;
            double weaponDps = MeasuredDps("Weapon_BasicAttack", "Weapon_DashAttack");
            for (int i = 0; i < charmOf.Length; i++)
            {
                var c = charmOf[i];
                if (c == null) continue;
                int L = Levels(c);
                try
                {
                    switch (c)
                    {
                        case Charm_Lightning_DarkCloudBuff:
                        {
                            var t = LevelTable(c, "darkCloudTimeByLevel");
                            if (t != null) e.CloudGen.Add(new Feeder { Item = i, PerSec = Per(L, l => SafeAt(t, l) > 0 ? 1.0 / SafeAt(t, l) : 0) });
                            break;
                        }
                        case Charm_LightningPouch:
                        {
                            var cd = LevelTable(c, "cooldownByLevel");
                            var cl = LevelTable(c, "cloudByLevel");
                            if (cl != null) e.CloudGen.Add(new Feeder { Item = i, PerSec = Per(L, l => SafeAt(cl, l) / (SafeAt(cd, l) + 1 / PlayDash)) });
                            break;
                        }
                        case Charm_LightningBread:
                        {
                            var cl = LevelTable(c, "cloudByLevel");
                            double drop = Num(c, "dropPercent") / 100.0;
                            if (cl != null) e.CloudGen.Add(new Feeder { Item = i, PerSec = Per(L, l => play.Kill * drop * BreadPickup * SafeAt(cl, l)) });
                            break;
                        }
                        case Charm_BasicLightning:
                        {
                            var cut = LevelTable(c, "damageCutByLevel");
                            var cl = LevelTable(c, "cloudByLevel");
                            double hits = swingHits * asNow + (play.DashAttack + play.Strike) * play.HitsPerSwing;
                            if (cl != null)
                                e.CloudGen.Add(new Feeder
                                {
                                    Item = i,
                                    PerSec = Per(L, l =>
                                    {
                                        double k = SafeAt(cut, l);
                                        double times = k <= 0 ? hits : weaponDps > 0 ? Math.Min(hits, weaponDps / k) : hits / 2;
                                        return times * SafeAt(cl, l);
                                    })
                                });
                            break;
                        }
                        case Charm_MiniBallista:
                        {
                            var pc = LevelTable(c, "addCloudPercentByLevel");
                            if (pc != null) e.CloudGen.Add(new Feeder { Item = i, PerSec = Per(L, l => PlaySummon * SafeAt(pc, l) / 100.0) });
                            break;
                        }
                        case Charm_Lightning_BasicAttack:
                        {
                            var pc = LevelTable(c, "lightningPercentByLevel");
                            var cd = LevelTable(c, "cooldownTimeByLevel");
                            if (pc != null)
                                e.CloudUse.Add(new Feeder
                                {
                                    Item = i, Kind = 1, Hits = swingHits, HitsFixed = (play.DashAttack + play.Strike) * play.HitsPerSwing,
                                    Chance = Per(L, l => SafeAt(pc, l) / 100.0 * play.AttackWeight), Cd = cd ?? new float[] { 1 }
                                });
                            break;
                        }
                        case Charm_CompanionCloud:
                        {
                            var cc = LevelTable(c, "cloudCountByLevel");
                            if (cc != null && lightningFollowers > 0) e.CloudUse.Add(new Feeder { Item = i, PerSec = Per(L, l => lightningFollowers * SafeAt(cc, l)) });
                            break;
                        }
                    }
                }
                catch (Exception ex) { WarnOnce("乌云 " + m.Items[i].Name, ex); }
            }
            if (weapon != null && weapon.addons != null)
                foreach (var ad in weapon.addons)
                {
                    if (ad is WeaponAddonCommon_AddDarkCloudOnAttack ao && ao.swingCountToActivate > 0)
                    {
                        double per = ao.addDarkCloudCount / (double)ao.swingCountToActivate;
                        e.CloudOtherAs += swingHits * per;
                        e.CloudOther += otherHits * per;
                    }
                    else if (ad is WeaponAddonKatana_GainDarkCloudOnLastAttack gl)
                    {
                        int combo = weapon.basicComboAttacks != null ? Math.Max(1, weapon.basicComboAttacks.Count(x => x != null)) : 3;
                        e.CloudOtherAs += play.Swing / combo * gl.gainDarkCloudCount;
                    }
                }
            if (weapon is WeaponSimple_Crossbow)
            {
                int xbCloud = StatWithWeapon(avatar, "LIGHTNINGCROSSBOW_CLOUD", weapon);
                if (xbCloud > 0)
                    e.CloudOtherAs += play.Swing * Math.Min(100, Math.Max(0, StatWithWeapon(avatar, "LIGHTNINGCROSSBOW", weapon))) / 100.0 * xbCloud;
            }
            int parry = avatar.GetCustomStatUnsafe("DARKCLOUDPARRY");
            if (parry > 0 && weapon != null && weapon.weaponType == EWeaponType.Dagger) e.CloudOther += play.Parry * parry;
            dcs.Note = Tr("economy.discharges_modeled_cloud_supply");
        }

        var fss = sources.FirstOrDefault(x => x.Ids.Contains("Ability_FlameSword"));
        if (fss != null)
        {
            var fs = avatar.Inventory != null ? avatar.Inventory.FindComboEffect("FLAMESWORD") as ComboEffect_FlameSword : null;
            MakeEco(fss, EcoKind.FlameSword);
            fss.NoCrit = false;
            e.kFsDmg = K("FLAMESWORDDAMAGE"); e.kFsCrit = K("FLAMESWORDCRITICAL"); e.kFsCritDmg = K("FLAMESWORDCRITICALDAMAGERATE");
            e.kFsLuck = K("FLAMESWORDLUCK"); e.kFsMagic = K("FLAMESWORDMAGICDAMAGE"); e.kFsMax = K("FLAMESWORDMAX"); e.kFsPick = K("FLAMESWORDPICKBONUS");
            e.kFsReturn = K("FLAMESWORDRETURN"); e.kFsFall = K("FLAMESWORDFASTFALL"); e.kFsAdd = K("FLAMESWORDADDITIONALATTACK");
            e.kFsAddW = K("FLAMESWORDADDITIONALATTACKFROMWEAPON"); e.kFsAddM = K("FLAMESWORDADDITIONALATTACKFROMMAGIC"); e.kFsFrost = K("FLAMESWORDFROST");
            e.kFsRelic = K("FLAMESWORDCALLBACKFROST");
            fss.DmgElem = avatar.GetCustomStatUnsafe("FLAMESWORDFROST") > 0 ? 2 : 1;
            try { e.FsLuckBonus = KeywordDatabase.GetConstValue("flameSwordLuckBonusDamagePercent"); } catch { }
            if (fs != null)
            {
                if (fs.damagePercent > 0) e.FsPct = fs.damagePercent;
                e.FsMinCd = Math.Max(0.02, fs.minCooldownTime);
                if (fs.defaultMaxSword > 0) e.FsMax0 = fs.defaultMaxSword;
                var pick = fs.flameSwordPickPrefab != null ? fs.flameSwordPickPrefab.GetComponent<FlameSwordPickLocal>() : null;
                if (pick != null && pick.lifeTime > 0) e.FsLife = pick.lifeTime;
            }
            e.FsWalk = melee ? FsWalkMelee : FsWalkRanged;
            e.FsHitsSwing = swingHits;
            e.FsHitsOther = otherHits;
            for (int i = 0; i < charmOf.Length; i++)
                if (charmOf[i] is Charm_FlameSwordAuto auto)
                {
                    var t = LevelTable(auto, "restoreFlameSwordTimeByLevel");
                    if (t != null) e.FsGen.Add(new Feeder { Item = i, PerSec = Per(Levels(auto), l => SafeAt(t, l) > 0 ? 1.0 / SafeAt(t, l) : 0) });
                }
            fss.Note = Tr("economy.fired_weapon_magic_hits", e.FsPct);
        }

        var gate = new List<int>();
        for (int i = 0; i < charmOf.Length; i++) if (charmOf[i] != null && charmOf[i].flameGround) gate.Add(i);
        e.GroundGate = gate.ToArray();
        if (gate.Count > 0)
        {
            bool weaponFire = weapon != null && weapon.basicComboAttacks != null && weapon.basicComboAttacks.Any(a => a != null && a.damageElementalType == EDamageElementalType.Fire);
            double fireHits = (weaponFire ? swingHits * asNow + otherHits : 0) + (fss != null ? 1.0 : 0) + 0.3;
            for (int i = 0; i < charmOf.Length; i++)
            {
                var c = charmOf[i];
                if (c == null) continue;
                int L = Levels(c);
                if (c is Charm_FireDamageGround)
                {
                    var pc = LevelTable(c, "fireDamagePercent");
                    float cell = (float)Num(c, "cell");
                    if (pc != null) e.GroundGen.Add(new Feeder { Item = i, PerSec = Per(L, l => fireHits * SafeAt(pc, l) / 100.0), Radius = new[] { cell > 0 ? cell : 1.5f } });
                }
                else if (c is Charm_FlameWeapon)
                {
                    var r = LevelTable(c, "radiusByLevel");
                    if (r != null) e.GroundGen.Add(new Feeder { Item = i, PerSec = Per(L, l => play.Special), Radius = r, AtPlayer = true });
                }
            }
            e.kGroundDur = K(GroundDurKey);
            e.kGroundRange = K(GroundRangeKey);
            e.kDebuffDmg = K("DEBUFFDAMAGE");
            var fg = avatar.GetFlameGround();
            d.ExtraBase[e.kGroundDur] = fg != null ? fg.durationPercent : 100;
            d.ExtraBase[e.kGroundRange] = fg != null ? fg.rangePercent : 100;
            var gs = sources.FirstOrDefault(x => x.Ids.Contains("FlameGround"));
            if (gs == null && e.GroundGen.Count > 0)
            {
                gs = new DpsSource { Kind = SrcKind.Ability, Name = Tr("src.ability", DamageIdName("FlameGround")) };
                gs.Ids.Add("FlameGround");
                sources.Add(gs);
            }
            if (gs != null)
            {
                MakeEco(gs, EcoKind.FlameGround);
                gs.DmgElem = 1;
                double measured = MeasuredDps("FlameGround");
                if (measured > 0)
                {
                    double perTick = (2 + 0.5 * avatar.GetCustomStat(ECustomStat.FireDamage)) * Pct(avatar.GetCustomStatUnsafe("DEBUFFDAMAGE"))
                                   * Pct(avatar.GetCustomStat(ECustomStat.AllDamageBonus));
                    double ticks = GroundTicksPerArea(fg != null ? fg.durationPercent : 100);
                    double range = (fg != null ? fg.rangePercent : 100) / 100.0;
                    double known = 0;
                    foreach (var f in e.GroundGen)
                    {
                        var c = charmOf[f.Item];
                        if (c == null || !c.IsEffectEnabled) continue;
                        int idx = c.CurrentLevelToIdx();
                        known += SafeAt(f.PerSec, idx) * ticks * GroundCover(SafeAt(f.Radius, idx) * range, f.AtPlayer, melee, e.Enemies);
                    }
                    if (perTick > 0) e.GroundOther = Math.Max(0, measured / perTick - known);
                }
                gs.Note = Tr("economy.ticks_every_0_25");
            }
        }

        e.SwingHits = swingHits;
        e.OtherHits = otherHits + 1.0;
        bool trueNow = avatar.GetCustomStatUnsafe("TRUEDAMAGE") > 0;
        bool trueChanges = d.Adds.Any(list => list != null && list.Any(a => a.Key == d.kTrue));
        if (trueNow || trueChanges)
        {
            var ts = new DpsSource { Kind = SrcKind.Ability, Name = Tr("src.ability_true_damage"), NoCrit = true, Note = Tr("economy.each_hit_true_damage") };
            MakeEco(ts, EcoKind.TrueDamage);
            sources.Add(ts);
        }

        try { BuildDebuffs(avatar, m, charmOf, d, K, play, sources); }
        catch (Exception ex) { WarnOnce("减益模型", ex); d.Debuffs.Clear(); foreach (var s in sources) if (s.Eco == EcoKind.Debuff) s.Eco = EcoKind.None; }
        try { BuildDebuffLinks(avatar, m, charmOf, d, e, sources); }
        catch (Exception ex) { WarnOnce("减益联动", ex); }

        try
        {
            int multi = 1;
            var sc = avatar.GetComponent<SkillController>();
            if (sc != null) multi = Math.Max(1, sc.GetMultipleCastCount());
            int maxMp = avatar.MaxMp;
            for (int i = 0; i < charmOf.Length; i++)
                if (charmOf[i] is Charm_MPMultipleCast mc && mc.multipleCastMPThresholdByLevel != null)
                {
                    var th = mc.multipleCastMPThresholdByLevel.Select(x => (float)x).ToArray();
                    d.MultiCast.Add(new Feeder { Item = i, Chance = th, Mul = mc.multicast });
                    if (mc.IsEffectEnabled && maxMp >= SafeAt(th, mc.CurrentLevelToIdx())) multi -= mc.multicast;
                }
            d.MultiBase = Math.Max(1, multi);
            if (d.MultiCast.Count > 0) m.Notes.Add(Tr("economy.multicast_mp_threshold", d.MultiCast.Count));
        }
        catch (Exception ex) { WarnOnce("多重施放", ex); }

        m.Notes.Add(EnemiesMeasured(out _) ? Tr("economy.average_enemies_nearby_measured", e.Enemies) : Tr("economy.assuming_enemies_nearby_measured", e.Enemies));
        if (play.Single) m.Notes.Add(Tr("economy.single_target_boss_evaluation"));
        if (dcs != null || fss != null)
            m.Notes.Add(battleCount >= 3 && battleSeconds >= 30 ? Tr("economy.battles_last_s_average", e.Battle) : Tr("economy.assuming_s_per_battle", e.Battle));
        int feeders = e.CloudGen.Count + e.CloudUse.Count + e.FsGen.Count + e.GroundGen.Count;
        if (feeders > 0) m.Notes.Add(Tr("economy.cloud_discharge_blade_return", feeders));
    }

    void BuildDebuffLinks(PlayerAvatar avatar, ArrModel m, Charm_Basic[] charmOf, DpsModel d, EcoModel e, List<DpsSource> sources)
    {
        var chim = sources.FirstOrDefault(x => x.Kind == SrcKind.Ability && x.Ids.Contains("Debuff_Chim"));
        if (chim != null)
        {
            chim.Flat = true;
            chim.ElemIdx = -1;
            chim.MulKeys = new[] { d.kDebuff, d.kPoison };
            chim.Slopes = null;
            chim.Table = null;
            chim.HasTheory = false;
            var appliers = Enumerable.Range(0, charmOf.Length).Where(i => charmOf[i] is Charm_FarChimDamage).ToArray();
            if (appliers.Length > 0) chim.GateItems = appliers;
            chim.Note = Tr("economy.fixed_damage_20_per");
        }
        var debuffs = sources.Where(x => x.Kind == SrcKind.Ability && x.Ids.Any(id => id.StartsWith("Debuff", StringComparison.Ordinal))).ToList();
        if (debuffs.Count == 0) return;
        for (int i = 0; i < charmOf.Length; i++)
        {
            var c = charmOf[i];
            if (c is Charm_AttackChim)
            {
                var pc = LevelTable(c, "chancePercentByLevel");
                if (pc == null) continue;
                int L = Math.Max(1, c.maxLevel + 1);
                foreach (var s in debuffs)
                {
                    bool dot = !s.Ids.Contains("Debuff_Chim");
                    double b0 = DotTargetShare * e.Enemies;
                    var boost = Per(L, l =>
                    {
                        double p = Math.Max(0, SafeAt(pc, l)) / 100.0;
                        return dot ? Math.Min(e.Enemies, b0 + p * 5) / b0 - 1 : p * 0.5;
                    });
                    (s.Boost ??= new List<Feeder>()).Add(new Feeder { Item = i, PerSec = boost, MultiOnly = true });
                }
            }
        }
    }

    static void MakeEco(DpsSource s, EcoKind kind)
    {
        s.Eco = kind;
        s.TheoryK = 1;
        s.HasTheory = true;
        s.SwingBased = false;
        s.StatRate = RateStat.None;
        s.ElemIdx = -1;
        s.MulKeys = null;
        s.Slopes = null;
        s.Table = null;
        s.Rate = null;
    }

    static double GroundTicksPerArea(double durationPercent) => Math.Max(1, Math.Floor(3 * durationPercent / 100.0));

    static double GroundCover(double radius, bool atPlayer, bool melee, double enemies)
    {
        double n = Math.Min(enemies, Math.Max(0.3, radius / 1.2));
        if (atPlayer) n *= melee ? 0.6 : 0.15;
        return n;
    }

    void EstimateTriggers(PlayerAvatar avatar, WeaponSimple weapon, Charm_Basic[] charmOf, Behavior play)
    {
        double cdr = Pct(avatar.GetCustomStat(ECustomStat.CooldownRecoverySpeed));
        foreach (var c in charmOf)
            if (c is Charm_Magic cm && cm.IsEffectEnabled && cm.ContainedMagic != null && cm.ContainedMagic.cooldownTime > 0)
            {
                int cost = 0;
                try { cost = cm.GetCost(avatar, cm.CurrentLevelToIdx()); } catch { }
                play.MpPerSec += cdr / cm.ContainedMagic.cooldownTime * Math.Max(0, cost);
            }
        if (play.SpecialByMp) play.MpPerSec += play.Special * Math.Max(0, play.SweepCost);
        double asf = AttackSpeedFactor(avatar.GetCustomStat(ECustomStat.AttackSpeed), WeaponAsAmp(weapon));
        double hits = (play.Swing * asf + play.Special + play.DashAttack + play.Strike) * play.HitsPerSwing;
        bool phys = weapon != null && weapon.basicComboAttacks != null
                    && weapon.basicComboAttacks.Any(a => a != null && a.damageElementalType == EDamageElementalType.Physical);
        double crit = Math.Min(1, Math.Max(0, (avatar.GetCustomStat(ECustomStat.Critical) + avatar.GetCustomStatUnsafe("WEAPONCRITICAL")) / 10000.0));
        play.PhysCritHits = phys ? hits * crit : 0;
        double[] el =
        {
            avatar.GetCustomStat(ECustomStat.PhysicalDamage), avatar.GetCustomStat(ECustomStat.FireDamage),
            avatar.GetCustomStat(ECustomStat.IceDamage), avatar.GetCustomStat(ECustomStat.LightningDamage)
        };
        double sum = el.Sum(x => Math.Max(0, x));
        play.IceShare = sum > 0 ? Math.Max(0, el[2]) / sum : 0;
        play.FrostbiteRate = play.IceShare > 0.3 ? 1.5 : play.IceShare > 0.1 ? 0.5 : 0.1;
    }
}
