using System;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    const string BloodHudStack = "GreatswordBloodStack", BloodHudTransform = "GreatswordTransformed";
    const double BloodDefaultShare = 0.9;
    const int BloodMinSamples = 3;

    UnitAvatar bloodOwner;
    Action<string, string> bloodCreateHandler, bloodValueHandler;
    Action<string> bloodDestroyHandler;
    int bloodLiveStack = -1;
    bool bloodHudOn;
    float bloodEngagedAt = -1;
    double bloodShareSum;
    int bloodShareCount;

    static double UncappedMaxHp(UnitAvatar a, out bool cursed)
    {
        cursed = false;
        double free = a.maxHp + a.finalMaxHp * a.maxHp / 100.0;
        if (a.isHPCursed <= 0) return free;
        var live = a.GetComponent<WeaponControllerSimple>()?.currentWeapon as WeaponSimple_GreatSword;
        var bb = live != null && live.addons != null ? live.addons.OfType<WeaponAddonGreatsword_BoneBlood>().FirstOrDefault(x => x != null) : null;
        if (bb != null && live.isTransformed)
        {
            if (FieldValue(bb, "isEngaged") is bool engaged && engaged)
            {
                if (FieldValue(bb, "cachedHpCursed") is sbyte was && was > 0)
                {
                    cursed = true;
                    return Convert.ToDouble(FieldValue(bb, "cachedCursedMaxHp"));
                }
                return free;
            }
            if (a.cursedMaxHp == Math.Min((int)Math.Floor(free), bb.transformedMaxHp)) return free;
        }
        cursed = true;
        return a.cursedMaxHp;
    }

    void TrackBlood()
    {
        var a = LocalAvatar();
        if (a != bloodOwner)
        {
            UntrackBlood();
            if (a == null) return;
            bloodOwner = a;
            bloodCreateHandler = (id, name) => { if (name == BloodHudStack) bloodHudOn = true; };
            bloodValueHandler = (name, value) =>
            {
                if (name == BloodHudTransform)
                {
                    if (bloodLiveStack < 0) bloodLiveStack = 0;
                    bloodEngagedAt = Time.unscaledTime;
                }
                else if (name == BloodHudStack && int.TryParse(value, out int stack))
                {
                    bloodLiveStack = Math.Max(0, stack);
                    bloodHudOn = true;
                }
            };
            bloodDestroyHandler = name =>
            {
                if (name == BloodHudStack) { bloodHudOn = false; bloodLiveStack = bloodLiveStack < 0 ? -1 : 0; }
                else if (name == BloodHudTransform) { bloodHudOn = false; bloodLiveStack = -1; bloodEngagedAt = -1; }
            };
            a.OnEffectHUDCreated += bloodCreateHandler;
            a.OnEffectHUDSetValue += bloodValueHandler;
            a.OnEffectHUDDestroyed += bloodDestroyHandler;
            return;
        }
        if (a == null || bloodEngagedAt < 0 || Time.unscaledTime - bloodEngagedAt < 0.3f) return;
        bloodEngagedAt = -1;
        var live = a.GetComponent<WeaponControllerSimple>()?.currentWeapon as WeaponSimple_GreatSword;
        var bb = live != null && live.addons != null ? live.addons.OfType<WeaponAddonGreatsword_BoneBlood>().FirstOrDefault(x => x != null) : null;
        if (bb == null || bloodLiveStack < 0) return;
        if (bloodHudOn && bloodLiveStack == 0) return;
        double max = Math.Floor(UncappedMaxHp(a, out bool cursed)), kept = Math.Min(max, bb.transformedMaxHp);
        if (cursed || max <= kept) return;
        bloodShareSum += Math.Min(1, (bloodLiveStack + kept) / max);
        bloodShareCount++;
    }

    void UntrackBlood()
    {
        if (bloodOwner != null)
        {
            try
            {
                bloodOwner.OnEffectHUDCreated -= bloodCreateHandler;
                bloodOwner.OnEffectHUDSetValue -= bloodValueHandler;
                bloodOwner.OnEffectHUDDestroyed -= bloodDestroyHandler;
            }
            catch { }
        }
        bloodOwner = null;
        bloodCreateHandler = bloodValueHandler = null;
        bloodDestroyHandler = null;
        bloodLiveStack = -1;
        bloodHudOn = false;
        bloodEngagedAt = -1;
        bloodShareSum = 0;
        bloodShareCount = 0;
    }

    double BloodShare(out bool measured)
    {
        measured = bloodShareCount >= BloodMinSamples;
        return measured ? Math.Min(1, Math.Max(0, bloodShareSum / bloodShareCount)) : BloodDefaultShare;
    }

    int BloodLiveStack(UnitAvatar a, WeaponAddonGreatsword_BoneBlood bb)
    {
        if (a != bloodOwner) return 0;
        if (bloodLiveStack > 0) return bloodLiveStack;
        if (!bloodHudOn) return 0;
        double max = UncappedMaxHp(a, out bool cursed), kept = Math.Min(Math.Floor(max), bb.transformedMaxHp);
        return cursed ? 0 : (int)Math.Max(0, Math.Floor(max * BloodShare(out _)) - kept);
    }
}
