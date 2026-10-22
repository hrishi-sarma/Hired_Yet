using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Palette shared with the interview room UI (orange / yellow / blue paper cards, dark ink outlines).</summary>
public static class Toon
{
    public static readonly Color Ink = new Color(0.15f, 0.12f, 0.2f, 1f);
    public static readonly Color Cream = new Color(1f, 0.98f, 0.9f, 1f);
    public static readonly Color KeyBack = new Color(0.82f, 0.77f, 0.6f, 1f);
    public static readonly Color Orange = new Color(1f, 0.70f, 0.28f, 1f);     // #FFB347
    public static readonly Color Yellow = new Color(1f, 0.89f, 0.48f, 1f);     // #FFE27A
    public static readonly Color Blue = new Color(0.62f, 0.88f, 1f, 1f);       // #9EE0FF
    public static readonly Color Pink = new Color(1f, 0.70f, 0.78f, 1f);
    public static readonly Color Mint = new Color(0.55f, 0.94f, 0.69f, 1f);
    public static readonly Color Red = new Color(1f, 0.42f, 0.4f, 1f);

    public static Image Card(RectTransform rt, Color fill, int radius = 14, float outline = 4f, bool shadow = true)
    {
        return UIKit.Boxy(UIKit.AddImage(rt, UIKit.Rounded(radius), fill), outline, shadow);
    }

    public static float OutBack(float t)
    {
        t = Mathf.Clamp01(t) - 1f;
        return 1f + t * t * (2.7f * t + 1.7f);
    }

    /// <summary>Chunky keycap (back plate + front cap). Returns the front cap; the label is its first child.</summary>
    public static RectTransform KeyCap(RectTransform parent, string name, string letter, float size, TMP_FontAsset font, Color cap, out RectTransform back)
    {
        back = UIKit.NewRect(name, parent);
        UIKit.Place(back, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(size, size));
        Card(back, KeyBack, 10, 3f, false);
        RectTransform front = UIKit.NewRect("Cap", back);
        UIKit.Place(front, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        Card(front, cap, 10, 3f, false);
        TextMeshProUGUI t = UIKit.AddText(front, "Letter", letter, size * 0.56f, Ink, TextAlignmentOptions.Center, font, FontStyles.Bold);
        UIKit.Stretch(t.rectTransform);
        return front;
    }
}

/// <summary>Procedurally drawn cartoon icons (white fill, dark outline). Tint with Image.color.</summary>
public static class ToonIcons
{
    const int N = 96;
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    static float Circle(float x, float y, float cx, float cy, float r) { float dx = x - cx, dy = y - cy; return Mathf.Sqrt(dx * dx + dy * dy) - r; }
    static float Box(float x, float y, float cx, float cy, float hw, float hh, float r)
    {
        float qx = Mathf.Abs(x - cx) - hw + r, qy = Mathf.Abs(y - cy) - hh + r;
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }
    static float Cap(float x, float y, float ax, float ay, float bx, float by, float r)
    {
        float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
        float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
        float dx = pax - bax * h, dy = pay - bay * h;
        return Mathf.Sqrt(dx * dx + dy * dy) - r;
    }
    static float Tri(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d = Mathf.Min(Cap(x, y, ax, ay, bx, by, 0f), Mathf.Min(Cap(x, y, bx, by, cx, cy, 0f), Cap(x, y, cx, cy, ax, ay, 0f)));
        float s1 = (bx - ax) * (y - ay) - (by - ay) * (x - ax);
        float s2 = (cx - bx) * (y - by) - (cy - by) * (x - bx);
        float s3 = (ax - cx) * (y - cy) - (ay - cy) * (x - cx);
        bool inside = (s1 >= 0f && s2 >= 0f && s3 >= 0f) || (s1 <= 0f && s2 <= 0f && s3 <= 0f);
        return inside ? -d : d;
    }
    static float Sub(float d, float hole) { return Mathf.Max(d, -hole); }

