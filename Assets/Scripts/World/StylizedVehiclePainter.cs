using System.Collections.Generic;
using UnityEngine;

public class StylizedVehiclePainter : MonoBehaviour
{
    enum Part { None, Body, Bed, Bumper, Grille, Headlights, TailLights, Rims, Tires, Glass }

    static readonly Color[] bodyColors =
    {
        new Color(0.88f, 0.27f, 0.23f), new Color(0.25f, 0.48f, 0.85f), new Color(0.95f, 0.76f, 0.19f),
        new Color(0.94f, 0.94f, 0.92f), new Color(0.16f, 0.17f, 0.20f), new Color(0.62f, 0.65f, 0.69f),
        new Color(0.95f, 0.52f, 0.18f), new Color(0.22f, 0.62f, 0.45f), new Color(0.12f, 0.22f, 0.44f),
        new Color(0.50f, 0.15f, 0.22f), new Color(0.55f, 0.82f, 0.76f), new Color(0.78f, 0.80f, 0.84f)
    };

    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    static readonly List<Material> headMats = new List<Material>();
    static readonly List<Material> tailMats = new List<Material>();
    static float currentNight;

    float nextScan;

    public static float CurrentNight { get { return currentNight; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<StylizedVehiclePainter>() != null) return;
        GameObject go = new GameObject("StylizedVehiclePainter");
        go.AddComponent<StylizedVehiclePainter>();
        DontDestroyOnLoad(go); // survive scene switches (resume world -> open world)
    }

