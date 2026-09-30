using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    enum SrcCat : byte { Weapon, Magic, Charm, Ability, Other }

    sealed class SourceInfo
    {
        public string Key, Name;
        public SrcCat Cat;
        public readonly List<int> Instances = new();
    }

    sealed class ItemRef
    {
        public int Instance, EntityId, Level, MaxLevel;
        public string Name;
    }

    sealed class PlayerSources
    {
        public readonly Dictionary<string, List<ItemRef>> ByNameKey = new(StringComparer.Ordinal);
        public readonly Dictionary<string, List<ItemRef>> ByDamageId = new(StringComparer.Ordinal);
        public readonly Dictionary<string, SourceInfo> Resolved = new(StringComparer.Ordinal);
    }

    readonly Dictionary<uint, PlayerSources> playerSources = new();
    readonly PlayerSources noInventory = new();
    static readonly Dictionary<int, string[]> charmDamageIds = new();

    static readonly Color[] CatColors =
    {
        new(0.95f, 0.6f, 0.3f, 0.35f),
        new(0.4f, 0.6f, 1f, 0.35f),
        new(0.45f, 0.85f, 0.5f, 0.35f),
        new(0.8f, 0.5f, 0.95f, 0.35f),
        new(0.7f, 0.7f, 0.7f, 0.3f)
    };

    static Color ElementColor(EDamageElementalType e) => e switch
    {
        EDamageElementalType.Fire => new Color(1f, 0.45f, 0.3f, 0.35f),
        EDamageElementalType.Ice => new Color(0.4f, 0.85f, 1f, 0.35f),
        EDamageElementalType.Lightning => new Color(1f, 0.9f, 0.3f, 0.35f),
        EDamageElementalType.Physical => new Color(0.85f, 0.85f, 0.85f, 0.3f),
        _ => new Color(0.8f, 0.5f, 0.95f, 0.35f)
    };

    static string[] CharmDamageIds(int entityId, Charm_Basic c)
    {
        if (charmDamageIds.TryGetValue(entityId, out var ids)) return ids;
        var set = new HashSet<string>();
        try
        {
            CollectIds(c, set);
            CollectPrefabIds(c, set);
            if (c is Charm_Magic cm && cm.ContainedMagic != null && cm.ContainedMagic.magicPrefab != null)
                foreach (var comp in cm.ContainedMagic.magicPrefab.GetComponentsInChildren<Component>(true)) CollectIds(comp, set);
            set.RemoveWhere(IsDebuffId);
        }
        catch { }
        return charmDamageIds[entityId] = set.ToArray();
    }

    static bool IsDebuffId(string id) => id != null && id.StartsWith("Debuff", StringComparison.Ordinal);

    static void AddRef(Dictionary<string, List<ItemRef>> map, string key, ItemRef r)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!map.TryGetValue(key, out var list)) map[key] = list = new List<ItemRef>();
        if (!list.Contains(r)) list.Add(r);
    }

    PlayerSources ReadPlayerSources(PlayerAvatar a)
    {
        var ps = new PlayerSources();
        var inv = a.Inventory;
        if (inv == null) return ps;
        foreach (var kv in inv.inventoryMatrix)
        {
            var v = kv.Value;
            var c = v?.Charm;
            if (c == null) continue;
            var r = new ItemRef { Instance = v.InstanceID, EntityId = v.EntityID, Level = c.DisplayedLevel, MaxLevel = c.maxLevel, Name = ItemName(v.EntityID) };
            AddRef(ps.ByNameKey, ItemDatabase.FindItemById(v.EntityID)?.aName?.key, r);
            if (c is Charm_Magic cm && cm.ContainedMagic != null) AddRef(ps.ByNameKey, cm.ContainedMagic.aName?.key, r);
            foreach (var id in CharmDamageIds(v.EntityID, c)) AddRef(ps.ByDamageId, id, r);
        }
        return ps;
    }

    void RefreshSources(List<PlayerSpawner> players)
    {
        foreach (var s in players)
        {
            var a = s != null ? s.PlayerAvatar : null;
            if (a == null) continue;
            try { playerSources[a.netId] = ReadPlayerSources(a); }
            catch (Exception e) { WarnOnce("伤害来源", e); }
        }
    }

    SourceInfo ResolveSource(PlayerSources ps, string id)
    {
        if (ps != null && ps.Resolved.TryGetValue(id, out var hit)) return hit;
        DamageIdEntity e = null;
        try { e = KeywordDatabase.GetDamageIdEntity(id); } catch { }
        string nameKey = e != null ? e.aName?.key : null;
        List<ItemRef> refs = null;
        if (ps != null && (string.IsNullOrEmpty(nameKey) || !ps.ByNameKey.TryGetValue(nameKey, out refs)))
            ps.ByDamageId.TryGetValue(id, out refs);

        SourceInfo info;
        if (refs != null && refs.Count > 0)
        {
            bool magic = (e != null && e.category == DamageIdEntity.ECategory.Magic) || (nameKey != null && nameKey.StartsWith("Skill_", StringComparison.Ordinal));
            string name = magic && e != null ? Clean(e.aName.ToString()) : refs[0].Name;
            if (string.IsNullOrEmpty(name)) name = refs[0].Name;
            info = new SourceInfo { Key = "i:" + refs[0].Instance, Name = name + LevelText(refs), Cat = magic ? SrcCat.Magic : SrcCat.Charm };
            info.Instances.AddRange(refs.Select(r => r.Instance));
        }
        else
        {
            string name = null;
            try { if (e != null) name = Clean(e.aName.ToString()); } catch { }
            if (string.IsNullOrEmpty(name)) name = id;
            var cat = e == null ? SrcCat.Other : e.category switch
            {
                DamageIdEntity.ECategory.Weapon => SrcCat.Weapon,
                DamageIdEntity.ECategory.Magic => SrcCat.Magic,
                DamageIdEntity.ECategory.Charm => SrcCat.Charm,
                DamageIdEntity.ECategory.Ability => SrcCat.Ability,
                _ => SrcCat.Other
            };
            info = new SourceInfo { Key = "d:" + name, Name = name, Cat = cat };
        }
        if (ps != null) ps.Resolved[id] = info;
        return info;
    }

    SourceInfo SourceFor(uint player, string id) =>
        ResolveSource(playerSources.TryGetValue(player, out var ps) ? ps : noInventory, id);
}
