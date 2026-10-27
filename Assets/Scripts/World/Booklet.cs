using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tutorial booklet (open book, 2 spreads: CONTROLS / GOALS). Styled like the resume desk and the inventory folio.
/// Opens by itself once, right after a brand-new save spawns into the open world (SaveData.bookletPending),
/// and from the BOOKLET button that appears bottom-left while the inventory is open. Closes with the X button or Esc.
/// </summary>
public class Booklet : MonoBehaviour
{
    static Booklet inst;
    static int closeFrame = -10;
    public static bool IsOpen { get { return inst != null && inst.open; } }
    /// <summary>True while open and for the frame it closes (so the Esc / Tab that closed it is not reused by the pause menu / inventory).</summary>
    public static bool Active { get { return inst != null && (inst.open || Time.frameCount == closeFrame); } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { inst = null; closeFrame = -10; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (inst != null) return;
        var go = new GameObject("Booklet (Auto)");
        inst = go.AddComponent<Booklet>();
        DontDestroyOnLoad(go);
    }

    /// <summary>First spawn of a new save: waits for the world to be playable, then opens the booklet.</summary>
    public static void ShowIntro() { Install(); inst.StartCoroutine(inst.IntroRoutine()); }

    // ---------------------------------------------------------------- style (same palette as the resume desk / inventory)
    static readonly Color Ink = new Color(0.1f, 0.2f, 0.65f), Dark = new Color(0.15f, 0.12f, 0.2f), Orange = new Color(1f, 0.6f, 0.15f);
    static readonly Color Cream = new Color(1f, 1f, 0.95f), Yellow = new Color(1f, 0.886f, 0.478f), Pink = new Color(1f, 0.54f, 0.5f);
    static readonly Color Muted = new Color(0.15f, 0.12f, 0.2f, 0.6f);
    static readonly Color[] TabCols = { new Color(1f, 0.9f, 0.45f), new Color(1f, 0.62f, 0.62f) };
    static readonly string[] TabNames = { "CONTROLS", "GOALS" };

    Font F { get { return SaveSystem.UiFont != null ? SaveSystem.UiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } }

    bool open; int spread;
    GameObject canvasGo, btnCanvas;
    RectTransform book, leftPage, rightPage;
    Text counter; Image prevImg, nextImg; Button prevBtn, nextBtn;
    Image[] tabImgs = new Image[2]; RectTransform[] tabRts = new RectTransform[2];
    float prevScale = 1f; CursorLockMode prevLock; bool prevVisible;
    readonly List<MonoBehaviour> frozen = new List<MonoBehaviour>();

    static bool InWorld() { return SaveSystem.InGame && SceneManager.GetActiveScene().name == WorldLauncher.WorldSceneName; }

    // ---------------------------------------------------------------- flow
    IEnumerator IntroRoutine()
    {
        float guard = 0f;
        while ((!InWorld() || WorldLauncher.Launching || WorldLauncher.Player == null) && guard < 30f) { guard += Time.unscaledDeltaTime; yield return null; }
        float t = 0f;
        while (t < 1.4f) { t += Time.unscaledDeltaTime; yield return null; }   // let the loading screen fade out first
        if (!open && InWorld()) Open(0);
    }

    void Update()
    {
        bool wantBtn = InWorld() && !open && Items.InventoryUI.IsOpen;
        if (wantBtn && btnCanvas == null) BuildButton();
        if (btnCanvas != null && btnCanvas.activeSelf != wantBtn) btnCanvas.SetActive(wantBtn);
        if (!open) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame) Close();
        else if (kb.rightArrowKey.wasPressedThisFrame) Turn(1);
        else if (kb.leftArrowKey.wasPressedThisFrame) Turn(-1);
    }

    void OnDisable() { if (open) Close(); }

    public static void OpenFromUI() { if (inst != null) inst.Open(0); }

    void Open(int page)
    {
        if (open || !InWorld()) return;
        if (canvasGo == null) BuildBook();
        open = true;
        prevScale = Time.timeScale > 0f ? Time.timeScale : 1f; prevLock = Cursor.lockState; prevVisible = Cursor.visible;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        FreezePlayer(true);
        canvasGo.SetActive(true);
        ShowSpread(page);
        StartCoroutine(PopIn());
    }

