using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Rnd = System.Random;

/// <summary>
/// Pool expansion (30 games). Every level is generated from a fresh seed; level 1 = easy, 2 = medium, 3 = hard.
/// A per-game/per-level history of signatures stops the same level from showing up again right away.
/// </summary>
public partial class CognitiveAssessmentController
{
    int levelToken;
    [SerializeField] bool keepMazeAndFlow = false;

    static readonly Dictionary<string, List<string>> seen = new Dictionary<string, List<string>>();
    static List<CognitiveGameType> lastGames = new List<CognitiveGameType>();
    static readonly string[] PalName = { "Yellow", "Blue", "Pink", "Green", "Orange", "Purple" };
    static readonly string[] ShapeName = { "Circle", "Square", "Diamond", "Ring" };
    static Color[] _pal;
    static Color[] Pal { get { return _pal ?? (_pal = new[] { ChalkYellow, ChalkCyan, ChalkPink, ChalkGreen, ChalkOrange, new Color(0.78f, 0.65f, 1f) }); } }
    static Color CellC { get { return new Color(0.16f, 0.32f, 0.22f, 1f); } }

    class GD { public int n, k, m; public List<int> l = new List<int>(), l2 = new List<int>(); public string sig; }
    class Q
    {
        public string prompt, head, sig; public Color hc = Color.white; public int headSize = 44, ans;
        public List<string> opts = new List<string>(); public List<Action<RectTransform>> icons = new List<Action<RectTransform>>();
        public Action<RectTransform> draw;
    }

    // ===================================================================== pick / titles / time
    List<CognitiveGameType> PickGames(Rnd rng)
    {
        var pool = ((CognitiveGameType[])Enum.GetValues(typeof(CognitiveGameType))).ToList();
        var res = new List<CognitiveGameType>();
        if (keepMazeAndFlow) { res.Add(CognitiveGameType.MazeRunner); res.Add(CognitiveGameType.FlowLineMatch); pool.Remove(CognitiveGameType.MazeRunner); pool.Remove(CognitiveGameType.FlowLineMatch); }
        var fresh = pool.Where(g => !lastGames.Contains(g)).ToList();   // nothing from the previous playthrough
        Shf(rng, fresh);
        while (res.Count < 3 && fresh.Count > 0) { res.Add(fresh[0]); fresh.RemoveAt(0); }
        Shf(rng, res);
        lastGames = new List<CognitiveGameType>(res);
        return res;
    }

    string ExtraTitle(CognitiveGameType t)
    {
        switch (t)
        {
            case CognitiveGameType.NumberSeries: return "NUMBER TRAIN";
            case CognitiveGameType.ArrowSpin: return "SPIN SPOT";
            case CognitiveGameType.CopyGrid: return "COPY CAT GRID";
            case CognitiveGameType.SpotDifference: return "SPOT THE CHANGE";
            case CognitiveGameType.CountShapes: return "COUNT UP";
            case CognitiveGameType.TileSum: return "TILE SUM";
            case CognitiveGameType.LightsOut: return "LIGHTS OUT";
            case CognitiveGameType.SlidePuzzle: return "SLIDE PUZZLE";
            case CognitiveGameType.StarCatcher: return "STAR CATCHER";
            case CognitiveGameType.PaintMixer: return "PAINT MIXER";
            case CognitiveGameType.ClockReader: return "CLOCK TIME";
            case CognitiveGameType.ShopSum: return "TINY SHOP";
            case CognitiveGameType.NumberDash: return "NUMBER DASH";
            case CognitiveGameType.WordScramble: return "WORD SCRAMBLE";
            case CognitiveGameType.ArrowRush: return "ARROW RUSH";
            case CognitiveGameType.CardPairs: return "CARD PAIRS";
            case CognitiveGameType.TrailMemory: return "TRAIL MEMORY";
            case CognitiveGameType.HeightRiddle: return "WHO IS TALLER";
            case CognitiveGameType.ColorSudoku: return "COLOR GRID";
            case CognitiveGameType.TowerStack: return "TOWER STACK";
            default: return "FUN PUZZLE";
        }
    }

    float ExtraTime(CognitiveGameType t)
    {
        switch (t)
        {
            case CognitiveGameType.SlidePuzzle: case CognitiveGameType.TowerStack: return 110f;
            case CognitiveGameType.LightsOut: case CognitiveGameType.CardPairs: return 90f;
            case CognitiveGameType.TrailMemory: return 75f;
            default: return defaultLevelTime;
        }
    }

    // ===================================================================== generic helpers
    static Vector2 V(float x, float y) { return new Vector2(x, y); }
    static T Pk<T>(Rnd r, IList<T> l) { return l[r.Next(l.Count)]; }
    static void Shf<T>(Rnd r, IList<T> l) { for (int i = l.Count - 1; i > 0; i--) { int j = r.Next(i + 1); T t = l[i]; l[i] = l[j]; l[j] = t; } }
    static string Hx(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }
    static Key KeyAt(Key b, int i) { return (Key)((int)b + i); }
    static string TokName(int t) { return PalName[t % 6] + " " + ShapeName[(t / 6) % 4]; }
    void Hint(string s) { feedbackText.text = "<color=" + HexYellow + ">" + s + "</color>"; }

    T Fresh<T>(CognitiveGameType t, int L, Func<Rnd, int, T> g, Func<T, string> sg)
    {
        string key = t + "|" + L; List<string> h;
        if (!seen.TryGetValue(key, out h)) { h = new List<string>(); seen[key] = h; }
        T v = default(T);
        for (int i = 0; i < 40; i++) { v = g(new Rnd(Guid.NewGuid().GetHashCode()), L); if (!h.Contains(sg(v))) break; }
        h.Add(sg(v)); if (h.Count > 12) h.RemoveAt(0);
        return v;
    }
    Q FQ(CognitiveGameType t, int L, Func<Rnd, int, Q> g) { return Fresh(t, L, g, q => q.sig); }
    GD FG(CognitiveGameType t, int L, Func<Rnd, int, GD> g) { return Fresh(t, L, g, d => d.sig); }

    RectTransform Img(Transform p, Vector2 pos, Vector2 sz, Sprite sp, Color c, float rot = 0f, bool ray = false)
    {
        RectTransform rt = UIKit.NewRect("g", p);
        rt.anchoredPosition = pos; rt.sizeDelta = sz; rt.localEulerAngles = new Vector3(0f, 0f, rot);
        Image im = rt.gameObject.AddComponent<Image>();
        im.sprite = sp; im.color = c; im.raycastTarget = ray;
        if (sp != null) im.type = Image.Type.Sliced;
        return rt;
    }

    RectTransform Box(Transform p, Vector2 pos, Vector2 sz, Color c, int rad = 10, bool ring = false, float rot = 0f, bool ray = false)
    {
        RectTransform rt = Img(p, pos, sz, UIKit.Rounded(rad), c, rot, ray);
        if (ring) Img(rt, Vector2.zero, sz, UIKit.Ring(rad, 2), new Color(ChalkWhite.r, ChalkWhite.g, ChalkWhite.b, 0.45f));
        return rt;
    }

    TextMeshProUGUI Tx(Transform p, string s, float size, Vector2 pos, Vector2 sz, Color c)
    {
        TextMeshProUGUI t = CreateChalkText(p, s, size, TextAlignmentOptions.Center, c);
        t.raycastTarget = false; t.enableWordWrapping = false;
        t.enableAutoSizing = true; t.fontSizeMin = size * 0.5f; t.fontSizeMax = size;
        t.rectTransform.anchoredPosition = pos; t.rectTransform.sizeDelta = sz;
        return t;
    }

    Button Click(RectTransform rt, Action a)
    {
        Image im = rt.GetComponent<Image>(); im.raycastTarget = true;
        Button b = rt.gameObject.AddComponent<Button>();
        b.onClick.AddListener(() => { if (!levelResolved) a(); });
        return b;
    }

    RectTransform Arena()
    {
        RectTransform rt = UIKit.NewRect("Arena", gameViewport);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = V(780f, 400f);
        return rt;
    }

    void Tok(Transform p, int t, Vector2 pos, float s)
    {
        Color c = Pal[t % 6]; int sh = (t / 6) % 4;
        if (sh == 0) Box(p, pos, V(s, s), c, (int)(s / 2));
        else if (sh == 1) Box(p, pos, V(s * 0.88f, s * 0.88f), c, 5);
        else if (sh == 2) Box(p, pos, V(s * 0.68f, s * 0.68f), c, 3, false, 45f);
        else Img(p, pos, V(s, s), UIKit.Ring((int)(s / 2), (int)Mathf.Max(4f, s / 6f)), c);
    }

    void Arrow(Transform p, Vector2 pos, float ang, float len, Color c, float th = 10f)
    {
        float rad = ang * Mathf.Deg2Rad; Vector2 u = V(Mathf.Sin(rad), Mathf.Cos(rad));
        Img(p, pos - u * len * 0.12f, V(th, len * 0.7f), UIKit.Rounded(4), c, -ang);
        Img(p, pos + u * len * 0.32f, V(th * 2f, th * 2f), UIKit.Rounded(3), c, -ang + 45f);
    }

    Action<RectTransform> TokIcon(int t) { return v => Tok(v, t, Vector2.zero, 34f); }
    Action<RectTransform> SwIcon(Color c) { return v => Box(v, Vector2.zero, V(34f, 34f), c, 17); }
    Action<RectTransform> ArrIcon(float a) { return v => Arrow(v, Vector2.zero, a, 34f, ChalkCyan, 6f); }

