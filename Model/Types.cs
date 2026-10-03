using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mirror;
using UnityEngine;

public partial class SephiriaToolbox
{
    public enum ArrangeGoal { Levels, Total, Weapon, Magic, Custom }
    static readonly string[] GoalNames = { Tr("arrange.most_levels"), Tr("arrange.overall_damage"), Tr("arrange.weapon_first"), Tr("arrange.magic_first"), Tr("arrange.custom") };

    enum SrcKind { WeaponBasic, WeaponSpecial, WeaponDash, Magic, Proc, Rider, Ability }

    sealed class DpsSource
    {
        public SrcKind Kind;
        public string Name;
        public int Item = -1;
        public EDamageElementalType Element;
        public string Formula = "";
        public bool UseAttackSpeed = true;
        public double Prior = 1;
        public float[] Default, Percent;
        public string RelKey;
        public float[] Table;
        public double Cooldown = 10;
        public int Ammo = 1;
        public bool PowerOnStat;
        public float[] CritAdd;
        public float HitInterval;
        public double DirectCap;
        public bool Column;
        public readonly HashSet<string> Ids = new();
        public double Measured, K, Weight;
        public bool Estimated;
        public float[] Rate;
        public int ElemIdx = -1;
        public int StatKey = -1;
        public int[] MulKeys;
        public int AddKey = -1;
        public double AddBase;
        public bool NoCrit;
        public bool MagicCrit;
        public bool WeaponCrit;
        public Slope[] Slopes;
        public double TheoryK;
        public bool HasTheory;
        public bool SwingBased;
        public RateStat StatRate;
        public float StatCap;
        public double CdSeconds, CdBase, CdBaseM;
        public double CdChance = 1;
        public int HasteKey = -1, AmpKey = -1;
        public float ChanceBase;
        public float[] ChanceTable;
        public string Note;
        public byte Basis;
        public double TheoryShare;
        public int DmgElem = -1;
        public bool Direct;
        public bool Summon;
        public double SummonWait, SummonFixed;
        public int GateCat = -1, GateCount;
        public double FinalShare;
        public bool AsDash;
        public bool PerCrit;
        public bool SwingScaled, RideBasicAsDash;
        public double RideBasic, RideSpecial, RideDash;
        public double EclipseAdd;
        public bool BurnRing;
        public int RateKeyA = -1, RateKeyB = -1;
        public EcoKind Eco;
        public bool Flat;
        public bool Preset;
        public int[] GateItems;
        public List<Feeder> Boost;
        public List<XMod> X;
        public int HasteKey2 = -1;
        public int MpMulKey = -1, MpFlatKey = -1;
        public float[] MagicCost;
        public float[] CritDmgAmp;
        public float[] MagicCostBase;
        public int MagicCostAdd;
        public bool SpecialAs;
        public double MultiScale = 1;
        public float[] RateM;
        public bool FirstFree;
        public bool Relic;
        public double PerUse = 1;
        public int Debuff = -1;
        public byte DebuffPart;
        public bool IsWeapon => Kind <= SrcKind.WeaponDash || Kind == SrcKind.Rider;
    }

    sealed class Slope
    {
        public int Key, Elem = -1;
        public float[] PerLevel;
        public double S0;
        public bool Step;
        public bool BaseOn;
    }

    sealed class DpsWindow
    {
        public double Seconds, Other;
        public double[] Measured;
        public int[] Perm, Rot;
    }

    struct StatAdd { public int Key; public byte Mode; public int[] Values; public int Src; public float[] Div; }

    sealed class Mod
    {
        public double WideMul = 1;
        public int DebuffUp = -1;
        public bool PerDebuffCount;
        public int Item;
        public byte Kind;
        public int Elem = -1;
        public bool Direct;
        public bool OwnOnly;
        public float[] Values;
    }

    sealed class ItemExtra
    {
        public bool Magic, BoltMagic, Attackable, Planet, Companion;
        public int MagicElem;
        public int[] Cats;
        public int CatWeight = 1;
    }

    enum SpecialKind : byte { NearLevel, Hourglass, Booster, Telescope, AutoMagic, ByRow, FireIce, WhitePaper, Badge, QuickRow, ArrBonus, CostLeft }

    sealed class Special
    {
        public SpecialKind Kind;
        public int Item;
        public float[] F;
        public int[] A, B;
        public int Dx, Dy;
        public bool Cond;
        public int MaxRarity;
        public int[] RowCats, RowKeys;
        public int KeyL, KeyR;
        public int Match = 2;
        public int[] PatIds, PatDx, PatDy;
        public byte[] PatFlag;
    }

