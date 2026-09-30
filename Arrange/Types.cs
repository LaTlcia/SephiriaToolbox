using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

public partial class SephiriaToolbox
{
    const byte KindEmpty = 0, KindItem = 1, KindCharm = 2, KindMagic = 3;
    const byte CondAnyItem = 1, CondCharm = 2;
    const byte EffLevel = 1, EffDisable = 2, EffIgnore = 3, EffMultiply = 4;

    enum Crit : byte { None, Const, Top, Bottom, SideEnd, Inside, Outlined, BothSideCharm, BothSidesEmpty, NeighborsFull, NearMagic }

    sealed class ArrItem
    {
        public int InstanceId, EntityId, Rarity;
        public string Name;
        public byte Kind;
        public bool IsTablet, Rotatable;
        public int TabletIndex = -1, StartRotation;
        public int MaxLevel, Enchant;
        public Crit Crit;
        public bool CritConst = true, WeaponOk = true;
        public int UniqueGroup = -1;
        public int ActualLevel;
        public bool ActualEnabled;
    }

    struct Cond { public int Slot; public byte Type; }
    struct Eff { public int Slot; public byte Type; public int Param; }

    sealed class Geom
    {
        public bool Blocked;
        public Cond[] Conds;
        public Eff[] Effs;
    }

    sealed class TabletRule
    {
        public string Condition, Effect;
        public int X, Y, Rotation;
    }

    sealed class ArrModel
    {
        public int W, H, N, UniqueGroups;
        public ArrItem[] Items;
        public int[] Start, StartRot;
        public bool[] Movable;
        public int[] TabletItem;
        public TabletRule[] TabletRules, EngravingRules;
        public Geom[][][] TabletGeom;
        public Geom[] Engravings;
        public int[] BaseLevel, BaseMult, BaseDisable, BaseIgnore;
        public ArrangeGoal Goal;
        public bool Protect;
        public DpsModel Dps;
        public bool[] StartOn;
        public double Norm = 1;
        public int[] KeepSideItem;
        public bool[] KeepSideLeft;
        public int[][] Patterns;
        public List<string> Notes = new();
        public bool CanEngrave;
        public int EngraveTablet = -1;

        public ArrModel CloneForEngrave(int t)
        {
            var c = (ArrModel)MemberwiseClone();
            int slot = Array.IndexOf(Start, TabletItem[t]);
            c.Start = (int[])Start.Clone();
            c.StartRot = (int[])StartRot.Clone();
            c.Movable = (bool[])Movable.Clone();
            if (slot >= 0) { c.Start[slot] = -1; c.Movable[slot] = true; }
            c.StartRot[TabletItem.Length + t] = slot;
            c.EngraveTablet = t;
            c.Notes = new List<string>();
            return c;
        }
    }

    sealed class ArrEval
    {
        public double Score;
        public int Enabled, Charms, EffectiveLevels;
        public int[] Level;
        public bool[] On;
        public double[] Source;
        public double Total, Weighted;
    }

    sealed class ArrResult
    {
        public int[] Perm, Rot;
        public ArrEval Before, After;
        public int Swaps, Rotations, Chains;
        public long Evaluations;
        public double Seconds;
        public List<CharmValue> Values;
    }
}
