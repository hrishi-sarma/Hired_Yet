using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using EasyPeasyFirstPersonController;
using UnityEngine.UI;
using TMPro;

/// <summary>Cartoon on-screen panel for the Timing bar, the Sequence arrows and the Choice / Sorting questions. Built on demand, one instance.
/// Handlers push state into it each tick; <see cref="WorldEvent"/> hides it when the task ends or is cancelled. Same look as the interview room (paper cards, ink outlines, key caps).</summary>
public class EventPanelUI : MonoBehaviour
{
    const float W = 860f, BarW = 700f, BarH = 46f;
    const int Cells = 6;
    static EventPanelUI inst;
    static readonly Regex NumRx = new Regex(@"^(\d)\.\s+(.*)$");
    static readonly Color[] OptColors = { Toon.Orange, Toon.Blue, Toon.Pink, Toon.Mint };

    static FirstPersonController frozenCtl;
    static CursorLockMode prevLock;
    static bool prevVisible, inputLocked;
    public static bool InputLock { get { return inputLocked; } }

    static void LockInput()
    {
        if (inputLocked) return;
        inputLocked = true;
        prevLock = Cursor.lockState; prevVisible = Cursor.visible;
        frozenCtl = Object.FindFirstObjectByType<FirstPersonController>();
        if (frozenCtl != null && frozenCtl.enabled) frozenCtl.enabled = false; else frozenCtl = null;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    static void UnlockInput()
    {
        if (!inputLocked) return;
        inputLocked = false;
        if (frozenCtl != null) frozenCtl.enabled = true;
        frozenCtl = null;
        Cursor.lockState = prevLock;
        Cursor.visible = prevVisible;
    }

    public static int ClickedOption(int max)
    {
        Mouse m = Mouse.current;
        if (inst == null || m == null || !m.leftButton.wasPressedThisFrame) return -1;
        Vector2 pos = m.position.ReadValue();
        for (int i = 0; i < max && i < inst.optRow.Length; i++)
            if (inst.optRow[i].gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(inst.optRow[i], pos, null)) return i;
        return -1;
    }

    public static bool Visible { get { return inst != null && inst.root != null && inst.root.gameObject.activeSelf; } }

    RectTransform root, timing, sequence, choice, zone, pointer, timeFill, timeBack, hintKey, hintKeyFront;
    Image rootBg, swatch, barBg, headIcon;
    TextMeshProUGUI title, info, qTxt;
    TextMeshProUGUI[] optTxt = new TextMeshProUGUI[4], optNum = new TextMeshProUGUI[4];
    RectTransform[] optRow = new RectTransform[4];
    Image[] pips = new Image[0];
    Image[] cells = new Image[Cells], arrows = new Image[Cells];
    RectTransform[] cellBack = new RectTransform[Cells];
    float flash, t0;
    static readonly Color Cream = Toon.Cream;

    // ---------- public API ----------
    public static void ShowTiming(string heading, int need)
    {
        EventPanelUI p = Get();
        p.Head(heading, "bolt");
        p.sequence.gameObject.SetActive(false);
        p.choice.gameObject.SetActive(false);
        p.SetSize(270f);
        p.timing.gameObject.SetActive(true);
        p.Hint(true);
        p.BuildPips(need);
        p.root.gameObject.SetActive(true);
    }

    public static void SetTiming(float ptr, float zMin, float zMax, int hits, float timeFrac, string text)
    {
        if (inst == null) return;
        EventPanelUI p = inst;
        p.pointer.anchoredPosition = new Vector2(Mathf.Clamp01(ptr) * BarW, 0f);
        p.zone.anchoredPosition = new Vector2(zMin * BarW, 0f);
        p.zone.sizeDelta = new Vector2(Mathf.Max(8f, (zMax - zMin) * BarW), BarH - 14f);
        for (int i = 0; i < p.pips.Length; i++)
        {
            bool on = i < hits;
            p.pips[i].color = on ? Toon.Yellow : new Color(0.15f, 0.12f, 0.2f, 0.28f);
            p.pips[i].rectTransform.localScale = Vector3.one * (on ? 1.12f : 0.9f);
        }
        p.SetTime(timeFrac);
        p.info.text = text;
    }

    public static void Pulse(bool hit)
    {
        if (inst != null) inst.flash = hit ? 1f : -1f;
    }

    public static void ShowSequence(string heading)
    {
        EventPanelUI p = Get();
        p.Head(heading, "arrow");
        p.timing.gameObject.SetActive(false);
        p.choice.gameObject.SetActive(false);
        p.SetSize(270f);
        p.sequence.gameObject.SetActive(true);
        p.Hint(false);
        p.root.gameObject.SetActive(true);
    }

    // arrow ids: 0 up, 1 down, 2 left, 3 right
    public static void SetSequence(int[] seq, int step, float timeFrac, string text)
    {
        if (inst == null || seq == null) return;
        EventPanelUI p = inst;
        float[] rot = { 0f, 180f, 90f, -90f };
        for (int i = 0; i < Cells; i++)
        {
            int idx = step + i;
            bool has = idx < seq.Length;
            p.cellBack[i].gameObject.SetActive(has);
            if (!has) continue;
            bool cur = i == 0;
            Color flashC = p.flash < 0f ? Toon.Red : Toon.Mint;
            p.cells[i].color = cur ? Color.Lerp(Toon.Yellow, flashC, Mathf.Abs(p.flash)) : Cream;
            p.arrows[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot[seq[idx]]);
            p.arrows[i].color = cur ? Toon.Orange : Color.Lerp(Toon.Blue, Color.white, 0.2f);
            float bob = cur ? Mathf.Sin(Time.unscaledTime * 8f) * 4f : 0f;
            p.cellBack[i].anchoredPosition = new Vector2((i - (Cells - 1) * 0.5f) * 104f + (cur ? -6f : 0f), 4f + bob);
            p.cellBack[i].localScale = Vector3.one * (cur ? 1.22f : 0.88f);
        }
        p.SetTime(timeFrac);
        p.info.text = text;
    }

    public static void ShowChoice(string heading, bool withSwatch)
    {
        EventPanelUI p = Get();
        p.Head(heading, "question");
        p.timing.gameObject.SetActive(false);
        p.sequence.gameObject.SetActive(false);
        p.SetSize(468f);
        p.choice.gameObject.SetActive(true);
        p.Hint(false);
        p.swatch.gameObject.SetActive(withSwatch);
        p.root.gameObject.SetActive(true);
        LockInput();
    }

    public static void SetChoice(string question, string[] options, bool withSwatch, Color swatchColor, float timeFrac, string text)
    {
        if (inst == null) return;
        EventPanelUI p = inst;
        p.qTxt.text = question;
        p.swatch.gameObject.SetActive(withSwatch);
        p.swatch.color = swatchColor;
        RectTransform qr = p.qTxt.rectTransform;
        qr.anchoredPosition = new Vector2(withSwatch ? 44f : 0f, qr.anchoredPosition.y);
        qr.sizeDelta = new Vector2(withSwatch ? 640f : 760f, qr.sizeDelta.y);
        int n = 0;
        for (int i = 0; i < p.optTxt.Length; i++)
        {
            bool has = options != null && i < options.Length;
            p.optRow[i].gameObject.SetActive(has);
            if (!has) continue;
            n++;
            string o = options[i];
            Match m = NumRx.Match(o);
            p.optNum[i].text = m.Success ? m.Groups[1].Value : (i + 1).ToString();
            p.optTxt[i].text = m.Success ? m.Groups[2].Value : o;
        }
        p.SetSize(194f + 58f * Mathf.Max(0, n - 1) + 24f + 100f);
        p.SetTime(timeFrac);
        p.info.text = text;
    }

    public static void Hide()
    {
        UnlockInput();
        if (inst != null) inst.root.gameObject.SetActive(false);
    }

    // ---------- build ----------
    static EventPanelUI Get()
    {
        if (inst == null)
        {
            GameObject go = new GameObject("EventPanel");
            inst = go.AddComponent<EventPanelUI>();
            inst.Build();
        }
        return inst;
    }

    static RectTransform R(string n, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        RectTransform r = UIKit.NewRect(n, parent);
        UIKit.Place(r, anchor, pivot, pos, size);
        return r;
    }

    static Image Img(string n, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c, Sprite sp = null)
    {
        return UIKit.AddImage(R(n, parent, anchor, pivot, pos, size), sp, c);
    }

    TextMeshProUGUI Txt(string n, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, float fs, Color c)
    {
        InteractionSystem sys = InteractionSystem.Instance;
        TextMeshProUGUI t = UIKit.AddText(parent, n, "", fs, c, TextAlignmentOptions.Center, sys != null ? sys.font : null, FontStyles.Bold, 1f);
        UIKit.Place(t.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, size);
        return t;
    }

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99;
        CanvasScaler sc = gameObject.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        InteractionSystem sys = InteractionSystem.Instance;
        TMP_FontAsset font = sys != null ? sys.font : null;

        Vector2 top = new Vector2(0.5f, 1f), mid = new Vector2(0.5f, 0.5f), bot = new Vector2(0.5f, 0f);
        root = R("Panel", transform, top, top, new Vector2(0f, -86f), new Vector2(W, 270f));
        rootBg = Toon.Card(root, Cream, 22, 5f, true);
        root.localRotation = Quaternion.Euler(0f, 0f, -0.6f);
        UIKit.Tape(root, -370f, -4f);
        UIKit.Tape(root, 370f, 3f);

        // header sticker: icon badge + title on an orange pill straddling the top edge
        RectTransform head = R("Header", root, top, mid, new Vector2(0f, -4f), new Vector2(520f, 60f));
        Toon.Card(head, Toon.Orange, 18, 4f, true);
        RectTransform hb = R("Badge", head, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(42f, 42f));
        Toon.Card(hb, Cream, 21, 3f, false);
        headIcon = Img("Icon", hb, mid, mid, Vector2.zero, new Vector2(30f, 30f), Color.white, ToonIcons.Get("bolt"));
        title = Txt("Title", head, mid, new Vector2(26f, 0f), new Vector2(440f, 52f), 28f, Toon.Ink);
        title.enableAutoSizing = true; title.fontSizeMin = 16f; title.fontSizeMax = 28f;

        info = Txt("Info", root, bot, new Vector2(0f, 30f), new Vector2(420f, 40f), 26f, Toon.Ink);
        RectTransform kb;
        hintKeyFront = Toon.KeyCap(root, "HintKey", "E", 40f, font, Cream, out kb);
        hintKey = kb;
        UIKit.Place(hintKey, bot, mid, new Vector2(-120f, 30f), new Vector2(40f, 40f));

        // timing: dark track, mint zone, pointer with a little arrow, star pips
        timing = R("Timing", root, mid, mid, new Vector2(0f, 10f), new Vector2(BarW + 40f, 130f));
        barBg = Img("Bar", timing, mid, mid, new Vector2(0f, 14f), new Vector2(BarW + 28f, BarH), new Color(0.2f, 0.16f, 0.28f, 1f), UIKit.Rounded(23));
        UIKit.Boxy(barBg, 4f, false);
        RectTransform lane = R("Lane", barBg.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-BarW * 0.5f, 0f), new Vector2(BarW, BarH));
        RectTransform zr = R("Zone", lane, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(100f, BarH - 14f));
        Toon.Card(zr, Toon.Mint, 12, 3f, false);
        zone = zr;
        RectTransform pr = R("Pointer", lane, new Vector2(0f, 0.5f), mid, Vector2.zero, new Vector2(14f, BarH + 18f));
        Toon.Card(pr, Cream, 7, 3f, false);
        pointer = pr;
        RectTransform pa = R("PointerArrow", pr, top, new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(34f, 34f));
        pa.localRotation = Quaternion.Euler(0f, 0f, 180f);
        UIKit.AddImage(pa, ToonIcons.Get("arrow"), Toon.Orange);

