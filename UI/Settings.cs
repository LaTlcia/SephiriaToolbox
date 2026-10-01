using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    public class UiSettings
    {
        public float scale = 1f;
        public bool minimized;
        public bool locked;
        public bool bySource = true;
        public float x = 20f, y = 150f;
        public int tab;
        public string lang = "auto";
        public int arrGoal = (int)ArrangeGoal.Total;
        public bool arrProtect = true, arrRotate = true, arrMoveOthers = true;
        public bool arrUseMeasured;
        public bool arrSingle = true;
        public float wWeapon = 1f, wMagic = 1f, wProc = 1f;
        public bool lootAuto = true;
        public float lootX = -1f, lootY = -1f;
        public List<float> bossFights = new();
        public List<int> bossPhases = new();
        public int bossDebuffSamples, bossOtherBurn, bossOtherFrost;
        public float bossOtherDebuffs;
        public int[] bossDist;
        public float bossSeconds;
        public int bossHits;
        public double bossAddSum;
        public int bossAddSamples;
    }

    UiSettings settings = new();
    bool settingsDirty;
    float nextSettingsSave;
    static string SettingsPath => System.IO.Path.Combine(LogDir, "settings.json");

    void SaveSettings() => SaveSettings(sync: false);

    void SaveSettings(bool sync)
    {
        settingsDirty = false;
        try
        {
            settings.x = windowRect.x;
            settings.y = windowRect.y;
            settings.tab = tab;
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
            if (sync)
            {
                System.IO.Directory.CreateDirectory(LogDir);
                System.IO.File.WriteAllText(SettingsPath, json);
            }
            else WriteFileInBackground(SettingsPath, () => json);
        }
        catch (Exception e) { WarnOnce("保存界面设置", e); }
    }

    void SettingsChangedLater()
    {
        if (settingsDirty) return;
        settingsDirty = true;
        nextSettingsSave = Time.unscaledTime + 30f;
    }

    void SaveSettingsIfDirty()
    {
        if (settingsDirty && Time.unscaledTime >= nextSettingsSave) SaveSettings();
    }
}
