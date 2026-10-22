using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class ContactShadows
{
    static Material material;
    static Mesh quad;

    static Material GetMaterial()
    {
        if (material != null) return material;
        int s = 64;
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, true, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = Mathf.Abs(x / (s - 1f) - 0.5f) * 2f;
            float dy = Mathf.Abs(y / (s - 1f) - 0.5f) * 2f;
            float d = Mathf.Max(dx, dy);
            float round = Mathf.Sqrt(dx * dx + dy * dy) * 0.5f + d * 0.5f;
            float a = 1f - Mathf.SmoothStep(0.55f, 1f, round);
            t.SetPixel(x, y, new Color(0f, 0f, 0f, a));
        }
        t.Apply(true, false);
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        m.name = "ContactShadow";
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.02f, 0.03f, 0.04f, 0.4f));
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", 5f);
            m.SetFloat("_DstBlend", 10f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
        }
        m.renderQueue = 2990;
        material = m;
        return m;
    }

    public static void Add(Transform parent, Vector3 center, Vector2 size, float yawDeg)
    {
        if (quad == null)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad = q.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(q);
        }
        GameObject go = new GameObject("ContactShadow");
        go.transform.SetParent(parent, true);
        go.transform.position = center;
        go.transform.rotation = Quaternion.Euler(90f, yawDeg, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GetMaterial();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }
}

public class AtmosphereEffects : MonoBehaviour
{
    public float rayStrength = 0.22f;
    public float aerialDensityScale = 1.9f;
    public float dawnMistBoost = 1.6f;

    [Header("Color Grading")]
    public bool gradeEnabled = false;
    [Range(0f, 1f)] public float toningStrength = 0.6f;
    [Range(-30f, 30f)] public float toningBalance = 12f;
    public Color nightHighlightTint = new Color(0.72f, 0.82f, 1f);
    public Color nightShadowTint = new Color(0.50f, 0.62f, 0.82f);
    public Color dayShadowTint = new Color(0.40f, 0.52f, 0.78f);
    public Color goldenShadowTint = new Color(0.55f, 0.50f, 0.72f);
    public bool adjustColors = false;
    [Range(-50f, 50f)] public float dayContrastOffset = 0f;
    [Range(-50f, 50f)] public float daySaturationOffset = 0f;
    [Range(-1f, 1f)] public float dayExposureOffset = 0f;
    [Range(-50f, 50f)] public float nightContrastOffset = -10f;
    [Range(-50f, 50f)] public float nightSaturationOffset = -15f;
    [Range(-1f, 1f)] public float nightExposureOffset = 0.15f;
    [Range(-60f, 60f)] public float nightHueShift = 0f;
    public Color nightColorFilter = Color.white;