        // sequence: key caps with arrows
        sequence = R("Sequence", root, mid, mid, new Vector2(0f, 8f), new Vector2(BarW + 40f, 110f));
        for (int i = 0; i < Cells; i++)
        {
            RectTransform back;
            RectTransform front = Toon.KeyCap(sequence, "Cell" + i, "", 78f, font, Cream, out back);
            back.anchorMin = back.anchorMax = mid; back.pivot = mid;
            cellBack[i] = back;
            cells[i] = front.GetComponent<Image>();
            arrows[i] = Img("Arrow", front, mid, mid, Vector2.zero, new Vector2(52f, 52f), Toon.Orange, ToonIcons.Get("arrow"));
        }

        // choice: question + numbered answer pills
        choice = R("Choice", root, top, top, Vector2.zero, new Vector2(W, 420f));
        qTxt = Txt("Question", choice, top, new Vector2(0f, -100f), new Vector2(760f, 100f), 30f, Toon.Ink);
        qTxt.enableAutoSizing = true; qTxt.fontSizeMin = 20f; qTxt.fontSizeMax = 30f;
        swatch = Img("Swatch", choice, top, mid, new Vector2(-340f, -100f), new Vector2(64f, 64f), Color.white, UIKit.Rounded(16));
        UIKit.Boxy(swatch, 4f, true);
        for (int i = 0; i < 4; i++)
        {
            RectTransform row = R("Option" + i, choice, top, mid, new Vector2(0f, -170f - i * 58f), new Vector2(760f, 50f));
            Toon.Card(row, OptColors[i], 16, 3f, true);
            optRow[i] = row;
            RectTransform nb = R("Num", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(9f, 0f), new Vector2(34f, 34f));
            Toon.Card(nb, Cream, 17, 3f, false);
            optNum[i] = Txt("N", nb, mid, Vector2.zero, new Vector2(34f, 34f), 22f, Toon.Ink);
            optTxt[i] = Txt("Text", row, mid, new Vector2(24f, 0f), new Vector2(680f, 46f), 26f, Toon.Ink);
            optTxt[i].alignment = TextAlignmentOptions.MidlineLeft;
            optTxt[i].rectTransform.anchoredPosition = new Vector2(24f, 0f);
            optTxt[i].enableAutoSizing = true; optTxt[i].fontSizeMin = 16f; optTxt[i].fontSizeMax = 26f;
        }
        choice.gameObject.SetActive(false);