    List<int> NumOpt(Rnd r, int c, int n, int spread)
    {
        var s = new List<int>();
        for (int i = 0; i < 80 && s.Count < n; i++) { int v = c + r.Next(-spread, spread + 1); if (v != c && v >= 0 && !s.Contains(v)) s.Add(v); }
        while (s.Count < n) s.Add(c + s.Count + 1 + spread);
        return s;
    }

    // ---- multiple-choice scaffolding ----
    void O(Q q, string t, Action<RectTransform> ic = null) { q.opts.Add(t); q.icons.Add(ic); }

    Q Fin(Q q, Rnd r, string extra = "")
    {
        var idx = Enumerable.Range(0, q.opts.Count).ToList(); Shf(r, idx);
        string cor = q.opts[0]; var o = new List<string>(); var ic = new List<Action<RectTransform>>();
        foreach (int i in idx) { o.Add(q.opts[i]); ic.Add(q.icons[i]); }
        q.ans = idx.IndexOf(0); q.opts = o; q.icons = ic;
        q.sig = q.prompt + "|" + q.head + "|" + cor + "|" + extra + "|" + string.Join(",", o.OrderBy(x => x));
        return q;
    }

    void ShowQ(Q q)
    {
        promptLabel.text = q.prompt;
        RectTransform a = Arena();
        bool hasHead = !string.IsNullOrEmpty(q.head), vis = hasHead || q.draw != null;
        if (hasHead) Tx(a, q.head, q.headSize, V(0f, 105f), V(760f, 170f), q.hc);
        if (q.draw != null) q.draw(Box(a, V(0f, 105f), V(760f, 170f), Color.clear, 4));
        int n = q.opts.Count, cols = n <= 3 ? n : (n == 4 ? 2 : 3), rowsN = (n + cols - 1) / cols;
        float w = n <= 3 ? 240f : (n == 4 ? 330f : 240f), oy = vis ? 0f : 95f;
        for (int i = 0; i < n; i++)
        {
            int row = i / cols, col = i % cols, inRow = row < rowsN - 1 ? cols : n - cols * (rowsN - 1), idx = i;
            float x = (col - (inRow - 1) / 2f) * (w + 14f);
            float y = (rowsN == 1 ? -110f : -85f - row * 70f) + oy;
            RectTransform b = Box(a, V(x, y), V(w, 58f), CellC, 12, true, 0f, true);
            Click(b, () => PickQ(idx, q.ans));
            bool ic = q.icons[i] != null;
            if (ic) { RectTransform r = UIKit.NewRect("ic", b); r.anchoredPosition = V(-w / 2f + 34f, 0f); r.sizeDelta = V(44f, 44f); q.icons[i](r); }
            Tx(b, (char)('A' + i) + ".  " + q.opts[i], 24f, V(ic ? 30f : 0f, 0f), V(w - (ic ? 90f : 30f), 50f), ChalkWhite);
        }
        StartCoroutine(QKeys(n, q.ans, levelToken));
    }

    void PickQ(int i, int ans)
    {
        if (i == ans) OnLevelPassed("SUPER JOB!"); else OnLevelFailed("Not quite! Keep going!");
    }

