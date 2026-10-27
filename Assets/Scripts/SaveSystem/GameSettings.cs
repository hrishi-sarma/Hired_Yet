using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One place for every player setting (saved in PlayerPrefs, shared by the main-menu SETTINGS card and the pause menu).
/// Audio: a master volume plus one volume per kind of sound. Graphics: ON/OFF + intensity for the post-processing effects, plus video options (resolution, window mode, vsync, fps cap, quality, render scale, shadows, anti-aliasing, textures, brightness).
/// Only values the player has actually changed are applied, so the game's own defaults stay untouched until then.
/// </summary>
public static class GameSettings
{
    // ------------------------------------------------------------------ audio
    public static readonly string[] AudioKeys = { "master", "music", "ambient", "animals", "vehicles", "footsteps", "voices", "effects" };
    public static readonly string[] AudioNames = { "MASTER", "MUSIC", "AMBIENCE", "ANIMALS", "VEHICLES", "FOOTSTEPS", "VOICES", "ITEMS & GUITAR" };
    static string AKey(int i) { return i == 0 ? "rg_volume" : "rg_vol_" + AudioKeys[i]; }   // master keeps its old key, so saved volumes carry over

    public static float GetAudio(int i) { return PlayerPrefs.GetFloat(AKey(i), 1f); }
    public static void SetAudio(int i, float v)
    {
        v = Mathf.Clamp01(v); PlayerPrefs.SetFloat(AKey(i), v); Dirty = true;
        if (i == 0 && !AudioListener.pause) AudioListener.volume = v;   // while paused PauseAudio owns the listener volume (it ramps back to Master on resume)
    }
    /// <summary>Master volume (applied through AudioListener.volume, which PauseAudio now fades relative to this value).</summary>
    public static float Master { get { return GetAudio(0); } }

    /// <summary>Per-kind volume for an AudioDirector category name (master is applied separately, on the listener).</summary>
    public static float Category(string cat)
    {
        if (string.IsNullOrEmpty(cat)) return GetAudio(7);
        switch (cat.ToLowerInvariant())
        {
            case "menu": case "music": return GetAudio(1);
            case "ambient": return GetAudio(2);
            case "animals": return GetAudio(3);
            case "cars": case "vehicles": return GetAudio(4);
            case "footsteps": return GetAudio(5);
            case "npcs": case "interview": case "voices": return GetAudio(6);
            default: return GetAudio(7);
        }
    }

    // ------------------------------------------------------------------ graphics: effects (ON/OFF + intensity)
    public static readonly string[] GfxNames = { "LENS FLARE" };
    static readonly string[] GKeys = { "rg_lensflare" };
    static readonly bool[] GDef = { true };
    static float flareBase = -1f, dayFlareBase = -1f;

    /// <summary>Set whenever a setting changes; GfxApplier flushes PlayerPrefs once a second so nothing is lost on a crash.</summary>
    public static bool Dirty;

    public static bool GetOn(int i) { return PlayerPrefs.GetInt(GKeys[i], GDef[i] ? 1 : 0) == 1; }
    public static float GetAmt(int i) { return PlayerPrefs.GetFloat(GKeys[i] + "_amt", 0.5f); }
    public static void SetOn(int i, bool on) { PlayerPrefs.SetInt(GKeys[i], on ? 1 : 0); Dirty = true; Apply(i); }
    public static void SetAmt(int i, float a) { PlayerPrefs.SetFloat(GKeys[i] + "_amt", Mathf.Clamp01(a)); Dirty = true; Apply(i); }
    static bool Touched(int i) { return PlayerPrefs.HasKey(GKeys[i]) || PlayerPrefs.HasKey(GKeys[i] + "_amt"); }

    /// <summary>Piecewise curve: 0..0.5 -> 0..baseValue (the game's own look at 50%), 0.5..1 -> baseValue..maxValue.</summary>
    static float Curve(float a, float baseValue, float maxValue)
    {
        return a <= 0.5f ? baseValue * (a * 2f) : Mathf.Lerp(baseValue, maxValue, (a - 0.5f) * 2f);
    }

    /// <summary>Re-applies everything the player has changed (also called every second, because scenes create their own effect objects).</summary>
    public static void ApplyAll()
    {
        for (int i = 0; i < GfxNames.Length; i++) if (Touched(i)) Apply(i);
        ApplyVideoContinuous();
    }