    static float Shape(string id, float x, float y)
    {
        switch (id)
        {
            case "paw":
                return Mathf.Min(Mathf.Min(Circle(x, y, .5f, .33f, .21f), Circle(x, y, .22f, .6f, .1f)),
                    Mathf.Min(Circle(x, y, .4f, .75f, .1f), Mathf.Min(Circle(x, y, .6f, .75f, .1f), Circle(x, y, .78f, .6f, .1f))));
            case "crumb":
                return Mathf.Min(Mathf.Min(Circle(x, y, .3f, .32f, .15f), Circle(x, y, .66f, .27f, .12f)),
                    Mathf.Min(Circle(x, y, .5f, .62f, .17f), Circle(x, y, .8f, .62f, .09f)));
            case "hand":
                return Mathf.Min(Mathf.Min(Box(x, y, .5f, .33f, .24f, .2f, .1f), Cap(x, y, .3f, .45f, .3f, .78f, .07f)),
                    Mathf.Min(Mathf.Min(Cap(x, y, .45f, .5f, .45f, .88f, .07f), Cap(x, y, .6f, .5f, .6f, .85f, .07f)),
                    Mathf.Min(Cap(x, y, .74f, .45f, .74f, .72f, .07f), Cap(x, y, .28f, .35f, .1f, .5f, .07f))));
            case "door":
                return Sub(Sub(Box(x, y, .5f, .5f, .27f, .4f, .05f), Circle(x, y, .62f, .48f, .05f)), Box(x, y, .5f, .68f, .1f, .08f, .02f));
            case "paper":
                return Sub(Sub(Sub(Sub(Box(x, y, .5f, .5f, .28f, .38f, .04f), Box(x, y, .5f, .68f, .16f, .025f, .01f)),
                    Box(x, y, .5f, .55f, .16f, .025f, .01f)), Box(x, y, .5f, .42f, .16f, .025f, .01f)), Box(x, y, .44f, .29f, .1f, .025f, .01f));
            case "bolt":
                return Mathf.Min(Tri(x, y, .58f, .95f, .2f, .48f, .52f, .52f), Tri(x, y, .42f, .05f, .8f, .55f, .48f, .48f)) - .02f;
            case "drop":
                return Mathf.Min(Circle(x, y, .5f, .34f, .24f), Tri(x, y, .5f, .92f, .28f, .44f, .72f, .44f));
            case "box":
                return Sub(Box(x, y, .5f, .42f, .34f, .3f, .03f), Box(x, y, .5f, .62f, .34f, .018f, .005f));
            case "speech":
                return Mathf.Min(Box(x, y, .5f, .55f, .38f, .27f, .1f), Tri(x, y, .28f, .36f, .28f, .08f, .52f, .32f));
            case "arrow":
                return Mathf.Min(Box(x, y, .5f, .3f, .12f, .22f, .04f), Tri(x, y, .5f, .98f, .1f, .5f, .9f, .5f)) - .02f;
            case "question":
                return Sub(Mathf.Min(Mathf.Min(Circle(x, y, .5f, .66f, .26f), Box(x, y, .5f, .36f, .09f, .1f, .03f)), Circle(x, y, .5f, .1f, .1f)), Circle(x, y, .5f, .66f, .11f));
            default: // star / sparkle
                return Mathf.Min(Mathf.Min(Tri(x, y, .5f, .95f, .4f, .5f, .6f, .5f), Tri(x, y, .5f, .05f, .4f, .5f, .6f, .5f)),
                    Mathf.Min(Tri(x, y, .05f, .5f, .5f, .4f, .5f, .6f), Tri(x, y, .95f, .5f, .5f, .4f, .5f, .6f))) - .04f;
        }
    }

    public static Sprite Get(string id)
    {
        Sprite s;
        if (cache.TryGetValue(id, out s) && s != null) return s;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[N * N];
        const float t = 0.05f, aa = 1.4f / N;
        Color ink = Toon.Ink;
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                float x = (i + .5f) / N, y = (j + .5f) / N;
                float d = Shape(id, x, y);
                float outer = Mathf.Clamp01(.5f - (d - t) / aa), fill = Mathf.Clamp01(.5f - d / aa);
                Color c = Color.Lerp(ink, Color.white, fill);
                c.a = outer;
                px[j * N + i] = c;
            }
        tex.SetPixels(px);
        tex.Apply(false, true);
        s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), 100f);
        cache[id] = s;
        return s;
    }

    /// <summary>Picks an icon from the words of a prompt.</summary>
    public static string ForText(string s)
    {
        s = s.ToLowerInvariant();
        if (Regex.IsMatch(s, @"\b(pat|pet|dog|cat|deer|animal|puppy)\b")) return "paw";
        if (Regex.IsMatch(s, @"crumb|feed|pigeon|bread|throw")) return "crumb";
        if (Regex.IsMatch(s, @"pickpocket|steal|wallet|\bid\b")) return "hand";
        if (Regex.IsMatch(s, @"interview|door|enter")) return "door";
        if (Regex.IsMatch(s, @"paper|document|sheet|catch")) return "paper";
        if (Regex.IsMatch(s, @"water|tub|plant")) return "drop";
        if (Regex.IsMatch(s, @"lift|box|grab|log|carry|deliver|set down|marker|couch|bag|grocer|pick up|take")) return "box";
        if (s.IndexOf('"') >= 0 || Regex.IsMatch(s, @"ask|talk|answer")) return "speech";
        if (Regex.IsMatch(s, @"tap|mash|press|hold")) return "bolt";
        return "star";
    }
}

