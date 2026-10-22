using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Single authority for the DAY sun. Several scripts (Anime sky, HDRI sky, scene "Directional Light") used to
/// enable/disable each other's suns, so the day sun flickered on and off and cast no shadows.
/// Now: one realtime sun is created and driven here every frame from the active sky's time of day;
/// every other directional light except the moons is switched off in LateUpdate (after the sky scripts' Update).
/// </summary>
public sealed class DayNightLightGuard : MonoBehaviour
{
    const float SunShadowStrength = 0.78f;
    Light sun;
    Light[] dirs = new Light[0];
    float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<DayNightLightGuard>() != null) return;
        var go = new GameObject("DayNightLightGuard (Auto)");
        go.AddComponent<DayNightLightGuard>();
        DontDestroyOnLoad(go);
    }

    void OnEnable() { SceneManager.sceneLoaded += OnScene; }
    void OnDisable() { SceneManager.sceneLoaded -= OnScene; }
    void OnScene(Scene s, LoadSceneMode m) { nextScan = 0f; }

    public static bool IsDayNightLight(Light l)
    {
        if (l == null) return false;
        var hdri = FindFirstObjectByType<StylizedHDRISkyController>();
        if (hdri != null && (l == hdri.sunLight || l == hdri.moonLight)) return true;
        var anime = FindFirstObjectByType<AnimeSkyController>();
        if (anime != null && (l == anime.sunLight || l == anime.moonLight)) return true;
        return false;
    }

    AnimeSkyController anime;
    StylizedHDRISkyController hdri;

    void LateUpdate()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            anime = FindFirstObjectByType<AnimeSkyController>();
            hdri = FindFirstObjectByType<StylizedHDRISkyController>();
            dirs = FindObjectsByType<Light>(FindObjectsSortMode.None);
        }
        if (anime == null && hdri == null) return;   // no day/night system -> leave lights alone

        // Which sky is on screen? Prefer the one whose skybox is assigned; Anime wins ties.
        bool useAnime = anime != null && (hdri == null || (RenderSettings.skybox != null && RenderSettings.skybox.name.StartsWith("Anime")));

        float elevation, intensity; Vector3 sunDir; Color col;
        if (useAnime)
        {
            float ang = (anime.timeOfDay * 360f - 90f) * Mathf.Deg2Rad;
            sunDir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0.18f).normalized;
            elevation = sunDir.y;
            float dl = Mathf.Clamp01((elevation + 0.12f) / 0.45f);
            col = Color.Lerp(anime.dawnLightColor, anime.dayLightColor, dl);
            intensity = anime.daySunIntensity * dl;
        }
        else
        {
            float hour = hdri.CurrentHour;
            elevation = Mathf.Sin((hour - 6f) / 12f * Mathf.PI);
            float az = ((hour / 24f) * 360f + 90f) * Mathf.Deg2Rad;
            sunDir = new Vector3(Mathf.Cos(az), elevation, Mathf.Sin(az)).normalized;
            float dl = Mathf.Clamp01((elevation + 0.08f) / 0.42f);
            col = Color.Lerp(new Color(1f, 0.34f, 0.20f), new Color(1f, 0.94f, 0.80f), Mathf.Clamp01(elevation * 2.2f + 0.25f));
            intensity = (hdri.settings != null ? hdri.settings.middaySunIntensity : 1.25f) * dl;
        }

        if (sun == null)
        {
            var go = new GameObject("Day Sun (Auto)");
            DontDestroyOnLoad(go);
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = SunShadowStrength;
            sun.shadowBias = 0.045f;
            sun.shadowNormalBias = 0.32f;
            sun.shadowNearPlane = 0.2f;
            sun.renderMode = LightRenderMode.ForcePixel;
            sun.cullingMask = ~0;
        }
        sun.transform.rotation = Quaternion.LookRotation(-sunDir, Vector3.up);
        sun.color = col;
        sun.intensity = intensity;
        sun.enabled = intensity > 0.02f;
        if (sun.enabled && RenderSettings.sun != sun) RenderSettings.sun = sun;

        // Everything else directional (scene "Directional Light", the sky scripts' own suns) stays off; moons are left alone.
        for (int i = 0; i < dirs.Length; i++)
        {
            Light l = dirs[i];
            if (l == null || l == sun || l.type != LightType.Directional || !l.enabled) continue;
            if ((anime != null && l == anime.moonLight) || (hdri != null && l == hdri.moonLight)) continue;
            l.enabled = false;
        }
    }
}