    IEnumerator QKeys(int n, int ans, int tok)
    {
        while (!levelResolved && tok == levelToken)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
                for (int i = 0; i < n; i++)
                    if (kb[KeyAt(Key.Digit1, i)].wasPressedThisFrame || kb[KeyAt(Key.A, i)].wasPressedThisFrame) { PickQ(i, ans); break; }
            yield return null;
        }
    }

    // ===================================================================== dispatcher
    bool SetupGenerated(CognitiveGameType t, int L)
    {
        switch (t)
        {
            case CognitiveGameType.MazeRunner: case CognitiveGameType.FlowLineMatch: case CognitiveGameType.MemoryGrid: return false;
            case CognitiveGameType.PatternSequence: ShowQ(FQ(t, L, GenPattern)); return true;
            case CognitiveGameType.BalanceScales: ShowQ(FQ(t, L, GenBalance)); return true;
            case CognitiveGameType.OddOneOut: ShowQ(FQ(t, L, GenOdd)); return true;
            case CognitiveGameType.FruitMath: ShowQ(FQ(t, L, GenMath)); return true;
            case CognitiveGameType.DirectionCompass: ShowQ(FQ(t, L, GenCompass)); return true;
            case CognitiveGameType.CartoonColorMatch: ShowQ(FQ(t, L, GenStroop)); return true;
            case CognitiveGameType.CircuitWire: BuildPipes(FG(t, L, GenPipes), L); return true;
            case CognitiveGameType.NumberSeries: ShowQ(FQ(t, L, GenSeries)); return true;
            case CognitiveGameType.ArrowSpin: ShowQ(FQ(t, L, GenSpin)); return true;
            case CognitiveGameType.CountShapes: ShowQ(FQ(t, L, GenCount)); return true;
            case CognitiveGameType.PaintMixer: ShowQ(FQ(t, L, GenPaint)); return true;
            case CognitiveGameType.ClockReader: ShowQ(FQ(t, L, GenClock)); return true;
            case CognitiveGameType.ShopSum: ShowQ(FQ(t, L, GenShop)); return true;
            case CognitiveGameType.WordScramble: ShowQ(FQ(t, L, GenWord)); return true;
            case CognitiveGameType.HeightRiddle: ShowQ(FQ(t, L, GenHeight)); return true;
            case CognitiveGameType.ColorSudoku: ShowQ(FQ(t, L, GenSudoku)); return true;
            case CognitiveGameType.CopyGrid: BuildCopyGrid(FG(t, L, GenCopyGrid)); return true;
            case CognitiveGameType.SpotDifference: BuildSpot(FG(t, L, GenSpot)); return true;
            case CognitiveGameType.TileSum: BuildTileSum(FG(t, L, GenTileSum)); return true;
            case CognitiveGameType.LightsOut: BuildLights(FG(t, L, GenLights)); return true;
            case CognitiveGameType.SlidePuzzle: BuildSlide(FG(t, L, GenSlide)); return true;
            case CognitiveGameType.StarCatcher: BuildStars(L); return true;
            case CognitiveGameType.NumberDash: BuildDash(FG(t, L, GenDash)); return true;
            case CognitiveGameType.ArrowRush: BuildRush(FG(t, L, GenRush), L); return true;
            case CognitiveGameType.CardPairs: BuildPairs(FG(t, L, GenPairs)); return true;
            case CognitiveGameType.TrailMemory: BuildTrail(FG(t, L, GenTrail)); return true;
            case CognitiveGameType.TowerStack: BuildHanoi(L); return true;
        }
        return false;
    }

    // ===================================================================== Q generators (old games, now procedural)
    Q GenPattern(Rnd r, int L)
    {
        string[] us = L == 0 ? new[] { "AB" } : L == 1 ? new[] { "ABC", "AAB", "ABB" } : new[] { "ABCD", "AABC", "ABAC", "ABBC", "AABB" };
        string u = Pk(r, us); int n = u.Length * 2 + (L == 2 ? 2 : 1);
        var ids = Enumerable.Range(0, 24).ToList(); Shf(r, ids);
        var map = new Dictionary<char, int>(); int k = 0;
        foreach (char ch in u) if (!map.ContainsKey(ch)) map[ch] = ids[k++];
        var seq = new List<int>(); for (int i = 0; i <= n; i++) seq.Add(map[u[i % u.Length]]);
        int cor = seq[n];
        var q = new Q { prompt = "What comes next in the pattern?" };
        O(q, TokName(cor), TokIcon(cor));
        var wr = map.Values.Where(x => x != cor).ToList(); wr.AddRange(ids.Skip(k)); Shf(r, wr);
        foreach (int w in wr.Take(3)) O(q, TokName(w), TokIcon(w));
        q.draw = v =>
        {
            for (int i = 0; i < n; i++) Tok(v, seq[i], V((i - n / 2f) * 64f, 0f), 50f);
            Box(v, V((n - n / 2f) * 64f, 0f), V(50f, 50f), CellC, 12, true);
            Tx(v, "?", 40f, V((n - n / 2f) * 64f, 0f), V(50f, 50f), ChalkYellow);
        };
        return Fin(q, r, string.Join(",", seq.Take(n)));
    }

    static readonly string[] BN = { "Mouse", "Strawberry", "Apple", "Cat", "Pumpkin", "Watermelon", "Dog", "Bicycle", "Human", "Piano", "Horse", "Car", "Elephant", "Bus", "Whale", "Feather", "Brick", "Laptop", "Rabbit", "Lion", "Cow" };
    static readonly float[] BW = { 0.03f, 0.02f, 0.2f, 4f, 5f, 8f, 25f, 13f, 70f, 300f, 500f, 1500f, 5000f, 12000f, 100000f, 0.001f, 2.5f, 2f, 2f, 190f, 700f };

    Q GenBalance(Rnd r, int L)
    {
        int cnt = L + 2; float ratio = L == 0 ? 40f : L == 1 ? 4f : 2.2f;
        var idx = Enumerable.Range(0, BN.Length).ToList(); var pick = new List<int>();
        for (int tr = 0; tr < 200 && pick.Count < cnt; tr++)
        {
            Shf(r, idx); pick = new List<int>();
            foreach (int i in idx)
            {
                bool ok = true;
                foreach (int j in pick) if (Mathf.Max(BW[i], BW[j]) / Mathf.Min(BW[i], BW[j]) < ratio) { ok = false; break; }
                if (ok) { pick.Add(i); if (pick.Count == cnt) break; }
            }
        }
        var sorted = pick.OrderByDescending(i => BW[i]).ToList();
        bool heavy = L == 0 || r.Next(2) == 0;
        int cor = heavy ? sorted[0] : sorted[sorted.Count - 1];
        var q = new Q { prompt = L == 0 ? "Two friends hop on a seesaw. Who is heavier?" : (heavy ? "Which one is the HEAVIEST?" : "Which one is the LIGHTEST?") };
        O(q, BN[cor]); foreach (int i in pick) if (i != cor) O(q, BN[i]);
        Shf(r, pick);
        q.draw = v =>
        {
            float sp = cnt == 2 ? 270f : cnt == 3 ? 230f : 195f;
            for (int i = 0; i < cnt; i++)
            {
                Vector2 pos = V((i - (cnt - 1) / 2f) * sp, 0f);
                Box(v, pos, V(cnt == 4 ? 170f : 190f, 74f), Pal[i % 6], 16, true);
                Tx(v, BN[pick[i]], 30f, pos, V(cnt == 4 ? 150f : 170f, 60f), UIKit.Dark);
            }
            if (cnt == 2) Tx(v, "VS", 34f, V(0f, 0f), V(60f, 50f), ChalkWhite);
        };
        return Fin(q, r, string.Join(",", pick.OrderBy(x => x)));
    }

    static readonly string[][] OG = {
        new[]{"Apple","Banana","Grape","Mango","Pear","Cherry","Melon"},
        new[]{"Carrot","Potato","Onion","Pea","Corn","Beet"},
        new[]{"Dog","Cat","Horse","Cow","Bunny","Bear"},
        new[]{"Eagle","Parrot","Owl","Duck","Crow","Robin"},
        new[]{"Shark","Tuna","Salmon","Trout","Eel","Cod"},
        new[]{"Car","Bus","Train","Truck","Tram","Taxi"},
        new[]{"Guitar","Piano","Drum","Flute","Violin","Harp"},
        new[]{"Hammer","Saw","Drill","Wrench","Pliers","Chisel"},
        new[]{"Chair","Table","Sofa","Desk","Bed","Shelf"},
        new[]{"Mars","Venus","Saturn","Earth","Jupiter","Neptune"} };
    static readonly int[] OS = { 0, 0, 1, 1, 1, 2, 2, 2, 2, 3 };

    Q GenOdd(Rnd r, int L)
    {
        int gx = 0; List<int> cand = null;
        for (int t = 0; t < 50; t++)
        {
            gx = r.Next(OG.Length); int g0 = gx;
            cand = Enumerable.Range(0, OG.Length).Where(g => g != g0 && (L == 0 ? OS[g] != OS[g0] : OS[g] == OS[g0])).ToList();
            if (cand.Count > 0) break;
        }
        int gy = Pk(r, cand); int same = L == 2 ? 5 : 3;
        var a = OG[gx].ToList(); Shf(r, a);
        var q = new Q { prompt = "Which one does NOT belong with the others?", head = "Spot the odd one out!", hc = ChalkYellow, headSize = 46 };
        O(q, Pk(r, OG[gy]));
        foreach (string s in a.Take(same)) O(q, s);
        return Fin(q, r, gx + "-" + gy);
    }

    static readonly string[] FN = { "APPLE", "BANANA", "CHERRY", "GRAPE", "LEMON", "MELON" };
    string FNc(int i) { return "<color=" + Hx(Pal[i]) + ">" + FN[i] + "</color>"; }

    Q GenMath(Rnd r, int L)
    {
        var f = Enumerable.Range(0, 6).ToList(); Shf(r, f); int A = f[0], B = f[1];
        int a = r.Next(2, 8), b = r.Next(2, 9); if (b == a) b++;
        string h; int cor; string ask;
        if (L == 0) { h = FNc(A) + " + " + FNc(A) + " = " + 2 * a; cor = a; ask = FN[A]; }
        else if (L == 1) { h = FNc(A) + " + " + FNc(A) + " = " + 2 * a + "\n" + FNc(A) + " + " + FNc(B) + " = " + (a + b); cor = b; ask = FN[B]; }
        else { h = FNc(A) + " + " + FNc(A) + " + " + FNc(B) + " = " + (2 * a + b) + "\n" + FNc(A) + " + " + FNc(B) + " + " + FNc(B) + " = " + (a + 2 * b); cor = a; ask = FN[A]; }
        var q = new Q { prompt = "Fruit Math: how much is one " + ask + "?", head = h, headSize = L == 0 ? 52 : 40 };
        O(q, cor.ToString()); foreach (int w in NumOpt(r, cor, 3, 3)) O(q, w.ToString());
        return Fin(q, r);
    }

    static readonly string[] DN = { "NORTH", "EAST", "SOUTH", "WEST" };

    Q GenCompass(Rnd r, int L)
    {
        int start = r.Next(4), cur = start, nt = L == 0 ? 1 : L == 1 ? 2 : 4;
        var parts = new List<string>();
        for (int i = 0; i < nt; i++)
        {
            int k = L == 0 ? (r.Next(2) == 0 ? 1 : 3) : r.Next(1, 4);
            cur = (cur + k) % 4;
            parts.Add(k == 1 ? "turns RIGHT" : k == 3 ? "turns LEFT" : "spins AROUND");
        }
        string an = Pk(r, new[] { "bunny", "puppy", "kitten", "duckling", "panda" });
        var q = new Q { prompt = "A " + an + " starts facing " + DN[start] + ". It " + string.Join(", then ", parts) + ". Which way is it facing now?" };
        O(q, DN[cur]); for (int i = 0; i < 4; i++) if (i != cur) O(q, DN[i]);
        q.draw = v =>
        {
            Img(v, Vector2.zero, V(130f, 130f), UIKit.Ring(65, 3), ChalkWhite);
            string[] ab = { "N", "E", "S", "W" }; Vector2[] ps = { V(0f, 84f), V(88f, 0f), V(0f, -84f), V(-88f, 0f) };
            for (int i = 0; i < 4; i++) Tx(v, ab[i], 30f, ps[i], V(40f, 36f), ChalkYellow);
            Arrow(v, Vector2.zero, start * 90f, 70f, ChalkPink, 10f);
        };
        return Fin(q, r, start + ":" + string.Join("", parts));
    }

    Q GenStroop(Rnd r, int L)
    {
        int w = r.Next(6), ink = (w + 1 + r.Next(5)) % 6, nopt = L == 0 ? 3 : L == 1 ? 4 : 6, cor; bool byInk = true;
        var q = new Q();
        if (L == 0)
        {
            cor = w; q.prompt = "What color is the balloon?";
            q.draw = v =>
            {
                Img(v, V(0f, 8f), V(110f, 130f), UIKit.Rounded(55), Pal[w]);
                Img(v, V(-22f, 40f), V(22f, 30f), UIKit.Rounded(11), new Color(1f, 1f, 1f, 0.45f));
                Img(v, V(0f, -78f), V(4f, 28f), UIKit.Rounded(2), ChalkWhite);
            };
        }
        else
        {
            byInk = L == 1 || r.Next(2) == 0; cor = byInk ? ink : w;
            q.prompt = byInk ? "Pick the COLOR OF THE INK (ignore what the word says)!" : "Pick the color the WORD SAYS (ignore the ink)!";
            q.draw = v =>
            {
                if (L == 2) Tx(v, byInk ? "RULE: INK" : "RULE: WORD", 26f, V(0f, 70f), V(300f, 40f), ChalkYellow);
                Tx(v, PalName[w].ToUpper(), 80f, V(0f, L == 2 ? 0f : 10f), V(560f, 110f), Pal[ink]);
            };
        }
        var others = Enumerable.Range(0, 6).Where(i => i != cor).ToList(); Shf(r, others);
        Action<int> add = i => O(q, L == 0 ? PalName[i] : "", L == 0 ? null : SwIcon(Pal[i]));
        add(cor); foreach (int i in others.Take(nopt - 1)) add(i);
        return Fin(q, r, w + ":" + ink);
    }

    // ===================================================================== new Q generators
    Q GenSeries(Rnd r, int L)
    {
        var s = new List<int>(); int shown, kind = L == 0 ? 0 : L == 1 ? r.Next(2) : r.Next(3);
        if (L == 0) { int a = r.Next(1, 10), d = r.Next(2, 6); for (int i = 0; i < 5; i++) s.Add(a + d * i); }
        else if (L == 1 && kind == 0) { int a = r.Next(1, 5), m = r.Next(2, 4); int c = a; for (int i = 0; i < 5; i++) { s.Add(c); c *= m; } }
        else if (L == 1) { int a = r.Next(1, 8), x = r.Next(1, 6), y = r.Next(1, 6); if (x == y) y++; int c = a; for (int i = 0; i < 6; i++) { s.Add(c); c += i % 2 == 0 ? x : y; } }
        else if (kind == 0) { int c = r.Next(1, 8), d = r.Next(1, 4); for (int i = 0; i < 6; i++) { s.Add(c); c += d * (i + 1); } }
        else if (kind == 1) { int a = r.Next(1, 5), b = r.Next(2, 7); s.Add(a); s.Add(b); for (int i = 2; i < 6; i++) s.Add(s[i - 1] + s[i - 2]); }
        else { int c = r.Next(2, 6); for (int i = 0; i < 6; i++) { s.Add(c); c = i % 2 == 0 ? c * 2 : c - 1; } }
        shown = s.Count - 1; int cor = s[shown];
        var q = new Q { prompt = "Which number comes next in the series?", head = string.Join("    ", s.Take(shown)) + "    <color=" + HexYellow + ">?</color>", headSize = 56 };
        O(q, cor.ToString()); foreach (int w in NumOpt(r, cor, 3, Mathf.Max(3, cor / 4))) O(q, w.ToString());
        return Fin(q, r);
    }

    static readonly string[] DirN = { "North", "North-East", "East", "South-East", "South", "South-West", "West", "North-West" };

    Q GenSpin(Rnd r, int L)
    {
        int cur = r.Next(8), cnt = L == 2 ? 5 : 4; var seq = new List<int>(); int sg = r.Next(2) == 0 ? 1 : -1;
        for (int i = 0; i <= cnt; i++)
        {
            seq.Add(((cur % 8) + 8) % 8);
            int step = L == 0 ? 2 : L == 1 ? 1 : (i % 2 == 0 ? 2 : -1);
            cur += (L == 2 ? 1 : sg) * step;
        }
        int cor = seq[cnt];
        var q = new Q { prompt = "The arrow keeps turning. Which way does it point next?" };
        O(q, DirN[cor], ArrIcon(cor * 45f));
        var o = Enumerable.Range(0, 8).Where(i => i != cor).ToList(); Shf(r, o);
        foreach (int i in o.Take(3)) O(q, DirN[i], ArrIcon(i * 45f));
        q.draw = v =>
        {
            for (int i = 0; i < cnt; i++) { Vector2 p = V((i - cnt / 2f) * 112f, 0f); Box(v, p, V(92f, 92f), CellC, 16, true); Arrow(v, p, seq[i] * 45f, 52f, Pal[i % 6], 9f); }
            Vector2 qp = V((cnt - cnt / 2f) * 112f, 0f); Box(v, qp, V(92f, 92f), CellC, 16, true); Tx(v, "?", 54f, qp, V(60f, 60f), ChalkYellow);
        };
        return Fin(q, r, string.Join("", seq.Take(cnt)));
    }

    Q GenCount(Rnd r, int L)
    {
        int types = L == 0 ? 2 : L == 1 ? 4 : 8, total = L == 0 ? 12 : L == 1 ? 18 : 26, cor = r.Next(3 + L, 7 + L * 2);
        var ids = Enumerable.Range(0, 24).ToList(); Shf(r, ids); int tg = ids[0];
        var pool = ids.Skip(1).ToList();
        if (L == 2) pool = pool.OrderByDescending(x => (x % 6 == tg % 6 ? 1 : 0) + ((x / 6) % 4 == (tg / 6) % 4 ? 1 : 0)).ToList();
        pool = pool.Take(types - 1).ToList();
        var toks = new List<int>(); for (int i = 0; i < cor; i++) toks.Add(tg);
        while (toks.Count < total) toks.Add(Pk(r, pool));
        Shf(r, toks);
        var cells = Enumerable.Range(0, 40).ToList(); Shf(r, cells); var pos = new List<Vector2>();
        for (int i = 0; i < total; i++) pos.Add(V((cells[i] % 10 - 4.5f) * 72f + r.Next(-9, 10), (1.5f - cells[i] / 10) * 40f + r.Next(-4, 5)));
        var q = new Q { prompt = "Count the " + TokName(tg).ToUpper() + "S!" };
        O(q, cor.ToString()); foreach (int w in NumOpt(r, cor, 3, 2)) O(q, w.ToString());
        q.draw = v => { for (int i = 0; i < total; i++) Tok(v, toks[i], pos[i], 32f); };
        return Fin(q, r, string.Join(",", toks));
    }

    static readonly string[] PA = { "Red", "Red", "Blue", "Red", "Blue", "Yellow", "Green" };
    static readonly string[] PB = { "Yellow", "Blue", "Yellow", "White", "White", "White", "White" };
    static readonly string[] PR = { "Orange", "Purple", "Green", "Pink", "Sky", "Cream", "Mint" };
    static Color PC(string n)
    {
        switch (n)
        {
            case "Red": return new Color(0.95f, 0.35f, 0.35f); case "Yellow": return new Color(1f, 0.9f, 0.35f);
            case "Blue": return new Color(0.35f, 0.55f, 1f); case "White": return new Color(0.97f, 0.97f, 0.97f);
            case "Green": return new Color(0.4f, 0.85f, 0.4f); case "Orange": return new Color(1f, 0.6f, 0.25f);
            case "Purple": return new Color(0.65f, 0.4f, 0.9f); case "Pink": return new Color(1f, 0.65f, 0.75f);
            case "Sky": return new Color(0.65f, 0.85f, 1f); case "Cream": return new Color(1f, 0.97f, 0.7f);
            default: return new Color(0.7f, 1f, 0.75f);
        }
    }

    Q GenPaint(Rnd r, int L)
    {
        int lo = L == 0 ? 0 : L == 1 ? 3 : 0, hi = L == 0 ? 3 : 7, c = r.Next(lo, hi);
        var q = new Q();
        var others = Enumerable.Range(0, 7).Where(i => i != c && (L == 2 || (i >= lo && i < hi) || L == 1)).ToList(); Shf(r, others);
        if (L < 2)
        {
            q.prompt = "Mix the two paints. What color do you get?";
            O(q, PR[c], SwIcon(PC(PR[c]))); foreach (int i in others.Take(3)) O(q, PR[i], SwIcon(PC(PR[i])));
            q.draw = v =>
            {
                Box(v, V(-190f, 0f), V(90f, 90f), PC(PA[c]), 45, true); Tx(v, PA[c], 22f, V(-190f, -64f), V(120f, 30f), ChalkWhite);
                Box(v, V(-40f, 0f), V(90f, 90f), PC(PB[c]), 45, true); Tx(v, PB[c], 22f, V(-40f, -64f), V(120f, 30f), ChalkWhite);
                Tx(v, "+", 54f, V(-115f, 0f), V(50f, 60f), ChalkYellow); Tx(v, "=", 54f, V(35f, 0f), V(50f, 60f), ChalkYellow);
                Box(v, V(120f, 0f), V(90f, 90f), CellC, 45, true); Tx(v, "?", 56f, V(120f, 0f), V(60f, 60f), ChalkYellow);
            };
        }
        else
        {
            q.prompt = "Which two paints mix to make this color?";
            Action<int> add = i => O(q, PA[i] + " + " + PB[i], v => { Box(v, V(-9f, 0f), V(26f, 26f), PC(PA[i]), 13); Box(v, V(9f, 0f), V(26f, 26f), PC(PB[i]), 13); });
            add(c); foreach (int i in others.Take(3)) add(i);
            q.draw = v => { Box(v, Vector2.zero, V(130f, 130f), PC(PR[c]), 65, true); };
        }
        return Fin(q, r, c.ToString());
    }

    Q GenClock(Rnd r, int L)
    {
        int h = r.Next(1, 13), m = L == 0 ? 0 : L == 1 ? 15 * r.Next(4) : 5 * r.Next(1, 12);
        Func<int, int, string> f = (hh, mm) => hh + ":" + mm.ToString("00");
        var set = new List<string> { f(h, m) };
        int[] ms = L == 0 ? new[] { 0 } : L == 1 ? new[] { 0, 15, 30, 45 } : new[] { 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55 };
        for (int t = 0; t < 60 && set.Count < 4; t++)
        {
            int hh = r.Next(3) == 0 ? (m / 5 == 0 ? 12 : m / 5) : (r.Next(2) == 0 ? (h % 12) + 1 : r.Next(1, 13));
            int mm = L == 0 ? 0 : (r.Next(3) == 0 && h % 12 * 5 % 60 != m && L == 2 ? (h % 12) * 5 : Pk(r, ms));
            string s = f(hh, mm); if (!set.Contains(s)) set.Add(s);
        }
        var q = new Q { prompt = "What time does the clock show?" };
        foreach (string s in set) O(q, s);
        q.draw = v =>
        {
            Box(v, Vector2.zero, V(158f, 158f), UIKit.Cream, 79);
            Img(v, Vector2.zero, V(158f, 158f), UIKit.Ring(79, 5), UIKit.Dark);
            for (int i = 1; i <= 12; i++)
            {
                if (L == 2 && i % 3 != 0) { float aa = i * 30f * Mathf.Deg2Rad; Box(v, V(Mathf.Sin(aa) * 64f, Mathf.Cos(aa) * 64f), V(4f, 4f), Ink, 2); continue; }
                float a = i * 30f * Mathf.Deg2Rad; Tx(v, i.ToString(), 20f, V(Mathf.Sin(a) * 58f, Mathf.Cos(a) * 58f), V(30f, 26f), Ink);
            }
            RectTransform hr = Box(v, Vector2.zero, V(8f, 40f), Ink, 4, false, -((h % 12) * 30f + m * 0.5f)); hr.pivot = V(0.5f, 0f);
            RectTransform mn = Box(v, Vector2.zero, V(5f, 58f), new Color(0.85f, 0.25f, 0.35f), 3, false, -(m * 6f)); mn.pivot = V(0.5f, 0f);
            Box(v, Vector2.zero, V(12f, 12f), UIKit.Dark, 6);
        };
        return Fin(q, r, f(h, m));
    }

    static readonly string[] SI = { "Pen", "Apple", "Book", "Toy", "Cake", "Hat", "Ball", "Juice", "Kite", "Cup" };

    Q GenShop(Rnd r, int L)
    {
        int n = L == 0 ? 2 : 3; var it = Enumerable.Range(0, SI.Length).ToList(); Shf(r, it);
        var pr = new List<int>(); var qt = new List<int>(); int tot = 0;
        for (int i = 0; i < n; i++) { int p = L == 0 ? r.Next(1, 7) : L == 1 ? r.Next(2, 10) : r.Next(3, 9); int c = L == 2 ? r.Next(1, 4) : 1; pr.Add(p); qt.Add(c); tot += p * c; }
        int cor = L == 2 ? 50 - tot : tot;
        var q = new Q { prompt = L == 2 ? "You pay with a $50 note. How much change do you get?" : "How much do you pay in total?" };
        O(q, "$" + cor); foreach (int w in NumOpt(r, cor, 3, 4)) O(q, "$" + w);
        q.draw = v =>
        {
            for (int i = 0; i < n; i++)
            {
                Vector2 p = V((i - (n - 1) / 2f) * 250f, 0f);
                Box(v, p, V(220f, 110f), Pal[i % 6], 14, true);
                Tx(v, (qt[i] > 1 ? qt[i] + " x " : "") + SI[it[i]], 30f, p + V(0f, 20f), V(200f, 40f), UIKit.Dark);
                Tx(v, "$" + pr[i], 40f, p + V(0f, -22f), V(120f, 46f), UIKit.Dark);
            }
        };
        return Fin(q, r, string.Join(",", pr) + string.Join(",", qt));
    }

    static readonly string[] W4 = { "MOON", "STAR", "TREE", "FISH", "BIRD", "CAKE", "RAIN", "WIND", "SHIP", "FROG", "LAMP", "BOOK", "RING", "SAND", "GOLD" };
    static readonly string[] W5 = { "APPLE", "PLANT", "CHAIR", "CLOUD", "RIVER", "TIGER", "BREAD", "CLOCK", "MOUSE", "LIGHT", "OCEAN", "PIANO", "QUEEN", "STONE" };
    static readonly string[] W7 = { "GARDEN", "PURPLE", "ORANGE", "WINDOW", "ROCKET", "PENCIL", "BUTTER", "PLANET", "BRIDGE", "JUNGLE", "DRAGON", "CASTLE", "PIRATE", "RABBIT", "MONITOR", "KITCHEN", "DOLPHIN" };

    Q GenWord(Rnd r, int L)
    {
        string[] pool = L == 0 ? W4 : L == 1 ? W5 : W7; string w = Pk(r, pool); string sc;
        do { var ch = w.ToCharArray().ToList(); Shf(r, ch); sc = new string(ch.ToArray()); } while (sc == w);
        var q = new Q { prompt = "Unscramble the letters. Which word is hiding?" };
        O(q, w); var o = pool.Where(x => x != w && x.Length == w.Length).ToList(); Shf(r, o);
        foreach (string s in o.Take(3)) O(q, s);
        q.draw = v => { for (int i = 0; i < sc.Length; i++) { Vector2 p = V((i - (sc.Length - 1) / 2f) * 72f, 0f); Box(v, p, V(62f, 70f), Pal[i % 6], 12, true); Tx(v, sc[i].ToString(), 44f, p, V(50f, 60f), UIKit.Dark); } };
        return Fin(q, r, sc);
    }

    static readonly string[] HN = { "Ann", "Ben", "Cy", "Dee", "Eli", "Fay", "Gus" };

    Q GenHeight(Rnd r, int L)
    {
        int n = L + 3; var nm = Enumerable.Range(0, HN.Length).ToList(); Shf(r, nm); var ord = nm.Take(n).ToList();   // tallest -> shortest
        var st = new List<string>();
        for (int i = 0; i < n - 1; i++) st.Add(r.Next(2) == 0 ? HN[ord[i]] + " is taller than " + HN[ord[i + 1]] : HN[ord[i + 1]] + " is shorter than " + HN[ord[i]]);
        Shf(r, st);
        int cor = L == 0 ? ord[0] : L == 1 ? ord[n - 1] : ord[2];
        var q = new Q { prompt = L == 0 ? "Who is the TALLEST?" : L == 1 ? "Who is the SHORTEST?" : "Who stands in the MIDDLE?", head = string.Join("\n", st), headSize = n == 5 ? 34 : 40 };
        O(q, HN[cor]); foreach (int i in ord) if (i != cor) O(q, HN[i]);
        return Fin(q, r, string.Join("|", st));
    }

    Q GenSudoku(Rnd r, int L)
    {
        int n = L + 3; var rows = Enumerable.Range(0, n).ToList(); var cols = Enumerable.Range(0, n).ToList(); var sym = Enumerable.Range(0, 6).ToList();
        Shf(r, rows); Shf(r, cols); Shf(r, sym);
        var g = new int[n, n]; for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) g[i, j] = sym[(rows[i] + cols[j]) % n];
        int hr = r.Next(n), hc = r.Next(n); var blank = new HashSet<int>();
        if (L == 1) { var c = new List<int>(); for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) if (i != hr && j != hc) c.Add(i * n + j); Shf(r, c); blank.Add(c[0]); blank.Add(c[1]); }
        if (L == 2) for (int j = 0; j < n; j++) if (j != hc) blank.Add(hr * n + j);
        int cor = g[hr, hc];
        var q = new Q { prompt = "Every row and column holds each color once. What color goes in the ? square?" };
        Action<int> add = s => O(q, PalName[s], SwIcon(Pal[s]));
        add(cor); for (int i = 0; i < n; i++) if (sym[i] != cor) add(sym[i]);
        q.draw = v =>
        {
            float cs = n == 5 ? 44f : n == 4 ? 50f : 56f;
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
                {
                    Vector2 p = V((j - (n - 1) / 2f) * (cs + 4f), ((n - 1) / 2f - i) * (cs + 4f));
                    if (i == hr && j == hc) { Box(v, p, V(cs, cs), CellC, 8, true); Tx(v, "?", cs * 0.8f, p, V(cs, cs), ChalkYellow); }
                    else if (blank.Contains(i * n + j)) Box(v, p, V(cs, cs), new Color(0.1f, 0.2f, 0.14f), 8, true);
                    else Box(v, p, V(cs, cs), Pal[g[i, j]], 8);
                }
        };
        string gs = ""; for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) gs += g[i, j];
        return Fin(q, r, gs + hr + hc);
    }

    // ===================================================================== interactive games
    // ---- Garden Pipes (old game, now a real generated pipe grid) ----
    GD GenPipes(Rnd r, int L)
    {
        int cols = L + 4, rows = L == 0 ? 3 : 4; var d = new GD { n = cols, k = rows };
        int x = 0, y = r.Next(rows); var path = new List<int> { y * cols };
        var vis = new HashSet<int> { y * cols };
        while (x < cols - 1)
        {
            var mv = new List<int> { 1, 1 }; if (y > 0 && !vis.Contains((y - 1) * cols + x)) mv.Add(0); if (y < rows - 1 && !vis.Contains((y + 1) * cols + x)) mv.Add(2);
            int m = Pk(r, mv); if (m == 1) x++; else y += m - 1;
            path.Add(y * cols + x); vis.Add(y * cols + x);
        }
        // need masks (bit0 up, bit1 right, bit2 down, bit3 left) for path cells; l = need per cell (0 for decoys), l2 = current mask
        var need = new int[cols * rows];
        for (int i = 0; i < path.Count; i++)
        {
            int c = path[i], cx = c % cols, cy = c / cols, msk = 0;
            if (i == 0) msk |= 8; else msk |= DirBit(cx, cy, path[i - 1] % cols, path[i - 1] / cols);
            if (i == path.Count - 1) msk |= 2; else msk |= DirBit(cx, cy, path[i + 1] % cols, path[i + 1] / cols);
            need[c] = msk;
        }
        var cur = new int[cols * rows]; bool solved;
        do
        {
            solved = true;
            for (int c = 0; c < cur.Length; c++)
            {
                int bas = need[c] != 0 ? need[c] : Pk(r, new[] { 5, 10, 3, 6, 12, 9 });
                int rot = r.Next(4); for (int k = 0; k < rot; k++) bas = RotMask(bas); cur[c] = bas;
                if (need[c] != 0 && cur[c] != need[c]) solved = false;
            }
        } while (solved);
        d.l = need.ToList(); d.l2 = cur.ToList(); d.sig = string.Join("", need) + "/" + string.Join("", cur);
        return d;
    }
    static int DirBit(int x, int y, int tx, int ty) { return ty < y ? 1 : tx > x ? 2 : ty > y ? 4 : 8; }
    static int RotMask(int m) { return ((m << 1) | (m >> 3)) & 15; }

    void BuildPipes(GD d, int L)
    {
        promptLabel.text = "Click the pipes to rotate them so water flows from the tap to the flower!";
        RectTransform a = Arena(); int cols = d.n, rows = d.k; float cs = L == 2 ? 74f : 80f;
        var cur = d.l2.ToArray(); var arms = new Image[cur.Length][]; var bgs = new Image[cur.Length];
        Vector2[] dir = { V(0f, 1f), V(1f, 0f), V(0f, -1f), V(-1f, 0f) };
        Func<int, Vector2> P = c => V((c % cols - (cols - 1) / 2f) * (cs + 6f), ((rows - 1) / 2f - c / cols) * (cs + 6f) - 6f);
        for (int c = 0; c < cur.Length; c++)
        {
            int cc = c; Vector2 p = P(c); RectTransform b = Box(a, p, V(cs, cs), CellC, 12, true, 0f, true); bgs[c] = b.GetComponent<Image>();
            arms[c] = new Image[4];
            for (int k = 0; k < 4; k++) arms[c][k] = Box(b, dir[k] * cs * 0.25f, V(k % 2 == 0 ? cs * 0.2f : cs * 0.5f, k % 2 == 0 ? cs * 0.5f : cs * 0.2f), ChalkCyan, 4).GetComponent<Image>();
            Box(b, Vector2.zero, V(cs * 0.3f, cs * 0.3f), ChalkCyan, (int)(cs * 0.15f));
            Click(b, () => { cur[cc] = RotMask(cur[cc]); Draw(cc); Check(); });
        }
        // source / flower: first and last path cells in the first and last column
        int sc = -1, fc = -1; for (int c = 0; c < d.l.Count; c++) if (d.l[c] != 0) { if (c % cols == 0 && (d.l[c] & 8) != 0) sc = c; if (c % cols == cols - 1 && (d.l[c] & 2) != 0) fc = c; }
        Tx(a, "TAP", 26f, P(sc) + V(-(cs / 2f + 36f), 0f), V(70f, 40f), ChalkCyan);
        Tx(a, "FLOWER", 24f, P(fc) + V(cs / 2f + 46f, 0f), V(86f, 40f), ChalkPink);
        Draw0();
        void Draw(int c) { for (int k = 0; k < 4; k++) arms[c][k].gameObject.SetActive((cur[c] >> k & 1) == 1); }
        void Draw0() { for (int c = 0; c < cur.Length; c++) Draw(c); }
        void Check()
        {
            for (int c = 0; c < cur.Length; c++) if (d.l[c] != 0 && cur[c] != d.l[c]) return;
            for (int c = 0; c < cur.Length; c++) if (d.l[c] != 0) { foreach (var im in arms[c]) im.color = ChalkGreen; bgs[c].color = new Color(0.2f, 0.4f, 0.28f); }
            OnLevelPassed("WATER FLOWS! FLOWER IS SAVED!");
        }
    }

    // ---- Lights Out ----
    GD GenLights(Rnd r, int L)
    {
        var d = new GD { n = L + 3 }; var g = new int[d.n * d.n]; var cells = Enumerable.Range(0, g.Length).ToList(); Shf(r, cells);
        for (int i = 0; i < (L == 0 ? 2 : L == 1 ? 4 : 6); i++) Press(g, d.n, cells[i]);
        if (g.All(x => x == 0)) Press(g, d.n, cells[0]);
        d.l = g.ToList(); d.sig = d.n + string.Join("", g); return d;
    }
    static void Press(int[] g, int n, int i) { int x = i % n, y = i / n; Tg(g, n, x, y); Tg(g, n, x + 1, y); Tg(g, n, x - 1, y); Tg(g, n, x, y + 1); Tg(g, n, x, y - 1); }
    static void Tg(int[] g, int n, int x, int y) { if (x < 0 || y < 0 || x >= n || y >= n) return; g[y * n + x] ^= 1; }

    void BuildLights(GD d)
    {
        promptLabel.text = "Turn OFF every light! Tapping a light also flips the lights next to it.";
        RectTransform a = Arena(); int n = d.n; float cs = n == 3 ? 92f : n == 4 ? 82f : 70f; var g = d.l.ToArray(); var ims = new Image[g.Length];
        Action refresh = () =>
        {
            for (int i = 0; i < g.Length; i++) ims[i].color = g[i] == 1 ? ChalkYellow : CellC;
            if (g.All(x => x == 0)) OnLevelPassed("LIGHTS OUT!");
        };
        for (int i = 0; i < g.Length; i++)
        {
            int idx = i; RectTransform b = Box(a, V((i % n - (n - 1) / 2f) * (cs + 8f), ((n - 1) / 2f - i / n) * (cs + 8f) - 6f), V(cs, cs), CellC, 16, true, 0f, true);
            ims[i] = b.GetComponent<Image>(); Click(b, () => { Press(g, n, idx); refresh(); });
        }
        refresh(); if (levelResolved) return;
        feedbackText.text = "";
    }

    // ---- Slide puzzle ----
    static List<int> Neigh(int i, int n) { var l = new List<int>(); int x = i % n, y = i / n; if (x > 0) l.Add(i - 1); if (x < n - 1) l.Add(i + 1); if (y > 0) l.Add(i - n); if (y < n - 1) l.Add(i + n); return l; }

    GD GenSlide(Rnd r, int L)
    {
        var d = new GD { n = 3 }; var g = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 0 }; int blank = 8, prev = -1, moves = L == 0 ? 6 : L == 1 ? 22 : 44;
        for (int i = 0; i < moves + 1; i++)
        {
            var nb = Neigh(blank, 3).Where(x => x != prev).ToList(); int m = Pk(r, nb);
            g[blank] = g[m]; g[m] = 0; prev = blank; blank = m;
            if (i >= moves - 1 && !(blank == 8 && g[0] == 1 && g[1] == 2 && g[2] == 3 && g[3] == 4 && g[4] == 5 && g[5] == 6 && g[6] == 7 && g[7] == 8)) break;
        }
        d.l = g.ToList(); d.sig = string.Join("", g); return d;
    }

    void BuildSlide(GD d)
    {
        promptLabel.text = "Slide the tiles into order 1 to 8 with the empty spot bottom-right. Click a tile next to the gap!";
        RectTransform a = Arena(); var g = d.l.ToArray(); float cs = 100f; var rts = new RectTransform[9];
        Func<int, Vector2> P = c => V((c % 3 - 1) * (cs + 8f), (1 - c / 3) * (cs + 8f) - 4f);
        for (int c = 0; c < 9; c++)
        {
            int v = g[c]; if (v == 0) continue;
            RectTransform t = Box(a, P(c), V(cs, cs), Pal[(v - 1) % 6], 16, true, 0f, true); rts[v] = t;
            Tx(t, v.ToString(), 52f, Vector2.zero, V(cs - 20f, cs - 20f), UIKit.Dark);
            int val = v;
            Click(t, () =>
            {
                int cell = Array.IndexOf(g, val), bl = Array.IndexOf(g, 0);
                if (!Neigh(bl, 3).Contains(cell)) return;
                g[bl] = val; g[cell] = 0; rts[val].anchoredPosition = P(bl);
                bool ok = true; for (int i = 0; i < 8; i++) if (g[i] != i + 1) ok = false;
                if (ok) OnLevelPassed("PUZZLE SOLVED!");
            });
        }
    }

    // ---- Tower Stack (Hanoi) ----
    void BuildHanoi(int L)
    {
        int discs = L + 2; promptLabel.text = "Move the whole tower to the RIGHT peg. One disc at a time, never a big disc on a small one!";
        RectTransform a = Arena(); var pegs = new List<int>[3]; for (int i = 0; i < 3; i++) pegs[i] = new List<int>();
        for (int i = discs; i >= 1; i--) pegs[0].Add(i);
        var dr = new RectTransform[discs + 1]; int sel = -1, moves = 0;
        Box(a, V(0f, -150f), V(740f, 18f), WoodHighlight, 6, true);
        for (int p = 0; p < 3; p++) Box(a, V((p - 1) * 250f, -30f), V(12f, 230f), WoodFrame, 5);
        for (int s = 1; s <= discs; s++) dr[s] = Box(a, Vector2.zero, V(60f + s * 34f, 30f), Pal[(s - 1) % 6], 12, true);
        Action layout = () =>
        {
            for (int p = 0; p < 3; p++)
                for (int j = 0; j < pegs[p].Count; j++)
                {
                    bool lift = sel == p && j == pegs[p].Count - 1;
                    dr[pegs[p][j]].anchoredPosition = V((p - 1) * 250f, lift ? 110f : -128f + j * 34f);
                }
        };
        for (int p = 0; p < 3; p++)
        {
            int pp = p; RectTransform hit = Img(a, V((p - 1) * 250f, -20f), V(230f, 270f), null, new Color(0f, 0f, 0f, 0f), 0f, true);
            hit.SetAsFirstSibling();
            Click(hit, () =>
            {
                if (sel < 0) { if (pegs[pp].Count > 0) sel = pp; }
                else if (sel == pp) sel = -1;
                else
                {
                    int top = pegs[sel][pegs[sel].Count - 1];
                    if (pegs[pp].Count > 0 && pegs[pp][pegs[pp].Count - 1] < top) Hint("A big disc can't go on a small one!");
                    else { pegs[sel].RemoveAt(pegs[sel].Count - 1); pegs[pp].Add(top); sel = -1; moves++; Hint("MOVES: " + moves); }
                }
                layout();
                if (pegs[2].Count == discs) OnLevelPassed("TOWER BUILT IN " + moves + " MOVES!");
            });
        }
        layout();
    }

    // ---- Tile Sum ----
    GD GenTileSum(Rnd r, int L)
    {
        var d = new GD { n = L == 0 ? 6 : L == 1 ? 8 : 10 }; int k = L == 0 ? 2 : 3, hi = L == 0 ? 9 : L == 1 ? 20 : 40, tg = 0;
        for (int i = 0; i < k; i++) { int v = r.Next(1, hi + 1); d.l.Add(v); tg += v; }
        while (d.l.Count < d.n) d.l.Add(r.Next(1, hi + 1));
        Shf(r, d.l); d.k = tg; d.sig = tg + ":" + string.Join(",", d.l); return d;
    }

    void BuildTileSum(GD d)
    {
        promptLabel.text = "Pick tiles that add up exactly to the target number!";
        RectTransform a = Arena(); int n = d.n, per = n <= 6 ? 3 : 5, sum = 0; var sel = new bool[n]; var ims = new Image[n];
        Tx(a, "TARGET: " + d.k, 54f, V(0f, 160f), V(500f, 64f), ChalkYellow);
        TextMeshProUGUI st = Tx(a, "SUM: 0", 34f, V(0f, 108f), V(300f, 44f), ChalkCyan);
        for (int i = 0; i < n; i++)
        {
            int idx = i; int row = i / per, inRow = Mathf.Min(per, n - row * per);
            RectTransform b = Box(a, V((i % per - (inRow - 1) / 2f) * 120f, 30f - row * 82f), V(100f, 70f), CellC, 14, true, 0f, true);
            ims[i] = b.GetComponent<Image>(); TextMeshProUGUI tt = Tx(b, d.l[i].ToString(), 40f, Vector2.zero, V(84f, 56f), ChalkWhite);
            Click(b, () =>
            {
                sel[idx] = !sel[idx]; sum += sel[idx] ? d.l[idx] : -d.l[idx];
                ims[idx].color = sel[idx] ? ChalkCyan : CellC; tt.color = sel[idx] ? UIKit.Dark : ChalkWhite;
                st.text = "SUM: " + sum;
                if (sum == d.k) OnLevelPassed("PERFECT SUM!"); else if (sum > d.k) Hint("Too big! Un-pick a tile.");
            });
        }
        RectTransform rs = Box(a, V(0f, -170f), V(160f, 46f), ChalkOrange, 12, true, 0f, true); Tx(rs, "RESET", 26f, Vector2.zero, V(140f, 40f), UIKit.Dark);
        Click(rs, () => { for (int i = 0; i < n; i++) { sel[i] = false; ims[i].color = CellC; foreach (var t in ims[i].GetComponentsInChildren<TextMeshProUGUI>()) t.color = ChalkWhite; } sum = 0; st.text = "SUM: 0"; });
    }

    // ---- Copy the grid ----
    GD GenCopyGrid(Rnd r, int L)
    {
        var d = new GD { n = L + 3 }; var c = Enumerable.Range(0, d.n * d.n).ToList(); Shf(r, c);
        d.l = c.Take(L == 0 ? 3 : L == 1 ? 5 : 8).OrderBy(x => x).ToList(); d.sig = d.n + ":" + string.Join(",", d.l); return d;
    }

    void BuildCopyGrid(GD d)
    {
        promptLabel.text = "Memorize the glowing squares, then rebuild the pattern from memory!";
        RectTransform a = Arena(); int n = d.n; float cs = n == 3 ? 86f : n == 4 ? 76f : 66f; var on = new bool[n * n]; var ims = new Image[n * n]; bool input = false; int tok = levelToken;
        foreach (int c in d.l) on[c] = true;
        for (int i = 0; i < n * n; i++)
        {
            int idx = i; RectTransform b = Box(a, V((i % n - (n - 1) / 2f) * (cs + 8f), ((n - 1) / 2f - i / n) * (cs + 8f) + 14f), V(cs, cs), on[i] ? ChalkCyan : CellC, 14, true, 0f, true);
            ims[i] = b.GetComponent<Image>();
            Click(b, () => { if (!input) return; on[idx] = !on[idx]; ims[idx].color = on[idx] ? ChalkGreen : CellC; });
        }
        RectTransform ok = Box(a, V(0f, -178f), V(150f, 42f), ChalkGreen, 12, true, 0f, true); Tx(ok, "DONE", 26f, Vector2.zero, V(130f, 36f), UIKit.Dark);
        Click(ok, () =>
        {
            if (!input) return;
            for (int i = 0; i < on.Length; i++) if (on[i] != d.l.Contains(i)) { OnLevelFailed("That wasn't the pattern!"); return; }
            OnLevelPassed("PERFECT COPY!");
        });
        StartCoroutine(CopyFlow());
        IEnumerator CopyFlow()
        {
            for (int s = 3; s >= 1; s--) { Hint("MEMORIZE!  " + s); yield return new WaitForSecondsRealtime(1f); if (tok != levelToken || levelResolved) yield break; }
            for (int i = 0; i < on.Length; i++) { on[i] = false; ims[i].color = CellC; }
            input = true; Hint("NOW REBUILD IT, THEN PRESS DONE");
        }
    }

    // ---- Spot the change ----
    GD GenSpot(Rnd r, int L)
    {
        var d = new GD { n = L + 4 }; int T = L == 0 ? 3 : L == 1 ? 5 : 8; var ids = Enumerable.Range(0, 24).ToList(); Shf(r, ids); var pool = ids.Take(T).ToList();
        for (int i = 0; i < d.n * d.n; i++) d.l.Add(Pk(r, pool));
        d.m = r.Next(d.n * d.n); int t = d.l[d.m];
        d.k = L == 0 ? Pk(r, ids.Where(x => x != t).ToList()) : L == 1 ? t - t % 6 + (t % 6 + 1 + r.Next(5)) % 6 : (t + 6 * (1 + r.Next(3))) % 24;
        d.sig = string.Join(",", d.l) + d.m + ":" + d.k; return d;
    }

    void BuildSpot(GD d)
    {
        promptLabel.text = "The right picture has ONE change. Click it! (two wrong clicks and you're out)";
        RectTransform a = Arena(); int n = d.n, wrong = 0; float cs = n == 4 ? 62f : n == 5 ? 54f : 46f;
        Tx(a, "ORIGINAL", 24f, V(-200f, 170f), V(200f, 30f), ChalkCyan); Tx(a, "FIND THE CHANGE", 24f, V(200f, 170f), V(240f, 30f), ChalkPink);
        for (int side = 0; side < 2; side++)
            for (int i = 0; i < n * n; i++)
            {
                int idx = i; Vector2 p = V(side * 400f - 200f + (i % n - (n - 1) / 2f) * (cs + 4f), ((n - 1) / 2f - i / n) * (cs + 4f) - 10f);
                RectTransform b = Box(a, p, V(cs, cs), CellC, 8, true, 0f, side == 1);
                Tok(b, side == 1 && i == d.m ? d.k : d.l[i], Vector2.zero, cs * 0.66f);
                if (side == 1) Click(b, () =>
                {
                    if (idx == d.m) { b.GetComponent<Image>().color = ChalkGreen; OnLevelPassed("SHARP EYES!"); }
                    else { wrong++; b.GetComponent<Image>().color = ChalkPink; if (wrong >= 2) OnLevelFailed("Two misses!"); else Hint("Not that one! One miss left."); }
                });
            }
    }

    // ---- Star catcher (reaction) ----
    void BuildStars(int L)
    {
        int goal = L == 0 ? 5 : L == 1 ? 8 : 11, allowed = L == 0 ? 3 : L == 1 ? 2 : 1; float life = L == 0 ? 2.4f : L == 1 ? 1.6f : 1.15f, decoy = L == 0 ? 0f : L == 1 ? 0.4f : 0.65f;
        promptLabel.text = "Catch the YELLOW stars before they vanish! Avoid the pink squares!";
        RectTransform a = Arena(); int tok = levelToken; TextMeshProUGUI cnt = Tx(a, "CAUGHT 0/" + goal, 30f, V(-280f, 175f), V(220f, 36f), ChalkYellow);
        StartCoroutine(Run());
        IEnumerator Run()
        {
            int hit = 0, miss = 0; var rr = new Rnd(Guid.NewGuid().GetHashCode());
            yield return new WaitForSecondsRealtime(0.5f);
            while (hit < goal)
            {
                if (tok != levelToken || levelResolved) yield break;
                bool got = false, bad = false;
                RectTransform s = Box(a, V(rr.Next(-320, 321), rr.Next(-140, 120)), V(78f, 78f), ChalkYellow, 39, true, 0f, true);
                Tx(s, "*", 70f, V(0f, -8f), V(70f, 70f), UIKit.Dark);
                Click(s, () => got = true);
                RectTransform dc = null;
                if (rr.NextDouble() < decoy)
                {
                    dc = Box(a, V(rr.Next(-320, 321), rr.Next(-140, 120)), V(66f, 66f), ChalkPink, 8, true, 0f, true);
                    Tx(dc, "X", 48f, Vector2.zero, V(50f, 50f), UIKit.Dark); Click(dc, () => bad = true);
                }
                float t = 0f;
                while (t < life && !got && !bad) { if (tok != levelToken || levelResolved) yield break; t += PauseMenu.Dt; s.localScale = Vector3.one * (1f - 0.35f * t / life); yield return null; }
                WorldUtil.Kill(s.gameObject); if (dc != null) WorldUtil.Kill(dc.gameObject);
                if (got) { hit++; cnt.text = "CAUGHT " + hit + "/" + goal; } else { miss++; Hint("MISSES: " + miss + "/" + allowed); if (miss > allowed) { OnLevelFailed("Too many misses!"); yield break; } }
                yield return new WaitForSecondsRealtime(0.22f);
            }
            OnLevelPassed("STAR CATCHER!");
        }
    }

    // ---- Number dash ----
    GD GenDash(Rnd r, int L)
    {
        var d = new GD { n = L == 0 ? 6 : L == 1 ? 8 : 10, k = L == 2 && r.Next(2) == 0 ? 1 : 0 };
        var vals = L == 0 ? Enumerable.Range(1, 6).ToList() : Enumerable.Range(L == 1 ? 1 : 10, L == 1 ? 60 : 190).OrderBy(x => r.Next()).Take(d.n).ToList();
        Shf(r, vals); var cells = Enumerable.Range(0, 18).ToList(); Shf(r, cells);
        d.l = vals; d.l2 = cells.Take(d.n).ToList(); d.sig = d.k + string.Join(",", vals); return d;
    }

    void BuildDash(GD d)
    {
        var order = d.l.OrderBy(x => x).ToList(); if (d.k == 1) order.Reverse();
        promptLabel.text = d.k == 1 ? "Tap the numbers from LARGEST to SMALLEST!" : "Tap the numbers from SMALLEST to LARGEST!";
        RectTransform a = Arena(); int next = 0, jit = d.sig.GetHashCode();
        for (int i = 0; i < d.n; i++)
        {
            int c = d.l2[i], val = d.l[i]; var jr = new Rnd(jit + i);
            Vector2 p = V((c % 6 - 2.5f) * 124f + jr.Next(-14, 15), (1 - c / 6) * 112f + jr.Next(-12, 13));
            RectTransform b = Box(a, p, V(82f, 66f), Pal[i % 6], 16, true, 0f, true); Tx(b, val.ToString(), 38f, Vector2.zero, V(70f, 56f), UIKit.Dark);
            Click(b, () =>
            {
                if (val == order[next]) { b.GetComponent<Image>().color = new Color(0.25f, 0.45f, 0.3f); b.GetComponent<Button>().interactable = false; next++; if (next == d.n) OnLevelPassed("LIGHTNING FAST!"); }
                else OnLevelFailed("Wrong number!");
            });
        }
    }

    // ---- Arrow rush ----
    GD GenRush(Rnd r, int L)
    {
        var d = new GD { n = L == 0 ? 5 : L == 1 ? 8 : 10 };
        for (int i = 0; i < d.n; i++) { d.l.Add(r.Next(4)); d.l2.Add(L == 0 ? 0 : L == 1 ? 1 : r.Next(2)); }
        d.sig = string.Join("", d.l) + string.Join("", d.l2); return d;
    }

    void BuildRush(GD d, int L)
    {
        promptLabel.text = L == 0 ? "Press the arrow key (or WASD) that matches each arrow!" : L == 1 ? "PINK arrows: press the OPPOSITE direction!" : "GREEN = same direction, PINK = opposite direction!";
        RectTransform a = Arena(); int idx = 0, tok = levelToken;
        RectTransform stage = Box(a, V(0f, 20f), V(260f, 260f), CellC, 30, true);
        TextMeshProUGUI prog = Tx(a, "0/" + d.n, 30f, V(0f, -150f), V(200f, 36f), ChalkYellow);
        Tx(a, L == 1 ? "PINK = OPPOSITE" : L == 2 ? "GREEN = SAME     PINK = OPPOSITE" : "GREEN = SAME", 22f, V(0f, -182f), V(500f, 28f), ChalkWhite);
        Action show = () =>
        {
            for (int i = stage.childCount - 1; i >= 0; i--) WorldUtil.Kill(stage.GetChild(i).gameObject);
            Arrow(stage, V(0f, 6f), d.l[idx] * 90f, 150f, d.l2[idx] == 0 ? ChalkGreen : ChalkPink, 26f);
        };
        show(); StartCoroutine(Keys());
        IEnumerator Keys()
        {
            while (!levelResolved && tok == levelToken)
            {
                Keyboard kb = Keyboard.current; int pressed = -1;
                if (kb != null)
                {
                    if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) pressed = 0;
                    else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) pressed = 1;
                    else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) pressed = 2;
                    else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) pressed = 3;
                }
                if (pressed >= 0)
                {
                    int want = d.l2[idx] == 0 ? d.l[idx] : (d.l[idx] + 2) % 4;
                    if (pressed != want) { OnLevelFailed("Wrong way!"); yield break; }
                    idx++; prog.text = idx + "/" + d.n;
                    if (idx >= d.n) { OnLevelPassed("REFLEXES OF STEEL!"); yield break; }
                    show();
                }
                yield return null;
            }
        }
    }

    // ---- Card pairs ----
    GD GenPairs(Rnd r, int L)
    {
        var d = new GD { n = L == 0 ? 3 : L == 1 ? 6 : 8 }; var ids = Enumerable.Range(0, 24).ToList(); Shf(r, ids);
        var cards = new List<int>(); for (int i = 0; i < d.n; i++) { cards.Add(ids[i]); cards.Add(ids[i]); }
        Shf(r, cards); d.l = cards; d.sig = string.Join(",", cards); return d;
    }

    void BuildPairs(GD d)
    {
        promptLabel.text = "Flip two cards at a time and find all the matching pairs!";
        RectTransform a = Arena(); int total = d.l.Count, cols = d.n == 3 ? 3 : 4, rows = total / cols, first = -1, matched = 0, tok = levelToken; float cs = d.n == 8 ? 78f : 88f; bool lockIn = false;
        var done = new bool[total]; var fronts = new GameObject[total];
        for (int i = 0; i < total; i++)
        {
            int idx = i; RectTransform b = Box(a, V((i % cols - (cols - 1) / 2f) * (cs + 10f), ((rows - 1) / 2f - i / cols) * (cs + 10f) - 6f), V(cs, cs), new Color(0.3f, 0.5f, 0.42f), 16, true, 0f, true);
            Tx(b, "?", 50f, Vector2.zero, V(cs * 0.6f, cs * 0.7f), ChalkWhite);
            RectTransform f = Box(b, Vector2.zero, V(cs, cs), UIKit.Cream, 16, true); Tok(f, d.l[i], Vector2.zero, cs * 0.55f); fronts[i] = f.gameObject; f.gameObject.SetActive(false);
            Click(b, () =>
            {
                if (lockIn || done[idx] || fronts[idx].activeSelf) return;
                fronts[idx].SetActive(true);
                if (first < 0) { first = idx; return; }
                int a1 = first, b1 = idx; first = -1;
                if (d.l[a1] == d.l[b1]) { done[a1] = done[b1] = true; matched++; Hint("MATCH! " + matched + "/" + d.n); if (matched == d.n) OnLevelPassed("ALL PAIRS FOUND!"); }
                else StartCoroutine(Flip());
                IEnumerator Flip()
                {
                    lockIn = true; yield return new WaitForSecondsRealtime(0.7f);
                    if (tok == levelToken) { fronts[a1].SetActive(false); fronts[b1].SetActive(false); }
                    lockIn = false;
                }
            });
        }
    }

    // ---- Trail memory ----
    GD GenTrail(Rnd r, int L)
    {
        var d = new GD { n = L + 3 }; int len = L == 0 ? 3 : L == 1 ? 5 : 7;
        for (int t = 0; t < 100; t++)
        {
            d.l = new List<int> { r.Next(d.n * d.n) };
            while (d.l.Count < len)
            {
                var nb = Neigh(d.l[d.l.Count - 1], d.n).Where(x => !d.l.Contains(x)).ToList(); if (nb.Count == 0) break; d.l.Add(Pk(r, nb));
            }
            if (d.l.Count == len) break;
        }
        d.sig = d.n + ":" + string.Join(",", d.l); return d;
    }

    void BuildTrail(GD d)
    {
        promptLabel.text = "Watch the trail light up, then tap the same squares in the same order!";
        RectTransform a = Arena(); int n = d.n, step = 0, tok = levelToken; float cs = n == 3 ? 92f : n == 4 ? 82f : 70f; bool input = false; var ims = new Image[n * n];
        for (int i = 0; i < n * n; i++)
        {
            int idx = i; RectTransform b = Box(a, V((i % n - (n - 1) / 2f) * (cs + 8f), ((n - 1) / 2f - i / n) * (cs + 8f) - 6f), V(cs, cs), CellC, 16, true, 0f, true); ims[i] = b.GetComponent<Image>();
            Click(b, () =>
            {
                if (!input) return;
                if (idx == d.l[step]) { ims[idx].color = ChalkGreen; step++; Hint("STEP " + step + "/" + d.l.Count); if (step >= d.l.Count) OnLevelPassed("TRAIL REMEMBERED!"); }
                else { ims[idx].color = ChalkPink; OnLevelFailed("Wrong square!"); }
            });
        }
        StartCoroutine(Play());
        IEnumerator Play()
        {
            Hint("WATCH CLOSELY..."); yield return new WaitForSecondsRealtime(0.7f);
            foreach (int c in d.l)
            {
                if (tok != levelToken || levelResolved) yield break;
                ims[c].color = ChalkCyan; yield return new WaitForSecondsRealtime(0.55f); ims[c].color = CellC; yield return new WaitForSecondsRealtime(0.2f);
            }
            input = true; Hint("YOUR TURN! FOLLOW THE TRAIL");
        }
    }
}