/// <summary>A stack of cartoon prompt cards. Text format understood (all of the game's old prompt strings keep working):
/// "[ PRESS E ] Pat the dog", "HOLD E  [####------]", "TAP E  [##--]  3.2s", "[ Q ] Throw crumbs". Lines are separated with '\n'.
/// Key words become key caps, "[#---]" bars become a progress bar, a trailing time / distance / count becomes a pill.</summary>
public class PromptStack : MonoBehaviour
{
    const int MaxCards = 3;
    const float CardH = 64f, BarExtra = 16f, Gap = 10f;

    class Line { public string tag = "", key = "", label = "", pill = "", icon = "star"; public float bar = -1f; public string Sig() { return tag + "|" + key + "|" + label + "|" + icon + "|" + (pill.Length > 0) + "|" + (bar >= 0f); } }

    class Card
    {
        public RectTransform rt, badge, keyBack, keyFront, pillRt, barRt, barFill;
        public Image bg, icon, pill, fill;
        public TextMeshProUGUI label, pillText;
        public string sig = "", key = "";
        public float pop = 1f, fillShown, fillTarget, kick, phase, height = CardH, rot;
        public bool active;
    }

    static readonly Regex BarRx = new Regex(@"\[([#\-]{6,})\]");
    static readonly Regex TimeRx = new Regex(@"\s(\d+(?:\.\d+)?s)\s*$");
    static readonly Regex DistRx = new Regex(@"\s(\d+\s?m)\s*$");
    static readonly Regex CountRx = new Regex(@"\(?(\d+\s?/\s?\d+)(?: WATERED)?\)?");
    static readonly Regex KeyRx = new Regex(@"(?:\b(HOLD|TAP|PRESS|RELEASE|KEEP HOLDING|MASH)\s+)?\b([EQP])\b");
    static readonly Regex Spaces = new Regex(@"\s{2,}");

    readonly Card[] cards = new Card[MaxCards];
    TMP_FontAsset font;
    bool top;
    float baseY;
    string lastRaw = "";
    int shown;

