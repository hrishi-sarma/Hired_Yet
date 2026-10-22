using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Text;
using System.Text.RegularExpressions;

public static class PostItArt
{
    static float H(int x, int y) { return Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f); }

    public static Sprite Note(Color c)
    {
        const int S = 192;
        var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var px = new Color[S * S];
        float m = S * 0.04f, rr = S * 0.07f, ow = S * 0.03f, half = S * 0.5f - m;
        Color line = new Color(0.2f, 0.15f, 0.25f, 1f);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float ny = (y + 0.5f) / S;
                float qx = Mathf.Abs(x + 0.5f - S * 0.5f) - (half - rr), qy = Mathf.Abs(y + 0.5f - S * 0.5f) - (half - rr);
                float ax = Mathf.Max(qx, 0f), ay = Mathf.Max(qy, 0f);
                float sd = Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - rr;
                Color o = Color.clear;
                if (sd < 0f)
                {
                    if (-sd < ow) o = line;
                    else
                    {
                        float g = Mathf.Lerp(0.93f, 1.03f, ny) + (H(x, y) - 0.5f) * 0.04f;
                        if (ny > 0.86f) g += 0.035f;
                        if (Mathf.Abs(ny - 0.86f) < 0.006f) g -= 0.06f;
                        if (ny < 0.45f)
                        {
                            float dx = (x % 12) - 6f, dy = (y % 12) - 6f;
                            if (dx * dx + dy * dy < 4f) g -= 0.05f * (0.45f - ny) / 0.45f * 2.2f;
                        }
                        if (ny < 0.12f) g -= (0.12f - ny) * 0.7f;
                        o = new Color(Mathf.Clamp01(c.r * g), Mathf.Clamp01(c.g * g), Mathf.Clamp01(c.b * g), 1f);
                    }
                }
                px[y * S + x] = o;
            }
        t.SetPixels(px); t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp; t.Apply(false);
        return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite card;
    /// <summary>Shared sharp-cornered, outlined, glossy glass card (9-sliced). Fill is semi-transparent (alpha is baked into the texture,
    /// so the outline stays solid); tint it with Image.color. The colour argument of Note() is for the pause board only.</summary>
    public static Sprite Card()
    {
        if (card != null) return card;
        const int S = 128; const float ow = 5f, m = 1f, half = S * 0.5f - m;
        var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var px = new Color[S * S];
        Color line = new Color(0.2f, 0.15f, 0.25f, 1f);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float nx = (x + 0.5f) / S, ny = (y + 0.5f) / S;
                float sd = Mathf.Max(Mathf.Abs(x + 0.5f - S * 0.5f), Mathf.Abs(y + 0.5f - S * 0.5f)) - half;   // square corners
                float cover = Mathf.Clamp01(0.5f - sd);
                if (cover <= 0f) { px[y * S + x] = Color.clear; continue; }
                float inner = -sd - ow;                                    // distance inside the outline (px)
                float g = Mathf.Lerp(0.82f, 1f, ny), a = 0.74f;            // glass body: darker toward the bottom, semi-transparent
                // everything in the middle of the card is a SMOOTH gradient: this region is stretched by the 9-slice, so any hard
                // edge or fine pattern would show up as jagged, low-res steps and stripes on big cards
                float gl = Mathf.SmoothStep(0f, 1f, (ny - (0.5f - 0.25f * nx)) / 0.4f);   // soft gloss: brighter upper area
                g = Mathf.Min(1f, g + 0.12f * gl); a = Mathf.Lerp(a, 0.86f, gl);
                float u = nx + ny * 0.5f;                                  // soft diagonal sheen
                float sk = Mathf.Exp(-Mathf.Pow((u - 0.42f) / 0.07f, 2f)) * 0.7f + Mathf.Exp(-Mathf.Pow((u - 0.6f) / 0.03f, 2f)) * 0.4f;
                g = Mathf.Lerp(g, 1f, sk); a = Mathf.Lerp(a, 0.93f, sk);
                if (inner >= 0f && inner < 2.5f)                           // bevel: light top/left edge, dark bottom/right edge
                {
                    if (ny > 0.5f || nx < 0.5f) { g = 1f; a = 0.97f; } else { g *= 0.72f; a = 0.9f; }
                }
                Color fill = new Color(Mathf.Clamp01(g), Mathf.Clamp01(g), Mathf.Clamp01(g), a);
                Color o = Color.Lerp(line, fill, Mathf.Clamp01(inner + 0.5f));
                o.a *= cover;
                px[y * S + x] = o;
            }
        t.SetPixels(px); t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp; t.Apply(false);
        card = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
        return card;
    }

    static Sprite blob;
    public static Sprite Blob()
    {
        if (blob == null) { var t = DeskKit.Blob(); blob = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f); }
        return blob;
    }
}

