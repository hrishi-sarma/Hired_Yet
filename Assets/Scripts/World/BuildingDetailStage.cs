using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Procedural World/Stages/Building Details")]
public class BuildingDetailStage : WorldStage
{
    public override int Order { get { return StageOrder.Props + 60; } }
    public override string StageName { get { return "Building Details"; } }

    class MB
    {
        public List<Vector3> v = new List<Vector3>();
        public List<Vector3> n = new List<Vector3>();
        public List<Vector2> uv = new List<Vector2>();
        public List<int> t = new List<int>();
        public bool collide;

        public void Quad(Vector3 o, Vector3 r, Vector3 u, float w, float h, float uw, float vh)
        {
            Vector3 nn = Vector3.Cross(u, r).normalized;
            int i = v.Count;
            v.Add(o); v.Add(o + r * w); v.Add(o + u * h); v.Add(o + r * w + u * h);
            for (int k = 0; k < 4; k++) n.Add(nn);
            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(uw, 0f)); uv.Add(new Vector2(0f, vh)); uv.Add(new Vector2(uw, vh));
            t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i + 2); t.Add(i + 3); t.Add(i + 1);
        }

        public void Box(Vector3 c, Vector3 r, Vector3 u, Vector3 nrm, float sx, float sy, float sz, float tile)
        {
            float tx = Mathf.Max(0.25f, sx / tile), ty = Mathf.Max(0.25f, sy / tile), tz = Mathf.Max(0.25f, sz / tile);
            Quad(c + nrm * (sz * 0.5f) - r * (sx * 0.5f) - u * (sy * 0.5f), r, u, sx, sy, tx, ty);
            Quad(c - nrm * (sz * 0.5f) + r * (sx * 0.5f) - u * (sy * 0.5f), -r, u, sx, sy, tx, ty);
            Quad(c + u * (sy * 0.5f) + nrm * (sz * 0.5f) - r * (sx * 0.5f), r, -nrm, sx, sz, tx, tz);
            Quad(c - u * (sy * 0.5f) - nrm * (sz * 0.5f) - r * (sx * 0.5f), r, nrm, sx, sz, tx, tz);
            Quad(c + r * (sx * 0.5f) + nrm * (sz * 0.5f) - u * (sy * 0.5f), -nrm, u, sz, sy, tz, ty);
            Quad(c - r * (sx * 0.5f) - nrm * (sz * 0.5f) - u * (sy * 0.5f), nrm, u, sz, sy, tz, ty);
        }
    }

    struct Fc
    {
        public Vector3 o, r, n;
        public float w;
        public Vector3 P(float x, float y, float z) { return o + r * x + Vector3.up * y + n * z; }
    }

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static readonly Dictionary<string, Texture2D> texs = new Dictionary<string, Texture2D>();
    static readonly Color[] doorTints =
    {
        new Color(0.45f, 0.12f, 0.12f), new Color(0.12f, 0.2f, 0.4f), new Color(0.13f, 0.3f, 0.2f), new Color(0.1f, 0.1f, 0.11f),
        new Color(0.75f, 0.55f, 0.15f), new Color(0.1f, 0.4f, 0.42f), new Color(0.45f, 0.28f, 0.15f)
    };
    static readonly Color[] trimTints =
    {
        new Color(0.88f, 0.85f, 0.78f), new Color(0.72f, 0.62f, 0.52f), new Color(0.3f, 0.31f, 0.34f), new Color(0.8f, 0.74f, 0.62f)
    };
    static readonly Color[] awnTints =
    {
        new Color(0.75f, 0.18f, 0.16f), new Color(0.15f, 0.35f, 0.6f), new Color(0.2f, 0.5f, 0.3f), new Color(0.9f, 0.62f, 0.15f), new Color(0.3f, 0.2f, 0.4f)
    };
    static readonly Color[] warm =
    {
        new Color(1f, 0.72f, 0.38f), new Color(1f, 0.82f, 0.55f), new Color(1f, 0.9f, 0.72f)
    };

    public override void Run(WorldContext c)
    {
        mats.Clear();
        texs.Clear();
        WorldRng rng = c.Rng("buildingdetail");
        GameObject rootGo = new GameObject("BuildingDetails");
        rootGo.transform.SetParent(c.LayerRoot(PlacementLayer.Prop), false);
        List<SpawnRecord> list = new List<SpawnRecord>();
        for (int i = 0; i < c.occupancy.Records.Count; i++)
            if (c.occupancy.Records[i].layer == PlacementLayer.Building) list.Add(c.occupancy.Records[i]);
        int done = 0;
        for (int i = 0; i < list.Count; i++)
            if (Decorate(c, list[i], rng, rootGo.transform)) done++;
        Debug.Log("Building Details: decorated " + done + " buildings.");
    }

    delegate Color PixFn(float u, float v);

    static Texture2D Tex(string key, int w, int h, PixFn fn)
    {
        Texture2D t;
        if (texs.TryGetValue(key, out t) && t != null) return t;
        t = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
        t.name = key;
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = fn((x + 0.5f) / w, (y + 0.5f) / h);
        t.SetPixels(px);
        t.Apply(true, false);
        texs[key] = t;
        return t;
    }

    static float Nz(float u, float v, int s)
    {
        return DetailTex.Pn(Mathf.Repeat(u, 1f), Mathf.Repeat(v, 1f), s, 3);
    }

    static bool PaneFrame(int kind, float u, float v)
    {
        if (u < 0.12f || u > 0.88f || v < 0.09f || v > 0.91f) return true;
        if (kind == 0) return Mathf.Abs(u - 0.5f) < 0.045f || Mathf.Abs(v - 0.5f) < 0.045f;
        if (kind == 1) return Mathf.Abs(v - 0.5f) < 0.05f;
        if (kind == 2) return Mathf.Abs(u - 0.333f) < 0.03f || Mathf.Abs(u - 0.667f) < 0.03f;
        return Mathf.Abs(u - 0.333f) < 0.04f || Mathf.Abs(u - 0.667f) < 0.04f || Mathf.Abs(v - 0.76f) < 0.04f;
    }

    static Texture2D PaneTex(int kind, bool emission)
    {
        return Tex("pane" + kind + (emission ? "e" : "b"), 128, 128, (u, v) =>
        {
            bool outline = u < 0.035f || u > 0.965f || v < 0.03f || v > 0.97f;
            bool fr = PaneFrame(kind, u, v);
            bool edge = !fr && (PaneFrame(kind, u - 0.035f, v) || PaneFrame(kind, u + 0.035f, v) || PaneFrame(kind, u, v - 0.035f) || PaneFrame(kind, u, v + 0.035f));
            float d = u - v * 0.7f;
            bool glint = (d > 0.30f && d < 0.40f) || (d > 0.48f && d < 0.52f);
            if (emission)
            {
                if (fr || outline) return Color.black;
                float k = edge ? 0.55f : (glint ? 1f : (v < 0.4f ? 0.72f : 0.88f));
                if (v > 0.8f) k *= 0.7f;
                return new Color(1f, 0.8f, 0.45f) * k;
            }
            if (outline) return new Color(0.13f, 0.15f, 0.22f);
            if (fr) return v < 0.17f ? new Color(0.8f, 0.74f, 0.62f) : new Color(0.97f, 0.94f, 0.84f);
            if (edge) return new Color(0.2f, 0.36f, 0.55f);
            if (glint) return new Color(0.86f, 0.96f, 1f);
            return v < 0.4f ? new Color(0.36f, 0.62f, 0.82f) : new Color(0.47f, 0.74f, 0.9f);
        });
    }

    static Texture2D CurtainTex(int variant, bool emission)
    {
        return Tex("curtain" + variant + (emission ? "e" : "b"), 256, 256, (u, v) =>
        {
            int cx = Mathf.Min(7, (int)(u * 8f)), cy = Mathf.Min(7, (int)(v * 8f));
            float lu = u * 8f - cx, lv = v * 8f - cy;
            System.Random cr = new System.Random(variant * 7919 + cx * 131 + cy * 17);
            System.Random rr = new System.Random(variant * 104729 + cy * 31);
            float rowBias = (float)rr.NextDouble();
            bool lit = cr.NextDouble() < 0.18 + 0.4 * rowBias * rowBias;
            float tintJ = (float)cr.NextDouble();
            bool frame = lu < 0.06f || lu > 0.94f || lv < 0.05f || lv > 0.95f;
            bool spandrel = variant % 3 == 1 && lv < 0.34f;
            bool pier = variant % 3 == 2 && (lu < 0.2f || lu > 0.8f);
            bool solid = frame || spandrel || pier;
            if (emission)
            {
                if (solid || !lit) return Color.black;
                float blind = Mathf.Repeat(lv * 7f, 1f) < 0.14f ? 0.7f : 1f;
                Color w = Color.Lerp(warm[0], warm[2], tintJ);
                return w * (0.75f + 0.25f * (1f - lv)) * blind;
            }
            if (solid) return Color.Lerp(new Color(0.34f, 0.37f, 0.42f), new Color(0.5f, 0.53f, 0.58f), Nz(u * 4f, v * 4f, 51));
            Color g = Color.Lerp(new Color(0.16f, 0.28f, 0.4f), new Color(0.42f, 0.6f, 0.74f), Mathf.Clamp01(lv * 0.8f + tintJ * 0.25f));
            if (Mathf.Abs(lu - (0.2f + 0.6f * lv)) < 0.08f) g += new Color(0.1f, 0.1f, 0.08f);
            return g;
        });
    }

    static Texture2D StoreTex(bool emission)
    {
        return Tex("store" + (emission ? "e" : "b"), 128, 64, (u, v) =>
        {
            bool fr = u < 0.03f || u > 0.97f || v > 0.95f || Mathf.Abs(u - 0.5f) < 0.012f || v < 0.1f || Mathf.Abs(v - 0.8f) < 0.015f;
            if (emission)
            {
                if (fr) return Color.black;
                return new Color(1f, 0.85f, 0.6f) * (0.7f + 0.3f * Nz(u * 3f, v * 3f, 61));
            }
            if (v < 0.1f) return new Color(0.2f, 0.2f, 0.22f);
            if (fr) return new Color(0.16f, 0.17f, 0.19f);
            return Color.Lerp(new Color(0.22f, 0.35f, 0.46f), new Color(0.5f, 0.66f, 0.76f), v);
        });
    }

    static Texture2D StoneTex()
    {
        return Tex("stone", 64, 64, (u, v) =>
        {
            float row = Mathf.Floor(v * 4f);
            float uu = Mathf.Repeat(u * 4f + (row % 2f) * 0.5f, 1f);
            float vv = Mathf.Repeat(v * 4f, 1f);
            float mortar = (uu < 0.06f || vv < 0.08f) ? 0.7f : 1f;
            float n = 0.88f + 0.12f * Nz(u * 3f, v * 3f, 71);
            float c = mortar * n;
            return new Color(c, c * 0.98f, c * 0.94f);
        });
    }

    static Texture2D GratingTex()
    {
        return Tex("grating", 64, 64, (u, v) =>
        {
            float a = Mathf.Repeat(u * 8f, 1f), b = Mathf.Repeat(v * 8f, 1f);
            bool slot = a > 0.25f && a < 0.75f && b > 0.25f && b < 0.75f;
            float n = Nz(u * 4f, v * 4f, 81);
            return slot ? new Color(0.03f, 0.03f, 0.04f) : new Color(0.2f, 0.21f, 0.22f) * (0.8f + 0.4f * n);
        });
    }

    static Texture2D AwningTex()
    {
        return Tex("awning", 64, 64, (u, v) =>
        {
            float s = Mathf.Repeat(u * 8f, 2f) < 1f ? 1f : 0.42f;
            float n = 0.92f + 0.08f * Nz(u * 6f, v * 6f, 91);
            return new Color(s, s, s) * n;
        });
    }

    static Texture2D DoorTexture()
    {
        return Tex("doorwood", 64, 128, (u, v) =>
        {
            bool border = u < 0.1f || u > 0.9f || v < 0.05f || v > 0.95f || Mathf.Abs(v - 0.5f) < 0.03f;
            bool panelEdge = (u > 0.2f && u < 0.24f || u > 0.76f && u < 0.8f) && v > 0.1f && v < 0.9f;
            float grain = 0.82f + 0.18f * Nz(u * 8f, v * 1.5f, 101);
            float c = border ? 1f : (panelEdge ? 0.55f : 0.78f);
            return new Color(c, c, c) * grain;
        });
    }

    static Texture2D RollTex()
    {
        return Tex("roll", 32, 64, (u, v) =>
        {
            float s = Mathf.Repeat(v * 12f, 1f);
            float c = s < 0.12f ? 0.55f : 0.9f - 0.15f * s;
            return new Color(c, c, c);
        });
    }

    static Material Mk(string key, Texture2D map, Color tint, float smooth, Texture2D emis, Color emisColor, float emisStrength, float emisMax, bool registerNight)
    {
        Material m;
        if (mats.TryGetValue(key, out m) && m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh);
        m.name = "BD_" + key;
        Texture2D t = map != null ? map : Texture2D.whiteTexture;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (emis != null)
        {
            if (registerNight) StylizedMaterialFactory.RegisterEmissive(m, emisColor, emisStrength, emisMax, emis);
            else
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", emis);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emisColor * emisStrength * emisMax);
            }
        }
        mats[key] = m;
        return m;
    }

    static Material PaneMat(int kind, int lit)
    {
        if (lit == 0) return Mk("pane" + kind, PaneTex(kind, false), Color.white, 0.75f, null, Color.black, 0f, 0f, false);
        return Mk("pane" + kind + "L" + lit, PaneTex(kind, false), Color.white, 0.6f, PaneTex(kind, true), warm[lit - 1], 1f, 1.5f, true);
    }

    static Material CurtainMat(int variant)
    {
        return Mk("curtain" + variant, CurtainTex(variant, false), Color.white, 0.8f, CurtainTex(variant, true), new Color(1f, 0.95f, 0.85f), 1f, 1.6f, true);
    }

    static Material StoreMat(bool lit)
    {
        if (!lit) return Mk("store", StoreTex(false), Color.white, 0.7f, null, Color.black, 0f, 0f, false);
        return Mk("storeL", StoreTex(false), Color.white, 0.6f, StoreTex(true), new Color(1f, 0.88f, 0.65f), 1f, 1.4f, true);
    }

    static Material StoneMat(Color tint) { return Mk("stone" + tint.GetHashCode(), StoneTex(), tint, 0.1f, null, Color.black, 0f, 0f, false); }
    static Material StepMat() { return Mk("step", StoneTex(), new Color(0.75f, 0.72f, 0.68f), 0.1f, null, Color.black, 0f, 0f, false); }
    static Material GratingMat() { return Mk("grating", GratingTex(), Color.white, 0.3f, null, Color.black, 0f, 0f, false); }
    static Material AwningMat(Color tint) { return Mk("awn" + tint.GetHashCode(), AwningTex(), tint, 0.15f, null, Color.black, 0f, 0f, false); }
    static Material DoorMat(Color tint) { return Mk("door" + tint.GetHashCode(), DoorTexture(), tint, 0.3f, null, Color.black, 0f, 0f, false); }
    public static Material CreateDoorMaterial(Color tint) { return DoorMat(tint); }
    static Material RollMat(Color tint) { return Mk("roll" + tint.GetHashCode(), RollTex(), tint, 0.35f, null, Color.black, 0f, 0f, false); }
    static Material Flat(string key, Color tint, float smooth) { return Mk("flat" + key, null, tint, smooth, null, Color.black, 0f, 0f, false); }
    static Material Marquee() { return Mk("marquee", null, new Color(1f, 0.8f, 0.4f), 0.2f, Texture2D.whiteTexture, new Color(1f, 0.75f, 0.3f), 1f, 2.2f, true); }

    static MB Get(Dictionary<Material, MB> d, Material m)
    {
        MB b;
        if (!d.TryGetValue(m, out b)) { b = new MB(); d[m] = b; }
        return b;
    }

    static void Bx(Dictionary<Material, MB> d, Material m, Fc f, float x, float y, float z, float sx, float sy, float sz)
    {
        Get(d, m).Box(f.P(x, y, z), f.r, Vector3.up, f.n, sx, sy, sz, 1f);
    }

    static void Fq(Dictionary<Material, MB> d, Material m, Fc f, float x, float y, float z, float w, float h)
    {
        Get(d, m).Quad(f.P(x, y, z), f.r, Vector3.up, w, h, 1f, 1f);
    }

    static void Awn(Dictionary<Material, MB> d, Material m, Fc f, float x, float w, float y, float depth, float drop)
    {
        float len = Mathf.Sqrt(depth * depth + drop * drop);
        Vector3 s = (-f.n * depth + Vector3.up * drop) / len;
        MB b = Get(d, m);
        float stripes = Mathf.Max(1f, w / 1.6f);
        b.Quad(f.P(x - w * 0.5f, y, depth), f.r, s, w, len, stripes, 1f);
        b.Quad(f.P(x + w * 0.5f, y, depth), -f.r, s, w, len, stripes, 1f);
        b.Quad(f.P(x - w * 0.5f, y - 0.25f, depth), f.r, Vector3.up, w, 0.25f, stripes, 1f);
    }

    static bool Reserve(WorldContext c, Fc f, float x, float width, float depth, float height)
    {
        Vector3 ctr = f.P(x, 0f, depth * 0.5f + 0.06f);
        float yaw = Mathf.Atan2(f.n.x, f.n.z) * Mathf.Rad2Deg;
        OrientedBox b = OrientedBox.FromBase(ctr, new Vector3(width, height, depth), yaw);
        return c.occupancy.TryReserve(b, PlacementLayer.Prop, 0.05f, null);
    }

    static void Door(Dictionary<Material, MB> d, Fc f, float x, float y, int type, Color tint, Material trim, WorldRng rng)
    {
        float w = type == 1 ? 1.9f : 1.05f;
        float h = type == 1 ? 2.3f : 2.15f;
        float tr = type == 0 ? 0.5f : 0f;
        Material leaf = type == 1 ? StoreMat(rng.Chance(0.5f)) : DoorMat(tint);
        Fq(d, leaf, f, x - w * 0.5f, y, 0.03f, w, h);
        if (tr > 0f) Fq(d, PaneMat(2, rng.Chance(0.5f) ? 1 : 0), f, x - w * 0.5f, y + h, 0.03f, w, tr);
        float fh = h + tr + 0.14f;
        Bx(d, trim, f, x - w * 0.5f - 0.07f, y + fh * 0.5f, 0.07f, 0.14f, fh, 0.14f);
        Bx(d, trim, f, x + w * 0.5f + 0.07f, y + fh * 0.5f, 0.07f, 0.14f, fh, 0.14f);
        Bx(d, trim, f, x, y + h + tr + 0.07f, 0.07f, w + 0.28f, 0.14f, 0.14f);
        if (type != 1) Bx(d, Flat("brass", new Color(0.8f, 0.6f, 0.2f), 0.7f), f, x + w * 0.5f - 0.15f, y + 1.0f, 0.08f, 0.07f, 0.07f, 0.07f);
    }

    static void Stoop(Dictionary<Material, MB> d, Fc f, float x, float width, int count, float rise)
    {
        Material st = StepMat();
        MB b = Get(d, st);
        b.collide = true;
        float L = 1.1f, run = 0.3f;
        float H = (count + 1) * rise;
        b.Box(f.P(x, H * 0.5f, L * 0.5f + 0.02f), f.r, Vector3.up, f.n, width, H, L, 1f);
        for (int j = 0; j < count; j++)
        {
            float hh = (count - j) * rise;
            b.Box(f.P(x, hh * 0.5f, L + (j + 0.5f) * run), f.r, Vector3.up, f.n, width, hh, run, 1f);
        }
        Material iron = Flat("iron", new Color(0.08f, 0.08f, 0.09f), 0.4f);
        for (int s = -1; s <= 1; s += 2)
        {
            float sx = x + s * (width * 0.5f + 0.05f);
            for (int j = 0; j < count; j++)
            {
                float hh = (count - j) * rise + 0.45f;
                b.Box(f.P(sx, hh * 0.5f, L + (j + 0.5f) * run), f.r, Vector3.up, f.n, 0.1f, hh, run, 1f);
            }
            Bx(d, iron, f, sx, H + 0.9f, L * 0.5f, 0.05f, 0.05f, L);
            Bx(d, iron, f, sx, H + 0.45f, L - 0.03f, 0.05f, 0.9f, 0.05f);
            Bx(d, iron, f, sx, H + 0.45f, 0.05f, 0.05f, 0.9f, 0.05f);
        }
    }

    static void FireEscape(Dictionary<Material, MB> d, Fc f, int floors, float floorH, float W)
    {
        Material g = GratingMat();
        Material iron = Flat("iron", new Color(0.08f, 0.08f, 0.09f), 0.4f);
        float fw = Mathf.Min(3.0f, W * 0.45f);
        MB gb = Get(d, g);
        for (int fl = 1; fl < floors; fl++)
        {
            float y = fl * floorH;
            gb.Box(f.P(0f, y - 0.04f, 0.55f), f.r, Vector3.up, f.n, fw, 0.08f, 1.1f, 0.6f);
            Bx(d, iron, f, 0f, y + 0.95f, 1.08f, fw, 0.05f, 0.05f);
            Bx(d, iron, f, -fw * 0.5f, y + 0.95f, 0.55f, 0.05f, 0.05f, 1.1f);
            Bx(d, iron, f, fw * 0.5f, y + 0.95f, 0.55f, 0.05f, 0.05f, 1.1f);
            Bx(d, iron, f, -fw * 0.5f, y + 0.45f, 1.08f, 0.05f, 0.9f, 0.05f);
            Bx(d, iron, f, fw * 0.5f, y + 0.45f, 1.08f, 0.05f, 0.9f, 0.05f);
            if (fl + 1 < floors)
            {
                bool fwd = fl % 2 == 0;
                float run = fw - 0.7f;
                float xa = fwd ? -fw * 0.5f + 0.35f : fw * 0.5f - 0.35f;
                Vector3 dir = (fwd ? f.r : -f.r) * run + Vector3.up * floorH;
                float len = dir.magnitude;
                Vector3 s = dir / len;
                gb.Quad(f.P(xa, y, 0.2f), f.n, s, 0.7f, len, 0.7f / 0.6f, len / 0.6f);
                gb.Quad(f.P(xa, y, 0.9f), -f.n, s, 0.7f, len, 0.7f / 0.6f, len / 0.6f);
            }
        }
    }

    bool Decorate(WorldContext c, SpawnRecord rec, WorldRng rng, Transform root)
    {
        OrientedBox box = rec.box;
        float h = box.maxY - box.minY;
        if (h < 3.5f) return false;
        if (c.zoneMap == null) return false;
        // lots placed in the City zone are always city buildings, even if the zone map reads differently at their centre
        bool cityLot = rec.owner != null && BuildingStage.CityBuildingIds.Contains(rec.owner.GetInstanceID());
        ZoneType zc = cityLot ? ZoneType.City : c.zoneMap.ZoneAt(box.center);
        if (zc != ZoneType.City && zc != ZoneType.Open) return false;
        string name = rec.owner != null ? rec.owner.name : "";
        ZoneType zone = zc;
        bool interview = InterviewDoorStage.HasChosen && (InterviewDoorStage.ChosenCenter - box.center).sqrMagnitude < 0.01f;
        bool sky = h >= 40f;
        bool mid = !sky && h >= 14f;
        bool parking = name.Contains("Parking");
        bool warehouse = name.Contains("Warehouse") || name.Contains("Workshop");
        bool fireSt = name.Contains("Fire");
        bool cinema = name.Contains("Cinema");
        bool shop = name.Contains("Shop") || name.Contains("Corner") || name.Contains("Cafe");
        bool stoopType = name.Contains("Row") || name.Contains("House") || name.Contains("Cottage") || name.Contains("Flats");
        bool house = zone == ZoneType.Suburb && (name.Contains("House") || name.Contains("Cottage"));
        float groundY = box.minY + 0.3f;
        float hh = h - 0.3f;
        float floorH = sky ? 3.6f : (mid ? 3.4f : 3.1f);
        Color trimC = trimTints[rng.Range(0, trimTints.Length)];
        Material trim = StoneMat(trimC);
        Color doorTint = doorTints[rng.Range(0, doorTints.Length)];
        Dictionary<Material, MB> mb = new Dictionary<Material, MB>();
        int layout = mid ? rng.Range(0, 2) : rng.Range(0, 4);
        if (warehouse || cinema) layout = 2;
        float ww, wh, pitch, sill;
        int kind = layout;
        if (layout == 0) { ww = 1.3f; wh = 1.7f; pitch = 3.0f; sill = 0.9f; }
        else if (layout == 1) { ww = 1.0f; wh = 2.1f; pitch = 2.4f; sill = 0.7f; }
        else if (layout == 2) { ww = 2.8f; wh = 1.3f; pitch = 4.0f; sill = 1.0f; }
        else { ww = 2.2f; wh = 1.6f; pitch = 3.6f; sill = 0.9f; }
        if (mid) { ww *= 1.15f; wh *= 1.15f; pitch *= 1.1f; }
        int floors = Mathf.Max(1, Mathf.FloorToInt((hh - 0.5f) / floorH));
        int curtainVar = rng.Range(0, 3);
        Color shutter = new Color(0.25f, 0.42f, 0.35f);
        float sr = rng.Value();
        if (sr < 0.33f) shutter = new Color(0.28f, 0.34f, 0.52f); else if (sr < 0.66f) shutter = new Color(0.5f, 0.2f, 0.18f);
        bool shutters = house && rng.Chance(0.55f);
        int feFace = (mid && rng.Chance(0.7f)) ? 2 + rng.Range(0, 2) : -1;

        Vector2[] ns = { box.axisZ, -box.axisZ, box.axisX, -box.axisX };
        for (int fi = 0; fi < 4; fi++)
        {
            Vector2 n2 = ns[fi];
            bool alongX = fi < 2;
            Fc f = new Fc();
            float W = alongX ? box.half.x * 2f : box.half.y * 2f;
            float dist = alongX ? box.half.y : box.half.x;
            f.n = new Vector3(n2.x, 0f, n2.y);
            f.r = Vector3.Cross(f.n, Vector3.up);
            f.o = new Vector3(box.center.x, groundY, box.center.y) + f.n * dist;
            f.w = W;
            bool front = fi == 0;

            if (interview)
            {
                Material gold = Mk("gold", null, new Color(1f, 0.78f, 0.1f), 0.4f, Texture2D.whiteTexture, new Color(1f, 0.78f, 0.1f), 1f, 3f, false);
                float top = hh + 10f;
                Bx(mb, gold, f, 0f, top, 0.75f, W + 1.5f, 0.6f, 0.6f);
                if (fi < 2)
                    for (int ps = -1; ps <= 1; ps += 2)
                        Bx(mb, gold, f, ps * (W * 0.5f + 0.75f), top * 0.5f, 0.75f, 0.6f, top + 0.6f, 0.6f);
                if (fi == 0)
                {
                    GameObject bl = new GameObject("InterviewBeaconLight");
                    bl.transform.SetParent(root, false);
                    bl.transform.position = new Vector3(box.center.x, groundY + top + 3f, box.center.y);
                    Light lt = bl.AddComponent<Light>();
                    lt.type = LightType.Point;
                    lt.color = new Color(1f, 0.8f, 0.25f);
                    lt.range = 160f;
                    lt.intensity = 8f;
                    lt.shadows = LightShadows.None;
                }
            }

            if (sky)
            {
                SkyFace(mb, f, hh, front && interview, curtainVar, trim);
                if (front && !interview) SkyEntrance(c, mb, f, trim, rng);
                continue;
            }

            float doorX = 0f, doorW = 0f;
            bool store = false;
            if (front && !interview)
            {
                bool stoopOk = false;
                store = (shop || (zone == ZoneType.City && h < 12f && !stoopType && !parking && !warehouse && !fireSt)) && W >= 7f && !cinema;
                if (store)
                {
                    doorW = 1.9f;
                    doorX = W * 0.5f - 1.6f;
                    float gx0 = -W * 0.5f + 0.6f, gx1 = doorX - doorW * 0.5f - 0.35f;
                    float gw = gx1 - gx0;
                    if (gw > 1.5f)
                    {
                        MB sb = Get(mb, StoreMat(rng.Chance(0.55f)));
                        sb.Quad(f.P(gx0, 0.35f, 0.03f), f.r, Vector3.up, gw, 2.45f, Mathf.Max(1f, Mathf.Round(gw / 2.5f)), 1f);
                        Bx(mb, trim, f, gx0 - 0.12f, 1.5f, 0.07f, 0.24f, 3.0f, 0.14f);
                        Bx(mb, trim, f, gx1 + 0.12f, 1.5f, 0.07f, 0.24f, 3.0f, 0.14f);
                        Bx(mb, trim, f, (gx0 + gx1) * 0.5f, 0.17f, 0.07f, gw + 0.5f, 0.34f, 0.14f);
                    }
                    Door(mb, f, doorX, 0f, 1, doorTint, trim, rng);
                    if (Reserve(c, f, 0f, W - 0.4f, 1.3f, 3f))
                        Awn(mb, AwningMat(awnTints[rng.Range(0, awnTints.Length)]), f, 0f, W - 0.4f, 3.05f, 1.3f, 0.45f);
                }
                else if (cinema)
                {
                    Door(mb, f, 0f, 0f, 1, doorTint, trim, rng);
                    if (Reserve(c, f, 0f, Mathf.Min(W - 1f, 8f), 1.8f, 4f))
                    {
                        float mw = Mathf.Min(W - 1f, 8f);
                        Bx(mb, trim, f, 0f, 3.6f, 0.9f, mw, 0.25f, 1.8f);
                        Bx(mb, Marquee(), f, 0f, 4.0f, 1.82f, mw, 0.55f, 0.06f);
                        Bx(mb, Flat("iron", new Color(0.08f, 0.08f, 0.09f), 0.4f), f, -mw * 0.5f + 0.1f, 1.8f, 1.7f, 0.1f, 3.6f, 0.1f);
                        Bx(mb, Flat("iron", new Color(0.08f, 0.08f, 0.09f), 0.4f), f, mw * 0.5f - 0.1f, 1.8f, 1.7f, 0.1f, 3.6f, 0.1f);
                    }
                    doorW = 2.4f;
                }
                else if (warehouse || fireSt)
                {
                    int bays = fireSt ? 2 : 1;
                    float bw = fireSt ? 3.4f : 3.8f, bh = fireSt ? 3.5f : 3.6f;
                    if (W > bays * (bw + 1f) + 1f && hh > bh + 1f)
                    {
                        for (int bi = 0; bi < bays; bi++)
                        {
                            float bx = bays == 1 ? 0f : (bi == 0 ? -(bw * 0.5f + 0.4f) : bw * 0.5f + 0.4f);
                            Fq(mb, RollMat(fireSt ? new Color(0.85f, 0.85f, 0.82f) : new Color(0.55f, 0.6f, 0.62f)), f, bx - bw * 0.5f, 0f, 0.03f, bw, bh);
                            Bx(mb, trim, f, bx - bw * 0.5f - 0.1f, bh * 0.5f + 0.1f, 0.07f, 0.2f, bh + 0.2f, 0.14f);
                            Bx(mb, trim, f, bx + bw * 0.5f + 0.1f, bh * 0.5f + 0.1f, 0.07f, 0.2f, bh + 0.2f, 0.14f);
                            Bx(mb, trim, f, bx, bh + 0.1f, 0.07f, bw + 0.4f, 0.2f, 0.14f);
                        }
                        doorW = bays * (bw + 1f);
                    }
                }
                else
                {
                    int dt = zone == ZoneType.City ? (mid ? 1 : rng.Range(0, 2)) : 0;
                    doorX = (house || stoopType) ? 0f : rng.Range(-0.2f, 0.2f) * W;
                    doorW = dt == 1 ? 1.9f : 1.05f;
                    float lift = 0f;
                    if (stoopType && dt == 0 && Reserve(c, f, doorX, 2.4f, 2.5f, 1.8f))
                    {
                        int cnt = house ? 3 : 4;
                        Stoop(mb, f, doorX, 2.2f, cnt, 0.17f);
                        lift = (cnt + 1) * 0.17f;
                        stoopOk = true;
                    }
                    if (!stoopOk && Reserve(c, f, doorX, doorW + 0.9f, 0.7f, 0.3f))
                    {
                        Bx(mb, StepMat(), f, doorX, 0.075f, 0.35f, doorW + 0.8f, 0.15f, 0.6f);
                        lift = 0.15f;
                    }
                    Door(mb, f, doorX, lift, dt, doorTint, trim, rng);
                    if (mid && dt == 1 && Reserve(c, f, doorX, 3.6f, 1.6f, 3.4f))
                    {
                        Bx(mb, Flat("iron", new Color(0.12f, 0.12f, 0.14f), 0.5f), f, doorX, 3.0f, 0.8f, 3.6f, 0.18f, 1.6f);
                        Bx(mb, Flat("iron", new Color(0.12f, 0.12f, 0.14f), 0.5f), f, doorX - 1.7f, 1.5f, 1.45f, 0.08f, 3.0f, 0.08f);
                        Bx(mb, Flat("iron", new Color(0.12f, 0.12f, 0.14f), 0.5f), f, doorX + 1.7f, 1.5f, 1.45f, 0.08f, 3.0f, 0.08f);
                    }
                    else if (!mid && zone == ZoneType.City && dt == 0 && !stoopOk && Reserve(c, f, doorX, doorW + 1.2f, 1.1f, 3f))
                        Awn(mb, AwningMat(awnTints[rng.Range(0, awnTints.Length)]), f, doorX, doorW + 1.2f, 2.9f, 1.1f, 0.4f);
                }
            }

            if (parking)
            {
                Material dark = Flat("dark", new Color(0.08f, 0.09f, 0.1f), 0.2f);
                int fl2 = Mathf.Max(1, Mathf.FloorToInt(hh / 3.2f));
                for (int fl = 0; fl < fl2; fl++)
                {
                    float y = fl * 3.2f + 0.8f;
                    Fq(mb, dark, f, -W * 0.5f + 0.8f, y, 0.03f, W - 1.6f, 1.6f);
                    Bx(mb, trim, f, 0f, y - 0.9f + 0.05f, 0.1f, W - 0.2f, 0.2f, 0.2f);
                }
                int pc = Mathf.FloorToInt(W / 5f);
                for (int pi = 1; pi <= pc; pi++)
                    Bx(mb, trim, f, -W * 0.5f + pi * W / (pc + 1), hh * 0.5f, 0.1f, 0.4f, hh - 0.2f, 0.2f);
                continue;
            }

            int cols = Mathf.FloorToInt((W - 1.6f) / pitch);
            if (cols < 1) continue;
            float x0 = -(cols - 1) * pitch * 0.5f;
            int maxFloors = warehouse ? 1 : floors;
            for (int fl = 0; fl < maxFloors; fl++)
            {
                float y0 = fl * floorH + sill;
                float wH = wh;
                if (warehouse) { y0 = Mathf.Max(3.8f, hh - 2.6f); wH = 1.0f; }
                if (y0 + wH > hh - 0.35f) continue;
                if (fl == 0 && front && store) continue;
                for (int ci = 0; ci < cols; ci++)
                {
                    float x = x0 + ci * pitch;
                    if (fl == 0 && front && !interview && doorW > 0f && Mathf.Abs(x - doorX) < doorW * 0.5f + ww * 0.5f + 0.6f) continue;
                    if (fl == 0 && front && interview && Mathf.Abs(x) < 3.2f) continue;
                    if (fl > 0 && fi == feFace && Mathf.Abs(x) < 1.8f) continue;
                    int lit = rng.Chance(0.42f) ? 1 + rng.Range(0, 3) : 0;
                    Material pm = PaneMat(kind, lit);
                    Get(mb, pm).Quad(f.P(x - ww * 0.5f, y0, 0.03f), f.r, Vector3.up, ww, wH, 1f, 1f);
                    MB tb = Get(mb, trim);
                    tb.Quad(f.P(x - ww * 0.5f - 0.12f, y0 - 0.12f, 0.12f), f.r, Vector3.up, ww + 0.24f, 0.12f, 1f, 1f);
                    tb.Quad(f.P(x - ww * 0.5f - 0.12f, y0, 0.12f), f.r, -f.n, ww + 0.24f, 0.09f, 1f, 1f);
                    tb.Quad(f.P(x - ww * 0.5f - 0.12f, y0 - 0.12f, 0.03f), f.r, f.n, ww + 0.24f, 0.09f, 1f, 1f);
                    if (shutters)
                    {
                        Material sm = Flat("shut" + shutter.GetHashCode(), shutter, 0.2f);
                        Fq(mb, sm, f, x - ww * 0.5f - 0.4f, y0, 0.04f, 0.32f, wH);
                        Fq(mb, sm, f, x + ww * 0.5f + 0.08f, y0, 0.04f, 0.32f, wH);
                    }
                }
            }

            if (mid)
            {
                Bx(mb, trim, f, 0f, hh - 0.35f, 0.3f, W + 0.6f, 0.7f, 0.6f);
                Bx(mb, trim, f, 0f, hh - 0.8f, 0.12f, W + 0.2f, 0.12f, 0.24f);
            }
            if (fi == feFace && floors > 2 && W >= 10f && Reserve(c, f, 0f, Mathf.Min(3.0f, W * 0.45f), 1.3f, 2f))
                FireEscape(mb, f, floors, floorH, W);
        }

        Flush(mb, root, name);
        return true;
    }

    static void SkyFace(Dictionary<Material, MB> mb, Fc f, float hh, bool skipLobby, int variant, Material trim)
    {
        float lobby = 4.5f;
        int floors = Mathf.FloorToInt((hh - lobby - 0.9f) / 3.6f);
        if (floors < 2) return;
        int cols = Mathf.Max(2, Mathf.RoundToInt(f.w / 2f));
        float wc = f.w - 0.12f;
        Get(mb, CurtainMat(variant)).Quad(f.P(-wc * 0.5f, lobby, 0.05f), f.r, Vector3.up, wc, floors * 3.6f, cols / 8f, floors / 8f);
        float topY = lobby + floors * 3.6f;
        Bx(mb, trim, f, 0f, topY + 0.3f, 0.15f, f.w + 0.6f, 0.6f, 0.3f);
        Bx(mb, trim, f, 0f, lobby - 0.15f, 0.15f, f.w + 0.6f, 0.3f, 0.3f);
        int pc = Mathf.FloorToInt(f.w / 8f);
        for (int i = 1; i <= pc; i++)
            Bx(mb, trim, f, -f.w * 0.5f + i * f.w / (pc + 1), lobby + floors * 1.8f, 0.2f, 0.45f, floors * 3.6f, 0.3f);
        if (!skipLobby)
        {
            float lw = Mathf.Max(1f, Mathf.Round(f.w / 4f));
            Get(mb, StoreMat(true)).Quad(f.P(-wc * 0.5f, 0.1f, 0.05f), f.r, Vector3.up, wc, lobby - 0.4f, lw, 1f);
        }
    }

    static void SkyEntrance(WorldContext c, Dictionary<Material, MB> mb, Fc f, Material trim, WorldRng rng)
    {
        if (!Reserve(c, f, 0f, 5f, 2.6f, 3.6f)) return;
        Material iron = Flat("iron", new Color(0.12f, 0.12f, 0.14f), 0.5f);
        Bx(mb, iron, f, 0f, 3.5f, 1.3f, 5f, 0.2f, 2.6f);
        Bx(mb, trim, f, -2.3f, 1.75f, 2.3f, 0.15f, 3.5f, 0.15f);
        Bx(mb, trim, f, 2.3f, 1.75f, 2.3f, 0.15f, 3.5f, 0.15f);
        MB b = Get(mb, StepMat());
        b.collide = true;
        for (int j = 0; j < 2; j++)
        {
            float hs = (2 - j) * 0.14f;
            b.Box(f.P(0f, hs * 0.5f, 0.4f + (j + 0.5f) * 0.45f), f.r, Vector3.up, f.n, 4.6f, hs, 0.45f, 1f);
        }
    }

    static void Flush(Dictionary<Material, MB> mb, Transform root, string name)
    {
        foreach (KeyValuePair<Material, MB> p in mb)
        {
            MB b = p.Value;
            if (b.v.Count == 0) continue;
            Mesh m = new Mesh();
            m.name = "BD_" + p.Key.name;
            if (b.v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(b.v);
            m.SetNormals(b.n);
            m.SetUVs(0, b.uv);
            m.SetTriangles(b.t, 0);
            m.RecalculateBounds();
            GameObject go = new GameObject("Details_" + p.Key.name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = p.Key;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bool glass = p.Key.name.StartsWith("BD_pane") || p.Key.name.StartsWith("BD_curtain") || p.Key.name.StartsWith("BD_store") || p.Key.name.StartsWith("BD_door") || p.Key.name.StartsWith("BD_marquee");
            if (b.collide || !glass) go.AddComponent<MeshCollider>().sharedMesh = m;
        }
    }
}