    /// <summary>Creates a screen-space canvas holding the stack. top = hang from the top edge, otherwise sit above the bottom edge.</summary>
    public static PromptStack Create(string name, int sortingOrder, TMP_FontAsset font, bool top, float offsetY)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        PromptStack p = go.AddComponent<PromptStack>();
        p.font = font; p.top = top; p.baseY = offsetY;
        for (int i = 0; i < MaxCards; i++) p.cards[i] = p.BuildCard(i);
        return p;
    }

    public void Show(string text)
    {
        if (text == null) text = "";
        if (text == lastRaw) return;
        lastRaw = text;
        string[] parts = text.Split('\n');
        int n = 0;
        for (int i = 0; i < parts.Length && n < MaxCards; i++)
        {
            if (parts[i].Trim().Length == 0) continue;
            Apply(cards[n], Parse(parts[i]));
            n++;
        }
        for (int i = n; i < MaxCards; i++) { cards[i].active = false; cards[i].rt.gameObject.SetActive(false); cards[i].sig = ""; }
        shown = n;
    }

    public void Hide() { Show(""); }
    /// <summary>Whole stack opacity (0..1), e.g. to fade a prompt away.</summary>
    public void SetAlpha(float a) { var g = GetComponent<CanvasGroup>(); if (g != null) g.alpha = Mathf.Clamp01(a); }

    // ---------- parsing ----------
    static Line Parse(string raw)
    {
        Line l = new Line();
        string s = raw.Trim();
        Match m = BarRx.Match(s);
        if (m.Success)
        {
            string b = m.Groups[1].Value;
            int hashes = 0;
            for (int i = 0; i < b.Length; i++) if (b[i] == '#') hashes++;
            l.bar = hashes / (float)b.Length;
            s = s.Remove(m.Index, m.Length);
        }
        m = TimeRx.Match(s);
        if (m.Success) { l.pill = m.Groups[1].Value; s = s.Remove(m.Index); }
        if (l.pill.Length == 0)
        {
            m = DistRx.Match(s);
            if (m.Success) { l.pill = m.Groups[1].Value; s = s.Remove(m.Index); }
        }
        if (l.pill.Length == 0)
        {
            m = CountRx.Match(s);
            if (m.Success) { l.pill = m.Groups[1].Value.Replace(" ", ""); s = s.Remove(m.Index, m.Length); }
        }
        m = KeyRx.Match(s);
        if (m.Success) { l.key = m.Groups[2].Value; l.tag = m.Groups[1].Value; s = s.Remove(m.Index, m.Length); }
        s = s.Replace("[", "").Replace("]", "").Replace("(", "").Replace(")", "");
        s = Spaces.Replace(s, " ").Trim(' ', '-', ':');
        if (s.StartsWith("TO ", System.StringComparison.OrdinalIgnoreCase)) s = s.Substring(3);
        if (s.IndexOf('"') < 0) s = s.ToUpperInvariant();
        if (s.Length == 0)
        {
            if (l.tag.Length > 0) { s = l.tag; l.tag = ""; }
            else if (l.pill.Length > 0) { s = l.pill; l.pill = ""; }
        }
        l.label = s;
        l.icon = ToonIcons.ForText(s + " " + l.tag);
        return l;
    }

    static bool hasKeyTag(Line l) { return l.key.Length > 0 && l.tag.Length > 0 && l.tag != "PRESS"; }

    static Color Accent(Line l)
    {
        switch (l.tag)
        {
            case "HOLD": case "KEEP HOLDING": return Toon.Blue;
            case "TAP": case "MASH": return Toon.Pink;
            case "RELEASE": return Toon.Mint;
            case "PRESS": return Toon.Orange;
        }
        return l.key.Length > 0 ? Toon.Orange : Toon.Yellow;
    }

    // ---------- building ----------
    Card BuildCard(int index)
    {
        Card c = new Card();
        Vector2 a = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        c.rt = UIKit.NewRect("Card" + index, transform);
        UIKit.Place(c.rt, a, a, Vector2.zero, new Vector2(300f, CardH));
        c.bg = Toon.Card(c.rt, Toon.Orange);
        c.phase = index * 1.7f;
        c.rot = index % 2 == 0 ? -1f : 0.8f;

        c.badge = UIKit.NewRect("Badge", c.rt);
        UIKit.Place(c.badge, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(46f, 46f));
        Image bi = UIKit.AddImage(c.badge, UIKit.Rounded(23), Toon.Cream);
        UIKit.Boxy(bi, 3f, false);
        RectTransform ic = UIKit.NewRect("Icon", c.badge);
        UIKit.Place(ic, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
        c.icon = UIKit.AddImage(ic, ToonIcons.Get("star"), Color.white);

        c.keyFront = Toon.KeyCap(c.rt, "Key", "E", 46f, font, Toon.Cream, out c.keyBack);
        c.label = UIKit.AddText(c.rt, "Label", "", 26f, Toon.Ink, TextAlignmentOptions.MidlineLeft, font, FontStyles.Bold, 1f);

        c.pillRt = UIKit.NewRect("Pill", c.rt);
        UIKit.Place(c.pillRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(86f, 36f));
        c.pill = Toon.Card(c.pillRt, Toon.Ink, 12, 2f, false);
        c.pillText = UIKit.AddText(c.pillRt, "Text", "", 21f, Toon.Cream, TextAlignmentOptions.Center, font, FontStyles.Bold);
        UIKit.Stretch(c.pillText.rectTransform);

        c.barRt = UIKit.NewRect("Bar", c.rt);
        UIKit.AddImage(c.barRt, UIKit.Rounded(6), new Color(Toon.Ink.r, Toon.Ink.g, Toon.Ink.b, 0.55f));
        c.barFill = UIKit.NewRect("Fill", c.barRt);
        c.fill = UIKit.AddImage(c.barFill, UIKit.Rounded(6), Toon.Yellow);
        c.rt.gameObject.SetActive(false);
        return c;
    }

    void Apply(Card c, Line l)
    {
        c.fillTarget = Mathf.Clamp01(l.bar);
        string sig = l.Sig();
        bool structural = sig != c.sig;
        if (!c.active) { c.active = true; c.rt.gameObject.SetActive(true); structural = true; c.fillShown = c.fillTarget; }
        if (c.pill.gameObject.activeSelf != (l.pill.Length > 0)) c.pill.gameObject.SetActive(l.pill.Length > 0);
        if (l.pill.Length > 0) c.pillText.text = l.pill;
        if (!structural) return;

        c.sig = sig;
        c.pop = 0f;
        c.key = l.key;
        bool hasKey = l.key.Length > 0, hasBar = l.bar >= 0f, hasPill = l.pill.Length > 0;
        c.height = CardH + (hasBar ? BarExtra : 0f);
        c.bg.color = Accent(l);
        c.icon.sprite = ToonIcons.Get(l.icon);
        string shownLabel = (hasKeyTag(l) ? "<size=62%>" + l.tag + "</size>  " : "") + l.label;
        c.label.text = shownLabel;

        float x = 12f + 46f + 10f;
        c.keyBack.gameObject.SetActive(hasKey);
        if (hasKey)
        {
            c.keyBack.anchoredPosition = new Vector2(x, hasBar ? BarExtra * 0.5f : 0f);
            c.keyFront.GetChild(0).GetComponent<TextMeshProUGUI>().text = l.key;
            x += 46f + 12f;
        }
        float lw = c.label.GetPreferredValues(shownLabel).x;
        float w = x + lw + 22f + (hasPill ? 96f : 0f);
        w = Mathf.Max(w, 250f);
        c.rt.sizeDelta = new Vector2(w, c.height);
        float rowY = hasBar ? BarExtra * 0.5f : 0f;
        UIKit.Place(c.badge, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, rowY), new Vector2(46f, 46f));
        UIKit.Place(c.label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, rowY), new Vector2(lw + 8f, 40f));
        UIKit.Place(c.pillRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, rowY), new Vector2(86f, 36f));

        c.barRt.gameObject.SetActive(hasBar);
        if (hasBar)
        {
            UIKit.Place(c.barRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(w - 28f, 12f));
        }
        c.barFill.anchorMin = Vector2.zero;
        c.barFill.anchorMax = new Vector2(c.fillShown, 1f);
        c.barFill.offsetMin = Vector2.zero;
        c.barFill.offsetMax = Vector2.zero;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
        Keyboard kb = Keyboard.current;
        float y = baseY;
        for (int i = 0; i < shown; i++)
        {
            Card c = cards[i];
            if (!c.active) continue;
            c.pop = Mathf.MoveTowards(c.pop, 1f, dt * 4.5f);
            float sc = Mathf.LerpUnclamped(0.6f, 1f, Toon.OutBack(c.pop));
            c.rt.localScale = new Vector3(sc, sc, 1f);
            c.rt.localRotation = Quaternion.Euler(0f, 0f, c.rot + Mathf.Sin(t * 1.6f + c.phase) * 0.5f);
            float bob = Mathf.Sin(t * 2.2f + c.phase) * 3f;
            c.rt.anchoredPosition = new Vector2(0f, top ? -(y) - bob : y + bob);
            y += c.height + Gap;

            if (c.fillShown != c.fillTarget && c.barRt.gameObject.activeSelf)
            {
                c.fillShown = Mathf.Lerp(c.fillShown, c.fillTarget, 1f - Mathf.Exp(-dt * 16f));
                c.barFill.anchorMax = new Vector2(Mathf.Clamp01(c.fillShown), 1f);
                c.fill.color = Color.Lerp(Toon.Yellow, Toon.Mint, Mathf.SmoothStep(0.6f, 1f, c.fillShown));
            }

            if (c.key.Length > 0)
            {
                bool held = false, hit = false;
                if (kb != null)
                {
                    if (c.key == "E") { held = kb.eKey.isPressed; hit = kb.eKey.wasPressedThisFrame; }
                    else if (c.key == "Q") { held = kb.qKey.isPressed; hit = kb.qKey.wasPressedThisFrame; }
                    else if (c.key == "P") { held = kb.pKey.isPressed; hit = kb.pKey.wasPressedThisFrame; }
                }
                if (hit) c.kick = 1f;
                c.kick = Mathf.MoveTowards(c.kick, 0f, dt * 5f);
                float idle = held ? 0f : Mathf.Max(0f, Mathf.Sin(t * 3f + c.phase)) * 2f;
                c.keyFront.anchoredPosition = new Vector2(0f, idle - (held ? 6f : 0f));
                float ks = 1f + c.kick * 0.18f;
                c.keyFront.localScale = new Vector3(ks, ks, 1f);
            }
        }
    }
}