public class NoteAnim : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Action onClick;
    public bool peel = true, hoverFx = true;
    public Image shadow;
    public Text label;
    public Vector2 basePos, size;
    public float baseRot;
    RectTransform rt;
    CanvasGroup cg;
    float hov, pl, dis, app = 1f, bounce;
    bool over, peeling, done, dismiss;
    Action afterPeel;

    bool Live { get { return !peeling && !done && !dismiss && app >= 1f; } }

    void Awake() { rt = (RectTransform)transform; cg = GetComponent<CanvasGroup>(); }

    public void OnPointerEnter(PointerEventData e) { over = true; }
    public void OnPointerExit(PointerEventData e) { over = false; }

    public void OnPointerClick(PointerEventData e)
    {
        if (!Live) return;
        if (peel) peeling = true;
        else { bounce = 1f; if (onClick != null) onClick(); }
    }

    public void PeelOff(Action a) { if (peeling || done) return; afterPeel = a; peeling = true; }
    public void Dismiss(bool d) { dismiss = d; }
    public void PopIn() { app = 0f; }

    public void Restore()
    {
        pl = 0f; peeling = false; done = false; dismiss = false; dis = 0f; app = 0f; afterPeel = null;
        if (label != null) label.gameObject.SetActive(true);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        hov = Mathf.MoveTowards(hov, (over && Live && hoverFx) ? 1f : 0f, dt * 7f);
        dis = Mathf.MoveTowards(dis, dismiss ? 1f : 0f, dt * 4f);
        app = Mathf.MoveTowards(app, 1f, dt * 3f);
        bounce = Mathf.MoveTowards(bounce, 0f, dt * 5f);
        if (peeling)
        {
            pl = Mathf.MoveTowards(pl, 1f, dt / 0.55f);
            if (pl >= 1f)
            {
                peeling = false; done = true;
                Action a = afterPeel != null ? afterPeel : onClick;
                afterPeel = null;
                if (a != null) a();
            }
        }
        float eh = hov * hov * (3f - 2f * hov);
        float sc = (1f + 0.13f * eh) * (0.85f + 0.15f * app) * (1f - 0.06f * Mathf.Sin(bounce * Mathf.PI));
        float sY = 1f, up = 16f * eh - 90f * dis, rot = baseRot * (1f - 0.7f * eh), alpha = app * (1f - dis);
        float shadowK = 1f;
        if (pl > 0f)
        {
            float e = pl < 0.5f ? 2f * pl * pl : 1f - Mathf.Pow(-2f * pl + 2f, 2f) * 0.5f;
            sY = Mathf.Lerp(1f, -0.85f, e); rot += 16f * e; up += 46f * e;
            alpha *= 1f - Mathf.Clamp01((pl - 0.7f) / 0.3f);
            shadowK = 1f - pl;
            if (label != null) label.gameObject.SetActive(pl < 0.5f);
        }
        rt.anchoredPosition = basePos + new Vector2(0f, up + size.y * 0.5f * (1f - sY) * sc);
        rt.localScale = new Vector3(sc, sc * sY, 1f);
        rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        cg.alpha = alpha;
        cg.blocksRaycasts = !done && !dismiss && !peeling && app > 0.5f;
        if (shadow != null)
        {
            var s = shadow.rectTransform;
            s.anchoredPosition = basePos + new Vector2(10f + 6f * eh, -size.y * 0.46f - 4f * eh - 90f * dis);
            s.localScale = Vector3.one * (1f + 0.12f * eh);
            var c = shadow.color; c.a = 0.3f * (1f - 0.35f * eh) * alpha * shadowK; shadow.color = c;
        }
    }
}

public class PostItMenu : MonoBehaviour
{
    static readonly Color Ink = new Color(0.2f, 0.15f, 0.25f),
        Mint = new Color(0.74f, 0.95f, 0.84f), Sky = new Color(0.72f, 0.88f, 1f), Lav = new Color(0.82f, 0.79f, 0.98f),
        Pink = new Color(1f, 0.78f, 0.86f), Lemon = new Color(1f, 0.96f, 0.72f);

    Font hand;
    RectTransform root;
    readonly List<MenuItemAnim> notes = new List<MenuItemAnim>();
    readonly List<GameObject> extras = new List<GameObject>();
    NoteAnim panel;
    NoteAnim panelBack;
    int historyPage;
    int reportPage;
    const int ReportLinesPerPage = 5;
    Action onStart;
    string credits;

    public void Init(Font h, Font l, Action start, string cred)
    {
        hand = h; onStart = start; credits = cred;
        AudioListener.volume = GameSettings.Master;
        var go = new GameObject("Post-it Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var cv = go.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 20;
        var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f); sc.matchWidthOrHeight = 0.5f;
        root = go.GetComponent<RectTransform>();

        // cartoon strip menu: left-anchored list inside a container that shrinks on small / narrow screens
        var lg = new GameObject("List", typeof(RectTransform)); listRoot = lg.GetComponent<RectTransform>(); listRoot.SetParent(root, false);
        listRoot.anchorMin = listRoot.anchorMax = new Vector2(0f, 0.5f); listRoot.pivot = new Vector2(0f, 0.5f); listRoot.sizeDelta = Vector2.zero; listRoot.anchoredPosition = Vector2.zero;
        string[] names = { "START", "HISTORY", "SETTINGS", "CREDITS", "QUIT" };
        if (!string.IsNullOrEmpty(Title)) notes.Add(MakeItem(Title, 54, new Vector2(ItemX - 8f, TitleY), 0, null, false, -2.5f));
        for (int i = 0; i < names.Length; i++)
        {
            int k = i;
            notes.Add(MakeItem(names[i], 40, new Vector2(ItemX, FirstY - i * Spacing), i + 1, () => Pressed(k), true, Tilt[i % Tilt.Length]));
        }
        Fit();
    }

    RectTransform listRoot;
    static readonly float[] Tilt = { -2f, 1.5f, -1.2f, 2f, -1.6f };
    float lastW, lastH;
    void Update() { if (root != null && (root.rect.width != lastW || root.rect.height != lastH)) Fit(); }

    // keeps the list fully visible on any aspect ratio (ultrawide: unchanged, 4:3 / portrait: scaled down)
    void Fit()
    {
        lastW = root.rect.width; lastH = root.rect.height;
        float need = ItemX + 560f, needH = 640f;
        float s = Mathf.Min(1f, lastW / need, lastH / needH);
        listRoot.localScale = Vector3.one * Mathf.Max(0.35f, s);
    }

