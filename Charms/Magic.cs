using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public partial class SephiriaToolbox
{
    const double MeteorSpread = 6.5, MeteorSeekRange = 1.0, MeteorSeekShare = 2.0 / 3;
    const int MeteorSeekTries = 61;

    sealed class MagicBuff
    {
        public int Item;
        public double Cooldown = 10, Dur;
        public int Ammo = 1;
        public float[] Cost, CostBase;
        public int CostAdd;
        public (int Key, byte Mode, double Value)[] Stats;
    }

    static double StayBulletHits(GameObject prefab)
    {
        var bl = prefab != null ? prefab.GetComponent<Bullet>() : null;
        if (bl == null || bl.collosionType != Bullet.ECollisionTiming.Stay) return 1;
        var mv = prefab.GetComponent<BulletMoveModule>();
        if (mv is not BulletMoveModule_BlinkToTargetPosition || !mv.destroyOnTime || mv.destroyTimer == null) return 1;
        double life = mv.destroyTimer.time;
        var vis = prefab.GetComponent<ArrowRainBulletVisual>();
        if (vis != null) life -= vis.collisionActivateDelay;
        double interval = Math.Max(0.05, bl.collisionStayDamageIntervalTimer != null ? bl.collisionStayDamageIntervalTimer.time : 0.25) + 1 / 60.0;
        double n = 1 + Math.Floor(Math.Max(0, life) / interval);
        return bl.pierceCreatureCount > 0 ? Math.Min(n, bl.pierceCreatureCount) : n;
    }

    static double MeteorHitsPerMeteor(GameObject meteorPrefab, double enemies)
    {
        double radius = 2.25, shape = 0.5;
        var ex = meteorPrefab != null ? meteorPrefab.GetComponent<BulletDestroyModule_Explode>() : null;
        if (ex != null)
        {
            radius = ex.explodeRadius * (1 + ex.explodeRadiusBonusPercent / 100.0);
            shape = ex.explosionShape == BulletDestroyModule_Explode.EExplosionShape.Ellipse ? 0.5 : 1;
        }
        double n = Math.Max(1, enemies);
        double near = Math.Min(1, n * MeteorSeekRange * MeteorSeekRange / (MeteorSpread * MeteorSpread));
        double found = MeteorSeekShare * (1 - Math.Pow(1 - near, MeteorSeekTries));
        double blast = Math.Min(1, radius * radius * shape / (MeteorSpread * MeteorSpread));
        return found * (1 + (n - 1) * blast) + (1 - found) * n * blast;
    }

    static bool ApplyMagicMech(DpsSource s, ActiveSkill skill, Behavior b, int levels, int multiCast)
    {
        float hitsPerCast = 1;
        float[] byLevel = null, byLevelM = null;
        bool known = true;
        float chainM = 1;
        float area = (float)Math.Max(1, 0.6 * b.EnemiesM) / (float)Math.Max(1, 0.6 * b.Enemies);
        switch (skill)
        {
            case ActiveSkill_LightningArmor:
            {
                s.Default ??= ReadFloats(skill, "damagesByLevel");
                s.RelKey ??= "LIGHTNINGDAMAGE";
                float duration = TimerSeconds(skill, "durationTimer"), tick = TimerSeconds(skill, "damageTickTimer");
                hitsPerCast = (duration > 0 && tick > 0 ? duration / tick : 30) * (float)Math.Min(3, b.Enemies);
                chainM = (float)(Math.Min(3, b.EnemiesM) / Math.Max(1e-6, Math.Min(3, b.Enemies)));
                s.HitInterval = tick > 0 ? tick : 0.33f;
                s.Note = Tr("trigger.lasts_s_chain_lightning", duration, tick);
                break;
            }
            case ActiveSkill_Bolt bolt:
                hitsPerCast = Math.Max(1, (float)Math.Ceiling(bolt.fireCount));
                s.HitInterval = 0.1f;
                break;
            case ActiveSkill_Arrow arrow:
                s.PowerOnStat = true;
                chainM = (float)Math.Max(1, Math.Min(b.EnemiesM, Math.Max(1, arrow.multiShotCount)) * 0.6) / (float)Math.Max(1, Math.Min(b.Enemies, Math.Max(1, arrow.multiShotCount)) * 0.6);
                break;
            case ActiveSkill_ArrowRain rain:
                hitsPerCast = Math.Max(1, rain.bulletCount) * (float)StayBulletHits(SafeAt(rain.bulletPrefabByPower, 0));
                chainM = area;
                s.HitInterval = 0.2f;
                break;
            case ActiveSkill_FireBullet fb:
                hitsPerCast = Math.Max(1, fb.bulletCount) * (float)StayBulletHits(SafeAt(fb.bulletPrefabByPower, 0));
                break;
            case ActiveSkill_Projectile pj:
            {
                var prefab = SafeAt(pj.projectilePrefabs, 0);
                var zone = prefab != null ? prefab.GetComponent<SpecialProjectile_AreaJudgement>() : null;
                int attacks = 1;
                if (zone != null)
                {
                    attacks = Math.Max(1, zone.attackCount);
                    if (zone.bladeZoneAttackTimer != null && zone.bladeZoneAttackTimer.time > 0 && zone.destroyTimer != null)
                        attacks = Math.Min(attacks, Math.Max(1, (int)Math.Floor(zone.destroyTimer.time / zone.bladeZoneAttackTimer.time + 1e-6)));
                    chainM = area;
                    s.HitInterval = zone.bladeZoneAttackTimer != null ? zone.bladeZoneAttackTimer.time : 0.08f;
                }
                hitsPerCast = Math.Max(1, pj.bulletCount) * attacks;
                break;
            }
            case ActiveSkill_MeteorShower ms:
            {
                var n = LevelTable(skill, "numberOfMeteorsByLevel");
                if (n == null) break;
                byLevel = n.Select(v => (float)(Math.Max(0, v - 1) * MeteorHitsPerMeteor(ms.meteorBulletPrefab, b.Enemies))).ToArray();
                byLevelM = n.Select(v => (float)(Math.Max(0, v - 1) * MeteorHitsPerMeteor(ms.meteorBulletPrefab, b.EnemiesM))).ToArray();
                s.HitInterval = ms.meteorFireTimer != null && ms.meteorFireTimer.time > 0 ? ms.meteorFireTimer.time : 0.36f;
                break;
            }
            case ActiveSkill_Ball ball:
                hitsPerCast = 8;
                if (ball.criticalChanceBonusByLevel != null && ball.criticalChanceBonusByLevel.Any(v => v != 0))
                    s.CritAdd = ball.criticalChanceBonusByLevel.Select(v => (float)v).ToArray();
                s.HitInterval = 0.05f;
                break;
            case ActiveSkill_LightningBoomerang:
                hitsPerCast = 1 + (float)CloseUptime(null, 2.75, wide: true);
                s.HitInterval = 1f;
                break;
            case ActiveSkill_Summon:
            case ActiveSkill_CallLightning:
            case ActiveSkill_FireBulletsToEnemies:
            case ActiveSkill_WhirlWind:
                known = false;
                break;
            default:
                if (FieldValue(skill, "bulletCount") is int bc && bc > 1) hitsPerCast = bc;
                break;
        }
        int mc = Math.Max(1, multiCast);
        var hits = byLevel != null ? Per(levels, l => SafeAt(byLevel, l) * mc) : Enumerable.Repeat(hitsPerCast * mc, levels).ToArray();
        s.Rate = hits.Any(v => Math.Abs(v - 1f) > 1e-6f) ? hits : null;
        if (byLevelM != null) s.RateM = Per(levels, l => SafeAt(byLevelM, l) * mc);
        else s.RateM = Math.Abs(chainM - 1f) > 1e-6f ? hits.Select(v => v * chainM).ToArray() : null;
        if (s.Default == null && s.Percent == null) known = false;
        return known;
    }

    static void ReadMagicCost(Charm_Magic cm, PlayerAvatar avatar, Charm_Basic[] charmOf, out float[] cost, out float[] costBase, out int costAdd)
    {
        cost = null; costBase = null; costAdd = 0;
        try { cost = Enumerable.Range(0, Math.Max(1, cm.maxLevel + 1)).Select(l => (float)cm.GetCost(avatar, l)).ToArray(); } catch { }
        try
        {
            costBase = cm.ContainedMagic?.mpCostsByLevel?.Select(v => (float)v).ToArray();
            int add = cm.AdditionalCost;
            foreach (var other in charmOf)
                if (other is Charm_ReduceMPCost rc && ReferenceEquals(FieldValue(rc, "currentMagicCharm"), cm) && Num(rc, "reduceActivated") > 0)
                    add += (int)Num(rc, "reducedPercent");
            costAdd = add;
        }
        catch { costBase = null; }
    }

    static bool ReadMagicBuff(Charm_Magic cm, ActiveSkill_Buff skill, int item, PlayerAvatar avatar, Charm_Basic[] charmOf, DpsModel d, Func<string, int> key,
                              Dictionary<int, double> curRaw, Dictionary<int, double> curAmp)
    {
        var prefab = skill.buffPrefab;
        var entity = cm.ContainedMagic;
        if (prefab == null || entity == null) return false;
        var stats = BuffStats(prefab);
        if (stats.Count == 0) return false;
        double dur = prefab.defaultDuration;
        if (!prefab.ignoreDurationBonus) dur *= Pct(avatar.GetCustomStat(ECustomStat.BuffDuration));
        var mb = new MagicBuff
        {
            Item = item, Cooldown = Math.Max(0.1, entity.cooldownTime), Ammo = Math.Max(1, entity.ammo), Dur = dur,
            Stats = stats.Select(x => (key(x.Key), x.Mode, x.Value)).ToArray()
        };
        ReadMagicCost(cm, avatar, charmOf, out mb.Cost, out mb.CostBase, out mb.CostAdd);
        d.MagicBuffs.Add(mb);
        if (BuffsField?.GetValue(avatar) is Dictionary<string, CharacterBuff> buffs && !string.IsNullOrEmpty(prefab.ID)
            && buffs.TryGetValue(prefab.ID, out var active) && active != null)
            foreach (var (k, mode, v) in mb.Stats)
            {
                var target = mode == 1 ? curAmp : curRaw;
                target[k] = target.GetValueOrDefault(k) + v * active.Amplified * active.CurrentStack;
            }
        return true;
    }

    sealed partial class Evaluator
    {
        void MagicBuffStats()
        {
            foreach (var mb in d.MagicBuffs)
            {
                if (!ItemOn[mb.Item]) continue;
                double up = Math.Min(1, manualRate[mb.Item] * mb.Dur);
                if (up <= 0) continue;
                foreach (var (k, mode, v) in mb.Stats)
                    if (mode == 1) amp[k] += v * up; else raw[k] += v * up;
            }
        }
    }
}
