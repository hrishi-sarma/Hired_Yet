using System;
using System.Collections.Generic;
using UnityEngine;

namespace Items
{
    public static class ItemMeshes
    {
        public const float PaperW = 0.21f, PaperH = 0.297f;
        const int MaxCache = 120;
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
        static Material template;

        class MB
        {
            public List<Vector3> v = new List<Vector3>();
            public List<Vector2> uv = new List<Vector2>();
            public List<int>[] t = { new List<int>(), new List<int>(), new List<int>() };

            public int Add(Vector3 p, Vector2 u) { v.Add(p); uv.Add(u); return v.Count - 1; }

            public void Tri(int s, int a, int b, int c) { t[s].Add(a); t[s].Add(b); t[s].Add(c); }

            public void TriOut(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Vector2 ua, Vector2 ub, Vector2 uc)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { var tp = b; b = c; c = tp; var tu = ub; ub = uc; uc = tu; }
                Tri(s, Add(a, ua), Add(b, ub), Add(c, uc));
            }

            public void QuadOut(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                Vector2 z = Vector2.zero;
                TriOut(s, a, b, c, outward, z, z, z);
                TriOut(s, a, c, d, outward, z, z, z);
            }

            public void Box(int s, Vector3 c, Vector3 size, Quaternion rot)
            {
                Vector3 h = size * 0.5f;
                for (int ax = 0; ax < 3; ax++)
                    for (int sg = -1; sg <= 1; sg += 2)
                    {
                        Vector3 n = Vector3.zero; n[ax] = sg;
                        Vector3 u = Vector3.zero; u[(ax + 1) % 3] = 1;
                        Vector3 w = Vector3.zero; w[(ax + 2) % 3] = 1;
                        Vector3 ctr = Vector3.Scale(n, h);
                        Vector3 p0 = ctr - Vector3.Scale(u, h) - Vector3.Scale(w, h);
                        Vector3 p1 = ctr + Vector3.Scale(u, h) - Vector3.Scale(w, h);
                        Vector3 p2 = ctr + Vector3.Scale(u, h) + Vector3.Scale(w, h);
                        Vector3 p3 = ctr - Vector3.Scale(u, h) + Vector3.Scale(w, h);
                        Vector3 on = rot * n;
                        QuadOut(s, c + rot * p0, c + rot * p1, c + rot * p2, c + rot * p3, on);
                    }
            }

            public void Disc(int s, Vector3 c, float r, int seg, Vector3 normal)
            {
                Vector3 up = normal.normalized;
                Vector3 tan = Vector3.Cross(up, Mathf.Abs(up.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
                Vector3 bit = Vector3.Cross(up, tan);
                int ci = Add(c, new Vector2(0.5f, 0.5f));
                int first = -1, prev = -1;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    int vi = Add(c + (tan * Mathf.Cos(a) + bit * Mathf.Sin(a)) * r, new Vector2(0.5f, 0.5f));
                    if (prev >= 0) { Vector3 pa = v[ci], pb = v[prev], pc = v[vi]; if (Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), up) >= 0f) Tri(s, ci, prev, vi); else Tri(s, ci, vi, prev); }
                    if (first < 0) first = vi; prev = vi;
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                float miny = float.MaxValue, maxy = float.MinValue;
                foreach (var p in v) { if (p.y < miny) miny = p.y; if (p.y > maxy) maxy = p.y; }
                float cy = (miny + maxy) * 0.5f;
                var arr = v.ToArray();
                for (int i = 0; i < arr.Length; i++) arr[i].y -= cy;
                m.vertices = arr; m.uv = uv.ToArray(); m.subMeshCount = 3;
                for (int s = 0; s < 3; s++) m.SetTriangles(t[s], s);
                m.RecalculateNormals(); m.RecalculateBounds();
                return m;
            }
        }

        public static string Key(ItemInstance i) { return i.def.id + ":" + i.seed + ":" + (int)i.condition; }