    void OnEnable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        nextScan = 0f; // repaint new scene's vehicles right away
    }

    void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 2f;
        Renderer[] all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++) Paint(all[i]);
    }

    public static void ApplyNight(float night)
    {
        currentNight = Mathf.Clamp01(night);
        for (int i = 0; i < headMats.Count; i++)
            if (headMats[i] != null) headMats[i].SetColor("_EmissionColor", new Color(1f, 0.92f, 0.72f) * (3.2f * currentNight));
        for (int i = 0; i < tailMats.Count; i++)
            if (tailMats[i] != null) tailMats[i].SetColor("_EmissionColor", new Color(1f, 0.08f, 0.05f) * (2.4f * currentNight));
    }

    static Part Classify(string raw)
    {
        string n = raw.ToLowerInvariant().Replace(" ", "_").Replace("-", "_").Replace(".", "_");
        string[] t = n.Split('_');
        bool Has(string s) { for (int i = 0; i < t.Length; i++) if (t[i] == s) return true; return false; }
        if (Has("tail") || Has("taillights") || Has("taillight") || Has("brake")) return Part.TailLights;
        if (Has("headlights") || Has("headlight") || (Has("head") && (Has("lights") || Has("light")))) return Part.Headlights;
        if (Has("rims") || Has("rim") || Has("hubcap") || Has("hubcaps")) return Part.Rims;
        if (Has("tires") || Has("tire") || Has("tyres") || Has("tyre")) return Part.Tires;
        if (Has("bumper") || Has("bumpers")) return Part.Bumper;
        if (Has("grille") || Has("grill")) return Part.Grille;
        if (Has("bed")) return Part.Bed;
        if (Has("chassis") || Has("body") || Has("cab") || Has("hood")) return Part.Body;
        if (Has("glass") || Has("window") || Has("windows") || Has("windshield")) return Part.Glass;
        return Part.None;
    }

    static void Paint(Renderer r)
    {
        if (r == null) return;
        Material current = r.sharedMaterial;
        if (current != null && (current.name.StartsWith("Veh_") || current.name.StartsWith("Stylized_house_"))) return;
        Part part = Classify(r.transform.name);
        if (part == Part.None) return;
        Transform top = r.transform;
        while (top.parent != null && Classify(top.parent.name) != Part.None) top = top.parent;
        Transform vehicle = top.parent != null ? top.parent : top;
        bool hasSibling = false;
        for (int i = 0; i < vehicle.childCount && !hasSibling; i++)
        {
            Transform s = vehicle.GetChild(i);
            if (s != top && Classify(s.name) != Part.None) hasSibling = true;
        }
        if (!hasSibling) return;
        int colorIndex = Mathf.Abs(vehicle.GetInstanceID()) % bodyColors.Length;
        Material m = GetMaterial(part, colorIndex);
        Material[] mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++) mats[i] = m;
        r.sharedMaterials = mats;
        if (part == Part.Headlights || part == Part.TailLights)
        {
            VehicleLights lights = vehicle.GetComponent<VehicleLights>();
            if (lights == null) lights = vehicle.gameObject.AddComponent<VehicleLights>();
            lights.Add(r, part == Part.Headlights);
        }
    }

    static Material GetMaterial(Part part, int colorIndex)
    {
        bool tinted = part == Part.Body || part == Part.Bed;
        string key = part + (tinted ? "_" + colorIndex : "");
        Material m;
        if (materials.TryGetValue(key, out m) && m != null)
        {
            // Re-attach texture if it was unloaded during a scene change.
            if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") == null) { Texture2D rt = GetTexture(part); m.SetTexture("_BaseMap", rt); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", rt); }
            return m;
        }

        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh);
        m.name = "Veh_" + key;
        Color tint = Color.white;
        float metallic = 0.1f, smooth = 0.4f;
        switch (part)
        {
            case Part.Body: case Part.Bed: tint = bodyColors[colorIndex]; metallic = 0.35f; smooth = 0.68f; break;
            case Part.Bumper: tint = new Color(0.78f, 0.80f, 0.83f); metallic = 0.85f; smooth = 0.7f; break;
            case Part.Grille: tint = new Color(0.13f, 0.14f, 0.16f); metallic = 0.5f; smooth = 0.45f; break;
            case Part.Rims: tint = new Color(0.82f, 0.84f, 0.87f); metallic = 0.9f; smooth = 0.75f; break;
            case Part.Tires: tint = new Color(0.10f, 0.10f, 0.11f); metallic = 0f; smooth = 0.12f; break;
            case Part.Headlights: tint = new Color(0.95f, 0.96f, 0.98f); metallic = 0.1f; smooth = 0.92f; break;
            case Part.TailLights: tint = new Color(0.85f, 0.10f, 0.08f); metallic = 0.1f; smooth = 0.9f; break;
            case Part.Glass: tint = new Color(0.22f, 0.36f, 0.46f); metallic = 0.2f; smooth = 0.95f; break;
        }
        Texture2D tex = GetTexture(part);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (part == Part.Headlights || part == Part.TailLights)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", GetTexture(part == Part.Headlights ? Part.Glass : Part.TailLights));
            if (part == Part.Headlights) { headMats.Add(m); m.SetTexture("_EmissionMap", tex); }
            else tailMats.Add(m);
            ApplyNight(currentNight);
        }
        materials[key] = m;
        return m;
    }

    static Texture2D GetTexture(Part part)
    {
        string key = part == Part.Bed ? Part.Body.ToString() : part.ToString();
        Texture2D t;
        if (textures.TryGetValue(key, out t) && t != null) return t;
        int size = 128;
        t = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
        t.name = "Veh_" + key;
        t.wrapMode = TextureWrapMode.Repeat;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)size, v = y / (float)size;
            float noise = Mathf.PerlinNoise(u * 9f, v * 9f);
            float g = 1f;
            switch (part)
            {
                case Part.Body: case Part.Bed:
                {
                    float soft = Mathf.PerlinNoise(u * 1.5f + 3f, v * 1.5f + 8f);
                    float fine = Mathf.PerlinNoise(u * 40f, v * 40f);
                    g = 0.9f + 0.06f * soft + 0.02f * fine + (fine > 0.9f ? 0.04f : 0f);
                    break;
                }
                case Part.Bumper:
                case Part.Rims:
                {
                    float sheen = 0.65f + 0.3f * Mathf.Abs(Mathf.Sin(u * 6.28f * 1.5f + noise));
                    float brush = Mathf.PerlinNoise(u * 4f, v * 90f);
                    g = sheen * (0.88f + 0.12f * brush);
                    break;
                }
                case Part.Grille:
                {
                    float slat = Mathf.Abs(Mathf.Sin(v * 6.28f * 7f));
                    g = 0.35f + 0.65f * Mathf.SmoothStep(0.25f, 0.8f, slat);
                    break;
                }
                case Part.Tires:
                {
                    float tread = Mathf.Abs(Mathf.Sin(u * 6.28f * 10f + v * 6.28f * 2f));
                    g = 0.55f + 0.45f * Mathf.SmoothStep(0.3f, 0.7f, tread) * (0.8f + 0.2f * noise);
                    break;
                }
                case Part.Glass:
                {
                    g = 0.82f + 0.12f * Mathf.PerlinNoise(u * 1.2f + 5f, v * 1.2f + 2f);
                    break;
                }
                case Part.Headlights:
                {
                    float facet = Mathf.Abs(Mathf.Sin(u * 6.28f * 4f) * Mathf.Sin(v * 6.28f * 4f));
                    float rad = 1f - Mathf.Clamp01(Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 1.6f);
                    g = 0.55f + 0.3f * facet + 0.15f * rad;
                    break;
                }
                case Part.TailLights:
                {
                    float rib = Mathf.Abs(Mathf.Sin(v * 6.28f * 6f));
                    g = 0.6f + 0.4f * Mathf.SmoothStep(0.2f, 0.9f, rib);
                    break;
                }
            }
            g = Mathf.Clamp01(g);
            px[y * size + x] = new Color(g, g, g, 1f);
        }
        t.SetPixels(px);
        t.Apply(true, false);
        textures[key] = t;
        return t;
    }
}
