using System.Collections.Generic;
using UnityEngine;

public static class StylizedMaterialFactory
{
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static bool warnedMissingShader;
    static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Texture2D> normalTextures = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Texture2D> heightTextures = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Texture2D> occlusionTextures = new Dictionary<string, Texture2D>();

    static readonly Color[][] palettes = new Color[][]
    {
        new Color[] { Hex("F0EEE7"), Hex("202329"), Hex("604333"), Hex("F3F0E8") },
        new Color[] { Hex("B9C8B0"), Hex("202329"), Hex("49352B"), Hex("F3F0E8") },
        new Color[] { Hex("D7D0C5"), Hex("202329"), Hex("4A3025"), Hex("F7F2E9") },
        new Color[] { Hex("C6D0D7"), Hex("202329"), Hex("604333"), Hex("F4F1E9") },
        new Color[] { Hex("C5BBA9"), Hex("202329"), Hex("343536"), Hex("F0EEE7") },
        new Color[] { Hex("D4DDD0"), Hex("202329"), Hex("654832"), Hex("F4F1E9") },
        new Color[] { Hex("E4D8C7"), Hex("202329"), Hex("604333"), Hex("F7F4ED") },
        new Color[] { Hex("AEBDAF"), Hex("202329"), Hex("4A382C"), Hex("F0EEE7") },
        new Color[] { Hex("F1F0E9"), Hex("34383D"), Hex("554034"), Hex("25282B") },
        new Color[] { Hex("B9C8B0"), Hex("292E2B"), Hex("604333"), Hex("F3F0E8") },
        new Color[] { Hex("C2D0D7"), Hex("596875"), Hex("5C4030"), Hex("F4F0E7") },
        new Color[] { Hex("B97861"), Hex("333538"), Hex("563B2E"), Hex("F0E6D8") },
        new Color[] { Hex("C8CBCD"), Hex("202329"), Hex("44352D"), Hex("202329") },
        new Color[] { Hex("D4C8B7"), Hex("4A4039"), Hex("5A3B2C"), Hex("F6F0E5") },
        new Color[] { Hex("C4C5BE"), Hex("353A40"), Hex("553D30"), Hex("26292D") },
        new Color[] { Hex("C5D0C6"), Hex("42483E"), Hex("614630"), Hex("F4F0E8") },
        new Color[] { Hex("D1D4D3"), Hex("343A40"), Hex("514238"), Hex("22272B") },
        new Color[] { Hex("E1D7C9"), Hex("3B3C3D"), Hex("654631"), Hex("F5F0E8") },
        new Color[] { Hex("B9C8D0"), Hex("20262C"), Hex("604333"), Hex("F3F0E8") },
        new Color[] { Hex("E9E4D9"), Hex("292D32"), Hex("4C372A"), Hex("34383D") },
        new Color[] { Hex("D8E0D6"), Hex("252B2A"), Hex("624633"), Hex("F3EEE4") },
        new Color[] { Hex("D9C9B6"), Hex("303438"), Hex("65452F"), Hex("EEE8DE") },
        new Color[] { Hex("AFC4C0"), Hex("252B30"), Hex("533D30"), Hex("F1EEE7") },
        new Color[] { Hex("E5D9C8"), Hex("343638"), Hex("75513A"), Hex("F4EFE5") },
        new Color[] { Hex("C5D2C0"), Hex("262A2B"), Hex("5B4030"), Hex("F5F0E7") },
        new Color[] { Hex("D5D8DA"), Hex("30343A"), Hex("684A36"), Hex("F0EEE8") },
        new Color[] { Hex("D7C7BC"), Hex("292D31"), Hex("593D2D"), Hex("F4EDE3") },
        new Color[] { Hex("B8C9B8"), Hex("343A38"), Hex("644833"), Hex("F4F0E8") },
        new Color[] { Hex("E7E0D2"), Hex("2C3035"), Hex("674833"), Hex("F2EEE5") },
        new Color[] { Hex("BACBD4"), Hex("2C3238"), Hex("634532"), Hex("F5F1E8") },
        new Color[] { Hex("D4DCCF"), Hex("303633"), Hex("6A4B36"), Hex("F4EFE7") },
        new Color[] { Hex("DCC9B9"), Hex("30343A"), Hex("583D2F"), Hex("F4F0E8") },
        new Color[] { Hex("C8D2D6"), Hex("262B30"), Hex("674B37"), Hex("F2EEE6") },
        new Color[] { Hex("F7F7F4"), Hex("202329"), Hex("2B2B2D"), Hex("F3F0E8"), Hex("131416") },
        new Color[] { Hex("F4F4F1"), Hex("5A5F66"), Hex("604333"), Hex("F3F0E8"), Hex("1A1B1E") },
        new Color[] { Hex("EFEDE6"), Hex("3A2E2A"), Hex("8A5A3C"), Hex("F3F0E8"), Hex("202225") },
        new Color[] { Hex("DADCDE"), Hex("15171A"), Hex("F2F0E8"), Hex("F3F0E8"), Hex("0F1012") },
        new Color[] { Hex("F2E8D5"), Hex("7A3B2E"), Hex("3B2A22"), Hex("F3F0E8"), Hex("2A2B2D") },
        new Color[] { Hex("F6F5F1"), Hex("8C3A2B"), Hex("2B2B2D"), Hex("F3F0E8"), Hex("353638") },
        new Color[] { Hex("B9C8B0"), Hex("202329"), Hex("F3F0E8"), Hex("F3F0E8"), Hex("1E2023") },
        new Color[] { Hex("9FB4C4"), Hex("F0EEE7"), Hex("202329"), Hex("F3F0E8"), Hex("F2F0EA") },
        new Color[] { Hex("3A3D42"), Hex("15171A"), Hex("F0EEE7"), Hex("F3F0E8"), Hex("8E8F8A") },
        new Color[] { Hex("E0CFA8"), Hex("7A4A32"), Hex("3B2A22"), Hex("F3F0E8"), Hex("D8D4CA") },
        new Color[] { Hex("F0E3A8"), Hex("4A4F55"), Hex("F4F1E9"), Hex("F3F0E8"), Hex("E8E6DF") },
        new Color[] { Hex("C98B6B"), Hex("2B2B2D"), Hex("F0EEE7"), Hex("F3F0E8"), Hex("4A4A4A") },
        new Color[] { Hex("F8F8F6"), Hex("2E3A4A"), Hex("2E3A4A"), Hex("F3F0E8"), Hex("17181B") },
        new Color[] { Hex("E9E9E6"), Hex("6B2F2A"), Hex("1C1C1E"), Hex("F3F0E8"), Hex("0E0F10") },
        new Color[] { Hex("2B2D31"), Hex("D8D6CF"), Hex("D8D6CF"), Hex("F3F0E8"), Hex("F4F3EF") },
        new Color[] { Hex("C9D7C3"), Hex("3E4A3B"), Hex("F5F2EA"), Hex("F3F0E8"), Hex("161718") },
        new Color[] { Hex("D6C4D8"), Hex("2B2B2D"), Hex("F5F2EA"), Hex("F3F0E8"), Hex("E6E4DE") },
        new Color[] { Hex("F5F1E6"), Hex("A35B3A"), Hex("4A3025"), Hex("F3F0E8"), Hex("1D1E20") }
    };

