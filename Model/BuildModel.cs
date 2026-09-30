using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mirror;
using UnityEngine;

public partial class SephiriaToolbox
{
    WeaponSimple weaponOverride;

    WeaponSimple ModelWeapon(PlayerAvatar a)
    {
        if (weaponOverride != null) return weaponOverride;
        var wc = a != null ? a.GetComponent<WeaponControllerSimple>() : null;
        return wc != null ? wc.currentWeapon : null;
    }

    DpsModel BuildDpsModel(PlayerAvatar avatar, ArrModel m, Charm_Basic[] charmOf, out string info)
    {
        var d = new DpsModel();
        var keys = new List<string>();
        var index = new Dictionary<string, int>();
        int K(string k)
        {
            if (!index.TryGetValue(k, out var i)) { i = keys.Count; keys.Add(k); index[k] = i; }
            return i;
        }
        d.kAll = K("ALLDAMAGEBONUS"); d.kWdb = K("WEAPONDAMAGEBONUS"); d.kBad = K("BASICATTACKDAMAGEBONUS");
        d.kSad = K("SPECIALATTACKDAMAGEBONUS"); d.kDad = K("DASHATTACKDAMAGEBONUS"); d.kFwd = K("FINALWEAPONDAMAGE");
        d.kMdb = K("MAGICDAMAGEBONUS"); d.kAs = K("ATTACKSPEED"); d.kCrit = K("CRITICAL"); d.kCritDmg = K("CRITICALDAMAGEBONUS");
        d.kWCrit = K("WEAPONCRITICAL"); d.kWCritDmg = K("WEAPONCRITICALDAMAGE"); d.kWCritAmp = K("WEAPONCRITICALDAMAGEAMPLIFY");
        d.kMCrit = K("MAGICCRITICAL"); d.kMCritDmg = K("MAGICCRITICALDAMAGEBONUS"); d.kCdr = K("COOLDOWNRECOVERYSPEED");
        d.kExec = K("EXECUTION"); d.kLuck = K("LUCK"); d.kCharmDmg = K("CHARMDAMAGEBONUS"); d.kLastBasic = K("LASTBASICATTACKDAMAGE");
        d.kFinalCrit = K(FinalCritKey); d.kFinalDmg = K(FinalDmgKey); d.kDashDmg = K(DashDmgKey);
        d.kWdbDash = K("WEAPONDAMAGEBONUSBYDASHCOUNT"); d.kDashCount = K("DASHCOUNT"); d.kElite = K("ELITEDAMAGE");
        d.kGoldHand = K("GOLDHAND"); d.kGoldHandUnl = K("GOLDHANDUNLIMIT"); d.kDefToAtk = K("DEFENSETOATTACK"); d.kDef = K("DAMAGEREDUCTION");
        d.kDebuff = K("DEBUFFDAMAGE"); d.kPoison = K("POISONDEBUFFDAMAGEBONUS");
        d.kFollowerDmg = K("FOLLOWERDAMAGE"); d.kFollowerCrit = K("FOLLOWERCRITICAL"); d.kFollowerCritContrib = K("FOLLOWERCRITICALCONTRIBUTE");
        d.kBlockMagic = K("BLOCKCASTMAGIC");
        d.kMpSkill = K("MPSKILLDAMAGE"); d.kMagicMp = K("MAGICMP"); d.kTrue = K("TRUEDAMAGE"); d.kIgnoreDef = K("IGNOREDEFENSE");
        d.kFsIgnore = K("FLAMESWORDIGNOREDEFENSE"); d.kFixedAs = K("FIXEDATTACKSPEED"); d.kSpecAs = K("SPECIALATTACKSPEED");
        d.kUnlimited = K("UNLIMITEDCOMBO"); d.kAdvNego = K("ADVANCED_NEGOTIATION"); d.kNego = K("NEGOTIATION"); d.kFollowerAs2 = K("FOLLOWERATTACKSPEED");
        d.kMaxMp = K(MaxMpKey); d.kFinalMp = K("FINALMP");
        d.ExtraBase[d.kMaxMp] = avatar.maxMp;
        try { d.DefaultMp = KeywordDatabase.GetConstValue("PLAYERDEFAULTMP"); } catch { }
        try
        {
            var dm = DungeonManager.Instance;
            if (dm != null && !string.IsNullOrEmpty(avatar.currentFloorGuid) && dm.generatedFloors.TryGetValue(avatar.currentFloorGuid, out var fd) && fd != null)
            {
                d.StageDef = StageDefense(dm, fd.stageName, out var plate);
                d.StagePlate = plate;
                d.StageDefKnown = true;
            }
        }
        catch (Exception e) { WarnOnce("关卡防御", e); }
        if (EnemyDefenseMeasured(out var enemyDef)) d.EnemyDef = enemyDef;
        else if (d.StageDefKnown) d.EnemyDef = d.StageDef;
        try
        {
            int leaf = KeywordDatabase.GetConstValue("GOLDHANDLEAF");
            if (leaf > 0)
            {
                d.GoldHandPctUnl = avatar.Money / leaf;
                d.GoldHandPct = Math.Min(d.GoldHandPctUnl, KeywordDatabase.GetConstValue("GOLDHANDMAX"));
            }
        }
        catch { }
        for (int e = 0; e < 4; e++)
        {
            d.kElem[e] = K(ElemKeys[e]);
            for (int t = 0; t < 4; t++) d.kConv[e, t] = e == t ? -1 : K(ElemNames[e] + "TO" + ElemNames[t]);
        }

        var adds = new StatAdd[m.Items.Length][];
        var curRaw = new Dictionary<int, double>();
        var curAmp = new Dictionary<int, double>();
        var mods = new List<Mod>();
        double curHighest = 0;
        int passiveCharms = 0;
        d.kFollowerAs = K(FollowerAsKey);
        var wc = avatar.GetComponent<WeaponControllerSimple>();
        var weapon = weaponOverride != null ? weaponOverride : wc != null ? wc.currentWeapon : null;
        var play = MeasureBehavior(weapon, settings.arrSingle);
        d.Single = play.Single;
        buildSingle = play.Single;
        buildOwner = avatar;
        buildWeapon = weapon;
        buildCloseCdf = play.Single ? BossCloseCdf() : null;
        buildHitRate = play.Single ? BossHitRate() : -1;
        buildGuardHold = GuardHoldUptime();
        buildDashAttack = play.DashAttack;
        if (play.Single)
        {
            d.FightLen = BossFightLength(out d.FightCount);
            try { ReadBossAffix(avatar, d); } catch (Exception e) { WarnOnce("Boss 词缀", e); }
            d.OtherDebuff[0] = BossOtherDebuff(0);
            d.OtherDebuff[2] = BossOtherDebuff(2);
        }
        double bossOthers = play.Single ? BossOtherDebuffObjects() : -1;
        d.OtherDebuffObjects = bossOthers >= 0 ? bossOthers : OtherDebuffsOnTarget();
        if (weapon is WeaponSimple_SwordAndShield ss) BuildSweep(avatar, d, K, ss, play, m);
        else if (weapon is WeaponSimple_GreatSword gsw) BuildGreatSwordSweep(avatar, d, K, gsw, play, m);
        else if (weapon is WeaponSimple_Crossbow xbw) BuildCrossbowModel(avatar, d, K, xbw, play, m);
        else if (weapon is WeaponSimple_Katana ktw) BuildKatanaSweep(avatar, d, K, ktw, play, m);
        else if (weapon is WeaponSimple_QuartterStaff qsw) BuildQuarterStaffModel(avatar, d, K, qsw, play, m);
        buildWeaponHits = Math.Max(0.2, play.Swing * AttackSpeedFactor(avatar.GetCustomStat(ECustomStat.AttackSpeed), weapon != null ? WeaponAsAmp(weapon) : 0)
                                        + play.Special + play.DashAttack + play.Strike);
        bool guards = weapon is WeaponSimple_SwordAndShield or WeaponSimple_QuartterStaff;
        d.GuardRate = guards ? play.Guard : 0;
        d.PerfectGuardRate = guards ? play.PerfectGuard : 0;
        d.kGuardResist = K("GUARDRESIST");
        d.kInfMp = K("INFINITYMP");
        if (play.Single)
        {
            m.Notes.Add(d.FightCount > 0 ? Tr("model.boss_fights_assumed_last", d.FightLen, d.FightCount)
                                         : Tr("model.boss_fights_assumed_last_s", d.FightLen));
            if (buildCloseCdf != null) m.Notes.Add(Tr("model.time_within_2_5", CloseShare(buildCloseCdf, 2.5)));
            if (buildHitRate >= 0) m.Notes.Add(Tr("model.hit_boss_every_s", (buildHitRate > 0 ? 1 / buildHitRate : 999)));
            if (!string.IsNullOrEmpty(d.BossAffix)) m.Notes.Add(Tr("model.next_boss_modifier_included", d.BossAffix));
        }
        if (play.DefendMeasured) m.Notes.Add(Tr("model.blocks_s_perfect_parries", play.Guard, play.PerfectGuard, play.Parry));
        try
        {
            var tal = CaptureTalents(avatar);
            if (tal.Count > 0) m.Notes.Add(Tr("model.talents_already_included_through", string.Join(Tr("common.sep_comma"), tal.Select(t => $"{t.name} {t.level}"))));
        }
        catch (Exception e) { WarnOnce("天赋", e); }
        d.kEvasion = K("EVASION");
        d.kNoMagicCost = K("NOMAGICCOST");
        d.kMagicCostReduce = K("MAGICCOSTREDUCE");
        {
            double ignore = play.Single ? Math.Min(100, d.BossIgnoreEvasion) : 0;
            double chanceNow = EvasionChance(avatar.GetCustomStat(ECustomStat.Evasion)) * (1 - ignore / 100.0);
            d.EvadeAttempts = play.Damaged / Math.Max(0.25, 1 - chanceNow);
            if (chanceNow > 0.005)
                m.Notes.Add(Tr("model.evasion_attacked_about_times", chanceNow * 100, d.EvadeAttempts, (ignore > 0 ? Tr("model.boss_ignores_evasion", ignore) : "")));
        }
        if (weaponOverride != null) m.Notes.Add(Tr("model.enhance_preview_weapon_treated", (WeaponDatabase.FindWeaponById(weaponOverride.entityId) != null ? WeaponName(weaponOverride.entityId) : Tr("model.new_weapon"))));
        try { EstimateTriggers(avatar, weapon, charmOf, play); } catch (Exception e) { WarnOnce("Buff 触发频率", e); }
        d.Melee = weapon == null || !(weapon.weaponType is EWeaponType.Crossbow or EWeaponType.StaffMagic or EWeaponType.Golem);
        d.kBuffDur = K("BUFFDURATION");
        try { BuildWeaponModel(avatar, d, K, weapon, play); } catch (Exception e) { WarnOnce("武器模型", e); }
        for (int i = 0; i < m.Items.Length; i++)
        {
            var c = charmOf[i];
            if (c == null) continue;
            var list = new List<StatAdd>();
            if (c is Charm_StatusInstance si && si.stats != null)
                foreach (var g in si.stats)
                {
                    if (g == null || g.valuesByLevel == null || g.valuesByLevel.Length == 0) continue;
                    if (!MapStatus(g.statusID, out var key, out var mode)) continue;
                    var a = new StatAdd { Key = mode == 2 ? -1 : K(key), Mode = mode, Values = g.valuesByLevel };
                    list.Add(a);
                    if (!si.IsEffectEnabled) continue;
                    int v = SafeAt(g.valuesByLevel, si.CurrentLevelToIdx());
                    if (mode == 2) curHighest += v;
                    else if (mode == 1) curAmp[a.Key] = curAmp.GetValueOrDefault(a.Key) + v;
                    else curRaw[a.Key] = curRaw.GetValueOrDefault(a.Key) + v;
                }
            try
            {
                if (ReadPassives(c, avatar, i, K, list, mods, curRaw, curAmp, (what, e) => WarnOnce(what, e)) > 0) passiveCharms++;
                if (ReadBuffPassives(c, avatar, play, K, list, curRaw, curAmp, d.DynBuffs, i) > 0) passiveCharms++;
                if (ReadAddStatByAnotherStat(c, K, list, curRaw) > 0) passiveCharms++;
                ApplyModelHooks(c, i, d);
                if (c is Charm_BoltMagicMultiShot bm && bm.multiShotDamageRatioByLevel != null && bm.multiShotDamageRatioByLevel.Length > 0)
                {
                    mods.Add(new Mod { Item = i, Kind = 3, Values = Enumerable.Range(0, Math.Max(1, c.maxLevel + 1)).Select(l => 3 * SafeAt(bm.multiShotDamageRatioByLevel, l)).ToArray() });
                    passiveCharms++;
                }
            }
            catch (Exception e) { WarnOnce("被动 " + m.Items[i].Name, e); }
            if (list.Count > 0) adds[i] = list.ToArray();
        }
        d.Adds = adds;
        d.Mods = mods.ToArray();
        try { WeaponGuardBuffs(avatar, m, d, K, weapon, play, curRaw); } catch (Exception e) { WarnOnce("武器的完美格挡 Buff", e); }
        try { WeaponAttackBuffs(avatar, m, d, K, weapon, curRaw); } catch (Exception e) { WarnOnce("武器的攻击 Buff", e); }
        try { BuildWeaponCondDamage(m, d, K, weapon); } catch (Exception e) { WarnOnce("武器的条件增伤", e); }
        try { WeaponSpecialBuffs(avatar, m, d, K, weapon, play, curRaw); } catch (Exception e) { WarnOnce("武器的特攻 Buff", e); }
        try { GreatSwordLiveState(avatar, K, curRaw); } catch (Exception e) { WarnOnce("大剑的临时属性", e); }
        try { DaggerDashBuff(avatar, m, d, K, weapon, play, curRaw); } catch (Exception e) { WarnOnce("匕首的冲刺 Buff", e); }
        try { CrossbowLiveState(avatar, K, curRaw); } catch (Exception e) { WarnOnce("弩的霜之帷幕", e); }

        var measuredIds = new Dictionary<string, Dictionary<EDamageElementalType, double>>(StringComparer.Ordinal);
        foreach (var kv in avatar.dealsStatistics)
        {
            if (!measuredIds.TryGetValue(kv.Key.Id, out var byElem)) measuredIds[kv.Key.Id] = byElem = new Dictionary<EDamageElementalType, double>();
            byElem[kv.Key.ElementalType] = byElem.GetValueOrDefault(kv.Key.ElementalType) + kv.Value;
        }

        var sources = new List<DpsSource>();
        d.SwingInfo = weapon == null ? "" : play.SwingMeasured
            ? Tr("model.swing_speed_from_live", play.Swing)
            : Tr("model.swing_speed_from_weapon", play.Swing);
        if (weapon != null)
        {
            d.AsAmp = WeaponAsAmp(weapon);
            var mv = MovesOf(weapon, avatar, play);
            double meleeMulti = play.HitsPerSwingM / Math.Max(1e-6, play.HitsPerSwing);
            DpsSource AddWeapon(SrcKind kind, string name, NewWeaponFireData fd, double perSecond, bool attackSpeed, string id, string note)
            {
                var s = new DpsSource
                {
                    Kind = kind, Name = name, UseAttackSpeed = attackSpeed,
                    Element = fd != null ? fd.damageElementalType : EDamageElementalType.Physical,
                    Formula = mv.Formula ?? fd?.relatedStatFormula ?? "",
                    Prior = perSecond, TheoryK = perSecond * play.HitsPerSwing, HasTheory = true, Note = note,
                    Direct = true, MultiScale = meleeMulti
                };
                s.DmgElem = ElemIndex(s.Element);
                s.Ids.Add(id);
                sources.Add(s);
                return s;
            }
            var basicSrc = mv.BasicIsDash
                ? AddWeapon(SrcKind.WeaponBasic, Tr("src.weapon_rapier_thrust"), mv.BasicFd, play.Swing * mv.BasicMult, true, "Weapon_DashAttack", Tr("model.every_weapon_swing_counts"))
                : AddWeapon(SrcKind.WeaponBasic, Tr("src.weapon_normal_attack"), mv.BasicFd, play.Swing * mv.BasicMult, true, "Weapon_BasicAttack",
                            play.Strike > 0 ? Tr("model.every_weapon_swing_including") : Tr("model.every_weapon_swing"));
            basicSrc.AsDash = mv.BasicIsDash;
            basicSrc.FinalShare = mv.FinalShare;
            bool dagger = d.Weapon.Dg != null;
            if (!mv.NoSpecial && (play.Special > 0 || dagger))
            {
                string baseNote = mv.SpecialNote.Length > 0 ? mv.SpecialNote : Tr("model.special_attack");
                string note = play.SpecialMeasured ? Tr("model.note_live_rate", baseNote, play.Special) : baseNote;
                var sp = AddWeapon(SrcKind.WeaponSpecial, Tr("src.weapon_special_attack"), mv.SpecialFd, dagger ? 1 : play.Special * mv.SpecialMult, weapon.specialAttackIsRelatedToAttackSpeed,
                                   "Weapon_SpecialAttack", note);
                sp.SpecialAs = weapon.specialAttackIsRelatedToAttackSpeed;
                if (d.Weapon.Gs != null && d.Weapon.Gs.Amethyst)
                {
                    sp.Ids.Add("Weapon_SpecialAttack_Amethyst");
                    sp.Name = Tr("src.weapon_amethyst");
                }
                if (d.Weapon.Ss != null && d.Weapon.Ss.Ring)
                    sp.MultiScale = Math.Max(1, Math.Min(d.Weapon.Ss.RingBase, play.EnemiesM) * 0.6) / Math.Max(1, d.Weapon.Ss.RingBase / 6.0);
            }
            if (!mv.NoDash)
            {
                double near = buildCloseCdf != null ? CloseShare(buildCloseCdf, 3) : 0.6;
                double auto = mv.AutoDash > 0 ? PlayDash * near : 0;
                double perSec = play.DashAttack * mv.DashMult + auto * mv.AutoDash;
                if (perSec > 0)
                    AddWeapon(SrcKind.WeaponDash, Tr("src.weapon_dash_attack"), mv.DashFd, perSec, false, "Weapon_DashAttack",
                              auto > 0
                                  ? (play.DashAttackMeasured ? Tr("model.dash_attack_live_auto", play.DashAttack, auto) : Tr("model.dash_attack_auto", auto))
                                  : (play.DashAttackMeasured ? Tr("model.dash_attack_live", play.DashAttack) : Tr("model.dash_attack")));
            }
            if (d.Weapon.Gs != null && d.Weapon.Gs.Needle)
            {
                var g = d.Weapon.Gs;
                var s = new DpsSource
                {
                    Kind = SrcKind.Rider, Name = Tr("src.weapon_spikes"), ElemIdx = 0, AddBase = g.NeedleRatio * 100 * g.NeedleCount * g.NeedleHit, PerCrit = true,
                    Prior = play.Swing, TheoryK = (play.Swing + play.Special + play.DashAttack) * play.HitsPerSwing, HasTheory = true,
                    Note = Tr("model.fires_spikes_direct_attack"), DmgElem = 0, MultiScale = play.HitsPerSwingM / Math.Max(1e-6, play.HitsPerSwing)
                };
                s.Ids.Add("Weapon_Spike");
                sources.Add(s);
            }
            if (weapon.addons != null)
                foreach (var ad in weapon.addons.OfType<WeaponAddonCommon_AdditionalElementalDamage>())
                {
                    if (ad == null || string.IsNullOrEmpty(ad.damageId) || (!measuredIds.ContainsKey(ad.damageId) && weaponOverride == null)) continue;
                    if (sources.Any(x => x.Ids.Contains(ad.damageId))) continue;
                    string stat = ad.statId.ToString().ToUpperInvariant();
                    int e = Array.IndexOf(ElemKeys, stat);
                    var s = new DpsSource
                    {
                        Kind = SrcKind.Rider, Name = Tr("src.weapon", TrimPrefix(DamageIdName(ad.damageId), "武器")),
                        ElemIdx = e, StatKey = e < 0 ? K(stat) : -1,
                        AddKey = K("ADDITIONALELEMENTALDAMAGEBONUS"), AddBase = ad.additionalDamagePercent,
                        Prior = play.Swing, TheoryK = (play.Swing + play.Special + play.DashAttack + play.Strike) * play.HitsPerSwing, HasTheory = true, Note = Tr("model.added_every_weapon_hit"),
                        DmgElem = ElemIndex(ad.elementalType), MultiScale = play.HitsPerSwingM / Math.Max(1e-6, play.HitsPerSwing)
                    };
                    s.Ids.Add(ad.damageId);
                    sources.Add(s);
                }
            try { AddBurnRingSource(d, K, weapon, play, sources); } catch (Exception e) { WarnOnce("武器的火焰之环", e); }
        }
        var extra = new ItemExtra[m.Items.Length];
        var srcOfItem = Enumerable.Repeat(-1, m.Items.Length).ToArray();
        for (int i = 0; i < m.Items.Length; i++)
        {
            extra[i] = new ItemExtra();
            var c = charmOf[i];
            if (c == null) continue;
            try
            {
                bool attackable = c is IAttackableCharm ac0 && ac0.IsAttackableCharm();
                extra[i].Attackable = attackable;
                extra[i].Companion = c is ICompanionCharm;
                var ent = ItemDatabase.FindItemById(m.Items[i].EntityId);
                extra[i].Planet = c is Charm_SummonGreenBat && ent != null && ent.categories != null && ent.categories.Contains("PLANET");
                if (c is Charm_Magic cm)
                {
                    var entity = cm.ContainedMagic;
                    var skill = entity != null && entity.magicPrefab != null ? entity.magicPrefab.GetComponent<ActiveSkill>() : null;
                    extra[i].Magic = true;
                    extra[i].BoltMagic = skill is ActiveSkill_Bolt;
                    extra[i].MagicElem = entity == null ? 0 : entity.GetMajorClass() switch
                    {
                        EMagicClass.Fire => 1,
                        EMagicClass.Water => 2,
                        EMagicClass.Air => 3,
                        _ => 0
                    };
                    if (skill == null || !skill.IsAttackMagic()) continue;
                    var s = new DpsSource
                    {
                        Kind = SrcKind.Magic, Item = i, Name = Tr("src.magic", m.Items[i].Name),
                        Default = ReadFloats(skill, "defaultDamageByLevel"),
                        Percent = ReadFloats(skill, "damagePercentByLevel"),
                        RelKey = ReadString(skill, "relatedDamage")?.ToUpperInvariant(),
                        Cooldown = Math.Max(0.1, entity.cooldownTime),
                        Prior = 1, TheoryK = 1, HasTheory = true,
                        Note = Tr("model.cooldown_s", entity.cooldownTime)
                    };
                    if (!ApplyMagicMech(s, skill, play, Math.Max(1, c.maxLevel + 1), 1)) s.HasTheory = false;
                    try { s.MagicCost = Enumerable.Range(0, Math.Max(1, c.maxLevel + 1)).Select(l => (float)cm.GetCost(avatar, l)).ToArray(); } catch { }
                    try
                    {
                        s.MagicCostBase = cm.ContainedMagic?.mpCostsByLevel?.Select(v => (float)v).ToArray();
                        int add = cm.AdditionalCost;
                        foreach (var other in charmOf)
                            if (other is Charm_ReduceMPCost rc && ReferenceEquals(FieldValue(rc, "currentMagicCharm"), cm) && Num(rc, "reduceActivated") > 0)
                                add += (int)Num(rc, "reducedPercent");
                        s.MagicCostAdd = add;
                    }
                    catch { s.MagicCostBase = null; }
                    s.DmgElem = extra[i].MagicElem;
                    if (!string.IsNullOrEmpty(s.RelKey) && !index.ContainsKey(s.RelKey)) s.RelKey = null;
                    foreach (var comp in entity.magicPrefab.GetComponentsInChildren<Component>(true)) CollectIds(comp, s.Ids);
                    srcOfItem[i] = sources.Count;
                    sources.Add(s);
                }
                else if (attackable)
                {
                    var ac = (IAttackableCharm)c;
                    var table = new float[Math.Max(1, c.maxLevel + 1)];
                    int saved = c.limitedEffectEnabledLevel;
                    try
                    {
                        for (int l = 0; l < table.Length; l++)
                        {
                            c.limitedEffectEnabledLevel = l;
                            table[l] = i == m.NewItem && c is Charm_Reddew rd ? ReddewDamage(rd, avatar, l) : ac.GetDamage(avatar);
                        }
                    }
                    finally { c.limitedEffectEnabledLevel = saved; }
                    if (table.All(v => Math.Abs(v) < 1e-6f)) continue;
                    var s = new DpsSource
                    {
                        Kind = SrcKind.Proc, Item = i, Name = Tr("src.artifact", m.Items[i].Name), Table = table, Prior = 0.3,
                        Column = c is Charm_NearMagicBullet, Rate = CooldownRates(c)
                    };
                    CollectIds(c, s.Ids);
                    CollectPrefabIds(c, s.Ids);
                    s.Ids.RemoveWhere(IsDebuffId);
                    srcOfItem[i] = sources.Count;
                    sources.Add(s);
                }
            }
            catch (Exception e) { WarnOnce("输出来源 " + m.Items[i].Name, e); }
        }
        d.Extra = extra;
        BuildSpecials(avatar, m, charmOf, d, K);

        var changeable = new HashSet<int>();
        foreach (var list in adds) if (list != null) foreach (var a in list) if (a.Key >= 0) changeable.Add(a.Key);
        foreach (var tiers in d.Tiers) foreach (var t in tiers) foreach (var a in t.Adds) if (a.Key >= 0) changeable.Add(a.Key);
        foreach (var sp in d.Specials)
        {
            if (sp.Kind == SpecialKind.NearLevel) changeable.Add(d.kAll);
            if (sp.RowKeys != null) foreach (var k in sp.RowKeys) changeable.Add(k);
            if (sp.Kind == SpecialKind.FireIce) { changeable.Add(sp.KeyL); changeable.Add(sp.KeyR); }
        }
        if (d.ComboBonus != 0) changeable.Add(d.kAll);
        for (int e = 0; e < 4; e++) changeable.Add(d.kElem[e]);
        if (weaponStatDelta != null) foreach (var kv in weaponStatDelta) if (kv.Value != 0) changeable.Add(K(kv.Key));

        PlayerSources ps;
        try { ps = ReadPlayerSources(avatar); }
        catch (Exception e) { WarnOnce("整理：伤害来源", e); ps = new PlayerSources(); }
        var itemOfInstance = new Dictionary<int, int>();
        for (int i = 0; i < m.Items.Length; i++) itemOfInstance[m.Items[i].InstanceId] = i;
        int basic = sources.FindIndex(x => x.Kind == SrcKind.WeaponBasic);
        var byId = new Dictionary<string, int>(StringComparer.Ordinal);
        int SourceOf(string id)
        {
            if (byId.TryGetValue(id, out var hit)) return hit;
            if (weaponOverride != null && forgeWeaponIds != null && id.StartsWith("Weapon", StringComparison.Ordinal)
                && id is not ("Weapon_BasicAttack" or "Weapon_SpecialAttack" or "Weapon_DashAttack") && !forgeWeaponIds.Contains(id))
                return byId[id] = -1;
            int found = sources.FindIndex(x => x.Ids.Contains(id));
            if (found < 0)
            {
                var src = ResolveSource(ps, id);
                foreach (var inst in src.Instances)
                {
                    if (!itemOfInstance.TryGetValue(inst, out var item)) continue;
                    if (srcOfItem[item] < 0)
                    {
                        srcOfItem[item] = sources.Count;
                        sources.Add(new DpsSource { Kind = SrcKind.Proc, Item = item, Name = Tr("src.artifact", m.Items[item].Name), Prior = 0.3, Rate = CooldownRates(charmOf[item]) });
                    }
                    found = srcOfItem[item];
                    break;
                }
                if (found < 0 && !IsReflectDamage(id) && WeaponIdScaling.TryGetValue(id, out var ws))
                {
                    if (ws.Special)
                    {
                        int sp = sources.FindIndex(x => x.Kind == SrcKind.WeaponSpecial);
                        if (sp >= 0) found = sp;
                    }
                    else
                    {
                        found = sources.Count;
                        var ns = new DpsSource
                        {
                            Kind = SrcKind.Ability, Name = Tr("src.weapon", TrimPrefix(DamageIdName(id), "武器")), Prior = 0.5, Preset = true,
                            ElemIdx = ws.Elem, MulKeys = ws.Keys.Select(K).ToArray(), Note = ws.Note
                        };
                        if (ws.RateKey != null) ns.X = new List<XMod> { new XMod { Kind = XKind.RatePct, Key = K(ws.RateKey) } };
                        sources.Add(ns);
                    }
                }
                if (found < 0 && basic >= 0 && id.StartsWith("Weapon", StringComparison.Ordinal)) found = basic;
                if (found < 0 && !IsReflectDamage(id))
                {
                    bool debuff = id.StartsWith("Debuff", StringComparison.Ordinal);
                    string name = src.Cat != SrcCat.Ability ? Tr("src.other", src.Name) : debuff ? Tr("src.debuff", src.Name) : Tr("src.ability", src.Name);
                    found = sources.FindIndex(x => x.Kind == SrcKind.Ability && x.Name == name);
                    if (found < 0)
                    {
                        found = sources.Count;
                        sources.Add(new DpsSource { Kind = SrcKind.Ability, Name = name, Prior = 0.3 });
                    }
                }
                if (found >= 0) sources[found].Ids.Add(id);
            }
            return byId[id] = found;
        }
        foreach (var id in measuredIds.Keys)
        {
            try { SourceOf(id); }
            catch (Exception e) { WarnOnce("整理：伤害归属 " + id, e); byId[id] = -1; }
        }
        try
        {
            if (avatar.Inventory != null && avatar.Inventory.FindComboEffect("FLAMESWORD") is ComboEffect_FlameSword fs && fs.isEnabled && fs.isFlameSwordEnabled
                && SourceOf("Ability_FlameSword") < 0)
            {
                var src = new DpsSource { Kind = SrcKind.Ability, Name = Tr("src.ability", DamageIdName("Ability_FlameSword")) };
                src.Ids.Add("Ability_FlameSword");
                byId["Ability_FlameSword"] = sources.Count;
                sources.Add(src);
            }
        }
        catch (Exception e) { WarnOnce("整理：太阳剑", e); }
        try
        {
            if (avatar.Inventory != null && avatar.Inventory.FindComboEffect("DARKCLOUD") is ComboEffect_DarkCloud dc && dc.isEnabled && SourceOf("Ability_DarkCloud") < 0)
            {
                var src = new DpsSource { Kind = SrcKind.Ability, Name = Tr("src.ability", DamageIdName("Ability_DarkCloud")) };
                src.Ids.Add("Ability_DarkCloud");
                byId["Ability_DarkCloud"] = sources.Count;
                sources.Add(src);
            }
        }
        catch (Exception e) { WarnOnce("整理：乌云", e); }

        foreach (var s in sources)
        {
            try
            {
                if (s.Kind == SrcKind.Proc && s.Item >= 0 && charmOf[s.Item] is Charm_Basic c)
                {
                    var mech = FindMech(c);
                    if (mech != null) ApplyMech(s, c, mech, play, K);
                    else { s.TheoryK = PlayUnknown; s.Note = Tr("model.not_database_assuming_0"); }
                    ApplyExtras(s, c, play, K);
                }
                else if (s.Kind == SrcKind.Ability)
                {
                    bool debuff = s.Ids.Any(id => id.StartsWith("Debuff", StringComparison.Ordinal));
                    s.TheoryK = debuff ? PlayDebuff : PlayAbility;
                    if (s.Ids.Contains("Ability_DarkCloud"))
                    {
                        double pct = 100;
                        try { pct = KeywordDatabase.GetConstValue("darkCloudDamagePercent"); } catch { }
                        double multi = Math.Max(1, avatar.GetCustomStatUnsafe("DARKCLOUDMULTISHOT"));
                        s.TheoryK = 0.5 * pct / 100 * multi;
                        s.HasTheory = true;
                        s.StatRate = RateStat.DarkCloud;
                        s.RateKeyA = K("DARKCLOUDSPEED");
                        s.RateKeyB = K("DARKCLOUDATKSPEEDBONUS");
                        s.Note = Tr("model.discharges_every_2_s", pct);
                    }
                    if (s.Ids.Contains("FlameGround")) s.Note = Tr("model.blazing_field_2_50");
                    if (s.Ids.Contains("Ability_FlameSword"))
                    {
                        double direct = play.Swing + play.Special + play.DashAttack + play.Strike;
                        s.TheoryK = Math.Min(direct * play.HitsPerSwing + 0.3, 1 / 0.15) * 1.5;
                        s.MultiScale = Math.Min(direct * play.HitsPerSwingM + 0.3, 1 / 0.15) * 1.5 / Math.Max(1e-6, s.TheoryK);
                        s.HasTheory = true;
                        s.SwingBased = true;
                        s.Note = Tr("model.fires_solar_blades_weapon");
                    }
                }
            }
            catch (Exception e) { WarnOnce("整理：机制 " + s.Name, e); }
        }

        try { BuildEconomy(avatar, m, charmOf, d, K, play, sources, measuredIds); }
        catch (Exception e) { WarnOnce("整理：资源模型", e); d.Eco = null; foreach (var s in sources) s.Eco = EcoKind.None; }

        var probeKeys = changeable.Select(k => (key: k, name: keys[k], elem: Array.IndexOf(d.kElem, k))).ToList();
        UnitAvatar probe = null;
        try { probe = Probe(avatar); } catch (Exception e) { WarnOnce("属性探针", e); }
        int probed = 0, estimated = 0, byLevel = 0, modeled = 0;
        foreach (var s in sources)
        {
            if (s.Rate != null && s.Kind != SrcKind.Magic) byLevel++;
            if (s.Kind != SrcKind.Proc && s.Kind != SrcKind.Ability || s.Eco != EcoKind.None || s.Flat || s.Preset) continue;
            bool ok = false;
            try
            {
                if (probe != null && s.Kind == SrcKind.Proc && s.Table != null && charmOf[s.Item] is IAttackableCharm ac && charmOf[s.Item] is not Charm_Reddew)
                    ok = ProbeCharm(s, charmOf[s.Item], ac, avatar, probe, probeKeys);
                else if (probe != null && s.Kind == SrcKind.Ability)
                {
                    Func<UnitAvatar, float> g = s.Ids.Contains("Debuff_Burn") ? a => CharacterDebuff_Burn.CalculateTickDamage(a, out _)
                        : s.Ids.Contains("Debuff_Plasma") ? a => CharacterDebuff_Plasma.CalculateTickDamage(a, out _)
                        : null;
                    if (g != null && (ok = ProbeFunction(s, g, avatar, probe, probeKeys)))
                    {
                        s.TheoryK = 2 * DotTargetShare * play.Enemies;
                        s.HasTheory = true;
                        s.Note = Tr("model.deals_damage_every_0");
                    }
                }
            }
            catch (Exception e) { WarnOnce("属性探针 " + s.Name, e); ok = false; }
            try
            {
                bool hasSlopes = ok && s.Slopes != null && s.Slopes.Length > 0;
                bool placeholder = s.Table == null || s.Table.All(v => Math.Abs(v - 1f) < 1e-6f);
                bool flameSword = s.Ids.Contains("Ability_FlameSword") || s.Ids.Contains("FlameSwordReturn") || (s.Item >= 0 && charmOf[s.Item] is Charm_FlameSwordFall);
                if (hasSlopes) probed++;
                if (placeholder || !ok)
                {
                    if (placeholder) s.Table = null;
                    s.Slopes = null;
                    s.ElemIdx = MainElement(s.Ids, measuredIds);
                    var mul = TokenKeys(s.Ids, index, changeable, d).ToList();
                    if (s.Ids.Contains("Ability_DarkCloud")) s.ElemIdx = avatar.GetCustomStatUnsafe("DARKCLOUDICE") > 0 ? 4 : 3;
                    if (s.Ids.Contains("FlameGround")) s.ElemIdx = 1;
                    if (flameSword)
                    {
                        s.ElemIdx = avatar.GetCustomStatUnsafe("FLAMESWORDFROST") > 0 ? 2 : 1;
                        if (index.TryGetValue("FLAMESWORDDAMAGE", out var fsd) && changeable.Contains(fsd) && !mul.Contains(fsd)) mul.Add(fsd);
                    }
                    s.MulKeys = mul.ToArray();
                    estimated++;
                    if (placeholder && !flameSword) s.HasTheory = false;
                }
                if (s.NoCrit && s.Ids.Any(id => id.StartsWith("Debuff", StringComparison.Ordinal) || id == "FlameGround"))
                    s.MulKeys = (s.MulKeys ?? new int[0]).Append(d.kDebuff).Append(d.kPoison).Distinct().ToArray();
            }
            catch (Exception e) { WarnOnce("整理：估算 " + s.Name, e); s.Slopes = null; }
        }
        try
        {
            void Gate(string cat, int count, Func<DpsSource, bool> match)
            {
                int ci = Array.IndexOf(d.CatNames, cat);
                if (ci < 0 || count <= 0) return;
                foreach (var s in sources) if (match(s)) { s.GateCat = ci; s.GateCount = count; }
            }
            if (avatar.Inventory != null)
            {
                if (avatar.Inventory.FindComboEffect("FLAMESWORD") is ComboEffect_FlameSword fsc)
                    Gate("FLAMESWORD", fsc.flameSwordComboCount, s => s.Ids.Contains("Ability_FlameSword") || s.Ids.Contains("FlameSwordReturn") || (s.Item >= 0 && charmOf[s.Item] is Charm_FlameSwordFall));
                if (avatar.Inventory.FindComboEffect("DARKCLOUD") is ComboEffect_DarkCloud dcc)
                    Gate("DARKCLOUD", dcc.darkCloudComboCount, s => s.Ids.Contains("Ability_DarkCloud"));
            }
        }
        catch (Exception e) { WarnOnce("整理：连击门槛", e); }
        foreach (var s in sources)
        {
            if (s.HasTheory) modeled++;
            if (s.Kind is SrcKind.Proc or SrcKind.Ability && s.DmgElem < 0)
            {
                int e = s.ElemIdx is >= 0 and < 4 ? s.ElemIdx : MainElement(s.Ids, measuredIds);
                if (e >= 4 && s.Kind == SrcKind.Proc && s.Item >= 0 && charmOf[s.Item] != null)
                {
                    if (FieldValue(charmOf[s.Item], "elementalType") is EDamageElementalType et) e = ElemIndex(et);
                    else if (s.Slopes != null)
                    {
                        var best = s.Slopes.Where(sl => sl.Elem is >= 0 and < 4 && sl.PerLevel != null && sl.PerLevel.Length > 0)
                                           .OrderByDescending(sl => sl.PerLevel.Max()).FirstOrDefault();
                        if (best != null) e = best.Elem;
                    }
                }
                s.DmgElem = e is >= 0 and < 4 ? e : -1;
            }
        }
        if (passiveCharms > 0) m.Notes.Add(Tr("model.passive_bonuses_coded_artifacts", passiveCharms));
        if (d.Mods.Length > 0) m.Notes.Add(Tr("model.damage_modifiers_element_condition", d.Mods.Length));
        d.Sources = sources.ToArray();
        int column = d.Sources.Count(x => x.Column);
        if (column > 0) m.Notes.Add(Tr("model.same_column_magic_bolts", column));

        d.Keys = keys.ToArray();
        d.BaseRaw = new double[keys.Count];
        d.BaseAmp = new double[keys.Count];
        for (int k = 0; k < keys.Count; k++)
        {
            d.BaseRaw[k] = (avatar.customStats.TryGetValue(keys[k], out var v) ? v : 0) + d.ExtraBase.GetValueOrDefault(k) - curRaw.GetValueOrDefault(k)
                         + (weaponStatDelta != null && weaponStatDelta.TryGetValue(keys[k], out var dv) ? dv : 0);
            d.BaseAmp[k] = avatar.GetCustomStatAmpUnsafe(keys[k]) - curAmp.GetValueOrDefault(k);
        }
        d.BaseHighest = avatar.highestElementalBonus - curHighest;
        if (weaponStatDelta != null)
        {
            d.StatDelta = new Dictionary<int, double>();
            foreach (var kv in weaponStatDelta)
                if (kv.Value != 0 && index.TryGetValue(kv.Key, out var dk)) d.StatDelta[dk] = kv.Value;
        }
        int unlimitedNow = avatar.GetCustomStatUnsafe("UNLIMITEDCOMBO");
        if (unlimitedNow > 0 && d.CatActual != null)
        {
            int over = 0;
            for (int c = 0; c < d.CatActual.Length && c < d.CatHighest.Length; c++)
                if (d.CatHighest[c] > 0 && d.CatActual[c] > d.CatHighest[c]) over += d.CatActual[c] - d.CatHighest[c];
            d.BaseRaw[d.kAll] -= unlimitedNow * over;
            if (over > 0) m.Notes.Add(Tr("model.unlimited_combo_over_top", over, unlimitedNow));
        }

        foreach (var kv in avatar.dealsStatistics)
        {
            double v = kv.Value;
            d.Measured += v;
            int s = byId.TryGetValue(kv.Key.Id, out var si) ? si : -1;
            if (s >= 0) d.Sources[s].Measured += v;
            else d.Other += v;
        }
        List<DpsWindow> windows;
        double windowSeconds;
        try { windows = BuildWindows(avatar, m, d, byId, out windowSeconds); }
        catch (Exception e) { WarnOnce("整理：分段校准", e); windows = new List<DpsWindow>(); windowSeconds = 0; }
        if (windowSeconds >= MinWindowSeconds)
        {
            d.Windows = windows;
            d.WindowSeconds = windowSeconds;
            if (EnemyDefenseMeasured(out var recentDef, recent: true)) d.EnemyDef = recentDef;
        }
        if (d.Mods.Any(md => md.PerDebuffCount))
            m.Notes.Add(Tr("model.scrap_red_cloth_teammates", d.OtherDebuffObjects, (d.Single && BossOtherDebuffObjects() >= 0 ? Tr("model.measured_boss_fights") : Tr("model.measured_nearby_enemies_0"))));
        if (d.Single && d.StageDefKnown)
        {
            double bd = d.StageDef + d.BossDefBonus;
            if (bd != 0)
            {
                double stage = d.StageDef - d.StagePlate, cut = DefenseReduction(bd) * 100;
                m.Notes.Add(d.StagePlate != 0 && d.BossDefBonus != 0 ? Tr("model.boss_defense_plate_affix", bd, stage, d.StagePlate, d.BossDefBonus, cut)
                            : d.StagePlate != 0 ? Tr("model.boss_defense_plate", bd, stage, d.StagePlate, cut)
                            : d.BossDefBonus != 0 ? Tr("model.boss_defense_affix", bd, stage, d.BossDefBonus, cut)
                            : Tr("model.boss_defense", bd, stage, cut));
            }
        }
        if (d.EnemyDef != 0) m.Notes.Add(Tr("model.nearby_enemy_defense_used", d.EnemyDef, (d.Windows != null ? Tr("model.measured_recent_fights") : Tr("model.measured_this_run_this"))));

        if (byLevel > 0) m.Notes.Add(Tr("model.rates_that_change_level", byLevel));
        if (probed > 0) m.Notes.Add(Tr("model.damage_that_scales_stats", probed));
        if (estimated > 0) m.Notes.Add(Tr("model.estimated_from_elemental_atk", estimated));
        m.Notes.Add(Tr("model.mechanics_database_covers_sources", modeled, sources.Count));

        info = Tr("model.damage_sources_damage_computed", d.Sources.Length, d.SwingInfo);
        if (d.Windows != null)
        {
            double all = windows.Sum(w => w.Other + w.Measured.Sum()), other = windows.Sum(w => w.Other);
            info += all > 0 ? Tr("model.last_s_combat_layout", windowSeconds, windows.Count, (all - other) / all)
                            : Tr("model.last_s_combat_layout_available", windowSeconds, windows.Count);
        }
        return d;
    }

    static bool KeyChangeable(DpsModel d, int key)
    {
        if (key < 0) return false;
        if (d.Adds != null)
            foreach (var list in d.Adds)
                if (list != null)
                    foreach (var a in list)
                        if (a.Key == key) return true;
        if (d.Tiers != null)
            foreach (var tiers in d.Tiers)
                foreach (var t in tiers)
                    foreach (var a in t.Adds)
                        if (a.Key == key) return true;
        return false;
    }
}
