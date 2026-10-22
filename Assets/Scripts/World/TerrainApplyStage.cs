using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("Procedural World/Stages/5 Terrain Apply")]
public class TerrainApplyStage : WorldStage
{
    const string Folder = "Assets/WorldGenerated";

    public TerrainLayer[] customLayers = new TerrainLayer[0];
    public int alphamapResolution = 512;
    public TerrainPainterSettings paint = new TerrainPainterSettings();
    [Range(1f, 50f)] public float pixelError = 6f;
    public float basemapDistance = 700f;

    [Header("Ground Grass Details")]
    [Tooltip("Higher detail resolution places grass more uniformly. 1024 is the maximum Unity Terrain detail resolution.")]
    [Range(128, 1024)] public int grassDetailResolution = 1024;
    [Tooltip("Billboard clumps per detail cell. Each clump texture contains many blade silhouettes, giving high apparent blade density without spawning thousands of separate objects.")]
    [Range(0, 64)] public int forestGrassDensity = 64;
    [Tooltip("Keep open-zone grass lighter to preserve performance and visual contrast.")]
    [Range(0, 32)] public int openGrassDensity = 16;
    [Range(0.1f, 3f)] public float grassMinHeight = 0.9f;
    [Range(0.1f, 4f)] public float grassMaxHeight = 2.1f;
    [Range(0.1f, 3f)] public float grassMinWidth = 0.45f;
    [Range(0.1f, 3f)] public float grassMaxWidth = 1.1f;
    [Range(0f, 1f)] public float grassMaxSlope = 35f;

    public override int Order { get { return StageOrder.TerrainApply; } }
    public override string StageName { get { return "Terrain Apply"; } }

    public override void Run(WorldContext c)
    {
        TerrainData td = new TerrainData();
        td.heightmapResolution = c.Res;
        td.size = new Vector3(c.Size, c.settings.maxTerrainHeight, c.Size);
        td.SetHeights(0, 0, c.heights);
        td.terrainLayers = ResolveLayers();
        td.alphamapResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(alphamapResolution), 16, 1024);
        TerrainPainter.Paint(td, c, paint);
        ConfigureGrass(td, c);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EnsureFolder();
            string path = Folder + "/WorldTerrain.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(td, path);
            AssetDatabase.SaveAssets();
        }
#endif
        GameObject go = Terrain.CreateTerrainGameObject(td);
        go.name = "Terrain";
        go.transform.SetParent(c.root, false);
        go.transform.localPosition = new Vector3(-c.Size * 0.5f, 0f, -c.Size * 0.5f);
        Terrain t = go.GetComponent<Terrain>();
        t.heightmapPixelError = pixelError;
        t.basemapDistance = basemapDistance;
        t.drawInstanced = false;
        // Limit the draw distance of grass details: density is focused around the player,
        // avoiding the large GPU/CPU cost of drawing dense grass across the whole world.
        t.detailObjectDistance = Mathf.Clamp(c.Size * 0.16f, 90f, 150f);
        t.detailObjectDensity = 1.0f;
        if (GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial != null)
            t.materialTemplate = GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial;
        else
        {
            // In a BUILD URP returns null for defaultTerrainMaterial, so the runtime-created terrain had no material and did not render.
            Shader ts = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (ts != null) t.materialTemplate = new Material(ts) { name = "WorldTerrain (runtime)" };
            else Debug.LogError("TerrainApplyStage: 'Universal Render Pipeline/Terrain/Lit' is missing from the build, terrain will not render.");
        }
        c.terrain = t;
    }

    void ConfigureGrass(TerrainData td, WorldContext c)
    {
        // Grass details are intentionally disabled: the world now uses authored-looking
        // low-cost shrubs, fallen timber, stumps and water features instead.
        int resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(grassDetailResolution), 128, 1024);
        td.SetDetailResolution(resolution, 8);
        td.detailPrototypes = new DetailPrototype[0];
        return;