    struct GlowEntry
    {
        public Material material;
        public Color color;
        public float strength;
        public float maxIntensity;
    }

    static readonly List<GlowEntry> glowEntries = new List<GlowEntry>();
    static float currentNight;
    static Texture2D windowGlowTexture;

    public static float CurrentNight { get { return currentNight; } }

    public static void SetNightGlow(float night)
    {
        currentNight = Mathf.Clamp01(night);
        for (int i = 0; i < glowEntries.Count; i++)
        {
            GlowEntry g = glowEntries[i];
            if (g.material == null) continue;
            g.material.SetColor("_EmissionColor", g.color * (g.strength * g.maxIntensity * currentNight));
        }
    }

    /// <summary>Start Menu baker: snapshot of the glow registry (window / lamp emission) so a baked scene can restore it.</summary>
    public static void ExportGlow(List<Material> mats, List<Color> colors, List<float> strengths, List<float> maxes)
    {
        for (int i = 0; i < glowEntries.Count; i++)
        {
            GlowEntry g = glowEntries[i];
            if (g.material == null) continue;
            mats.Add(g.material); colors.Add(g.color); strengths.Add(g.strength); maxes.Add(g.maxIntensity);
        }
    }

    public static void ImportGlow(Material m, Color color, float strength, float maxIntensity)
    {
        if (m == null) return;
        for (int i = 0; i < glowEntries.Count; i++) if (glowEntries[i].material == m) return;
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        GlowEntry g = new GlowEntry();
        g.material = m; g.color = color; g.strength = strength; g.maxIntensity = maxIntensity;
        glowEntries.Add(g);
        m.SetColor("_EmissionColor", color * (strength * maxIntensity * currentNight));
    }

    static void RegisterGlow(Material m, Color color, float strength, float maxIntensity, Texture2D map, Vector2 tiling)
    {
        if (!m.HasProperty("_EmissionColor")) return;
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (map != null && m.HasProperty("_EmissionMap"))
        {
            m.SetTexture("_EmissionMap", map);
            m.SetTextureScale("_EmissionMap", tiling);
        }
        GlowEntry g = new GlowEntry();
        g.material = m;
        g.color = color;
        g.strength = strength;
        g.maxIntensity = maxIntensity;
        glowEntries.Add(g);
        m.SetColor("_EmissionColor", color * (strength * maxIntensity * currentNight));
    }

