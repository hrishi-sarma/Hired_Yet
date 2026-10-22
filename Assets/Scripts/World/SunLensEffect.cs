using UnityEngine;
using UnityEngine.UI;

public class SunLensEffect : MonoBehaviour
{
    public float dirtStrength = 0.4f;
    public float flareStrength = 1.6f;
    public float dayBoost = 1.8f;      // extra punch in daylight, where the bright sky washes out a weak flare
    public float occlusionDistance = 600f;

    Camera cam;
    Light sun;
    Image dirt;
    readonly Image[] elements = new Image[7];
    readonly float[] positions = { 1f, 0.55f, 0.25f, -0.25f, -0.55f, -0.9f, 1f };
    readonly float[] sizes = { 340f, 70f, 140f, 55f, 100f, 190f, 700f };
    readonly Color[] colors =
    {
        new Color(1f, 0.88f, 0.62f), new Color(0.6f, 0.8f, 1f), new Color(1f, 0.72f, 0.5f),
        new Color(0.7f, 1f, 0.8f), new Color(1f, 0.8f, 0.6f), new Color(0.65f, 0.75f, 1f), new Color(1f, 0.92f, 0.75f)
    };
    float visibility;
    float nextFind;
    Material additive;
    bool isAdditive;
    Image veil;
    readonly RaycastHit[] hits = new RaycastHit[12];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<SunLensEffect>() != null) return;
        new GameObject("SunLensEffect").AddComponent<SunLensEffect>();
    }

    void Start()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -5;

        string[] addShaders = { "Legacy Shaders/Particles/Additive", "Mobile/Particles/Additive", "Particles/Additive", "Sprites/Default" };
        for (int i = 0; i < addShaders.Length && additive == null; i++)
        {
            Shader add = Shader.Find(addShaders[i]);
            if (add == null) continue;
            additive = new Material(add);
            isAdditive = i < 3;
            if (additive.HasProperty("_TintColor")) additive.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }

        veil = MakeImage("SunVeil", BuildGlow(false), new Color(1f, 0.93f, 0.75f));
        RectTransform vr = veil.rectTransform;
        vr.anchorMin = vr.anchorMax = new Vector2(0.5f, 0.5f);
        vr.sizeDelta = new Vector2(1800f, 1800f);
        veil.material = additive;
        dirt = MakeImage("LensDirt", BuildDirt(), Color.white);
        RectTransform dr = dirt.rectTransform;
        dr.anchorMin = Vector2.zero;
        dr.anchorMax = Vector2.one;
        dr.offsetMin = Vector2.zero;
        dr.offsetMax = Vector2.zero;

        Sprite glow = BuildGlow(false);
        Sprite ring = BuildGlow(true);
        Sprite streak = BuildStreak();
        for (int i = 0; i < elements.Length; i++)
        {
            Sprite sp = i == 0 || i == 3 ? glow : (i == 6 ? streak : (i % 2 == 1 ? ring : glow));
            elements[i] = MakeImage("Flare" + i, sp, colors[i]);
            elements[i].rectTransform.sizeDelta = i == 6 ? new Vector2(sizes[i], 36f) : new Vector2(sizes[i], sizes[i]);
            elements[i].material = additive;
        }
        dirt.material = additive;
    }

    Image MakeImage(string n, Sprite sp, Color c)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(transform, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sp;
        img.color = new Color(c.r, c.g, c.b, 0f);
        img.raycastTarget = false;
        return img;
    }

    static Sprite ToSprite(Texture2D t)
    {
        t.Apply(true, false);
        t.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite BuildGlow(bool ring)
    {
        int s = 128;
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, true, false);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float r = Mathf.Sqrt(Mathf.Pow(x / (s - 1f) - 0.5f, 2f) + Mathf.Pow(y / (s - 1f) - 0.5f, 2f)) * 2f;
            float a;
            if (ring) a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.72f) / 0.22f) * 0.55f + Mathf.Clamp01(1f - r) * 0.12f;
            else a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
            if (r > 1f) a = 0f;
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        return ToSprite(t);
    }

    static Sprite BuildStreak()
    {
        int w = 256, h = 16;
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = Mathf.Abs(x / (w - 1f) - 0.5f) * 2f;
            float v = Mathf.Abs(y / (h - 1f) - 0.5f) * 2f;
            float a = Mathf.Pow(1f - u, 2.5f) * Mathf.Pow(1f - v, 3f);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a * 0.7f));
        }
        return ToSprite(t);
    }

    static Sprite BuildDirt()
    {
        int s = 256;
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, true, false);
        float[] a = new float[s * s];
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float n = Mathf.PerlinNoise(x * 0.012f + 4f, y * 0.012f + 9f) * 0.6f + Mathf.PerlinNoise(x * 0.04f, y * 0.04f + 20f) * 0.4f;
            a[y * s + x] = Mathf.Clamp01((n - 0.55f) * 1.6f) * 0.55f;
        }
        System.Random rng = new System.Random(1234);
        for (int k = 0; k < 170; k++)
        {
            float cx = (float)rng.NextDouble() * s, cy = (float)rng.NextDouble() * s;
            float rad = 1.5f + (float)rng.NextDouble() * (k < 20 ? 14f : 5f);
            float amp = 0.35f + (float)rng.NextDouble() * 0.6f;
            int x0 = Mathf.Max(0, (int)(cx - rad)), x1 = Mathf.Min(s - 1, (int)(cx + rad));
            int y0 = Mathf.Max(0, (int)(cy - rad)), y1 = Mathf.Min(s - 1, (int)(cy + rad));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / rad;
                if (d < 1f) a[y * s + x] = Mathf.Max(a[y * s + x], amp * (1f - d * d));
            }
        }
        for (int i = 0; i < a.Length; i++) t.SetPixel(i % s, i / s, new Color(1f, 1f, 1f, Mathf.Clamp01(a[i])));
        return ToSprite(t);
    }

    Light FindSun()
    {
        StylizedHDRISkyController hdri = FindFirstObjectByType<StylizedHDRISkyController>();
        if (hdri != null && hdri.sunLight != null && hdri.sunLight.enabled && hdri.sunLight.intensity > 0.02f) return hdri.sunLight;
        AnimeSkyController anime = FindFirstObjectByType<AnimeSkyController>();
        if (anime != null && anime.sunLight != null && anime.sunLight.enabled && anime.sunLight.intensity > 0.02f) return anime.sunLight;
        if (RenderSettings.sun != null && RenderSettings.sun.enabled && RenderSettings.sun.intensity > 0.02f) return RenderSettings.sun;
        Light best = null;
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light l = lights[i];
            if (l == null || l.type != LightType.Directional || !l.enabled || !l.gameObject.activeInHierarchy || l.intensity < 0.05f) continue;
            if (best == null || l.intensity > best.intensity) best = l;
        }
        return best;
    }

    /// <summary>True only if a real, reasonably small collider sits between the camera and the sun
    /// (sky domes, world-boundary shells, the player and triggers never block it).</summary>
    bool SunBlocked(Vector3 toSun)
    {
        Vector3 o = cam.transform.position;
        int n = Physics.RaycastNonAlloc(o, toSun, hits, occlusionDistance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (c == null) continue;
            if (c.transform.root == cam.transform.root) continue;                       // the player
            if (c.bounds.size.magnitude > 900f) continue;                               // sky dome / giant shell
            if (c is TerrainCollider && hits[i].distance > 400f) continue;
            return true;
        }
        return false;
    }

    Vector2 canvasSizeCache() { return new Vector2(Screen.width, Screen.height); }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    void LateUpdate()
    {
        if (dirt == null) return;
        if (cam == null) cam = Camera.main;
        if ((sun == null || !sun.enabled || sun.intensity < 0.02f || Time.unscaledTime >= nextFind + 4f) && Time.unscaledTime >= nextFind)
        {
            nextFind = Time.unscaledTime + 0.5f;
            sun = FindSun();
        }

        float target = 0f;
        Vector2 screen = Vector2.zero;
        float edgeFade = 0f;
        float facing = 0f;
        if (cam != null && sun != null && sun.enabled && sun.gameObject.activeInHierarchy && sun.intensity > 0.02f)
        {
            Vector3 toSun = -sun.transform.forward;
            float horizon = Smooth(0.02f, 0.18f, toSun.y);
            Vector3 vp = cam.WorldToViewportPoint(cam.transform.position + toSun * 1000f);
            if (vp.z > 0f && horizon > 0f)
            {
                screen = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                edgeFade = 1f - Smooth(0.65f, 1.15f, Mathf.Max(Mathf.Abs(screen.x), Mathf.Abs(screen.y)));
                facing = Smooth(0.25f, 0.8f, Vector3.Dot(cam.transform.forward, toSun));
                bool blocked = SunBlocked(toSun);
                target = blocked ? 0f : horizon;
            }
        }
        visibility = Mathf.MoveTowards(visibility, target, Time.unscaledDeltaTime * 2.5f);
        float day = sun != null ? Mathf.Clamp01(sun.intensity / 0.8f) : 0f;
        float boost = Mathf.Lerp(1f, dayBoost, day) * (isAdditive ? 1f : 1.25f);
        if (veil != null)
        {
            Color vc = veil.color;
            vc.a = visibility * edgeFade * facing * day * 0.35f;
            veil.color = vc;
            veil.rectTransform.anchoredPosition = new Vector2(screen.x * canvasSizeCache().x, screen.y * canvasSizeCache().y);
        }

        Color dc = dirt.color;
        dc.a = visibility * facing * dirtStrength;
        dirt.color = dc;

        Vector2 canvasSize = new Vector2(Screen.width, Screen.height);
        float centerDistance = Mathf.Clamp01(screen.magnitude * 1.6f);
        for (int i = 0; i < elements.Length; i++)
        {
            Image img = elements[i];
            Vector2 p = new Vector2(screen.x * positions[i] * canvasSize.x, screen.y * positions[i] * canvasSize.y);
            img.rectTransform.anchoredPosition = p + canvasSize * 0.5f - canvasSize * 0.5f;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            float amp = i == 0 || i == 6 ? 1f : Mathf.Lerp(0.25f, 0.8f, centerDistance);
            Color c = colors[i];
            c.a = Mathf.Clamp01(visibility * edgeFade * flareStrength * boost * amp * (i == 0 ? 0.55f : (i == 6 ? 0.4f : 0.3f)));
            img.color = c;
            if (i == 6) img.rectTransform.localRotation = Quaternion.identity;
        }
    }
}
