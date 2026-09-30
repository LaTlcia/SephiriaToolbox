using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    enum PKind : byte { Stat, Amp, PerStat, Mul, Crit, CritDmg, PerDebuffStack }

    sealed class PassiveDef
    {
        public PKind Kind;
        public string Key;
        public string KeyField;
        public string By;
        public float Const = 1;
        public string ConstField;
        public float Scale = 1;
        public float Uptime = 1;
        public Func<Charm_Basic, PlayerAvatar, double> Factor;
        public Func<Charm_Basic, PlayerAvatar, float[], float[]> Transform;
        public string Cur;
        public Func<Charm_Basic, PlayerAvatar, double> CurFunc;
        public string PerKey, DivBy;
        public float Div = 1;
        public int Elem = -1;
        public string ElemField;
        public bool Direct;
        public int DebuffUp = -1;
        public bool PerDebuffCount;
    }

    const float UpBurn = 0.5f, UpFrostbite = 0.5f, UpFullHp = 0.7f, UpNoHit = 0.4f, UpReload = 0.8f, UpGuardSmite = 0.05f,
                UpFirstHit = 0.1f, UpShield = 0.5f, NormalEnemyShare = 0.7f;

    static bool buildSingle;
    static double NormalShare() => buildSingle ? 0 : NormalEnemyShare;

    static double CloseUptime(PlayerAvatar a, double range = 2.5, bool wide = false)
    {
        if (buildCloseCdf != null && range > 0) return Math.Max(0.02, CloseShare(buildCloseCdf, wide ? range * 2 : range));
        var w = buildWeapon;
        bool ranged = w != null && w.weaponType is EWeaponType.Crossbow or EWeaponType.StaffMagic or EWeaponType.Golem;
        return wide ? (ranged ? 0.5 : 0.9) : (ranged ? 0.25 : 0.6);
    }

    static double[] buildCloseCdf;
    static double buildHitRate = -1;
    static double buildWeaponHits = 2;
    static double buildGuardHold = 0.1;
    static double buildDashAttack = 0.3;
    static WeaponSimple buildWeapon;

    static double NoHitShare(double returnSeconds) =>
        buildHitRate >= 0 && returnSeconds > 0 ? 1 / (1 + returnSeconds * buildHitRate) : UpNoHit;

    static PassiveDef[] P(params PassiveDef[] defs) => defs;

    const string FollowerAsKey = "__FOLLOWERATTACKSPEED";

    static int ReadPassives(Charm_Basic c, PlayerAvatar avatar, int item, Func<string, int> key, List<StatAdd> adds, List<Mod> mods,
                            Dictionary<int, double> curRaw, Dictionary<int, double> curAmp, Action<string, Exception> warn)
    {
        PassiveDef[] defs = null;
        for (var t = c.GetType(); t != null && t != typeof(Charm_Basic) && defs == null; t = t.BaseType) PassiveDefs.TryGetValue(t.Name, out defs);
        if (defs == null) return 0;
        int levels = Math.Max(1, c.maxLevel + 1), cur = c.CurrentLevelToIdx(), found = 0;
        bool on = c.IsEffectEnabled;
        foreach (var p in defs)
        {
            try
            {
                float constValue = p.ConstField != null ? (float)Num(c, p.ConstField) : p.Const;
                var table = p.By != null ? LevelTable(c, p.By) : null;
                if (p.By != null && table == null) continue;
                var v = new float[levels];
                for (int l = 0; l < levels; l++) v[l] = table != null ? SafeAt(table, l) : constValue;
                if (p.Transform != null) v = p.Transform(c, avatar, v);
                double factor = (p.Factor != null ? p.Factor(c, avatar) : 1) * p.Uptime * p.Scale;
                for (int l = 0; l < levels; l++) v[l] = (float)(v[l] * factor);

                switch (p.Kind)
                {
                    case PKind.Stat:
                    case PKind.Amp:
                    {
                        string keyName = p.KeyField != null ? (FieldValue(c, p.KeyField) as string)?.ToUpperInvariant() : p.Key;
                        if (string.IsNullOrEmpty(keyName)) continue;
                        int k = key(keyName);
                        adds.Add(new StatAdd { Key = k, Mode = (byte)(p.Kind == PKind.Amp ? 1 : 0), Values = v.Select(x => (int)Math.Round(x)).ToArray() });
                        if (on)
                        {
                            double now = p.CurFunc != null ? p.CurFunc(c, avatar) * p.Scale : p.Cur != null ? Num(c, p.Cur) : SafeAt(v, cur);
                            var target = p.Kind == PKind.Amp ? curAmp : curRaw;
                            target[k] = target.GetValueOrDefault(k) + now;
                        }
                        break;
                    }
                    case PKind.PerDebuffStack:
                    {
                        int k = key(p.Key);
                        adds.Add(new StatAdd { Key = k, Mode = 5, Values = v.Select(x => (int)Math.Round(x)).ToArray() });
                        if (on) curRaw[k] = curRaw.GetValueOrDefault(k) + (p.Cur != null ? Num(c, p.Cur) : 0);
                        break;
                    }
                    case PKind.PerStat:
                    {
                        int k = key(p.Key);
                        var div = p.DivBy != null ? LevelTable(c, p.DivBy) : null;
                        adds.Add(new StatAdd
                        {
                            Key = k, Mode = 3, Values = v.Select(x => (int)Math.Round(x)).ToArray(), Src = key(p.PerKey),
                            Div = Enumerable.Range(0, levels).Select(l => Math.Max(0.001f, div != null ? SafeAt(div, l) : p.Div)).ToArray()
                        });
                        if (on) curRaw[k] = curRaw.GetValueOrDefault(k) + (p.CurFunc != null ? p.CurFunc(c, avatar) : p.Cur != null ? Num(c, p.Cur) : 0);
                        break;
                    }
                    default:
                    {
                        int elem = p.Elem;
                        if (p.ElemField != null && FieldValue(c, p.ElemField) is EDamageElementalType et) elem = ElemIndex(et);
                        mods.Add(new Mod
                        {
                            Item = item, Kind = (byte)(p.Kind == PKind.Mul ? 0 : p.Kind == PKind.Crit ? 1 : 2),
                            Elem = elem, Direct = p.Direct, Values = v, DebuffUp = p.DebuffUp, PerDebuffCount = p.PerDebuffCount,
                            WideMul = c is Charm_TooCloseDamage ? CloseUptime(avatar, Num(c, "range"), wide: true) / Math.Max(0.01, CloseUptime(avatar, Num(c, "range"))) : 1
                        });
                        break;
                    }
                }
                found++;
            }
            catch (Exception e) { warn("被动 " + c.GetType().Name, e); }
        }
        return found;
    }
}