    struct ComboTier { public int Count; public StatAdd[] Adds; }

    sealed class DpsModel
    {
        public string[] Keys;
        public double[] BaseRaw, BaseAmp;
        public double BaseHighest;
        public StatAdd[][] Adds;
        public DpsSource[] Sources;
        public double Other, OtherWeight;
        public double Measured;
        public int kAll, kWdb, kBad, kSad, kDad, kFwd, kMdb, kAs, kCrit, kCritDmg, kWCrit, kWCritDmg, kWCritAmp, kMCrit, kMCritDmg, kCdr, kExec;
        public readonly int[] kElem = new int[4];
        public readonly int[,] kConv = new int[4, 4];
        public ItemExtra[] Extra;
        public Special[] Specials;
        public Mod[] Mods = new Mod[0];
        public string[] CatNames;
        public ComboTier[][] Tiers;
        public int[] CatActual, CatStatic;
        public double ComboBonus;
        public int kComboBonus = -1;
        public bool Calibrated;
        public List<DpsWindow> Windows;
        public double WindowSeconds;
        public bool UseMeasured;
        public bool Single;
        public double FightLen;
        public int FightCount;
        public double SweepCost;
        public bool NoOther;
        public SweepModel Sweep;
        public double GuardRate, PerfectGuardRate;
        public int kGuardResist = -1, kInfMp = -1;
        public readonly List<DynBuff> DynBuffs = new();
        public readonly List<MagicBuff> MagicBuffs = new();
        public int kEvCd = -1, kBoltHoming = -1;
        public double BoltSideShare = 1;
        public readonly double[] BossResist = new double[4];
        public double BossCritResist, BossToughness, BossDefBonus;
        public string BossAffix;
        public bool StageDefKnown;
        public double StageDef, StagePlate;
        public readonly double[] OtherDebuff = new double[6];
        public double OtherDebuffObjects;
        public int kEvasion = -1, kDashEvasion = -1;
        public double EvadeAttempts = PlayDamaged, BossIgnoreEvasion;
        public readonly List<(int Item, float[] Seconds)> EvadeBarriers = new();
        public readonly List<MpDrain> MpDrains = new();
        public readonly List<MpHeal> MpHeals = new();
        public int kNoMagicCost = -1, kMagicCostReduce = -1;
        public Dictionary<int, double> StatDelta;
        public int kLuck, kFollowerAs = -1, kCharmDmg = -1, kLastBasic = -1;
        public double AsAmp;
        public double Realization = 1;
        public string SwingInfo = "";
        public int kFinalCrit = -1, kFinalDmg = -1, kDashDmg = -1, kMaxMp = -1, kFinalMp = -1;
        public int kWdbDash = -1, kDashCount = -1, kElite = -1, kGoldHand = -1, kGoldHandUnl = -1, kDefToAtk = -1, kDef = -1,
                   kDebuff = -1, kPoison = -1, kFollowerDmg = -1, kFollowerCrit = -1, kFollowerCritContrib = -1, kBlockMagic = -1;
        public double GoldHandPct, GoldHandPctUnl;
        public int kMpSkill = -1, kMagicMp = -1, kTrue = -1, kIgnoreDef = -1, kFsIgnore = -1, kFixedAs = -1, kSpecAs = -1,
                   kUnlimited = -1, kAdvNego = -1, kNego = -1, kFollowerAs2 = -1;
        public double DefaultMp = 50;
        public double EnemyDef;
        public int[] CatHighest = new int[0];
        public readonly List<DebuffInfo> Debuffs = new();
        public readonly HitModel Hits = new();
        public int BurnIdx = -1, ElecIdx = -1, PoisonIdx = -1, PlasmaIdx = -1, kPlasma = -1, kPlasmaDmg = -1, kDebuffDur = -1;
        public readonly WeaponModel Weapon = new();
        public int kBuffDur = -1;
        public EcoModel Eco;
        public readonly Dictionary<int, double> ExtraBase = new();
        public double MultiBase = 1;
        public readonly List<Feeder> MultiCast = new();
        public bool Melee = true;
        public double Dash = PlayDash;
        public double MpGain = 1;
        public double ConstMul = 1;
    }

    static readonly string[] ElemKeys = { "PHYSICALDAMAGE", "FIREDAMAGE", "ICEDAMAGE", "LIGHTNINGDAMAGE" };
    static readonly string[] ElemNames = { "PHYSICAL", "FIRE", "ICE", "LIGHTNING" };
}
