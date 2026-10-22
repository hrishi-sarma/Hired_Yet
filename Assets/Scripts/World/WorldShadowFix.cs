using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

// Makes sure the generated world actually has shadows after the resume scene -> world switch.
//  1) URP asset / quality: main-light shadows on, soft, 2 cascades, ~110 m distance (only raised, never lowered).
//  2) The day/night sun is the shadow-casting main light (soft, bias set, RenderSettings.sun assigned).
//  3) Every solid (opaque / alpha-test) mesh casts + receives. Skipped on purpose: transparent stuff (mist, fog, water, glass),
//     particles, tiny details (< 30 cm), flat decals (they only receive), and the player's own body.
// Cost control: time-sliced (4 ms per frame), each renderer is processed once, and the shadow distance + 2 cascades keep
// the shadow pass cheap (distant casters are culled by the shadow distance).
public static class WorldShadowFix
{
    const float ShadowDistance = 110f;
    const float MinExtent = 0.3f;       // smaller than this: does not cast (invisible shadow, wasted caster)
    const float FlatHeight = 0.08f;     // road markings / decals: receive only
    const float SunStrength = 0.78f;

    static readonly string[] SkipNames =
        { "mist", "fog", "cloud", "water", "pond", "glow", "decal", "puddle", "flare", "godray", "sunray", "particle", "sky", "volume" };
    static readonly HashSet<int> done = new HashSet<int>();
    static bool warned;
    public static int ApplyCount;       // how many full passes have started (lets WorldGenerator tell whether a bootstrap already did it)

    // Full pass, run by WorldBootstrap just before the loading screen finishes.
    public static IEnumerator Apply(System.Action<float> progress = null)
    {
        ApplyCount++;
        done.Clear();
        SetupPipeline();
        SetupSun();
        SetupCameras();
        yield return Scan(progress);
    }