#pragma warning disable 0162
        Texture2D grassTexture = ResolveGrassTexture();
        DetailPrototype prototype = new DetailPrototype();
        prototype.prototypeTexture = grassTexture;
        prototype.renderMode = DetailRenderMode.GrassBillboard;
        prototype.usePrototypeMesh = false;
        prototype.minHeight = Mathf.Min(grassMinHeight, grassMaxHeight);
        prototype.maxHeight = Mathf.Max(grassMinHeight, grassMaxHeight);
        prototype.minWidth = Mathf.Min(grassMinWidth, grassMaxWidth);
        prototype.maxWidth = Mathf.Max(grassMinWidth, grassMaxWidth);
        prototype.noiseSpread = 0.8f;
        prototype.healthyColor = new Color(0.55f, 0.72f, 0.34f, 1f);
        prototype.dryColor = new Color(0.56f, 0.49f, 0.29f, 1f);
        td.detailPrototypes = new DetailPrototype[] { prototype };

        int[,] details = new int[resolution, resolution];
        float inv = 1f / (resolution - 1f);
        float half = c.Size * 0.5f;
        for (int z = 0; z < resolution; z++)
        {
            float v = z * inv;
            float wz = v * c.Size - half;
            for (int x = 0; x < resolution; x++)
            {
                float u = x * inv;
                float wx = u * c.Size - half;
                float height = td.GetInterpolatedHeight(u, v) / Mathf.Max(1f, td.size.y);
                float slope = td.GetSteepness(u, v);
                if (slope > grassMaxSlope || height >= paint.snowHeight - 0.03f || height >= paint.mountainRockHeight + paint.mountainRockBlend)
                    continue;

                int density = 0;
                if (c.zoneMap != null)
                {
                    ZoneType zone = c.zoneMap.ZoneAt(wx, wz);
                    if (zone == ZoneType.Forest) density = forestGrassDensity;
                    else if (zone == ZoneType.Open) density = openGrassDensity;
                    else continue;
                }
                else density = openGrassDensity;

                // Deterministic patchiness: dense clumps in forest, sparse tufts in open grassland.
                float patch = NoiseLib.Fbm(wx * 0.025f + 177f, wz * 0.025f + 353f, 3, 2f, 0.5f);
                float patchFactor = Mathf.Lerp(0.92f, 1.08f, Mathf.SmoothStep(0.12f, 0.72f, patch));
                float fine = NoiseLib.Fbm(wx * 0.11f + 89f, wz * 0.11f + 51f, 2, 2f, 0.5f);
                details[z, x] = Mathf.Clamp(Mathf.RoundToInt(density * patchFactor * Mathf.Lerp(0.94f, 1.06f, fine)), 0, 64);
            }
        }
        td.SetDetailLayer(0, 0, 0, details);
    }

    static Texture2D ResolveGrassTexture()
    {
#if UNITY_EDITOR
        string path = Folder + "/GrassDetail.png";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
#endif
        // One terrain detail instance is a billboard clump, not a single blade.
        // This texture contains 24 varied blade silhouettes, so 64 clumps per detail
        // cell produce roughly 1,500 visible blade silhouettes without creating 1,500
        // independent GameObjects/meshes per cell.
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.name = "GrassDetailGenerated";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
        {
            float ny = y / (float)(size - 1);
            for (int x = 0; x < size; x++)
            {
                float nx = x / (float)(size - 1);
                bool blade = false;
                float bladeShade = 1f;
                for (int i = 0; i < 24; i++)
                {
                    // Deterministic spread across the clump, with varied lean and width.
                    float center = Mathf.Repeat(i * 0.6180339f + 0.13f, 1f);
                    float lean = (Mathf.Repeat(i * 0.371f, 1f) - 0.5f) * 0.42f;
                    float width = 0.012f + Mathf.Repeat(i * 0.217f, 1f) * 0.012f;
                    float bladeTop = 0.48f + Mathf.Repeat(i * 0.173f, 1f) * 0.50f;
                    if (ny <= bladeTop && ny > 0.025f)
                    {
                        float t = ny / bladeTop;
                        float bladeX = center + lean * (t - 0.15f);
                        float taperedWidth = width * (1f - t * 0.78f);
                        if (Mathf.Abs(nx - bladeX) <= taperedWidth)
                        {
                            blade = true;
                            bladeShade = 0.78f + 0.22f * Mathf.PerlinNoise(i * 0.37f, ny * 7.3f);
                            break;
                        }
                    }
                }
                if (!blade) { tex.SetPixel(x, y, clear); continue; }
                Color green = Color.Lerp(new Color(0.12f, 0.25f, 0.045f), new Color(0.48f, 0.68f, 0.22f), ny) * bladeShade;
                tex.SetPixel(x, y, new Color(green.r, green.g, green.b, 1f));
            }
        }
        tex.Apply(true, false);
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EnsureFolder();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.Refresh();
            Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (imported != null) return imported;
        }
