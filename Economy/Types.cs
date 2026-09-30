using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public partial class SephiriaToolbox
{
    enum EcoKind : byte { None, DarkCloud, FlameSword, FlameGround, TrueDamage, Debuff }

    sealed class Feeder
    {
        public int Item;
        public byte Kind;
        public float[] PerSec;
        public float[] Chance, Cd;
        public double Hits, HitsFixed;
        public float[] Radius;
        public bool AtPlayer;
        public double Mul = 1;
        public bool MultiOnly;
    }

    sealed class EcoModel
    {
        public double Battle = DefaultBattle;
        public double Enemies = PlayEnemies;
        public int kCloudMin = -1, kCloudScale = -1, kCloudRestore = -1, kCloudKeep = -1, kCloudMulti = -1, kCloudLuck = -1,
                   kCloudDmg = -1, kCloudSpeed = -1, kCloudAsBonus = -1, kCloudIce = -1;
        public double CloudPct = 100, CloudPctIce = 50, CloudSpeed0 = 1, CloudInterval = 2, CloudDefault = 20, CloudRestorePct = 5, CloudRestoreSec = 5;
        public double CloudOther, CloudOtherAs;
        public readonly List<Feeder> CloudGen = new(), CloudUse = new();
        public int kFsDmg = -1, kFsCrit = -1, kFsCritDmg = -1, kFsLuck = -1, kFsMagic = -1, kFsMax = -1, kFsPick = -1, kFsReturn = -1,
                   kFsFall = -1, kFsAdd = -1, kFsAddW = -1, kFsAddM = -1, kFsFrost = -1,
                   kFsRelic = -1;
        public double FsPct = 150, FsLuckBonus = 100, FsMinCd = 0.15, FsLife = 2, FsWalk = 2, FsMax0 = 4;
        public double FsHitsSwing, FsHitsOther;
        public readonly List<Feeder> FsGen = new();
        public int[] GroundGate = new int[0];
        public int kGroundDur = -1, kGroundRange = -1, kDebuffDmg = -1;
        public double GroundOther;
        public readonly List<Feeder> GroundGen = new();
        public double SwingHits, OtherHits;
    }

    const double DefaultBattle = 40;
    const double BreadPickup = 0.7;
    const double FsWalkMelee = 2, FsWalkRanged = 6;
    const double FlagInRange = 0.6;
    const double UpFloorStart = 0.6;
    const double PlayFreeze = 0.15, PlayPotion = 0.01;
    const double EliteShare = 0.3;
    const double DotTargetShare = 0.6;

    const string MaxMpKey = "__MAXMP", FinalCritKey = "__FINALCOMBOCRITICAL", FinalDmgKey = "__FINALCOMBODAMAGE", DashDmgKey = "__DASHATTACKDAMAGE",
                 GroundDurKey = "__FLAMEGROUNDDURATION", GroundRangeKey = "__FLAMEGROUNDRANGE";
}
