using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public partial class SephiriaToolbox
{
    uint enemyOwner;
    float enemyTimer;
    double enemySum, enemyDefSum;
    int enemySamples, enemyDefCount;
    readonly Queue<(float t, double sum, int n)> enemyDefRecent = new();
    double enemyDefRecentSum;
    int enemyDefRecentCount;
    const float EnemyRadius = 4f;

    void TrackEnemies()
    {
        var a = LocalAvatar();
        if (a == null) return;
        if (enemyOwner != a.netId) { enemyOwner = a.netId; ResetEnemies(); }
        if (!a.IsInBattle || a.IsDead) return;
        enemyTimer -= Time.deltaTime;
        if (enemyTimer > 0) return;
        enemyTimer = 0.5f;
        var cm = CombatManager.Instance;
        if (cm == null || cm.AllCreatures == null) return;
        var pos = a.transform.position;
        int n = 0;
        double defTick = 0;
        foreach (var u in cm.AllCreatures)
        {
            if (u == null || u == a || u.IsDead || !u.gameObject.activeSelf || u.canBeTarget.IsFalse()) continue;
            if (!CombatManager.ContainsAttackableFaction(u.GetHostileFactionLayers(EDamageFromType.None), a.faction)) continue;
            if ((u.transform.position - pos).sqrMagnitude > EnemyRadius * EnemyRadius) continue;
            n++;
            defTick += u.GetCustomStat(ECustomStat.DamageReduction);
        }
        if (n == 0) return;
        enemyDefSum += defTick;
        enemyDefCount += n;
        enemyDefRecent.Enqueue((runCombatTime, defTick, n));
        enemyDefRecentSum += defTick;
        enemyDefRecentCount += n;
        while (enemyDefRecent.Count > 0 && enemyDefRecent.Peek().t < runCombatTime - SegmentWindow)
        {
            var old = enemyDefRecent.Dequeue();
            enemyDefRecentSum -= old.sum;
            enemyDefRecentCount -= old.n;
        }
        enemySum += n;
        enemySamples++;
        SampleDebuffs(a);
    }

    void ResetEnemies()
    {
        enemyTimer = 0;
        enemySum = 0;
        enemySamples = 0;
        enemyDefSum = 0;
        enemyDefCount = 0;
        enemyDefRecent.Clear();
        enemyDefRecentSum = 0;
        enemyDefRecentCount = 0;
        ResetDebuffSamples();
    }

    bool EnemyDefenseMeasured(out double defense, bool recent = false)
    {
        defense = 0;
        double sum = recent ? enemyDefRecentSum : enemyDefSum;
        int n = recent ? enemyDefRecentCount : enemyDefCount;
        if (n < 20) return false;
        defense = sum / n;
        return true;
    }

    bool EnemiesMeasured(out double enemies)
    {
        enemies = PlayEnemies;
        if (enemySamples < 20) return false;
        enemies = Math.Min(6, Math.Max(1, enemySum / enemySamples));
        return true;
    }
}