        public static Mesh Build(ItemInstance i)
        {
            string k = Key(i); Mesh m;
            if (cache.TryGetValue(k, out m) && m != null) return m;
            if (cache.Count >= MaxCache) ClearCache();
            var b = new MB();
            switch (i.def.form)
            {
                case ItemForm.Paper:
                    if (i.def.id == 17) ReportStack(b, i);
                    else Sheet(b, PaperW, PaperH, 0.0008f, 12, 16, i.condition == Condition.Crumpled, i.seed);
                    break;
                case ItemForm.SmallPaper: Sheet(b, 0.09f, 0.225f, 0.0006f, 6, 14, false, i.seed); break;
                case ItemForm.Card: Card(b, i); break;
                case ItemForm.Booklet: Booklet(b); break;
                case ItemForm.Envelope: Envelope(b); break;
                default: if (i.def.id == 19) Guitar(b); else GunProp(b); break;
            }
            m = b.ToMesh("Item_" + k); cache[k] = m;
            return m;
        }

        public static void ClearCache()
        {
            foreach (var m in cache.Values) if (m != null) WorldUtil.Kill(m);
            cache.Clear();
        }

        static Vector3 Crumple(float x, float z, float w, float h, int seed)
        {
            var r = new System.Random(seed * 7919 + 13);
            float ox = (float)r.NextDouble() * 50f, oz = (float)r.NextDouble() * 50f;
            float nx = x / w + 0.5f, nz = z / h + 0.5f;
            float y = (Mathf.PerlinNoise(ox + nx * 2.2f, oz + nz * 2.2f) - 0.5f) * 0.05f
                    + (Mathf.PerlinNoise(ox + 31f + nx * 5.5f, oz + 17f + nz * 5.5f) - 0.5f) * 0.022f;
            for (int c = 0; c < 4; c++)
            {
                float px = (float)r.NextDouble() - 0.5f, pz = (float)r.NextDouble() - 0.5f;
                float ang = (float)r.NextDouble() * Mathf.PI, amp = ((float)r.NextDouble() - 0.5f) * 0.03f;
                float d = ((nx - 0.5f - px) * Mathf.Sin(ang) + (nz - 0.5f - pz) * Mathf.Cos(ang));
                y += amp * Mathf.Exp(-d * d * 220f);
            }
            float dx = (Mathf.PerlinNoise(ox + 71f + nx * 3f, oz + nz * 3f) - 0.5f) * 0.02f;
            float dz = (Mathf.PerlinNoise(ox + nx * 3f, oz + 91f + nz * 3f) - 0.5f) * 0.02f;
            return new Vector3(x * 0.85f + dx, y, z * 0.85f + dz);
        }

        static Vector3 Bend(float x, float z, float h, int seed)
        {
            float t = z / h * 2f, s = (seed & 1) == 0 ? 1f : -1f;
            return new Vector3(x, s * 0.012f * t * t, z);
        }

        static void Sheet(MB m, float w, float h, float th, int nx, int nz, bool crumpled, int seed)
        {
            int cols = nx + 1, rows = nz + 1;
            var front = new int[cols * rows]; var back = new int[cols * rows];
            var fp = new Vector3[cols * rows]; var bp = new Vector3[cols * rows];
            float thick = crumpled ? 0.0015f : th;
            for (int zi = 0; zi < rows; zi++)
                for (int xi = 0; xi < cols; xi++)
                {
                    float u = xi / (float)nx, v = zi / (float)nz;
                    float x = (u - 0.5f) * w, z = (v - 0.5f) * h;
                    Vector3 p = crumpled ? Crumple(x, z, w, h, seed) : Bend(x, z, h, seed);
                    int id = zi * cols + xi;
                    fp[id] = p; bp[id] = p + new Vector3(0, -thick, 0);
                    front[id] = m.Add(p, new Vector2(u, v));
                    back[id] = m.Add(bp[id], new Vector2(u, v));
                }
            for (int zi = 0; zi < nz; zi++)
                for (int xi = 0; xi < nx; xi++)
                {
                    int a = zi * cols + xi, b = zi * cols + xi + 1, c = (zi + 1) * cols + xi + 1, d = (zi + 1) * cols + xi;
                    m.Tri(0, front[a], front[d], front[c]); m.Tri(0, front[a], front[c], front[b]);
                    m.Tri(1, back[a], back[c], back[d]); m.Tri(1, back[a], back[b], back[c]);
                }
            for (int xi = 0; xi < nx; xi++)
            {
                Edge(m, fp[xi], fp[xi + 1], bp[xi], bp[xi + 1], Vector3.back);
                int o = nz * cols + xi;
                Edge(m, fp[o], fp[o + 1], bp[o], bp[o + 1], Vector3.forward);
            }
            for (int zi = 0; zi < nz; zi++)
            {
                int l = zi * cols, l2 = (zi + 1) * cols;
                Edge(m, fp[l], fp[l2], bp[l], bp[l2], Vector3.left);
                Edge(m, fp[l + nx], fp[l2 + nx], bp[l + nx], bp[l2 + nx], Vector3.right);
            }
        }