    // layout in 1920x1080 reference units, measured from the centre-left anchor
    public static string Title = "Hired Yet?";          // set to "" to hide the heading
    const float ItemX = 110f, FirstY = 196f, Spacing = 98f, TitleY = 330f;
    static readonly Color Cream = new Color(1f, 0.98f, 0.90f), Orange = new Color(1f, 0.62f, 0.2f), HotYellow = new Color(1f, 0.85f, 0.3f);

    MenuItemAnim MakeItem(string text, int fs, Vector2 pos, int order, Action click, bool interactive, float rot)
    {
        float w = interactive ? 420f + ((order * 37) % 5) * 16f : 540f, h = interactive ? 78f : 104f;
        Vector2 size = new Vector2(w, h);

        var sg = new GameObject("Shadow", typeof(Image));
        var sr = sg.GetComponent<RectTransform>(); sr.SetParent(listRoot, false);
        sr.anchorMin = sr.anchorMax = new Vector2(0f, 0.5f); sr.pivot = new Vector2(0f, 0.5f); sr.sizeDelta = size;
        var si = sg.GetComponent<Image>(); si.sprite = PostItArt.Card(); si.type = Image.Type.Sliced; si.color = new Color(0f, 0f, 0f, 0.5f); si.raycastTarget = false;

        var go = new GameObject(text, typeof(Image), typeof(CanvasGroup), typeof(MenuItemAnim));
        var r = go.GetComponent<RectTransform>(); r.SetParent(listRoot, false);
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f); r.pivot = new Vector2(0f, 0.5f); r.sizeDelta = size;
        var img = go.GetComponent<Image>(); img.sprite = PostItArt.Card(); img.type = Image.Type.Sliced;
        img.color = interactive ? Cream : Orange; img.raycastTarget = interactive;

        var tg = new GameObject("Label", typeof(Text));
        var tr = tg.GetComponent<RectTransform>(); tr.SetParent(r, false);
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(16f, 4f); tr.offsetMax = new Vector2(-16f, -4f);
        var t = tg.GetComponent<Text>();
        t.font = hand; t.text = text; t.fontSize = fs; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
        t.color = interactive ? Ink : Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
        if (!interactive) { var ol = tg.AddComponent<Outline>(); ol.effectColor = Ink; ol.effectDistance = new Vector2(3f, -3f); }

