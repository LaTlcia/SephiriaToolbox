using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    enum DebuffType : byte { Burn, Electric, Frostbite, Poison, Wound, Plasma }

    static readonly string[] DebuffIds = { "BURN", "ELECTRIC", "FROSTBITE", "POISON", "WOUND", "PLASMA" };
    static readonly string[] DebuffNames = { Tr("debuff.burn"), Tr("debuff.electrocution"), Tr("debuff.frostbite"), Tr("debuff.poison"), Tr("debuff.wound"), Tr("debuff.plasma") };

    sealed class DebuffApplier
    {
        public byte Kind;
        public int Cat = -1, Activate, HasteCount;
        public double Cooldown = 2, Haste = 100;
        public int Elem = -1;
        public int Key = -1;
        public int Item = -1;
        public int Src = -1;
        public float[] ChanceTable;
        public float[] PerLevel;
        public bool PerTarget;
        public double Chance = 1;
        public string Name = "";
    }

    sealed class DebuffInfo
    {
        public DebuffType Type;
        public int Source = -1, Source2 = -1;
        public double Duration0 = 5, Tick0 = 0.5, Dmg0 = 1, StatPct = 100, FreezeMul = 2;
        public bool Renew = true;
        public int kStack = -1, kAdd = -1, kDmg = -1, kSpeed = -1, kDur = -1, kEvo = -1, kBlue = -1, kLuck = -1, kQuick = -1,
                   kFrostDmg = -1, kFreezeDmg = -1, kFreezeTh = -1, kStack2 = -1, kDmg2 = -1;
        public readonly List<DebuffApplier> Appliers = new();
        public double Targets = -1, Stacks = -1;
        public double Kappa = 1;
        public double Residual;
        public readonly List<(int Item, float[] Pct)> ExtraStack = new();
    }

    sealed class HitModel
    {
        public double Swing, Other, Special;
        public double SwingM, OtherM, SpecialM;
        public int WeaponElem = -1;
    }
}