    AnimeSkyController anime;
    StylizedHDRISkyController hdri;
    Camera cam;
    Light sun;
    Volume volume;
    SplitToning split;
    ShadowsMidtonesHighlights smh;
    ColorAdjustments adj;
    StylizedWorldSetup setup;
    Image rays;
    float rayVisibility;
    float nextFind;
    float nextGrade;
    float baseDayDensity = -1f;
    float baseNightDensity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<AtmosphereEffects>() != null) return;
        new GameObject("AtmosphereEffects").AddComponent<AtmosphereEffects>();
    }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    void Start()
    {
        BuildGrade();
        BuildRays();
    }

    void BuildGrade()
    {
        GameObject go = new GameObject("ColorGradeVolume");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 101f;
        volume.weight = 1f;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.DontSave;
        split = profile.Add<SplitToning>(true);
        split.shadows.overrideState = true;
        split.highlights.overrideState = true;
        split.balance.overrideState = true;
        split.balance.value = 12f;
        smh = profile.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.overrideState = true;
        smh.shadows.value = new Vector4(0.94f, 0.97f, 1.04f, -0.04f);
        smh.midtones.overrideState = true;
        smh.midtones.value = new Vector4(1f, 1f, 1f, 0f);
        smh.highlights.overrideState = true;
        smh.highlights.value = new Vector4(1.04f, 1.01f, 0.95f, 0.02f);
        adj = profile.Add<ColorAdjustments>(true);
        adj.postExposure.overrideState = true;
        adj.contrast.overrideState = true;
        adj.saturation.overrideState = true;
        adj.hueShift.overrideState = true;
        adj.colorFilter.overrideState = true;
        volume.sharedProfile = profile;
    }

    void BuildRays()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -6;
        int s = 512;
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, true, false);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = x / (s - 1f) * 2f - 1f;
            float v = y / (s - 1f) * 2f - 1f;
            float r = Mathf.Sqrt(u * u + v * v);
            float ang = Mathf.Atan2(v, u);
            float ray = Mathf.PerlinNoise(Mathf.Cos(ang) * 4f + 7f, Mathf.Sin(ang) * 4f + 3f);
            ray = Mathf.Pow(Mathf.Clamp01((ray - 0.35f) * 2.2f), 1.6f);
            float fall = Mathf.Pow(Mathf.Clamp01(1f - r), 1.7f);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, ray * fall));
        }
        t.Apply(true, false);
        t.wrapMode = TextureWrapMode.Clamp;
        GameObject go = new GameObject("SunShafts");
        go.transform.SetParent(transform, false);
        rays = go.AddComponent<Image>();
        rays.sprite = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        rays.raycastTarget = false;
        rays.color = new Color(1f, 0.93f, 0.75f, 0f);
        Shader add = Shader.Find("Legacy Shaders/Particles/Additive");
        if (add != null)
        {
            Material m = new Material(add);
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            rays.material = m;
        }
        RectTransform rt = rays.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
    }

    Light FindSun()
    {
        AnimeSkyController a = FindFirstObjectByType<AnimeSkyController>();
        if (a != null && a.sunLight != null) return a.sunLight;
        if (RenderSettings.sun != null) return RenderSettings.sun;
        Light best = null;
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type != LightType.Directional) continue;
            if (best == null || lights[i].intensity > best.intensity) best = lights[i];
        }
        return best;
    }

    void Update()
    {
        if (cam == null) cam = Camera.main;
        if ((anime == null && hdri == null) || sun == null)
        {
            if (Time.unscaledTime >= nextFind)
            {
                nextFind = Time.unscaledTime + 1f;
                if (anime == null) anime = FindFirstObjectByType<AnimeSkyController>();
                if (anime == null && hdri == null) hdri = FindFirstObjectByType<StylizedHDRISkyController>();
                if (sun == null) sun = FindSun();
            }
        }
        if (sun == null) return;

        float elev = (-sun.transform.forward).y;
        UpdateRays(elev);
        UpdateFog(elev);
        if (Time.unscaledTime >= nextGrade)
        {
            nextGrade = Time.unscaledTime + 0.1f;
            UpdateGrade(elev);
        }
    }

    void UpdateRays(float elev)
    {
        float target = 0f;
        Vector2 screen = Vector2.zero;
        float facing = 0f;
        if (cam != null && sun.enabled && sun.gameObject.activeInHierarchy)
        {
            Vector3 toSun = -sun.transform.forward;
            float horizon = Smooth(0.02f, 0.15f, toSun.y);
            Vector3 vp = cam.WorldToViewportPoint(cam.transform.position + toSun * 1000f);
            if (vp.z > 0f && horizon > 0f)
            {
                screen = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                facing = Smooth(0.35f, 0.9f, Vector3.Dot(cam.transform.forward, toSun));
                bool blocked = Physics.Raycast(cam.transform.position, toSun, 600f, ~0, QueryTriggerInteraction.Ignore);
                target = blocked ? 0f : horizon;
            }
        }
        rayVisibility = Mathf.MoveTowards(rayVisibility, target, Time.unscaledDeltaTime * 2f);
        float lowSun = 1f + (1f - Mathf.Clamp01(elev * 2.5f)) * 0.9f;
        Color c = rays.color;
        c.a = Mathf.Clamp01(rayVisibility * facing * rayStrength * lowSun);
        rays.color = c;
        RectTransform rt = rays.rectTransform;
        float size = Mathf.Max(Screen.width, Screen.height) * 2.2f;
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(screen.x * Screen.width, screen.y * Screen.height);
        rt.localRotation = Quaternion.Euler(0f, 0f, Time.time * 1.2f);
    }

    void UpdateFog(float elev)
    {
        float dawn = 0f;
        if (anime != null)
        {
            if (baseDayDensity < 0f)
            {
                baseDayDensity = anime.fogDensityDay * aerialDensityScale;
                baseNightDensity = anime.fogDensityNight;
                anime.fogDay = Color.Lerp(anime.fogDay, new Color(0.66f, 0.79f, 0.93f), 0.5f);
            }
            float t = anime.timeOfDay;
            dawn = Smooth(0.17f, 0.25f, t) * (1f - Smooth(0.28f, 0.38f, t));
            anime.fogDensityDay = baseDayDensity * (1f + dawn * dawnMistBoost);
        }
        else if (hdri != null)
        {
            float h = hdri.CurrentHour;
            dawn = Smooth(4.2f, 6f, h) * (1f - Smooth(6.7f, 9f, h));
            float daylight = Smooth(-0.05f, 0.3f, elev);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Mathf.Lerp(0.0012f, 0.0013f * aerialDensityScale, daylight) * (1f + dawn * dawnMistBoost);
            Color dayCol = new Color(0.66f, 0.79f, 0.93f);
            Color goldCol = new Color(0.95f, 0.70f, 0.52f);
            Color nightCol = new Color(0.04f, 0.055f, 0.10f);
            float golden = 1f - Smooth(0.05f, 0.3f, elev);
            Color col = Color.Lerp(nightCol, Color.Lerp(dayCol, goldCol, golden * 0.8f), daylight);
            RenderSettings.fogColor = col;
        }
    }

    void UpdateGrade(float elev)
    {
        if (volume == null) return;
        volume.weight = gradeEnabled ? 1f : 0f;
        float day = Smooth(-0.05f, 0.3f, elev);
        float night = 1f - day;
        float golden = (1f - Smooth(0.08f, 0.38f, elev)) * day;
        Color warm = Color.Lerp(new Color(1f, 0.92f, 0.80f), new Color(1f, 0.68f, 0.42f), golden);
        Color hi = Color.Lerp(nightHighlightTint, warm, day);
        Color coolShadow = Color.Lerp(dayShadowTint, goldenShadowTint, golden);
        Color sh = Color.Lerp(nightShadowTint, coolShadow, day);
        split.highlights.value = Color.Lerp(Color.white, hi, toningStrength);
        split.shadows.value = Color.Lerp(Color.white, sh, toningStrength);
        split.balance.value = toningBalance;
        if (setup == null) setup = FindFirstObjectByType<StylizedWorldSetup>();
        float baseContrast = setup != null ? setup.contrast : 12f;
        float baseSaturation = setup != null ? setup.saturation : 8f;
        if (adjustColors)
        {
            adj.active = true;
            adj.postExposure.value = 0.05f + Mathf.Lerp(dayExposureOffset, nightExposureOffset, night);
            adj.contrast.value = baseContrast + Mathf.Lerp(dayContrastOffset, nightContrastOffset, night);
            adj.saturation.value = baseSaturation + Mathf.Lerp(daySaturationOffset, nightSaturationOffset, night);
            adj.hueShift.value = nightHueShift * night;
            adj.colorFilter.value = Color.Lerp(new Color(1f, 0.99f, 0.96f), nightColorFilter, night);
        }
        else adj.active = false;
    }
}
