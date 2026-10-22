using System.Collections.Generic;
using UnityEngine;

namespace Items
{
    // Procedural acoustic guitar at real size (about 1.2 m). Local axes: +Z toward the headstock, +Y = soundboard normal.
    // Root holds the static mesh (6 submeshes). Six string objects and anchor empties are children (used by Stage 3).
    public static class GuitarModel
    {
        const int WoodTop = 0, WoodSide = 1, NeckM = 2, Black = 3, Metal = 4, Cream = 5, MatCount = 6;
        public const float BodyHalf = 0.25f, NutZ = 0.53f, BridgeZ = -0.12f;
        static readonly float[] kt = { 0, .03f, .10f, .25f, .40f, .55f, .68f, .82f, .93f, .96f, 1f };
        static readonly float[] kw = { 0, .22f, .33f, .385f, .36f, .27f, .285f, .30f, .25f, .12f, .06f };
        static Mesh body, strMesh;
        static Material template;

        public static float HalfW(float z)
        {
            float t = Mathf.Clamp01((z + BodyHalf) / (2f * BodyHalf));
            for (int i = 0; i < kt.Length - 1; i++)
                if (t <= kt[i + 1]) return 0.5f * Mathf.Lerp(kw[i], kw[i + 1], Mathf.SmoothStep(0, 1, Mathf.InverseLerp(kt[i], kt[i + 1], t)));
            return 0f;
        }
        public static float TopY(float z) { return 0.049f - 0.009f * ((z + BodyHalf) / (2f * BodyHalf)); }
        static float FretboardY(float z) { return 0.0495f + (NutZ - z) / 0.40f * 0.0027f; }
        static float SaddleY { get { return TopY(BridgeZ) + 0.0118f; } }
        const float NutY = 0.0525f;

        class GB
        {
            public List<Vector3> v = new List<Vector3>();
            public List<int>[] t = new List<int>[MatCount];
            public GB() { for (int i = 0; i < MatCount; i++) t[i] = new List<int>(); }
            public int Add(Vector3 p) { v.Add(p); return v.Count - 1; }
            public void Tri(int s, int a, int b, int c, Vector3 outward)
            {
                Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                if (Vector3.Dot(n, outward) < 0f) { int x = b; b = c; c = x; }
                t[s].Add(a); t[s].Add(b); t[s].Add(c);
            }
            public void QuadI(int s, int a, int b, int c, int d, Vector3 outward) { Tri(s, a, b, c, outward); Tri(s, a, c, d, outward); }
            public void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward) { QuadI(s, Add(a), Add(b), Add(c), Add(d), outward); }

