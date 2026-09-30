using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HorayAnalytics;
using Mirror;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

public partial class SephiriaToolbox
{
    string FloorTitle(string guid)
    {
        var dm = DungeonManager.Instance;
        if (dm && !string.IsNullOrEmpty(guid) && dm.generatedFloors.TryGetValue(guid, out var fd))
            return Tr("names.progress", StageName(fd.stageName), fd.nodeProgress);
        return Tr("names.unknown_area");
    }

    static string StageName(string stageName)
    {
        try
        {
            var race = RaceDatabase.FindById(DungeonManager.Instance.raceId);
            if (race)
            {
                if (race.lobbyStage && race.lobbyStage.name == stageName) return Clean(race.lobbyStage.aName.ToString());
                foreach (var s in race.stages)
                    if (s && s.name == stageName) return Clean(s.aName.ToString());
            }
        }
        catch { }
        return stageName;
    }

    string SourceName(DamageKey key)
    {
        if (sourceNameCache.TryGetValue(key, out var cached)) return cached;
        string n = key.Id;
        try
        {
            var e = KeywordDatabase.GetDamageIdEntity(key.Id);
            if (e) n = Clean(e.aName.ToString());
        }
        catch { }
        string el = key.ElementalType switch
        {
            EDamageElementalType.Physical => Tr("names.physical"),
            EDamageElementalType.Fire => Tr("names.fire"),
            EDamageElementalType.Ice => Tr("names.ice"),
            EDamageElementalType.Lightning => Tr("names.lightning"),
            EDamageElementalType.Chaos => Tr("names.chaos"),
            EDamageElementalType.IceAndLightning => Tr("names.ice_lightning"),
            EDamageElementalType.FireAndIce => Tr("names.fire_ice"),
            EDamageElementalType.FireAndLightning => Tr("names.fire_lightning"),
            _ => null
        };
        return sourceNameCache[key] = el == null ? n : $"{n} [{el}]";
    }

    static string Clean(string s)
    {
        try { s = KeywordDatabase.Convert(s, useColor: false, useSprite: false); } catch { }
        return Regex.Replace(s ?? "", "<.*?>", "").Trim();
    }

    static string Fmt(float seconds)
    {
        int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }

    readonly Dictionary<string, string> nameCache = new();

    string Cached(string key, Func<string> get)
    {
        if (nameCache.TryGetValue(key, out var v)) return v;
        try { v = Clean(get()); } catch { v = null; }
        if (string.IsNullOrEmpty(v)) v = key.Substring(key.IndexOf(':') + 1);
        return nameCache[key] = v;
    }

    string WeaponName(int id) => Cached("w:" + id, () => WeaponDatabase.FindWeaponById(id)?.aName.ToString());
    string ItemName(int id) => Cached("i:" + id, () => ItemDatabase.FindItemById(id)?.Name);
    string CostumeName(string id) => Cached("c:" + id, () => CostumeDatabase.FindCostumeByID(id)?.aName.ToString());
    string MiracleName(Miracle m) => Cached("m:" + m.id, () => m.Name);
    string CategoryName(string id) => Cached("s:" + id, () => ItemDatabase.FindItemCategory(id)?.categoryName.ToString());
    string FruitName(string id) => Cached("f:" + id, () => ItemDatabase.FindItemCategory(id)?.categoryFruitName.ToString());
    string TypeName(EItemType t) => Cached("t:" + t, () => ItemDatabase.GetItemTypeName(t).ToString());

    static string Signed(double ratio) => (ratio >= 0 ? "+" : "") + ratio.ToString("P1");

    static string LevelText(List<ItemRef> refs)
    {
        if (refs.All(r => r.MaxLevel <= 0)) return "";
        return " " + string.Join("/", refs.Select(r => r.Level).OrderByDescending(l => l).Select(l => l >= 0 ? "+" + l : l.ToString()));
    }
}
