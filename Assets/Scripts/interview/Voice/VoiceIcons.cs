using UnityEngine;

public static class VoiceIcons
{
    public enum Kind { Mic, Pause, Stop, Redo, Check }
    static readonly Sprite[] cache = new Sprite[5];
    static readonly Color InkCol = new Color(0.15f, 0.12f, 0.2f, 1f);
    static readonly Color GlyphCol = new Color(0.04f, 0.08f, 0.3f, 1f);

    public static Sprite Get(Kind k)
    {
        int i = (int)k;
        if (cache[i] == null) cache[i] = Bake(k, 160);
        return cache[i];
    }

    static Color FaceColor(Kind k)
    {
        switch (k)
        {
            case Kind.Mic: return new Color(1f, 0.54f, 0.5f, 1f);
            case Kind.Pause: return new Color(1f, 0.88f, 0.4f, 1f);
            case Kind.Stop: return new Color(1f, 0.62f, 0.26f, 1f);
            case Kind.Redo: return new Color(1f, 1f, 0.95f, 1f);
            default: return new Color(0.49f, 0.91f, 0.48f, 1f);
        }
    }

    static float Circle(float x, float y, float cx, float cy, float r)
    {
        float dx = x - cx, dy = y - cy;
        return Mathf.Sqrt(dx * dx + dy * dy) - r;
    }

    static float Cap(float x, float y, float ax, float ay, float bx, float by, float r)
    {
        float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
        float h = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy + 1e-6f));
        float ex = px - dx * h, ey = py - dy * h;
        return Mathf.Sqrt(ex * ex + ey * ey) - r;
    }

    static float RBox(float x, float y, float cx, float cy, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(x - cx) - hx + r, qy = Mathf.Abs(y - cy) - hy + r;
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static float Tri(float x, float y, Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 p = new Vector2(x, y);
        float d = Mathf.Min(Seg(p, a, b), Mathf.Min(Seg(p, b, c), Seg(p, c, a)));
        float s1 = Cross(a, b, p), s2 = Cross(b, c, p), s3 = Cross(c, a, p);
        bool inside = (s1 >= 0f && s2 >= 0f && s3 >= 0f) || (s1 <= 0f && s2 <= 0f && s3 <= 0f);
        return inside ? -d : d;
    }

    static float Cross(Vector2 a, Vector2 b, Vector2 p) { return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x); }

    static float Seg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a, ap = p - a;
        float h = Mathf.Clamp01(Vector2.Dot(ap, ab) / (Vector2.Dot(ab, ab) + 1e-6f));
        return (ap - ab * h).magnitude;
    }

    static float Glyph(Kind k, float x, float y)
    {
        switch (k)
        {
            case Kind.Mic:
            {
                float body = Cap(x, y, 0f, 0.04f, 0f, 0.26f, 0.15f);
                float rr = Mathf.Sqrt(x * x + (y - 0.04f) * (y - 0.04f));
                float cradle = y < 0.04f ? Mathf.Abs(rr - 0.27f) - 0.045f : 1f;
                float stem = Cap(x, y, 0f, -0.23f, 0f, -0.33f, 0.04f);
                float baseL = Cap(x, y, -0.15f, -0.36f, 0.15f, -0.36f, 0.045f);
                return Mathf.Min(Mathf.Min(body, cradle), Mathf.Min(stem, baseL));
            }
            case Kind.Pause:
                return Mathf.Min(RBox(x, y, -0.16f, 0f, 0.08f, 0.27f, 0.035f), RBox(x, y, 0.16f, 0f, 0.08f, 0.27f, 0.035f));
            case Kind.Stop:
                return RBox(x, y, 0f, 0f, 0.27f, 0.27f, 0.06f);
            case Kind.Check:
                return Mathf.Min(Cap(x, y, -0.3f, 0.0f, -0.09f, -0.21f, 0.075f), Cap(x, y, -0.09f, -0.21f, 0.31f, 0.24f, 0.075f));
            default:
            {
                float ang = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
                float rr = Mathf.Sqrt(x * x + y * y);
                float ring = (ang > 62f || ang < -25f) ? Mathf.Abs(rr - 0.29f) - 0.065f : 1f;
                float capEnd = Circle(x, y, 0.29f * Mathf.Cos(-25f * Mathf.Deg2Rad), 0.29f * Mathf.Sin(-25f * Mathf.Deg2Rad), 0.065f);
                float a = 62f * Mathf.Deg2Rad;
                Vector2 p = new Vector2(0.29f * Mathf.Cos(a), 0.29f * Mathf.Sin(a));
                Vector2 t = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                Vector2 n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float head = Tri(x, y, p + t * 0.19f, p + n * 0.17f, p - n * 0.17f);
                return Mathf.Min(ring, Mathf.Min(capEnd, head));
            }
        }
    }

    static Color Over(Color dst, Color src, float cov)
    {
        float a = src.a * cov;
        float oa = a + dst.a * (1f - a);
        if (oa <= 0f) return new Color(0f, 0f, 0f, 0f);
        Color o = (src * a + dst * dst.a * (1f - a)) / oa;
        o.a = oa;
        return o;
    }

    static Sprite Bake(Kind k, int n)
    {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[n * n];
        float aa = 2.2f / n * 1.3f;
        Color face = FaceColor(k);
        Color shadeCol = new Color(0f, 0f, 0.1f, 0.2f);
        Color shadow = new Color(0f, 0f, 0f, 0.5f);
        Color white = new Color(1f, 1f, 1f, 0.9f);
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = ((i + 0.5f) / n * 2f - 1f) * 1.1f, y = ((j + 0.5f) / n * 2f - 1f) * 1.1f;
                Color c = new Color(0f, 0f, 0f, 0f);
                c = Over(c, shadow, Cov(Circle(x, y, 0.07f, -0.09f, 0.82f), aa));
                c = Over(c, InkCol, Cov(Circle(x, y, 0f, 0f, 0.82f), aa));
                float fd = Circle(x, y, 0f, 0f, 0.72f);
                c = Over(c, face, Cov(fd, aa));
                float shade = Mathf.Max(fd, -Circle(x, y, -0.1f, 0.12f, 0.72f));
                c = Over(c, shadeCol, Cov(shade, aa));
                c = Over(c, white, Cov(Cap(x, y, -0.45f, 0.36f, -0.31f, 0.54f, 0.055f), aa));
                c = Over(c, white, Cov(Circle(x, y, -0.55f, 0.22f, 0.03f), aa));
                c = Over(c, GlyphCol, Cov(Glyph(k, x, y), aa));
                px[j * n + i] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(c.a * 255f));
            }
        t.SetPixels32(px);
        t.Apply(true);
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static float Cov(float d, float aa) { return Mathf.Clamp01(0.5f - d / aa); }
}
