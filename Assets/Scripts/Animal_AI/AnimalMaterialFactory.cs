using System.Collections.Generic;
using UnityEngine;

/// <summary>Small procedural coat textures for primitive-built animals; no image assets required.</summary>
public static class AnimalMaterialFactory
{
    static readonly Dictionary<int, Material> cache = new Dictionary<int, Material>();
    static readonly Dictionary<int, Texture2D> textureCache = new Dictionary<int, Texture2D>();

    public static Material Fur(Color baseColor, int pattern = 0)
    {
        int key = ColorKey(baseColor, pattern, true);
        Material cached;
        if (cache.TryGetValue(key, out cached) && cached != null) return cached;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        Material mat = new Material(shader);
        mat.name = "Animal Fur " + key;
        Texture2D tex = CoatTexture(baseColor, pattern, key);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.18f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.18f);
        cache[key] = mat;
        return mat;
    }

    public static Material Flat(Color color, float smoothness = 0.25f)
    {
        int key = ColorKey(color, Mathf.RoundToInt(smoothness * 100f), false);
        Material cached;
        if (cache.TryGetValue(key, out cached) && cached != null) return cached;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        Material mat = new Material(shader);
        mat.name = "Animal Accent " + key;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        cache[key] = mat;
        return mat;
    }

    static Texture2D CoatTexture(Color baseColor, int pattern, int key)
    {
        Texture2D tex;
        if (textureCache.TryGetValue(key, out tex) && tex != null) return tex;
        const int size = 64;
        tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.name = "Procedural Animal Coat " + key;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        int seed = key ^ 0x2F6E2B1;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Hash01(x, y, seed);
                float broad = Mathf.PerlinNoise((x + (seed & 31)) * 0.075f, (y + ((seed >> 5) & 31)) * 0.075f);
                float streak = Mathf.PerlinNoise(x * 0.17f + pattern * 2.3f, y * 0.045f + pattern * 4.1f);
                float value = 0.91f + n * 0.09f + (broad - 0.5f) * 0.11f + (streak - 0.5f) * 0.035f;
                if (pattern == 2)
                {
                    float bands = Mathf.Sin((x * 0.43f) + Mathf.Sin(y * 0.12f) * 1.8f);
                    value *= 1f + Mathf.Max(0f, bands) * 0.07f;
                }
                value = Mathf.Clamp(value, 0.78f, 1.16f);
                pixels[y * size + x] = new Color(
                    Mathf.Clamp01(baseColor.r * value),
                    Mathf.Clamp01(baseColor.g * value),
                    Mathf.Clamp01(baseColor.b * value), 1f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(true, true);
        textureCache[key] = tex;
        return tex;
    }

    static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x00FFFFFF) / 16777215f;
        }
    }

    static int ColorKey(Color c, int variant, bool fur)
    {
        unchecked
        {
            int r = Mathf.RoundToInt(c.r * 255f);
            int g = Mathf.RoundToInt(c.g * 255f);
            int b = Mathf.RoundToInt(c.b * 255f);
            int a = Mathf.RoundToInt(c.a * 255f);
            int h = r | (g << 8) | (b << 16) | (a << 24);
            h = h * 397 ^ variant;
            h = h * 397 ^ (fur ? 1 : 0);
            return h;
        }
    }
}
