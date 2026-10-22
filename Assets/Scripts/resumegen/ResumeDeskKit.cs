using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Procedural stylized textures + props for the 3D resume desk.</summary>
public static class DeskKit
{
    static Material baseMat;
    static float N(float x, float y, float s) { return Mathf.PerlinNoise(x * s + 17.3f, y * s + 4.1f); }
    static float Hash(float x, float y) { float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }
    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    public static Texture2D Make(int w, int h, Func<float, float, Color> f, TextureWrapMode wrap = TextureWrapMode.Repeat)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
        t.SetPixels(px); t.wrapMode = wrap; t.filterMode = FilterMode.Bilinear; t.anisoLevel = 4; t.Apply(true);
        return t;
    }

    static float Toon(float v, int levels, float amt) { return Mathf.Lerp(v, Mathf.Round(v * levels) / levels, amt); }

    // ---------- textures ----------
    public static Texture2D Wood()
    {
        Color lt = C(0.88f, 0.68f, 0.44f), dk = C(0.66f, 0.46f, 0.27f);
        return Make(768, 768, (u, v) =>
        {
            const int planks = 8;
            float pv = v * planks; int pi = Mathf.FloorToInt(pv); float lv = pv - pi;
            float g = N(u * 2f + pi * 0.37f, pv * 9f, 1f);
            float f = N(u * 9f + pi, pv * 30f, 1f);
            float ring = Mathf.Abs(Mathf.Sin((g * 5f + lv * 1.5f) * Mathf.PI));
            float val = Toon(Mathf.Clamp01(0.45f * g + 0.3f * ring + 0.25f * f), 5, 0.55f);
            Color c = Color.Lerp(dk, lt, val) * (0.92f + 0.08f * ((pi * 37) % 5) / 4f);
            if (lv < 0.028f || lv > 0.972f) c *= 0.55f;
            float joint = Mathf.Abs(Mathf.Repeat(u * 1.5f + pi * 0.31f, 1f) - 0.5f);
            if (joint < 0.003f) c *= 0.6f;
            c.a = 1f; return c;
        });
    }

    public static Texture2D Paper()
    {
        return Make(512, 724, (u, v) =>
        {
            float fib = (N(u, v, 90f) - 0.5f) * 0.035f + (N(u, v, 300f) - 0.5f) * 0.02f;
            float speck = Hash(Mathf.Floor(u * 400f), Mathf.Floor(v * 560f)) > 0.996f ? -0.07f : 0f;
            Color c = C(0.995f, 0.975f, 0.925f) + new Color(fib + speck, fib + speck, fib + speck, 0f);
            float e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            if (e < 0.025f) c *= 0.93f + e * 2.8f;
            c *= 1f - 0.04f * (1f - v);
            c.a = 1f; return c;
        }, TextureWrapMode.Clamp);
    }

    public static Texture2D Cloth(Color baseC)
    {
        return Make(128, 128, (u, v) =>
        {
            float weave = Mathf.Sin(u * 380f) * Mathf.Sin(v * 380f) * 0.035f;
            float wr = (N(u, v, 5f) - 0.5f) * 0.22f;
            Color c = baseC * (1f + weave + wr); c.a = 1f; return c;
        });
    }

    public static Texture2D Soft(Color c0, float noise = 0.1f)
    {
        return Make(64, 64, (u, v) => { Color c = c0 * (1f + (N(u, v, 6f) - 0.5f) * noise + (v - 0.5f) * 0.1f); c.a = 1f; return c; });
    }

    public static Texture2D Stripes(Color c0, float count, float depth)
    {
        return Make(128, 64, (u, v) => { float f = Mathf.Abs(Mathf.Sin(u * Mathf.PI * count)); Color c = c0 * (1f - depth + depth * f); c.a = 1f; return c; });
    }

    public static Texture2D Leaf()
    {
        return Make(64, 64, (u, v) =>
        {
            Color c = Color.Lerp(C(0.20f, 0.52f, 0.27f), C(0.46f, 0.80f, 0.38f), Toon(v, 3, 0.6f));
            if (Mathf.Abs(u - 0.5f) < 0.03f) c = Color.Lerp(c, C(0.75f, 0.95f, 0.6f), 0.6f);
            c.a = 1f; return c;
        });
    }

    public static Texture2D Terracotta()
    {
        return Make(128, 64, (u, v) =>
        {
            Color c = C(0.82f, 0.46f, 0.30f) * (1f + (N(u, v, 14f) - 0.5f) * 0.18f);
            if (v > 0.82f) c *= 1.08f;
            if (Mathf.Abs(v - 0.8f) < 0.015f) c *= 0.75f;
            c.a = 1f; return c;
        });
    }

    public static Texture2D Coffee()
    {
        return Make(128, 128, (u, v) =>
        {
            float dx = u - 0.5f, dy = v - 0.5f; float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float sw = Mathf.Sin(Mathf.Atan2(dy, dx) * 2f + r * 7f) * 0.5f + 0.5f;
            Color c = Color.Lerp(C(0.56f, 0.34f, 0.18f), C(0.24f, 0.13f, 0.07f), Mathf.Clamp01(r * 0.9f + sw * 0.25f));
            c.a = 1f; return c;
        });
    }

    public static Texture2D Glass()
    {
        return Make(64, 128, (u, v) =>
        {
            float g = Mathf.Clamp01(1f - Mathf.Abs((u + v) - 0.9f) * 7f) * 0.13f;
            Color c = C(0.05f, 0.06f, 0.09f) + new Color(g, g, g * 1.2f, 0f); c.a = 1f; return c;
        });
    }

    public static Texture2D Ruler()
    {
        return Make(256, 32, (u, v) =>
        {
            Color c = C(0.72f, 0.88f, 0.96f);
            float t = Mathf.Repeat(u * 32f, 1f); bool big = Mathf.Repeat(u * 6.4f, 1f) < 0.04f;
            if (v > (big ? 0.35f : 0.65f) && t < 0.06f) c = C(0.15f, 0.25f, 0.4f);
            c.a = 1f; return c;
        });
    }

    public static Texture2D Blob()
    {
        return Make(64, 64, (u, v) =>
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            return new Color(0f, 0f, 0f, Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f));
        }, TextureWrapMode.Clamp);
    }

    // ---------- materials / primitives ----------
    public static Material Mat(Color c, Texture2D tex = null, float gloss = 0.12f)
    {
        if (baseMat == null) baseMat = WorldUtil.DefaultLit();   // NOT the primitive's default material: that is the (invisible) built-in Standard shader in a build
        var m = new Material(baseMat); m.color = c;
        if (tex != null) m.mainTexture = tex;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", gloss);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        return m;
    }

    public static Material Sprite(Texture2D tex, Color c, int queue)
    {
        Shader sh = Shader.Find("Sprites/Default"); if (sh == null) sh = Shader.Find("Unlit/Transparent");
        var m = new Material(sh); if (tex != null) m.mainTexture = tex; m.color = c; m.renderQueue = queue; return m;
    }

    public static Transform P(PrimitiveType type, string name, Transform parent, Material m, Vector3 lp, Vector3 euler, Vector3 ls, bool shadow = true)
    {
        var go = GameObject.CreatePrimitive(type);
        var col = go.GetComponent<Collider>(); if (col != null) WorldUtil.Kill(col);
        go.name = name; var t = go.transform; t.SetParent(parent, false);
        t.localPosition = lp; t.localEulerAngles = euler; t.localScale = ls;
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = m;
        r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return t;
    }

    /// <summary>capsule between two local points</summary>
    public static Transform Limb(Transform parent, string name, Material m, Vector3 a, Vector3 b, float r, bool shadow = false)
    {
        Vector3 d = b - a;
        var t = P(PrimitiveType.Capsule, name, parent, m, (a + b) * 0.5f, Vector3.zero, new Vector3(2f * r, d.magnitude * 0.5f, 2f * r), shadow);
        t.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        return t;
    }

    /// <summary>re-stretch a capsule between two WORLD points (parent must be unscaled)</summary>
    public static void Seg(Transform t, Vector3 a, Vector3 b, float r)
    {
        Vector3 d = b - a; if (d.sqrMagnitude < 1e-8f) return;
        t.position = (a + b) * 0.5f; t.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        t.localScale = new Vector3(2f * r, d.magnitude * 0.5f, 2f * r);
    }

    static Mesh coneMesh;
    public static Mesh Cone()
    {
        if (coneMesh != null) return coneMesh;
        const int n = 20; var v = new Vector3[n + 1]; var tri = new List<int>();
        v[0] = Vector3.zero;
        for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2f / n; v[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 1f); }
        for (int i = 0; i < n; i++) { tri.Add(0); tri.Add(((i + 1) % n) + 1); tri.Add(i + 1); }
        coneMesh = new Mesh { name = "DeskCone", vertices = v, triangles = tri.ToArray() };
        coneMesh.RecalculateNormals(); coneMesh.RecalculateBounds();
        return coneMesh;
    }

    public static Transform ConePart(Transform parent, Material m, float z0, float len, float radius)
    {
        var go = new GameObject("cone"); go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0, 0, z0); go.transform.localScale = new Vector3(radius, radius, len);
        go.AddComponent<MeshFilter>().sharedMesh = Cone();
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
        return go.transform;
    }

    /// <summary>cylinder lying along local +Z from z0 to z1</summary>
    public static Transform Rod(Transform parent, Material m, float z0, float z1, float radius, bool shadow = false)
    {
        return P(PrimitiveType.Cylinder, "rod", parent, m, new Vector3(0, 0, (z0 + z1) * 0.5f), new Vector3(90, 0, 0), new Vector3(radius * 2f, (z1 - z0) * 0.5f, radius * 2f), shadow);
    }

    // ---------- scene ----------
    public static GameObject Table(Transform parent)
    {
        var m = Mat(Color.white, Wood(), 0.22f);
        var t = P(PrimitiveType.Quad, "Table", parent, m, Vector3.zero, new Vector3(90, 0, 0), new Vector3(1.4f, 1.4f, 1f), false);
        return t.gameObject;
    }

    public static Transform PaperSheet(Transform parent, float w, float h, float thick)
    {
        var t = P(PrimitiveType.Cube, "Paper", parent, Mat(Color.white, Paper(), 0.05f), new Vector3(0, thick * 0.5f, 0), Vector3.zero, new Vector3(w, thick, h));
        return t;
    }

    public static void Accessories(Transform parent, Camera cam, float halfPaperW, System.Random rnd, Font font)
    {
        var all = new List<KeyValuePair<Action<Transform, System.Random>, float>>
        {
            new KeyValuePair<Action<Transform, System.Random>, float>(Notebook, 0.085f),
            new KeyValuePair<Action<Transform, System.Random>, float>(Sticky, 0.045f),
            new KeyValuePair<Action<Transform, System.Random>, float>(Highlighter, 0.06f),
            new KeyValuePair<Action<Transform, System.Random>, float>(Pen, 0.075f),
            new KeyValuePair<Action<Transform, System.Random>, float>(Plant, 0.05f),
            new KeyValuePair<Action<Transform, System.Random>, float>(Phone, 0.08f),
            new KeyValuePair<Action<Transform, System.Random>, float>(Mug, 0.055f),
            new KeyValuePair<Action<Transform, System.Random>, float>(Eraser, 0.025f),
            new KeyValuePair<Action<Transform, System.Random>, float>(RulerProp, 0.085f),
        };
        for (int i = all.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var t = all[i]; all[i] = all[j]; all[j] = t; }

        float dist = cam.transform.position.y;
        float halfH = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), halfW = halfH * cam.aspect;
        float inner = halfPaperW + 0.02f;
        var placed = new List<Vector3>();
        Broke(parent, font, placed);
        int count = Mathf.Min(3, all.Count);
        for (int i = 0; i < count; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f, r = all[i].Value;
            float span = Mathf.Max(0.001f, halfW - inner - 2f * r - 0.01f);
            bool ok = false; float x = 0f, z = 0f;
            for (int tries = 0; tries < 60 && !ok; tries++)
            {
                x = side * (inner + r + (float)rnd.NextDouble() * span);
                z = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Max(0.001f, halfH - r - 0.01f);
                ok = !Blocked(x, z);
                for (int k = 0; k < placed.Count; k++)
                {
                    float dx = placed[k].x - x, dz = placed[k].y - z;
                    if (Mathf.Sqrt(dx * dx + dz * dz) < r + placed[k].z + 0.006f) { ok = false; break; }
                }
            }
            if (!ok) continue;
            var root = new GameObject("Prop").transform; root.SetParent(parent, false);
            root.localPosition = new Vector3(x, 0f, z); root.localEulerAngles = new Vector3(0f, (float)rnd.NextDouble() * 360f, 0f);
            all[i].Key(root, rnd); placed.Add(new Vector3(x, z, r));
        }
    }

    // keep the areas where the hands / sleeves / lamp live free of props
    static bool Blocked(float x, float z)
    {
        if (x < -0.10f && x > -0.27f && z < 0.06f) return true;          // left hand + sleeve
        if (x > 0.105f && x < 0.20f && z < 0.03f) return true;           // right forearm
        if (x > 0.09f && z < 0.09f) return true;
        if (x < -0.18f && z > 0.08f) return true;                        // lamp base
        return false;
    }

    /// <summary>closed room (floor, walls, ceiling) around the desk + a desk lamp whose base is visible in the top-left corner</summary>
    public static Vector3 RoomAndLamp(Transform parent)
    {
        Material wall = Mat(Color.white, Soft(new Color(0.42f, 0.30f, 0.26f), 0.12f), 0.05f);
        Material floor = Mat(Color.white, Soft(new Color(0.22f, 0.15f, 0.11f), 0.15f), 0.05f);
        float e = 1.4f, fy = -0.72f, cy = 1.1f, h = cy - fy;
        P(PrimitiveType.Quad, "Floor", parent, floor, new Vector3(0, fy, 0), new Vector3(90, 0, 0), new Vector3(2 * e, 2 * e, 1), false);
        P(PrimitiveType.Quad, "Ceiling", parent, wall, new Vector3(0, cy, 0), new Vector3(-90, 0, 0), new Vector3(2 * e, 2 * e, 1), false);
        P(PrimitiveType.Quad, "WallN", parent, wall, new Vector3(0, fy + h / 2, e), new Vector3(0, 0, 0), new Vector3(2 * e, h, 1), false);
        P(PrimitiveType.Quad, "WallS", parent, wall, new Vector3(0, fy + h / 2, -e), new Vector3(0, 180, 0), new Vector3(2 * e, h, 1), false);
        P(PrimitiveType.Quad, "WallE", parent, wall, new Vector3(e, fy + h / 2, 0), new Vector3(0, 90, 0), new Vector3(2 * e, h, 1), false);
        P(PrimitiveType.Quad, "WallW", parent, wall, new Vector3(-e, fy + h / 2, 0), new Vector3(0, -90, 0), new Vector3(2 * e, h, 1), false);

        // lamp: round base + arm rising to a head hanging above-left of the sheet (head is out of frame, its light isn't)
        Material metal = Mat(new Color(0.16f, 0.17f, 0.2f), null, 0.45f), brass = Mat(new Color(0.85f, 0.68f, 0.3f), null, 0.5f);
        var lamp = new GameObject("Desk Lamp").transform; lamp.SetParent(parent, false); lamp.localPosition = new Vector3(-0.235f, 0f, 0.125f);
        P(PrimitiveType.Cylinder, "base", lamp, metal, new Vector3(0, 0.006f, 0), Vector3.zero, new Vector3(0.075f, 0.006f, 0.075f));
        P(PrimitiveType.Cylinder, "ring", lamp, brass, new Vector3(0, 0.0125f, 0), Vector3.zero, new Vector3(0.03f, 0.0015f, 0.03f), false);
        Limb(lamp, "arm1", metal, new Vector3(0, 0.012f, 0), new Vector3(0.02f, 0.16f, -0.005f), 0.005f, true);
        Limb(lamp, "arm2", metal, new Vector3(0.02f, 0.16f, -0.005f), new Vector3(0.07f, 0.215f, -0.02f), 0.005f, true);
        return new Vector3(-0.165f, 0.225f, 0.105f);   // lamp head position (world, desk root is at the origin)
    }

    static Transform At(Transform parent, List<Vector3> placed, string n, float x, float z, float yaw, float r)
    {
        var t = new GameObject(n).transform; t.SetParent(parent, false);
        t.localPosition = new Vector3(x, 0f, z); t.localEulerAngles = new Vector3(0f, yaw, 0f);
        placed.Add(new Vector3(x, z, r));
        return t;
    }

    // The default font material ignores depth (text drew over the pen hand). Depth-tested clone, kept in sync with the font atlas.
    static readonly Dictionary<Font, Material> textMats = new Dictionary<Font, Material>();
    static Material TextMat(Font f)
    {
        Material m;
        if (textMats.TryGetValue(f, out m) && m != null) return m;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) return f.material;
        m = new Material(sh) { mainTexture = f.material.mainTexture, renderQueue = 3000 };
        textMats[f] = m;
        Font.textureRebuilt += rf => { Material mm; if (textMats.TryGetValue(rf, out mm) && mm != null) mm.mainTexture = rf.material.mainTexture; };
        return m;
    }

    static void Txt(Transform parent, Font f, string s, Vector3 lp, float yaw, float h, Color c)
    {
        if (f == null) return;
        var go = new GameObject("txt"); go.transform.SetParent(parent, false);
        go.transform.localPosition = lp; go.transform.localEulerAngles = new Vector3(90f, yaw, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.font = f; tm.text = s; tm.fontSize = 64; tm.characterSize = h / 6.4f;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = c;
        go.GetComponent<MeshRenderer>().sharedMaterial = TextMat(f);
    }

    static void Broke(Transform parent, Font f, List<Vector3> placed)
    {
        Wallet(At(parent, placed, "Empty Wallet", 0.185f, 0.108f, -12f, 0.055f), f);
        var fly = new GameObject("Fly").AddComponent<DeskFly>();
        fly.Init(parent, new Vector3(0.185f, 0f, 0.108f));
        RentNote(At(parent, placed, "Rent Note", -0.155f, 0.085f, 10f, 0.04f), f);
        Envelope(At(parent, placed, "Final Notice", -0.155f, 0.137f, -8f, 0.035f), f);
        NoodleCup(At(parent, placed, "Empty Noodle Cup", 0.24f, 0.052f, 25f, 0.035f));
        var c = At(parent, placed, "Coins", 0.265f, 0.088f, 0f, 0.016f);
        Material cu = Mat(C(0.72f, 0.45f, 0.25f), null, 0.5f);
        P(PrimitiveType.Cylinder, "coin", c, cu, new Vector3(0f, 0.00075f, 0f), Vector3.zero, new Vector3(0.018f, 0.00075f, 0.018f), false);
        P(PrimitiveType.Cylinder, "coin", c, cu, new Vector3(0.012f, 0.00075f, -0.011f), Vector3.zero, new Vector3(0.018f, 0.00075f, 0.018f), false);
        Material pap = Mat(Color.white, Soft(C(0.93f, 0.92f, 0.88f), 0.2f), 0.05f);
        P(PrimitiveType.Sphere, "ball", At(parent, placed, "Crumpled Paper", 0.145f, 0.045f, 0f, 0.014f), pap, new Vector3(0f, 0.01f, 0f), new Vector3(20f, 40f, 10f), new Vector3(0.026f, 0.022f, 0.024f));
        P(PrimitiveType.Sphere, "ball", At(parent, placed, "Crumpled Paper", 0.262f, 0.135f, 0f, 0.014f), pap, new Vector3(0f, 0.009f, 0f), new Vector3(60f, 10f, 30f), new Vector3(0.022f, 0.019f, 0.021f));
    }

    static void Wallet(Transform root, Font f)
    {
        Material leather = Mat(Color.white, Soft(C(0.36f, 0.2f, 0.1f), 0.12f), 0.3f), lining = Mat(C(0.55f, 0.42f, 0.3f), null, 0.1f), edge = Mat(C(0.5f, 0.3f, 0.16f), null, 0.2f);
        P(PrimitiveType.Cube, "wallet", root, leather, new Vector3(0f, 0.0035f, 0f), Vector3.zero, new Vector3(0.095f, 0.007f, 0.064f));
        P(PrimitiveType.Cube, "lining", root, lining, new Vector3(0f, 0.0072f, 0f), Vector3.zero, new Vector3(0.088f, 0.0004f, 0.057f), false);
        P(PrimitiveType.Cube, "fold", root, leather, new Vector3(0f, 0.0076f, 0f), Vector3.zero, new Vector3(0.0025f, 0.0012f, 0.064f), false);
        for (int i = 0; i < 3; i++)
            P(PrimitiveType.Cube, "slot", root, edge, new Vector3(-0.022f, 0.0078f, -0.018f + i * 0.018f), Vector3.zero, new Vector3(0.04f, 0.0012f, 0.0022f), false);
        P(PrimitiveType.Cube, "flap", root, edge, new Vector3(0.023f, 0.0082f, -0.026f), Vector3.zero, new Vector3(0.04f, 0.0014f, 0.008f), false);
        Txt(root, f, "$0.00", new Vector3(0.023f, 0.0086f, 0.004f), 0f, 0.007f, C(0.25f, 0.15f, 0.1f));
    }

    /// <summary>Clickable "MAIN MENU" sticky-note button for the desk (same sticky-note look as the rent note). Returns the root; collider is on the top note.</summary>
    public static Transform BackNote(Transform parent, Font f, Vector3 localPos)
    {
        var root = new GameObject("Back To Menu Note").transform; root.SetParent(parent, false);
        root.localPosition = localPos; root.localEulerAngles = new Vector3(0f, -7f, 0f);
        P(PrimitiveType.Cube, "n1", root, Mat(Color.white, Soft(C(0.6f, 0.85f, 1f), 0.05f), 0.05f), new Vector3(0.002f, 0.001f, -0.002f), new Vector3(0f, 12f, 0f), new Vector3(0.07f, 0.002f, 0.045f));
        var top = P(PrimitiveType.Cube, "n2", root, Mat(Color.white, Soft(C(1f, 0.88f, 0.42f), 0.05f), 0.05f), new Vector3(0f, 0.003f, 0f), new Vector3(0f, -3f, 0f), new Vector3(0.07f, 0.002f, 0.045f));
        P(PrimitiveType.Cube, "tape", root, Mat(C(0.95f, 0.93f, 0.8f)), new Vector3(0f, 0.0042f, 0.022f), new Vector3(0f, 4f, 0f), new Vector3(0.026f, 0.0004f, 0.008f), false);
        Txt(root, f, "< MAIN MENU", new Vector3(0f, 0.0046f, -0.002f), -3f, 0.0085f, C(0.12f, 0.1f, 0.14f));
        // P() destroys the primitive's collider (deferred), so add a fresh one; taller than the note so the ray always hits it
        var col = top.gameObject.AddComponent<BoxCollider>(); col.size = new Vector3(1f, 6f, 1f);
        top.gameObject.AddComponent<BackNoteTag>();
        return root;
    }

    static void RentNote(Transform root, Font f)
    {
        P(PrimitiveType.Cube, "n1", root, Mat(Color.white, Soft(C(1f, 0.58f, 0.74f), 0.05f), 0.05f), new Vector3(0.002f, 0.001f, -0.002f), new Vector3(0f, -14f, 0f), new Vector3(0.05f, 0.002f, 0.05f));
        P(PrimitiveType.Cube, "n2", root, Mat(Color.white, Soft(C(1f, 0.88f, 0.42f), 0.05f), 0.05f), new Vector3(0f, 0.003f, 0f), new Vector3(0f, 6f, 0f), new Vector3(0.05f, 0.002f, 0.05f));
        Txt(root, f, "RENT DUE\nIN 1 DAY", new Vector3(0f, 0.0041f, 0f), 6f, 0.0085f, C(0.75f, 0.08f, 0.08f));
    }

    static void Envelope(Transform root, Font f)
    {
        P(PrimitiveType.Cube, "env", root, Mat(C(0.95f, 0.93f, 0.86f)), new Vector3(0f, 0.001f, 0f), Vector3.zero, new Vector3(0.056f, 0.002f, 0.037f));
        P(PrimitiveType.Cube, "band", root, Mat(C(0.8f, 0.1f, 0.1f)), new Vector3(0f, 0.0021f, 0.0125f), Vector3.zero, new Vector3(0.056f, 0.0004f, 0.009f), false);
        Txt(root, f, "FINAL NOTICE", new Vector3(0f, 0.0026f, 0.0125f), 0f, 0.0055f, Color.white);
        Txt(root, f, "OVERDUE", new Vector3(0f, 0.0026f, -0.005f), 0f, 0.007f, C(0.75f, 0.08f, 0.08f));
    }

    static void NoodleCup(Transform root)
    {
        P(PrimitiveType.Cylinder, "cup", root, Mat(C(0.85f, 0.18f, 0.14f), null, 0.3f), new Vector3(0f, 0.0225f, 0f), Vector3.zero, new Vector3(0.056f, 0.0225f, 0.056f));
        P(PrimitiveType.Cylinder, "rim", root, Mat(C(0.95f, 0.95f, 0.92f)), new Vector3(0f, 0.0455f, 0f), Vector3.zero, new Vector3(0.06f, 0.0012f, 0.06f), false);
        P(PrimitiveType.Cylinder, "inside", root, Mat(C(0.55f, 0.35f, 0.15f)), new Vector3(0f, 0.0462f, 0f), Vector3.zero, new Vector3(0.048f, 0.0004f, 0.048f), false);
        Material wood = Mat(C(0.8f, 0.62f, 0.38f));
        for (int i = -1; i <= 1; i += 2)
        {
            var a = new GameObject("chopstick").transform; a.SetParent(root, false);
            a.localPosition = new Vector3(i * 0.006f, 0.0485f, 0f); a.localEulerAngles = new Vector3(0f, i * 8f, 0f);
            Rod(a, wood, -0.03f, 0.05f, 0.0014f);
        }
    }

    static Color Pick(System.Random r, params Color[] cs) { return cs[r.Next(cs.Length)]; }

    static void Notebook(Transform root, System.Random r)
    {
        Color cover = Pick(r, C(0.15f, 0.17f, 0.24f), C(0.55f, 0.18f, 0.20f), C(0.18f, 0.40f, 0.36f));
        P(PrimitiveType.Cube, "cover", root, Mat(Color.white, Soft(cover, 0.16f), 0.3f), new Vector3(0, 0.005f, 0), Vector3.zero, new Vector3(0.095f, 0.009f, 0.135f));
        P(PrimitiveType.Cube, "pages", root, Mat(C(0.96f, 0.93f, 0.85f)), new Vector3(0.002f, 0.0046f, 0), Vector3.zero, new Vector3(0.093f, 0.0075f, 0.131f), false);
        Material ring = Mat(C(0.78f, 0.80f, 0.86f), null, 0.5f);
        for (int i = 0; i < 12; i++)
            P(PrimitiveType.Cylinder, "ring", root, ring, new Vector3(-0.047f, 0.0052f, -0.06f + i * 0.0109f), new Vector3(0, 0, 90), new Vector3(0.0045f, 0.007f, 0.0045f), false);
        P(PrimitiveType.Cube, "label", root, Mat(C(0.97f, 0.95f, 0.88f)), new Vector3(0.006f, 0.0097f, -0.03f), Vector3.zero, new Vector3(0.05f, 0.0006f, 0.03f), false);
    }

    static void Sticky(Transform root, System.Random r)
    {
        Color[] cols = { C(1f, 0.58f, 0.74f), C(1f, 0.88f, 0.42f), C(0.55f, 0.78f, 1f), C(0.62f, 0.9f, 0.66f) };
        for (int i = 0; i < 3; i++)
        {
            Color c = cols[(i + r.Next(4)) % 4];
            P(PrimitiveType.Cube, "sticky", root, Mat(Color.white, Soft(c, 0.05f), 0.05f), new Vector3(i * 0.006f - 0.004f, 0.003f + i * 0.0062f, i * 0.004f), new Vector3(0, i * 17f + r.Next(-8, 8), 0), new Vector3(0.05f, 0.006f, 0.05f));
        }
    }

    static void Highlighter(Transform root, System.Random r)
    {
        Color c = Pick(r, C(1f, 0.55f, 0.1f), C(0.95f, 0.9f, 0.2f), C(0.35f, 0.85f, 0.5f), C(1f, 0.4f, 0.65f));
        Material body = Mat(Color.white, Soft(c, 0.1f), 0.4f), cap = Mat(c * 0.78f, null, 0.4f);
        var a = new GameObject("hl").transform; a.SetParent(root, false); a.localPosition = new Vector3(0, 0.0058f, 0);
        Rod(a, body, -0.05f, 0.012f, 0.0058f, true); Rod(a, cap, 0.012f, 0.05f, 0.0064f, true);
        Rod(a, Mat(C(0.97f, 0.97f, 0.92f)), -0.056f, -0.05f, 0.0036f);
        P(PrimitiveType.Cube, "stripe", a, Mat(Color.white), new Vector3(0, 0.0062f, 0.03f), Vector3.zero, new Vector3(0.0025f, 0.0015f, 0.022f), false);
        a.localEulerAngles = new Vector3(0, 0, 0);
    }

    static void Pen(Transform root, System.Random r)
    {
        Color c = Pick(r, C(0.08f, 0.08f, 0.1f), C(0.15f, 0.25f, 0.6f), C(0.6f, 0.12f, 0.15f));
        var a = new GameObject("pen").transform; a.SetParent(root, false); a.localPosition = new Vector3(0, 0.0048f, 0);
        Rod(a, Mat(c, null, 0.55f), -0.065f, 0.045f, 0.0048f, true);
        Rod(a, Mat(C(0.8f, 0.82f, 0.88f), null, 0.6f), 0.045f, 0.065f, 0.003f, true);
        P(PrimitiveType.Cube, "clip", a, Mat(C(0.8f, 0.82f, 0.88f), null, 0.6f), new Vector3(0, 0.0056f, -0.045f), Vector3.zero, new Vector3(0.0018f, 0.0012f, 0.034f), false);
    }

    static void Plant(Transform root, System.Random r)
    {
        P(PrimitiveType.Cylinder, "pot", root, Mat(Color.white, Terracotta(), 0.15f), new Vector3(0, 0.022f, 0), Vector3.zero, new Vector3(0.062f, 0.022f, 0.062f));
        P(PrimitiveType.Cylinder, "soil", root, Mat(C(0.22f, 0.14f, 0.09f)), new Vector3(0, 0.0435f, 0), Vector3.zero, new Vector3(0.054f, 0.0012f, 0.054f), false);
        Material leaf = Mat(Color.white, Leaf(), 0.3f);
        int n = 9;
        for (int i = 0; i < n; i++)
        {
            var node = new GameObject("leafNode").transform; node.SetParent(root, false);
            node.localPosition = new Vector3(0, 0.045f, 0); node.localEulerAngles = new Vector3(0, i * 360f / n + r.Next(-12, 12), 0);
            float tilt = -(25f + (i % 3) * 18f);
            P(PrimitiveType.Sphere, "leaf", node, leaf, new Vector3(0, 0.012f + (i % 3) * 0.004f, 0.02f + (i % 3) * 0.004f), new Vector3(tilt, 0, 0), new Vector3(0.017f, 0.0035f, 0.044f));
        }
    }

    static void Phone(Transform root, System.Random r)
    {
        P(PrimitiveType.Cube, "phone", root, Mat(Color.white, Glass(), 0.7f), new Vector3(0, 0.004f, 0), Vector3.zero, new Vector3(0.07f, 0.008f, 0.145f));
        P(PrimitiveType.Cylinder, "lens", root, Mat(C(0.02f, 0.02f, 0.03f), null, 0.8f), new Vector3(-0.02f, 0.0082f, 0.055f), Vector3.zero, new Vector3(0.009f, 0.0003f, 0.009f), false);
    }

    static void Mug(Transform root, System.Random r)
    {
        Color c = Pick(r, C(0.96f, 0.93f, 0.88f), C(0.5f, 0.75f, 0.95f), C(0.95f, 0.55f, 0.5f), C(0.55f, 0.82f, 0.62f));
        Material m = Mat(Color.white, Soft(c, 0.06f), 0.55f);
        P(PrimitiveType.Cylinder, "mug", root, m, new Vector3(0, 0.032f, 0), Vector3.zero, new Vector3(0.068f, 0.032f, 0.068f));
        P(PrimitiveType.Cylinder, "coffee", root, Mat(Color.white, Coffee(), 0.6f), new Vector3(0, 0.0618f, 0), Vector3.zero, new Vector3(0.058f, 0.0005f, 0.058f), false);
        for (int i = 0; i < 6; i++)
        {
            float a = (-70f + i * 28f) * Mathf.Deg2Rad;
            P(PrimitiveType.Cube, "handle", root, m, new Vector3(0.036f + Mathf.Cos(a) * 0.018f, 0.032f, Mathf.Sin(a) * 0.018f), new Vector3(0, -a * Mathf.Rad2Deg + 90f, 0), new Vector3(0.007f, 0.012f, 0.012f));
        }
    }

    static void Eraser(Transform root, System.Random r)
    {
        P(PrimitiveType.Cube, "eraser", root, Mat(C(0.97f, 0.97f, 0.97f), null, 0.2f), new Vector3(0, 0.006f, 0), Vector3.zero, new Vector3(0.042f, 0.012f, 0.02f));
        P(PrimitiveType.Cube, "sleeve", root, Mat(C(0.25f, 0.45f, 0.85f), null, 0.2f), new Vector3(0.004f, 0.0063f, 0), Vector3.zero, new Vector3(0.026f, 0.0126f, 0.0206f), false);
    }

    static void RulerProp(Transform root, System.Random r)
    {
        P(PrimitiveType.Cube, "ruler", root, Mat(Color.white, Ruler(), 0.5f), new Vector3(0, 0.0015f, 0), Vector3.zero, new Vector3(0.17f, 0.003f, 0.03f));
    }
}

