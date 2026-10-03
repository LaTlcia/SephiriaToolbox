using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static bool SummonCadence(GameObject unit, out double wait, out double fixedSeconds)
    {
        wait = fixedSeconds = 0;
        if (unit == null) return false;
        double attack = AttackAnimSeconds(unit, 0.8);
        switch (unit.GetComponent<UnitAI_NewBasic>())
        {
            case UnitAI_Soldier s when s.tryAttackTimer != null && s.tryAttackTimer.time > 0:
                wait = s.tryAttackTimer.time * 0.875;
                fixedSeconds = attack + 0.325;
                return true;
            case UnitAI_Grenadier g when g.tryAttackTimer != null && g.tryAttackTimer.time > 0:
                wait = g.tryAttackTimer.time * 0.85;
                fixedSeconds = attack;
                return true;
            case UnitAI_MiniBallista b:
                wait = FieldValue(b, "attackIntervalTimer") is Timer t && t.time > 0 ? t.time : 2;
                fixedSeconds = attack;
                return true;
            case UnitAI_HandBomber h when h.tryAttackTimer != null && h.tryAttackTimer.time > 0:
                wait = h.tryAttackTimer.time * 0.85;
                fixedSeconds = 0;
                return true;
            case UnitAI_MoleChieftain_Ally:
                wait = 0.01;
                fixedSeconds = 1 / PlaySummon - wait;
                return true;
        }
        return false;
    }

    static double AttackAnimSeconds(GameObject unit, double fallback)
    {
        foreach (var an in unit.GetComponentsInChildren<Animator2D_Basic>(true))
        {
            var set = an != null ? an.currentSet : null;
            if (set == null || set.sprites == null) continue;
            foreach (var st in set.sprites)
            {
                if (st == null || st.fps <= 0 || !string.Equals(st.state, "ATTACK", StringComparison.OrdinalIgnoreCase)) continue;
                int last = 0;
                if (st.timeline != null) foreach (var k in st.timeline) if (k != null) last = Math.Max(last, k.frameIdx + 1);
                if (st.frameEvents != null) foreach (var fe in st.frameEvents) if (fe != null) last = Math.Max(last, fe.frame);
                if (last > 0) return last / (double)st.fps;
            }
        }
        return fallback;
    }

    static void ApplySummonCadence(DpsSource s, GameObject unit)
    {
        if (!SummonCadence(unit, out double wait, out double fixedSeconds)) return;
        s.TheoryK *= 1 / (wait + fixedSeconds) / PlaySummon;
        s.SummonWait = wait;
        s.SummonFixed = fixedSeconds;
    }

    static void RegisterCompanion()
    {
        PassiveDefs["Charm_FollowerAttackSpeed"] = P(new PassiveDef { Kind = PKind.Stat, Key = FollowerAsKey, By = "attackSpeedPercentByLevel", CurFunc = (c, a) => 0 });

        CharmMechs["Charm_LeadNPC"] = new() { Trig = Trig.Summon, BonusBy = "levelBonusByLevel", Note = Tr("trigger.companion_attacks") };
        Extra<Charm_LeadNPC>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            AddRevive(s, c, key);
        });

        CharmMechs["Charm_MiniBallista"] = new() { Trig = Trig.Summon, CountBy = "bulletCountByLevel", Note = Tr("trigger.ballista_attacks") };
        Extra<Charm_MiniBallista>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            ApplySummonCadence(s, ((Charm_MiniBallista)c).unitPrefab);
            double own = s.SummonWait > 0 ? 1 / (s.SummonWait + s.SummonFixed) : PlaySummon;
            AddX(s, new XMod { Kind = XKind.RateAddIf, Key = key("ENHANCEDMINIBALLISTA"), A = b.Guard / Math.Max(0.1, own) });
            AddX(s, new XMod { Kind = XKind.DmgIf, Key = key("ENHANCEDMINIBALLISTA"), A = 1.3 });
            AddRevive(s, c, key);
        });

        CharmMechs["Charm_SummonUnit"] = new() { Trig = Trig.Summon, Note = Tr("trigger.summon_attacks") };
        Extra<Charm_SummonUnit>((s, c, b, key, levels, baseRate, swingHits) =>
        {
            var su = (Charm_SummonUnit)c;
            ApplySummonCadence(s, su.unitPrefab);
            if (Num(su, "isJellyfish") > 0)
            {
                AddX(s, new XMod { Kind = XKind.DmgPct, Key = key("JELLYFISHBASICDAMAGE") });
                AddX(s, new XMod { Kind = XKind.DmgIf, Key = key("JELLYFISHDOUBLEATTACK"), A = 2 });
            }
            AddRevive(s, c, key);
        });
    }
}
