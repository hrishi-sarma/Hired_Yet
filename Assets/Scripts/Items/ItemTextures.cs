using System.Collections.Generic;
using UnityEngine;

namespace Items
{
    // Runtime texture cache. Layout (expensive) is cached per (def, seed); the name layer is drawn
    // on a copy, so name changes only redraw the cheap part. Textures are cached per name state.
    public static class ItemTextures
    {
        class Base { public ItemCanvas canvas; public RectInt nameRect; }
        static readonly Dictionary<string, Base> bases = new Dictionary<string, Base>();
        static readonly Dictionary<string, Texture2D> finals = new Dictionary<string, Texture2D>();
        const int MaxBases = 60, MaxFinals = 200;

        public static Vector2Int SizeFor(ItemDef d) { return ItemLayouts.SizeFor(d); }

        static string BaseKey(ItemDef d, int seed) { return d.id + ":" + seed; }
        static string FinalKey(ItemInstance i)
        { return i.def.id + ":" + i.seed + ":" + (int)i.nameState + ":" + i.printedName + ":" + i.originalName; }

        static Base GetBase(ItemDef d, int seed)
        {
            string k = BaseKey(d, seed); Base b;
            if (bases.TryGetValue(k, out b)) return b;
            if (bases.Count >= MaxBases) bases.Clear();
            b = new Base(); b.canvas = ItemLayouts.Build(d, seed, out b.nameRect); bases[k] = b;
            return b;
        }

        public static RectInt NameRect(ItemDef d, int seed) { return GetBase(d, seed).nameRect; }

        public static Texture2D Get(ItemInstance i)
        {
            string k = FinalKey(i); Texture2D t;
            if (finals.TryGetValue(k, out t) && t != null) return t;
            var b = GetBase(i.def, i.seed);
            var c = new ItemCanvas(b.canvas);
            if (b.nameRect.width > 0) DrawName(c, b.nameRect, i);
            t = new Texture2D(c.w, c.h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; t.name = "Item_" + k;
            t.SetPixels32(c.px); t.Apply(false, false);
            if (finals.Count >= MaxFinals) ClearFinals();
            finals[k] = t;
            return t;
        }

        // Call after a name edit if you hold on to the old texture (Get() already keys on name state).
        public static Texture2D Rebuild(ItemInstance i)
        {
            string k = FinalKey(i); Texture2D t;
            if (finals.TryGetValue(k, out t)) { if (t != null) WorldUtil.Kill(t); finals.Remove(k); }
            return Get(i);
        }

        static void ClearFinals() { foreach (var t in finals.Values) if (t != null) WorldUtil.Kill(t); finals.Clear(); }
        public static void ClearCache() { ClearFinals(); bases.Clear(); }

        static float Fit(string s, RectInt r, float maxH)
        { return Mathf.Max(1f, Mathf.Min(maxH / 7f, (r.width - 8f) / Mathf.Max(1f, s.Length * 6f))); }

        static void DrawName(ItemCanvas c, RectInt r, ItemInstance i)
        {
            string player = ItemFactory.PlayerName;
            string other = ItemFactory.RandomOtherName(i.seed, player);
            switch (i.nameState)
            {
                case NameState.Own:
                case NameState.Other:
                {
                    string s = !string.IsNullOrEmpty(i.printedName) ? i.printedName : (i.nameState == NameState.Own ? player : other);
                    float sc = Fit(s, r, r.height * 0.85f);
                    c.Text(s, r.x + 4, r.yMax - 7 * sc - 2, sc, ItemPal.Ink);
                    break;
                }
                case NameState.Scribbled:
                {
                    string orig = !string.IsNullOrEmpty(i.originalName) ? i.originalName : other;
                    string mine = !string.IsNullOrEmpty(i.printedName) ? i.printedName : player;
                    float s1 = Fit(orig, r, r.height * 0.5f), y1 = r.yMax - 7 * s1 - 2, tw = ItemCanvas.TextWidth(orig, s1), x1 = r.x + 4;
                    c.Text(orig, x1, y1, s1, ItemPal.Ink);
                    float ym = y1 + 3.5f * s1;
                    for (int k = 0; k < 3; k++)
                    {
                        float yy = ym + (k - 1) * s1 * 0.9f;
                        c.Line(x1 - 4, yy, x1 + tw + 4, yy + (k % 2 == 0 ? 3 : -3), 3, ItemPal.Ink);
                    }
                    float s2 = Fit(mine, r, r.height * 0.5f), y2 = Mathf.Max(2f, y1 - 7 * s2 - 3);
                    c.Text(mine, x1 + 6, y2, s2, ItemPal.Pen, 0.25f, 0.5f, i.seed);
                    break;
                }
                case NameState.Illegible:
                {
                    var rnd = new System.Random(i.seed ^ 0x51);
                    var smudge = new Color(0.35f, 0.30f, 0.36f, 0.22f);
                    for (int k = 0; k < 16; k++)
                        c.Circle(r.x + 10 + (float)rnd.NextDouble() * (r.width - 20), r.y + r.height * (0.35f + 0.5f * (float)rnd.NextDouble()),
                                 8 + (float)rnd.NextDouble() * 12, smudge);
                    c.Line(r.x + 8, r.yMax - r.height * 0.3f, r.xMax - 40, r.yMax - r.height * 0.3f + 5, r.height * 0.3f, new Color(0.3f, 0.27f, 0.33f, 0.3f));
                    break;
                }
            }
        }
    }
}
