using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mirror;
using UnityEngine;

public partial class SephiriaToolbox
{
    static void CollectIds(object o, HashSet<string> ids)
    {
        if (o == null) return;
        for (var t = o.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (f.Name.IndexOf("damageid", StringComparison.OrdinalIgnoreCase) < 0) continue;
                switch (f.GetValue(o))
                {
                    case string s when !string.IsNullOrEmpty(s): ids.Add(s); break;
                    case string[] arr: foreach (var s in arr) if (!string.IsNullOrEmpty(s)) ids.Add(s); break;
                }
            }
        }
    }

    sealed class WeaponIdInfo
    {
        public int Elem = 4;
        public string[] Keys = new string[0];
        public string RateKey;
        public bool Special;
        public string Note = "";
    }

    static readonly Dictionary<string, WeaponIdInfo> WeaponIdScaling = new(StringComparer.Ordinal)
    {
        ["Weapon_BurnRing"] = new() { Elem = 1, Keys = new[] { "WEAPONDAMAGEBONUS", "FINALWEAPONDAMAGE", "BURNDAMAGE" }, RateKey = "BURNSPEED", Note = Tr("src.ring_fire_scales_weapon") },
        ["Weapon_Spike"] = new() { Elem = 0, Keys = new[] { "WEAPONDAMAGEBONUS", "BASICATTACKDAMAGEBONUS", "FINALWEAPONDAMAGE" }, Note = Tr("src.thorns_scales_weapon_normal") },
        ["Weapon_Nastrond"] = new() { Elem = 2, Keys = new[] { "FROSTRELICDAMAGE" }, Note = Tr("src.frost_relic_scales_ice") },
        ["Weapon_SpecialAttack_Amethyst"] = new() { Special = true },
        ["KatanaMagicBlade"] = new() { Special = true },
    };

    static bool IsReflectDamage(string id) =>
        id.IndexOf("Thorns", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("Reflect", StringComparison.OrdinalIgnoreCase) >= 0;

    static string DamageIdName(string id)
    {
        try
        {
            var e = KeywordDatabase.GetDamageIdEntity(id);
            if (e != null)
            {
                var n = Clean(e.aName.ToString());
                if (!string.IsNullOrEmpty(n)) return n;
            }
        }
        catch { }
        return id;
    }

    static string TrimPrefix(string s, string prefix) => s.StartsWith(prefix, StringComparison.Ordinal) && s.Length > prefix.Length ? s.Substring(prefix.Length) : s;

    static float[] CooldownRates(Charm_Basic c)
    {
        if (c is not Charm_Active act) return null;
        try
        {
            var r = new float[Math.Max(1, c.maxLevel + 1)];
            bool byLevel = false;
            for (int l = 0; l < r.Length; l++)
            {
                float cd = act.GetCooldownTime(out bool changes, l, 0);
                byLevel |= changes;
                r[l] = 1f / Math.Max(0.1f, cd);
            }
            return byLevel && r.Any(v => Math.Abs(v - r[0]) > 1e-6f) ? r : null;
        }
        catch { return null; }
    }

    static int MainElement(IEnumerable<string> ids, Dictionary<string, Dictionary<EDamageElementalType, double>> measured)
    {
        var sum = new Dictionary<EDamageElementalType, double>();
        foreach (var id in ids)
            if (measured.TryGetValue(id, out var byElem))
                foreach (var kv in byElem) sum[kv.Key] = sum.GetValueOrDefault(kv.Key) + kv.Value;
        if (sum.Count == 0) return 4;
        return sum.OrderByDescending(kv => kv.Value).First().Key switch
        {
            EDamageElementalType.Physical => 0,
            EDamageElementalType.Fire => 1,
            EDamageElementalType.Ice => 2,
            EDamageElementalType.Lightning => 3,
            _ => 4
        };
    }

    static readonly HashSet<string> IdPrefixes = new(StringComparer.Ordinal) { "Ability", "Abiltiy", "Debuff", "Charm", "Skill", "Weapon", "Companion" };

    static IEnumerable<int> TokenKeys(IEnumerable<string> ids, Dictionary<string, int> index, HashSet<int> changeable, DpsModel d)
    {
        var generic = new HashSet<int> { d.kAll, d.kWdb, d.kBad, d.kSad, d.kDad, d.kFwd, d.kMdb };
        foreach (var k in d.kElem) generic.Add(k);
        var result = new HashSet<int>();
        foreach (var id in ids)
        {
            var parts = id.Split('_').Where(p => p.Length > 0).ToList();
            if (parts.Count > 1 && IdPrefixes.Contains(parts[0])) parts.RemoveAt(0);
            var words = Regex.Matches(string.Concat(parts), "[A-Z]+(?![a-z])|[A-Z]?[a-z0-9]+").Cast<Match>().Select(x => x.Value).ToList();
            for (int n = words.Count; n >= 1; n--)
            {
                string p = string.Concat(words.Take(n)).ToUpperInvariant();
                bool hit = false;
                foreach (var suffix in new[] { "DAMAGE", "DAMAGEBONUS" })
                    if (index.TryGetValue(p + suffix, out var k) && changeable.Contains(k) && !generic.Contains(k)) { result.Add(k); hit = true; }
                if (hit) break;
            }
        }
        return result;
    }

    static void CollectPrefabIds(object o, HashSet<string> ids)
    {
        for (var t = o.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                GameObject go = f.GetValue(o) switch
                {
                    GameObject g => g,
                    Component c => c != null ? c.gameObject : null,
                    _ => null
                };
                if (go == null) continue;
                foreach (var comp in go.GetComponentsInChildren<Component>(true)) CollectIds(comp, ids);
            }
        }
    }
}