        timeBack = R("TimeBack", root, bot, bot, new Vector2(0f, 62f), new Vector2(BarW, 18f));
        UIKit.Boxy(UIKit.AddImage(timeBack, UIKit.Rounded(9), new Color(0.2f, 0.16f, 0.28f, 1f)), 3f, false);
        timeFill = R("TimeFill", timeBack, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(BarW, 18f));
        UIKit.AddImage(timeFill, UIKit.Rounded(9), Toon.Orange);
        root.gameObject.SetActive(false);
    }

    void Head(string heading, string icon)
    {
        title.text = heading != null ? heading.ToUpperInvariant() : "";
        headIcon.sprite = ToonIcons.Get(icon);
        t0 = Time.unscaledTime;
    }

    void Hint(bool key)
    {
        hintKey.gameObject.SetActive(key);
        info.alignment = key ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
        info.rectTransform.anchoredPosition = new Vector2(key ? -80f : 0f, 30f);
        info.rectTransform.sizeDelta = new Vector2(key ? 300f : 420f, 40f);
    }

    void BuildPips(int need)
    {
        for (int i = 0; i < pips.Length; i++) if (pips[i] != null) WorldUtil.Kill(pips[i].gameObject);
        pips = new Image[Mathf.Max(1, need)];
        for (int i = 0; i < pips.Length; i++)
            pips[i] = Img("Pip" + i, timing, new Vector2(0.5f, 0f), mid(), new Vector2((i - (pips.Length - 1) * 0.5f) * 42f, 18f), new Vector2(36f, 36f), new Color(0.15f, 0.12f, 0.2f, 0.28f), ToonIcons.Get("star"));
    }

    static Vector2 mid() { return new Vector2(0.5f, 0.5f); }

    void SetSize(float h)
    {
        root.sizeDelta = new Vector2(W, h);
        choice.sizeDelta = new Vector2(W, h);
    }

    void SetTime(float f)
    {
        timeBack.gameObject.SetActive(f >= 0f);
        if (f < 0f) return;
        f = Mathf.Clamp01(f);
        timeFill.sizeDelta = new Vector2(BarW * f, 18f);
        timeFill.GetComponent<Image>().color = f < 0.25f ? Toon.Red : Toon.Orange;
    }

    void Update()
    {
        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 2.5f);
        if (rootBg != null) rootBg.color = Color.Lerp(Cream, flash < 0f ? Toon.Red : Toon.Mint, Mathf.Abs(flash) * 0.6f);
        if (barBg != null) barBg.color = Color.Lerp(new Color(0.2f, 0.16f, 0.28f, 1f), flash < 0f ? Toon.Red : Toon.Mint, Mathf.Abs(flash) * 0.5f);
        if (root != null && root.gameObject.activeSelf)
        {
            float pop = Toon.OutBack((Time.unscaledTime - t0) * 4f);
            float s = Mathf.LerpUnclamped(0.8f, 1f, pop);
            root.localScale = new Vector3(s, s, 1f);
            if (hintKey.gameObject.activeSelf)
            {
                UnityEngine.InputSystem.Keyboard k = UnityEngine.InputSystem.Keyboard.current;
                bool held = k != null && k.eKey.isPressed;
                hintKeyFront.anchoredPosition = new Vector2(0f, held ? -5f : Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 4f)) * 2f);
            }
        }
    }

    void OnDestroy()
    {
        if (inst == this) inst = null;
    }
}