        static void Edge(MB m, Vector3 f0, Vector3 f1, Vector3 b0, Vector3 b1, Vector3 outward)
        {
            m.QuadOut(1, f0, f1, b1, b0, outward);
        }

        static void ReportStack(MB m, ItemInstance i)
        {
            const float w = PaperW, h = PaperH, stack = 0.006f;
            Sheet(m, w, h, 0.0008f, 12, 16, false, i.seed);
            m.Box(1, new Vector3(0, -0.0008f - stack * 0.5f, 0), new Vector3(w * 0.998f, stack, h * 0.998f), Quaternion.identity);
            m.Box(2, new Vector3(-w * 0.5f + 0.012f, 0.0008f, h * 0.5f - 0.02f), new Vector3(0.022f, 0.0016f, 0.004f), Quaternion.Euler(0, -25, 0));
            m.Box(2, new Vector3(-w * 0.5f + 0.003f, -0.0008f - stack * 0.5f, 0), new Vector3(0.006f, stack + 0.0016f, h), Quaternion.identity);
        }

        static void Card(MB m, ItemInstance i)
        {
            const float w = 0.0854f, h = 0.0540f, th = 0.001f, r = 0.005f;
            int segs = 5;
            var pts = new List<Vector2>();
            Vector2[] cs = { new Vector2(w / 2 - r, h / 2 - r), new Vector2(-w / 2 + r, h / 2 - r), new Vector2(-w / 2 + r, -h / 2 + r), new Vector2(w / 2 - r, -h / 2 + r) };
            for (int c = 0; c < 4; c++)
                for (int s = 0; s <= segs; s++)
                {
                    float a = (c * 90f + s * 90f / segs) * Mathf.Deg2Rad;
                    pts.Add(cs[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            int n = pts.Count;
            int fc = m.Add(Vector3.zero, new Vector2(0.5f, 0.5f)), bc = m.Add(new Vector3(0, -th, 0), new Vector2(0.5f, 0.5f));
            var fi = new int[n]; var bi = new int[n];
            for (int k = 0; k < n; k++)
            {
                var uv = new Vector2(pts[k].x / w + 0.5f, pts[k].y / h + 0.5f);
                fi[k] = m.Add(new Vector3(pts[k].x, 0, pts[k].y), uv);
                bi[k] = m.Add(new Vector3(pts[k].x, -th, pts[k].y), uv);
            }
            for (int k = 0; k < n; k++)
            {
                int k2 = (k + 1) % n;
                m.Tri(0, fc, fi[k2], fi[k]); m.Tri(1, bc, bi[k], bi[k2]);
                Vector3 mid = new Vector3((pts[k].x + pts[k2].x) * 0.5f, 0, (pts[k].y + pts[k2].y) * 0.5f);
                m.QuadOut(1, m.v[fi[k]], m.v[fi[k2]], m.v[bi[k2]], m.v[bi[k]], mid);
            }
            int id = i.def.id;
            if (id == 14 || id == 15)
                m.Box(2, new Vector3(0, -th * 0.5f, h * 0.5f + 0.012f), new Vector3(0.016f, th * 1.6f, 0.026f), Quaternion.identity);
        }

        static void Booklet(MB m)
        {
            const float w = 0.15f, h = 0.1875f, c = 0.0015f, p = 0.0065f;
            m.Box(2, new Vector3(0, c * 0.5f, 0), new Vector3(w, c, h), Quaternion.identity);
            m.Box(1, new Vector3(0.002f, c + p * 0.5f, 0), new Vector3(w - 0.006f, p, h - 0.006f), Quaternion.identity);
            float ty = c + p + c * 0.5f;
            m.Box(2, new Vector3(0, ty, 0), new Vector3(w, c, h), Quaternion.identity);
            m.Box(2, new Vector3(-w * 0.5f + 0.0015f, (c + p + c) * 0.5f, 0), new Vector3(0.003f, c + p + c, h), Quaternion.identity);
            float y = ty + c * 0.5f + 0.0001f;
            int a = m.Add(new Vector3(-w * 0.5f, y, -h * 0.5f), new Vector2(0, 0));
            int b = m.Add(new Vector3(-w * 0.5f, y, h * 0.5f), new Vector2(0, 1));
            int cc = m.Add(new Vector3(w * 0.5f, y, h * 0.5f), new Vector2(1, 1));
            int d = m.Add(new Vector3(w * 0.5f, y, -h * 0.5f), new Vector2(1, 0));
            m.Tri(0, a, b, cc); m.Tri(0, a, cc, d);
        }

        static void Envelope(MB m)
        {
            const float w = 0.22f, h = 0.1547f, th = 0.003f;
            Sheet(m, w, h, th, 4, 4, false, 2);
            float vTop = 0.972f, vTip = 0.43f;
            Vector3 l = new Vector3((0.02f - 0.5f) * w, 0.0004f, (vTop - 0.5f) * h);
            Vector3 r = new Vector3((0.98f - 0.5f) * w, 0.0004f, (vTop - 0.5f) * h);
            Vector3 tip = new Vector3(0, 0.0045f, (vTip - 0.5f) * h);
            int ia = m.Add(l, new Vector2(0.02f, vTop)), ib = m.Add(r, new Vector2(0.98f, vTop)), ic = m.Add(tip, new Vector2(0.5f, vTip));
            m.Tri(0, ia, ib, ic);
            m.TriOut(1, l, tip, r, Vector3.down, Vector2.zero, Vector2.zero, Vector2.zero);
            m.Disc(2, tip + new Vector3(0, 0.0015f, 0), 0.0146f, 16, Vector3.up);
        }

        static float GuitarWidth(float t)
        {
            float[] ts = { 0f, 0.04f, 0.22f, 0.45f, 0.62f, 0.82f, 0.96f, 1f };
            float[] ws = { 0f, 0.18f, 0.34f, 0.26f, 0.24f, 0.29f, 0.17f, 0f };
            for (int k = 0; k < ts.Length - 1; k++)
                if (t <= ts[k + 1]) return Mathf.Lerp(ws[k], ws[k + 1], Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ts[k], ts[k + 1], t)));
            return 0f;
        }

        static void Guitar(MB m)
        {
            const float len = 0.46f, depth = 0.05f; const int st = 28, sg = 18;
            var idx = new int[st + 1, sg];
            for (int s = 0; s <= st; s++)
            {
                float t = s / (float)st, wd = GuitarWidth(t) * 0.5f, z = (t - 0.5f) * len;
                for (int k = 0; k < sg; k++)
                {
                    float a = k * Mathf.PI * 2f / sg;
                    idx[s, k] = m.Add(new Vector3(Mathf.Cos(a) * wd, Mathf.Sin(a) * depth * Mathf.Sqrt(Mathf.Min(1f, wd / 0.05f)), z), new Vector2(0.5f, 0.5f));
                }
            }
            for (int s = 0; s < st; s++)
                for (int k = 0; k < sg; k++)
                {
                    int k2 = (k + 1) % sg;
                    m.Tri(0, idx[s, k], idx[s + 1, k2], idx[s + 1, k]); m.Tri(0, idx[s, k], idx[s, k2], idx[s + 1, k2]);
                }
            m.Disc(2, new Vector3(0, depth * 0.98f, -0.02f), 0.05f, 20, Vector3.up);
            m.Box(1, new Vector3(0, 0.012f, len * 0.5f + 0.2f), new Vector3(0.05f, 0.028f, 0.44f), Quaternion.identity);
            m.Box(2, new Vector3(0, 0.03f, len * 0.5f + 0.2f), new Vector3(0.04f, 0.004f, 0.4f), Quaternion.identity);
            m.Box(1, new Vector3(0, 0.012f, len * 0.5f + 0.46f), new Vector3(0.075f, 0.026f, 0.1f), Quaternion.Euler(-8, 0, 0));
            m.Box(2, new Vector3(0, depth * 0.98f, -len * 0.5f + 0.07f), new Vector3(0.1f, 0.006f, 0.014f), Quaternion.identity);
        }

        static void GunProp(MB m)
        {
            m.Box(0, new Vector3(0, 0.03f, 0.06f), new Vector3(0.04f, 0.05f, 0.2f), Quaternion.identity);
            m.Box(0, new Vector3(0, 0.0f, -0.06f), new Vector3(0.036f, 0.1f, 0.055f), Quaternion.Euler(-18, 0, 0));
            m.Box(1, new Vector3(0, -0.012f, 0.0f), new Vector3(0.012f, 0.03f, 0.05f), Quaternion.identity);
            m.Box(1, new Vector3(0, 0.058f, 0.05f), new Vector3(0.018f, 0.014f, 0.16f), Quaternion.identity);
            m.Box(2, new Vector3(0, 0.03f, 0.175f), new Vector3(0.043f, 0.053f, 0.03f), Quaternion.identity);
        }

        static Material Template()
        {
            if (template != null) return template;
            template = new Material(WorldUtil.DefaultLit());
            return template;
        }

        public static Color BodyColor(ItemInstance i)
        {
            if (i.def.form == ItemForm.Object) return i.def.id == 19 ? ItemPal.Peach : ItemPal.Mint;
            return new Color(0.96f, 0.94f, 0.88f);
        }

        public static Color AccentColor(ItemInstance i)
        {
            switch (i.def.id)
            {
                case 17: return new Color(0.45f, 0.42f, 0.52f);
                case 19: return new Color(0.35f, 0.26f, 0.3f);
                case 20: return new Color(1f, 0.55f, 0.15f);
                default: return ItemPal.Accent(i.seed, 9);
            }
        }

        static Color Sub1Color(ItemInstance i)
        {
            if (i.def.id == 19) return new Color(0.78f, 0.58f, 0.42f);
            if (i.def.id == 20) return new Color(0.55f, 0.52f, 0.62f);
            return BodyColor(i);
        }

        public static Material[] CreateMaterials(ItemInstance i)
        {
            var tex = new Material(Template()); tex.mainTexture = ItemTextures.Get(i); tex.color = Color.white;
            var body = new Material(Template()); body.color = Sub1Color(i);
            var acc = new Material(Template()); acc.color = AccentColor(i);
            return new[] { tex, body, acc };
        }

        public static GameObject CreateObject(ItemInstance i, string name = null)
        {
            var go = new GameObject(name ?? ("Item_" + i.def.id));
            go.AddComponent<MeshFilter>().sharedMesh = Build(i);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = CreateMaterials(i);
            return go;
        }

        public static void RefreshTexture(GameObject go, ItemInstance i)
        {
            var r = go.GetComponent<MeshRenderer>();
            if (r != null && r.sharedMaterials.Length > 0 && r.sharedMaterials[0] != null) r.sharedMaterials[0].mainTexture = ItemTextures.Rebuild(i);
        }
    }
}