    void Close()
    {
        if (!open) return;
        open = false; closeFrame = Time.frameCount;
        if (canvasGo != null) canvasGo.SetActive(false);
        Time.timeScale = prevScale;
        FreezePlayer(false);
        if (Items.InventoryUI.IsOpen) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }   // back to the folio
        else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }

    // same idea as the pause menu: player scripts + look scripts sleep while the book is open
    void FreezePlayer(bool on)
    {
        if (on)
        {
            frozen.Clear();
            Transform p = SaveSystem.FindPlayer();
            if (p == null) return;
            foreach (var b in p.GetComponents<MonoBehaviour>()) if (b != null && b.enabled) { b.enabled = false; frozen.Add(b); }
            foreach (var pi in p.GetComponentsInChildren<PlayerInput>()) if (pi != null && pi.enabled) { pi.enabled = false; frozen.Add(pi); }
            foreach (var b in p.GetComponentsInChildren<MonoBehaviour>())
            {
                if (b == null || !b.enabled) continue;
                string n = b.GetType().Name;
                if (n.IndexOf("Look", System.StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("MouseOrbit", System.StringComparison.OrdinalIgnoreCase) >= 0) { b.enabled = false; frozen.Add(b); }
            }
        }
        else { foreach (var b in frozen) if (b != null) b.enabled = true; frozen.Clear(); }
    }

    IEnumerator PopIn()
    {
        float t = 0f;
        while (t < 0.3f && open)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.3f), s = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);   // ease-out-back
            book.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(s));
            yield return null;
        }
        if (book != null) book.localScale = Vector3.one;
    }

    void Turn(int d) { int to = Mathf.Clamp(spread + d, 0, 1); if (to != spread) ShowSpread(to); }

    // ---------------------------------------------------------------- UI helpers
    RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    Image Box(string name, Transform parent, Color c, Vector2 pos, Vector2 size, float outline = 0f, bool shadow = false, bool raycast = false)
    {
        var r = Rect(name, parent, pos, size);
        var im = r.gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = raycast;
        if (outline > 0f) UIKit.Boxy(im, outline, shadow);
        return im;
    }

    Text Txt(Transform parent, string s, int size, Color c, Vector2 pos, Vector2 box, TextAnchor a = TextAnchor.MiddleCenter, bool bold = false, bool fit = false)
    {
        var r = Rect("Text", parent, pos, box);
        var t = r.gameObject.AddComponent<Text>(); t.font = F; t.text = s; t.fontSize = size; t.color = c; t.alignment = a; t.raycastTarget = false;
        t.supportRichText = true; if (bold) t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        if (fit) { t.resizeTextForBestFit = true; t.resizeTextMinSize = Mathf.Max(10, size - 8); t.resizeTextMaxSize = size; t.verticalOverflow = VerticalWrapMode.Truncate; }
        return t;
    }

    Button MakeButton(string name, Transform parent, Color c, Vector2 pos, Vector2 size, string label, int fs, UnityEngine.Events.UnityAction click, out Image img)
    {
        img = Box(name, parent, c, pos, size, 3f, true, true);
        var b = img.gameObject.AddComponent<Button>();
        var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1f, .96f, .8f, 1f); cb.pressedColor = new Color(.8f, .8f, .8f, 1f); cb.fadeDuration = .05f; b.colors = cb;
        b.onClick.AddListener(() => { UiClick.Play(); });
        b.onClick.AddListener(click);
        Txt(img.transform, label, fs, Dark, Vector2.zero, size, TextAnchor.MiddleCenter, true);
        return b;
    }

    static void EnsureEventSystem()
    {
        var es = Object.FindAnyObjectByType<EventSystem>();
        if (es == null) es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        var legacy = es.GetComponent<StandaloneInputModule>(); if (legacy != null) WorldUtil.Kill(legacy);
        var m = es.GetComponent<InputSystemUIInputModule>();
        if (m == null) { m = es.gameObject.AddComponent<InputSystemUIInputModule>(); m.AssignDefaultActions(); }
    }

    GameObject NewCanvas(string name, int order)
    {
        var cg = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cg.transform.SetParent(transform, false);
        var cv = cg.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = order;
        var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = .5f;
        return cg;
    }

    // ---------------------------------------------------------------- inventory button (bottom-left, outside the folio)
    void BuildButton()
    {
        EnsureEventSystem();
        btnCanvas = NewCanvas("BookletButtonCanvas", 126);
        var root = Rect("Anchor", btnCanvas.transform, Vector2.zero, new Vector2(230, 70));
        root.anchorMin = root.anchorMax = Vector2.zero; root.pivot = Vector2.zero; root.anchoredPosition = new Vector2(28, 28);
        root.localRotation = Quaternion.Euler(0, 0, 2f);
        Image img;
        var b = MakeButton("BookletButton", root, Yellow, new Vector2(115, 35), new Vector2(230, 70), "", 1, () => Open(0), out img);
        Destroy(img.transform.GetChild(0).gameObject);
        var icon = Box("Icon", img.transform, Orange, new Vector2(-82, 0), new Vector2(46, 46), 3f);
        Txt(icon.transform, "?", 34, Dark, Vector2.zero, new Vector2(46, 46), TextAnchor.MiddleCenter, true);
        Txt(img.transform, "BOOKLET", 26, Dark, new Vector2(24, 10), new Vector2(150, 34), TextAnchor.MiddleCenter, true);
        Txt(img.transform, "how to play", 15, Muted, new Vector2(24, -16), new Vector2(150, 22));
    }

    // ---------------------------------------------------------------- book
    void BuildBook()
    {
        EnsureEventSystem();
        canvasGo = NewCanvas("BookletCanvas", 130);
        Box("Dim", canvasGo.transform, new Color(.08f, .06f, .14f, .72f), Vector2.zero, new Vector2(4000, 4000), 0f, false, true);
        book = Rect("Book", canvasGo.transform, new Vector2(0, 8), new Vector2(1040, 600));

        // cover, spine and the two taped pages
        var cover = Box("Cover", book, new Color(1f, .6f, .15f), Vector2.zero, new Vector2(1040, 600), 4f, true, true);
        leftPage = PageRect("LeftPage", new Vector2(-250, 0), 0.6f);
        rightPage = PageRect("RightPage", new Vector2(250, 0), -0.6f);
        Box("Spine", book, new Color(.15f, .12f, .2f, .85f), Vector2.zero, new Vector2(14, 570));
        UIKit.Tape(book, -330f, -4f); UIKit.Tape(book, 330f, 3f);

        // side tabs (click to jump to a spread)
        for (int i = 0; i < 2; i++)
        {
            int idx = i; Image ti;
            var b = MakeButton("Tab" + i, book, TabCols[i], new Vector2(552, 160 - i * 78), new Vector2(104, 62), TabNames[i], 15, () => ShowSpread(idx), out ti);
            tabImgs[i] = ti; tabRts[i] = ti.rectTransform;
            tabRts[i].SetSiblingIndex(0);   // tuck behind the cover
        }

        // close (X)
        Image xi;
        MakeButton("Close", book, Pink, new Vector2(506, 276), new Vector2(54, 54), "X", 34, Close, out xi);

        // page turn arrows + counter
        prevBtn = MakeButton("Prev", book, Orange, new Vector2(-70, -282), new Vector2(60, 44), "<", 28, () => Turn(-1), out prevImg);
        nextBtn = MakeButton("Next", book, Orange, new Vector2(70, -282), new Vector2(60, 44), ">", 28, () => Turn(1), out nextImg);
        counter = Txt(book, "", 22, Dark, new Vector2(0, -282), new Vector2(80, 36), TextAnchor.MiddleCenter, true);

        Txt(canvasGo.transform, "ESC  to close   |   arrow keys  or  < >  to turn the page", 18, new Color(1f, 1f, .95f, .8f), new Vector2(0, -348), new Vector2(800, 30));
        canvasGo.SetActive(false);
    }

    RectTransform PageRect(string name, Vector2 pos, float rot)
    {
        var im = Box(name, book, Cream, pos, new Vector2(488, 566), 4f, true);
        im.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
        return im.rectTransform;
    }

    static void ClearChildren(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject); }

    void ShowSpread(int s)
    {
        spread = Mathf.Clamp(s, 0, 1);
        ClearChildren(leftPage); ClearChildren(rightPage);
        if (spread == 0) { ControlsMovement(leftPage); ControlsInteraction(rightPage); }
        else { ObjectiveReach(leftPage); ObjectiveHelp(rightPage); }
        Txt(leftPage, (spread * 2 + 1).ToString(), 20, Muted, new Vector2(0, -258), new Vector2(60, 24), TextAnchor.MiddleCenter, true);
        Txt(rightPage, (spread * 2 + 2).ToString(), 20, Muted, new Vector2(0, -258), new Vector2(60, 24), TextAnchor.MiddleCenter, true);
        counter.text = (spread + 1) + " / 2";
        prevImg.color = spread == 0 ? new Color(1f, .6f, .15f, .35f) : Orange; nextImg.color = spread == 1 ? new Color(1f, .6f, .15f, .35f) : Orange;
        prevBtn.interactable = spread > 0; nextBtn.interactable = spread < 1;
        for (int i = 0; i < 2; i++)
        {
            tabImgs[i].color = i == spread ? Color.Lerp(TabCols[i], Color.white, .35f) : TabCols[i];
            tabRts[i].anchoredPosition = new Vector2(i == spread ? 566f : 552f, 160 - i * 78);
        }
    }

    // ---------------------------------------------------------------- page building blocks
    void Header(RectTransform page, string title, Color c)
    {
        var h = Box("Header", page, c, new Vector2(0, 232), new Vector2(400, 62), 3f);
        Txt(h.transform, title, 36, Dark, Vector2.zero, new Vector2(390, 58), TextAnchor.MiddleCenter, true);
    }

    void Key(Transform parent, string k, float x, float y, out float w)
    {
        w = Mathf.Max(34f, 14f + 11f * k.Length);
        var cap = Box("Key", parent, Cream, new Vector2(x + w * 0.5f, y), new Vector2(w, 38), 3f, true);
        Txt(cap.transform, k, 18, Dark, new Vector2(0, 1), new Vector2(w, 34), TextAnchor.MiddleCenter, true);
    }

    /// <summary>keys like "SHIFT","+","C","/","CTRL": "+" and "/" are drawn as plain separators.</summary>
    void Row(RectTransform page, float y, string[] keys, string desc, string sub = null)
    {
        float x = -224f;
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == "+" || keys[i] == "/") { Txt(page, keys[i], 22, Dark, new Vector2(x + 8f, y), new Vector2(16, 30), TextAnchor.MiddleCenter, true); x += 17f; continue; }
            float w; Key(page, keys[i], x, y, out w); x += w + 3f;
        }
        if (sub == null) Txt(page, desc, 23, Ink, new Vector2(112, y), new Vector2(236, 48), TextAnchor.MiddleLeft, false, true);
        else
        {
            Txt(page, desc, 22, Ink, new Vector2(112, y + 10), new Vector2(236, 28), TextAnchor.MiddleLeft, false, true);
            Txt(page, sub, 14, Muted, new Vector2(112, y - 13), new Vector2(236, 22), TextAnchor.MiddleLeft, false, true);
        }
        Box("Rule", page, new Color(.15f, .12f, .2f, .12f), new Vector2(0, y - 29), new Vector2(440, 2));
    }

    void Note(RectTransform page, string text, Color c, Vector2 pos, Vector2 size, float rot)
    {
        var n = Box("Note", page, c, pos, size, 3f, true);
        n.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
        Txt(n.transform, text, 19, Dark, Vector2.zero, size - new Vector2(22, 10), TextAnchor.MiddleCenter, true, true);
    }

    void Para(RectTransform page, string head, string body, float y, float h, Color headCol)
    {
        Txt(page, head, 26, headCol, new Vector2(0, y), new Vector2(430, 32), TextAnchor.MiddleLeft, true);
        Txt(page, body, 21, Ink, new Vector2(0, y - 20 - h * 0.5f), new Vector2(430, h), TextAnchor.UpperLeft, false, true);
    }

    // ---------------------------------------------------------------- spread 1: controls
    void ControlsMovement(RectTransform p)
    {
        Header(p, "MOVEMENT", new Color(.8f, .7f, 1f));
        Row(p, 150, new[] { "W", "A", "S", "D" }, "Move around");
        Row(p, 86, new[] { "SPACE" }, "Jump");
        Row(p, 22, new[] { "C", "/", "CTRL" }, "Crouch");
        Row(p, -42, new[] { "SHIFT" }, "Sprint");
        Row(p, -106, new[] { "SHIFT", "+", "C", "/", "CTRL" }, "Slide", "sprint first, then crouch");
        Note(p, "TIP: sprint into a slide to dash under low gaps!", Yellow, new Vector2(0, -190), new Vector2(330, 62), -2f);
    }

    void ControlsInteraction(RectTransform p)
    {
        Header(p, "INTERACTION", new Color(1f, .9f, .45f));
        Row(p, 168, new[] { "E" }, "Interact / talk / open doors");
        Row(p, 122, new[] { "P" }, "Sit down / stand up");
        Row(p, 76, new[] { "Q" }, "Feed the birds");
        Row(p, 30, new[] { "F" }, "Equip / unequip last item");
        Row(p, -16, new[] { "G" }, "Drop the equipped item");
        Row(p, -62, new[] { "DBL-CLICK" }, "Hold / unequip item", "on an item in the inventory");
        Row(p, -108, new[] { "R-CLICK" }, "Item actions", "on an item in the inventory");
        Row(p, -154, new[] { "TAB" }, "Open your inventory");
        Row(p, -200, new[] { "ESC" }, "Pause menu");
    }

    // ---------------------------------------------------------------- spread 2: objectives
    void ObjectiveReach(RectTransform p)
    {
        Header(p, "YOUR GOAL", new Color(1f, .62f, .62f));
        // little cartoon of the office door with its golden glow
        var scene = Box("Scene", p, new Color(.62f, .88f, 1f), new Vector2(0, 134), new Vector2(380, 130), 3f);
        Box("Building", scene.transform, new Color(.8f, .8f, .86f), new Vector2(0, -8), new Vector2(210, 112), 3f);
        var glow = Box("Glow", scene.transform, new Color(1f, .82f, .2f, .45f), new Vector2(0, -20), new Vector2(92, 98));
        var o = glow.gameObject.AddComponent<Outline>(); o.effectColor = new Color(1f, .85f, .2f, .6f); o.effectDistance = new Vector2(6, -6);
        Box("Door", scene.transform, new Color(.55f, .32f, .15f), new Vector2(0, -24), new Vector2(54, 82), 3f);
        Box("Knob", scene.transform, new Color(1f, .85f, .3f), new Vector2(14, -26), new Vector2(8, 8), 2f);
        for (int i = 0; i < 4; i++)
        {
            var st = Box("Spark", scene.transform, new Color(1f, .92f, .4f), new Vector2(i % 2 == 0 ? -62 + i * 6 : 62 - i * 6, 20 + i * 12), new Vector2(11, 11), 2f);
            st.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
        }
        Para(p, "REACH THE OFFICE", "Find the building whose door frame <b>glows golden</b> - its warm light shows you the way. Walk up to it and press <b>[E]</b> to start your interview.", 50, 92, Orange);
        Para(p, "WATCH THE CLOCK", "On <b>HARD</b> and <b>EXPERT</b> a punctuality timer is running - open your inventory <b>[TAB]</b> to see it. Arriving very early or very late changes how the interviewer treats you.", -78, 110, Orange);
    }

    void ObjectiveHelp(RectTransform p)
    {
        Header(p, "HELP OTHERS", new Color(.6f, .9f, .6f));
        // cartoon speech bubbles + heart
        var b1 = Box("Bubble1", p, Cream, new Vector2(-70, 150), new Vector2(190, 70), 3f, true); b1.rectTransform.localRotation = Quaternion.Euler(0, 0, 2f);
        Txt(b1.transform, "Help! I need a hand!", 20, Ink, Vector2.zero, new Vector2(176, 60), TextAnchor.MiddleCenter, true, true);
        var b2 = Box("Bubble2", p, Yellow, new Vector2(80, 100), new Vector2(190, 56), 3f, true); b2.rectTransform.localRotation = Quaternion.Euler(0, 0, -2f);
        Txt(b2.transform, "Sure! Press E", 20, Dark, Vector2.zero, new Vector2(176, 50), TextAnchor.MiddleCenter, true, true);
        var heart = Box("Heart", p, Pink, new Vector2(176, 160), new Vector2(46, 46), 3f); heart.rectTransform.localRotation = Quaternion.Euler(0, 0, 8f);
        Txt(heart.transform, "+", 34, Dark, Vector2.zero, new Vector2(46, 46), TextAnchor.MiddleCenter, true);
        Para(p, "LEND A HAND", "Walk up to people around the city and press <b>[E]</b>. Some of them are in a pickle - <b>help them out</b> and you will get <b>something in return</b>.", 40, 92, Orange);
        Para(p, "KEEP YOUR EYES OPEN", "Useful items are lying around too. Pick them up, check your inventory <b>[TAB]</b> and equip what you need with <b>[F]</b> - it might save you in the interview.", -78, 110, Orange);
    }
}
