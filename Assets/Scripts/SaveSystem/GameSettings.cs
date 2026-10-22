using UnityEngine;

/// <summary>
/// One place for every player setting (saved in PlayerPrefs, shared by the main-menu SETTINGS card and the pause menu).
/// Audio: a master volume plus one volume per kind of sound. Graphics: ON/OFF + intensity for lens flare and the post-processing effects.
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
        v = Mathf.Clamp01(v); PlayerPrefs.SetFloat(AKey(i), v);
        if (i == 0) AudioListener.volume = v;   // PauseAudio keeps it in sync during pause fades
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

    // ------------------------------------------------------------------ graphics
    public static readonly string[] GfxNames = { "LENS FLARE", "DEPTH OF FIELD", "FILM GRAIN", "CHROMATIC", "OUTLINES" };
    static readonly string[] GKeys = { "rg_lensflare", "rg_dof", "rg_grain", "rg_chroma", "rg_outline" };
    static readonly bool[] GDef = { true, false, false, false, false };
    static float flareBase = -1f, dayFlareBase = -1f;

    public static bool GetOn(int i) { return PlayerPrefs.GetInt(GKeys[i], GDef[i] ? 1 : 0) == 1; }
    public static float GetAmt(int i) { return PlayerPrefs.GetFloat(GKeys[i] + "_amt", 0.5f); }
    public static void SetOn(int i, bool on) { PlayerPrefs.SetInt(GKeys[i], on ? 1 : 0); Apply(i); }
    public static void SetAmt(int i, float a) { PlayerPrefs.SetFloat(GKeys[i] + "_amt", Mathf.Clamp01(a)); Apply(i); }
    static bool Touched(int i) { return PlayerPrefs.HasKey(GKeys[i]) || PlayerPrefs.HasKey(GKeys[i] + "_amt"); }

    /// <summary>Re-applies everything the player has changed (also called every second, because scenes create their own effect objects).</summary>
    public static void ApplyAll() { for (int i = 0; i < GfxNames.Length; i++) if (Touched(i)) Apply(i); }

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
        var fx = CinematicEffects.Instance; if (fx == null) return;
        switch (i)
        {
            case 1:   // depth of field: stronger = starts closer, blurs more
                fx.depthOfField = on;
                fx.dofStart = Mathf.Lerp(180f, 15f, a); fx.dofEnd = fx.dofStart + Mathf.Lerp(320f, 90f, a); fx.dofMaxRadius = Mathf.Lerp(0.7f, 4.5f, a);
                break;
            case 2: fx.filmGrain = on; fx.grainIntensity = a; break;
            case 3: fx.chromaticAberration = on; fx.chromaticIntensity = a; break;
            default:
                fx.outlines = on; fx.outlineOpacity = Mathf.Lerp(0.2f, 1f, a);
                fx.buildingWidth = Mathf.Lerp(0.012f, 0.07f, a); fx.characterWidth = Mathf.Lerp(0.008f, 0.045f, a);
                break;
        }
    }
}