    public static Texture2D GetWindowGlowTexture()
    {
        if (windowGlowTexture != null) return windowGlowTexture;
        int size = 128;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
        t.name = "StylizedWindowGlow";
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)size, v = y / (float)size;
            float blotch = DetailTex.Pn(u, v, 21, 1);
            float fine = DetailTex.Pn(u, v, 22, 2);
            float pool = 0.5f + 0.5f * Mathf.Sin(Mathf.PI * 2f * (v + 0.18f * blotch));
            float folds = 0.5f + 0.5f * Mathf.Sin(Mathf.PI * 2f * (3f * u + 0.3f * blotch));
            float drift = 0.5f + 0.5f * Mathf.Sin(Mathf.PI * 2f * (u + v + 0.25f * fine));
            float val = Mathf.Clamp(0.40f + 0.22f * blotch + 0.11f * folds + 0.08f * pool + 0.07f * drift + 0.04f * fine, 0.3f, 0.9f);
            Color tint = Color.Lerp(new Color(1f, 0.78f, 0.52f), new Color(1f, 0.93f, 0.78f), Mathf.Clamp01(0.35f + 0.5f * pool + 0.3f * blotch));
            px[y * size + x] = new Color(val * tint.r, val * tint.g, val * tint.b, 1f);
        }
        t.SetPixels(px);
        t.Apply(true, false);
        windowGlowTexture = t;
        return t;
    }

    static void RegisterWindowGlow(Material m, int variant, Vector2 tiling)
    {
        uint h = unchecked((uint)(variant * 2654435761u + 12345u));
        float roll = (h % 1000u) / 1000f;
        float strength = roll < 0.15f ? 0f : 0.75f + 0.25f * (((h >> 10) % 100u) / 100f);
        float warmth = ((h >> 17) % 100u) / 100f;
        Color color = Color.Lerp(new Color(1f, 0.70f, 0.36f), new Color(1f, 0.86f, 0.62f), warmth);
        RegisterGlow(m, color, strength, 1.5f, GetWindowGlowTexture(), tiling);
    }

    public static void RegisterEmissive(Material m, Color color, float strength, float maxIntensity, Texture2D map)
    {
        RegisterGlow(m, color, strength, maxIntensity, map, Vector2.one);
    }

    public static Material GetLampGlow()
    {
        return Get("lamp_glow", Hex("FFE7A3"), 0);
    }

    static bool IsLampGlowName(string name)
    {
        string n = Normalize(name);
        if (n.Contains("pole") || n.Contains("traffic") || n.Contains("signal") || n.Contains("post") || n.Contains("base")) return false;
        return n.Contains("bulb") || n.Contains("lantern") || n.Contains("lamphead") || n.Contains("lamp_head") || n.Contains("lamphead") || n.Contains("light") || n.Contains("head");
    }

    static Color Hex(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString("#" + hex, out c);
        return c;
    }


    public static void Reset()
    {
        glowEntries.Clear();
        windowGlowTexture = null;
        foreach (KeyValuePair<string, Material> pair in materials)
            if (pair.Value != null) WorldUtil.Kill(pair.Value);
        foreach (KeyValuePair<string, Texture2D> pair in textures)
            if (pair.Value != null) WorldUtil.Kill(pair.Value);
        foreach (KeyValuePair<string, Texture2D> pair in normalTextures)
            if (pair.Value != null) WorldUtil.Kill(pair.Value);
        foreach (KeyValuePair<string, Texture2D> pair in heightTextures)
            if (pair.Value != null) WorldUtil.Kill(pair.Value);
        foreach (KeyValuePair<string, Texture2D> pair in occlusionTextures)
            if (pair.Value != null) WorldUtil.Kill(pair.Value);
        materials.Clear();
        textures.Clear();
        normalTextures.Clear();
        heightTextures.Clear();
        occlusionTextures.Clear();
    }

    public static bool LooksLikeBuilding(Transform root)
    {
        if (root == null) return false;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string n = Normalize(all[i].name);
            if (n == "house" || n.Contains("building") || n.Contains("wall") || n.Contains("facade") ||
                n == "door" || n.StartsWith("door_") || n.Contains("window_frame") || n.Contains("windowframe") ||
                n.Contains("roof") || n.Contains("entrance_door") || n.Contains("cottage") || n.Contains("mall") ||
                n.Contains("office") || n.Contains("hotel") || n.Contains("hospital") || n.Contains("school") ||
                n.Contains("apartment") || n.Contains("cinema") || n.Contains("station") || n.Contains("warehouse") ||
                n.Contains("clinic") || n.Contains("shop") || n.Contains("cafe") || n.Contains("motel") ||
                n.Contains("library") || n.Contains("workshop") || n.Contains("plaza") || n.Contains("tower") ||
                n.Contains("hall") || n.Contains("complex") || n.Contains("firestation") || n.Contains("sportsclub"))
                return true;
        }
        return false;
    }

    public static void ApplyColoredRole(Transform root, string role, Color color, int variant)
    {
        if (root == null) return;
        Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            rs[i].sharedMaterial = Get(role, color, variant);
            StylizedUVMapper.Apply(rs[i]);
        }
    }

    public static void ApplyUniformRole(Transform root, string role)
    {
        if (root == null) return;
        Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
        string normalizedRole = Normalize(role);
        for (int i = 0; i < rs.Length; i++)
        {
            Color fallback = GetRendererColor(rs[i]);
            if (normalizedRole == "ground") fallback = new Color(0.47f, 0.61f, 0.31f, 1f);
            rs[i].sharedMaterial = Get(role, fallback, 0);
            StylizedUVMapper.Apply(rs[i]);
        }
    }

    public static void ApplyWorldMaterials(Transform root)
    {
        if (root == null) return;
        Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            Color fallback = GetRendererColor(rs[i]);
            string part = rs[i].transform.name;
            Transform p = IsLampGlowName(part) ? null : rs[i].transform.parent;
            while (p != null && p != root)
            {
                string pn = Normalize(p.name);
                if (pn.Contains("trafficlight") || pn.Contains("signal") || pn.Contains("pole") ||
                    pn.Contains("lamp") || pn.Contains("barrier") || pn.Contains("rail") ||
                    pn.Contains("sign") || pn.Contains("rock") || pn.Contains("wood") ||
                    pn.Contains("log") || pn.Contains("stump") || pn.Contains("road") ||
                    pn.Contains("asphalt") || pn.Contains("concrete") || pn.Contains("sidewalk"))
                { part = p.name; break; }
                p = p.parent;
            }
            rs[i].sharedMaterial = GetWorldPart(part, fallback);
            StylizedUVMapper.Apply(rs[i]);
        }
    }

    static Color GetRendererColor(Renderer r)
    {
        if (r != null && r.sharedMaterial != null)
        {
            Material m = r.sharedMaterial;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
        }
        return new Color(0.72f, 0.72f, 0.72f, 1f);
    }

    public static Material GetHousePart(string part, int paletteIndex, bool smallHouse)
    {
        Color[] p = palettes[Mathf.Abs(paletteIndex) % palettes.Length];
        string n = Normalize(part);
        Color color;
        string role;
        if (n.Contains("fence") || n.Contains("picket"))
        {
            color = p[0].grayscale < 0.50f ? Hex("F2F0E8") : Hex("59402F");
            role = "house_trim";
        }
        else if (n.Contains("door") && !n.Contains("frame")) { color = p[2]; role = "house_wood"; }
        else if (n.Contains("window") && !n.Contains("frame")) { color = Hex("78AFCF"); role = "house_glass"; }
        else if (n.Contains("frame") || n.Contains("trim") || n.Contains("sill"))
        {
            color = p[0].grayscale >= 0.46f ? Hex("30343A") : Hex("E9E5DA");
            role = "house_trim";
        }
        else if (n.Contains("roof") || n.Contains("shingle") || n.Contains("gable"))
        {
            color = (smallHouse && p.Length <= 4) ? Hex("202329") : p[1];
            role = "house_roof";
        }
        else if (n.Contains("platform") || n.Contains("foundation") || n.Contains("step"))
        {
            color = p.Length > 4 ? p[4] : Hex("AAA9A1"); role = "house_concrete";
        }
        else if (n.Contains("wall") || n == "house" || n.Contains("facade") || n.Contains("building"))
        {
            color = p[0]; role = smallHouse ? "house_plaster_small" : "house_plaster";
        }
        else
        {
            color = p[0]; role = smallHouse ? "house_plaster_small" : "house_plaster";
        }
        return Get(role, color, paletteIndex);
    }

    public static Material GetWorldPart(string part, Color fallback)
    {
        string n = Normalize(part);
        if (IsLampGlowName(n)) return GetLampGlow();
        if (n.Contains("lane") || n.Contains("marking") || n.Contains("stripe")) return Get("paint", fallback, 0);
        if (n.Contains("asphalt") || n.Contains("road") || n.Contains("highway")) return Get("asphalt", fallback, 0);
        if (n.Contains("window") || n.Contains("glass")) return Get("glass", fallback, 0);
        if (n.Contains("trafficlight") || n.Contains("signal")) return Get("plastic", fallback, 0);
        if (n.Contains("fence") || n.Contains("picket"))
        {
            Color fence = fallback.grayscale < 0.50f ? Hex("F2F0E8") : Hex("59402F");
            return Get("paintedMetal", fence, 0);
        }
        if (n.Contains("pole") || n.Contains("barrier") || n.Contains("rail") || n.Contains("sign")) return Get("paintedMetal", fallback, 0);
        if (n.Contains("rock") || n.Contains("boulder")) return Get("rock", fallback, 0);
        if (n.Contains("wood") || n.Contains("log") || n.Contains("stump") || n.Contains("door")) return Get("wood", fallback, 0);
        if (n.Contains("snow")) return Get("snow", fallback, 0);
        if (n.Contains("terrain") || n.Contains("ground") || n.Contains("grass"))
            return Get("ground", new Color(0.47f, 0.61f, 0.31f, 1f), 0);
        if (n.Contains("concrete") || n.Contains("sidewalk") || n.Contains("curb") || n.Contains("platform")) return Get("concrete", fallback, 0);
        return Get("surface", fallback, 0);
    }

    public static void CollectByRole(string role, List<Material> into)
    {
        foreach (KeyValuePair<string, Material> p in materials)
            if (p.Value != null && p.Key.StartsWith(role + "_") && !into.Contains(p.Value)) into.Add(p.Value);
    }

    public static Material Get(string role, Color baseColor, int variant = 0)
    {
        string key = role + "_" + ColorKey(baseColor) + "_" + variant;
        Material cached;
        if (materials.TryGetValue(key, out cached) && cached != null) return cached;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null && !warnedMissingShader)
        {
            warnedMissingShader = true;
            Debug.LogError("StylizedMaterialFactory: shader 'Universal Render Pipeline/Lit' is missing from this build, world materials will look wrong. Run Tools > Build > Prepare Shaders For Build and rebuild.");
        }
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        Material m = new Material(shader);
        m.name = "Stylized_" + key;
        string textureRole = role.Contains("house_plaster") ? role + "_v" + Mathf.Abs(variant % 20) : role;
        Texture2D tex = GetTexture(textureRole);
        Texture2D heightMap = GetHeightMap(textureRole, tex);
        Texture2D normalMap = GetNormalMap(textureRole, heightMap);
        Texture2D aoMap = GetOcclusionMap(textureRole, heightMap);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", normalMap); m.EnableKeyword("_NORMALMAP"); SetFloat(m, "_BumpScale", role.Contains("asphalt") ? 0.72f : role.Contains("concrete") ? 0.20f : role.Contains("plaster") ? 0.42f : (role.Contains("foliage") || role.Contains("leaf")) ? 0.48f : role.Contains("roof") ? 0.55f : 0.22f); }
        if (m.HasProperty("_OcclusionMap")) { m.SetTexture("_OcclusionMap", aoMap); m.EnableKeyword("_OCCLUSIONMAP"); SetFloat(m, "_OcclusionStrength", role.Contains("asphalt") || role.Contains("plaster") ? 0.72f : role.Contains("concrete") ? 0.34f : 0.42f); }
        if (m.HasProperty("_ParallaxMap")) { m.SetTexture("_ParallaxMap", heightMap); SetFloat(m, "_Parallax", role.Contains("asphalt") ? 0.012f : 0.006f); }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
        if (m.HasProperty("_Color")) m.SetColor("_Color", baseColor);
        SetFloat(m, "_Metallic", 0f);
        SetFloat(m, "_Smoothness", role.Contains("glass") ? 0.34f : role.Contains("paintedMetal") ? 0.08f : 0.015f);
        SetFloat(m, "_Glossiness", role.Contains("glass") ? 0.34f : 0.015f);
        SetFloat(m, "_SpecularHighlights", 0f);
        SetFloat(m, "_EnvironmentReflections", role.Contains("glass") ? 1f : 0f);
        if (role.StartsWith("house_"))
        {
            SetFloat(m, "_Cull", 0f);
            SetFloat(m, "_CullMode", 0f);
        }
        float tileWorldSize = GetTileWorldSize(role);
        Vector2 tiling = Vector2.one / Mathf.Max(0.05f, tileWorldSize);
        if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", tiling);
        if (m.HasProperty("_MainTex")) m.SetTextureScale("_MainTex", tiling);
        if (m.HasProperty("_BumpMap")) m.SetTextureScale("_BumpMap", tiling);
        if (m.HasProperty("_OcclusionMap")) m.SetTextureScale("_OcclusionMap", tiling);
        if (m.HasProperty("_ParallaxMap")) m.SetTextureScale("_ParallaxMap", tiling);
        if (role == "house_glass") RegisterWindowGlow(m, variant, tiling);
        else if (role == "lamp_glow") RegisterGlow(m, new Color(1f, 0.72f, 0.36f), 1f, 2.3f, null, Vector2.one);
        materials[key] = m;
        return m;
    }

    static float GetTileWorldSize(string role)
    {
        string r = Normalize(role);
        if (r.Contains("asphalt")) return 0.85f;
        if (r.Contains("concrete")) return 5.0f;
        if (r.Contains("roof")) return 0.72f;
        if (r.Contains("wood")) return 1.4f;
        if (r.Contains("glass")) return 1.25f;
        if (r.Contains("rock")) return 3.2f;
        if (r.Contains("snow")) return 5.5f;
        if (r.Contains("ground") || r.Contains("lawn")) return 12.0f;
        if (r.Contains("plaster_small")) return 1.45f;
        if (r.Contains("plaster")) return 3.0f;
        if (r.Contains("paint")) return 2.0f;
        if (r.Contains("plastic")) return 1.5f;
        if (r.Contains("metal")) return 1.8f;
        return 2.5f;
    }

    static void SetFloat(Material m, string property, float value)
    {
        if (m.HasProperty(property)) m.SetFloat(property, value);
    }

    static string Normalize(string s)
    {
        return (s ?? "").ToLowerInvariant().Replace(" ", "").Replace("-", "_");
    }

    static int ColorKey(Color c)
    {
        Color32 b = c;
        return b.r | (b.g << 8) | (b.b << 16);
    }

    static Texture2D GetTexture(string role)
    {
        string key = Normalize(role);
        Texture2D t;
        if (textures.TryGetValue(key, out t) && t != null) return t;
        int size = key.Contains("ground") ? 1024 : key.Contains("asphalt") || key.Contains("plaster") || key.Contains("concrete") ? 512 : 256;
        t = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
        t.name = "StylizedTexture_" + key;
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Trilinear;
        t.anisoLevel = 2;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)size, v = y / (float)size;
            float broad = Mathf.PerlinNoise(u * 4.4f + 13.1f, v * 4.4f + 7.7f);
            float mid = Mathf.PerlinNoise(u * 11.0f + 3.4f, v * 11.0f + 19.2f);
            float fine = Mathf.PerlinNoise(u * 29.0f + 8.1f, v * 29.0f + 2.7f);
            float value = 0.91f + (broad - 0.5f) * 0.30f + (mid - 0.5f) * 0.12f + (fine - 0.5f) * 0.06f;
            float line = 0f;
            if (key.Contains("roof"))
            {
                float cell = size / 14f;
                int rowIndex = Mathf.FloorToInt(y / cell);
                float row = Mathf.Repeat(y, cell);
                float tile = Mathf.Repeat(x + ((rowIndex % 2) * cell * 0.5f), cell * 2f);
                bool seam = row < cell * 0.055f || tile < cell * 0.055f;
                bool ridge = row >= cell * 0.055f && row < cell * 0.14f;
                line = seam ? -0.30f : ridge ? 0.10f : ((row < cell * 0.25f) ? -0.035f : 0.015f);
                value = 0.88f + (broad - 0.5f) * 0.12f;
            }
            else if (key.Contains("wood"))
            {
                float grain = Mathf.PerlinNoise(u * 7f + 2f, v * 1.2f + 5f);
                float streak = Mathf.PerlinNoise(u * 24f + 1.7f, v * 2.0f + 4.3f);
                line = (grain > 0.60f ? -0.18f : 0f) + (streak > 0.72f ? 0.09f : 0f);
                value = 0.91f + (broad - 0.5f) * 0.18f + (mid - 0.5f) * 0.08f;
            }
            else if (key.Contains("asphalt"))
            {
                float aggregateFine = Mathf.PerlinNoise(u * 181f + 2.71f, v * 173f + 8.37f);
                float aggregateMid = Mathf.PerlinNoise(u * 67f + 14.2f, v * 61f + 3.8f);
                float binder = Mathf.PerlinNoise(u * 13f + 5.7f, v * 15f + 12.4f);
                float chips = Mathf.PerlinNoise(u * 97f + 27.1f, v * 89f + 16.8f);
                float pore = Mathf.PerlinNoise(u * 229f + 41.3f, v * 211f + 18.9f);
                value = 0.70f + (binder - 0.5f) * 0.09f
                    + (aggregateMid - 0.5f) * 0.19f + (aggregateFine - 0.5f) * 0.15f;
                if (chips > 0.76f && aggregateFine > 0.54f) value += 0.10f;
                if (chips < 0.20f && pore < 0.43f) value -= 0.085f;
                if (pore < 0.24f) value -= 0.035f;
            }
            else if (key.Contains("concrete"))
            {
                float pebble = Mathf.PerlinNoise(u * 83f + 18.7f, v * 79f + 3.2f);
                value = 0.92f + (broad - 0.5f) * 0.16f + (mid - 0.5f) * 0.09f + (pebble - 0.5f) * 0.08f;
                if (fine > 0.992f && pebble < 0.36f) value -= 0.0035f;
            }
            else if (key.Contains("rock"))
            {
                float strata = Mathf.PerlinNoise(u * 5f + Mathf.Sin(v * 13f) * 0.2f, v * 3f);
                value = 0.76f + (broad - 0.5f) * 0.42f + (mid - 0.5f) * 0.20f;
                line = strata > 0.63f ? -0.13f : 0.025f;
            }
            else if (key.Contains("ground"))
            {
                float warpX = Mathf.PerlinNoise(u * 1.7f + 31.0f, v * 1.6f + 11.0f);
                float warpY = Mathf.PerlinNoise(u * 1.8f + 7.0f, v * 1.5f + 23.0f);
                float lawnMacro = Mathf.PerlinNoise(u * 2.15f + warpX * 1.9f + 9.4f,
                                                    v * 2.05f + warpY * 1.8f + 17.2f);
                float lawnMid = Mathf.PerlinNoise(u * 5.0f + warpY * 0.9f + 22.1f,
                                                  v * 4.7f + warpX * 0.8f + 4.8f);
                float lawnFine = Mathf.PerlinNoise(u * 17.0f + 3.8f, v * 15.0f + 28.6f);
                float clumpEdge = Mathf.PerlinNoise(u * 3.1f + warpX * 1.5f + 5.0f,
                                                    v * 3.0f + warpY * 1.4f + 13.0f);
                value = 0.925f
                    + (lawnMacro - 0.5f) * 0.23f
                    + (clumpEdge - 0.5f) * 0.055f
                    + (lawnMid - 0.5f) * 0.030f
                    + (lawnFine - 0.5f) * 0.012f;
                float bladeNoise = Mathf.PerlinNoise(u * 31.0f + lawnMacro * 2.1f + 14.0f,
                                                     v * 27.0f + lawnMid * 1.7f + 7.0f);
                if (bladeNoise > 0.81f && lawnFine > 0.53f) value += 0.010f;
            }
            else if (key.Contains("snow"))
            {
                value = 0.97f + (broad - 0.5f) * 0.12f + (mid - 0.5f) * 0.04f;
                line = (Mathf.PerlinNoise(u * 3f + 0.2f, v * 7f) > 0.78f) ? -0.08f : 0f;
            }
            else if (key.Contains("plaster"))
            {
                float pores = Mathf.PerlinNoise(u * 97f + 21.4f, v * 103f + 8.2f);
                float finePores = Mathf.PerlinNoise(u * 211f + 17.1f, v * 197f + 31.6f);
                value = 0.91f + (broad - 0.5f) * 0.20f + (mid - 0.5f) * 0.08f;
                if (pores < 0.27f && finePores < 0.58f) value -= 0.10f;
                if (pores > 0.77f && finePores > 0.62f) value += 0.045f;
                value = Mathf.Round(value * 15f) / 15f;
            }
            else if (key.Contains("glass"))
            {
                value = 0.79f + (v - 0.5f) * 0.08f + (broad - 0.5f) * 0.045f;
                float diagonal = u - (0.20f + v * 0.48f);
                float parallel = u - (0.28f + v * 0.48f);
                float highlight = u - (0.58f + v * 0.27f);
                if (Mathf.Abs(diagonal) < 0.022f && v > 0.08f && v < 0.94f) value += 0.55f;
                if (Mathf.Abs(parallel) < 0.010f && v > 0.10f && v < 0.90f) value += 0.36f;
                if (Mathf.Abs(highlight) < 0.018f && v > 0.38f && v < 0.92f) value += 0.30f;
                if (u > 0.07f && u < 0.105f && v > 0.10f && v < 0.90f) value += 0.12f;
                line = 0f;
            }
            else if (key.Contains("foliage") || key.Contains("leaf"))
            {
                float leafCloud = Mathf.PerlinNoise(u * 19f + 4.1f, v * 17f + 9.7f);
                float veins = Mathf.PerlinNoise(u * 42f + 7.2f, v * 11f + 2.4f);
                value = 0.78f + (broad - 0.5f) * 0.28f + (leafCloud - 0.5f) * 0.25f;
                if (veins > 0.70f) value += 0.11f;
                if (leafCloud < 0.31f) value -= 0.12f;
            }
            else if (key.Contains("paint") || key.Contains("plastic") || key.Contains("surface"))
            {
                value = 0.91f + (broad - 0.5f) * 0.18f + (mid - 0.5f) * 0.07f;
            }
            float k = Mathf.Clamp(value + line, 0.48f, key.Contains("glass") ? 1.65f : 1.22f);
            if (!key.Contains("asphalt")) k = Mathf.Round(k * 12f) / 12f;
            t.SetPixel(x, y, new Color(k, k, k, 1f));
        }
        BlendTextureEdges(t, key.Contains("asphalt") ? 28 : key.Contains("ground") ? 26 : 14);
        t.Apply(true, true);
        textures[key] = t;
        return t;
    }

    static Texture2D GetHeightMap(string role, Texture2D albedo)
    {
        Texture2D cached;
        if (heightTextures.TryGetValue(role, out cached) && cached != null) return cached;
        int size = albedo != null ? albedo.width : 256;
        Texture2D map = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
        map.name = "StylizedHeight_" + role;
        map.wrapMode = TextureWrapMode.Repeat;
        map.filterMode = FilterMode.Trilinear;
        bool asphalt = role.Contains("asphalt");
        bool plaster = role.Contains("plaster") || role.Contains("concrete");
        bool roof = role.Contains("roof") || role.Contains("shingle") || role.Contains("gable");
        bool foliage = role.Contains("foliage") || role.Contains("leaf");
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)size, v = y / (float)size;
            float broad = Mathf.PerlinNoise(u * (asphalt ? 15f : 7f) + 13.7f, v * (asphalt ? 13f : 8f) + 5.1f);
            float mid = Mathf.PerlinNoise(u * (asphalt ? 71f : plaster ? 43f : 29f) + 2.3f, v * (asphalt ? 67f : plaster ? 47f : 31f) + 19.4f);
            float fine = Mathf.PerlinNoise(u * (asphalt ? 211f : plaster ? 151f : 97f) + 31.8f, v * (asphalt ? 197f : plaster ? 137f : 101f) + 7.2f);
            bool concrete = role.Contains("concrete");
            float h = 0.53f + (broad - 0.5f) * (asphalt ? 0.10f : 0.07f)
                + (mid - 0.5f) * (asphalt ? 0.24f : concrete ? 0.065f : plaster ? 0.16f : 0.10f)
                + (fine - 0.5f) * (asphalt ? 0.26f : concrete ? 0.04f : plaster ? 0.17f : 0.08f);
            if (roof)
            {
                float cell = size / 14f;
                int rowIndex = Mathf.FloorToInt(y / cell);
                float row = Mathf.Repeat(y, cell);
                float staggeredX = Mathf.Repeat(x + ((rowIndex % 2) * cell * 0.5f), cell * 2f);
                float seam = Mathf.Min(row, Mathf.Min(staggeredX, cell - row));
                float ridge = Mathf.Exp(-Mathf.Pow((row - cell * 0.19f) / (cell * 0.075f), 2f));
                h += ridge * 0.13f;
                if (seam < cell * 0.045f) h -= 0.11f;
            }
            if (foliage)
            {
                float clusters = Mathf.PerlinNoise(u * 18f + 4.1f, v * 17f + 9.7f);
                float veins = Mathf.PerlinNoise(u * 43f + 7.2f, v * 12f + 2.4f);
                h += (clusters - 0.5f) * 0.18f;
                if (veins > 0.76f) h += 0.045f;
                if (clusters < 0.28f) h -= 0.035f;
            }
            if (asphalt && fine < 0.30f) h -= 0.10f;
            if (concrete && fine < 0.045f && mid < 0.35f) h -= 0.003f;
            else if (plaster && !concrete && fine < 0.22f && mid < 0.55f) h -= 0.07f;
            h = Mathf.Clamp01(h);
            map.SetPixel(x, y, new Color(h, h, h, 1f));
        }
        BlendTextureEdges(map, Mathf.Max(4, size / 24));
        map.Apply(true, false);
        heightTextures[role] = map;
        return map;
    }

    static Texture2D GetNormalMap(string role, Texture2D height)
    {
        Texture2D cached;
        if (normalTextures.TryGetValue(role, out cached) && cached != null) return cached;
        int size = height.width;
        Color[] src = height.GetPixels();
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
        normal.name = "StylizedNormal_" + role;
        normal.wrapMode = TextureWrapMode.Repeat;
        normal.filterMode = FilterMode.Trilinear;
        float strength = role.Contains("asphalt") ? 5.2f : role.Contains("concrete") ? 1.0f : role.Contains("plaster") ? 3.0f : role.Contains("rock") ? 3.6f : role.Contains("roof") ? 3.0f : (role.Contains("foliage") || role.Contains("leaf")) ? 2.65f : 1.65f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int xl = y * size + (x + size - 1) % size;
            int xr = y * size + (x + 1) % size;
            int yd = ((y + size - 1) % size) * size + x;
            int yu = ((y + 1) % size) * size + x;
            float dx = (src[xr].r - src[xl].r) * strength;
            float dy = (src[yu].r - src[yd].r) * strength;
            Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
            normal.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
        }
        normal.Apply(true, true);
        normalTextures[role] = normal;
        return normal;
    }

    static Texture2D GetOcclusionMap(string role, Texture2D height)
    {
        Texture2D cached;
        if (occlusionTextures.TryGetValue(role, out cached) && cached != null) return cached;
        int size = height.width;
        Color[] src = height.GetPixels();
        Texture2D ao = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
        ao.name = "StylizedAO_" + role;
        ao.wrapMode = TextureWrapMode.Repeat;
        ao.filterMode = FilterMode.Trilinear;
        float strength = role.Contains("asphalt") ? 0.68f : role.Contains("concrete") ? 0.18f : role.Contains("plaster") ? 0.48f : role.Contains("rock") ? 0.52f : 0.28f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int i = y * size + x;
            float h = src[i].r;
            float around = (src[y * size + (x + size - 1) % size].r + src[y * size + (x + 1) % size].r + src[((y + size - 1) % size) * size + x].r + src[((y + 1) % size) * size + x].r) * 0.25f;
            float cavity = Mathf.Clamp01((around - h) * 2.8f);
            float value = Mathf.Clamp01(1f - cavity * strength - Mathf.Max(0f, 0.46f - h) * strength * 0.45f);
            ao.SetPixel(x, y, new Color(value, value, value, 1f));
        }
        ao.Apply(true, true);
        occlusionTextures[role] = ao;
        return ao;
    }

    static void BlendTextureEdges(Texture2D texture, int edgeWidth)
    {
        if (texture == null) return;
        int size = texture.width;
        int edge = Mathf.Clamp(edgeWidth, 2, size / 8);
        Color[] pixels = texture.GetPixels();
        for (int y = 0; y < size; y++)
        {
            for (int d = 0; d < edge; d++)
            {
                int left = y * size + d;
                int right = y * size + (size - 1 - d);
                float amount = 1f - d / (float)edge;
                Color avg = (pixels[left] + pixels[right]) * 0.5f;
                pixels[left] = Color.Lerp(pixels[left], avg, amount);
                pixels[right] = Color.Lerp(pixels[right], avg, amount);
            }
        }
        for (int x = 0; x < size; x++)
        {
            for (int d = 0; d < edge; d++)
            {
                int top = d * size + x;
                int bottom = (size - 1 - d) * size + x;
                float amount = 1f - d / (float)edge;
                Color avg = (pixels[top] + pixels[bottom]) * 0.5f;
                pixels[top] = Color.Lerp(pixels[top], avg, amount);
                pixels[bottom] = Color.Lerp(pixels[bottom], avg, amount);
            }
        }
        texture.SetPixels(pixels);
    }

    public static void ApplyHouseMaterials(Transform root, Vector3 approximateSize, string seedName)
    {
        if (root == null) return;
        bool smallHouse = true;
        int stable = 17;
        string s = (seedName ?? root.name);
        for (int i = 0; i < s.Length; i++) stable = unchecked(stable * 31 + s[i]);
        int palette = (int)((uint)stable % (uint)palettes.Length);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Transform tr = renderers[i].transform;
            string part = tr.name;
            Transform parent = tr.parent;
            while (parent != null && parent != root)
            {
                string pn = Normalize(parent.name);
                if (pn.Contains("door") || pn.Contains("window") || pn.Contains("roof") || pn.Contains("wall") || pn.Contains("platform") || pn.Contains("house") || pn.Contains("frame") || pn.Contains("facade") || pn.Contains("fence") || pn.Contains("picket"))
                { part = parent.name; break; }
                parent = parent.parent;
            }
            Material[] original = renderers[i].sharedMaterials;
            bool combinedBuildingMesh = Normalize(part) == "house" || Normalize(part).Contains("building");
            bool hasNamedRoofMaterial = false;
            if (original != null)
            {
                for (int m = 0; m < original.Length; m++)
                {
                    string mn = Normalize(original[m] != null ? original[m].name : "");
                    if (mn.Contains("roof") || mn.Contains("shingle") || mn.Contains("gable")) hasNamedRoofMaterial = true;
                }
            }
            if (combinedBuildingMesh && !hasNamedRoofMaterial && TrySplitCombinedRoof(renderers[i], palette, smallHouse))
            {
                StylizedUVMapper.ApplyHouse(renderers[i], stable);
                continue;
            }

            if (original != null && original.Length > 1)
            {
                Material[] styled = new Material[original.Length];
                for (int m = 0; m < original.Length; m++)
                {
                    string materialName = original[m] != null ? original[m].name : part;
                    styled[m] = GetHousePart(materialName + "_" + part, palette, smallHouse);
                }
                renderers[i].sharedMaterials = styled;
            }
            else
            {
                renderers[i].sharedMaterial = GetHousePart(part, palette, smallHouse);
            }
            StylizedUVMapper.ApplyHouse(renderers[i], stable);
        }
    }

    static bool TrySplitCombinedRoof(Renderer renderer, int palette, bool smallHouse)
    {
        MeshRenderer mr = renderer as MeshRenderer;
        if (mr == null || mr.sharedMaterials == null || mr.sharedMaterials.Length == 0) return false;
        MeshFilter filter = mr.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return false;
        Mesh source = filter.sharedMesh;
        if (!source.isReadable) return false;
        if (source.vertexCount < 12 || source.subMeshCount < 1) return false;

        Vector3[] vertices = source.vertices;
        Vector3[] normals = source.normals;
        Bounds bounds = source.bounds;
        float roofHeight = bounds.min.y + bounds.size.y * 0.60f;
        List<int> wallTriangles = new List<int>();
        List<int> roofTriangles = new List<int>();
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            int[] triangles = source.GetTriangles(sub);
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                Vector3 a = vertices[ia], b = vertices[ib], c = vertices[ic];
                Vector3 centroid = (a + b + c) / 3f;
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                if (normals != null && normals.Length == vertices.Length)
                {
                    Vector3 avg = (normals[ia] + normals[ib] + normals[ic]).normalized;
                    if (avg.sqrMagnitude > 0.001f) normal = avg;
                }
                bool roofFace = centroid.y >= roofHeight && Mathf.Abs(normal.y) >= 0.22f;
                List<int> target = roofFace ? roofTriangles : wallTriangles;
                target.Add(ia); target.Add(ib); target.Add(ic);
            }
        }
        if (roofTriangles.Count < 3 || wallTriangles.Count < 3) return false;

        Mesh split = Object.Instantiate(source);
        split.name = source.name + "_StylizedRoofSplit";
        split.subMeshCount = 2;
        split.SetTriangles(wallTriangles, 0, true);
        split.SetTriangles(roofTriangles, 1, true);
        filter.sharedMesh = split;
        StylizedMeshCleanup cleanup = mr.GetComponent<StylizedMeshCleanup>();
        if (cleanup == null) cleanup = mr.gameObject.AddComponent<StylizedMeshCleanup>();
        cleanup.Track(source, split);
        Color[] p = palettes[Mathf.Abs(palette) % palettes.Length];
        Color wallColor = p[0];
        Color roofColor = (smallHouse && p.Length <= 4) ? Hex("202329") : p[1];
        mr.sharedMaterials = new Material[]
        {
            Get(smallHouse ? "house_plaster_small" : "house_plaster", wallColor, palette),
            Get("house_roof", roofColor, palette)
        };
        return true;
    }
}
