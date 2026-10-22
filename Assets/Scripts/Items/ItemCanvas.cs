using System.Collections.Generic;
using UnityEngine;

namespace Items
{
    // Tiny CPU painter (top-left origin, anti-aliased shapes, 5x7 bitmap text) used by the procedural item textures.
    public class ItemCanvas
    {
        public readonly int w, h;
        public Color32[] px;

        public ItemCanvas(int w, int h, Color bg)
        {
            this.w = w; this.h = h; px = new Color32[w * h];
            Color32 b = bg; for (int i = 0; i < px.Length; i++) px[i] = b;
        }
        public ItemCanvas(ItemCanvas o) { w = o.w; h = o.h; px = (Color32[])o.px.Clone(); }

        void Put(int x, int y, Color c, float cov)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            float a = cov * c.a; if (a <= 0.001f) return;
            int i = (h - 1 - y) * w + x; Color32 d = px[i];
            float da = d.a / 255f, oa = a + da * (1f - a), k = da * (1f - a);
            px[i] = new Color32(
                (byte)Mathf.Clamp((c.r * 255f * a + d.r * k) / oa, 0, 255),
                (byte)Mathf.Clamp((c.g * 255f * a + d.g * k) / oa, 0, 255),
                (byte)Mathf.Clamp((c.b * 255f * a + d.b * k) / oa, 0, 255),
                (byte)Mathf.Clamp(oa * 255f, 0, 255));
        }

        void Shade(int x, int y, float d, Color fill, Color outline, float ow)
        {
            float o = Mathf.Clamp01(0.5f - d);
            if (ow > 0f)
            {
                float i = Mathf.Clamp01(0.5f - (d + ow));
                Put(x, y, outline, o - i); Put(x, y, fill, i);
            }
            else Put(x, y, fill, o);
        }

        public void Rect(float x, float y, float rw, float rh, Color c) { RRect(x, y, rw, rh, 0f, c, c, 0f); }
        public void RRect(float x, float y, float rw, float rh, float r, Color fill) { RRect(x, y, rw, rh, r, fill, fill, 0f); }
        public void RRect(float x, float y, float rw, float rh, float r, Color fill, Color outline, float ow)
        {
            float cx = x + rw * 0.5f, cy = y + rh * 0.5f, hx = rw * 0.5f - r, hy = rh * 0.5f - r;
            int x0 = Mathf.Max(0, (int)(x - 2)), x1 = Mathf.Min(w - 1, (int)(x + rw + 2));
            int y0 = Mathf.Max(0, (int)(y - 2)), y1 = Mathf.Min(h - 1, (int)(y + rh + 2));
            for (int yy = y0; yy <= y1; yy++)
                for (int xx = x0; xx <= x1; xx++)
                {
                    float qx = Mathf.Abs(xx + 0.5f - cx) - hx, qy = Mathf.Abs(yy + 0.5f - cy) - hy;
                    float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                    Shade(xx, yy, d, fill, outline, ow);
                }
        }

        public void Circle(float cx, float cy, float rad, Color fill) { Circle(cx, cy, rad, fill, fill, 0f); }
        public void Circle(float cx, float cy, float rad, Color fill, Color outline, float ow)
        {
            int x0 = Mathf.Max(0, (int)(cx - rad - 2)), x1 = Mathf.Min(w - 1, (int)(cx + rad + 2));
            int y0 = Mathf.Max(0, (int)(cy - rad - 2)), y1 = Mathf.Min(h - 1, (int)(cy + rad + 2));
            for (int yy = y0; yy <= y1; yy++)
                for (int xx = x0; xx <= x1; xx++)
                {
                    float d = new Vector2(xx + 0.5f - cx, yy + 0.5f - cy).magnitude - rad;
                    Shade(xx, yy, d, fill, outline, ow);
                }
        }

        public void Line(float ax, float ay, float bx, float by, float th, Color c)
        {
            float pad = th * 0.5f + 2f;
            int x0 = Mathf.Max(0, (int)(Mathf.Min(ax, bx) - pad)), x1 = Mathf.Min(w - 1, (int)(Mathf.Max(ax, bx) + pad));
            int y0 = Mathf.Max(0, (int)(Mathf.Min(ay, by) - pad)), y1 = Mathf.Min(h - 1, (int)(Mathf.Max(ay, by) + pad));
            Vector2 a = new Vector2(ax, ay), ab = new Vector2(bx - ax, by - ay); float l2 = ab.sqrMagnitude;
            for (int yy = y0; yy <= y1; yy++)
                for (int xx = x0; xx <= x1; xx++)
                {
                    Vector2 p = new Vector2(xx + 0.5f, yy + 0.5f);
                    float t = l2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0f;
                    float d = (p - (a + ab * t)).magnitude - th * 0.5f;
                    Put(xx, yy, c, Mathf.Clamp01(0.5f - d));
                }
        }

