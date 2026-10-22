using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>Fake-3D pause menu: three post-its pinned on a tilted whiteboard, standing on a wooden desk with a flexible lamp that
/// swings over to whatever you hover (springy). Everything is unlit quads + one tiny extra camera parked far away from the game,
/// so it is cheap, pipeline-agnostic and the game camera is almost free while the menu is up.</summary>
public class PauseBoard : MonoBehaviour
{
    // ---------------------------------------------------------------- tiny helpers
    class Spring
    {
        public float x, v, target;
        public void Step(float dt, float k, float c)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / 0.008f)); float h = dt / n;
            for (int i = 0; i < n; i++) { v += (k * (target - x) - c * v) * h; x += v * h; }
        }
    }
    class Layer { public Transform t; public float offset, delay, closeDelay; public Spring s = new Spring(); }
    class Spec { public string text; public Color col; public Vector2 pos, size; public float rot; public Action click; public Func<float> get; public Action<float> set; public Func<bool> enabled; }

    static readonly Color Ink = new Color(0.2f, 0.15f, 0.25f), Mint = new Color(0.74f, 0.95f, 0.84f), Sky = new Color(0.72f, 0.88f, 1f),
        Pink = new Color(1f, 0.78f, 0.86f), Lemon = new Color(1f, 0.93f, 0.6f);


    // ---------------------------------------------------------------- caches (built once, spread over frames by Warm())
    static readonly Dictionary<string, Texture2D> tc = new Dictionary<string, Texture2D>();
    static readonly Dictionary<Color, Sprite> noteSprites = new Dictionary<Color, Sprite>();
    static Texture2D T(string key, Func<Texture2D> make) { Texture2D t; if (!tc.TryGetValue(key, out t) || t == null) { t = make(); tc[key] = t; } return t; }
    static Sprite NoteSprite(Color c) { Sprite s; if (!noteSprites.TryGetValue(c, out s) || s == null) { s = PostItArt.Note(c); noteSprites[c] = s; } return s; }
    static Color Dk(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, 1f); }
    static Color Lt(Color c, float f) { return Color.Lerp(c, Color.white, f); }

    static Texture2D TexBackdrop() { return T("bd", () => DeskKit.Make(8, 128, (u, v) => { Color top = new Color(0.25f, 0.22f, 0.27f), bot = new Color(0.12f, 0.1f, 0.14f); return Color.Lerp(bot, top, Mathf.Pow(v, 0.8f)); }, TextureWrapMode.Clamp)); }
    static float Hh(float x, float y) { float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }
    static Texture2D TexBoard() { return T("bf", () => DeskKit.Make(384, 208, (u, v) =>
    {
        // green cushioned pin-board: quilted puffy diamonds with a little button at every seam crossing + soft felt grain
        float x = u * 3f, y = v * 1.6f; const float cell = 0.3f;
        float fa = (x + y) / cell, fb = (x - y) / cell; fa -= Mathf.Floor(fa) + 0.5f; fb -= Mathf.Floor(fb) + 0.5f;
        float m = 0.5f - Mathf.Max(Mathf.Abs(fa), Mathf.Abs(fb));
        float puff = Mathf.Pow(Mathf.Clamp01(m * 3.2f), 0.6f);
        float lit = Mathf.Lerp(0.62f, 1.02f, puff) - 0.22f * fb * puff;
        Color c = Color.Lerp(new Color(0.1f, 0.3f, 0.19f), new Color(0.3f, 0.64f, 0.4f), Mathf.Clamp01(lit));
        float pd = Mathf.Sqrt((0.5f - Mathf.Abs(fa)) * (0.5f - Mathf.Abs(fa)) + (0.5f - Mathf.Abs(fb)) * (0.5f - Mathf.Abs(fb)));
        if (pd < 0.07f) c = Color.Lerp(c, new Color(0.06f, 0.18f, 0.11f), 1f - pd / 0.07f);
        float g = (Mathf.PerlinNoise(u * 150f, v * 80f) - 0.5f) * 0.1f + (Hh(Mathf.Floor(u * 384f), Mathf.Floor(v * 208f)) - 0.5f) * 0.06f;
        c = new Color(c.r + g, c.g + g * 1.2f, c.b + g, 1f);
        float d = (u - .5f) * (u - .5f) + (v - .5f) * (v - .5f); return Dk(c, 1f - 0.2f * d * 2f);
    }, TextureWrapMode.Clamp)); }
    static Texture2D TexGlow(string k, float pow) { return T(k, () => DeskKit.Make(64, 64, (u, v) => { float d = Mathf.Sqrt((u - .5f) * (u - .5f) + (v - .5f) * (v - .5f)) * 2f; return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), pow)); }, TextureWrapMode.Clamp)); }
    static Texture2D TexShade() { return T("sh", () => DeskKit.Make(4, 64, (u, v) => new Color(0f, 0f, 0f, Mathf.Pow(1f - v, 1.5f) * 0.55f), TextureWrapMode.Clamp)); }
    static Texture2D TexPin(Color pc) { return T("pin" + pc, () => DeskKit.Make(48, 48, (u, v) =>
    {
        float d = Mathf.Sqrt((u - .5f) * (u - .5f) + (v - .5f) * (v - .5f)) * 2f; if (d > 1f) return new Color(0, 0, 0, 0);
        if (d > 0.84f) return new Color(0.15f, 0.1f, 0.2f, 1f);
        float h = Mathf.Clamp01(1f - Mathf.Sqrt((u - .36f) * (u - .36f) + (v - .66f) * (v - .66f)) * 4.2f);
        float sh = Mathf.Clamp01((d - 0.45f) * 1.2f) * (v < 0.5f ? 0.45f : 0.1f);
        return Color.Lerp(Color.Lerp(pc, Color.black, sh), Color.white, h * 0.85f);
    }, TextureWrapMode.Clamp)); }
    /// <summary>Everything the menu needs, one item per frame, so opening it never hitches. Started once in-game by PauseMenu.</summary>
    public static System.Collections.IEnumerator Warm()
    {
        var steps = new List<Action> { () => TexBackdrop(), () => DeskKit.Blob(), () => TexBoard(), () => TexGlow("gl", 1.7f), () => TexGlow("gl2", 1.2f), () => TexShade(),
            () => TexPin(PinCols[0]), () => TexPin(PinCols[1]), () => TexPin(PinCols[2]), () => DeskKit.Wood(),
            () => NoteSprite(Mint), () => NoteSprite(Sky), () => NoteSprite(Pink), () => NoteSprite(Lemon) };
        foreach (var s in steps) { s(); yield return null; }
    }
    static readonly Color[] PinCols = { new Color(0.9f, 0.2f, 0.25f), new Color(0.25f, 0.45f, 0.95f), new Color(0.2f, 0.75f, 0.4f) };

    /// <summary>open-ended box along +Z (the visible front is a separate textured quad): gives notes / frame real thickness with toon-shaded sides</summary>
    static Mesh SideBox(float w, float h, float d, Color top, Color bottom, Color left, Color right)
    {
        float x = w * 0.5f, y = h * 0.5f; var v = new List<Vector3>(); var c = new List<Color>(); var tri = new List<int>();
        Action<Vector3, Vector3, Vector3, Vector3, Color> face = (a, b, e, f, col) =>
        {
            int i = v.Count; v.Add(a); v.Add(b); v.Add(e); v.Add(f);
            c.Add(Lt(col, 0.18f)); c.Add(Lt(col, 0.18f)); c.Add(Dk(col, 0.7f)); c.Add(Dk(col, 0.7f));
            tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        };
        face(new Vector3(-x, y, 0), new Vector3(x, y, 0), new Vector3(x, y, d), new Vector3(-x, y, d), top);
        face(new Vector3(x, -y, 0), new Vector3(-x, -y, 0), new Vector3(-x, -y, d), new Vector3(x, -y, d), bottom);
        face(new Vector3(-x, -y, 0), new Vector3(-x, y, 0), new Vector3(-x, y, d), new Vector3(-x, -y, d), left);
        face(new Vector3(x, y, 0), new Vector3(x, -y, 0), new Vector3(x, -y, d), new Vector3(x, y, d), right);
        var m = new Mesh { name = "SideBox" }; m.SetVertices(v); m.SetColors(c); m.SetTriangles(tri, 0); m.RecalculateBounds(); return m;
    }
    Transform Body(string n, Transform p, Mesh mesh, int q, Vector3 pos)
    {
        var g = new GameObject(n); g.transform.SetParent(p, false); g.transform.localPosition = pos;
        g.AddComponent<MeshFilter>().sharedMesh = mesh; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = M(null, Color.white, q);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return g.transform;
    }

    // ---------------------------------------------------------------- toon-lit 3D parts
    // Unlit vertex-coloured meshes. The sprite shader has no depth buffer / culling, so back faces are removed on the CPU each frame
    // (painter order inside a mesh = index order). This gives real volume + shading to the lamp, frame and desk items without any scene light.
    class Pal { public Color d, m, l, h; public Pal(Color d, Color m, Color l, Color h) { this.d = d; this.m = m; this.l = l; this.h = h; } }
    static Pal PC(Color c) { return new Pal(Dk(c, 0.4f), Dk(c, 0.7f), c, Lt(c, 0.5f)); }
    static readonly Vector3 LightDir = new Vector3(-0.35f, 0.8f, -0.45f).normalized;
    static Color Toon(Pal p, float dl)
    {
        float t = Mathf.Clamp01((dl + 0.6f) / 1.6f) * 3f; t = Mathf.Lerp(t, Mathf.Round(t * 2.5f) / 2.5f, 0.45f);
        return t < 1f ? Color.Lerp(p.d, p.m, t) : t < 2f ? Color.Lerp(p.m, p.l, t - 1f) : Color.Lerp(p.l, p.h, t - 2f);
    }
    class MB
    {
        public List<Vector3> V = new List<Vector3>(), N = new List<Vector3>(); public List<int> T = new List<int>(); public List<Pal> P = new List<Pal>(); public List<Color> F = new List<Color>();
        public int Add(Vector3 v, Vector3 n, Pal p, bool fx = false, Color c = default(Color)) { V.Add(v); N.Add(n); P.Add(p); F.Add(fx ? c : new Color(0f, 0f, 0f, -1f)); return V.Count - 1; }
    }
    static void Box(MB m, Vector3 c, Vector3 s, Pal p)
    {
        Vector3 h = s * 0.5f; Vector3[] ax = { Vector3.right, Vector3.up, Vector3.forward };
        for (int a = 0; a < 3; a++)
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Vector3 nrm = ax[a] * sg, u = ax[(a + 1) % 3], w = ax[(a + 2) % 3];
                Vector3 ctr = c + Vector3.Scale(nrm, h), uu = Vector3.Scale(u, h), ww = Vector3.Scale(w, h);
                int i = m.Add(ctr - uu - ww, nrm, p); m.Add(ctr + uu - ww, nrm, p); m.Add(ctr + uu + ww, nrm, p); m.Add(ctr - uu + ww, nrm, p);
                m.T.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
    }
    /// <summary>cylinder along Y, y -1..1, radius 0.5 (same proportions as the Unity primitive)</summary>
    static void Tube(MB m, Pal p, int seg, bool caps)
    {
        int i0 = m.V.Count;
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg; Vector3 nr = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            m.Add(nr * 0.5f + Vector3.down, nr, p); m.Add(nr * 0.5f + Vector3.up, nr, p);
        }
        for (int i = 0; i < seg; i++) { int b = i0 + i * 2; m.T.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 }); }
        if (!caps) return;
        for (int sg = -1; sg <= 1; sg += 2)
        {
            Vector3 nn = new Vector3(0f, sg, 0f); int c0 = m.Add(nn, nn, p), r0 = m.V.Count;
            for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI * 2f / seg; m.Add(new Vector3(Mathf.Cos(a) * 0.5f, sg, Mathf.Sin(a) * 0.5f), nn, p); }
            for (int i = 0; i < seg; i++) m.T.AddRange(new[] { c0, r0 + i, r0 + i + 1 });
        }
    }
    static void Ball(MB m, Pal p, int lat, int lon, Vector3 c, float r, bool fx = false, Color fc = default(Color))
    {
        int i0 = m.V.Count;
        for (int y = 0; y <= lat; y++)
        {
            float pv = Mathf.PI * y / lat;
            for (int x = 0; x <= lon; x++) { float a = Mathf.PI * 2f * x / lon; Vector3 nr = new Vector3(Mathf.Sin(pv) * Mathf.Cos(a), Mathf.Cos(pv), Mathf.Sin(pv) * Mathf.Sin(a)); m.Add(c + nr * r, nr, p, fx, fc); }
        }
        for (int y = 0; y < lat; y++)
            for (int x = 0; x < lon; x++) { int a0 = i0 + y * (lon + 1) + x, b0 = a0 + lon + 1; m.T.AddRange(new[] { a0, b0, a0 + 1, a0 + 1, b0, b0 + 1 }); }
    }
    /// <summary>ring strip around +Z (cone / rim / cap); nz = z part of the normal (|nz|>=1: flat disc)</summary>
    static void Strip(MB m, int seg, float z0, float r0, float z1, float r1, float nz, bool inward, Pal p, bool fx, Color c0, Color c1)
    {
        int i0 = m.V.Count;
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg; Vector3 rad = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Vector3 nn = Mathf.Abs(nz) >= 1f ? new Vector3(0f, 0f, nz) : (rad + new Vector3(0f, 0f, nz)).normalized; if (inward) nn = -nn;
            m.Add(rad * r0 + new Vector3(0f, 0f, z0), nn, p, fx, c0); m.Add(rad * r1 + new Vector3(0f, 0f, z1), nn, p, fx, c1);
        }
        for (int i = 0; i < seg; i++) { int b = i0 + i * 2; m.T.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 }); }
    }
    /// <summary>lamp shade: apex at z=0 opening to z=0.47. Index order = paint order: glowing inside, bulb, then the outer shell on top (so nothing leaks through it)</summary>
    static void Shade(MB m, Pal shade, Pal brass)
    {
        const int S = 22; float sl = (0.265f - 0.07f) / 0.44f; Color none = default(Color);
        Strip(m, S, 0f, 0.063f, 0.44f, 0.246f, -sl, true, shade, true, new Color(1f, 0.95f, 0.72f), new Color(0.95f, 0.6f, 0.22f));
        Ball(m, shade, 10, 14, new Vector3(0f, 0f, 0.17f), 0.085f, true, new Color(1f, 0.98f, 0.84f));
        Strip(m, S, 0f, 0.07f, 0.44f, 0.265f, -sl, false, shade, false, none, none);
        Strip(m, S, 0.44f, 0.265f, 0.47f, 0.28f, 0f, false, brass, false, none, none);
        Strip(m, S, 0.47f, 0.28f, 0.47f, 0.25f, 1f, false, brass, false, none, none);
        Strip(m, S, 0f, 0f, 0f, 0.07f, -1f, false, shade, false, none, none);
    }
    static Shader partShader; static bool partZ;
    static Material PartMat(int q)
    {
        if (partShader == null) { partShader = Shader.Find("Universal Render Pipeline/Particles/Unlit"); partZ = partShader != null; if (!partZ) partShader = Shader.Find("Sprites/Default"); }
        if (!partZ) return DeskKit.Sprite(null, Color.white, q);
        var m = new Material(partShader); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Surface", 0f); m.SetFloat("_Cull", 0f); m.SetFloat("_ZWrite", 1f); m.SetFloat("_SrcBlend", 1f); m.SetFloat("_DstBlend", 0f);
        m.renderQueue = 2000; return m;
    }
    class Part
    {
        public Transform t; Mesh mesh; Vector3[] v, n; Pal[] pal; Color[] fix, col; int[] src; readonly List<int> vis = new List<int>();
        public static Part Make(string name, Transform parent, int q, MB b, Vector3 lp)
        {
            var p = new Part(); var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = lp; p.t = g.transform;
            p.v = b.V.ToArray(); p.n = b.N.ToArray(); p.pal = b.P.ToArray(); p.fix = b.F.ToArray(); p.col = new Color[p.v.Length];
            var tri = new List<int>(b.T.Count);
            for (int i = 0; i < b.T.Count; i += 3)
            {
                int a = b.T[i], c1 = b.T[i + 1], c2 = b.T[i + 2];
                if (Vector3.Dot(Vector3.Cross(p.v[c1] - p.v[a], p.v[c2] - p.v[a]), p.n[a] + p.n[c1] + p.n[c2]) < 0f) { int sw = c1; c1 = c2; c2 = sw; }
                tri.Add(a); tri.Add(c1); tri.Add(c2);
            }
            p.src = tri.ToArray();
            p.mesh = new Mesh { name = name }; p.mesh.MarkDynamic(); p.mesh.vertices = p.v; p.mesh.colors = p.col; p.mesh.triangles = p.src;
            var bd = new Bounds(p.v[0], Vector3.zero); for (int i = 1; i < p.v.Length; i++) bd.Encapsulate(p.v[i]); bd.Expand(0.2f); p.mesh.bounds = bd;
            g.AddComponent<MeshFilter>().sharedMesh = p.mesh; var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = PartMat(q); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return p;
        }
        public void Refresh(Vector3 camW)
        {
            Quaternion rot = t.rotation; Vector3 cl = t.InverseTransformPoint(camW);
            for (int i = 0; i < v.Length; i++) col[i] = fix[i].a >= 0f ? fix[i] : Toon(pal[i], Vector3.Dot(rot * n[i], LightDir));
            mesh.colors = col;
            vis.Clear();
            for (int i = 0; i < src.Length; i += 3)
            {
                Vector3 a = v[src[i]];
                if (Vector3.Dot(Vector3.Cross(v[src[i + 1]] - a, v[src[i + 2]] - a), cl - a) > 0f) { vis.Add(src[i]); vis.Add(src[i + 1]); vis.Add(src[i + 2]); }
            }
            mesh.SetTriangles(vis, 0, false);
        }
    }
    readonly List<Part> parts = new List<Part>();
    void RefreshParts() { Vector3 cp = cam.transform.position; for (int i = 0; i < parts.Count; i++) parts[i].Refresh(cp); }
    /// <summary>capsule-like rod between two parent-local points</summary>
    Part RodPart(string n, Transform p, int q, Pal pal, Vector3 a, Vector3 b, float r)
    {
        var mb = new MB(); Tube(mb, pal, 14, true); var pt = Part.Make(n, p, q, mb, (a + b) * 0.5f); Vector3 d = b - a;
        pt.t.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized); pt.t.localScale = new Vector3(2f * r, d.magnitude * 0.5f, 2f * r); parts.Add(pt); return pt;
    }
    Part Slab(string n, int q, Color c, float x, float y0, float z, float w, float d, float th, float yaw)
    {
        var mb = new MB(); Box(mb, Vector3.zero, new Vector3(w, th, d), PC(c));
        var p = Part.Make(n, table.t, q, mb, new Vector3(x, -0.62f + y0 + th * 0.5f, z)); p.t.localRotation = Quaternion.Euler(0f, yaw, 0f); parts.Add(p); return p;
    }
    static void Ribbon(Transform q, Vector3 a, Vector3 b, float w)
    {
        Vector3 d = b - a; d.y = 0f; float len = d.magnitude; if (len < 1e-4f) { q.localScale = Vector3.zero; return; }
        q.position = (a + b) * 0.5f; q.rotation = Quaternion.LookRotation(Vector3.up, d / len); q.localScale = new Vector3(w, len, 1f);
    }

    // ---------------------------------------------------------------- state
    class NoteObj { public Transform tr, shadow, fillT, handleT; public Material mat, shMat; public Spec sp; public Spring sc = new Spring(), lift = new Spring(); public float delay, clickT = -1f, cw, ch, tx, ty, fit = 0.66f, val; public Text label, pct; public bool live = true; public List<Material> xm = new List<Material>(); public List<Color> xc = new List<Color>(); }
    const float SlL = 0.46f, SlR = 0.8f;

    Transform root, boardRoot, headT, shA, shB, shHead, glow, poolT;
    Part post, armA, armB, jS, jE, jH, headP; Mesh beamMesh; Vector3[] bv; const int BS = 24;
    Camera cam; Layer bg, board, table;
    Font font; Action onResume, onQuit, closedCb;
    readonly List<NoteObj> notes = new List<NoteObj>();
    int hover = -1; float t, closeT; bool closing, interactive, covered, keyMode, closedFired, inSettings;
    Vector2 lastMouse, camOff, lampLocal;
    Material beamMat, glowMat, poolMat;
    Spring lx = new Spring(), ly = new Spring(); Spring[] hs = { new Spring(), new Spring(), new Spring() };
    Action pendingAct; float pendingT;
    readonly Dictionary<Camera, int> masks = new Dictionary<Camera, int>();
    NoteObj fsNote; int screen; int drag = -1;
    Mesh glowMesh; readonly Vector3[] glowV = new Vector3[4]; readonly Vector2[] glowUV = new Vector2[4];

    const float L1 = 1.25f, L2 = 1.25f;
    static readonly Vector3 LampBase = new Vector3(1.5f, -0.60f, 3.0f);
    static readonly Color Wall = new Color(0.2f, 0.18f, 0.22f);
    public bool InSettings { get { return inSettings; } }

    // ---------------------------------------------------------------- build
    public void Init(Font f, Action resume, Action quit)
    {
        font = f; onResume = resume; onQuit = quit;
        root = new GameObject("Pause Scene").transform; root.SetParent(transform, false);
        root.position = new Vector3(0f, 3000f, 0f);     // parked far above the game; the main camera never sees it

        var cg = new GameObject("Pause Camera", typeof(Camera)); cg.transform.SetParent(root, false);
        cam = cg.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Depth; cam.depth = 500f; cam.fieldOfView = 44f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 60f;
        cam.allowHDR = false; cam.allowMSAA = false; cam.useOcclusionCulling = false; cam.cullingMask = ~0;
        cam.transform.localPosition = Vector3.zero;

        bg = NewLayer("Backdrop Group", -20f, 0f, 0.24f);
        board = NewLayer("Board Group", -10f, 0.14f, 0.12f);
        table = NewLayer("Table Group", -10f, 0.28f, 0f);
        StartCoroutine(BuildRoutine());   // one piece per frame: opening the menu never costs one big hitch frame
    }

    bool ready;
    System.Collections.IEnumerator BuildRoutine()
    {
        BuildBackdrop(); yield return null;   // backdrop slides in first (delay 0), the rest is built while it travels
        BuildTable(); yield return null;
        BuildBoard(); yield return null;
        BuildLamp(); yield return null;
        ShowMain();
        Layers(0.0001f);
        ready = true;
    }

    Layer NewLayer(string n, float off, float delay, float cd)
    {
        var g = new GameObject(n).transform; g.SetParent(root, false);
        var l = new Layer { t = g, offset = off, delay = delay, closeDelay = cd }; l.s.x = off; l.s.target = off; return l;
    }

    Material M(Texture tex, Color c, int q) { return DeskKit.Sprite(tex as Texture2D, c, q); }
    Transform Quad(string n, Transform p, Material m, Vector3 pos, Vector3 euler, Vector2 size) { return DeskKit.P(PrimitiveType.Quad, n, p, m, pos, euler, new Vector3(size.x, size.y, 1f), false); }
    void FlatShadow(Transform p, Vector3 pos, Vector2 size, float yaw, float a, int q) { Quad("Shadow", p, M(DeskKit.Blob(), new Color(0.2f, 0.1f, 0.05f, a), q), pos, new Vector3(90f, 0f, yaw), size); }

    void BuildBackdrop()
    {
        Quad("Backdrop", bg.t, M(TexBackdrop(), Color.white, 2800), new Vector3(0f, 0f, 9.2f), Vector3.zero, new Vector2(40f, 22f));
    }

    void BuildTable()
    {
        const float ty = -0.62f;
        var wood = M(DeskKit.Wood(), new Color(0.46f, 0.35f, 0.3f), 2805); wood.mainTextureScale = new Vector2(4f, 3f);
        Quad("Table", table.t, wood, new Vector3(0f, ty, 5.4f), new Vector3(90f, 0f, 0f), new Vector2(16f, 7f));
        Quad("TableShade", table.t, M(TexShade(), Color.white, 2806), new Vector3(0f, ty + 0.002f, 3.1f), new Vector3(90f, 0f, 0f), new Vector2(16f, 2.8f));
        poolMat = M(TexGlow("gl2", 1.2f), new Color(1f, 0.8f, 0.45f, 0.3f), 2806);
        poolT = Quad("LampPool", table.t, poolMat, new Vector3(0f, ty + 0.004f, 3.1f), new Vector3(90f, 0f, 0f), new Vector2(3.4f, 1.7f));
        Quad("Edge", table.t, M(null, new Color(0.22f, 0.14f, 0.1f), 2804), new Vector3(0f, ty - 0.17f, 1.9f), Vector3.zero, new Vector2(16f, 0.34f));
        Quad("EdgeHi", table.t, M(null, new Color(0.5f, 0.38f, 0.28f), 2806), new Vector3(0f, ty - 0.015f, 1.9f), Vector3.zero, new Vector2(16f, 0.035f));
        Quad("WallBase", table.t, M(null, Dk(Wall, 0.9f), 2802), new Vector3(0f, ty + 0.17f, 8.9f), Vector3.zero, new Vector2(40f, 0.36f));
        Quad("WallBaseTop", table.t, M(null, Dk(Wall, 1.25f), 2803), new Vector3(0f, ty + 0.36f, 8.9f), Vector3.zero, new Vector2(40f, 0.04f));

        FlatShadow(table.t, new Vector3(-0.75f, ty + 0.003f, 4.05f), new Vector2(4.6f, 1.7f), 0f, 0.5f, 2807);       // board
        FlatShadow(table.t, new Vector3(-1.55f, ty + 0.003f, 2.35f), new Vector2(1.6f, 1.2f), 0f, 0.5f, 2807);        // papers
        FlatShadow(table.t, new Vector3(-0.1f, ty + 0.003f, 2.5f), new Vector2(0.9f, 0.8f), 0f, 0.45f, 2807);
        FlatShadow(table.t, new Vector3(0.75f, ty + 0.003f, 2.35f), new Vector2(0.9f, 0.8f), 0f, 0.45f, 2807);
        FlatShadow(table.t, new Vector3(-0.25f, ty + 0.003f, 2.05f), new Vector2(1.0f, 0.3f), 0f, 0.4f, 2807);
        FlatShadow(table.t, new Vector3(LampBase.x - 0.3f, ty + 0.003f, LampBase.z + 0.1f), new Vector2(1.5f, 0.85f), 0f, 0.55f, 2807);   // lamp base

        // paper stack and sticky pads are real thin slabs now (top + shaded sides) instead of flat quads
        Slab("Papers2", 2810, new Color(0.38f, 0.38f, 0.46f), -1.33f, 0f, 2.22f, 1.0f, 0.7f, 0.03f, -8f);
        Slab("Papers", 2811, new Color(0.7f, 0.7f, 0.78f), -1.3f, 0.03f, 2.2f, 1.0f, 0.7f, 0.03f, -14f);
        Quad("Header", table.t, M(null, new Color(0.3f, 0.34f, 0.46f), 2816), new Vector3(-1.45f, ty + 0.063f, 2.38f), new Vector3(90f, 0f, 14f), new Vector2(0.35f, 0.1f));
        Quad("Lines", table.t, M(null, new Color(0.45f, 0.47f, 0.56f), 2816), new Vector3(-1.3f, ty + 0.063f, 2.12f), new Vector3(90f, 0f, 14f), new Vector2(0.7f, 0.035f));
        Slab("Sticky0", 2810, new Color(0.5f, 0.68f, 0.82f), -0.1f, 0f, 2.45f, 0.5f, 0.5f, 0.03f, 16f);
        Slab("Sticky2", 2810, new Color(0.9f, 0.75f, 0.34f), 0.75f, 0f, 2.3f, 0.5f, 0.5f, 0.03f, -10f);
        RodPart("Pen", table.t, 2812, PC(new Color(0.16f, 0.16f, 0.24f)), new Vector3(-0.55f, ty + 0.035f, 2.12f), new Vector3(0.05f, ty + 0.035f, 1.97f), 0.035f);
        RodPart("PenTip", table.t, 2813, PC(new Color(0.9f, 0.7f, 0.28f)), new Vector3(0.04f, ty + 0.035f, 1.972f), new Vector3(0.15f, ty + 0.035f, 1.945f), 0.022f);
    }

    void BuildBoard()
    {
        var bgp = new GameObject("Board").transform; bgp.SetParent(board.t, false);
        boardRoot = bgp; boardRoot.localPosition = new Vector3(-0.3f, 0.31f, 3.7f); boardRoot.localRotation = Quaternion.Euler(6f, -22f, 0f); boardRoot.localScale = Vector3.one;   // bottom of tray rests just above the desk (y = -0.62)
        Quad("WallShadow", board.t, M(DeskKit.Blob(), new Color(0.05f, 0.03f, 0.04f, 0.45f), 2809), new Vector3(-0.75f, 0.0f, 4.3f), Vector3.zero, new Vector2(5.2f, 3.2f));

        Quad("Face", boardRoot, M(TexBoard(), new Color(0.68f, 0.7f, 0.68f), 2811), new Vector3(0f, 0f, -0.004f), Vector3.zero, new Vector2(3.0f, 1.6f));
        glowMat = M(TexGlow("gl", 1.7f), new Color(1f, 0.9f, 0.55f, 0.5f), 2812);
        var gg = new GameObject("Glow"); gg.transform.SetParent(boardRoot, false); gg.transform.localPosition = new Vector3(0f, 0f, -0.01f); glow = gg.transform;
        glowMesh = new Mesh { name = "Glow" }; glowMesh.MarkDynamic(); glowMesh.vertices = glowV; glowMesh.uv = glowUV; glowMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        gg.AddComponent<MeshFilter>().sharedMesh = glowMesh; var gr = gg.AddComponent<MeshRenderer>(); gr.sharedMaterial = glowMat;
        gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; gr.receiveShadows = false;

        // solid frame: four bars with real depth, toon shaded on every face, and chunky corner blocks that stand proud of it
        Pal fp = PC(new Color(0.4f, 0.4f, 0.54f)), kp = PC(new Color(0.12f, 0.11f, 0.17f));
        var mf = new MB();
        Box(mf, new Vector3(0f, 0.85f, 0.03f), new Vector3(3.2f, 0.1f, 0.14f), fp);
        Box(mf, new Vector3(0f, -0.85f, 0.03f), new Vector3(3.2f, 0.1f, 0.14f), fp);
        Box(mf, new Vector3(-1.55f, 0f, 0.03f), new Vector3(0.1f, 1.6f, 0.14f), fp);
        Box(mf, new Vector3(1.55f, 0f, 0.03f), new Vector3(0.1f, 1.6f, 0.14f), fp);
        parts.Add(Part.Make("Frame", boardRoot, 2813, mf, Vector3.zero));
        var mk = new MB();
        foreach (var c in new[] { new Vector2(-1.6f, 0.9f), new Vector2(1.6f, 0.9f), new Vector2(-1.6f, -0.9f), new Vector2(1.6f, -0.9f) })
            Box(mk, new Vector3(c.x, c.y, 0f), new Vector3(0.22f, 0.22f, 0.22f), kp);
        parts.Add(Part.Make("Corners", boardRoot, 2814, mk, Vector3.zero));
        // marker tray + markers
        var mt = new MB(); Box(mt, Vector3.zero, new Vector3(2.5f, 0.08f, 0.2f), PC(new Color(0.58f, 0.58f, 0.7f)));
        parts.Add(Part.Make("Tray", boardRoot, 2815, mt, new Vector3(0f, -0.88f, -0.1f)));
        RodPart("Marker", boardRoot, 2816, PC(new Color(0.85f, 0.2f, 0.28f)), new Vector3(-0.8f, -0.812f, -0.12f), new Vector3(-0.45f, -0.812f, -0.12f), 0.025f);
        RodPart("Marker2", boardRoot, 2816, PC(new Color(0.2f, 0.42f, 0.9f)), new Vector3(-0.4f, -0.812f, -0.13f), new Vector3(-0.05f, -0.812f, -0.12f), 0.025f);
    }

    Part TubeP(string n, Pal p, int q) { var mb = new MB(); Tube(mb, p, 14, true); var pt = Part.Make(n, root, q, mb, Vector3.zero); parts.Add(pt); return pt; }
    Part BallP(string n, Pal p, float d, int q) { var mb = new MB(); Ball(mb, p, 9, 14, Vector3.zero, 0.5f); var pt = Part.Make(n, root, q, mb, Vector3.zero); pt.t.localScale = Vector3.one * d; parts.Add(pt); return pt; }
    void Disc(string n, Color c, Vector3 pos, Vector3 sc, int q) { var mb = new MB(); Tube(mb, PC(c), 28, true); var pt = Part.Make(n, table.t, q, mb, pos); pt.t.localScale = sc; parts.Add(pt); }

    void BuildLamp()
    {
        Pal metal = new Pal(new Color(0.05f, 0.05f, 0.09f), new Color(0.13f, 0.13f, 0.2f), new Color(0.3f, 0.31f, 0.43f), new Color(0.62f, 0.64f, 0.82f));
        Pal brass = new Pal(new Color(0.42f, 0.27f, 0.08f), new Color(0.72f, 0.48f, 0.15f), new Color(0.94f, 0.72f, 0.28f), new Color(1f, 0.95f, 0.7f));
        Pal shade = new Pal(new Color(0.06f, 0.06f, 0.11f), new Color(0.15f, 0.15f, 0.24f), new Color(0.3f, 0.31f, 0.46f), new Color(0.6f, 0.62f, 0.82f));
        // weighted base: three shaded discs
        Disc("base0", new Color(0.1f, 0.1f, 0.14f), LampBase + new Vector3(0f, -0.005f, 0f), new Vector3(0.66f, 0.03f, 0.66f), 3001);
        Disc("base1", new Color(0.24f, 0.24f, 0.33f), LampBase + new Vector3(0f, 0.03f, 0f), new Vector3(0.56f, 0.03f, 0.56f), 3002);
        Disc("base2", new Color(0.4f, 0.4f, 0.52f), LampBase + new Vector3(0f, 0.065f, 0f), new Vector3(0.4f, 0.025f, 0.4f), 3003);

        post = TubeP("LampStem", metal, 3009); armA = TubeP("armA", metal, 3010); armB = TubeP("armB", metal, 3011);
        jS = BallP("jS", brass, 0.15f, 3013); jE = BallP("jE", brass, 0.14f, 3014); jH = BallP("jH", brass, 0.15f, 3025);

        var mh = new MB(); Shade(mh, shade, brass);
        headP = Part.Make("Head", root, 3020, mh, Vector3.zero); headT = headP.t; parts.Add(headP);

        // cast shadows of the arm / head on the desk: flat ribbons (single layer, so no darker overlap bands)
        var shMat = M(null, new Color(0.02f, 0.01f, 0.02f, 0.3f), 2808);
        shA = Quad("shA", table.t, shMat, Vector3.zero, Vector3.zero, new Vector2(0.06f, 0.1f));
        shB = Quad("shB", table.t, shMat, Vector3.zero, Vector3.zero, new Vector2(0.05f, 0.1f));
        shHead = Quad("shHead", table.t, M(DeskKit.Blob(), new Color(0.02f, 0.01f, 0.02f, 0.38f), 2808), Vector3.zero, new Vector3(90f, 0f, 0f), new Vector2(0.9f, 0.6f));

        // light cone: brightest at the shade opening, fading out towards the board
        beamMat = M(null, Color.white, 2836);
        beamMesh = new Mesh { name = "Beam" }; beamMesh.MarkDynamic(); bv = new Vector3[BS * 2]; var bc = new Color[BS * 2]; var bt = new List<int>();
        for (int i = 0; i < BS; i++)
        {
            bc[i] = new Color(1f, 0.88f, 0.5f, 0.3f); bc[BS + i] = new Color(1f, 0.88f, 0.5f, 0.04f); int n = (i + 1) % BS;
            bt.AddRange(new[] { i, BS + i, BS + n, i, BS + n, n });
        }
        beamMesh.vertices = bv; beamMesh.colors = bc; beamMesh.SetTriangles(bt, 0);
        var bg2 = new GameObject("Beam"); bg2.transform.SetParent(root, false);
        bg2.AddComponent<MeshFilter>().sharedMesh = beamMesh; var br = bg2.AddComponent<MeshRenderer>(); br.sharedMaterial = beamMat;
        br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; br.receiveShadows = false;
        hs[0].x = hs[0].target = 0.6f; hs[1].x = hs[1].target = 1.1f; hs[2].x = hs[2].target = 3.2f;
    }

    // ---------------------------------------------------------------- notes
    Spec S(string text, Color c, float x, float y, float w, float h, float rot, Action click)
    { return new Spec { text = text, col = c, pos = new Vector2(x, y), size = new Vector2(w, h), rot = rot, click = click }; }

    Spec Sl(string text, Color c, float x, float y, float w, float h, float rot, Func<float> get, Action<float> set, Func<bool> enabled = null)
    { return new Spec { text = text, col = c, pos = new Vector2(x, y), size = new Vector2(w, h), rot = rot, get = get, set = set, enabled = enabled }; }

    void ShowMain()
    {
        screen = 0; inSettings = false; fsNote = null;
        SetNotes(new[]
        {
            S("RESUME", Mint, 0f, 0.45f, 0.96f, 0.4f, 2f, () => onResume()),
            S("SETTINGS", Sky, 0.04f, 0f, 1.08f, 0.4f, -2.5f, ShowSettings),
            S("QUIT TO MENU", Pink, 0.07f, -0.45f, 1.36f, 0.4f, 1.5f, () => onQuit()),
        });
    }

    public void Back() { if (!ready) return; if (screen == 2 || screen == 3) ShowSettings(); else if (screen == 1) ShowMain(); }

    void ShowSettings()
    {
        screen = 1; inSettings = true;
        SetNotes(new[]
        {
            Sl("MASTER", Lemon, 0f, 0.55f, 1.8f, 0.31f, -1.5f, () => GameSettings.Master, v => GameSettings.SetAudio(0, v)),
            S("", Sky, 0.02f, 0.18f, 1.4f, 0.31f, 1.5f, () => ToggleFull(true)),
            S("GRAPHICS", Mint, -0.5f, -0.19f, 0.92f, 0.31f, -2f, ShowGraphics),
            S("SOUNDS", Sky, 0.56f, -0.19f, 0.92f, 0.31f, 2f, ShowSounds),
            S("BACK", Lemon, 0.06f, -0.56f, 0.76f, 0.31f, 2f, ShowMain),
        });
        fsNote = notes[1]; ToggleFull(false);
    }

    NoteObj[] gfxOn = new NoteObj[5];

    // GRAPHICS: left column = ON/OFF, right column = intensity slider
    void ShowGraphics()
    {
        screen = 2; inSettings = true; fsNote = null;
        Color[] cs = { Mint, Sky, Lemon, Pink, Mint };
        var sp = new List<Spec>();
        for (int i = 0; i < 5; i++) { int k = i; float y = 0.6f - i * 0.24f; sp.Add(S("", cs[i], -0.78f, y, 1.34f, 0.23f, (i % 2 == 0 ? 1f : -1f), () => ToggleGfx(k))); }
        for (int i = 0; i < 5; i++) { int k = i; float y = 0.6f - i * 0.24f; sp.Add(Sl("INTENSITY", Sky, 0.78f, y, 1.34f, 0.23f, (i % 2 == 0 ? -1f : 1f), () => GfxPrefs.Amt(k), v => GfxPrefs.SetAmt(k, v), () => GfxPrefs.Get(k))); }
        sp.Add(S("BACK", Lemon, 0.78f, -0.6f, 0.8f, 0.23f, -2f, ShowSettings));
        SetNotes(sp.ToArray());
        for (int i = 0; i < 5; i++) { gfxOn[i] = notes[i]; RefreshGfx(i); }
    }
    void ToggleGfx(int k) { GfxPrefs.Set(k, !GfxPrefs.Get(k)); RefreshGfx(k); }
    void RefreshGfx(int k)
    {
        if (gfxOn[k] != null) Fit(gfxOn[k], GfxPrefs.Names[k] + ": " + (GfxPrefs.Get(k) ? "ON" : "OFF"));
    }

    // SOUNDS: one volume slider per kind of sound; the master volume is on the settings screen
    void ShowSounds()
    {
        screen = 3; inSettings = true; fsNote = null;
        Color[] cs = { Mint, Sky, Lemon, Pink, Mint, Sky, Lemon };
        var sp = new List<Spec>();
        for (int i = 0; i < 7; i++) { int k = i + 1; float x = i % 2 == 0 ? -0.78f : 0.78f; float y = 0.55f - (i / 2) * 0.27f; sp.Add(Sl(GameSettings.AudioNames[k], cs[i], x, y, 1.34f, 0.24f, (i % 3 - 1) * 1.2f, () => GameSettings.GetAudio(k), v => GameSettings.SetAudio(k, v))); }
        sp.Add(S("BACK", Lemon, 0.78f, -0.26f, 0.8f, 0.24f, -2f, ShowSettings));
        SetNotes(sp.ToArray());
    }

    void ToggleFull(bool flip) { if (flip) Screen.fullScreen = !Screen.fullScreen; if (fsNote != null) Fit(fsNote, "FULLSCREEN: " + (Screen.fullScreen ? "ON" : "OFF")); }

    // shrink the handwriting until it sits inside the note (preferredWidth is the single-line width)
    void Fit(NoteObj n, string s)
    {
        var tx = n.label; tx.text = s;
        if (s.Length <= 1) { tx.fontSize = Mathf.RoundToInt(Mathf.Min(0.42f * n.ch, 380f)); return; }          // + / - symbols: big
        int fs = Mathf.RoundToInt(Mathf.Min(0.5f * n.ch, 150f)); tx.fontSize = fs;
        float lim = n.cw * n.fit;
        while (fs > 10 && TextW(tx, s, fs) > lim) { fs -= 3; }
        tx.fontSize = fs;
    }
    // measured width, with a conservative per-glyph estimate as floor (preferredWidth can under-report before the font atlas has the glyphs)
    static float TextW(Text tx, string s, int fs)
    {
        if (tx.font != null) tx.font.RequestCharactersInTexture(s, fs, FontStyle.Bold);
        tx.fontSize = fs; return Mathf.Max(tx.preferredWidth, s.Length * fs * 0.46f);
    }

    void SetNotes(Spec[] specs)
    {
        foreach (var n in notes) { if (n.tr != null) WorldUtil.Kill(n.tr.gameObject); if (n.shadow != null) WorldUtil.Kill(n.shadow.gameObject); }
        notes.Clear(); hover = -1; drag = -1;
        for (int i = 0; i < specs.Length; i++)
        {
            var sp = specs[i]; var n = new NoteObj { sp = sp, delay = t + 0.06f * i, tx = ((i * 37) % 5 - 2) * 0.7f, ty = ((i * 53) % 5 - 2) * 0.8f };
            var g = new GameObject("Note").transform; g.SetParent(boardRoot, false); n.tr = g;
            g.localPosition = new Vector3(sp.pos.x, sp.pos.y, -0.03f);

            // soft cast shadow stays on the board and slides away from the lamp
            n.shMat = M(DeskKit.Blob(), new Color(0.12f, 0.08f, 0.2f, 0.45f), 2835);
            n.shadow = Quad("NoteShadow", boardRoot, n.shMat, new Vector3(sp.pos.x, sp.pos.y, -0.012f), Vector3.zero, sp.size * 1.12f);

            var spr = NoteSprite(sp.col); var tex = spr.texture; var rc = spr.textureRect;
            n.mat = M(tex, Color.white, 2840);
            n.mat.mainTextureScale = new Vector2(rc.width / tex.width, rc.height / tex.height); n.mat.mainTextureOffset = new Vector2(rc.x / tex.width, rc.y / tex.height);
            Body("Body", g, SideBox(sp.size.x * 0.84f, sp.size.y * 0.8f, 0.055f, Lt(sp.col, 0.3f), Dk(sp.col, 0.45f), Dk(sp.col, 0.85f), Dk(sp.col, 0.55f)), 2838, new Vector3(0f, 0f, 0.001f));
            Quad("Paper", g, n.mat, new Vector3(0f, 0f, -0.001f), Vector3.zero, sp.size);

            if (sp.click != null || sp.text != "")
            {
                Color pc = PinCols[i % PinCols.Length];
                Quad("PinShadow", g, M(DeskKit.Blob(), new Color(0.1f, 0.05f, 0.15f, 0.5f), 2849), new Vector3(-0.02f, sp.size.y * 0.5f - 0.05f, -0.012f), Vector3.zero, new Vector2(0.14f, 0.12f));
                Quad("Pin", g, M(TexPin(pc), Color.white, 2850), new Vector3(0f, sp.size.y * 0.5f - 0.03f, -0.025f), Vector3.zero, new Vector2(0.1f, 0.1f));
            }
            float cw = 900f, ch = cw * sp.size.y / sp.size.x; n.cw = cw; n.ch = ch;
            var cgo = new GameObject("Label", typeof(RectTransform), typeof(Canvas)); var rt = cgo.GetComponent<RectTransform>(); rt.SetParent(g, false);
            var cv = cgo.GetComponent<Canvas>(); cv.renderMode = RenderMode.WorldSpace; cv.worldCamera = cam;
            rt.sizeDelta = new Vector2(cw, ch); rt.localScale = Vector3.one * (sp.size.x / cw); rt.localPosition = new Vector3(0f, 0.004f, -0.014f);
            var tg = new GameObject("Text", typeof(Text)); var tr = tg.GetComponent<RectTransform>(); tr.SetParent(rt, false);
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            var tx = tg.GetComponent<Text>(); tx.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tx.fontStyle = FontStyle.Bold; tx.alignment = TextAnchor.MiddleCenter; tx.color = Ink; tx.raycastTarget = false;
            tx.horizontalOverflow = HorizontalWrapMode.Overflow; tx.verticalOverflow = VerticalWrapMode.Overflow; tx.resizeTextForBestFit = false;
            n.label = tx; if (sp.set != null) BuildSlider(n, g, rt); Fit(n, sp.text);
            n.sc.x = 0f; n.sc.target = 0f; g.localScale = Vector3.zero;
            notes.Add(n);
        }
    }

    Material Ex(NoteObj n, Color c, int q) { var mt = M(null, c, q); n.xm.Add(mt); n.xc.Add(c); return mt; }

    void BuildSlider(NoteObj n, Transform g, RectTransform lrt)
    {
        Vector2 sz = n.sp.size; float x0 = (SlL - 0.5f) * sz.x, tw = (SlR - SlL) * sz.x, th = sz.y * 0.26f, hh = sz.y * 0.64f, z = -0.006f;
        n.val = Mathf.Clamp01(n.sp.get()); n.fit = 0.38f;
        n.label.alignment = TextAnchor.MiddleLeft;
        var lr = n.label.rectTransform; lr.anchorMin = new Vector2(0.05f, 0f); lr.anchorMax = new Vector2(0.95f, 1f); lr.offsetMin = lr.offsetMax = Vector2.zero;
        Quad("TrackEdge", g, Ex(n, Ink, 2841), new Vector3(x0 + tw * 0.5f, 0f, z), Vector3.zero, new Vector2(tw + 0.024f, th + 0.024f));
        Quad("Track", g, Ex(n, new Color(1f, 0.98f, 0.9f), 2842), new Vector3(x0 + tw * 0.5f, 0f, z - 0.001f), Vector3.zero, new Vector2(tw, th));
        var fp = new GameObject("FillPivot").transform; fp.SetParent(g, false); fp.localPosition = new Vector3(x0, 0f, z - 0.002f); n.fillT = fp;
        Quad("Fill", fp, Ex(n, new Color(1f, 0.62f, 0.2f), 2843), new Vector3(0.5f, 0f, 0f), Vector3.zero, new Vector2(1f, th * 0.6f));
        var hp = new GameObject("Handle").transform; hp.SetParent(g, false); hp.localPosition = new Vector3(x0, 0f, z - 0.004f); n.handleT = hp;
        Quad("HandleEdge", hp, Ex(n, Ink, 2845), Vector3.zero, Vector3.zero, new Vector2(0.06f, hh + 0.02f));
        Quad("HandleBody", hp, Ex(n, Mint, 2846), new Vector3(0f, 0f, -0.001f), Vector3.zero, new Vector2(0.044f, hh));
        var pg = new GameObject("Pct", typeof(Text)); var pr = pg.GetComponent<RectTransform>(); pr.SetParent(lrt, false);
        pr.anchorMin = new Vector2(SlR + 0.025f, 0f); pr.anchorMax = new Vector2(0.96f, 1f); pr.offsetMin = pr.offsetMax = Vector2.zero;
        var pt = pg.GetComponent<Text>(); pt.font = n.label.font; pt.fontStyle = FontStyle.Bold; pt.alignment = TextAnchor.MiddleCenter; pt.color = Ink; pt.raycastTarget = false;
        pt.horizontalOverflow = HorizontalWrapMode.Overflow; pt.verticalOverflow = VerticalWrapMode.Overflow; pt.fontSize = Mathf.RoundToInt(0.34f * n.ch);
        n.pct = pt;
        SliderLook(n);
    }

    void SliderLook(NoteObj n)
    {
        float w = n.sp.size.x, x0 = (SlL - 0.5f) * w, tw = (SlR - SlL) * w;
        n.fillT.localScale = new Vector3(Mathf.Max(0.0001f, n.val * tw), 1f, 1f);
        n.handleT.localPosition = new Vector3(x0 + n.val * tw, 0f, n.handleT.localPosition.z);
        n.pct.text = Mathf.RoundToInt(n.val * 100f) + "%";
    }

    void SetSlider(NoteObj n, float v)
    {
        v = Mathf.Round(Mathf.Clamp01(v) * 100f) / 100f;
        if (Mathf.Abs(v - n.val) < 0.005f) return;
        n.val = v; n.sp.set(v); SliderLook(n);
    }

    float NoteFrac(NoteObj n, Vector2 mp)
    {
        Ray r = cam.ScreenPointToRay(mp);
        var plane = new Plane(boardRoot.rotation * Vector3.back, boardRoot.position);
        float d; if (!plane.Raycast(r, out d)) return -10f;
        Vector3 lp = boardRoot.InverseTransformPoint(r.GetPoint(d));
        float a = n.sp.rot * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
        float dx = lp.x - n.sp.pos.x, dy = lp.y - n.sp.pos.y;
        return (dx * ca + dy * sa) / (Mathf.Max(0.0001f, n.tr.localScale.x) * n.sp.size.x) + 0.5f;
    }

    // ---------------------------------------------------------------- frame update
    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f); t += dt;
        Layers(dt);
        if (!ready) return;   // still being built (a few frames)
        Camera3D(dt);
        interactive = !closing && t > 0.85f;
        if (interactive && !covered) Cover(true);
        HandleInput(dt); Lamp(dt); Notes(dt); RefreshParts();
        if (pendingAct != null && (pendingT -= dt) <= 0f) { var a = pendingAct; pendingAct = null; a(); }
    }

    void Layers(float dt)
    {
        for (int li = 0; li < 3; li++)
        {
            var l = li == 0 ? bg : li == 1 ? board : table;
            if (!closing) l.s.target = t >= l.delay ? 0f : l.offset;
            else l.s.target = (t - closeT) >= l.closeDelay ? l.offset : 0f;
            if (!closing) l.s.Step(dt, 75f, 9f); else l.s.Step(dt, 130f, 19f);
            l.t.localPosition = new Vector3(0f, l.s.x, 0f);
        }
        if (closing && !closedFired && (t - closeT) > 0.24f && Mathf.Abs(bg.s.x - bg.offset) < 1.5f && Mathf.Abs(board.s.x - board.offset) < 1.5f && Mathf.Abs(table.s.x - table.offset) < 1.5f)
        { closedFired = true; if (closedCb != null) closedCb(); }
    }

    /// <summary>true once the backdrop has (almost) fully arrived over the game (PauseMenu freezes the world then)</summary>
    public bool BackdropUp { get { return bg != null && bg.s.x > -1.5f && !closing; } }

    void Camera3D(float dt)
    {
        Vector2 tgt = Vector2.zero;
        var m = Mouse.current;
        if (m != null) { Vector2 p = m.position.ReadValue(); tgt = new Vector2(p.x / Screen.width - 0.5f, p.y / Screen.height - 0.5f); }
        camOff = Vector2.Lerp(camOff, tgt, 1f - Mathf.Exp(-5f * dt));
        cam.transform.localPosition = new Vector3(camOff.x * 0.3f, camOff.y * 0.14f, 0f);
        cam.transform.LookAt(root.TransformPoint(new Vector3(0f, 0.15f, 3.6f)));
    }

    // the game is hidden behind an opaque backdrop: stop rendering it (big frame-rate win while paused)
    void Cover(bool on)
    {
        if (on == covered) return;
        covered = on;
        if (on)
        {
            foreach (var c in Camera.allCameras) if (c != cam && c.targetTexture == null) { masks[c] = c.cullingMask; c.cullingMask = 0; }
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Wall;
        }
        else
        {
            foreach (var kv in masks) if (kv.Key != null) kv.Key.cullingMask = kv.Value;
            masks.Clear(); cam.clearFlags = CameraClearFlags.Depth;
        }
    }

    public void Close(Action done)
    {
        if (closing) return;
        closing = true; closeT = t; closedCb = done; hover = -1; pendingAct = null;
        Cover(false);
    }

    void OnDestroy() { Cover(false); }

    int Pick(Vector2 mp)
    {
        Ray r = cam.ScreenPointToRay(mp);
        var plane = new Plane(boardRoot.rotation * Vector3.back, boardRoot.position);
        float d; if (!plane.Raycast(r, out d)) return -1;
        Vector3 lp = boardRoot.InverseTransformPoint(r.GetPoint(d));
        for (int i = notes.Count - 1; i >= 0; i--)
        {
            var n = notes[i]; if ((n.sp.click == null && n.sp.set == null) || n.sc.x < 0.6f) continue;
            if (Mathf.Abs(lp.x - n.sp.pos.x) < n.sp.size.x * 0.5f && Mathf.Abs(lp.y - n.sp.pos.y) < n.sp.size.y * 0.5f) return i;
        }
        return -1;
    }

    void HandleInput(float dt)
    {
        if (!interactive) return;
        var m = Mouse.current; var kb = Keyboard.current;
        int h = hover; Vector2 mp = Vector2.zero;
        if (m != null)
        {
            mp = m.position.ReadValue();
            if ((mp - lastMouse).sqrMagnitude > 9f) { keyMode = false; lastMouse = mp; }
            if (!keyMode) h = Pick(mp);
            if (drag >= 0 && (!m.leftButton.isPressed || drag >= notes.Count)) drag = -1;
            if (drag >= 0) h = drag;
        }
        if (kb != null)
        {
            int dir = (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) ? 1 : (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) ? -1 : 0;
            if (dir != 0 && notes.Count > 0)
            {
                keyMode = true; int i = hover;
                for (int k = 0; k < notes.Count; k++) { i = (i + dir + notes.Count) % notes.Count; if (notes[i].sp.click != null || notes[i].sp.set != null) break; }
                h = i;
            }
        }
        if (h != hover) hover = h;
        if (m != null && m.leftButton.wasPressedThisFrame && drag < 0 && hover >= 0 && hover < notes.Count && !keyMode)
        {
            var sn = notes[hover];
            if (sn.sp.set != null && sn.live) { float f = NoteFrac(sn, mp); if (f > SlL - 0.05f && f < SlR + 0.05f) drag = hover; }
        }
        if (drag >= 0 && drag < notes.Count) { float f = NoteFrac(notes[drag], mp); if (f > -5f) SetSlider(notes[drag], (f - SlL) / (SlR - SlL)); }
        if (kb != null && hover >= 0 && hover < notes.Count && notes[hover].sp.set != null && notes[hover].live)
        {
            int sd = (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) ? 1 : (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) ? -1 : 0;
            if (sd != 0) SetSlider(notes[hover], Mathf.Round(notes[hover].val * 20f) / 20f + 0.05f * sd);
        }
        bool click = (m != null && m.leftButton.wasPressedThisFrame && hover >= 0 && !keyMode) ||
                     (kb != null && hover >= 0 && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));
        if (click && hover < notes.Count && pendingAct == null && notes[hover].sp.set == null)
        {
            var n = notes[hover]; n.clickT = 0f; n.sc.v = -6f; pendingAct = n.sp.click; pendingT = 0.17f;
        }
    }

    void Notes(float dt)
    {
        for (int i = 0; i < notes.Count; i++)
        {
            var n = notes[i]; bool hv = hover == i;
            float want = t < n.delay ? 0f : (hv ? 1.08f : 1f);
            if (n.clickT >= 0f) { n.clickT += dt; if (n.clickT < 0.12f) want = 0.9f; else n.clickT = -1f; }
            n.sc.target = want; n.sc.Step(dt, 170f, 11f);
            n.lift.target = hv ? 0.09f : 0f; n.lift.Step(dt, 120f, 14f);
            float s = Mathf.Max(0.0001f, n.sc.x);
            n.tr.localScale = new Vector3(s, s, 1f);
            n.tr.localPosition = new Vector3(n.sp.pos.x, n.sp.pos.y, -0.03f - n.lift.x);
            // slight 3D tilt so the thick block catches the perspective; leans toward you on hover
            float hx = hv ? -4f : 0f, hy = hv ? 5f : 0f;
            n.tr.localRotation = Quaternion.Euler(n.tx + hx, n.ty + hy, n.sp.rot + (hv ? Mathf.Sin(t * 7f) * 1.5f : 0f));
            float d2 = (n.sp.pos - new Vector2(lx.x, ly.x)).sqrMagnitude;
            float m = Mathf.Lerp(0.5f, 1f, Mathf.Exp(-d2 / 0.5f));
            n.mat.color = new Color(m, m, m, 1f);
            n.live = n.sp.enabled == null || n.sp.enabled();
            if (n.xm.Count > 0)
            {
                float k = n.live ? m : m * 0.5f;
                for (int q = 0; q < n.xm.Count; q++) { Color c = n.xc[q]; n.xm[q].color = new Color(c.r * k, c.g * k, c.b * k, 1f); }
            }

            // shadow slides away from the lamp head and grows with the lift
            Vector2 dir = n.sp.pos - lampLocal; dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.down;
            Vector2 off = dir * (0.05f + n.lift.x * 1.2f);
            n.shadow.localPosition = new Vector3(n.sp.pos.x + off.x, n.sp.pos.y + off.y - 0.012f, -0.012f);
            n.shadow.localScale = new Vector3(n.sp.size.x * 1.12f * s * (1f + n.lift.x), n.sp.size.y * 1.15f * s * (1f + n.lift.x), 1f);
            n.shadow.localRotation = Quaternion.Euler(0f, 0f, n.sp.rot);
            n.shMat.color = new Color(0.12f, 0.08f, 0.2f, 0.45f * Mathf.Clamp01(s));
        }
    }

    Vector3 ShadowPt(Vector3 p) { const float ty = -0.612f; float h = p.y - ty; return new Vector3(p.x - 0.45f * h, ty, p.z + 0.3f * h); }

    void Lamp(float dt)
    {
        Vector2 aim = hover >= 0 && hover < notes.Count ? notes[hover].sp.pos : (notes.Count > 0 ? new Vector2(lx.target, ly.target) : Vector2.zero);
        if (hover < 0 && lx.target == 0f && ly.target == 0f && notes.Count > 0) aim = notes[0].sp.pos;
        lx.target = aim.x; ly.target = aim.y; lx.Step(dt, 70f, 8f); ly.Step(dt, 70f, 8f);

        Vector3 beamEnd = boardRoot.TransformPoint(new Vector3(lx.x, ly.x, -0.05f));
        {   // warm pool, cropped to the face of the board so no light ever spills past the frame
            const float hw = 1.7f, hh = 1.05f, fx = 1.49f, fy = 0.79f;
            float x0 = Mathf.Max(lx.x - hw, -fx), x1 = Mathf.Max(x0 + 0.01f, Mathf.Min(lx.x + hw, fx)), y0 = Mathf.Max(ly.x - hh, -fy), y1 = Mathf.Max(y0 + 0.01f, Mathf.Min(ly.x + hh, fy));
            glowV[0] = new Vector3(x0, y0, 0f); glowV[1] = new Vector3(x0, y1, 0f); glowV[2] = new Vector3(x1, y1, 0f); glowV[3] = new Vector3(x1, y0, 0f);
            for (int i = 0; i < 4; i++) glowUV[i] = new Vector2((glowV[i].x - lx.x) / (2f * hw) + 0.5f, (glowV[i].y - ly.x) / (2f * hh) + 0.5f);
            glowMesh.vertices = glowV; glowMesh.uv = glowUV; glowMesh.RecalculateBounds();
        }

        Vector3 aimRest = beamEnd - root.position - new Vector3(0f, board.s.x, 0f);          // rest frame (no layer offsets)
        Vector3 want = aimRest + new Vector3(0.8f, 0.55f, -0.45f);
        hs[0].target = want.x; hs[1].target = want.y; hs[2].target = want.z;
        foreach (var sp in hs) sp.Step(dt, 55f, 6.5f);                                       // springy overshoot
        Vector3 Hw = root.position + new Vector3(hs[0].x, hs[1].x + table.s.x, hs[2].x);
        Vector3 S0 = root.position + LampBase + new Vector3(0f, 0.08f + table.s.x, 0f);      // top of the base
        Vector3 Sw = root.position + LampBase + new Vector3(0f, 0.45f + table.s.x, 0f);      // shoulder: top of the post

        Vector3 v = Hw - Sw; float d = Mathf.Clamp(v.magnitude, 0.35f, L1 + L2 - 0.03f); Vector3 dir = v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.up;
        Hw = Sw + dir * d;
        float a = (d * d + L1 * L1 - L2 * L2) / (2f * d), h = Mathf.Sqrt(Mathf.Max(0f, L1 * L1 - a * a));
        Vector3 pole = Vector3.Cross(Vector3.Cross(dir, Vector3.up), dir); pole = pole.sqrMagnitude > 1e-6f ? pole.normalized : Vector3.up;
        Vector3 E = Sw + dir * a + pole * h;

        // every segment starts exactly on the previous joint: base -> post -> shoulder -> arm -> elbow -> arm -> head
        DeskKit.Seg(post.t, S0, Sw, 0.04f); DeskKit.Seg(armA.t, Sw, E, 0.035f); DeskKit.Seg(armB.t, E, Hw, 0.03f);
        jS.t.position = Sw; jE.t.position = E; jH.t.position = Hw;

        Vector3 look = beamEnd - Hw; if (look.sqrMagnitude < 1e-6f) look = Vector3.forward;
        Quaternion rot = Quaternion.LookRotation(look.normalized);
        headT.position = Hw; headT.rotation = rot;
        Vector3 axis = rot * Vector3.forward; float len = look.magnitude;

        // light cone starts at the shade opening
        Vector3 st = Hw + axis * 0.47f - root.position, en = beamEnd - root.position;
        Vector3 bu = Vector3.Cross(axis, Vector3.up); bu = bu.sqrMagnitude > 1e-6f ? bu.normalized : Vector3.right; Vector3 bw = Vector3.Cross(axis, bu);
        float room = Mathf.Min(1.45f - Mathf.Abs(lx.x), 0.75f - Mathf.Abs(ly.x)); float r0 = 0.26f, r1 = Mathf.Clamp(Mathf.Min(len * 0.3f, room), 0.22f, 0.6f);
        for (int i = 0; i < BS; i++)
        {
            float ang = i * Mathf.PI * 2f / BS; Vector3 off = bu * Mathf.Cos(ang) + bw * Mathf.Sin(ang);
            bv[i] = st + off * r0; bv[BS + i] = en + off * r1;
        }
        beamMesh.vertices = bv; beamMesh.RecalculateBounds();

        // cast shadows on the desk
        Vector3 lay = new Vector3(0f, table.s.x, 0f);
        Vector3 rS = Sw - root.position - lay, rE = E - root.position - lay, rH = Hw - root.position - lay;
        Vector3 ps = ShadowPt(rS) + root.position + lay, pe = ShadowPt(rE) + root.position + lay, ph = ShadowPt(rH) + root.position + lay;
        Ribbon(shA, ps, pe, 0.07f); Ribbon(shB, pe, ph, 0.055f);
        shHead.position = ph; shHead.rotation = Quaternion.Euler(90f, 0f, 0f);

        lampLocal = (Vector2)boardRoot.InverseTransformPoint(Hw);
        float vis = Mathf.Clamp01(1f - (Mathf.Abs(board.s.x) + Mathf.Abs(table.s.x)) / 2.5f);
        beamMat.color = new Color(1f, 1f, 1f, vis); glowMat.color = new Color(1f, 0.9f, 0.55f, 0.5f * vis); poolMat.color = new Color(1f, 0.8f, 0.45f, 0.32f * vis);
        poolT.localPosition = new Vector3(aimRest.x, -0.616f, Mathf.Min(aimRest.z - 0.5f, 3.3f));
    }
}


/// <summary>Pause-menu view of the graphics settings (data and effect wiring live in GameSettings).</summary>
public static class GfxPrefs
{
    public static readonly string[] Names = GameSettings.GfxNames;
    public static bool Get(int i) { return GameSettings.GetOn(i); }
    public static void Set(int i, bool on) { GameSettings.SetOn(i, on); }
    public static float Amt(int i) { return GameSettings.GetAmt(i); }
    public static void SetAmt(int i, float a) { GameSettings.SetAmt(i, a); }
}

public class GfxApplier : MonoBehaviour
{
    float next;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { var g = new GameObject("GfxApplier"); DontDestroyOnLoad(g); g.AddComponent<GfxApplier>(); AudioListener.volume = GameSettings.Master; }
    void Update()
    {
        if (Time.unscaledTime < next) return; next = Time.unscaledTime + 1f;   // scenes create their own effect objects, so saved choices are re-applied
        GameSettings.ApplyAll();
    }
}
