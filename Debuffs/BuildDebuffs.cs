using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    void BuildDebuffs(PlayerAvatar avatar, ArrModel m, Charm_Basic[] charmOf, DpsModel d, Func<string, int> K, Behavior play, List<DpsSource> sources)
    {
        d.Debuffs.Clear();
        d.BurnIdx = d.ElecIdx = d.PoisonIdx = d.PlasmaIdx = -1;
        d.kPlasma = K("PLASMAACTIVE"); d.kPlasmaDmg = K("PLASMADAMAGE"); d.kDebuffDur = K("DEBUFFDURATION");
        var weapon = ModelWeapon(avatar);
        var hm = d.Hits;
        hm.Swing = play.Swing * play.SwingHit * play.HitsPerSwing;
        hm.SwingM = play.Swing * play.SwingHit * play.HitsPerSwingM;
        hm.Special = play.Special * play.HitsPerSwing;
        hm.SpecialM = play.Special * play.HitsPerSwingM;
        hm.Other = (play.Special + play.DashAttack + play.Strike) * play.HitsPerSwing;
        hm.OtherM = (play.Special + play.DashAttack + play.Strike) * play.HitsPerSwingM;
        var fd = weapon != null && weapon.basicComboAttacks != null ? weapon.basicComboAttacks.FirstOrDefault(x => x != null) : null;
        hm.WeaponElem = fd != null ? ElemIndex(fd.damageElementalType) : 0;

        var cm = CombatManager.Instance;
        CharacterDebuff Prefab(DebuffType t)
        {
            try
            {
                switch (t)
                {
                    case DebuffType.Burn: return cm != null ? cm.burnDebuffPrefab : UnitDatabase.GetDebuff("BURN");
                    case DebuffType.Electric: return cm != null ? cm.electricDebuffPrefab : UnitDatabase.GetDebuff("ELECTRIC");
                    case DebuffType.Frostbite: return cm != null ? cm.frostbiteDebuffPrefab : UnitDatabase.GetDebuff("FROSTBITE");
                    case DebuffType.Poison: return UnitDatabase.GetDebuff("POISON");
                    case DebuffType.Wound: return UnitDatabase.GetDebuff("WOUND");
                    case DebuffType.Plasma: return UnitDatabase.GetDebuff("PLASMA");
                }
            }
            catch { }
            return null;
        }
        string[] sourceIds = { "Debuff_Burn", "Debuff_Electric", "Debuff_Frostbite", "Debuff_Poison", "Debuff_Wound", "Debuff_Plasma" };
        string[] directKeys = { "DIRECTATTACKBURN", "DIRECTATTACKELECTRIC", "DIRECTATTACKFROSTBITE", null, null, null };
        bool plasmaPossible = avatar.GetCustomStatUnsafe("PLASMAACTIVE") > 0 || KeyChangeable(d, d.kPlasma)
                              || sources.Any(x => x.Ids.Contains("Debuff_Plasma"));
        for (int t = 0; t < DebuffIds.Length; t++)
        {
            var type = (DebuffType)t;
            if (type == DebuffType.Plasma && !plasmaPossible) continue;
            var di = new DebuffInfo { Type = type };
            if (type is DebuffType.Electric or DebuffType.Plasma) { di.Renew = false; di.Duration0 = 2; di.StatPct = 180; }
            var prefab = Prefab(type);
            if (prefab != null)
            {
                di.Duration0 = Math.Max(0.1, prefab.defaultDuration);
                di.Renew = prefab.renewDurationOnStacked;
                switch (prefab)
                {
                    case CharacterDebuff_Burn b: if (b.tickTimer != null && b.tickTimer.time > 0) di.Tick0 = b.tickTimer.time; break;
                    case CharacterDebuff_Electric el: di.StatPct = el.statDamagePercent; break;
                    case CharacterDebuff_Plasma pl:
                        di.StatPct = pl.statDamagePercent;
                        if (pl.tickTimer != null && pl.tickTimer.time > 0) di.Tick0 = pl.tickTimer.time;
                        break;
                    case CharacterDebuff_Poison p: di.Dmg0 = p.damage; if (p.poisonTickTimer != null && p.poisonTickTimer.time > 0) di.Tick0 = p.poisonTickTimer.time; break;
                    case CharacterDebuff_Wound w: di.Dmg0 = w.damage; if (w.tickTimer != null && w.tickTimer.time > 0) di.Tick0 = w.tickTimer.time; break;
                    case CharacterDebuff_Frostbite fb:
                        if (fb.freezeDebuffPrefab is CharacterDebuff_Freeze fz) di.FreezeMul = fz.damageMultiplier;
                        di.Tick0 = 1;
                        break;
                }
            }
            switch (type)
            {
                case DebuffType.Burn:
                    di.kStack = K("BURNSTACK"); di.kAdd = K("BURNADD"); di.kDmg = K("BURNDAMAGE"); di.kSpeed = K("BURNSPEED");
                    di.kDur = K("BURNDURATION"); di.kEvo = K("BURNEVO"); di.kBlue = K("BLUEBURNCHANGE");
                    break;
                case DebuffType.Electric:
                    di.kStack = K("ELECTRICSTACK"); di.kDmg = K("ELECTRICDAMAGE"); di.kLuck = K("ELECTRICLUCK"); di.kQuick = K("ELECTRICQUICKNESS");
                    break;
                case DebuffType.Plasma:
                    di.kStack = K("BURNSTACK"); di.kStack2 = K("ELECTRICSTACK"); di.kAdd = K("BURNADD");
                    di.kDmg = K("BURNDAMAGE"); di.kDmg2 = K("ELECTRICDAMAGE"); di.kSpeed = K("BURNSPEED"); di.kDur = K("BURNDURATION");
                    di.kEvo = K("BURNEVO"); di.kBlue = K("BLUEBURNCHANGE"); di.kLuck = K("ELECTRICLUCK"); di.kQuick = K("ELECTRICQUICKNESS");
                    break;
                case DebuffType.Frostbite:
                    di.kFrostDmg = K("FROSTBITEDAMAGE"); di.kFreezeDmg = K("FREEZEDAMAGE"); di.kFreezeTh = K("FREEZETHRESHOLD");
                    break;
                case DebuffType.Poison:
                    di.kStack = K("POISONSTACK"); di.kDmg = K("POISONDAMAGE");
                    break;
                case DebuffType.Wound:
                    di.kStack = K("WOUNDSTACK"); di.kDmg = K("WOUNDDAMAGE");
                    break;
            }

            if (type != DebuffType.Plasma)
            {
                for (int c = 0; c < d.CatNames.Length; c++)
                {
                    try
                    {
                        var entity = ItemDatabase.FindItemCategory(d.CatNames[c]);
                        if (entity == null || entity.comboEffectPrefab == null || entity.comboEffectPrefab.GetComponent<ComboEffectBase>() is not ComboEffect_Debuff cd) continue;
                        if (cd.debuffPrefab == null || !cd.debuffPrefab.CompareID(DebuffIds[t])) continue;
                        di.Appliers.Add(new DebuffApplier
                        {
                            Kind = 0, Cat = c, Activate = cd.debuffActivateComboCount, Cooldown = Math.Max(0.1, cd.debuffActivateCooldownTime),
                            HasteCount = cd.debuffHasteComboCount, Haste = cd.debuffHastePercent, Elem = ElemIndex(cd.damageElementalTargetType),
                            Name = Tr("debuff.combo")
                        });
                    }
                    catch (Exception e) { WarnOnce("减益连击 " + d.CatNames[c], e); }
                }
                if (type == DebuffType.Frostbite)
                    for (int i = 0; i < charmOf.Length; i++)
                        if (charmOf[i] is Charm_Freeze fz && fz.freezePercents != null)
                            di.ExtraStack.Add((i, fz.freezePercents.Select(x => (float)x).ToArray()));
                if (directKeys[t] != null) di.Appliers.Add(new DebuffApplier { Kind = 1, Key = K(directKeys[t]), Name = Tr("debuff.hit_chance") });
                for (int i = 0; i < charmOf.Length; i++)
                {
                    var c = charmOf[i];
                    if (c == null) continue;
                    try
                    {
                        if (c is Charm_BlazingStormcloud)
                        {
                            if (type == DebuffType.Burn) di.Appliers.Add(new DebuffApplier { Kind = 3, Item = i, Name = m.Items[i].Name });
                            continue;
                        }
                        if (c is Charm_AttackChim or Charm_FarChimDamage or Charm_FrozenMemory or Charm_SnowNeckless) continue;
                        var db = DebuffFieldOf(c);
                        if (db == null || !db.CompareID(DebuffIds[t])) continue;
                        int L = Math.Max(1, c.maxLevel + 1);
                        int si = sources.FindIndex(x => x.Item == i && x.Kind == SrcKind.Proc);
                        float[] chance = null;
                        if (c is Charm_FireChakram)
                        {
                            var pct = LevelTable(c, "debuffPercentByLevel");
                            if (pct != null) chance = pct.Select(v => Math.Max(0f, v) / 100f).ToArray();
                        }
                        if (c is Charm_EchoOfTheGlacier)
                        {
                            var iv = LevelTable(c, "frostbiteTimeByLevel");
                            di.Appliers.Add(new DebuffApplier { Kind = 2, Item = i, PerTarget = true, PerLevel = Per(L, l => SafeAt(iv, l) > 0 ? 1 / SafeAt(iv, l) : 0), Name = m.Items[i].Name });
                        }
                        else if (si >= 0)
                            di.Appliers.Add(new DebuffApplier { Kind = 2, Item = i, Src = si, ChanceTable = chance, Name = m.Items[i].Name });
                        else
                            di.Appliers.Add(new DebuffApplier { Kind = 2, Item = i, PerLevel = new[] { (float)PlayUnknown }, Name = m.Items[i].Name });
                    }
                    catch (Exception e) { WarnOnce("减益施加 " + m.Items[i].Name, e); }
                }
                for (int si = 0; si < sources.Count; si++)
                {
                    var s = sources[si];
                    if (s.Kind != SrcKind.Magic || s.Item < 0 || charmOf[s.Item] is not Charm_Magic cmg) continue;
                    try
                    {
                        var skill = cmg.ContainedMagic != null && cmg.ContainedMagic.magicPrefab != null ? cmg.ContainedMagic.magicPrefab.GetComponent<ActiveSkill>() : null;
                        var db = skill != null ? DebuffFieldOf(skill, typeof(ActiveSkill)) : null;
                        if (db == null || !db.CompareID(DebuffIds[t])) continue;
                        float per = skill switch
                        {
                            ActiveSkill_Arrow ar => Math.Max(0, ar.debuffStack),
                            ActiveSkill_ArrowRain rain => Math.Min(100, Math.Max(0, rain.debuffPercent)) / 100f,
                            _ => 1f
                        };
                        di.Appliers.Add(new DebuffApplier { Kind = 2, Item = s.Item, Src = si, ChanceTable = Math.Abs(per - 1f) > 1e-6f ? new[] { per } : null, Name = m.Items[s.Item].Name });
                    }
                    catch (Exception e) { WarnOnce("减益施加 " + m.Items[s.Item].Name, e); }
                }
                if (type is DebuffType.Burn or DebuffType.Frostbite or DebuffType.Electric)
                {
                    double pct = 0;
                    try { pct = KeywordDatabase.GetConstValue("planetDebuffPercent"); } catch { }
                    if (pct > 0)
                        for (int i = 0; i < charmOf.Length; i++)
                            if (charmOf[i] is Charm_SummonGreenBat)
                            {
                                int si = sources.FindIndex(x => x.Item == i && x.Kind == SrcKind.Proc);
                                if (si < 0) continue;
                                di.Appliers.Add(new DebuffApplier { Kind = 2, Item = i, Src = si, ChanceTable = new[] { (float)(pct / 100.0) }, Key = K("PLANETDEBUFF"), Name = m.Items[i].Name });
                            }
                }
                if (weapon != null && weapon.addons != null)
                    foreach (var ad in weapon.addons)
                    {
                        switch (ad)
                        {
                            case WeaponAddonCommon_DebuffAttack da when da.debuffPrefab != null && da.debuffPrefab.CompareID(DebuffIds[t]):
                                di.Appliers.Add(new DebuffApplier { Kind = 4, Chance = da.debuffPercent / 100.0, Name = Tr("common.weapon") });
                                break;
                            case WeaponAddonCommon_DebuffAttack_OnlySpecialAttack ds when ds.debuffPrefab != null && ds.debuffPrefab.CompareID(DebuffIds[t]):
                                di.Appliers.Add(new DebuffApplier { Kind = 5, Chance = ds.debuffCount, Name = Tr("debuff.weapon_special") });
                                break;
                            case WeaponAddonCommon_SpecialAttackDebuff sd when sd.debuffPrefab != null && sd.debuffPrefab.CompareID(DebuffIds[t]):
                                di.Appliers.Add(new DebuffApplier { Kind = 5, Chance = sd.debuffPercent / 100.0, Name = Tr("debuff.weapon_special") });
                                break;
                        }
                    }
            }

            int si0 = sources.FindIndex(x => x.Kind == SrcKind.Ability && x.Ids.Contains(sourceIds[t]));
            bool anyApplier = type == DebuffType.Plasma || di.Appliers.Any(a => a.Kind != 1)
                              || directKeys[t] != null && avatar.GetCustomStatUnsafe(directKeys[t]) > 0;
            if (si0 < 0 && anyApplier && type != DebuffType.Frostbite)
            {
                var ns = new DpsSource { Kind = SrcKind.Ability, Name = Tr("src.debuff", DamageIdName(sourceIds[t])) };
                ns.Ids.Add(sourceIds[t]);
                sources.Add(ns);
                si0 = sources.Count - 1;
            }
            if (type == DebuffType.Frostbite)
            {
                int fz = sources.FindIndex(x => x.Kind == SrcKind.Ability && x.Ids.Contains("Debuff_Freeze"));
                if (fz < 0 && anyApplier)
                {
                    var ns = new DpsSource { Kind = SrcKind.Ability, Name = Tr("src.debuff", DamageIdName("Debuff_Freeze")) };
                    ns.Ids.Add("Debuff_Freeze");
                    sources.Add(ns);
                    fz = sources.Count - 1;
                }
                di.Source2 = fz;
            }
            di.Source = si0;
            if (si0 < 0 && di.Source2 < 0 && !(type is DebuffType.Burn or DebuffType.Electric && di.Appliers.Count > 0)) continue;
            foreach (var idx in new[] { di.Source, di.Source2 })
            {
                if (idx < 0) continue;
                var s = sources[idx];
                MakeEco(s, EcoKind.Debuff);
                s.Debuff = d.Debuffs.Count;
                s.DebuffPart = (byte)(idx == di.Source2 ? 1 : 0);
                s.NoCrit = false;
                s.DmgElem = type switch { DebuffType.Burn => 1, DebuffType.Electric => 3, DebuffType.Frostbite => 2, DebuffType.Plasma => 1, _ => -1 };
                s.Note = type switch
                {
                    DebuffType.Burn => Tr("debuff.stacks_tick_rate_application"),
                    DebuffType.Electric => Tr("debuff.duration_doesnt_refresh_pops"),
                    DebuffType.Plasma => Tr("debuff.ticks_expiry_pops_luck"),
                    DebuffType.Frostbite => idx == di.Source2 ? Tr("debuff.frostbite_turns_into_freeze") : Tr("debuff.frostbite_damage_per_second"),
                    _ => Tr("debuff.stacks_damage_per_second")
                };
            }
            if (DebuffMeasured(DebuffIds[t], out var tg, out var st))
            {
                di.Targets = tg;
                di.Stacks = st;
            }
            if (type == DebuffType.Burn) d.BurnIdx = d.Debuffs.Count;
            if (type == DebuffType.Electric) d.ElecIdx = d.Debuffs.Count;
            if (type == DebuffType.Poison) d.PoisonIdx = d.Debuffs.Count;
            if (type == DebuffType.Plasma) d.PlasmaIdx = d.Debuffs.Count;
            d.Debuffs.Add(di);
        }
        int modeledAppliers = d.Debuffs.Sum(x => x.Appliers.Count(a => a.Kind != 1));
        int withSource = d.Debuffs.Count(x => x.Source >= 0 || x.Source2 >= 0);
        if (withSource > 0)
            m.Notes.Add(d.Debuffs.Any(x => x.Stacks > 0)
                ? Tr("debuff.debuff_type_modeled_stacks", withSource, modeledAppliers)
                : Tr("debuff.debuff_type_modeled_stacks_duration", withSource, modeledAppliers));
    }

    static CharacterDebuff DebuffFieldOf(object c, Type stop = null)
    {
        stop ??= typeof(Charm_Basic);
        for (var t = c.GetType(); t != null && t != stop && t != typeof(MonoBehaviour); t = t.BaseType)
            foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                if (typeof(CharacterDebuff).IsAssignableFrom(f.FieldType) && f.GetValue(c) is CharacterDebuff db && db != null) return db;
        return null;
    }
}