public class DeskFly : MonoBehaviour
{
    Vector3 center; Transform body, wl, wr, shadow;

    public void Init(Transform parent, Vector3 c)
    {
        center = c; transform.SetParent(parent, false);
        Material dark = DeskKit.Mat(new Color(0.08f, 0.08f, 0.1f), null, 0.5f), wing = DeskKit.Mat(new Color(0.85f, 0.9f, 1f), null, 0.6f);
        body = new GameObject("body").transform; body.SetParent(transform, false);
        DeskKit.P(PrimitiveType.Sphere, "abdomen", body, dark, Vector3.zero, Vector3.zero, new Vector3(0.007f, 0.006f, 0.012f), false);
        DeskKit.P(PrimitiveType.Sphere, "head", body, dark, new Vector3(0f, 0f, 0.007f), Vector3.zero, new Vector3(0.005f, 0.005f, 0.005f), false);
        wl = new GameObject("wingL").transform; wl.SetParent(body, false); wl.localPosition = new Vector3(-0.003f, 0.003f, 0.001f);
        wr = new GameObject("wingR").transform; wr.SetParent(body, false); wr.localPosition = new Vector3(0.003f, 0.003f, 0.001f);
        DeskKit.P(PrimitiveType.Sphere, "w", wl, wing, new Vector3(-0.0055f, 0f, 0f), Vector3.zero, new Vector3(0.011f, 0.0006f, 0.006f), false);
        DeskKit.P(PrimitiveType.Sphere, "w", wr, wing, new Vector3(0.0055f, 0f, 0f), Vector3.zero, new Vector3(0.011f, 0.0006f, 0.006f), false);
        shadow = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
        WorldUtil.Kill(shadow.GetComponent<Collider>()); shadow.SetParent(transform, false);
        var r = shadow.GetComponent<Renderer>(); r.sharedMaterial = DeskKit.Sprite(DeskKit.Blob(), new Color(0f, 0f, 0f, 0.3f), 3100);
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        shadow.localRotation = Quaternion.Euler(90f, 0f, 0f); shadow.localScale = new Vector3(0.014f, 0.014f, 1f);
    }

