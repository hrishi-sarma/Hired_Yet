using UnityEngine;

public static class DetailTex
{
    static Color[] Blank(int w, int h, Color rgb)
    {
        Color[] p = new Color[w * h];
        Color c = new Color(rgb.r, rgb.g, rgb.b, 0f);
        for (int i = 0; i < p.Length; i++) p[i] = c;
        return p;
    }

    static Texture2D Make(Color[] px, int w, int h, TextureWrapMode wrap)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
        t.wrapMode = wrap;
        t.filterMode = FilterMode.Bilinear;
        t.SetPixels(px);
        t.Apply(true, false);
        return t;
    }

    static void Put(Color[] px, int w, int h, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        int i = y * w + x;
        if (c.a >= px[i].a) px[i] = c;
    }

    static float Sm(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / Mathf.Max(0.0001f, b - a));
        return t * t * (3f - 2f * t);
    }

    public static float Pn(float u, float v, int seed, int mult)
    {
        float sum = 0f, tot = 0f;
        for (int j = 0; j < 8; j++)
        {
            int fx = (1 + (seed * 7 + j * 5) % 5) * mult * (((seed + j) & 1) == 0 ? 1 : -1);
            int fy = (1 + (seed * 3 + j * 11) % 5) * mult;
            float a = 1f / (1f + 0.25f * (Mathf.Abs(fx) + fy));
            sum += a * Mathf.Sin(Mathf.PI * 2f * (fx * u + fy * v) + j * 1.7f + seed);
            tot += a;
        }
        return 0.5f + 0.5f * sum / tot;
    }

    static void Walk(Color[] px, int s, System.Random r, float x, float y, float a, int steps, int depth)
    {
        Color dark = new Color(0.05f, 0.05f, 0.055f, 0.9f);
        Color soft = new Color(0.05f, 0.05f, 0.055f, 0.35f);
        for (int i = 0; i < steps; i++)
        {
            a += ((float)r.NextDouble() - 0.5f) * 0.5f;
            x += Mathf.Cos(a);
            y += Mathf.Sin(a);
            if (x < 2f || y < 2f || x > s - 3f || y > s - 3f) return;
            int ix = (int)x, iy = (int)y;
            Put(px, s, s, ix + 1, iy, soft);
            Put(px, s, s, ix, iy + 1, soft);
            Put(px, s, s, ix, iy, dark);
            if (depth < 2 && r.NextDouble() < 0.035) Walk(px, s, r, x, y, a + (r.NextDouble() < 0.5 ? 0.8f : -0.8f), 20 + r.Next(40), depth + 1);
        }
    }

    public static Texture2D Crack()
    {
        int s = 256;
        Color[] px = Blank(s, s, new Color(0.05f, 0.05f, 0.055f));
        System.Random r = new System.Random(11);
        for (int k = 0; k < 3; k++) Walk(px, s, r, (float)r.NextDouble() * s, (float)r.NextDouble() * s, (float)(r.NextDouble() * 6.28), 70 + r.Next(90), 0);
        return Make(px, s, s, TextureWrapMode.Clamp);
    }

    public static Texture2D Patch()
    {
        int s = 128;
        Color[] px = Blank(s, s, new Color(0.17f, 0.17f, 0.18f));
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float e = Mathf.Min(Mathf.Min(x, s - 1 - x), Mathf.Min(y, s - 1 - y));
            float f = Sm(4f, 22f, e);
            float n = Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.03f;
            px[y * s + x] = new Color(0.17f + n, 0.17f + n, 0.18f + n, f);
        }
        return Make(px, s, s, TextureWrapMode.Clamp);
    }

    public static Texture2D Puddle()
    {
        int s = 128;
        Color[] px = new Color[s * s];
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = x / (s - 1f) * 2f - 1f, v = y / (s - 1f) * 2f - 1f;
            float ang = Mathf.Atan2(v, u);
            float rad = 0.5f + 0.42f * Mathf.PerlinNoise(Mathf.Cos(ang) * 1.5f + 5f, Mathf.Sin(ang) * 1.5f + 5f);
            float d = Mathf.Sqrt(u * u + v * v);
            px[y * s + x] = new Color(1f, 1f, 1f, (1f - Sm(rad - 0.2f, rad, d)) * 0.9f);
        }
        return Make(px, s, s, TextureWrapMode.Clamp);
    }

    public static Texture2D Zebra()
    {
        int s = 128;
        Color[] px = new Color[s * s];
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = x / (float)s, v = y / (float)s;
            float band = Sm(0.22f, 0.27f, v) * (1f - Sm(0.73f, 0.78f, v));
            float wear = 0.82f + 0.18f * Pn(u, v, 5, 2) - 0.25f * Mathf.Clamp01(Pn(u, v, 6, 3) * 2f - 0.9f);
            px[y * s + x] = new Color(0.92f, 0.92f, 0.88f, band * Mathf.Clamp01(wear) * 0.92f);
        }
        return Make(px, s, s, TextureWrapMode.Repeat);
    }

    public static Texture2D Manhole()
    {
        int s = 128;
        Color[] px = Blank(s, s, new Color(0.16f, 0.16f, 0.17f));
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = x - 63.5f, dy = y - 63.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > 61f) continue;
            Color c = new Color(0.15f, 0.15f, 0.16f, 1f);
            if (d > 52f) c = new Color(0.30f, 0.30f, 0.31f, 1f);
            else if (d < 46f && ((x % 12) < 2 || (y % 12) < 2)) c = new Color(0.24f, 0.24f, 0.25f, 1f);
            c.a = Mathf.Clamp01(61f - d);
            px[y * s + x] = c;
        }
        return Make(px, s, s, TextureWrapMode.Clamp);
    }

    public static Texture2D Dust(int fade = 0)
    {
        int s = 128;
        Color[] px = new Color[s * s];
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = x / (float)s, v = y / (s - 1f);
            float n = Mathf.Clamp01(Pn(u, v, 3, 1) * 1.7f - 0.25f);
            float n2 = 0.6f + 0.4f * Pn(u, v, 4, 2);
            px[y * s + x] = new Color(0.43f, 0.37f, 0.28f, Mathf.Pow(v, 1.6f) * n * n2 * 0.6f * (fade == 1 ? Sm(0f, 1f, x / (s - 1f)) : fade == 2 ? 1f - Sm(0f, 1f, x / (s - 1f)) : 1f));
        }
        return Make(px, s, s, fade == 0 ? TextureWrapMode.Repeat : TextureWrapMode.Clamp);
    }

    public static Texture2D Leaves()
    {
        int s = 128;
        Color[] px = Blank(s, s, new Color(0.6f, 0.35f, 0.12f));
        System.Random r = new System.Random(23);
        Color[] pal = { new Color(0.78f, 0.38f, 0.12f), new Color(0.82f, 0.62f, 0.15f), new Color(0.50f, 0.28f, 0.12f), new Color(0.55f, 0.50f, 0.14f), new Color(0.65f, 0.22f, 0.10f) };
        for (int i = 0; i < 80; i++)
        {
            float cx = (float)r.NextDouble() * s;
            float cy = s * Mathf.Sqrt((float)r.NextDouble());
            float ang = (float)r.NextDouble() * 3.14f;
            float a = 3f + (float)r.NextDouble() * 1.8f, b = 1.5f + (float)r.NextDouble() * 0.9f;
            Color col = pal[r.Next(pal.Length)];
            float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
            for (int dy = -6; dy <= 6; dy++)
            for (int dx = -6; dx <= 6; dx++)
            {
                float lu = dx * cs + dy * sn, lv = -dx * sn + dy * cs;
                if ((lu / a) * (lu / a) + (lv / b) * (lv / b) <= 1f) Put(px, s, s, (int)cx + dx, (int)cy + dy, new Color(col.r, col.g, col.b, 0.95f));
            }
        }
        return Make(px, s, s, TextureWrapMode.Repeat);
    }

    public static Texture2D Flowers()
    {
        int s = 128;
        Color[] px = Blank(s, s, new Color(0.3f, 0.55f, 0.2f));
        System.Random r = new System.Random(31);
        Color[] pal = { new Color(0.96f, 0.95f, 0.9f), new Color(0.97f, 0.82f, 0.2f), new Color(0.95f, 0.5f, 0.65f), new Color(0.65f, 0.45f, 0.85f), new Color(0.9f, 0.25f, 0.25f) };
        for (int k = 0; k < 20; k++)
        {
            float cx = 10f + (float)r.NextDouble() * (s - 20f), cy = 10f + (float)r.NextDouble() * (s - 20f);
            Color col = pal[r.Next(pal.Length)];
            int n = 6 + r.Next(6);
            for (int i = 0; i < n; i++)
            {
                int fx = (int)(cx + ((float)r.NextDouble() - 0.5f) * 14f), fy = (int)(cy + ((float)r.NextDouble() - 0.5f) * 14f);
                Put(px, s, s, fx - 1, fy - 2, new Color(0.25f, 0.5f, 0.18f, 0.95f));
                Put(px, s, s, fx + 1, fy - 2, new Color(0.25f, 0.5f, 0.18f, 0.95f));
                Put(px, s, s, fx, fy, new Color(col.r, col.g, col.b, 1f));
                Put(px, s, s, fx + 1, fy, new Color(col.r, col.g, col.b, 1f));
                Put(px, s, s, fx, fy + 1, new Color(col.r, col.g, col.b, 1f));
                Put(px, s, s, fx + 1, fy + 1, new Color(col.r, col.g, col.b, 1f));
            }
        }
        return Make(px, s, s, TextureWrapMode.Clamp);
    }

    public static Texture2D Tuft()
    {
        int s = 64;
        Color[] px = Blank(s, s, new Color(0.3f, 0.55f, 0.2f));
        System.Random r = new System.Random(41);
        for (int b = 0; b < 11; b++)
        {
            float bx = 6f + (float)r.NextDouble() * 52f;
            float h = 28f + (float)r.NextDouble() * 34f;
            float lean = ((float)r.NextDouble() - 0.5f) * 0.7f;
            float shade = 0.85f + (float)r.NextDouble() * 0.3f;
            for (int y = 0; y < (int)h; y++)
            {
                float t = y / h;
                float x = bx + lean * t * h;
                float w = 2.2f * (1f - t) + 0.5f;
                Color c = Color.Lerp(new Color(0.18f, 0.38f, 0.13f), new Color(0.55f, 0.75f, 0.28f), t) * shade;
                c.a = 1f;
                for (int xi = Mathf.FloorToInt(x - w); xi <= Mathf.CeilToInt(x + w); xi++) Put(px, s, s, xi, y, c);
            }
        }
        return Make(px, s, s, TextureWrapMode.Clamp);
    }

    public static Texture2D Path()
    {
        int s = 128;
        Color[] px = new Color[s * s];
        System.Random r = new System.Random(53);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = x / (s - 1f), v = y / (float)s;
            float d = Mathf.Abs(u - 0.5f) * 2f;
            float e = 0.68f + 0.22f * Pn(v, u, 5, 1);
            float a = (1f - Sm(e - 0.28f, e, d)) * (0.6f + 0.35f * Pn(v, u, 6, 2));
            if (r.NextDouble() < 0.06) a *= 0.4f;
            float k = 0.9f + 0.2f * Pn(v, u, 7, 3);
            px[y * s + x] = new Color(0.43f * k, 0.33f * k, 0.22f * k, a);
        }
        return Make(px, s, s, TextureWrapMode.Repeat);
    }

    public static Texture2D RoofWeather()
    {
        int s = 256;
        Color[] px = new Color[s * s];
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = x / (float)s, v = y / (float)s;
            float moss = Sm(0.58f, 0.72f, Pn(u, v, 1, 1)) * 0.8f;
            float stain = Sm(0.62f, 0.78f, Pn(u, v, 2, 1)) * 0.4f;
            float lichen = Sm(0.72f, 0.78f, Pn(u, v, 8, 3)) * 0.5f;
            Color c = new Color(0.1f, 0.1f, 0.1f, stain);
            if (lichen > c.a) c = new Color(0.62f, 0.66f, 0.52f, lichen);
            if (moss > c.a)
            {
                float k = 0.85f + 0.3f * Pn(u, v, 9, 2);
                c = new Color(0.27f * k, 0.38f * k, 0.15f * k, moss);
            }
            px[y * s + x] = c;
        }
        return Make(px, s, s, TextureWrapMode.Repeat);
    }

    public static Texture2D WallWeather()
    {
        int s = 256;
        Color[] px = new Color[s * s];
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float u = x / (float)s, v = y / (s - 1f);
            float band = (1f - Sm(0f, 0.16f + 0.14f * Pn(u, 0.3f, 4, 2), v)) * (0.3f + 0.6f * Pn(u, 0.5f, 10, 3));
            Color low = Color.Lerp(new Color(0.20f, 0.18f, 0.14f), new Color(0.27f, 0.40f, 0.17f), Sm(0.4f, 0.65f, Pn(u, 0.7f, 5, 2)));
            float st = Mathf.Pow(Sm(0.55f, 0.85f, Pn(u, 0.5f, 6, 6)), 2f);
            float sa = st * Mathf.Pow(Mathf.Clamp01((v - 0.15f) / 0.85f), 1.5f) * 0.5f;
            Color c = new Color(0.12f, 0.12f, 0.13f, sa);
            if (band * 0.65f > c.a) c = new Color(low.r, low.g, low.b, band * 0.65f);
            px[y * s + x] = c;
        }
        return Make(px, s, s, TextureWrapMode.Repeat);
    }
}
