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
    public class SourceRecord
    {
        public string name;
        public int damage;
        public int? elem, cat;
    }

    public class DamageRecord
    {
        public string name;
        public int damage, dps;
        public List<SourceRecord> sources;
        public List<SourceRecord> bySource;
    }

    public class PlayerRecord : DamageRecord
    {
        public int level;
        public string costume, costumeName;
        public int deathCount;
        public WeaponRecord weapon;
        public List<MiracleRecord> miracles = new();
        public List<SetRecord> sets = new();
        public List<FruitRecord> fruitSkewer = new();
        public List<ItemRecord> inventory = new();
        public List<TalentRecord> talents;
        public Dictionary<string, int> stats;
        public PlayerRecord Clone() => (PlayerRecord)MemberwiseClone();
    }

    public class TalentRecord
    {
        public long id;
        public string name;
        public int level, maxLevel;
        public List<string> perks;
    }

    public class WeaponRecord { public int id; public string name; }
    public class MiracleRecord { public string id, name; }
    public class SetRecord { public string id, name; public int count; }
    public class FruitRecord { public string category, name; public int weight; }

    public class ItemRecord
    {
        public int x, y, id, quantity, level, maxLevel, rotation, rarity;
        public string name, type, typeName;
    }

    public class HardModeRecord
    {
        public string key, name;
        public int level, value;
    }

    public class FloorRecord
    {
        public int index;
        public string title;
        public string bossAffix;
        public int? defense;
        public int durationSeconds, combatSeconds;
        public List<DamageRecord> players;
    }

    public class RunResult
    {
        public int endType;
        public string endName;
        public bool giveUp;
        public string stage, stageName;
        public int progress = -1;
        public int raceId, chapter, hardMode, playerCount, playTimeSeconds;
        public string gameVersion;
        public bool host;
    }

    public class RunRecord
    {
        public int version = 2;
        public string startedAt, endedAt;
        public int durationSeconds, combatSeconds;
        public RunResult result;
        public List<HardModeRecord> hardMode;
        public List<PlayerRecord> players;
        public List<DamageRecord> total;
        public List<FloorRecord> floors;
        public JObject official;
    }

    static string LogDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Saved Games", "Sephiria_DpsLogs");

    static readonly JsonSerializerSettings JsonSettings = new() { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented };

    string currentRunFile;
}