#endif
        return tex;
    }

    TerrainLayer[] ResolveLayers()
    {
        if (customLayers != null && customLayers.Length >= 4 && customLayers[0] != null && customLayers[1] != null && customLayers[2] != null && customLayers[3] != null)
            return new TerrainLayer[] { customLayers[0], customLayers[1], customLayers[2], customLayers[3] };
        string[] names = { "Grass", "Dirt", "Rock", "Snow" };
        Color[] colors = { new Color(0.30f, 0.43f, 0.17f), new Color(0.38f, 0.29f, 0.20f), new Color(0.39f, 0.39f, 0.38f), new Color(0.82f, 0.85f, 0.88f) };
        TerrainLayer[] r = new TerrainLayer[4];
        for (int i = 0; i < 4; i++) r[i] = GetLayer(names[i], colors[i]);
        if (r[2] != null) { r[2].tileSize = new Vector2(9f, 9f); r[2].normalScale = 0.3f; }
        return r;
    }

    static Texture2D MakeGroundTexture(string name, Color baseColor, float contrast, int size = 512)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        tex.name = "Layer_" + name + "_Tex";
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 8;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)size;
            float v = y / (float)size;
            float broad = Mathf.PerlinNoise(u * 4.2f + 1.7f, v * 4.2f + 9.1f);
            float fine = Mathf.PerlinNoise(u * 47f + 11.7f, v * 47f + 4.3f);
            float mid = Mathf.PerlinNoise(u * 17f + 17.3f, v * 17f + 2.1f);
            Color pixel;

            if (name == "Grass")
            {
                // Painterly cartoon grass: large soft clumps, smaller leaf-like strokes,
                // and occasional tiny flowers. This is color texture detail, not gloss/noise.
                float clump = Mathf.PerlinNoise(u * 5.5f + 3.1f, v * 5.5f + 8.7f);
                float leafWarp = Mathf.PerlinNoise(u * 12f + 5.3f, v * 12f + 2.9f);
                float bladeLine = Mathf.Abs(Mathf.Sin((u * 126f + v * 39f + leafWarp * 2.4f) * Mathf.PI));
                float bladeMask = (bladeLine < 0.17f && fine > 0.39f) ? 1f : 0f;
                float shadowMask = clump < 0.34f ? 1f : 0f;
                Color deep = new Color(baseColor.r * 0.68f, baseColor.g * 0.80f, baseColor.b * 0.66f, 1f);
                Color fresh = new Color(Mathf.Min(1f, baseColor.r * 1.38f), Mathf.Min(1f, baseColor.g * 1.30f), Mathf.Min(1f, baseColor.b * 1.22f), 1f);
                Color sunny = new Color(Mathf.Min(1f, baseColor.r * 1.58f), Mathf.Min(1f, baseColor.g * 1.48f), Mathf.Min(1f, baseColor.b * 1.34f), 1f);
                float clumpBlend = Mathf.Clamp01(0.18f + clump * 0.70f + (broad - 0.5f) * 0.32f);
                pixel = Color.Lerp(deep, fresh, clumpBlend);
                if (shadowMask > 0f) pixel = Color.Lerp(pixel, deep, 0.20f);
                if (bladeMask > 0f) pixel = Color.Lerp(pixel, sunny, 0.48f);

                // Small cream and blush flowers, distributed in a deterministic tile grid.
                // Keep them sparse so the terrain reads as grass first, not confetti.
                int gx = Mathf.FloorToInt(u * 38f);
                int gy = Mathf.FloorToInt(v * 38f);
                float cellU = u * 38f - gx;
                float cellV = v * 38f - gy;
                float hashA = Mathf.Repeat(Mathf.Sin(gx * 127.1f + gy * 311.7f) * 43758.5453f, 1f);
                float hashB = Mathf.Repeat(Mathf.Sin(gx * 269.5f + gy * 183.3f) * 24634.6345f, 1f);
                float flowerX = 0.18f + hashA * 0.64f;
                float flowerY = 0.18f + hashB * 0.64f;
                float flowerDist = Vector2.Distance(new Vector2(cellU, cellV), new Vector2(flowerX, flowerY));
                if (hashA > 0.76f && flowerDist < 0.052f)
                {
                    Color flower = hashB > 0.60f ? new Color(1f, 0.83f, 0.70f, 1f) : new Color(0.98f, 0.94f, 0.76f, 1f);
                    pixel = Color.Lerp(pixel, flower, Mathf.Clamp01((0.052f - flowerDist) * 24f));
                }
            }
            else
            {
                float grain = (broad - 0.5f) * contrast + (mid - 0.5f) * contrast * 0.58f + (fine - 0.5f) * contrast * 0.25f;
                float pattern = 0f;
                if (name == "Rock")
                {
                    float band = Mathf.PerlinNoise(u * 8f + v * 2.4f, v * 14f);
                    float facet = Mathf.PerlinNoise(u * 21f + 7.1f, v * 21f + 2.4f);
                    pattern = (band > 0.64f ? -0.15f : 0.015f) + (facet > 0.72f ? 0.08f : 0f);
                }
                else if (name == "Dirt")
                {
                    pattern = fine > 0.76f ? -0.08f : (fine < 0.22f ? 0.055f : 0f);
                }
                else if (name == "Snow")
                {
                    grain *= 0.45f;
                    pattern = mid < 0.35f ? -0.045f : (fine > 0.84f ? 0.018f : 0f);
                }
                float k = Mathf.Clamp(1f + grain + pattern, 0.52f, 1.38f);
                k = Mathf.Round(k * 12f) / 12f;
                pixel = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, 1f);
            }
            tex.SetPixel(x, y, pixel);
        }
        // Blend opposite edges before mipmaps are generated to suppress seams when layers repeat.
        BlendGroundTextureEdges(tex, 40);
        tex.Apply(true, false);
        return tex;
    }

    static Texture2D MakeTerrainNormalMap(Texture2D albedo, string layerName)
    {
        if (albedo == null) return null;
        int size = albedo.width;
        Color[] src = albedo.GetPixels();
        float[] heights = new float[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            Color c = src[i];
            float luminance = c.r * 0.24f + c.g * 0.66f + c.b * 0.10f;
            float raised = 0f;
            if (layerName == "Grass")
            {
                // The generated cream/blush flower pixels are deliberately raised above
                // the surrounding leaf strokes; grass blade highlights add a gentler bump.
                bool flower = c.r > 0.72f && c.g > 0.62f && c.b > 0.48f;
                raised = flower ? 0.22f : Mathf.Clamp01((c.g - c.r) * 1.5f) * 0.055f;
            }
            else if (layerName == "Rock") raised = (luminance - 0.4f) * 0.10f;
            else raised = (luminance - 0.4f) * 0.04f;
            heights[i] = Mathf.Clamp01(0.45f + luminance * 0.18f + raised);
        }

        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
        normal.name = "Layer_" + layerName + "_NormalGenerated";
        normal.wrapMode = TextureWrapMode.Repeat;
        normal.filterMode = FilterMode.Trilinear;
        normal.anisoLevel = 8;
        float strength = layerName == "Grass" ? 7.0f : layerName == "Rock" ? 3.2f : 1.8f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int i = y * size + x;
            float dx = heights[y * size + (x + 1) % size] - heights[y * size + (x + size - 1) % size];
            float dy = heights[((y + 1) % size) * size + x] - heights[((y + size - 1) % size) * size + x];
            Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
            normal.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
        }
        normal.Apply(true, true);
        return normal;
    }

    static void BlendGroundTextureEdges(Texture2D tex, int edgeWidth)
    {
        if (tex == null) return;
        int size = tex.width;
        int edge = Mathf.Clamp(edgeWidth, 2, size / 8);
        Color[] pixels = tex.GetPixels();
        for (int y = 0; y < size; y++)
        for (int d = 0; d < edge; d++)
        {
            int left = y * size + d;
            int right = y * size + (size - 1 - d);
            float blend = 1f - d / (float)edge;
            Color shared = (pixels[left] + pixels[right]) * 0.5f;
            pixels[left] = Color.Lerp(pixels[left], shared, blend);
            pixels[right] = Color.Lerp(pixels[right], shared, blend);
        }
        for (int x = 0; x < size; x++)
        for (int d = 0; d < edge; d++)
        {
            int bottom = d * size + x;
            int top = (size - 1 - d) * size + x;
            float blend = 1f - d / (float)edge;
            Color shared = (pixels[bottom] + pixels[top]) * 0.5f;
            pixels[bottom] = Color.Lerp(pixels[bottom], shared, blend);
            pixels[top] = Color.Lerp(pixels[top], shared, blend);
        }
        tex.SetPixels(pixels);
    }

    static TerrainLayer NewLayer(string name, Color color)
    {
        float contrast = name == "Rock" ? 0.58f : name == "Dirt" ? 0.44f : name == "Snow" ? 0.20f : 0.48f;
        Texture2D tex = MakeGroundTexture(name, color, contrast);
        TerrainLayer layer = new TerrainLayer();
        layer.name = "Layer_" + name;
        layer.diffuseTexture = tex;
        // TerrainLayer has no independent height slot in URP. Derive a tangent-space
        // normal map from the generated albedo, giving grass blades and bright flower
        // heads a gentle raised look without spawning extra meshes.
        layer.normalMapTexture = MakeTerrainNormalMap(tex, name);
        layer.tileSize = name == "Rock" ? new Vector2(26f, 26f) : name == "Grass" ? new Vector2(16f, 16f) : name == "Dirt" ? new Vector2(18f, 18f) : new Vector2(24f, 24f);
        layer.smoothness = 0f;
        layer.metallic = 0f;
        return layer;
    }

    static TerrainLayer GetLayer(string name, Color color)
    {
#if UNITY_EDITOR
        string path = Folder + "/Layer_" + name + ".terrainlayer";
        TerrainLayer existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (existing != null)
        {
            // Refresh generated terrain layers during editor regeneration so existing worlds
            // receive the stronger stylized textures instead of retaining previous flat maps.
            if (!Application.isPlaying)
            {
                string texPath = Folder + "/Layer_" + name + "_Tex.asset";
                Texture2D texture = MakeGroundTexture(name, color, name == "Rock" ? 0.58f : name == "Dirt" ? 0.44f : name == "Snow" ? 0.20f : 0.48f);
                UnityEngine.Object oldTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (oldTexture != null) AssetDatabase.DeleteAsset(texPath);
                AssetDatabase.CreateAsset(texture, texPath);
                existing.diffuseTexture = texture;
                string normalPath = Folder + "/Layer_" + name + "_Normal.asset";
                Texture2D normalTexture = MakeTerrainNormalMap(texture, name);
                UnityEngine.Object oldNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (oldNormal != null) AssetDatabase.DeleteAsset(normalPath);
                AssetDatabase.CreateAsset(normalTexture, normalPath);
                existing.normalMapTexture = normalTexture;
                existing.tileSize = name == "Rock" ? new Vector2(26f, 26f) : name == "Grass" ? new Vector2(16f, 16f) : name == "Dirt" ? new Vector2(18f, 18f) : new Vector2(24f, 24f);
                existing.smoothness = 0f;
                existing.metallic = 0f;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
            }
            return existing;
        }
        if (!Application.isPlaying)
        {
            EnsureFolder();
            TerrainLayer layer = NewLayer(name, color);
            string texPath = Folder + "/Layer_" + name + "_Tex.asset";
            string normalPath = Folder + "/Layer_" + name + "_Normal.asset";
            AssetDatabase.CreateAsset(layer.diffuseTexture, texPath);
            if (layer.normalMapTexture != null) AssetDatabase.CreateAsset(layer.normalMapTexture, normalPath);
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }
#endif
        return NewLayer(name, color);
    }

#if UNITY_EDITOR
    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "WorldGenerated");
    }
#endif
}
