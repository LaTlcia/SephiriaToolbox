using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorayAnalytics;
using Mirror;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static T Comp<T>(PlayerSpawner s) where T : Component
    {
        var c = s.GetComponent<T>();
        if (c == null && s.PlayerAvatar != null) c = s.PlayerAvatar.GetComponent<T>();
        return c;
    }

    void RefreshBuilds(List<PlayerSpawner> players)
    {
        RefreshSources(players);
        foreach (var s in players)
        {
            var a = s != null ? s.PlayerAvatar : null;
            if (a == null) continue;
            try { builds[a.netId] = CaptureBuild(s, a); }
            catch (Exception e) { WarnOnce("构筑快照", e); }
        }
    }

    PlayerRecord CaptureBuild(PlayerSpawner s, PlayerAvatar a)
    {
        var p = new PlayerRecord { name = names.TryGetValue(a.netId, out var n) ? n : "Player " + a.netId };
        var lc = Comp<LevelController>(s);
        if (lc != null) p.level = lc.currentLevel;
        var ld = Comp<PlayerLocalDataStorage>(s);
        if (ld != null)
        {
            p.costume = ld.defaultCostume;
            p.costumeName = CostumeName(ld.defaultCostume);
            p.deathCount = ld.deathCount;
        }
        var wc = Comp<WeaponControllerSimple>(s);
        if (wc != null && wc.currentWeapon != null)
            p.weapon = new WeaponRecord { id = wc.currentWeapon.entityId, name = WeaponName(wc.currentWeapon.entityId) };
        var mc = Comp<MiracleController>(s);
        if (mc != null)
            foreach (var m in mc.miracles)
                if (m != null) p.miracles.Add(new MiracleRecord { id = m.id, name = MiracleName(m) });

        var inv = a.Inventory;
        if (inv != null)
        {
            foreach (var kv in inv.inventoryMatrix)
            {
                var v = kv.Value;
                if (v == null) continue;
                var e = ItemDatabase.FindItemById(v.EntityID);
                p.inventory.Add(new ItemRecord
                {
                    x = kv.Key.x,
                    y = kv.Key.y,
                    id = v.EntityID,
                    quantity = v.Quantity,
                    level = v.Charm != null ? v.Charm.DisplayedLevel : 0,
                    maxLevel = v.Charm != null ? v.Charm.maxLevel : 0,
                    rotation = v.StoneTablet != null ? v.StoneTablet.rotation : 0,
                    name = ItemName(v.EntityID),
                    rarity = e != null ? (int)e.rarity : 0,
                    type = e != null ? e.type.ToString() : "",
                    typeName = e != null ? TypeName(e.type) : Tr("records.other")
                });
            }
            foreach (var kv in inv.currentSetEffectCount)
                if (kv.Value > 0) p.sets.Add(new SetRecord { id = kv.Key, name = CategoryName(kv.Key), count = kv.Value });
        }
        if (s.consumeFruitSkewerBonus != null)
            foreach (var f in s.consumeFruitSkewerBonus)
                p.fruitSkewer.Add(new FruitRecord { category = f.categoryName, name = FruitName(f.categoryName), weight = f.weight });
        try { p.talents = CaptureTalents(a); } catch (Exception e) { WarnOnce("天赋", e); }
        try { p.stats = CaptureStats(a); } catch (Exception e) { WarnOnce("属性快照", e); }
        return p;
    }

    List<TalentRecord> CaptureTalents(PlayerAvatar a)
    {
        var list = new List<TalentRecord>();
        if (a.passiveStats == null) return list;
        foreach (var kv in a.passiveStats)
        {
            if (kv.Value <= 0) continue;
            PassiveEntity e = null;
            try { e = PassiveDatabase.Find(kv.Key); } catch { }
            var t = new TalentRecord
            {
                id = (long)kv.Key,
                level = kv.Value,
                maxLevel = e != null ? e.maxLevel : 0,
                name = Cached("tl:" + kv.Key, () => e?.aName.ToString())
            };
            if (e != null)
            {
                var perks = new List<string>();
                void Perk(GameObject prefab, int need)
                {
                    if (kv.Value < need || prefab == null) return;
                    string text = PerkText(kv.Key, need, prefab);
                    if (!string.IsNullOrEmpty(text)) perks.Add(text);
                }
                Perk(e.lv5PerkPrefab, 5);
                Perk(e.lv10PerkPrefab, 10);
                Perk(e.lv20PerkPrefab, 20);
                if (perks.Count > 0) t.perks = perks;
            }
            list.Add(t);
        }
        return list.OrderByDescending(t => t.level).ThenBy(t => t.id).ToList();
    }

    string PerkText(ulong id, int need, GameObject prefab)
    {
        string key = "pk:" + id + ":" + need;
        if (nameCache.TryGetValue(key, out var v)) return v;
        var meta = prefab.GetComponent<PassiveObjectMetadata>();
        if (meta == null) return nameCache[key] = "";
        try { v = Clean(meta.GetEffectString()); } catch { v = null; }
        if (string.IsNullOrEmpty(v) || v == "UNKNOWN")
        {
            try { v = Clean(meta.effectString.ToString()); } catch { v = ""; }
        }
        return nameCache[key] = v ?? "";
    }

    static Dictionary<string, int> CaptureStats(PlayerAvatar a)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in a.customStats.Keys) keys.Add(k);
        foreach (var k in a.calculatedBonusStats.Keys) keys.Add(k);
        foreach (ECustomStat e in Enum.GetValues(typeof(ECustomStat))) keys.Add(e.ToString().ToUpperInvariant());
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var k in keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            int v;
            try { v = a.GetCustomStatUnsafe(k); } catch { continue; }
            if (v != 0) result[k] = v;
        }
        foreach (var kv in a.customStatsAmp.OrderBy(x => x.Key, StringComparer.Ordinal))
            if (kv.Value != 0) result["AMP_" + kv.Key] = kv.Value;
        return result;
    }

    List<HardModeRecord> hardModeCache;

    List<HardModeRecord> CaptureHardMode()
    {
        try
        {
            var dm = DungeonManager.Instance;
            if (dm == null || dm.hardModeShardLevels == null) return hardModeCache;
            var list = new List<HardModeRecord>();
            foreach (var kv in dm.hardModeShardLevels)
            {
                if (kv.Value <= 0) continue;
                HardModeShardEntity e = null;
                try { e = HardModeDatebase.Find(kv.Key); } catch { }
                list.Add(new HardModeRecord
                {
                    key = kv.Key,
                    level = kv.Value,
                    name = Cached("hm:" + kv.Key, () => e?.aName.ToString()),
                    value = dm.hardModeEnvironment != null && dm.hardModeEnvironment.TryGetValue(kv.Key, out var v) ? v : 0
                });
            }
            if (list.Count > 0 || hardModeCache == null) hardModeCache = list.OrderBy(h => h.key, StringComparer.Ordinal).ToList();
        }
        catch (Exception e) { WarnOnce("难度词条", e); }
        return hardModeCache;
    }

    string FloorBossAffix(string guid)
    {
        try
        {
            var dm = DungeonManager.Instance;
            if (dm == null || string.IsNullOrEmpty(guid) || !dm.generatedFloors.TryGetValue(guid, out var fd) || fd == null || string.IsNullOrEmpty(fd.bossHard)) return null;
            var ent = UnitDatabase.GetBossHardEntityByKey(fd.bossHard);
            string name = ent != null ? Cached("bh:" + fd.bossHard, () => ent.aName.ToString()) : fd.bossHard;
            if (ent != null && ent.effects != null && ent.effects.Length > 0) name = Tr("common.name_paren", name, string.Join(Tr("common.sep_list"), ent.effects));
            return name;
        }
        catch { return null; }
    }

    int? FloorDefense(string guid)
    {
        try
        {
            var dm = DungeonManager.Instance;
            if (dm == null || string.IsNullOrEmpty(guid) || !dm.generatedFloors.TryGetValue(guid, out var fd) || fd == null) return null;
            return StageDefense(dm, fd.stageName, out _);
        }
        catch { return null; }
    }

    RunResult CaptureResult(PlayerAvatar local, int playerCount)
    {
        var dm = DungeonManager.Instance;
        var r = new RunResult
        {
            endType = dm.victoryType,
            giveUp = dm.isGiveUpRun,
            raceId = dm.raceId,
            hardMode = dm.CalculateCurrentHardModePoints(),
            playTimeSeconds = Mathf.RoundToInt(dm.playedRealtimeClientside),
            gameVersion = Application.version,
            host = NetworkServer.active,
            playerCount = playerCount,
            stage = ""
        };
        if (dm.dungeonEnvironment.TryGetValue("ChapterNum", out var chapter)) r.chapter = chapter;
        if (local != null && !string.IsNullOrEmpty(local.currentFloorGuid) && dm.generatedFloors.TryGetValue(local.currentFloorGuid, out var fd))
        {
            r.stage = fd.stageName;
            r.progress = fd.nodeProgress;
        }
        r.stageName = StageName(r.stage);
        r.endName = EndName(r.endType, r.giveUp);
        return r;
    }

    static string EndName(int type, bool giveUp) => type switch
    {
        0 => giveUp ? Tr("records.abandoned") : Tr("records.defeated"),
        1 => Tr("records.cleared"),
        2 => Tr("records.story_ending"),
        3 => Tr("records.chapter_cleared"),
        4 => Tr("records.chapter_cleared_ii"),
        5 => Tr("records.chapter_5_cleared"),
        6 => Tr("records.chapter_5_escape"),
        _ => Tr("records.ending", type)
    };

    static JObject BuildOfficial(List<PlayerSpawner> players, RunResult r)
    {
        var d = new AnalyticsEvent_SessionData
        {
            id = Guid.NewGuid(),
            created_at = DateTime.UtcNow,
            end_type = (short)r.endType,
            is_giveup = r.giveUp,
            play_time = DungeonManager.Instance.playedRealtimeClientside,
            stage_name = r.stage,
            total_progress = r.progress,
            chapter_id = (short)r.raceId,
            total_players = (short)players.Count,
            game_version = HorayUtility.ParseVersionToInt(Application.version),
            hard_mode_level = (short)r.hardMode
        };
        var type = typeof(AnalyticsEvent_SessionData);
        for (int i = 0; i < players.Count && i < 4; i++)
        {
            var s = players[i];
            var a = s != null ? s.PlayerAvatar : null;
            if (a == null) continue;
            string prefix = $"player{i + 1}_";
            void Set(string field, object value) => type.GetProperty(prefix + field)?.SetValue(d, value);

            var lc = Comp<LevelController>(s);
            var wc = Comp<WeaponControllerSimple>(s);
            var mc = Comp<MiracleController>(s);
            var ld = Comp<PlayerLocalDataStorage>(s);
            if (lc != null) Set("level", (short?)(short)lc.currentLevel);
            Set("weapon_id", (int?)(wc != null && wc.currentWeapon != null ? wc.currentWeapon.entityId : -1));
            if (ld != null)
            {
                Set("costume", ld.defaultCostume);
                Set("death_count", (short?)ld.deathCount);
            }
            if (mc != null) Set("miracle", string.Join(",", mc.miracles.Where(m => m != null).Select(m => m.id)));

            var deals = a.dealsStatistics.Select(kv => new AnalyticsDealStats(kv.Key.Id, (int)kv.Key.ElementalType, kv.Value)).ToList();
            Set("deal_stats", deals.Count > 0 ? deals : null);

            var items = new List<AnalyticsItemData>();
            if (a.Inventory != null)
                foreach (var kv in a.Inventory.inventoryMatrix)
                {
                    var v = kv.Value;
                    if (v == null) continue;
                    items.Add(new AnalyticsItemData(kv.Key.x, kv.Key.y, v.EntityID, v.Quantity,
                        v.Charm != null ? v.Charm.DisplayedLevel : 0, v.StoneTablet != null ? v.StoneTablet.rotation : 0));
                }
            Set("inventory_snapshot", items.Count > 0 ? items : null);

            var fruits = s.consumeFruitSkewerBonus?.Select(f => new AnalyticsFruitData(f.categoryName, f.weight)).ToList();
            Set("fruit_skewer", fruits != null && fruits.Count > 0 ? fruits : null);
        }
        return JObject.FromObject(new AnalyticsEvent_SessionData_ForWebRequest(d));
    }
}