    Vector3 Path(float t)
    {
        float a = t * 2.6f + Mathf.Sin(t * 0.7f) * 0.8f;
        return center + new Vector3(Mathf.Cos(a) * 0.034f + Mathf.Sin(t * 5.3f) * 0.003f, 0.03f + 0.012f * Mathf.Sin(t * 3.1f), Mathf.Sin(a * 1.5f) * 0.026f);
    }

    void Update()
    {
        float t = Time.time;
        Vector3 p = Path(t), n = Path(t + 0.03f);
        p.y += Mathf.Sin(t * 60f) * 0.0012f;
        Vector3 d = n - p; d.y = 0f;
        body.localPosition = p;
        if (d.sqrMagnitude > 1e-10f) body.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        float flap = Mathf.Sin(t * 140f) * 40f;
        wl.localRotation = Quaternion.Euler(0f, 0f, -flap); wr.localRotation = Quaternion.Euler(0f, 0f, flap);
        shadow.localPosition = new Vector3(p.x, 0.0095f, p.z);
    }
}

public class LampFlicker : MonoBehaviour
{
    Light l; float baseI, burst, next;

    void Start() { l = GetComponent<Light>(); baseI = l.intensity; next = Time.unscaledTime + 2f; }

    void Update()
    {
        if (l == null) return;
        float t = Time.unscaledTime, k = 0.97f + 0.03f * Mathf.PerlinNoise(t * 9f, 0.5f);
        if (t >= next) { burst = 0.3f + UnityEngine.Random.value * 0.25f; next = t + 2.5f + UnityEngine.Random.value * 4f; }
        if (burst > 0f) { burst -= Time.unscaledDeltaTime; k *= Mathf.Sin(t * 70f) > 0f ? 1f : 0.55f + UnityEngine.Random.value * 0.2f; }
        l.intensity = baseI * k;
    }
}

/// <summary>Marks the desk's "back to main menu" note so the form UI can raycast it.</summary>
public class BackNoteTag : MonoBehaviour { }
