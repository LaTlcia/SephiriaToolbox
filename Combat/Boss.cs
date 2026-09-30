using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    const int BossKeep = 12;
    const float BossGrace = 10f;
    const double DefaultBossFight = 20;
    const float BossBin = 0.5f;
    const int BossBins = 21;

    uint bossOwner;
    bool bossActive, bossWasInBattle;
    float bossFight, bossGone, bossSampleTimer, bossLastHp = -1f;
    int bossRefills;

    void TrackBosses()
    {
        var a = LocalAvatar();
        if (a == null) return;
        if (bossOwner != a.netId) { bossOwner = a.netId; bossActive = false; bossLastHp = -1f; }
        float dt = Time.deltaTime;
        bool inBattle = a.IsInBattle && !a.IsDead;
        if (bossActive && inBattle && bossLastHp >= 0f && a.hp < bossLastHp - 0.5f) settings.bossHits++;
        bossLastHp = a.hp;
        if (bossActive && inBattle)
        {
            bossFight += dt;
            settings.bossSeconds += dt;
            if (!bossWasInBattle && bossFight > 1f) bossRefills++;
        }
        bossWasInBattle = inBattle;
        bossSampleTimer -= dt;
        if (bossSampleTimer > 0f) return;
        bossSampleTimer = 0.25f;

        UnitAvatar boss = null;
        float best = float.MaxValue;
        int adds = 0;
        var cm = CombatManager.Instance;
        if (!a.IsDead && cm != null && cm.AllCreatures != null)
        {
            var pos = a.transform.position;
            foreach (var u in cm.AllCreatures)
            {
                if (u == null || u == a || u.IsDead || !u.gameObject.activeSelf) continue;
                if (!CombatManager.ContainsAttackableFaction(u.GetHostileFactionLayers(EDamageFromType.None), a.faction)) continue;
                float dist = (u.transform.position - pos).magnitude;
                if (u.monsterType is EMonsterType.Boss or EMonsterType.Miniboss)
                {
                    if (dist < best) { best = dist; boss = u; }
                }
                else if (dist <= 8f && !u.canBeTarget.IsFalse()) adds++;
            }
        }
        if (boss != null)
        {
            if (!bossActive) { bossActive = true; bossFight = 0f; bossRefills = 0; bossWasInBattle = inBattle; }
            bossGone = 0f;
            if (!inBattle) return;
            var hist = BossHist();
            hist[Math.Min(BossBins - 1, (int)(best / BossBin))]++;
            bool otherBurn = false, otherFrost = false;
            int others = boss.isInStun ? 1 : 0;
            foreach (var db in boss.Debuffs)
            {
                if (db == null || db.IsEndBuff || db.NetworkAttacker == a) continue;
                others++;
                if (db.CompareID("BURN")) otherBurn = true;
                if (db.CompareID("FROSTBITE")) otherFrost = true;
            }
            settings.bossDebuffSamples++;
            settings.bossOtherDebuffs += others;
            if (otherBurn) settings.bossOtherBurn++;
            if (otherFrost) settings.bossOtherFrost++;
            if (settings.bossDebuffSamples > 40000)
            {
                settings.bossDebuffSamples /= 2; settings.bossOtherBurn /= 2; settings.bossOtherFrost /= 2; settings.bossOtherDebuffs /= 2;
            }
            if (hist.Sum() > 40000) for (int i = 0; i < hist.Length; i++) hist[i] /= 2;
            settings.bossAddSum += adds;
            settings.bossAddSamples++;
            settingsDirty = true;
        }
        else if (bossActive)
        {
            bossGone += 0.25f;
            if (bossGone >= BossGrace) FinishBossFight();
        }
    }

    void FinishBossFight()
    {
        bossActive = false;
        if (bossFight >= 2f)
        {
            settings.bossFights ??= new List<float>();
            settings.bossPhases ??= new List<int>();
            settings.bossFights.Add((float)Math.Round(bossFight, 1));
            settings.bossPhases.Add(bossRefills);
            while (settings.bossFights.Count > BossKeep) settings.bossFights.RemoveAt(0);
            while (settings.bossPhases.Count > BossKeep) settings.bossPhases.RemoveAt(0);
            SaveSettings();
        }
        bossFight = 0f;
    }

    int[] BossHist()
    {
        if (settings.bossDist == null || settings.bossDist.Length != BossBins) settings.bossDist = new int[BossBins];
        return settings.bossDist;
    }

    double BossFightLength(out int fights)
    {
        var f = settings.bossFights;
        fights = f != null ? f.Count : 0;
        if (fights == 0) return DefaultBossFight;
        var s = f.OrderBy(x => x).ToList();
        double med = s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2.0;
        return Math.Max(2, med);
    }

    double[] BossCloseCdf()
    {
        var hist = BossHist();
        double total = hist.Sum();
        if (total < 40) return null;
        var cdf = new double[BossBins];
        double acc = 0;
        for (int i = 0; i < BossBins; i++) { acc += hist[i]; cdf[i] = acc / total; }
        return cdf;
    }

    double BossRefillsPerFight()
    {
        var p = settings.bossPhases;
        return 1 + (p != null && p.Count > 0 ? p.Average() : 0);
    }

    double BossOtherDebuff(int type)
    {
        if (settings.bossDebuffSamples < 40) return 0;
        return (type == 0 ? settings.bossOtherBurn : type == 2 ? settings.bossOtherFrost : 0) / (double)settings.bossDebuffSamples;
    }

    double BossOtherDebuffObjects() => settings.bossDebuffSamples >= 40 ? settings.bossOtherDebuffs / settings.bossDebuffSamples : -1;

    double BossHitRate() => settings.bossSeconds >= 20f ? settings.bossHits / (double)settings.bossSeconds : -1;

    static double CloseShare(double[] cdf, double range)
    {
        if (cdf == null) return -1;
        double x = range / BossBin;
        int i = (int)Math.Floor(x);
        if (i >= BossBins - 1) return cdf[BossBins - 1];
        double lo = i > 0 ? cdf[i - 1] : 0, hi = cdf[i];
        return lo + (hi - lo) * (x - i);
    }
}
