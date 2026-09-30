using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    const string SourceLang = "zh-CN";
    const char TokOpen = '\u0001', TokSep = '\u0002', TokClose = '\u0003', TokNum = '\u0004';
    const int ResolveCacheMax = 4096;

    static string uiLang = SourceLang;
    static Dictionary<string, string> zhPack = LoadPack(SourceLang);
    static Dictionary<string, string> curPack = zhPack;
    static Dictionary<string, string> legacyKeys = new(StringComparer.Ordinal);
    static readonly Dictionary<string, string> resolveCache = new(StringComparer.Ordinal);
    static readonly Dictionary<string[], string[]> resolvedArrays = new();
    float nextLangCheck;

    static string Tr(string key) => TokOpen + key + TokClose;

    static string Tr(string key, params object[] args)
    {
        var sb = new StringBuilder(key.Length + 16 * (args?.Length ?? 0) + 2);
        sb.Append(TokOpen).Append(key);
        if (args != null)
            foreach (var a in args)
            {
                sb.Append(TokSep);
                switch (a)
                {
                    case null: break;
                    case string s: sb.Append(s); break;
                    case int v: sb.Append(TokNum).Append('i').Append(v.ToString(CultureInfo.InvariantCulture)); break;
                    case long v: sb.Append(TokNum).Append('l').Append(v.ToString(CultureInfo.InvariantCulture)); break;
                    case short or byte or sbyte or ushort or uint: sb.Append(TokNum).Append('l').Append(Convert.ToInt64(a).ToString(CultureInfo.InvariantCulture)); break;
                    case float v: sb.Append(TokNum).Append('f').Append(v.ToString("G9", CultureInfo.InvariantCulture)); break;
                    case double v: sb.Append(TokNum).Append('d').Append(v.ToString("G17", CultureInfo.InvariantCulture)); break;
                    case decimal v: sb.Append(TokNum).Append('m').Append(v.ToString(CultureInfo.InvariantCulture)); break;
                    default: sb.Append(Convert.ToString(a, CultureInfo.CurrentCulture)); break;
                }
            }
        return sb.Append(TokClose).ToString();
    }

    static string Resolve(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.IndexOf(TokOpen) < 0)
        {
            var legacy = legacyKeys;
            return legacy.Count > 0 && legacy.TryGetValue(s, out var key) ? Expand(Tr(key), curPack) : s;
        }
        lock (resolveCache)
            if (resolveCache.TryGetValue(s, out var hit)) return hit;
        string r = Expand(s, curPack);
        lock (resolveCache)
        {
            if (resolveCache.Count >= ResolveCacheMax) resolveCache.Clear();
            resolveCache[s] = r;
        }
        return r;
    }

    static string[] ResolveAll(string[] a)
    {
        if (a == null) return a;
        lock (resolvedArrays)
        {
            if (resolvedArrays.TryGetValue(a, out var t)) return t;
            if (resolvedArrays.Count >= 64) resolvedArrays.Clear();
            return resolvedArrays[a] = a.Select(Resolve).ToArray();
        }
    }

    static string ResolveZh(string s) => string.IsNullOrEmpty(s) || s.IndexOf(TokOpen) < 0 ? s : Expand(s, zhPack);

    static string ClipWidth(string s, int units)
    {
        if (string.IsNullOrEmpty(s)) return s;
        int w = 0;
        for (int i = 0; i < s.Length; i++)
        {
            w += s[i] >= 0x2E80 ? 2 : 1;
            if (w > units) return s.Substring(0, i);
        }
        return s;
    }

    static string Expand(string s, Dictionary<string, string> pack)
    {
        int i = s.IndexOf(TokOpen);
        if (i < 0) return s;
        var sb = new StringBuilder(s.Length + 32);
        int pos = 0;
        while (i >= 0)
        {
            sb.Append(s, pos, i - pos);
            int end = TokenEnd(s, i);
            if (end < 0) break;
            sb.Append(ExpandToken(s, i + 1, end, pack));
            pos = end + 1;
            i = s.IndexOf(TokOpen, pos);
        }
        if (pos < s.Length) sb.Append(s, pos, s.Length - pos);
        return sb.ToString();
    }

    static int TokenEnd(string s, int open)
    {
        int depth = 0;
        for (int j = open; j < s.Length; j++)
        {
            if (s[j] == TokOpen) depth++;
            else if (s[j] == TokClose && --depth == 0) return j;
        }
        return -1;
    }

    static string ExpandToken(string s, int start, int end, Dictionary<string, string> pack)
    {
        var parts = new List<string>(4);
        int depth = 0, from = start;
        for (int j = start; j < end; j++)
        {
            char c = s[j];
            if (c == TokOpen) depth++;
            else if (c == TokClose) depth--;
            else if (c == TokSep && depth == 0)
            {
                parts.Add(s.Substring(from, j - from));
                from = j + 1;
            }
        }
        parts.Add(s.Substring(from, end - from));
        string key = parts[0];
        if (!pack.TryGetValue(key, out var template) && !zhPack.TryGetValue(key, out template)) template = key;
        if (parts.Count == 1) return template;
        var args = new object[parts.Count - 1];
        for (int k = 1; k < parts.Count; k++) args[k - 1] = DecodeArg(parts[k], pack);
        try { return string.Format(CultureInfo.CurrentCulture, template, args); }
        catch (FormatException) { return template + " " + string.Join(" ", args); }
    }

    static object DecodeArg(string a, Dictionary<string, string> pack)
    {
        if (a.Length < 2 || a[0] != TokNum) return Expand(a, pack);
        string v = a.Substring(2);
        var inv = CultureInfo.InvariantCulture;
        switch (a[1])
        {
            case 'i': return int.Parse(v, inv);
            case 'l': return long.Parse(v, inv);
            case 'f': return float.Parse(v, NumberStyles.Float, inv);
            case 'd': return double.Parse(v, NumberStyles.Float, inv);
            case 'm': return decimal.Parse(v, NumberStyles.Float, inv);
            default: return v;
        }
    }

    static void ApplyUiLanguage(string lang)
    {
        var zh = LoadPack(SourceLang);
        var cur = lang == SourceLang ? zh : LoadPack(lang);
        var legacy = new Dictionary<string, string>(StringComparer.Ordinal);
        if (lang != SourceLang)
            foreach (var kv in zh)
                if (!kv.Key.StartsWith("__", StringComparison.Ordinal) && kv.Value.Length > 0 && kv.Value.IndexOf('{') < 0 && !legacy.ContainsKey(kv.Value))
                    legacy[kv.Value] = kv.Key;
        lock (resolveCache)
        {
            uiLang = lang;
            zhPack = zh;
            curPack = cur;
            legacyKeys = legacy;
            resolveCache.Clear();
        }
        lock (resolvedArrays) resolvedArrays.Clear();
    }

    static Dictionary<string, string> LoadPack(string lang)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        ReadPack(entries, EmbeddedPack(lang));
        ReadPack(entries, ExternalPack(lang));
        return entries;
    }

    static bool HasPack(string lang) => EmbeddedPack(lang) != null || ExternalPack(lang) != null;

    static List<string> AvailableUiLangs()
    {
        var list = new List<string> { SourceLang };
        foreach (var name in typeof(SephiriaToolbox).Assembly.GetManifestResourceNames())
            if (name.StartsWith("Lang.", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
                list.Add(name.Substring(5, name.Length - 10));
        var dir = ExternalLangDir();
        if (dir != null && Directory.Exists(dir))
            foreach (var f in Directory.GetFiles(dir, "*.json"))
                list.Add(Path.GetFileNameWithoutExtension(f));
        return list.Distinct().ToList();
    }

    static string EmbeddedPack(string lang)
    {
        using var st = typeof(SephiriaToolbox).Assembly.GetManifestResourceStream("Lang." + lang + ".json");
        if (st == null) return null;
        using var rd = new StreamReader(st, Encoding.UTF8);
        return rd.ReadToEnd();
    }

    static string ExternalLangDir()
    {
        try
        {
            string dll = typeof(SephiriaToolbox).Assembly.Location;
            return string.IsNullOrEmpty(dll) ? null : Path.Combine(Path.GetDirectoryName(dll), "lang");
        }
        catch { return null; }
    }

    static string ExternalPack(string lang)
    {
        try
        {
            var dir = ExternalLangDir();
            string p = dir == null ? null : Path.Combine(dir, lang + ".json");
            return p != null && File.Exists(p) ? File.ReadAllText(p, Encoding.UTF8) : null;
        }
        catch { return null; }
    }

    static void ReadPack(Dictionary<string, string> into, string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            foreach (var p in JObject.Parse(json).Properties())
                if (p.Value.Type == JTokenType.String) into[p.Name] = (string)p.Value;
        }
        catch (Exception e) { Debug.LogWarning("[SephiriaToolbox] 语言包读取失败：" + e.Message); }
    }

    static string LangShortName(string lang) =>
        LoadPack(lang).TryGetValue("__short", out var s) && !string.IsNullOrEmpty(s) ? s : lang;

    static string GameUiLang()
    {
        string g = null;
        try { g = LocalizationManager.Instance?.CurrentLanguage; }
        catch { }
        if (string.IsNullOrEmpty(g)) return null;
        if (HasPack(g)) return g;
        return g.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? SourceLang : "en-US";
    }

    void UpdateUiLanguage()
    {
        if (Time.unscaledTime < nextLangCheck) return;
        nextLangCheck = Time.unscaledTime + 1f;
        string want = string.IsNullOrEmpty(settings.lang) || settings.lang == "auto" ? GameUiLang() : settings.lang;
        if (want != null && want != SourceLang && !HasPack(want)) want = SourceLang;
        if (want != null && want != uiLang) ApplyUiLanguage(want);
    }

    void CycleUiLanguage()
    {
        var langs = AvailableUiLangs();
        int i = langs.IndexOf(uiLang);
        settings.lang = langs[(i + 1) % langs.Count];
        SaveSettings();
        ApplyUiLanguage(settings.lang);
    }

    string nextLangLabel, nextLangLabelFor;
    string NextLangLabel()
    {
        if (nextLangLabelFor != uiLang)
        {
            var langs = AvailableUiLangs();
            int i = langs.IndexOf(uiLang);
            nextLangLabel = LangShortName(langs[(i + 1) % langs.Count]);
            nextLangLabelFor = uiLang;
        }
        return nextLangLabel;
    }

    static class GUILayout
    {
        public static void Label(string text, params GUILayoutOption[] options) => UnityEngine.GUILayout.Label(Resolve(text), options);
        public static void Label(string text, GUIStyle style, params GUILayoutOption[] options) => UnityEngine.GUILayout.Label(Resolve(text), style, options);
        public static bool Button(string text, params GUILayoutOption[] options) => UnityEngine.GUILayout.Button(Resolve(text), options);
        public static bool Button(string text, GUIStyle style, params GUILayoutOption[] options) => UnityEngine.GUILayout.Button(Resolve(text), style, options);
        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) => UnityEngine.GUILayout.Toggle(value, Resolve(text), options);
        public static int Toolbar(int selected, string[] texts, params GUILayoutOption[] options) => UnityEngine.GUILayout.Toolbar(selected, ResolveAll(texts), options);
        public static Rect Window(int id, Rect rect, GUI.WindowFunction func, string text, params GUILayoutOption[] options) =>
            UnityEngine.GUILayout.Window(id, rect, func, Resolve(text), options);
        public static float HorizontalSlider(float value, float left, float right, params GUILayoutOption[] options) =>
            UnityEngine.GUILayout.HorizontalSlider(value, left, right, options);
        public static Vector2 BeginScrollView(Vector2 position, params GUILayoutOption[] options) => UnityEngine.GUILayout.BeginScrollView(position, options);
        public static void EndScrollView() => UnityEngine.GUILayout.EndScrollView();
        public static void BeginHorizontal(params GUILayoutOption[] options) => UnityEngine.GUILayout.BeginHorizontal(options);
        public static void EndHorizontal() => UnityEngine.GUILayout.EndHorizontal();
        public static void Space(float pixels) => UnityEngine.GUILayout.Space(pixels);
        public static void FlexibleSpace() => UnityEngine.GUILayout.FlexibleSpace();
        public static GUILayoutOption Width(float width) => UnityEngine.GUILayout.Width(width);
        public static GUILayoutOption Height(float height) => UnityEngine.GUILayout.Height(height);
        public static GUILayoutOption ExpandWidth(bool expand) => UnityEngine.GUILayout.ExpandWidth(expand);
    }
}
