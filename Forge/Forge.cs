using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public partial class SephiriaToolbox
{
    sealed class ForgeOption
    {
        public int WeaponId, Step;
        public string Name;
        public double Dps = -1;
        public string Error;
        public ArrModel Model;
    }

    Dictionary<string, int> weaponStatDelta;
    HashSet<string> forgeWeaponIds;
    List<ForgeOption> forgeOptions;
    Task forgeTask;
    volatile int forgeProgress;
    int forgeTotal, forgeGeneration;
    double forgeBase = -1;
    string forgeMessage = "", forgeCurrentName = "";

    static Dictionary<string, int> WeaponAddonStats(WeaponSimple w)
    {
        var r = new Dictionary<string, int>(StringComparer.Ordinal);
        if (w == null || w.addons == null) return r;
        void Add(string k, int v) => r[k] = r.GetValueOrDefault(k) + v;
        void AddE(ECustomStat k, int v) => Add(k.ToString().ToUpperInvariant(), v);
        foreach (var ad in w.addons)
        {
            switch (ad)
            {
                case WeaponAddonCommon_Status st when st.status != null:
                    foreach (var s in st.status)
                        if (s != null) AddE(s.statId, s.value);
                    break;
                case WeaponAddonCommon_StatusUnsafe su when su.status != null:
                    foreach (var s in su.status)
                        if (s != null && !string.IsNullOrEmpty(s.statId)) Add(s.statId.ToUpperInvariant(), s.value);
                    break;
            }
            switch (ad)
            {
                case WeaponAddon_OppositeAttackGuard og: AddE(ECustomStat.AttackSpeed, og.additionalAttackSpeed); break;
                case WeaponAddon_DashAttack da: AddE(ECustomStat.DashAttackDamageBonus, da.dashAttackDamageBonus); break;
                case WeaponAddon_BasicAttack ba: AddE(ECustomStat.BasicAttackDamageBonus, ba.basicAttackDamageBonus); break;
                case WeaponAddon_SweepDamage sd: AddE(ECustomStat.SpecialAttackDamageBonus, sd.specialAttackDamage); break;
                case WeaponAddon_ChargedSweep cs: AddE(ECustomStat.SpecialAttackDamageBonus, cs.specialAttackDamage); break;
                case WeaponAddon_GuardResist gr: AddE(ECustomStat.GuardResist, gr.guardResistPercent); break;
                case WeaponAddonGreatsword_BasicAttack gb:
                    AddE(ECustomStat.BasicAttackDamageBonus, gb.basicAttackDamage);
                    AddE(ECustomStat.SpecialAttackDamageBonus, gb.specialAttackDamage);
                    break;
                case WeaponAddonGreatsword_RapidWhirlwind rw: AddE(ECustomStat.SpecialAttackSpeed, rw.specialAttackSpeed); break;
                case WeaponAddonGreatsword_SuperQuick sq:
                    AddE(ECustomStat.SpecialAttackDamageBonus, sq.specialAttackDamage);
                    AddE(ECustomStat.SpecialAttackSpeed, sq.specialAttackSpeed);
                    break;
                case WeaponAddonGreatsword_TripleCombo: AddE(ECustomStat.BasicAttackDamageBonus, 10); break;
                case WeaponAddonCommon_EternalDarkCloud: Add("DARKCLOUDPARRY", 7); break;
            }
        }
        return r;
    }

    void StartForgeAdvice()
    {
        if (forgeTask != null) return;
        forgeMessage = "";
        forgeOptions = null;
        forgeBase = -1;
        var avatar = LocalAvatar();
        if (avatar == null) { forgeMessage = Tr("forge.analysis_only_available_during"); return; }
        var wc = avatar.GetComponent<WeaponControllerSimple>();
        var cur = wc != null ? wc.currentWeapon : null;
        if (cur == null) { forgeMessage = Tr("forge.no_weapon_equipped"); return; }
        List<EnhancementMetadata> first = null;
        try { first = WeaponDatabase.GetWeaponEnhancements(cur.entityId); } catch { }
        if (first == null || first.Count == 0) { forgeMessage = Tr("forge.this_weapon_fully_enhanced"); return; }
        forgeCurrentName = WeaponName(cur.entityId);

        var opts = new List<ForgeOption>();
        foreach (var e1 in first)
        {
            if (e1 == null || e1.enhanced == null) continue;
            opts.Add(new ForgeOption { WeaponId = e1.enhanced.id, Step = 1, Name = WeaponName(e1.enhanced.id) });
            List<EnhancementMetadata> second = null;
            try { second = WeaponDatabase.GetWeaponEnhancements(e1.enhanced.id); } catch { }
            if (second != null)
                foreach (var e2 in second)
                    if (e2 != null && e2.enhanced != null)
                        opts.Add(new ForgeOption { WeaponId = e2.enhanced.id, Step = 2, Name = WeaponName(e2.enhanced.id) });
        }

        var baseModel = BuildForgeModel(avatar, null, null, null, out var err0);
        if (baseModel == null) { forgeMessage = err0; return; }
        var curStats = WeaponAddonStats(cur);
        foreach (var o in opts)
        {
            try
            {
                var ent = WeaponDatabase.FindWeaponById(o.WeaponId);
                var ws = ent != null && ent.mainWeaponPrefab != null ? ent.mainWeaponPrefab.GetComponent<WeaponSimple>() : null;
                if (ws == null) { o.Error = Tr("forge.cannot_read_this_weapon"); continue; }
                var delta = WeaponAddonStats(ws);
                foreach (var kv in curStats) delta[kv.Key] = delta.GetValueOrDefault(kv.Key) - kv.Value;
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var comp in ws.GetComponentsInChildren<Component>(true)) CollectIds(comp, ids);
                o.Model = BuildForgeModel(avatar, ws, delta, ids, out var err);
                if (o.Model == null) o.Error = err;
            }
            catch (Exception e) { WarnOnce("锻造建模 " + o.Name, e); o.Error = Tr("forge.modeling_failed_short"); }
        }
        forgeOptions = opts;
        forgeTotal = 1 + opts.Count(o => o.Model != null);
        forgeProgress = 0;
        bool rotate = settings.arrRotate;
        int seed = Environment.TickCount, gen = ++forgeGeneration;
        forgeTask = Task.Run(() => LowPriority(() =>
        {
            double Best(ArrModel m)
            {
                var r = Optimize(m, rotate, 0.6, seed);
                return r != null && r.After != null ? r.After.Total : -1;
            }
            forgeBase = Best(baseModel);
            forgeProgress = 1;
            foreach (var o in opts)
            {
                if (gen != forgeGeneration) return;
                if (o.Model == null) continue;
                o.Dps = Best(o.Model);
                o.Model = null;
                forgeProgress++;
            }
        }));
    }

    ArrModel BuildForgeModel(PlayerAvatar avatar, WeaponSimple ws, Dictionary<string, int> delta, HashSet<string> ids, out string error)
    {
        weaponOverride = ws;
        weaponStatDelta = delta;
        forgeWeaponIds = ids;
        try
        {
            var model = BuildModel(avatar, settings.arrMoveOthers, out var charmOf, out error);
            if (model == null) return null;
            var goal = (ArrangeGoal)settings.arrGoal;
            if (goal == ArrangeGoal.Levels) goal = ArrangeGoal.Total;
            model.Goal = goal;
            model.Protect = settings.arrProtect;
            model.Dps = BuildDpsModel(avatar, model, charmOf, out _);
            model.Dps.UseMeasured = settings.arrUseMeasured;
            model.Dps.NoOther = true;
            ApplyGoalWeights(model.Dps, goal);
            return model;
        }
        catch (Exception e)
        {
            WarnOnce("锻造建模", e);
            error = Tr("forge.modeling_failed", e.Message);
            return null;
        }
        finally
        {
            weaponOverride = null;
            weaponStatDelta = null;
            forgeWeaponIds = null;
        }
    }

    void UpdateForge()
    {
        if (forgeTask == null || !forgeTask.IsCompleted) return;
        if (forgeTask.IsFaulted)
        {
            WarnOnce("锻造建议", forgeTask.Exception?.GetBaseException() ?? new Exception("未知错误"));
            forgeMessage = Tr("forge.calculation_error_please_try");
            forgeOptions = null;
        }
        forgeTask = null;
        if (forgeOptions != null) OpenBestForgeGroup();
    }
}