    public static void Apply(int i)
    {
        bool on = GetOn(i); float a = GetAmt(i);
        if (i == 0)
        {
            float mul = a * 2f;   // 50% = the game's own flare strength
            foreach (var c in Object.FindObjectsByType<SunLensEffect>(FindObjectsSortMode.None)) { c.enabled = on; var cv = c.GetComponent<Canvas>(); if (cv != null) cv.enabled = on; }
            foreach (var c in Object.FindObjectsByType<ProceduralLensDirtFlare>(FindObjectsSortMode.None))
            {
                if (flareBase < 0f && c.flareOpacity > 0f) { flareBase = c.flareOpacity; dayFlareBase = c.dayFlareOpacity; }
                float fb = flareBase < 0f ? 0.12f : flareBase, db = dayFlareBase < 0f ? 0.55f : dayFlareBase;
                c.flareOpacity = on ? Mathf.Clamp01(fb * mul) : 0f; c.dayFlareOpacity = on ? Mathf.Clamp01(db * mul) : 0f;
            }
            return;
        }
    }

    // ------------------------------------------------------------------ video options
    // choices (click to cycle): resolution, window mode, vsync, fps cap, quality preset, shadows, anti-aliasing, textures
    public static readonly string[] ChoiceNames = { "RESOLUTION", "WINDOW MODE", "VSYNC", "FPS LIMIT", "QUALITY", "SHADOWS", "ANTI-ALIAS", "TEXTURES" };
    // sliders: render scale, brightness
    public static readonly string[] SliderNames = { "RENDER SCALE", "BRIGHTNESS" };
    static readonly string[] ModeNames = { "FULLSCREEN", "BORDERLESS", "WINDOWED" };
    static readonly int[] FpsValues = { 30, 60, 90, 120, 144, 240, -1 };
    static readonly string[] ShadowNames = { "OFF", "LOW", "MEDIUM", "HIGH", "ULTRA" };
    static readonly float[] ShadowDist = { 0f, 50f, 115f, 180f, 300f };
    static readonly string[] AaNames = { "OFF", "FXAA", "SMAA", "MSAA 2X", "MSAA 4X" };
    static readonly string[] TexNames = { "FULL", "HALF", "QUARTER" };

    static string CKey(int id) { return "rg_vid_" + id; }
    static bool CTouched(int id) { return PlayerPrefs.HasKey(CKey(id)); }
    static UniversalRenderPipelineAsset Urp { get { return GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; } }

    static List<Vector2Int> resList;
    static List<Vector2Int> Resolutions()
    {
        if (resList != null) return resList;
        resList = new List<Vector2Int>();
        foreach (Resolution r in Screen.resolutions)
        {
            if (r.width < 800 || r.height < 480) continue;
            var v = new Vector2Int(r.width, r.height);
            if (!resList.Contains(v)) resList.Add(v);
        }
        var cur = new Vector2Int(Screen.width, Screen.height);
        if (resList.Count == 0) resList.Add(cur);
        resList.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return resList;
    }

    static int NearestRes(int w, int h)
    {
        var l = Resolutions(); int best = 0; long bd = long.MaxValue;
        for (int i = 0; i < l.Count; i++) { long d = (long)(l[i].x - w) * (l[i].x - w) + (long)(l[i].y - h) * (l[i].y - h); if (d < bd) { bd = d; best = i; } }
        return best;
    }

    public static int ChoiceCount(int id)
    {
        switch (id)
        {
            case 0: return Resolutions().Count;
            case 1: return ModeNames.Length;
            case 2: return 2;
            case 3: return FpsValues.Length;
            case 4: return Mathf.Max(1, QualitySettings.names.Length);
            case 5: return ShadowNames.Length;
            case 6: return AaNames.Length;
            default: return TexNames.Length;
        }
    }

