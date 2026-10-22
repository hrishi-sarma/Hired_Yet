using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Cartoon loading screen. Deliberately calm: a chunky striped bar with a glowing head, drifting sparkles and soft blobs.
// Everything that moves (stripes, sparkles, glow pulse, blobs) runs on its own clock, so the screen always looks alive
// even when real loading progress stalls or jumps. The shown progress is critically damped and never goes backwards.
// Pure uGUI + a few tiny generated sprites (no shaders, no render textures), so it is cheap and WebGL-safe.
// Font: Assets/CustomFonts/Caveat-Regular.ttf -> Resources/LoadingFontCaveat (built by Editor/LoadingFontBuilder.cs).
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    // How long the screen stays after everything has loaded (hides the post-load frame drop / texture streaming).
    public const float HoldAfterLoaded = 3f;

    static readonly Color Ink = new Color(0.15f, 0.12f, 0.2f, 1f);
    static readonly Color Cream = new Color(1f, 0.945f, 0.79f, 1f);
    static readonly Color Orange = H("#FFB347"), Pink = H("#FF8FB8"), Mint = H("#8FE3C0"), Sky = H("#8CCBFF"), Gold = H("#FFD35C");
    static readonly Color ErrorRed = H("#FF6B6B");
    const float BarH = 38f, BorderPx = 4f, StripeStep = 40f;
    const string FontResource = "LoadingFontCaveat";

    static readonly string[] Tips =
    {
        "Press Tab to open your inventory. It holds just three items.",
        "Your starting resume is a photo of the form you filled in. It lives in slot 1.",
        "Double-click an item in your inventory to hold it. Double-click again to put it away.",
        "Right-click an inventory slot to drop the item or edit the name written on it.",
        "Press F to quickly put away or take out the item you were holding.",
        "Press G to drop the item in your hand. It stays on the ground until you pick it up again.",
        "While you hold an item you can't interact with things. Put it away first.",
        "Press E near people and objects to interact with them.",
        "Helping people with their little problems can earn you useful documents.",
        "Gather every flying paper before the wind takes them, and you may find a document.",
        "Pickpocketing is risky. If you fail, the pedestrian notices you.",
        "Crumpled documents have an unreadable name, so they raise the least suspicion.",
        "A document with someone else's name raises the most suspicion. Use Change Name if you can.",
        "Documents with a blank name can be fixed with Add Name, which writes your name on them.",
        "A scribbled-out name looks suspicious, but less than a completely different name.",
        "ID cards and business cards can't be rewritten, so you'll have to bluff if the name is wrong.",
        "The interviewer asks for your resume first. Hold it and press E to hand it over.",
        "Documents you hand over leave your inventory for good.",
        "Hover over an item to see its stats and how risky it is to show.",
        "Green arrows on a tooltip mean the item beats your current document of the same kind.",
        "Item frames show rarity: sage is common, mint uncommon, sky rare, lilac epic and gold legendary.",
        "Legendary items never turn up randomly. Only special events can give them to you.",
        "Extra documents like certificates and portfolios can raise your score, but flawed ones add suspicion.",
        "When the interviewer questions a document, you can explain honestly, bluff, or stay silent.",
        "Bluffs work better when the interviewer is in a good mood.",
        "A stern or irritated interviewer is much harder to fool.",
        "The puzzles during the interview test your mind. Take your time and stay calm.",
        "If your inventory is full, picking something up lets you choose which item to swap out.",
        "The suburb, the forest and the city each have their own events to find.",
        "Head to the interview door when you feel ready."
    };


    /// <summary>What is loading, set by the caller before Show(): decides the title only.</summary>
    public enum LoadKind { Game, World, Interview }
    public static LoadKind Kind = LoadKind.Game;

    // vague, sarcastic status lines: they never say what is really loading. A shuffled bag, so no repeats until all are used.
    static readonly string[] Quips =
    {
        "putting together the tectonic plates", "teaching pigeons to look busy", "untangling the cables behind the sky",
        "convincing the sun to clock in", "inflating the clouds, slowly", "sharpening the corners of every building",
        "politely asking the traffic to be less traffic-y", "reheating yesterday's coffee for the NPCs", "fixing the one bug we definitely don't have",
        "rolling out the carpet of lawn", "assigning everyone a mildly urgent errand", "waiting for the progress bar to feel something",
        "dusting off the horizon", "finding a place for all the lost socks", "loading the loading screen's loading screen",
        "arguing with a very stubborn hill", "pretending to be productive", "stapling the pavement down",
        "counting every blade of grass, twice", "calibrating the Monday feeling", "untying the knot in the road",
        "bribing the squirrels with fake acorns", "making the water look wetter", "installing opinions in pedestrians",
        "turning it off and on again, spiritually", "warming up the small talk", "locating the office printer's will to live",
        "buffering the sense of purpose", "reorganizing the clouds by size", "moving the mountains a little to the left",
        "adding the fashionably late fog", "apologizing to the interviewer in advance", "rehearsing a confident handshake",
        "ironing out the wrinkles in the wallpaper", "negotiating with gravity", "polishing the doorknobs for no reason",
        "feeding the hamsters that power the servers", "sorting the paperclips by emotional value", "drafting a very optimistic cover letter"
    };
    static readonly System.Collections.Generic.List<int> quipBag = new System.Collections.Generic.List<int>();
    static int lastQuip = -1;
    string quip = ""; float quipT;

    static int NextQuip()
    {
        if (quipBag.Count == 0)
        {
            for (int i = 0; i < Quips.Length; i++) quipBag.Add(i);
            for (int i = quipBag.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); int tmp = quipBag[i]; quipBag[i] = quipBag[j]; quipBag[j] = tmp; }
            if (quipBag[quipBag.Count - 1] == lastQuip) { int tmp = quipBag[0]; quipBag[0] = quipBag[quipBag.Count - 1]; quipBag[quipBag.Count - 1] = tmp; }   // no repeat across bags
        }
        lastQuip = quipBag[quipBag.Count - 1]; quipBag.RemoveAt(quipBag.Count - 1);
        return lastQuip;
    }

    static string TitleFor(LoadKind k)
    {
        switch (k) { case LoadKind.World: return "Generating world"; case LoadKind.Interview: return "Preparing the interview"; default: return "Loading game"; }
    }

    CanvasGroup group, tipGroup, contentGroup;
    Image cover, fillImg, glowImg;
    RectTransform sceneRt, barRt, fillRt, stripesRt, headRt, tipRt, blobA, blobB, blobC;
    TextMeshProUGUI title, status, percent, tipText;
    RectTransform[] stripes, sparks;
    float[] sparkSeed;
    Sprite sCircle, sRound, sGlow, sStar;
    float target, shown, shownVel, time, barW = 900f;
    string statusText = "", errorText;
    bool closing;
    float tipTimer; int tipIndex = -1;
    const int SparkCount = 14;

    static Color H(string hex) { Color c; ColorUtility.TryParseHtmlString(hex, out c); return c; }

    public static LoadingScreen Show()
    {
        ScreenFade.Reveal(0.55f);   // came here through a fade-to-black: come up out of the dark
        if (Instance != null) { Instance.quip = Quips[NextQuip()]; Instance.quipT = 0f; return Instance; }
        GameObject go = new GameObject("LoadingScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<LoadingScreen>();
        Instance.Build();
        Instance.quip = Quips[NextQuip()]; Instance.quipT = 0f;
        return Instance;
    }

    // ------------------------------------------------------------------ sprites
    static Sprite MakeSprite(int n, System.Func<float, float, float> sdf, float border)
    {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = sdf(x + 0.5f, y + 0.5f);
                byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
        t.SetPixels32(px); t.Apply(false, true);
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    void MakeSprites()
    {
        sCircle = MakeSprite(64, (x, y) => Vector2.Distance(new Vector2(x, y), new Vector2(32, 32)) - 30f, 0f);
        sRound = MakeSprite(64, (x, y) =>
        {
            Vector2 q = new Vector2(Mathf.Abs(x - 32f) - 8f, Mathf.Abs(y - 32f) - 8f);   // half 30, radius 22
            return Vector2.Distance(Vector2.zero, new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f))) + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 22f;
        }, 24f);
        // soft radial glow
        sGlow = MakeSprite(64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32)) / 32f;
            return 0.5f - Mathf.Pow(Mathf.Clamp01(1f - d), 2f);   // alpha = (1-d)^2
        }, 0f);
        // four-point star
        sStar = MakeSprite(64, (x, y) =>
        {
            float f = Mathf.Sqrt(Mathf.Abs(x - 32f)) + Mathf.Sqrt(Mathf.Abs(y - 32f));
            return (f - 5.2f) * 3.5f;
        }, 0f);
    }

    // ------------------------------------------------------------------ builders
    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    Image Img(Transform parent, string name, Sprite s, Vector2 size, Vector2 pos, Color col, bool sliced = false)
    {
        RectTransform rt = NewRect(name, parent);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        Image im = rt.gameObject.AddComponent<Image>();
        im.sprite = s; im.color = col; im.raycastTarget = false;
        if (sliced) im.type = Image.Type.Sliced;
        return im;
    }

    TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, float size, Color col, TextAlignmentOptions al)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.color = col; t.alignment = al; t.raycastTarget = false; t.fontStyle = FontStyles.Normal;
        t.enableWordWrapping = false;
        return t;
    }

    static void SetCentered(RectTransform rt, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
    }

    void Build()
    {
        MakeSprites();
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        group = GetComponent<CanvasGroup>();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(FontResource);

        // background
        RectTransform bg = NewRect("Bg", transform);
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
        Image bgi = bg.gameObject.AddComponent<Image>(); bgi.color = Cream; bgi.raycastTarget = true;
        contentGroup = bg.gameObject.AddComponent<CanvasGroup>();
        blobA = Img(bg, "BlobA", sCircle, new Vector2(900, 900), new Vector2(-760, 330), new Color(1f, 0.88f, 0.66f, 0.55f)).rectTransform;
        blobB = Img(bg, "BlobB", sCircle, new Vector2(760, 760), new Vector2(780, -260), new Color(1f, 0.7f, 0.82f, 0.35f)).rectTransform;
        blobC = Img(bg, "BlobC", sCircle, new Vector2(520, 520), new Vector2(520, 380), new Color(0.62f, 0.88f, 1f, 0.35f)).rectTransform;

        sceneRt = NewRect("Scene", bg);
        sceneRt.anchorMin = Vector2.zero; sceneRt.anchorMax = Vector2.one; sceneRt.offsetMin = sceneRt.offsetMax = Vector2.zero;

        // sparkles drift upward behind everything else in the scene
        sparks = new RectTransform[SparkCount]; sparkSeed = new float[SparkCount];
        Color[] sc = { Pink, Mint, Sky, Gold, Orange };
        for (int i = 0; i < SparkCount; i++)
        {
            sparkSeed[i] = Random.value;
            Image s = Img(sceneRt, "Spark" + i, sStar, new Vector2(26, 26), Vector2.zero, sc[i % sc.Length]);
            s.rectTransform.anchorMin = s.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            sparks[i] = s.rectTransform;
        }

        title = Text(sceneRt, "Title", font, 120, Ink, TextAlignmentOptions.Center);
        SetCentered(title.rectTransform, new Vector2(1200, 130), new Vector2(0, 150));

        // bar: ink border -> mask track -> mask fill -> scrolling stripes
        Image border = Img(sceneRt, "BarBorder", sRound, new Vector2(900 + BorderPx * 2f, BarH + BorderPx * 2f), Vector2.zero, Ink, true);
        barRt = border.rectTransform; SetCentered(barRt, barRt.sizeDelta, new Vector2(0f, 10f));
        Image track = Img(barRt, "Track", sRound, new Vector2(900, BarH), Vector2.zero, new Color(1f, 0.99f, 0.93f, 1f), true);
        RectTransform trackRt = track.rectTransform;
        trackRt.anchorMin = Vector2.zero; trackRt.anchorMax = Vector2.one; trackRt.offsetMin = new Vector2(BorderPx, BorderPx); trackRt.offsetMax = new Vector2(-BorderPx, -BorderPx);
        track.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        fillImg = Img(trackRt, "Fill", sRound, new Vector2(BarH, BarH), Vector2.zero, Orange, true);
        fillRt = fillImg.rectTransform;
        fillRt.anchorMin = new Vector2(0f, 0f); fillRt.anchorMax = new Vector2(0f, 1f); fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
        fillImg.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        stripesRt = NewRect("Stripes", fillRt);
        stripesRt.anchorMin = new Vector2(0f, 0f); stripesRt.anchorMax = new Vector2(0f, 1f); stripesRt.pivot = new Vector2(0f, 0.5f);
        stripesRt.sizeDelta = new Vector2(1f, 0f);
        int n = Mathf.CeilToInt(1400f / StripeStep) + 4;
        stripes = new RectTransform[n];
        for (int i = 0; i < n; i++)
        {
            Image st = Img(stripesRt, "S" + i, sRound, new Vector2(15f, BarH * 2.4f), new Vector2(i * StripeStep, 0f), new Color(1f, 1f, 1f, 0.28f), true);
            st.rectTransform.anchorMin = st.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            st.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 28f);
            stripes[i] = st.rectTransform;
        }
        // glossy top highlight on the fill
        Image shine = Img(fillRt, "Shine", sRound, new Vector2(10f, 8f), Vector2.zero, new Color(1f, 1f, 1f, 0.4f), true);
        shine.rectTransform.anchorMin = new Vector2(0f, 1f); shine.rectTransform.anchorMax = new Vector2(1f, 1f); shine.rectTransform.pivot = new Vector2(0.5f, 1f);
        shine.rectTransform.offsetMin = new Vector2(12f, -13f); shine.rectTransform.offsetMax = new Vector2(-12f, -5f);

        // glowing head riding the end of the fill (not clipped by the mask)
        headRt = NewRect("Head", barRt);
        headRt.anchorMin = headRt.anchorMax = new Vector2(0f, 0.5f);
        headRt.sizeDelta = Vector2.zero;
        glowImg = Img(headRt, "Glow", sGlow, new Vector2(140, 140), Vector2.zero, new Color(1f, 0.85f, 0.4f, 0.8f));
        Image core = Img(headRt, "Star", sStar, new Vector2(46, 46), Vector2.zero, Color.white);
        core.rectTransform.name = "Star";

        percent = Text(sceneRt, "Percent", font, 64, Ink, TextAlignmentOptions.Center);
        SetCentered(percent.rectTransform, new Vector2(1000, 72), new Vector2(0, -62f));
        status = Text(sceneRt, "Status", font, 40, Ink, TextAlignmentOptions.Center);
        SetCentered(status.rectTransform, new Vector2(1200, 52), new Vector2(0, -122f));

        BuildTips(font);

        // black layer on top, used for the "fade to dark" before a scene change
        RectTransform cv = NewRect("Cover", transform);
        cv.anchorMin = Vector2.zero; cv.anchorMax = Vector2.one; cv.offsetMin = cv.offsetMax = Vector2.zero;
        cover = cv.gameObject.AddComponent<Image>(); cover.color = new Color(0f, 0f, 0f, 0f); cover.raycastTarget = false;
    }

    void BuildTips(TMP_FontAsset font)
    {
        Image panel = Img(sceneRt, "TipPanel", sRound, new Vector2(1000, 92), Vector2.zero, Ink, true);
        tipRt = panel.rectTransform;
        tipRt.anchorMin = tipRt.anchorMax = new Vector2(0.5f, 0f); tipRt.pivot = new Vector2(0.5f, 0f);
        tipRt.anchoredPosition = new Vector2(0, 56);
        tipGroup = tipRt.gameObject.AddComponent<CanvasGroup>();
        Image inner = Img(tipRt, "Inner", sRound, Vector2.zero, Vector2.zero, new Color(1f, 0.99f, 0.93f, 1f), true);
        inner.rectTransform.anchorMin = Vector2.zero; inner.rectTransform.anchorMax = Vector2.one;
        inner.rectTransform.offsetMin = new Vector2(3f, 3f); inner.rectTransform.offsetMax = new Vector2(-3f, -3f);
        Image chip = Img(tipRt, "Chip", sRound, new Vector2(84, 42), Vector2.zero, Orange, true);
        chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(0f, 0.5f); chip.rectTransform.pivot = new Vector2(0f, 0.5f); chip.rectTransform.anchoredPosition = new Vector2(20, 0);
        TextMeshProUGUI chipT = Text(chip.rectTransform, "T", font, 38, Ink, TextAlignmentOptions.Center);
        chipT.text = "TIP"; chipT.rectTransform.anchorMin = Vector2.zero; chipT.rectTransform.anchorMax = Vector2.one; chipT.rectTransform.offsetMin = chipT.rectTransform.offsetMax = Vector2.zero;
        tipText = Text(tipRt, "Text", font, 38, Ink, TextAlignmentOptions.MidlineLeft);
        tipText.enableWordWrapping = true; tipText.enableAutoSizing = true; tipText.fontSizeMin = 24; tipText.fontSizeMax = 38;
        RectTransform tr = tipText.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(120, 8); tr.offsetMax = new Vector2(-24, -8);
        NextTip();
        tipGroup.alpha = 1f;
    }

    void NextTip()
    {
        int n;
        do { n = Random.Range(0, Tips.Length); } while (n == tipIndex && Tips.Length > 1);
        tipIndex = n; tipText.text = Tips[n]; tipTimer = 0f;
    }

    // ------------------------------------------------------------------ API
    public void SetProgress(float t, string text) { target = Mathf.Clamp01(t); statusText = text; }
    public void ShowError(string message) { errorText = message; }

    public void Finish(float hold = HoldAfterLoaded)
    {
        if (closing) return;
        closing = true;
        StartCoroutine(FadeOut(hold));
    }

    // World -> interview room: the screen darkens gradually, the loading screen appears out of the dark,
    // the scene loads in the background, then the screen stays a moment and fades out.
    public static void TransitionToScene(string scene, float darkSeconds = 1.3f)
    {
        Kind = LoadKind.Interview;
        LoadingScreen s = Show();
        s.StartCoroutine(s.SceneRoutine(scene, darkSeconds));
    }

    IEnumerator SceneRoutine(string scene, float darkSeconds)
    {
        contentGroup.alpha = 0f; cover.raycastTarget = true;
        target = 0f; shown = 0f; shownVel = 0f; statusText = "loading interview room";
        float prevScale = Time.timeScale;
        Time.timeScale = 0f;                      // freeze the world while it darkens
        float t = 0f;
        while (t < darkSeconds)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float k = Mathf.Clamp01(t / darkSeconds);
            cover.color = new Color(0f, 0f, 0f, k * k * (3f - 2f * k));
            yield return null;
        }
        cover.color = Color.black; contentGroup.alpha = 1f;
        t = 0f;
        while (t < 0.4f)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            cover.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(t / 0.4f));
            yield return null;
        }
        cover.color = Color.clear; cover.raycastTarget = false;
        yield return null;

        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(scene);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) { SetProgress(op.progress / 0.9f * 0.9f, "loading interview room"); yield return null; }
        SetProgress(1f, "almost there");
        float guard = 0f;
        while (shown < 0.99f && guard < 6f) { guard += Mathf.Min(Time.unscaledDeltaTime, 0.1f); yield return null; }
        Time.timeScale = prevScale > 0f ? prevScale : 1f;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        SetProgress(1f, "ready");
        Finish(1.2f);
    }

    IEnumerator FadeOut(float holdSeconds)
    {
        target = 1f;
        float guard = 0f;
        while (shown < 1f && guard < 6f) { guard += Mathf.Min(Time.unscaledDeltaTime, 0.1f); yield return null; }
        // stay up a little longer: the world is already running behind the screen and needs a moment to settle
        float hold = errorText != null ? 0f : holdSeconds;
        while (hold > 0f) { hold -= Mathf.Min(Time.unscaledDeltaTime, 0.1f); yield return null; }
        float t = 0f;
        while (t < 0.5f)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            group.alpha = 1f - t / 0.5f;
            yield return null;
        }
        if (Instance == this) Instance = null;
        WorldUtil.Kill(gameObject);
    }

    // ------------------------------------------------------------------ animation
    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        time += dt;

        // critically damped, never backwards: stalls and jumps in real progress look like a smooth glide
        float want = Mathf.Max(target, shown);
        shown = Mathf.SmoothDamp(shown, want, ref shownVel, 0.3f, 0.8f, dt);
        if (want - shown < 0.003f) { shown = want; shownVel = 0f; }

        float canvasW = sceneRt.rect.width;
        barW = Mathf.Clamp(canvasW - 200f, 360f, 900f);
        barRt.sizeDelta = new Vector2(barW + BorderPx * 2f, BarH + BorderPx * 2f);

        // fill + stripes + head
        float inner = barW;
        float fillW = shown <= 0.0005f ? 0f : Mathf.Max(BarH, inner * shown);
        fillRt.sizeDelta = new Vector2(fillW, 0f);
        fillRt.gameObject.SetActive(fillW > 0f);
        float off = Mathf.Repeat(time * 34f, StripeStep);
        stripesRt.anchoredPosition = new Vector2(-StripeStep * 2f + off, 0f);
        float hue = 0.5f + 0.5f * Mathf.Sin(time * 0.5f);
        fillImg.color = Color.Lerp(Orange, Pink, hue * 0.7f);

        float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3f);
        headRt.anchoredPosition = new Vector2(BorderPx + fillW - BarH * 0.35f, 0f);
        float gs = 1.1f + 0.35f * pulse;
        glowImg.rectTransform.localScale = new Vector3(gs, gs, 1f);
        glowImg.color = new Color(1f, 0.85f, 0.45f, (0.45f + 0.35f * pulse) * Mathf.Clamp01(shown * 30f));
        Transform star = headRt.Find("Star");
        star.localRotation = Quaternion.Euler(0f, 0f, time * 60f);
        star.localScale = Vector3.one * (0.85f + 0.3f * pulse) * Mathf.Clamp01(shown * 30f);

        // texts
        int dots = (int)(time * 2f) % 4;
        if (errorText != null)
        {
            title.color = ErrorRed; title.text = "Oops! Generation failed";
            status.color = ErrorRed; status.text = errorText;
            percent.text = "";
        }
        else
        {
            title.text = TitleFor(Kind) + new string('.', dots) + "<alpha=#00>" + new string('.', 3 - dots);
            percent.text = Mathf.RoundToInt(shown * 100f) + "%";
            quipT += dt;
            if (quipT > 3.2f) { quipT = 0f; quip = Quips[NextQuip()]; }
            status.text = "<alpha=#BB>" + quip + "...";
        }

        AnimateAmbient(canvasW);
        AnimateTips(dt, canvasW);
    }

    void AnimateAmbient(float canvasW)
    {
        // slow breathing blobs
        blobA.anchoredPosition = new Vector2(-760f + 30f * Mathf.Sin(time * 0.20f), 330f + 24f * Mathf.Cos(time * 0.17f));
        blobB.anchoredPosition = new Vector2(780f + 26f * Mathf.Cos(time * 0.18f), -260f + 30f * Mathf.Sin(time * 0.22f));
        blobC.anchoredPosition = new Vector2(520f + 22f * Mathf.Sin(time * 0.25f), 380f + 22f * Mathf.Cos(time * 0.2f));

        // sparkles rise slowly and twinkle; each has its own lifetime so they never move in lockstep
        float h = sceneRt.rect.height;
        for (int i = 0; i < SparkCount; i++)
        {
            float seed = sparkSeed[i];
            float life = 9f + seed * 6f;
            float t = Mathf.Repeat(time / life + seed * 7.3f, 1f);
            float x = (Mathf.Repeat(seed * 977f + i * 0.37f, 1f) - 0.5f) * (canvasW - 120f) + 18f * Mathf.Sin(time * 0.7f + i);
            float y = Mathf.Lerp(-h * 0.5f - 20f, h * 0.5f + 20f, t);
            float fade = Mathf.Sin(t * Mathf.PI);
            float tw = 0.6f + 0.4f * Mathf.Sin(time * (1.5f + seed * 2f) + i);
            sparks[i].anchoredPosition = new Vector2(x, y);
            sparks[i].localRotation = Quaternion.Euler(0f, 0f, time * (10f + seed * 20f));
            sparks[i].localScale = Vector3.one * (0.5f + seed * 0.8f) * (0.6f + 0.4f * tw);
            Image im = sparks[i].GetComponent<Image>();
            Color c = im.color; c.a = fade * 0.75f; im.color = c;
        }
    }

    void AnimateTips(float dt, float canvasW)
    {
        tipRt.sizeDelta = new Vector2(Mathf.Min(1000f, canvasW - 60f), 92f);
        // unclamped real time: after a long frame the tip advances immediately instead of lagging behind
        tipTimer += Time.unscaledDeltaTime;
        const float show = 3.5f, fade = 0.25f;
        if (tipTimer > show + fade) { NextTip(); }
        float a = 1f;
        if (tipTimer < fade) a = tipTimer / fade;
        else if (tipTimer > show) a = 1f - (tipTimer - show) / fade;
        tipGroup.alpha = Mathf.Clamp01(a);
    }
}