        var n = go.GetComponent<MenuItemAnim>();
        n.label = t; n.strip = img; n.shadow = si; n.stripIdle = img.color; n.stripHot = HotYellow; n.baseRot = rot;
        n.basePos = pos; n.order = order; n.onClick = click; n.interactive = interactive; n.idle = t.color; n.hot = Ink;
        r.anchoredPosition = pos;
        return n;
    }

    public void Show() { foreach (var n in notes) n.Restore(); }

    public void HideAll() { foreach (var n in notes) n.Dismiss(true); }

    void Pressed(int k)
    {
        if (panel != null) return;      // a panel is already open (or peeling off): ignore stray clicks
        if (k == 0) { if (onStart != null) onStart(); HideAll(); }
        else if (k == 1) { historyPage = 0; OpenPanel("HISTORY", FillHistory, 780f, 840f); }
        else if (k == 2) OpenPanel("SETTINGS", FillSettings, 820f, 880f);
        else if (k == 3) OpenPanel("CREDITS", FillCredits, 760f, 680f);
        else
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }

    NoteAnim Make(string text, Color col, Vector2 pos, Vector2 size, float rot, int fs, Action click, bool peel, bool temp)
    {
        if (!string.IsNullOrEmpty(text) && text.Length * fs * 0.58f + 56f > size.x) size.x = text.Length * fs * 0.58f + 56f;
        var sg = new GameObject("Shadow", typeof(Image));
        var sr = sg.GetComponent<RectTransform>(); sr.SetParent(PR, false);
        sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f); sr.sizeDelta = new Vector2(size.x * 0.9f, size.y * 0.22f);
        var si = sg.GetComponent<Image>(); si.sprite = PostItArt.Blob(); si.color = new Color(0f, 0f, 0f, 0.3f); si.raycastTarget = false;

        var go = new GameObject("Note", typeof(Image), typeof(CanvasGroup), typeof(NoteAnim));
        var r = go.GetComponent<RectTransform>(); r.SetParent(PR, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = size;
        var cimg = go.GetComponent<Image>(); cimg.sprite = PostItArt.Card(); cimg.type = Image.Type.Sliced; cimg.color = col;

        var tg = new GameObject("Label", typeof(Text));
        var tr = tg.GetComponent<RectTransform>(); tr.SetParent(r, false);
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(18f, 18f); tr.offsetMax = new Vector2(-18f, -18f);
        var t = tg.GetComponent<Text>();
        t.font = hand; t.text = text; t.fontSize = fs; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter; t.color = Ink;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;

        var n = go.GetComponent<NoteAnim>();
        n.shadow = si; n.label = t; n.basePos = pos; n.size = size; n.baseRot = rot; n.onClick = click; n.peel = peel;
        r.anchoredPosition = pos;
        if (temp) { extras.Add(sg); extras.Add(go); }
        return n;
    }

    Text Txt(string s, int size, Vector2 pos, Vector2 sz)
    {
        var go = new GameObject("Text", typeof(Text));
        var r = go.GetComponent<RectTransform>(); r.SetParent(PR, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = sz; r.anchoredPosition = pos;
        var t = go.GetComponent<Text>();
        t.font = hand; t.text = s; t.fontSize = size; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter; t.color = Ink;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
        extras.Add(go);
        return t;
    }

    RectTransform pr;   // everything on a card lives under this: it is scaled up to fit the screen, so the cards are big and readable
    RectTransform PR { get { return pr != null ? pr : root; } }

    void OpenPanel(string title, Action fill, float w, float h)
    {
        foreach (var n in notes) n.Dismiss(true);
        var pg = new GameObject("Panel Root", typeof(RectTransform)); pr = pg.GetComponent<RectTransform>(); pr.SetParent(root, false);
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.sizeDelta = Vector2.zero; pr.anchoredPosition = Vector2.zero;
        float span = h + 110f;   // card + banner sticking out of the top + back note
        float k = Mathf.Clamp(0.95f * Mathf.Min(root.rect.height / span, root.rect.width / (w + 80f)), 0.55f, 1.7f);
        pr.localScale = Vector3.one * k;
        panel = Make("", Lemon, Vector2.zero, new Vector2(w, h), -1.5f, 40, null, false, true);
        panel.hoverFx = false; panel.PopIn();
        Banner(title, new Vector2(0f, h * 0.5f - 10f));
        fill();
        panelBack = Make("BACK", Mint, new Vector2(0f, -(h * 0.5f - 62f)), new Vector2(220f, 76f), 1.5f, 34, ClosePanel, false, true);
    }

    GameObject banner;
    // orange ribbon with the panel title, sitting on the top edge of the card
    void Banner(string title, Vector2 pos)
    {
        var go = new GameObject("Banner", typeof(Image));
        var r = go.GetComponent<RectTransform>(); r.SetParent(PR, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(Mathf.Max(360f, title.Length * 40f + 120f), 96f); r.anchoredPosition = pos;
        r.localRotation = Quaternion.Euler(0f, 0f, -2.5f);
        var im = go.GetComponent<Image>(); im.sprite = PostItArt.Card(); im.type = Image.Type.Sliced; im.color = Orange; im.raycastTarget = false;
        var tg = new GameObject("Label", typeof(Text), typeof(Outline));
        var tr = tg.GetComponent<RectTransform>(); tr.SetParent(r, false); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
        var t = tg.GetComponent<Text>(); t.font = hand; t.text = title; t.fontSize = 52; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        var ol = tg.GetComponent<Outline>(); ol.effectColor = Ink; ol.effectDistance = new Vector2(3f, -3f);
        extras.Add(go); banner = go;
    }

    // instant close (no peel animation)
    void ClosePanel()
    {
        if (panel == null) return;
        panel = null; panelBack = null; banner = null;
        foreach (var g in extras) if (g != null) WorldUtil.Kill(g);
        extras.Clear();
        if (pr != null) { WorldUtil.Kill(pr.gameObject); pr = null; }
        foreach (var n in notes) n.Restore();
    }

    // ------------------------------------------------------------------ settings (same structure as the pause menu:
    // master volume + fullscreen on the first screen, SOUND MIXER and GRAPHICS sub-screens with their own BACK)
    void FillSettings()
    {
        if (panelBack != null) panelBack.onClick = ClosePanel;
        Txt("MASTER VOLUME", 38, new Vector2(0f, 330f), new Vector2(600f, 56f));
        AudioRow(0, 262f, false);
        CheckRow("FULLSCREEN", Screen.fullScreen, 120f, v => { Screen.fullScreen = v; });
        Make("SOUND MIXER", Sky, new Vector2(0f, -20f), new Vector2(420f, 84f), 1f, 38, () => SubScreen(FillMixer), false, true);
        Make("GRAPHICS", Mint, new Vector2(0f, -140f), new Vector2(420f, 84f), -1f, 38, () => SubScreen(FillGraphics), false, true);
    }

    void SubScreen(Action fill)
    {
        if (panel == null) return;
        ClearHistoryContents();
        if (panelBack != null) panelBack.onClick = () => { if (panel == null) return; ClearHistoryContents(); FillSettings(); };
        fill();
    }

    void FillMixer()
    {
        Txt("SOUND MIXER", 36, new Vector2(0f, 345f), new Vector2(600f, 54f));
        for (int i = 1; i < GameSettings.AudioKeys.Length; i++) AudioRow(i, 280f - (i - 1) * 92f, true);
    }

    void FillGraphics()
    {
        Txt("EFFECTS", 36, new Vector2(0f, 345f), new Vector2(600f, 54f));
        for (int i = 0; i < GameSettings.GfxNames.Length; i++)
        {
            int k = i; float y = 250f - i * 118f;
            Text pct = Txt(Mathf.RoundToInt(GameSettings.GetAmt(k) * 100f) + "%", 30, new Vector2(285f, y), new Vector2(110f, 50f));
            Slider sl = MakeSlider(new Vector2(110f, y), 200f, GameSettings.GetAmt(k), v => { GameSettings.SetAmt(k, v); pct.text = Mathf.RoundToInt(v * 100f) + "%"; });
            sl.interactable = GameSettings.GetOn(k); SetSliderLook(sl, sl.interactable);
            CheckBox(new Vector2(-355f, y), GameSettings.GetOn(k), v => { GameSettings.SetOn(k, v); sl.interactable = v; SetSliderLook(sl, v); });
            Txt(GameSettings.GfxNames[i], 28, new Vector2(-175f, y), new Vector2(300f, 60f));
        }
    }

    // one audio row: [label] [slider] [percent]; master row has no label (the heading above it says what it is)
    void AudioRow(int i, float y, bool labelled)
    {
        float sx = labelled ? 85f : 0f, sw = labelled ? 200f : 320f;
        Text pct = Txt(Mathf.RoundToInt(GameSettings.GetAudio(i) * 100f) + "%", 32, new Vector2(sx + sw * 0.5f + 70f, y), new Vector2(120f, 50f));
        MakeSlider(new Vector2(sx, y), sw, GameSettings.GetAudio(i), v => { GameSettings.SetAudio(i, v); pct.text = Mathf.RoundToInt(v * 100f) + "%"; });
        if (labelled) Txt(GameSettings.AudioNames[i], 28, new Vector2(-165f, y), new Vector2(260f, 60f));
    }

    void CheckRow(string label, bool on, float y, Action<bool> changed)
    {
        Txt(label, 38, new Vector2(-60f, y), new Vector2(360f, 60f));
        CheckBox(new Vector2(200f, y), on, changed);
    }

    NoteAnim CheckBox(Vector2 pos, bool on, Action<bool> changed)
    {
        NoteAnim cb = Make(on ? "X" : "", on ? Mint : Pink, pos, new Vector2(70f, 70f), 0f, 44, null, false, true);
        var img = cb.GetComponent<Image>();
        cb.onClick = () => { on = !on; cb.label.text = on ? "X" : ""; img.color = on ? Mint : Pink; changed(on); };
        return cb;
    }

    Slider MakeSlider(Vector2 pos, float w, float value, Action<float> changed)
    {
        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(PR, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(w, 50f); rt.anchoredPosition = pos;
        extras.Add(go);
        RectTransform Child(string n, Transform p, bool image, out Image im)
        {
            var g = new GameObject(n, image ? new[] { typeof(RectTransform), typeof(Image) } : new[] { typeof(RectTransform) });
            var r = g.GetComponent<RectTransform>(); r.SetParent(p, false); im = image ? g.GetComponent<Image>() : null; return r;
        }
        Image bgI, fillI, hI, dummy;
        var bg = Child("Track", rt, true, out bgI);
        bg.anchorMin = new Vector2(0f, 0.5f); bg.anchorMax = new Vector2(1f, 0.5f); bg.sizeDelta = new Vector2(0f, 24f); bg.anchoredPosition = Vector2.zero;
        bgI.sprite = PostItArt.Card(); bgI.type = Image.Type.Sliced; bgI.color = new Color(1f, 0.98f, 0.9f);
        var area = Child("Fill Area", rt, false, out dummy);
        area.anchorMin = new Vector2(0f, 0.5f); area.anchorMax = new Vector2(1f, 0.5f); area.sizeDelta = new Vector2(-30f, 16f); area.anchoredPosition = Vector2.zero;
        var fill = Child("Fill", area, true, out fillI);
        fillI.sprite = PostItArt.Card(); fillI.type = Image.Type.Sliced; fillI.color = Orange; fillI.raycastTarget = false;
        var slide = Child("Handle Slide Area", rt, false, out dummy);
        slide.anchorMin = Vector2.zero; slide.anchorMax = Vector2.one; slide.offsetMin = new Vector2(15f, 0f); slide.offsetMax = new Vector2(-15f, 0f);
        var handle = Child("Handle", slide, true, out hI);
        handle.sizeDelta = new Vector2(30f, 50f);
        hI.sprite = PostItArt.Card(); hI.color = Mint;
        var sl = go.GetComponent<Slider>();
        sl.fillRect = fill; sl.handleRect = handle; sl.targetGraphic = hI; sl.direction = Slider.Direction.LeftToRight;
        sl.minValue = 0f; sl.maxValue = 1f; sl.wholeNumbers = false; sl.value = Mathf.Clamp01(value);
        sl.onValueChanged.AddListener(v => changed(Mathf.Round(v * 100f) / 100f));
        return sl;
    }

    static void SetSliderLook(Slider sl, bool on)
    {
        foreach (var im in sl.GetComponentsInChildren<Image>()) { var c = im.color; c.a = on ? 1f : 0.4f; im.color = c; }
    }

    void FillCredits() { Txt(credits, 46, new Vector2(0f, 10f), new Vector2(660f, 420f)); }

    void FillHistory()
    {
        List<InterviewHistoryEntry> entries = SaveSystem.GetHistory();
        if (entries.Count == 0)
        {
            Txt("NO COMPLETED RUNS YET", 38, new Vector2(0f, 35f), new Vector2(680f, 100f));
            Txt("Your finalized interview reports will be kept here.", 28, new Vector2(0f, -45f), new Vector2(640f, 90f));
            return;
        }

        int pageSize = 6;
        int start = historyPage * pageSize;
        if (start >= entries.Count) { historyPage = Mathf.Max(0, (entries.Count - 1) / pageSize); start = historyPage * pageSize; }
        int count = Mathf.Min(pageSize, entries.Count - start);
        float startY = 285f;
        for (int i = 0; i < count; i++)
        {
            InterviewHistoryEntry e = entries[start + i];
            string candidate = string.IsNullOrEmpty(e.candidateName) ? "CANDIDATE" : e.candidateName;
            string verdict = e.outcome.IndexOf("HIRED", StringComparison.OrdinalIgnoreCase) >= 0 ? "HIRED" : "REJECTED";
            string shortDate = !string.IsNullOrEmpty(e.completedAt) && e.completedAt.Length >= 10 ? "  |  " + e.completedAt.Substring(5, 5) : "";
            string label = (start + i + 1) + ". " + Trim(Plain(candidate), 12) + "  |  " + verdict + "  |  " + e.grade + " " + Mathf.RoundToInt(e.score) + "%" + shortDate;
            float y = startY - i * 62f;
            string rowId = e.id;
            NoteAnim row = Make(label, verdict == "HIRED" ? Mint : Pink, new Vector2(0f, y), new Vector2(700f, 54f), (i % 2 == 0 ? -0.5f : 0.5f), 24, () => ShowHistoryDetail(rowId, 0), false, true);
            row.hoverFx = true;
        }
        if (entries.Count > pageSize)
        {
            Txt("PAGE " + (historyPage + 1) + " / " + ((entries.Count + pageSize - 1) / pageSize), 24, new Vector2(0f, -135f), new Vector2(240f, 40f));
            if (historyPage > 0) Make("PREVIOUS", Sky, new Vector2(-230f, -205f), new Vector2(210f, 68f), -1f, 28, () => ChangeHistoryPage(-1), false, true);
            if (start + count < entries.Count) Make("NEXT", Mint, new Vector2(230f, -205f), new Vector2(210f, 68f), 1f, 28, () => ChangeHistoryPage(1), false, true);
        }
    }

    // report lines use TextMeshPro column tags (<pos=31%>) that the normal UI Text prints literally: turn "A<pos>B<pos>C<pos>D" into "A: B   |   C: D"
    static string CleanLine(string ln)
    {
        if (ln.IndexOf("<pos=", StringComparison.Ordinal) < 0) return ln;
        string[] parts = Regex.Split(ln, "<pos=[^>]*>");
        var sb = new StringBuilder(parts[0]);
        for (int i = 1; i < parts.Length; i++) sb.Append(i % 2 == 1 ? ": " : "   |   ").Append(parts[i]);
        return sb.ToString();
    }

    static string Plain(string v) { return string.IsNullOrEmpty(v) ? "" : v.Replace('<', '(').Replace('>', ')'); }   // names must not inject rich-text tags
    static string Trim(string v, int max) { return v != null && v.Length > max ? v.Substring(0, max - 1) + ".." : v; }

    void ChangeHistoryPage(int delta)
    {
        if (panel == null) return;
        historyPage = Mathf.Max(0, historyPage + delta);
        ClearHistoryContents();
        FillHistory();
    }

    void ShowHistoryDetail(string id, int page)
    {
        if (panel == null) return;      // panel is closing: do not build new notes into a menu that is being cleaned up
        List<InterviewHistoryEntry> entries = SaveSystem.GetHistory();
        InterviewHistoryEntry entry = entries.Find(e => e.id == id);
        if (entry == null) return;

        ClearHistoryContents();
        string candidate = string.IsNullOrEmpty(entry.candidateName) ? "CANDIDATE" : Plain(entry.candidateName);
        string job = Plain(string.IsNullOrEmpty(entry.jobName) ? entry.jobId : entry.jobName);
        string verdict = (entry.outcome ?? "").IndexOf("HIRED", StringComparison.OrdinalIgnoreCase) >= 0 ? "HIRED" : "REJECTED";
        Txt(Trim(candidate, 30) + "\n" + verdict + "  |  GRADE " + entry.grade + "  |  SCORE " + Mathf.RoundToInt(entry.score) + "/100",
            34, new Vector2(0f, 300f), new Vector2(700f, 110f));
        string slot = entry.sourceSlot >= 0 ? "  |  SLOT " + (entry.sourceSlot + 1) : "";
        string meta = entry.completedAt + slot + "\nJOB: " + Trim(job, 40) +
                      "\nANSWERS: " + entry.correct + "/" + entry.asked + "  |  FOLLOW-UPS: " + entry.followUps +
                      "\nCONTRADICTIONS: " + entry.contradictions + "  |  SUSPICION: " + Mathf.RoundToInt(entry.suspicion) +
                      "\nMOOD: " + Plain(entry.mood) + "  |  PROOF: " + Trim(Plain(entry.proofResult), 34);
        Txt(meta, 22, new Vector2(0f, 165f), new Vector2(720f, 150f));

        // finalized report: split into lines and show a few per page (same paging idiom as the run list)
        List<string> lines = new List<string>();
        foreach (string raw in (entry.finalizedReport ?? "").Split('\n'))
        {
            string ln = raw.Trim();
            if (ln.Length == 0 || ln.Replace("</color>", "").IndexOf("----", StringComparison.Ordinal) >= 0) continue;   // skip blank + rule lines
            lines.Add(CleanLine(ln));
        }
        if (lines.Count == 0) lines.Add("No report text was saved.");
        int pages = (lines.Count + ReportLinesPerPage - 1) / ReportLinesPerPage;
        reportPage = Mathf.Clamp(page, 0, pages - 1);
        int from = reportPage * ReportLinesPerPage, take = Mathf.Min(ReportLinesPerPage, lines.Count - from);
        Txt(string.Join("\n", lines.GetRange(from, take).ToArray()), 22, new Vector2(0f, -45f), new Vector2(720f, 230f));
        if (pages > 1)
        {
            Txt("REPORT " + (reportPage + 1) + " / " + pages, 24, new Vector2(0f, -190f), new Vector2(260f, 40f));
            if (reportPage > 0) Make("PREV", Sky, new Vector2(-250f, -190f), new Vector2(170f, 64f), -1f, 28, () => ShowHistoryDetail(id, reportPage - 1), false, true);
            if (reportPage < pages - 1) Make("NEXT", Mint, new Vector2(250f, -190f), new Vector2(170f, 64f), 1f, 28, () => ShowHistoryDetail(id, reportPage + 1), false, true);
        }
        Make("BACK TO HISTORY", Mint, new Vector2(0f, -270f), new Vector2(340f, 68f), 1f, 28, () => { if (panel == null) return; ClearHistoryContents(); FillHistory(); }, false, true);
    }

    void ClearHistoryContents()
    {
        GameObject panelObject = panel != null ? panel.gameObject : null;
        GameObject shadowObject = panel != null && panel.shadow != null ? panel.shadow.gameObject : null;
        GameObject backObject = panelBack != null ? panelBack.gameObject : null;
        GameObject backShadow = panelBack != null && panelBack.shadow != null ? panelBack.shadow.gameObject : null;
        for (int i = extras.Count - 1; i >= 0; i--)
        {
            GameObject g = extras[i];
            if (g == null) { extras.RemoveAt(i); continue; }
            if (g == panelObject || g == shadowObject || g == backObject || g == backShadow || g == banner) continue;
            WorldUtil.Kill(g);
            extras.RemoveAt(i);
        }
    }
}

public class DrowsyView : MonoBehaviour
{
    Camera cam;
    RenderTexture rt;
    RawImage main, ghost;
    RectTransform mainRt, ghostRt, topRt, botRt;
    GameObject c1, c2, screenCam;
    float sleep = 1f, wakeT = -1f, blinkAt, cover0 = 0.41f; int initFrame;
    const float Black = 0.72f;   // lid cover at which both lids overlap: pitch black
    // wake-up sequence: time, lid cover (0 = open, 0.72 = pitch black), sleepiness (blur/ghosting). Drowsy blinks that open wider each time.
    static readonly float[,] WakeKeys = {
        {0.9f, Black, 1f},    // pitch-black hold
        {1.9f, 0.47f, 1f},    // first heavy-lidded crack
        {2.5f, Black, 1f},    // falls shut again
        {3.0f, Black, 1f},
        {4.0f, 0.28f, 0.8f},  // second blink: wider, small droop, then awake
        {4.5f, 0.42f, 0.85f},
        {6.0f, 0f, 0f} };

    public void Init(Camera c)
    {
        cam = c;
        screenCam = new GameObject("Drowsy Screen Camera", typeof(Camera));
        var sc = screenCam.GetComponent<Camera>(); sc.cullingMask = 0; sc.clearFlags = CameraClearFlags.SolidColor; sc.backgroundColor = Color.black; sc.depth = c.depth - 10f;
        c1 = new GameObject("Drowsy View", typeof(Canvas));
        var cv = c1.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = -50;
        main = Raw(c1.transform, 1f, out mainRt);
        ghost = Raw(c1.transform, 0.3f, out ghostRt);

        c2 = new GameObject("Eyelids", typeof(Canvas));
        var cv2 = c2.GetComponent<Canvas>(); cv2.renderMode = RenderMode.ScreenSpaceOverlay; cv2.sortingOrder = 100;
        var lt = new Texture2D(4, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++)
        {
            float a = Mathf.SmoothStep(0f, 1f, y / 127f);
            for (int x = 0; x < 4; x++) lt.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        lt.wrapMode = TextureWrapMode.Clamp; lt.Apply(false);
        var spr = Sprite.Create(lt, new Rect(0, 0, 4, 128), new Vector2(0.5f, 0.5f), 100f);
        topRt = Lid(c2.transform, spr, false);
        botRt = Lid(c2.transform, spr, true);
        MakeRT();
        blinkAt = Time.unscaledTime + 3f;
        initFrame = Time.frameCount;
        SetLid(topRt, Black, true); SetLid(botRt, Black, false);   // first rendered frame is pitch black
    }

    RawImage Raw(Transform p, float a, out RectTransform r)
    {
        var go = new GameObject("Raw", typeof(RawImage));
        r = go.GetComponent<RectTransform>(); r.SetParent(p, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(-30f, -30f); r.offsetMax = new Vector2(30f, 30f);
        var ri = go.GetComponent<RawImage>(); ri.color = new Color(1f, 1f, 1f, a); ri.raycastTarget = false;
        return ri;
    }

    RectTransform Lid(Transform p, Sprite s, bool flip)
    {
        var go = new GameObject("Lid", typeof(RectTransform));
        var r = go.GetComponent<RectTransform>(); r.SetParent(p, false);
        Color dark = new Color(0.02f, 0.015f, 0.02f, 1f);
        var sg = new GameObject("Solid", typeof(Image));
        var sr = sg.GetComponent<RectTransform>(); sr.SetParent(r, false);
        sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = sr.offsetMax = Vector2.zero;
        var si = sg.GetComponent<Image>(); si.color = dark; si.raycastTarget = false;
        var gg = new GameObject("Edge", typeof(Image));
        var gr = gg.GetComponent<RectTransform>(); gr.SetParent(r, false);
        gr.anchorMin = Vector2.zero; gr.anchorMax = new Vector2(1f, 0f); gr.pivot = new Vector2(0.5f, 0f); gr.offsetMin = gr.offsetMax = Vector2.zero;
        var gi = gg.GetComponent<Image>(); gi.sprite = s; gi.color = dark; gi.raycastTarget = false;
        if (flip) r.localScale = new Vector3(1f, -1f, 1f);
        return r;
    }

    void MakeRT()
    {
        if (rt != null) { cam.targetTexture = null; rt.Release(); WorldUtil.Kill(rt); }
        rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        rt.useMipMap = true; rt.autoGenerateMips = true; rt.antiAliasing = 2; rt.filterMode = FilterMode.Trilinear; rt.wrapMode = TextureWrapMode.Clamp;
        rt.Create();
        cam.targetTexture = rt;
        main.texture = rt; ghost.texture = rt;
    }

    public void Wake()
    {
        // started in the same frame as Init (menu skipped): begin in pitch black; otherwise first close the half-open eyes
        cover0 = Time.frameCount == initFrame ? Black : lastCover;
        wakeT = 0f;
    }
    float lastCover = 0.41f;
    const float CloseDur = 0.5f;

    void WakeCurve(float t, out float cover, out float slp)
    {
        float lead = cover0 < Black - 0.01f ? CloseDur : 0f;
        if (t < lead) { float u = t / lead; u = u * u * (3f - 2f * u); cover = Mathf.Lerp(cover0, Black, u); slp = 1f; return; }
        t -= lead;
        float pt = 0f, pc = Black, ps = 1f;
        for (int i = 0; i < WakeKeys.GetLength(0); i++)
        {
            float kt = WakeKeys[i, 0];
            if (t <= kt) { float u = Mathf.Clamp01((t - pt) / (kt - pt)); u = u * u * (3f - 2f * u); cover = Mathf.Lerp(pc, WakeKeys[i, 1], u); slp = Mathf.Lerp(ps, WakeKeys[i, 2], u); return; }
            pt = kt; pc = WakeKeys[i, 1]; ps = WakeKeys[i, 2];
        }
        cover = 0f; slp = 0f;
    }

    void SetLid(RectTransform r, float cover, bool top)
    {
        r.anchorMin = top ? new Vector2(0f, 1f - cover) : Vector2.zero;
        r.anchorMax = top ? Vector2.one : new Vector2(1f, cover);
        r.offsetMin = r.offsetMax = Vector2.zero;
        float g = Mathf.Min(0.16f * Screen.height, cover * Screen.height);
        var solid = (RectTransform)r.GetChild(0); solid.offsetMin = new Vector2(0f, g); solid.offsetMax = Vector2.zero;
        var edge = (RectTransform)r.GetChild(1); edge.sizeDelta = new Vector2(0f, g);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
        float cover;
        if (wakeT >= 0f)
        {
            wakeT += Mathf.Min(dt, 0.05f);   // a load hitch must not skip the black hold
            WakeCurve(wakeT, out cover, out sleep);
            if (wakeT >= WakeKeys[WakeKeys.GetLength(0) - 1, 0] + CloseDur) { Finish(); return; }
        }
        else
        {
            float blink = 0f;
            if (t > blinkAt)
            {
                float u = (t - blinkAt) / 0.45f;
                if (u >= 1f) blinkAt = t + 3f + Mathf.PerlinNoise(t, 0.3f) * 4f;
                else blink = Mathf.Sin(u * Mathf.PI);
            }
            cover = sleep * (0.41f + 0.025f * Mathf.Sin(t * 0.9f) + blink * 0.15f);
        }
        lastCover = cover;
        if (rt.width != Screen.width || rt.height != Screen.height) MakeRT();
        rt.mipMapBias = 4.8f * sleep;
        SetLid(topRt, cover, true); SetLid(botRt, cover, false);
        mainRt.anchoredPosition = new Vector2(0f, Mathf.Sin(t * 0.6f) * 10f * sleep);
        ghostRt.anchoredPosition = new Vector2(Mathf.Sin(t * 0.9f) * 16f * sleep, Mathf.Cos(t * 0.7f) * 10f * sleep);
        ghost.color = new Color(1f, 1f, 1f, 0.42f * sleep);
    }

    void Finish()
    {
        cam.targetTexture = null;
        rt.Release(); WorldUtil.Kill(rt); rt = null;
        WorldUtil.Kill(c1); WorldUtil.Kill(c2); WorldUtil.Kill(screenCam); WorldUtil.Kill(this);
    }

    void OnDestroy()
    {
        if (cam != null && rt != null && cam.targetTexture == rt) cam.targetTexture = null;
        if (rt != null) rt.Release();
    }
}

/// <summary>Text-only menu entry: staggered slide-in, hover = red + nudge right, click bounce, fade-out on dismiss.</summary>
public class MenuItemAnim : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Action onClick;
    public bool interactive = true;
    public Text label;
    public Vector2 basePos;
    public int order;
    public Color idle = Color.white, hot = Color.red;
    public Image strip, shadow; public Color stripIdle = Color.white, stripHot = Color.yellow; public float baseRot;
    RectTransform rt;
    CanvasGroup cg;
    float appT, hov, dis, bounce;
    bool over, dismiss;

    void Awake() { rt = (RectTransform)transform; cg = GetComponent<CanvasGroup>(); }

    float App { get { float a = Mathf.Clamp01((appT - (0.15f + order * 0.08f)) / 0.45f); return a * a * (3f - 2f * a); } }
    bool Live { get { return interactive && !dismiss && App > 0.6f; } }

    public void OnPointerEnter(PointerEventData e) { over = true; }
    public void OnPointerExit(PointerEventData e) { over = false; }

    public void OnPointerClick(PointerEventData e)
    {
        if (!Live) return;
        bounce = 1f;
        if (onClick != null) onClick();
    }

    public void Dismiss(bool d) { dismiss = d; }
    public void Restore() { appT = 0f; dismiss = false; dis = 0f; }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        appT += dt;
        dis = Mathf.MoveTowards(dis, dismiss ? 1f : 0f, dt * 4f);
        hov = Mathf.MoveTowards(hov, (over && Live) ? 1f : 0f, dt * 8f);
        bounce = Mathf.MoveTowards(bounce, 0f, dt * 5f);
        float a = App, eh = hov * hov * (3f - 2f * hov);
        rt.anchoredPosition = new Vector2(basePos.x + 28f * eh - 140f * (1f - a) - 40f * dis, basePos.y);
        rt.localScale = Vector3.one * ((1f + 0.07f * eh) * (1f - 0.05f * Mathf.Sin(bounce * Mathf.PI)));
        rt.localRotation = Quaternion.Euler(0f, 0f, baseRot * (1f - 0.8f * eh));
        if (label != null) label.color = Color.Lerp(idle, hot, eh);
        if (strip != null && interactive) strip.color = Color.Lerp(stripIdle, stripHot, eh);
        if (shadow != null)
        {
            var sr = shadow.rectTransform;
            sr.anchoredPosition = rt.anchoredPosition + new Vector2(9f + 4f * eh, -10f - 3f * eh);
            sr.localScale = rt.localScale; sr.localRotation = rt.localRotation;
            var sc = shadow.color; sc.a = 0.5f * a * (1f - dis); shadow.color = sc;
        }
        cg.alpha = a * (1f - dis);
        cg.blocksRaycasts = interactive && !dismiss && a > 0.6f;
    }
}
