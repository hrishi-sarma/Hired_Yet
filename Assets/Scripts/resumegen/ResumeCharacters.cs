using UnityEngine;

/// <summary>8 preset characters (4 male, 4 female) built from primitives. Each has a fixed skin tone, hairstyle and outfit colour.
/// Portraits (head + neck + shoulders) are rendered once by a hidden camera far below the scene and cached as Texture2D.</summary>
public static class CharacterLibrary
{
    public class Def { public string name; public bool female; public Color skin, hair, outfit; public int style; }

    static Def D(string n, bool f, float sr, float sg, float sb, float hr, float hg, float hb, float or, float og, float ob, int st)
    { return new Def { name = n, female = f, skin = new Color(sr, sg, sb), hair = new Color(hr, hg, hb), outfit = new Color(or, og, ob), style = st }; }

    // 0-3 male, 4-7 female
    public static readonly Def[] All =
    {
        D("Sam",  false, 0.96f, 0.80f, 0.67f,  0.25f, 0.17f, 0.10f,  0.18f, 0.28f, 0.50f, 0),   // crew cut, navy
        D("Leo",  false, 0.86f, 0.66f, 0.48f,  0.12f, 0.10f, 0.09f,  0.20f, 0.42f, 0.30f, 1),   // quiff, forest green
        D("Omar", false, 0.62f, 0.43f, 0.29f,  0.08f, 0.07f, 0.07f,  0.52f, 0.20f, 0.26f, 2),   // curly + beard, burgundy
        D("Kai",  false, 0.38f, 0.25f, 0.17f,  0.15f, 0.10f, 0.07f,  0.30f, 0.32f, 0.40f, 3),   // long sides, charcoal
        D("Maya", true,  0.95f, 0.78f, 0.66f,  0.80f, 0.62f, 0.30f,  0.14f, 0.48f, 0.52f, 0),   // long straight, teal
        D("Nora", true,  0.82f, 0.62f, 0.45f,  0.35f, 0.17f, 0.10f,  0.45f, 0.25f, 0.50f, 1),   // bob + fringe, plum
        D("Ivy",  true,  0.60f, 0.42f, 0.29f,  0.10f, 0.08f, 0.08f,  0.78f, 0.58f, 0.18f, 2),   // bun, mustard
        D("Zoe",  true,  0.40f, 0.27f, 0.19f,  0.10f, 0.07f, 0.06f,  0.80f, 0.40f, 0.45f, 3),   // curly puffs, dusty rose
    };

    static readonly Texture2D[] cache = new Texture2D[8];
    static readonly Vector3 Studio = new Vector3(0f, -2000f, 0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { for (int i = 0; i < cache.Length; i++) cache[i] = null; }

    public static Texture2D Portrait(int idx)
    {
        if (idx < 0 || idx >= All.Length) return null;
        if (cache[idx] == null) cache[idx] = Render(All[idx]);
        return cache[idx];
    }

    static Texture2D Render(Def d)
    {
        const int W = 256, H = 320;
        var root = new GameObject("Portrait Studio").transform; root.position = Studio;
        Build(root, d);

        var cg = new GameObject("Portrait Cam"); cg.transform.SetParent(root, false);
        var cam = cg.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 0.46f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 10f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = d.female ? new Color(0.78f, 0.82f, 0.90f) : new Color(0.72f, 0.80f, 0.88f);
        cg.transform.localPosition = new Vector3(0f, 0.26f, -3f); cg.transform.localRotation = Quaternion.identity;
        cam.enabled = false;

        var lg = new GameObject("Portrait Light"); lg.transform.SetParent(root, false); lg.transform.localPosition = new Vector3(-0.8f, 1.0f, -1.6f);
        var li = lg.AddComponent<Light>(); li.type = LightType.Point; li.range = 6f; li.intensity = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? 3f : 1f; li.shadows = LightShadows.None;

        var rt = RenderTexture.GetTemporary(W, H, 24);
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); tex.filterMode = FilterMode.Bilinear;
        RenderTexture.active = prev; cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
        WorldUtil.Kill(root.gameObject);
        return tex;
    }

