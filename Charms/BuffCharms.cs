using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed class BuffCharm
    {
        public string Field = "buffPrefab";
        public string AmpBy;
        public string AmpField;
        public string StackBy;
        public Func<Charm_Basic, PlayerAvatar, Behavior, double> Rate;
        public byte Trigger = 255;
    }

    sealed class DynBuff
    {
        public int Item, Key;
        public byte Mode;
        public float[] PerStack;
        public float[] MaxStack;
        public double Dur;
        public byte Trigger;
    }

    static double GuardRate(Charm_Basic c, Behavior b) =>
        b.DefendMeasured ? (Num(c, "onOnlyPerfectGuard") > 0 ? b.PerfectGuard : b.Guard)
                         : (b.Weapon == EWeaponType.SwordAndShield ? 1.0 : 0.2) * (Num(c, "onOnlyPerfectGuard") > 0 ? PlayParry * 0.5 : PlayGuard);

    static List<(string Key, byte Mode, double Value)> BuffStats(CharacterBuff b)
    {
        var list = new List<(string, byte, double)>();
        void Add(string id, double v)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (MapStatus(id, out var key, out var mode)) { if (mode != 2) list.Add((key, mode, v)); }
            else list.Add((id.ToUpperInvariant(), 0, v));
        }
        switch (b)
        {
            case CharacterBuff_StatusInstance si when si.add != null:
                foreach (var st in si.add) if (st != null) Add(st.id, st.value);
                break;
            case CharacterBuff_StatusUnsafe su when su.add != null:
                foreach (var st in su.add) if (st != null) list.Add((st.id.ToUpperInvariant(), 0, st.value));
                break;
            case CharacterBuff_CustomStatusInstance cs when cs.add != null:
                foreach (var st in cs.add) if (st != null) Add(st.id, st.value);
                break;
            case CharacterBuff_Critical cr: list.Add(("CRITICAL", 0, cr.critical)); break;
            case CharacterBuff_PhysicalDamage pd: list.Add(("PHYSICALDAMAGE", 0, pd.basicAttackDamage)); break;
            case CharacterBuff_AP ap: list.Add(("AP", 0, ap.abilityPower)); break;
            case CharacterBuff_IncreaseCriticalChance_FromMpLossCharm: list.Add(("CRITICAL", 0, 100)); break;
            case CharacterBuff_AllAttackDamage_FromHpLossCharm: list.Add(("ALLDAMAGEBONUS", 0, 1)); break;
        }
        return list;
    }

    static double BuffAvgStacks(double rate, double dur, int max)
    {
        if (rate <= 0 || dur <= 0) return 0;
        double x = Math.Min(rate * dur, 30);
        double up = 1 - Math.Exp(-x);
        if (max <= 1) return up;
        double run = Math.Exp(x);
        double avg = run <= max ? (run + 1) / 2 : max - max * (max - 1) / (2 * run);
        return up * Math.Max(1, avg);
    }

    static readonly FieldInfo BuffsField = typeof(UnitAvatar).GetField("buffs", BindingFlags.Instance | BindingFlags.NonPublic);

    static int ReadBuffPassives(Charm_Basic c, PlayerAvatar avatar, Behavior play, Func<string, int> key, List<StatAdd> adds,
                                Dictionary<int, double> curRaw, Dictionary<int, double> curAmp, List<DynBuff> dyn, int item)
    {
        BuffCharm def = null;
        for (var t = c.GetType(); t != null && t != typeof(Charm_Basic) && def == null; t = t.BaseType) BuffCharms.TryGetValue(t.Name, out def);
        if (def == null || FieldValue(c, def.Field) is not CharacterBuff prefab) return 0;
        var stats = BuffStats(prefab);
        if (stats.Count == 0) return 0;
        int levels = Math.Max(1, c.maxLevel + 1);
        var amp = def.AmpBy != null ? LevelTable(c, def.AmpBy) : null;
        float ampConst = def.AmpField != null ? (float)Num(c, def.AmpField) : 1f;
        var stacks = def.StackBy != null ? LevelTable(c, def.StackBy) : null;
        double dur = prefab.defaultDuration;
        if (!prefab.ignoreDurationBonus) dur *= Pct(avatar.GetCustomStat(ECustomStat.BuffDuration));
        double rate = def.Rate(c, avatar, play);
        var avg = Per(levels, l => BuffAvgStacks(rate, dur, stacks != null ? (int)SafeAt(stacks, l) : prefab.MaxStackCount));
        CharacterBuff active = null;
        if (c.IsEffectEnabled && BuffsField?.GetValue(avatar) is Dictionary<string, CharacterBuff> buffs && !string.IsNullOrEmpty(prefab.ID))
            buffs.TryGetValue(prefab.ID, out active);
        foreach (var (k, mode, v) in stats)
        {
            int ki = key(k);
            if (def.Trigger != 255 && dyn != null)
            {
                dyn.Add(new DynBuff
                {
                    Item = item, Key = ki, Mode = mode, Dur = dur, Trigger = def.Trigger,
                    PerStack = Per(levels, l => v * (amp != null ? SafeAt(amp, l) : ampConst)),
                    MaxStack = Per(levels, l => stacks != null ? SafeAt(stacks, l) : prefab.MaxStackCount)
                });
            }
            else
                adds.Add(new StatAdd { Key = ki, Mode = mode, Values = Enumerable.Range(0, levels).Select(l => (int)Math.Round(v * (amp != null ? SafeAt(amp, l) : ampConst) * avg[l])).ToArray() });
            if (active != null)
            {
                double now = v * active.Amplified * active.CurrentStack;
                var target = mode == 1 ? curAmp : curRaw;
                target[ki] = target.GetValueOrDefault(ki) + now;
            }
        }
        return 1;
    }

    static int ReadAddStatByAnotherStat(Charm_Basic c, Func<string, int> key, List<StatAdd> adds, Dictionary<int, double> curRaw)
    {
        if (c is not Charm_AddStatByAnotherStat ab || ab.targetStats == null) return 0;
        string src = (!string.IsNullOrEmpty(ab.baseStatNameUnsafe) ? ab.baseStatNameUnsafe : ab.baseStatID ?? "").ToUpperInvariant();
        if (src.Length == 0) return 0;
        int levels = Math.Max(1, c.maxLevel + 1), found = 0;
        var added = FieldValue(c, "addedValues") as int[];
        var div = Per(levels, l => Math.Max(1, SafeAt(ab.perBaseStatByLevel, l)));
        for (int i = 0; i < ab.targetStats.Length; i++)
        {
            var ts = ab.targetStats[i];
            if (ts.addByLevel == null || !MapStatus(ts.statID, out var k, out var mode) || mode != 0) continue;
            int ki = key(k);
            adds.Add(new StatAdd { Key = ki, Mode = 3, Values = Enumerable.Range(0, levels).Select(l => SafeAt(ts.addByLevel, l)).ToArray(), Src = key(src), Div = div });
            if (c.IsEffectEnabled && added != null && i < added.Length) curRaw[ki] = curRaw.GetValueOrDefault(ki) + added[i];
            found++;
        }
        return found > 0 ? 1 : 0;
    }
}