    static int Derive(int id)
    {
        switch (id)
        {
            case 0: return NearestRes(Screen.width, Screen.height);
            case 1: { var m = Screen.fullScreenMode; return m == FullScreenMode.ExclusiveFullScreen ? 0 : (m == FullScreenMode.Windowed ? 2 : 1); }
            case 2: return QualitySettings.vSyncCount > 0 ? 1 : 0;
            case 3: { int t = Application.targetFrameRate; for (int i = 0; i < FpsValues.Length; i++) if (FpsValues[i] == t) return i; return FpsValues.Length - 1; }
            case 4: return QualitySettings.GetQualityLevel();
            case 5: { var a = Urp; float d = a != null ? a.shadowDistance : QualitySettings.shadowDistance; int b = 0; float bd = 1e9f; for (int i = 0; i < ShadowDist.Length; i++) { float x = Mathf.Abs(ShadowDist[i] - d); if (x < bd) { bd = x; b = i; } } return b; }
            case 6:
                {
                    var a = Urp; if (a != null && a.msaaSampleCount >= 4) return 4; if (a != null && a.msaaSampleCount == 2) return 3;
                    var cam = Camera.main; if (cam != null) { var d = cam.GetComponent<UniversalAdditionalCameraData>(); if (d != null) return d.antialiasing == AntialiasingMode.FastApproximateAntialiasing ? 1 : (d.antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing ? 2 : 0); }
                    return 0;
                }
            default:
#pragma warning disable 618
                return Mathf.Clamp(QualitySettings.masterTextureLimit, 0, TexNames.Length - 1);
#pragma warning restore 618
        }
    }

    public static int GetChoice(int id)
    {
        int n = Mathf.Max(1, ChoiceCount(id));
        int v = CTouched(id) ? PlayerPrefs.GetInt(CKey(id), 0) : Derive(id);
        return Mathf.Clamp(v, 0, n - 1);
    }

    public static string ChoiceLabel(int id)
    {
        int v = GetChoice(id);
        switch (id)
        {
            case 0: { var r = Resolutions(); return r[v].x + " X " + r[v].y; }
            case 1: return ModeNames[v];
            case 2: return v == 1 ? "ON" : "OFF";
            case 3: return FpsValues[v] < 0 ? "UNLIMITED" : FpsValues[v].ToString();
            case 4: { string[] n = QualitySettings.names; return n.Length == 0 ? "DEFAULT" : n[Mathf.Clamp(v, 0, n.Length - 1)].ToUpperInvariant(); }
            case 5: return ShadowNames[v];
            case 6: return AaNames[v];
            default: return TexNames[v];
        }
    }

    /// <summary>Moves a choice one step (dir = +1 / -1, wraps around) and applies it right away.</summary>
    public static void CycleChoice(int id, int dir)
    {
        int n = Mathf.Max(1, ChoiceCount(id));
        int v = (GetChoice(id) + (dir >= 0 ? 1 : n - 1)) % n;
        PlayerPrefs.SetInt(CKey(id), v); Dirty = true;
        if (id == 4)
        {   // a new preset replaces the individual quality choices made before it
            if (QualitySettings.names.Length > 0) QualitySettings.SetQualityLevel(v, true);
            PlayerPrefs.DeleteKey(CKey(5)); PlayerPrefs.DeleteKey(CKey(6)); PlayerPrefs.DeleteKey(CKey(7)); PlayerPrefs.DeleteKey("rg_vids_0");
            ApplyVideoContinuous();
            return;
        }
        if (id <= 1) ApplyDisplay();
        else ApplyVideoContinuous();
    }

    public static float GetVideoSlider(int id)
    {
        string k = "rg_vids_" + id;
        if (PlayerPrefs.HasKey(k)) return Mathf.Clamp01(PlayerPrefs.GetFloat(k));
        if (id == 0) { var a = Urp; return a != null ? Mathf.Clamp01((a.renderScale - 0.5f) / 0.75f) : 0.667f; }
        return 0.5f;
    }
    public static void SetVideoSlider(int id, float v) { PlayerPrefs.SetFloat("rg_vids_" + id, Mathf.Clamp01(v)); Dirty = true; ApplyVideoContinuous(); }
    public static float RenderScale { get { return 0.5f + 0.75f * GetVideoSlider(0); } }
    /// <summary>Extra exposure (EV) added by the BRIGHTNESS slider (0.5 = no change). Read by AtmosphereEffects.</summary>
    public static float BrightnessEv { get { return PlayerPrefs.HasKey("rg_vids_1") ? (Mathf.Clamp01(PlayerPrefs.GetFloat("rg_vids_1")) - 0.5f) * 3f : 0f; } }
    public static string SliderLabel(int id)
    {
        float v = GetVideoSlider(id);
        return id == 0 ? Mathf.RoundToInt(RenderScale * 100f) + "%" : (v >= 0.5f ? "+" : "") + Mathf.RoundToInt((v - 0.5f) * 200f) + "%";
    }