            // convex hexahedron: p[0..3] bottom loop, p[4..7] top loop (same order)
            public void Solid(int s, Vector3[] p)
            {
                Vector3 c = Vector3.zero; for (int i = 0; i < 8; i++) c += p[i]; c /= 8f;
                int[][] f = { new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 } };
                foreach (var q in f)
                {
                    Vector3 fc = (p[q[0]] + p[q[1]] + p[q[2]] + p[q[3]]) * 0.25f;
                    Quad(s, p[q[0]], p[q[1]], p[q[2]], p[q[3]], fc - c);
                }
            }
            public void Box(int s, Vector3 c, Vector3 size, Quaternion rot)
            {
                Vector3 h = size * 0.5f; var p = new Vector3[8];
                float[] ys = { -1, 1 };
                for (int k = 0; k < 2; k++)
                {
                    p[k * 4 + 0] = c + rot * new Vector3(-h.x, ys[k] * h.y, -h.z);
                    p[k * 4 + 1] = c + rot * new Vector3(h.x, ys[k] * h.y, -h.z);
                    p[k * 4 + 2] = c + rot * new Vector3(h.x, ys[k] * h.y, h.z);
                    p[k * 4 + 3] = c + rot * new Vector3(-h.x, ys[k] * h.y, h.z);
                }
                Solid(s, p);
            }
            public void Frustum(int s, Matrix4x4 m, float z0, float w0, float yt0, float th0, float z1, float w1, float yt1, float th1)
            {
                var p = new Vector3[8];
                p[0] = new Vector3(-w0 / 2, yt0 - th0, z0); p[1] = new Vector3(w0 / 2, yt0 - th0, z0);
                p[2] = new Vector3(w1 / 2, yt1 - th1, z1); p[3] = new Vector3(-w1 / 2, yt1 - th1, z1);
                p[4] = new Vector3(-w0 / 2, yt0, z0); p[5] = new Vector3(w0 / 2, yt0, z0);
                p[6] = new Vector3(w1 / 2, yt1, z1); p[7] = new Vector3(-w1 / 2, yt1, z1);
                for (int i = 0; i < 8; i++) p[i] = m.MultiplyPoint3x4(p[i]);
                Solid(s, p);
            }
            public void Ring(int s, float cx, float cz, float r0, float r1, int seg, float lift)
            {
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg, b = (i + 1) * Mathf.PI * 2f / seg;
                    Vector3 p0 = P(cx, cz, r0, a, lift), p1 = P(cx, cz, r1, a, lift), p2 = P(cx, cz, r1, b, lift), p3 = P(cx, cz, r0, b, lift);
                    Quad(s, p0, p1, p2, p3, Vector3.up);
                }
            }
            static Vector3 P(float cx, float cz, float r, float a, float lift)
            { float x = cx + Mathf.Cos(a) * r, z = cz + Mathf.Sin(a) * r; return new Vector3(x, TopY(z) + lift, z); }
            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v); m.subMeshCount = MatCount;
                for (int i = 0; i < MatCount; i++) m.SetTriangles(t[i], i);
                m.RecalculateNormals(); m.RecalculateBounds();
                return m;
            }
        }

        static void BuildBody()
        {
            var b = new GB();
            const int M = 48;
            var zs = new float[M + 1]; var hw = new float[M + 1];
            for (int i = 0; i <= M; i++) { zs[i] = -BodyHalf + 2f * BodyHalf * (0.5f - 0.5f * Mathf.Cos(Mathf.PI * i / M)); hw[i] = HalfW(zs[i]); }

            // soundboard (top), back, and side wall
            var tl = new int[M + 1]; var tr = new int[M + 1]; var bl = new int[M + 1]; var br = new int[M + 1];
            for (int i = 0; i <= M; i++)
            {
                float ty = TopY(zs[i]);
                tl[i] = b.Add(new Vector3(-hw[i], ty, zs[i])); tr[i] = b.Add(new Vector3(hw[i], ty, zs[i]));
                bl[i] = b.Add(new Vector3(-hw[i], -ty, zs[i])); br[i] = b.Add(new Vector3(hw[i], -ty, zs[i]));
            }
            for (int i = 0; i < M; i++)
            {
                b.QuadI(WoodTop, tl[i], tr[i], tr[i + 1], tl[i + 1], Vector3.up);
                b.QuadI(WoodSide, bl[i], br[i], br[i + 1], bl[i + 1], Vector3.down);
            }
            // ring: right side tail->top, then left side top->tail (own vertices, so the wall shades smoothly)
            var ring = new List<Vector2>();
            for (int i = 0; i <= M; i++) ring.Add(new Vector2(hw[i], zs[i]));
            for (int i = M; i >= 0; i--) ring.Add(new Vector2(-hw[i], zs[i]));
            int n = ring.Count; var rt = new int[n]; var rb = new int[n];
            for (int r = 0; r < n; r++)
            {
                float ty = TopY(ring[r].y);
                rt[r] = b.Add(new Vector3(ring[r].x, ty, ring[r].y)); rb[r] = b.Add(new Vector3(ring[r].x, -ty, ring[r].y));
            }
            for (int r = 0; r < n; r++)
            {
                int q = (r + 1) % n;
                if ((ring[r] - ring[q]).sqrMagnitude < 1e-8f) continue;
                Vector2 mid = (ring[r] + ring[q]) * 0.5f;
                b.QuadI(WoodSide, rt[r], rt[q], rb[q], rb[r], new Vector3(mid.x, 0f, mid.y));
            }

            // soundhole + rosette
            const float hz = 0.045f;
            b.Ring(Black, 0f, hz, 0f, 0.045f, 28, 0.0006f);
            b.Ring(Cream, 0f, hz, 0.045f, 0.052f, 28, 0.0008f);

            // bridge, saddle, pins
            b.Box(NeckM, new Vector3(0, TopY(BridgeZ) + 0.006f, BridgeZ - 0.002f), new Vector3(0.15f, 0.012f, 0.03f), Quaternion.identity);
            b.Box(Cream, new Vector3(0, SaddleY - 0.0015f, BridgeZ), new Vector3(0.068f, 0.003f, 0.004f), Quaternion.identity);
            for (int k = 0; k < 6; k++)
                b.Box(Black, new Vector3((k - 2.5f) * 0.0105f, TopY(BridgeZ) + 0.0125f, BridgeZ - 0.012f), new Vector3(0.005f, 0.003f, 0.005f), Quaternion.identity);

            // neck, fretboard, nut, frets, dots
            float zEnd = 0.13f;
            b.Frustum(NeckM, Matrix4x4.identity, zEnd, 0.052f, FretboardY(zEnd) - 0.006f, 0.024f, NutZ, 0.040f, FretboardY(NutZ) - 0.006f, 0.022f);
            b.Frustum(Black, Matrix4x4.identity, zEnd, 0.058f, FretboardY(zEnd), 0.006f, NutZ, 0.044f, FretboardY(NutZ), 0.006f);
            b.Box(Cream, new Vector3(0, NutY - 0.0025f, NutZ), new Vector3(0.044f, 0.005f, 0.004f), Quaternion.identity);
            for (int f = 1; f <= 15; f++)
            {
                float z = NutZ - 0.65f * (1f - Mathf.Pow(2f, -f / 12f));
                float w = Mathf.Lerp(0.044f, 0.058f, Mathf.InverseLerp(NutZ, zEnd, z)) + 0.002f;
                b.Box(Metal, new Vector3(0, FretboardY(z) + 0.0008f, z), new Vector3(w, 0.0018f, 0.0022f), Quaternion.identity);
            }
            foreach (int f in new[] { 3, 5, 7, 9, 12 })
            {
                float z0 = NutZ - 0.65f * (1f - Mathf.Pow(2f, -(f - 1) / 12f)), z1 = NutZ - 0.65f * (1f - Mathf.Pow(2f, -f / 12f));
                float z = (z0 + z1) * 0.5f;
                b.Box(Cream, new Vector3(0, FretboardY(z) + 0.0004f, z), new Vector3(0.006f, 0.0009f, 0.006f), Quaternion.identity);
            }

            // headstock tilted back, with tuners and a plate
            var m = Matrix4x4.TRS(new Vector3(0, FretboardY(NutZ), NutZ), Quaternion.Euler(12f, 0f, 0f), Vector3.one);
            b.Frustum(NeckM, m, 0f, 0.046f, 0f, 0.018f, 0.13f, 0.062f, 0f, 0.018f);
            b.Box(Cream, m.MultiplyPoint3x4(new Vector3(0, 0.0004f, 0.07f)), new Vector3(0.03f, 0.0008f, 0.05f), m.rotation);
            for (int i = 0; i < 3; i++)
            {
                float z = 0.03f + 0.037f * i, hwid = Mathf.Lerp(0.046f, 0.062f, z / 0.13f) * 0.5f + 0.007f;
                b.Box(Metal, m.MultiplyPoint3x4(new Vector3(-hwid, -0.009f, z)), new Vector3(0.014f, 0.006f, 0.012f), m.rotation);
                b.Box(Metal, m.MultiplyPoint3x4(new Vector3(hwid, -0.009f, z)), new Vector3(0.014f, 0.006f, 0.012f), m.rotation);
            }
            body = b.ToMesh("Guitar_Body");
        }

        static Material Template()
        {
            if (template != null) return template;
            template = new Material(WorldUtil.DefaultLit());
            return template;
        }

        static Material Mat(Color c) { var m = new Material(Template()); m.color = c; return m; }

        static Material[] Mats()
        {
            var a = new Material[MatCount];
            a[WoodTop] = Mat(new Color(0.86f, 0.64f, 0.42f));
            a[WoodSide] = Mat(new Color(0.66f, 0.43f, 0.28f));
            a[NeckM] = Mat(new Color(0.46f, 0.29f, 0.19f));
            a[Black] = Mat(new Color(0.09f, 0.07f, 0.08f));
            a[Metal] = Mat(new Color(0.80f, 0.80f, 0.84f));
            a[Cream] = Mat(new Color(0.96f, 0.91f, 0.78f));
            return a;
        }

        static Mesh StringMesh()
        {
            if (strMesh != null) return strMesh;
            var b = new GB(); b.Box(Metal, Vector3.zero, Vector3.one, Quaternion.identity);
            strMesh = b.ToMesh("Guitar_String");
            strMesh.subMeshCount = 1; strMesh.SetTriangles(b.t[Metal], 0);
            return strMesh;
        }

        public static Vector3 StringBridgePoint(int k) { return new Vector3((k - 2.5f) * 0.0105f, SaddleY, BridgeZ); }
        public static Vector3 StringNutPoint(int k) { return new Vector3((k - 2.5f) * 0.0072f, NutY, NutZ); }

        public static GameObject Create(ItemInstance item, string name)
        {
            if (body == null) BuildBody();
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = body;
            go.AddComponent<MeshRenderer>().sharedMaterials = Mats();

            var sm = Mat(new Color(0.90f, 0.86f, 0.76f));
            float[] thick = { 0.0030f, 0.0027f, 0.0023f, 0.0019f, 0.0016f, 0.0014f };
            for (int k = 0; k < 6; k++)
            {
                Vector3 a = StringBridgePoint(k), z = StringNutPoint(k);
                var s = new GameObject("String" + (k + 1));
                s.transform.SetParent(go.transform, false);
                s.transform.localPosition = (a + z) * 0.5f;
                s.transform.localRotation = Quaternion.LookRotation(z - a, Vector3.up);
                s.transform.localScale = new Vector3(thick[k], thick[k], (z - a).magnitude);
                s.AddComponent<MeshFilter>().sharedMesh = StringMesh();
                s.AddComponent<MeshRenderer>().sharedMaterial = sm;
            }
            Anchor(go.transform, "Bridge", new Vector3(0, SaddleY, BridgeZ));
            Anchor(go.transform, "Soundhole", new Vector3(0, TopY(0.045f), 0.045f));
            Anchor(go.transform, "Nut", new Vector3(0, NutY, NutZ));
            var d = go.AddComponent<GuitarPoseDriver>();
            d.Init(HoldPoses.Get(item.def));
            go.AddComponent<GuitarPerchBird>();   // Stage 4: one bird sits on the guitar while it is raised
            return go;
        }

        static void Anchor(Transform p, string n, Vector3 local)
        { var t = new GameObject(n).transform; t.SetParent(p, false); t.localPosition = local; }
    }
}
