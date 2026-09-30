using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public partial class SephiriaToolbox
{
    uint battleOwner;
    bool battleWas;
    int battleCount;
    float battleSeconds;

    void TrackBattles()
    {
        var a = LocalAvatar();
        if (a == null) return;
        if (battleOwner != a.netId) { battleOwner = a.netId; ResetBattles(); }
        bool now = a.IsInBattle;
        if (now && !battleWas) battleCount++;
        if (now && battleWas) battleSeconds += Time.deltaTime;
        battleWas = now;
    }

    void ResetBattles()
    {
        battleWas = false;
        battleCount = 0;
        battleSeconds = 0;
    }

    double BattleLength() => battleCount >= 3 && battleSeconds >= 30 ? Math.Min(240, Math.Max(10, battleSeconds / battleCount)) : DefaultBattle;
}