    // Synchronous full pass for EDIT MODE (generating / baking the world in the Scene view, where no bootstrap runs).
    public static void ApplyNow()
    {
        ApplyCount++;
        done.Clear();
        SetupPipeline();
        SetupSun();
        SetupCameras();
        Renderer[] rs = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Transform player = WorldLauncher.Player;
        for (int i = 0; i < rs.Length; i++)
            if (rs[i] != null && done.Add(rs[i].GetInstanceID())) Process(rs[i], player);
        Terrain[] ts = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        for (int i = 0; i < ts.Length; i++)
            if (ts[i] != null) ts[i].shadowCastingMode = ShadowCastingMode.On;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var urp = GraphicsSettings.currentRenderPipeline;
            if (urp != null) UnityEditor.EditorUtility.SetDirty(urp);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }
#endif
    }

    // Light follow-up for things that spawn after generation (NPCs, event props). Only touches renderers not seen before.
    public static IEnumerator Followup()
    {
        int tick = 0;
        while (true)
        {
            yield return new WaitForSecondsRealtime(3f);
            SetupPipeline();   // re-assert: scene switches / other scripts can reset these
            SetupSun();
            SetupCameras();
            if ((++tick & 1) == 0) yield return Scan(null);
        }
    }

    // ------------------------------------------------------------------ pipeline
    static void SetupPipeline()
    {
        QualitySettings.shadows = UnityEngine.ShadowQuality.All;
        QualitySettings.shadowDistance = ShadowDistance;   // built-in pipeline / editor; URP reads its asset (below)
        QualitySettings.shadowCascades = 2;

        UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;
        // Several of these setters are internal in URP, so the private fields are written (guarded: failure just means "left as is").
        // Main Light = Disabled in the URP asset means the sun gives NO light and NO shadows at all.
        if (urp.mainLightRenderingMode != LightRenderingMode.PerPixel) SetEnumField(urp, "m_MainLightRenderingMode", (int)LightRenderingMode.PerPixel);
        if (!urp.supportsMainLightShadows) SetField(urp, "m_MainLightShadowsSupported", true);
        if (!urp.supportsSoftShadows) SetField(urp, "m_SoftShadowsSupported", true);
        if (urp.shadowDistance < ShadowDistance) SetField(urp, "m_ShadowDistance", ShadowDistance);
        if (urp.shadowCascadeCount < 2) SetField(urp, "m_ShadowCascadeCount", 2);
        if (urp.mainLightShadowmapResolution < 1024) SetEnumField(urp, "m_MainLightShadowmapResolution", 2048);
    }

    static void SetField(object o, string name, object value)
    {
        try
        {
            FieldInfo f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null) f.SetValue(o, value);
            else Warn("field not found: " + name);
        }
        catch (System.Exception e) { Warn(e.Message); }
    }

    static void SetEnumField(object o, string name, int value)
    {
        try
        {
            FieldInfo f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null) f.SetValue(o, System.Enum.ToObject(f.FieldType, value));
            else Warn("field not found: " + name);
        }
        catch (System.Exception e) { Warn(e.Message); }
    }

    static void Warn(string m)
    {
        if (warned) return;
        warned = true;
        Debug.LogWarning("WorldShadowFix: could not adjust the URP asset (" + m + "). Check Main Light > Cast Shadows and Shadows > Max Distance in the URP asset.");
    }

    // ------------------------------------------------------------------ sun
    static void SetupSun()
    {
        Light sun = null;
        Light[] ls = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < ls.Length; i++)
        {
            Light l = ls[i];
            if (l == null || !l.enabled || l.type != LightType.Directional || l.intensity <= 0.02f) continue;
            if (sun == null || l.intensity > sun.intensity) sun = l;
        }
        if (sun == null) sun = RenderSettings.sun;
        if (sun == null) return;
        sun.enabled = true;
        if (sun.intensity < 0.05f && sun.transform.forward.y < -0.25f) sun.intensity = 1.1f;   // sun is up but dark
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = SunStrength;
        sun.shadowBias = 0.045f;
        sun.shadowNormalBias = 0.32f;
        sun.shadowNearPlane = 0.2f;
        sun.shadowResolution = LightShadowResolution.FromQualitySettings;
        sun.renderMode = LightRenderMode.ForcePixel;
        sun.cullingMask = ~0;
        if (RenderSettings.sun != sun) RenderSettings.sun = sun;
    }

    // A scene-placed sun set to Baked/Mixed gives no realtime light or shadows (the script-made moon is realtime, which is why night worked).
    static void EnsureRealtimeSun()
    {
        AnimeSkyController a = Object.FindFirstObjectByType<AnimeSkyController>();
        if (a != null && NeedsReplace(a.sunLight)) a.sunLight = MakeRealtime(a.sunLight, a.transform);
        StylizedHDRISkyController h = Object.FindFirstObjectByType<StylizedHDRISkyController>();
        if (h != null && NeedsReplace(h.sunLight)) h.sunLight = MakeRealtime(h.sunLight, h.transform);
    }

    static bool NeedsReplace(Light l)
    {
        return l != null && l.type == LightType.Directional && l.bakingOutput.lightmapBakeType != LightmapBakeType.Realtime;
    }

    static Light MakeRealtime(Light old, Transform parent)
    {
        GameObject go = new GameObject("Realtime Sun (Auto)");
        go.transform.SetParent(parent, false);
        go.transform.rotation = old.transform.rotation;
        Light n = go.AddComponent<Light>();
        n.type = LightType.Directional;
        n.color = old.color;
        n.intensity = Mathf.Max(old.intensity, 0.1f);
        n.shadows = LightShadows.Soft;
        old.enabled = false;
        return n;
    }

    // URP: a camera with "Render Shadows" off draws no shadows at all.
    static void SetupCameras()
    {
        Camera[] cs = Camera.allCameras;
        for (int i = 0; i < cs.Length; i++)
        {
            if (cs[i] == null || cs[i].targetTexture != null) continue;
            var d = cs[i].GetComponent<UniversalAdditionalCameraData>();
            if (d != null && !d.renderShadows) d.renderShadows = true;
        }
    }

    // ------------------------------------------------------------------ renderers
    static IEnumerator Scan(System.Action<float> progress)
    {
        Renderer[] rs = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Transform player = WorldLauncher.Player;
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < rs.Length; i++)
        {
            Renderer r = rs[i];
            if (r != null && done.Add(r.GetInstanceID())) Process(r, player);
            if (sw.ElapsedMilliseconds >= 4)
            {
                if (progress != null) progress(i / (float)rs.Length);
                yield return null;
                sw.Restart();
            }
        }
        Terrain[] ts = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        for (int i = 0; i < ts.Length; i++)
            if (ts[i] != null) ts[i].shadowCastingMode = ShadowCastingMode.On;
        if (progress != null) progress(1f);
    }

    static void Process(Renderer r, Transform player)
    {
        if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) return;     // particles, trails, lines: no
        if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) return;
        Material m = r.sharedMaterial;
        if (m == null || m.renderQueue >= 3000) return;                       // transparent: mist, fog, water, glass
        if (player != null && r.transform.IsChildOf(player)) return;          // first-person body
        string n = r.name.ToLowerInvariant();
        for (int i = 0; i < SkipNames.Length; i++) if (n.Contains(SkipNames[i])) return;
        Transform p = r.transform.parent;
        if (p != null)
        {
            string pn = p.name.ToLowerInvariant();
            for (int i = 0; i < SkipNames.Length; i++) if (pn.Contains(SkipNames[i])) return;
        }

        r.receiveShadows = true;
        if (r.shadowCastingMode != ShadowCastingMode.Off) return;             // already casting (On / TwoSided)
        Vector3 s = r.bounds.size;
        if (s.y < FlatHeight) return;                                          // flat decals only receive
        if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) < MinExtent && !n.StartsWith("item_")) return;
        r.shadowCastingMode = ShadowCastingMode.On;
    }
}