        static float Sign(Vector2 p, Vector2 a, Vector2 b) { return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y); }
        public void Tri(float ax, float ay, float bx, float by, float cx, float cy, Color col)
        {
            Vector2 a = new Vector2(ax, ay), b = new Vector2(bx, by), c = new Vector2(cx, cy);
            int x0 = Mathf.Max(0, (int)Mathf.Min(ax, Mathf.Min(bx, cx))), x1 = Mathf.Min(w - 1, (int)Mathf.Max(ax, Mathf.Max(bx, cx)) + 1);
            int y0 = Mathf.Max(0, (int)Mathf.Min(ay, Mathf.Min(by, cy))), y1 = Mathf.Min(h - 1, (int)Mathf.Max(ay, Mathf.Max(by, cy)) + 1);
            for (int yy = y0; yy <= y1; yy++)
                for (int xx = x0; xx <= x1; xx++)
                {
                    Vector2 p = new Vector2(xx + 0.5f, yy + 0.5f);
                    float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) Put(xx, yy, col, 1f);
                }
        }

        // Stylised body text: rows of rounded "word" dashes.
        public void TextLines(float x, float y, float wid, int n, float lh, float th, Color c, int seed, float lastFrac = 0.6f)
        {
            var r = new System.Random(seed);
            for (int i = 0; i < n; i++)
            {
                float yy = y + i * lh, xx = x, end = x + wid * (i == n - 1 ? lastFrac : 1f);
                while (xx < end - 8f)
                {
                    float wl = Mathf.Min(r.Next(18, 60), end - xx);
                    Line(xx, yy, xx + wl, yy, th, c); xx += wl + th + r.Next(5, 10);
                }
            }
        }

        public void Squiggle(float x, float y, float len, float amp, float th, Color c, int seed)
        {
            var r = new System.Random(seed); int n = Mathf.Max(2, (int)(len / 8f));
            float px0 = x, py0 = y;
            for (int i = 1; i <= n; i++)
            {
                float nx = x + i * len / n, ny = y + Mathf.Sin(i * 1.3f) * amp * (0.6f + (float)r.NextDouble() * 0.4f);
                Line(px0, py0, nx, ny, th, c); px0 = nx; py0 = ny;
            }
        }

        // Clears rows above/below a jagged line (torn paper). Leaves transparent pixels.
        public void TearEdges(int top, int bottom, int seed)
        {
            var r = new System.Random(seed);
            for (int x = 0; x < w; x += 6)
            {
                int ct = top + r.Next(0, 10), cb = bottom + r.Next(0, 10);
                for (int dx = 0; dx < 6 && x + dx < w; dx++)
                {
                    for (int y = 0; y < ct; y++) px[(h - 1 - y) * w + x + dx] = new Color32(0, 0, 0, 0);
                    for (int y = h - cb; y < h; y++) px[(h - 1 - y) * w + x + dx] = new Color32(0, 0, 0, 0);
                }
            }
        }

        // ---- 5x7 bitmap font (bit0 = top row). Upper-case only. ----
        static readonly Dictionary<char, byte[]> font = BuildFont();
        static Dictionary<char, byte[]> BuildFont()
        {
            string[] t =
            {
                "03E5149453E","100427F4000","24261514946","32141454B31","41814127F10","52745454539","63C4A494930","70171090503","83649494936","9064949291E",
                "A7E1111117E","B7F49494936","C3E41414122","D7F4141221C","E7F49494941","F7F09090901","G3E4149497A","H7F0808087F","I00417F4100","J2040413F01",
                "K7F08142241","L7F40404040","M7F020C027F","N7F0408107F","O3E4141413E","P7F09090906","Q3E4151215E","R7F09192946","S4649494931","T01017F0101",
                "U3F4040403F","V1F2040201F","W3F4038403F","X6314081463","Y0708700807","Z6151494543",
                ".0060600000",",0080600000","-0808080808",":0036360000","'0005030000","#147F147F14","+08083E0808","/2010080402","$242A7F2A12"," 0000000000","!00005F0000"
            };
            var d = new Dictionary<char, byte[]>();
            foreach (var s in t)
            {
                var g = new byte[5];
                for (int i = 0; i < 5; i++) g[i] = System.Convert.ToByte(s.Substring(1 + i * 2, 2), 16);
                d[s[0]] = g;
            }
            return d;
        }

        public static float TextWidth(string s, float sc) { return Mathf.Max(0f, s.Length * 6f * sc - sc); }

        public void Text(string s, float x, float y, float sc, Color c, float slant = 0f, float jitter = 0f, int seed = 0)
        {
            var r = new System.Random(seed); float cx = x;
            foreach (char ch0 in s.ToUpperInvariant())
            {
                byte[] g; if (font.TryGetValue(ch0, out g))
                    for (int col = 0; col < 5; col++)
                        for (int row = 0; row < 7; row++)
                            if (((g[col] >> row) & 1) != 0)
                            {
                                float jx = jitter > 0f ? (float)(r.NextDouble() - 0.5) * jitter * sc : 0f;
                                float jy = jitter > 0f ? (float)(r.NextDouble() - 0.5) * jitter * sc : 0f;
                                RRect(cx + col * sc + (6 - row) * slant * sc + jx, y + row * sc + jy, sc, sc, sc * 0.3f, c);
                            }
                cx += 6f * sc;
            }
        }

        public void TextCentered(string s, float cx, float y, float sc, Color c)
        { Text(s, cx - TextWidth(s, sc) * 0.5f, y, sc, c); }
    }
}
