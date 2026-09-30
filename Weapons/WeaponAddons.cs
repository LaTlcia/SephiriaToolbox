using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed class CondDamage
    {
        public int Key = -1;
        public byte Cond;
        public int Value;
        public double Pct;
    }

    static readonly string[] CondSymbols = { "≤", "≥", "=", "≠" };

    static void BuildWeaponCondDamage(ArrModel m, DpsModel d, Func<string, int> K, WeaponSimple weapon)
    {
        if (weapon?.addons == null) return;
        foreach (var c in weapon.addons.OfType<WeaponAddonCommon_ConditionalStat>())
        {
            if (c == null || string.IsNullOrEmpty(c.sourceStatId) || c.addDamagePercent == 0) continue;
            string key = c.sourceStatId.ToUpperInvariant();
            byte cond = (byte)Math.Min(3, Math.Max(0, (int)c.condition));
            d.Weapon.CondDmg.Add(new CondDamage { Key = K(key), Cond = cond, Value = c.sourceStatValue, Pct = c.addDamagePercent });
            string what = key == "CRITICAL" ? Tr("weapon.crit_chance", CondSymbols[cond], c.sourceStatValue / 100.0) : $"{key} {CondSymbols[cond]} {c.sourceStatValue}";
            m.Notes.Add(Tr("weapon.conditional_damage_all_damage", what, c.addDamagePercent));
        }
    }

    void WeaponAttackBuffs(PlayerAvatar avatar, ArrModel m, DpsModel d, Func<string, int> K, WeaponSimple weapon, Dictionary<int, double> curRaw)
    {
        var live = avatar.GetComponent<WeaponControllerSimple>()?.currentWeapon;
        if (live?.addons != null && BuffsField?.GetValue(avatar) is Dictionary<string, CharacterBuff> buffs)
            foreach (var ab in live.addons.OfType<WeaponAddonCommon_AttackBuff>())
                if (ab?.buffPrefab != null && !string.IsNullOrEmpty(ab.buffPrefab.ID) && buffs.TryGetValue(ab.buffPrefab.ID, out var active) && active != null)
                    foreach (var (key, mode, v) in BuffStats(ab.buffPrefab))
                        if (mode == 0) curRaw[K(key)] = curRaw.GetValueOrDefault(K(key)) + v * active.Amplified * active.CurrentStack;
        if (weapon?.addons == null) return;
        foreach (var ab in weapon.addons.OfType<WeaponAddonCommon_AttackBuff>())
        {
            var prefab = ab?.buffPrefab;
            if (prefab == null) continue;
            double dur = prefab.defaultDuration * (prefab.ignoreDurationBonus ? 1 : Pct(avatar.GetCustomStat(ECustomStat.BuffDuration)));
            int max = Math.Max(1, prefab.MaxStackCount);
            double rate = buildWeaponHits * Math.Min(100, Math.Max(0, ab.buffPercent)) / 100.0;
            double stacks = BuffAvgStacks(rate, dur, max);
            foreach (var (key, mode, v) in BuffStats(prefab))
                if (mode == 0) d.ExtraBase[K(key)] = d.ExtraBase.GetValueOrDefault(K(key)) + v * stacks;
            m.Notes.Add(Tr("weapon.attack_buff_s_weapon", dur, (max > 1 ? Tr("weapon.up_stacks", max) : ""), buildWeaponHits, stacks));
        }
    }

    void AddBurnRingSource(DpsModel d, Func<string, int> K, WeaponSimple weapon, Behavior play, List<DpsSource> sources)
    {
        if (weapon?.addons == null) return;
        foreach (var br in weapon.addons.OfType<WeaponAddonCommon_BurnRing>())
        {
            if (br == null || string.IsNullOrEmpty(br.damageId) || sources.Any(x => x.Ids.Contains(br.damageId))) continue;
            double tick = br.burnRingTickTimer != null && br.burnRingTickTimer.time > 0 ? br.burnRingTickTimer.time : 0.5;
            d.Weapon.BurnRingTime = br.burnRingDuration > 0 ? br.burnRingDuration : 10;
            int e = Array.IndexOf(ElemKeys, (br.relatedStatUnsafe ?? "FIREDAMAGE").ToUpperInvariant());
            double near = play.Single && buildCloseCdf != null ? CloseShare(buildCloseCdf, br.burnRingRadius) : 1;
            var s = new DpsSource
            {
                Kind = SrcKind.Ability, Name = Tr("src.weapon_ring_fire"), BurnRing = true, Preset = true,
                Table = new[] { br.damagePercent / 100f }, ElemIdx = e >= 0 ? e : 1,
                MulKeys = new[] { K("WEAPONDAMAGEBONUS"), K("FINALWEAPONDAMAGE"), K("BURNDAMAGE") },
                Prior = near / tick, TheoryK = near / tick, HasTheory = true, DmgElem = ElemIndex(br.elementalType),
                MultiScale = Math.Max(1, play.EnemiesM * 0.6) / Math.Max(0.05, near),
                Note = Tr("weapon.s_after_applying_burn", br.burnRingDuration, tick, br.burnRingRadius, br.damagePercent)
            };
            s.X = new List<XMod> { new XMod { Kind = XKind.RatePct, Key = K((br.relatedTickStatName ?? "BURNSPEED").ToUpperInvariant()) } };
            s.Ids.Add(br.damageId);
            sources.Add(s);
        }
    }

    sealed partial class Evaluator
    {
        void ApplyCondDamage()
        {
            foreach (var c in d.Weapon.CondDmg)
            {
                if (c.Key < 0) continue;
                double v = Math.Truncate(T(c.Key));
                bool ok = c.Cond switch { 0 => v <= c.Value, 1 => v >= c.Value, 2 => v == c.Value, _ => v != c.Value };
                if (ok) modMul[0] *= 1 + c.Pct / 100.0;
            }
        }

        double BurnRingUp(DpsSource s)
        {
            double lam = dbApply[(int)DebuffType.Burn] + dbApply[(int)DebuffType.Plasma];
            if (lam <= 0) return s.Measured > 0 ? 1 : 0;
            return 1 - Math.Exp(-Math.Min(30, lam * d.Weapon.BurnRingTime));
        }
    }
}