    // ---------- model (face looks toward -Z, neck base at y=0, head centre y=0.27) ----------
    static Material M(Color c)
    {
        var m = DeskKit.Mat(c, null, 0.1f);
        m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 0.45f);   // keeps it readable regardless of scene lighting
        return m;
    }

    static Transform Part(Transform p, PrimitiveType t, Material m, float x, float y, float z, float sx, float sy, float sz, float rz = 0f)
    { return DeskKit.P(t, "part", p, m, new Vector3(x, y, z), new Vector3(0f, 0f, rz), new Vector3(sx, sy, sz), false); }

    static void Build(Transform p, Def d)
    {
        Material skin = M(d.skin), hair = M(d.hair), outfit = M(d.outfit);
        Material dark = M(new Color(0.08f, 0.06f, 0.06f)), lip = M(Color.Lerp(d.skin, new Color(0.6f, 0.2f, 0.22f), 0.55f));
        Material nose = M(d.skin * 0.93f), white = M(new Color(0.95f, 0.95f, 0.92f));

        // body: shoulders + chest in outfit colour, neck, collar
        Part(p, PrimitiveType.Capsule, outfit, 0f, -0.10f, 0f, 0.18f, 0.27f, 0.18f, 90f);
        Part(p, PrimitiveType.Sphere, outfit, 0f, -0.20f, 0.01f, 0.54f, 0.34f, 0.27f);
        Part(p, PrimitiveType.Cylinder, skin, 0f, 0.07f, 0f, 0.11f, 0.09f, 0.11f);
        Part(p, PrimitiveType.Sphere, skin, 0f, -0.035f, -0.045f, 0.15f, 0.10f, 0.12f);          // neckline
        Part(p, PrimitiveType.Sphere, d.female ? outfit : white, -0.065f, -0.02f, -0.05f, 0.09f, 0.045f, 0.10f);   // collar L
        Part(p, PrimitiveType.Sphere, d.female ? outfit : white, 0.065f, -0.02f, -0.05f, 0.09f, 0.045f, 0.10f);    // collar R

        // head + face
        Part(p, PrimitiveType.Sphere, skin, 0f, 0.27f, 0f, 0.30f, 0.34f, 0.31f);
        Part(p, PrimitiveType.Sphere, skin, -0.152f, 0.27f, 0.005f, 0.04f, 0.07f, 0.04f);
        Part(p, PrimitiveType.Sphere, skin, 0.152f, 0.27f, 0.005f, 0.04f, 0.07f, 0.04f);
        Part(p, PrimitiveType.Sphere, white, -0.068f, 0.30f, -0.132f, 0.042f, 0.026f, 0.02f);
        Part(p, PrimitiveType.Sphere, white, 0.068f, 0.30f, -0.132f, 0.042f, 0.026f, 0.02f);
        Part(p, PrimitiveType.Sphere, dark, -0.068f, 0.30f, -0.139f, 0.022f, 0.022f, 0.012f);
        Part(p, PrimitiveType.Sphere, dark, 0.068f, 0.30f, -0.139f, 0.022f, 0.022f, 0.012f);
        float bw = d.female ? 0.05f : 0.062f, bh = d.female ? 0.009f : 0.014f;
        Part(p, PrimitiveType.Cube, hair, -0.068f, 0.342f, -0.128f, bw, bh, 0.012f);
        Part(p, PrimitiveType.Cube, hair, 0.068f, 0.342f, -0.128f, bw, bh, 0.012f);
        Part(p, PrimitiveType.Sphere, nose, 0f, 0.255f, -0.155f, 0.04f, 0.05f, 0.04f);
        Part(p, PrimitiveType.Capsule, lip, 0f, 0.20f, -0.140f, 0.012f, 0.032f, 0.012f, 90f);

        // hair (all from primitives; a cap slightly bigger than the head, shifted up/back, leaves the face clear)
        Transform cap = Part(p, PrimitiveType.Sphere, hair, 0f, 0.30f, 0.02f, 0.325f, 0.34f, 0.335f);
        if (!d.female)
        {
            switch (d.style)
            {
                case 0: cap.localScale = new Vector3(0.31f, 0.30f, 0.32f); cap.localPosition = new Vector3(0f, 0.31f, 0.015f); break;   // crew cut
                case 1: Part(p, PrimitiveType.Sphere, hair, 0f, 0.46f, -0.05f, 0.21f, 0.12f, 0.22f); break;                             // quiff
                case 2:                                                                                                                  // curly + beard
                    cap.localScale = new Vector3(0.42f, 0.40f, 0.40f); cap.localPosition = new Vector3(0f, 0.36f, 0.03f);
                    Part(p, PrimitiveType.Sphere, hair, 0f, 0.125f, -0.095f, 0.21f, 0.10f, 0.11f); break;
                case 3:                                                                                                                  // long sides
                    Part(p, PrimitiveType.Sphere, hair, -0.155f, 0.2f, 0.01f, 0.07f, 0.22f, 0.2f);
                    Part(p, PrimitiveType.Sphere, hair, 0.155f, 0.2f, 0.01f, 0.07f, 0.22f, 0.2f);
                    Part(p, PrimitiveType.Sphere, hair, 0f, 0.2f, 0.1f, 0.30f, 0.30f, 0.12f); break;
            }
        }
        else
        {
            switch (d.style)
            {
                case 0:                                                                                                                  // long straight
                    Part(p, PrimitiveType.Sphere, hair, 0f, 0.10f, 0.09f, 0.36f, 0.62f, 0.20f);
                    Part(p, PrimitiveType.Capsule, hair, -0.165f, 0.10f, -0.01f, 0.07f, 0.20f, 0.07f);
                    Part(p, PrimitiveType.Capsule, hair, 0.165f, 0.10f, -0.01f, 0.07f, 0.20f, 0.07f); break;
                case 1:                                                                                                                  // bob + fringe
                    Part(p, PrimitiveType.Sphere, hair, 0f, 0.20f, 0.04f, 0.36f, 0.30f, 0.28f);
                    Part(p, PrimitiveType.Sphere, hair, 0f, 0.43f, -0.10f, 0.28f, 0.07f, 0.10f); break;
                case 2:                                                                                                                  // bun
                    Part(p, PrimitiveType.Sphere, hair, 0f, 0.51f, 0.06f, 0.16f, 0.16f, 0.16f); break;
                case 3:                                                                                                                  // curly puffs
                    cap.localScale = new Vector3(0.46f, 0.44f, 0.40f); cap.localPosition = new Vector3(0f, 0.34f, 0.04f);
                    Part(p, PrimitiveType.Sphere, hair, -0.2f, 0.2f, 0.02f, 0.16f, 0.16f, 0.16f);
                    Part(p, PrimitiveType.Sphere, hair, 0.2f, 0.2f, 0.02f, 0.16f, 0.16f, 0.16f); break;
            }
        }
    }
}
