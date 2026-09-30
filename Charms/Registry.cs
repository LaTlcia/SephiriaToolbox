using System;
using System.Collections.Generic;
using UnityEngine;

public partial class SephiriaToolbox
{
    static readonly Dictionary<string, Mech> CharmMechs = new();
    static readonly Dictionary<string, PassiveDef[]> PassiveDefs = new();
    static readonly Dictionary<string, BuffCharm> BuffCharms = new();
    static readonly Dictionary<Type, ExtraFn> CharmExtras = new();
    static readonly Dictionary<Type, Action<Charm_Basic, int, DpsModel>> ModelHooks = new();

    delegate void ExtraFn(DpsSource s, Charm_Basic c, Behavior b, Func<string, int> key, int levels, double baseRate, double swingHits);

    static void Extra<T>(ExtraFn fn) where T : Charm_Basic => CharmExtras[typeof(T)] = fn;

    static void ModelHook<T>(Action<T, int, DpsModel> fn) where T : Charm_Basic => ModelHooks[typeof(T)] = (c, item, d) => fn((T)c, item, d);

    static void ApplyModelHooks(Charm_Basic c, int item, DpsModel d)
    {
        for (var t = c.GetType(); t != null && t != typeof(Charm_Basic); t = t.BaseType)
            if (ModelHooks.TryGetValue(t, out var fn)) { fn(c, item, d); return; }
    }

    static SephiriaToolbox()
    {
        Register(() => RegisterMagitech(), "魔法科技");
        Register(() => RegisterEmber(), "余烬");
        Register(() => RegisterGlacier(), "冰川");
        Register(() => RegisterFrost(), "冰霜武具");
        Register(() => RegisterCurse(), "诅咒");
        Register(() => RegisterPrecision(), "精密");
        Register(() => RegisterPlanet(), "行星");
        Register(() => RegisterCompanion(), "同伴");
        Register(() => RegisterDarkCloud(), "乌云");
        Register(() => RegisterFlameSword(), "太阳剑");
        Register(() => RegisterWindSong(), "风之歌");
        Register(() => RegisterSturdy(), "坚固");
        Register(() => RegisterGuardian(), "守护");
        Register(() => RegisterShadow(), "影子");
        Register(() => RegisterMystic(), "神秘");
        Register(() => RegisterLake(), "湖泊");
        Register(() => RegisterAcademy(), "学院");
        Register(() => RegisterAlchemy(), "炼金术");
        Register(() => RegisterElemental(), "元素");
        Register(() => RegisterParty(), "派对");
        Register(() => RegisterSavvy(), "谈判");
        Register(() => RegisterWeapon(), "锻造");
        Register(() => RegisterShared(), "跨套装（同一个神器类被不同套装的物品共用）");
        Register(() => RegisterNoSet(), "不属于任何套装");
        Register(() => RegisterUnused(), "当前游戏数据里没有物品用到");
    }

    static void Register(Action register, string name)
    {
        try { register(); }
        catch (Exception e) { Debug.LogWarning("[SephiriaToolbox] 神器数据库「" + name + "」登记失败：" + e); }
    }
}