    /// <summary>Plain fullscreen on/off used by the older toggle buttons; keeps the window-mode choice in step.</summary>
    public static void SetFullscreen(bool on)
    {
        PlayerPrefs.SetInt(CKey(1), on ? 1 : 2); Dirty = true; ApplyDisplay();
    }

    /// <summary>Resolution + window mode (applied on change and once at start-up, never every frame).</summary>
    public static void ApplyDisplay()
    {
        if (!CTouched(0) && !CTouched(1)) return;
        var l = Resolutions();
        int ri = GetChoice(0); int mi = GetChoice(1);
        FullScreenMode mode = mi == 0 ? FullScreenMode.ExclusiveFullScreen : (mi == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        Vector2Int r = l[Mathf.Clamp(ri, 0, l.Count - 1)];
        if (!CTouched(0)) r = new Vector2Int(Screen.width, Screen.height);
        if (mode == FullScreenMode.FullScreenWindow && !CTouched(0)) { Screen.fullScreenMode = mode; return; }
        Screen.SetResolution(r.x, r.y, mode);
    }

    /// <summary>Everything that is safe to re-apply every second (only touched settings are written).</summary>
    static void ApplyVideoContinuous()
    {
        if (CTouched(2)) { int vs = GetChoice(2); if (QualitySettings.vSyncCount != vs) QualitySettings.vSyncCount = vs; }
        if (CTouched(3))
        {
            int f = FpsValues[GetChoice(3)];
            if (Application.targetFrameRate != f) Application.targetFrameRate = f;
        }
        var asset = Urp;
        bool aaT = CTouched(6), shT = CTouched(5);
        if (CTouched(7))
        {
#pragma warning disable 618
            if (QualitySettings.masterTextureLimit != GetChoice(7)) QualitySettings.masterTextureLimit = GetChoice(7);
#pragma warning restore 618
        }
        if (PlayerPrefs.HasKey("rg_vids_0") && asset != null) { float rs = RenderScale; if (Mathf.Abs(asset.renderScale - rs) > 0.001f) asset.renderScale = rs; }
        int aa = GetChoice(6);
        if (aaT && asset != null) { int ms = aa == 4 ? 4 : (aa == 3 ? 2 : 1); if (asset.msaaSampleCount != ms) asset.msaaSampleCount = ms; }
        float sd = ShadowDist[GetChoice(5)];
        if (shT)
        {
            if (asset != null && Mathf.Abs(asset.shadowDistance - sd) > 0.5f) asset.shadowDistance = sd;
            if (Mathf.Abs(QualitySettings.shadowDistance - sd) > 0.5f) QualitySettings.shadowDistance = sd;
        }
        if (!aaT && !shT) return;
        foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam == null || cam.targetTexture != null) continue;
            var d = cam.GetComponent<UniversalAdditionalCameraData>();
            if (d == null || d.renderType == CameraRenderType.Overlay) continue;
            if (aaT) d.antialiasing = aa == 1 ? AntialiasingMode.FastApproximateAntialiasing : (aa == 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None);
            if (shT) d.renderShadows = sd > 0.5f;
        }
    }

    /// <summary>Called once at start-up: restores the saved resolution / window mode.</summary>
    public static void ApplyOnBoot() { ApplyDisplay(); ApplyAll(); }

    static readonly Dictionary<string, float> origAsset = new Dictionary<string, float>();
    /// <summary>The URP asset is a project file: put its values back when the game stops in the editor.</summary>
    public static void RememberAssetValues()
    {
        var a = Urp; if (a == null || origAsset.Count > 0) return;
        origAsset["rs"] = a.renderScale; origAsset["sd"] = a.shadowDistance; origAsset["ms"] = a.msaaSampleCount;
    }
    public static void RestoreAssetValues()
    {
        var a = Urp; if (a == null || origAsset.Count == 0) return;
        a.renderScale = origAsset["rs"]; a.shadowDistance = origAsset["sd"]; a.msaaSampleCount = (int)origAsset["ms"];
    }
}
