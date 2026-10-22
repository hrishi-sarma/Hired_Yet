using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

// Uses the project's real ResumeSession / JobData / Perk / PerkInfo / RiskInfo (ResumeSession.cs), so the data reaches the interview + world.

// ================= UI =================
// Attach to an empty GameObject in an empty scene. Builds everything at runtime.
// click = +/-1, hold = fast repeat
public class HoldRepeat : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Action tick;
    bool down; float next;
    public void OnPointerDown(PointerEventData e) { down = true; next = Time.unscaledTime + 0.4f; if (tick != null) tick(); }
    public void OnPointerUp(PointerEventData e) { down = false; }
    public void OnPointerExit(PointerEventData e) { down = false; }
    void Update() { if (down && Time.unscaledTime >= next) { next = Time.unscaledTime + 0.04f; if (tick != null) tick(); } }
}

public class SlotPage
{
    public int idx;
    public Transform hinge, body;
    public Material mat;
    public Canvas canvas;
    public GraphicRaycaster gr;
    public RectTransform paper;
    public InputField nameIn, hobbyIn, gradeIn;
    public int eduIdx = 2, expIdx;
    public List<Image> jobBtns = new List<Image>(), perkBtns = new List<Image>();
    public Perk[] perks;
    public JobData[] jobs;
    public string[] jobTexts;
    public int jobPick = -1, perkPick = -1;
    public Text msg, tag;
    public Image tabImg;
    public Color tabCol;
    public float yaw, xo, hl, bump, curYaw, curX;
    public List<Image> frame = new List<Image>();
    public List<GameObject> jobTicks = new List<GameObject>(), perkTicks = new List<GameObject>();
    public float pg, delay, hv, appear, appearDelay, z, y;
    // photo ID
    public int charIdx = -1, pickTmp = -1; public bool pickFemale;
    public RawImage photo; public Text photoHint; public RectTransform submitBtn, discardBtn;
    public GameObject picker; public Action pickerSync;
    public GameObject saveOverlay;   // shown instead of the blank form when this slot holds a saved game
}

public class PageTag : MonoBehaviour { public int idx; }

static class MouseIn
{
    public static Vector2 Pos()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    public static bool Down()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
}

public class ResumeFormUI : MonoBehaviour
{
    public Font handwritingFont;
    public Font labelFont;
    public string nextScene = "";
    [TextArea(3, 8)] public string creditsText = "A Job Interview Game\n\nDesign & Code: Your Name\nMade with Unity\n\nThanks for playing!";
    public static event Action Submitted;

    static bool woke;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { woke = false; }

    static readonly Color Ink = new Color(0.1f, 0.2f, 0.65f),
        Dark = new Color(0.15f, 0.12f, 0.2f), Orange = new Color(1f, 0.6f, 0.15f),
        Cream = new Color(1f, 1f, 0.95f), Sel = new Color(1f, 0.85f, 0.25f), Selp = new Color(0.45f, 0.9f, 0.4f);

    static readonly string[] Edu = { "High School", "Diploma", "Bachelor's", "Master's", "PhD" };
    static readonly int[] EduScore = { 55, 65, 75, 85, 95 };
    static readonly string[] Exp = { "None", "1-2 years", "3-5 years", "5+ years" };

    const float L = -264f, W = 528f;
    const float PaperW = 0.21f, PaperH = 0.297f, PaperThick = 0.002f, Strip = 0.002f;
    const int N = 5;
    static readonly float[] TiltYaw = { -1.2f, 2.6f, -3.1f, 3.4f, -2.2f }, TiltX = { 0f, 0.004f, -0.005f, 0.006f, -0.003f };
    static readonly Color[] TabCols = { new Color(0.8f, 0.7f, 1f), new Color(1f, 0.62f, 0.62f), new Color(1f, 0.9f, 0.45f), new Color(0.6f, 0.9f, 0.6f), new Color(0.6f, 0.78f, 1f) };

    RectTransform paper;
    Transform deskRoot;
    Camera cam;
    ResumePenHand pen;
    DrowsyView drowsy;
    PostItMenu menu;
    Canvas penRefCanvas;
    SlotPage[] slots;
    int cur;
    bool slotsActive;
    Transform backNote; float backHover;   // 3D 'MAIN MENU' note on the table
    Vector3 lampPos;
    AmbientMode oldAmbientMode; Color oldAmbient; float oldShadowDist;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        ResumeSession.EnsureLoaded();
        if (!ResumeSession.Ready) { Debug.LogError("interview_data.json missing from a Resources folder"); return; }
        ResumeSession.NewSession();
        if (labelFont == null) labelFont = handwritingFont != null ? handwritingFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (handwritingFont == null) handwritingFont = labelFont;
        if (FindFirstObjectByType<EventSystem>() == null) CreateEventSystem();
        bool fromMenu = StartMenuUI.EnteredFromMenu; StartMenuUI.EnteredFromMenu = false;   // arrived via START in the StartMenu scene
        if (!fromMenu) SaveSystem.MenuScene = SceneManager.GetActiveScene().name;           // (StartMenuUI already set MenuScene otherwise)
        SaveSystem.Active = -1; SaveSystem.Data = null; SaveSystem.Pending = null;
        SaveSystem.UiFont = handwritingFont; Time.timeScale = 1f; AudioListener.pause = false;
        BuildDesk();

        var dummy = new GameObject("Pen Ref", typeof(Canvas));
        dummy.transform.SetParent(deskRoot, false); dummy.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var dc = dummy.GetComponent<Canvas>(); dc.renderMode = RenderMode.WorldSpace; penRefCanvas = dc;
        pen = gameObject.AddComponent<ResumePenHand>();
        pen.Init(cam, dc, PaperThick + 0.0001f, deskRoot, lampPos);

        drowsy = gameObject.AddComponent<DrowsyView>(); drowsy.Init(cam);   // the sleepy-eyes intro plays every time the menu loads (also after quitting from a game)
        pen.locked = true;
        if (fromMenu) { StartGame(); return; }   // eyes open once, then the slots: no post-it menu in this scene any more
        menu = gameObject.AddComponent<PostItMenu>();
        menu.Init(handwritingFont, labelFont, StartGame, creditsText);
    }

    static void CreateEventSystem()
    {
        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    void StartGame()
    {
        woke = true;
        pen.WarpToRest();   // cursor (and so the pen hand) starts on the right, not wherever the START button was
        pen.locked = false; Cursor.visible = false;
        if (drowsy != null) drowsy.Wake();
        BuildSlots();
        BuildBackNote();
        RemoveEscHint();
        slotsActive = true;
        pen.SetCanvas(slots[0].canvas);
        ApplyCharacter(slots[0]);
    }

    void BuildDesk()
    {
        deskRoot = new GameObject("Resume Desk").transform;

        cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        if (cam == null) { cam = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); cam.tag = "MainCamera"; }
        cam.orthographic = false; cam.fieldOfView = 30f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.035f, 0.03f);
        float dist = (PaperH / 0.86f) * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        cam.transform.SetPositionAndRotation(new Vector3(0f, PaperThick + dist, 0.014f), Quaternion.Euler(90f, 0f, 0f));

        lampPos = DeskKit.RoomAndLamp(deskRoot);
        var lg = new GameObject("Desk Lamp Light").AddComponent<Light>();
        lg.transform.SetParent(deskRoot, false); lg.transform.position = lampPos;
        lg.transform.rotation = Quaternion.LookRotation(new Vector3(0f, PaperThick, 0f) - lampPos);
        lg.type = LightType.Spot; lg.spotAngle = 115f; lg.range = 1.6f;
        lg.color = new Color(1f, 0.86f, 0.62f); lg.shadows = LightShadows.Soft; lg.shadowStrength = 0.7f;
        bool srp = GraphicsSettings.currentRenderPipeline != null;
        lg.intensity = srp ? 0.2f : 1.5f;
        lg.gameObject.AddComponent<LampFlicker>();
        oldAmbientMode = RenderSettings.ambientMode; oldAmbient = RenderSettings.ambientLight; oldShadowDist = QualitySettings.shadowDistance;
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.17f, 0.14f, 0.15f);
        QualitySettings.shadowDistance = 3f;

        DeskKit.Table(deskRoot);
        DeskKit.Accessories(deskRoot, cam, PaperW * 0.5f, new System.Random(Guid.NewGuid().GetHashCode()), handwritingFont);
    }

    void BuildSlots()
    {
        Texture2D tex = DeskKit.Paper();
        slots = new SlotPage[N];
        for (int i = 0; i < N; i++)
        {
            var s = new SlotPage { idx = i, appearDelay = (N - 1 - i) * 0.12f, z = i * Strip, y = PaperThick - i * 0.0003f };
            s.yaw = s.curYaw = TiltYaw[i]; s.xo = s.curX = TiltX[i]; s.hl = i == 0 ? 1f : 0f;
            s.mat = DeskKit.Mat(Color.white, tex, 0.05f);
            s.mat.EnableKeyword("_EMISSION");
            s.hinge = new GameObject("Slot " + (i + 1)).transform; s.hinge.SetParent(deskRoot, false);
            s.body = new GameObject("Page").transform; s.body.SetParent(s.hinge, false); s.body.localPosition = new Vector3(0f, 0f, -PaperH * 0.5f);
            DeskKit.P(PrimitiveType.Cube, "Paper", s.body, s.mat, new Vector3(0f, -PaperThick * 0.5f, 0f), Vector3.zero, new Vector3(PaperW, PaperThick, PaperH));
            var hit = new GameObject("hit"); hit.transform.SetParent(s.body, false);
            var bc = hit.AddComponent<BoxCollider>(); bc.size = new Vector3(PaperW, PaperThick, PaperH); bc.center = new Vector3(0f, -PaperThick * 0.5f, 0f);
            hit.AddComponent<PageTag>().idx = i;
            {
                float sc = PaperW / 600f, tx = (L + i * 106f + 50f) * sc;
                var th = new GameObject("tabhit"); th.transform.SetParent(s.body, false);
                th.transform.localPosition = new Vector3(tx, -PaperThick * 0.5f, PaperH * 0.5f + 27f * sc);
                th.AddComponent<BoxCollider>().size = new Vector3(100f * sc, PaperThick, 54f * sc);
                th.AddComponent<PageTag>().idx = i;
            }

            var cv = new GameObject("Sheet", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cv.transform.SetParent(s.body, false);
            s.canvas = cv.GetComponent<Canvas>(); s.canvas.renderMode = RenderMode.WorldSpace; s.canvas.worldCamera = cam;
            var scaler = cv.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.dynamicPixelsPerUnit = 2.5f;
            s.gr = cv.GetComponent<GraphicRaycaster>();
            s.paper = cv.GetComponent<RectTransform>();
            s.paper.pivot = new Vector2(0.5f, 0.5f); s.paper.sizeDelta = new Vector2(600, 848);
            cv.transform.localScale = Vector3.one * (PaperW / 600f);
            cv.transform.localPosition = new Vector3(0f, 0.0001f, 0f);
            cv.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            slots[i] = s;
            BuildForm(s);
            BuildSaveOverlay(s);
            s.hinge.localPosition = new Vector3(s.xo, s.y, PaperH * 0.5f + s.z - 0.5f);
        }
    }

    // ================= SAVED GAME (each slot = one save file) =================
    void BuildSaveOverlay(SlotPage s)
    {
        var d = SaveSystem.Peek(s.idx);
        if (d == null) return;
        paper = s.paper;
        int n0 = paper.childCount;
        Box(-300f, 0f, 600f, 848f, new Color(1f, 0.98f, 0.9f, 1f));
        Label("JOB APPLICATION", L, -26, W, 52, 44, TextAnchor.MiddleCenter, Ink, true, handwritingFont);
        Box(L, -82, W, 3, Dark);
        Label("SAVED GAME", L, -96, W, 36, 28, TextAnchor.MiddleCenter, Orange, true, labelFont);
        Label("<b>" + (string.IsNullOrEmpty(d.name) ? "Candidate" : d.name) + "</b>", L, -150, W, 56, 40, TextAnchor.MiddleCenter, Ink, false, handwritingFont);
        string job = d.jobPick >= 0 && d.jobPick < d.offeredTexts.Length ? d.offeredTexts[d.jobPick] : "";
        Label("APPLYING FOR\n<b>" + job + "</b>", L, -222, W, 100, 22, TextAnchor.UpperCenter, Dark, false, labelFont);
        string where = d.stage == 0 ? "OPEN WORLD" : "INTERVIEW ROOM" + (d.attempts > 0 ? "  (retry #" + d.attempts + ")" : "");
        Label("WHERE:  <b>" + where + "</b>\nPLAYED:  <b>" + Mathf.Max(1, Mathf.RoundToInt(d.playTime / 60f)) + " min</b>\nSAVED:  <b>" + d.savedAt + "</b>", L + 20, -352, W - 40, 110, 24, TextAnchor.UpperLeft, Dark, false, labelFont);
        var info = Label("", L, -470, W, 30, 20, TextAnchor.MiddleCenter, new Color(0.85f, 0.1f, 0.15f), true, labelFont);
        Button("CONTINUE", L, -510, W, 84, Selp, Dark, 42, () =>
        {
            if (submitting) return;
            submitting = true; info.text = "Loading...";
            if (!SaveSystem.Continue(s.idx)) { submitting = false; info.text = "Save file could not be loaded!"; }
        });
        RectTransform nb = null; bool armed = false;
        nb = Button("NEW GAME  (erases this save)", L, -620, W, 64, new Color(1f, 0.62f, 0.62f), Dark, 24, () =>
        {
            if (!armed) { armed = true; nb.GetComponentInChildren<Text>().text = "CLICK AGAIN TO ERASE!"; return; }
            SaveSystem.Delete(s.idx);
            s.nameIn.text = ""; s.hobbyIn.text = ""; s.gradeIn.text = ""; SetCharacter(s, -1);
            if (s.saveOverlay != null) WorldUtil.Kill(s.saveOverlay);
        });
        var kids = new List<Transform>();
        for (int i = n0; i < paper.childCount; i++) kids.Add(paper.GetChild(i));
        var grp = new GameObject("Saved Game", typeof(RectTransform)); var gr = grp.GetComponent<RectTransform>();
        gr.SetParent(paper, false); gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one; gr.offsetMin = gr.offsetMax = Vector2.zero;
        foreach (var k in kids) k.SetParent(gr, false);
        s.saveOverlay = grp;
    }

    bool IsBusy()
    {
        for (int i = 0; i < N; i++)
        {
            var s = slots[i];
            if (s.appearDelay > 0f || s.appear < 1f || s.bump > 0.02f) return true;
        }
        return false;
    }

    void Select(int to)
    {
        if (to == cur || to < 0 || to >= N) return;
        SaveAll();
        slots[to].bump = 1f; slots[cur].bump = 0.5f;
        cur = to;
        pen.SetCanvas(slots[cur].canvas);
        ApplyCharacter(slots[cur]);
    }

    void Update()
    {
        if (!slotsActive) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        bool busy = IsBusy();
        int hover = -1; bool overBack = false;
        if (!busy)
        {
            RaycastHit h;
            if (Physics.Raycast(cam.ScreenPointToRay(MouseIn.Pos()), out h, 5f))
            {
                var tg = h.collider.GetComponent<PageTag>();
                if (tg != null) hover = tg.idx;
            }
            // the pen hand / other props can be hit first: look through all hits for the note
            foreach (var bh in Physics.RaycastAll(cam.ScreenPointToRay(MouseIn.Pos()), 5f))
                if (bh.collider.GetComponent<BackNoteTag>() != null) { overBack = true; break; }
            if (overBack && MouseIn.Down()) { BackToMenu(); return; }
        }
        if (backNote != null)
        {
            backHover = Mathf.MoveTowards(backHover, overBack ? 1f : 0f, dt * 8f);
            backNote.localScale = Vector3.one * (1f + 0.1f * backHover);
            backNote.localPosition = new Vector3(backNote.localPosition.x, backHover * 0.004f, backNote.localPosition.z);
        }
        if (hover >= 0 && hover != cur && MouseIn.Down()) Select(hover);

        float k = 1f - Mathf.Exp(-8f * dt);
        for (int j = 0; j < N; j++)
        {
            var s = slots[j];
            bool sel = j == cur;
            s.gr.enabled = !busy && sel;
            s.hv = Mathf.MoveTowards(s.hv, (j == hover && !sel) ? 1f : 0f, dt * 8f);
            s.hl = Mathf.MoveTowards(s.hl, sel ? 1f : 0f, dt * 4f);
            s.bump = Mathf.MoveTowards(s.bump, 0f, dt * 2.2f);
            int rank = sel ? 0 : (j < cur ? j + 1 : j);
            float tz = rank * Strip + s.hv * 0.004f;
            float ty = PaperThick - rank * 0.0003f + s.bump * 0.012f;
            s.z = Mathf.Lerp(s.z, tz, k); s.y = Mathf.Lerp(s.y, ty, k);
            s.curYaw = Mathf.Lerp(s.curYaw, sel ? s.yaw * 0.3f : s.yaw, k);
            s.curX = Mathf.Lerp(s.curX, sel ? s.xo * 0.3f : s.xo, k);
            if (s.appearDelay > 0f) s.appearDelay -= dt; else s.appear = Mathf.MoveTowards(s.appear, 1f, dt * 1.5f);
            float slideIn = -(1f - (1f - (1f - s.appear) * (1f - s.appear))) * 0.5f;
            s.hinge.localPosition = new Vector3(s.curX, s.y, PaperH * 0.5f + s.z + slideIn);
            s.hinge.localRotation = Quaternion.Euler(0f, s.curYaw, 0f);
            s.mat.SetColor("_EmissionColor", new Color(0.45f, 0.36f, 0.14f) * s.hv);
            if (s.tabImg != null) s.tabImg.color = Color.Lerp(Color.Lerp(s.tabCol, Color.white, s.hv * 0.5f), Sel, s.hl * 0.8f);
            Color fc = new Color(Sel.r, Sel.g, Sel.b, s.hl * 0.85f);
            for (int f = 0; f < s.frame.Count; f++) s.frame[f].color = fc;
        }
    }

    void BuildForm(SlotPage s)
    {
        paper = s.paper;
        string k = "rg_slot" + s.idx;
        s.tabCol = TabCols[s.idx];
        var tb = Box(L + s.idx * 106f, 54f, 100f, 58f, s.tabCol);
        Outline(tb.gameObject, Dark, 2);
        s.tabImg = tb.GetComponent<Image>(); s.tabImg.raycastTarget = false;
        var tt = Inner(tb, "SLOT " + (s.idx + 1), 24, TextAnchor.UpperCenter, Dark, labelFont);
        tt.fontStyle = FontStyle.Bold; tt.raycastTarget = false;
        tt.rectTransform.offsetMin = new Vector2(2, 2); tt.rectTransform.offsetMax = new Vector2(-2, -10);
        var fb = new[] { Box(-300f, 0f, 600f, 6f, Sel), Box(-300f, -842f, 600f, 6f, Sel), Box(-300f, 0f, 6f, 848f, Sel), Box(294f, 0f, 6f, 848f, Sel) };
        foreach (var f in fb) { var im = f.GetComponent<Image>(); im.raycastTarget = false; im.color = new Color(Sel.r, Sel.g, Sel.b, 0f); s.frame.Add(im); }
        Label("JOB APPLICATION", L, -26, W, 52, 44, TextAnchor.MiddleCenter, Ink, true, handwritingFont);
        Box(L, -82, W, 3, Dark);

        s.eduIdx = Mathf.Clamp(PlayerPrefs.GetInt(k + "e", 2), 0, Edu.Length - 1);
        s.expIdx = Mathf.Clamp(PlayerPrefs.GetInt(k + "x", 0), 0, Exp.Length - 1);
        s.nameIn = Field("NAME", L, -96, W - 112f, "Your full name", PlayerPrefs.GetString(k + "n", ""), false);
        s.hobbyIn = Field("HOBBY", L, -176, W - 112f, "What do you do for fun?", PlayerPrefs.GetString(k + "h", ""), false);
        BuildPhotoBox(s, PlayerPrefs.GetInt(k + "c", -1));
        Stepper("EDUCATION", L, -256, 256, () => Edu[s.eduIdx], d => s.eduIdx = (s.eduIdx + d + Edu.Length) % Edu.Length);
        s.gradeIn = Field("GRADE (%)", L + 272, -256, 256, "e.g. 85", PlayerPrefs.GetString(k + "g", ""), true);
        Stepper("EXPERIENCE <size=14>(difficulty setting)</size>", L, -336, W, () => Exp[s.expIdx] + "  -  " + Difficulty.Names[s.expIdx], d => s.expIdx = (s.expIdx + d + Exp.Length) % Exp.Length);

        Label("APPLYING FOR (pick one)", L, -416, W, 24, 20, TextAnchor.MiddleLeft, Dark, true, labelFont);
        s.jobs = OptionDrawer.Draw(ResumeSession.Database.jobs, 3);
        s.jobTexts = new string[3];
        for (int i = 0; i < 3; i++)
        {
            string[] v = s.jobs[i].displayVariants;
            s.jobTexts[i] = v[UnityEngine.Random.Range(0, v.Length)];
            string t = "<b>" + s.jobTexts[i] + "</b>\n<size=15>" + RiskInfo.Pay(s.jobs[i]) + " PAY  |  " + RiskInfo.Risk(s.jobs[i]) + " RISK</size>";
            int idx = i; RectTransform b = null;
            b = Button(t, L, -444 - i * 70, W - 46, 64, Cream, Dark, 21, () => { s.jobPick = idx; Highlight(s.jobBtns, idx, Sel, s.jobTicks); });
            s.jobBtns.Add(b.GetComponent<Image>());
            s.jobTicks.Add(Check(L + W - 38, -444 - i * 70 - 15, 34));
        }

        Label("PERK (pick one)", L, -662, W, 24, 20, TextAnchor.MiddleLeft, Dark, true, labelFont);
        s.perks = PerkInfo.Draw(3);
        for (int i = 0; i < 3; i++)
        {
            string t = "<b>" + PerkInfo.Name(s.perks[i]) + "</b>\n<size=13>" + PerkInfo.Description(s.perks[i]) + "</size>";
            int idx = i; RectTransform b = null;
            b = Button(t, L + i * 180, -690, 168, 80, Cream, Dark, 17, () => { s.perkPick = idx; Highlight(s.perkBtns, idx, Selp, s.perkTicks); });
            s.perkBtns.Add(b.GetComponent<Image>());
            s.perkTicks.Add(Check(L + i * 180 + 168 - 24, -690 + 12, 30));
        }

        s.msg = Label("", L, -786, 172, 44, 20, TextAnchor.MiddleLeft, new Color(0.85f, 0.1f, 0.15f), true, labelFont);
        s.discardBtn = Button("DISCARD", L + 180, -786, 166, 44, new Color(1f, 0.62f, 0.62f), Dark, 24, () => Discard(s));
        s.submitBtn = Button("SUBMIT", L + 356, -786, 172, 44, Color.black, Color.white, 26, () => Submit(s));
    }

    void SaveAll()
    {
        if (slots == null) return;
        foreach (var s in slots)
        {
            string k = "rg_slot" + s.idx;
            PlayerPrefs.SetString(k + "n", s.nameIn.text); PlayerPrefs.SetString(k + "h", s.hobbyIn.text); PlayerPrefs.SetString(k + "g", s.gradeIn.text);
            PlayerPrefs.SetInt(k + "c", s.charIdx); PlayerPrefs.SetInt(k + "e", s.eduIdx); PlayerPrefs.SetInt(k + "x", s.expIdx);
        }
        PlayerPrefs.Save();
    }

    void OnDestroy()
    {
        SaveAll();
        Cursor.visible = true;
        RenderSettings.ambientMode = oldAmbientMode; RenderSettings.ambientLight = oldAmbient;
        if (oldShadowDist > 0f) QualitySettings.shadowDistance = oldShadowDist;
    }

    void BuildBackNote()
    {
        if (backNote != null || cam == null) return;
        float halfH = cam.transform.position.y * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), halfW = halfH * cam.aspect;
        float inner = PaperW * 0.5f + 0.02f;
        float x = Mathf.Min(0.225f, halfW - 0.045f);   // right side, just below the noodle cup
        float z = -0.045f;
        backNote = DeskKit.BackNote(deskRoot, handwritingFont != null ? handwritingFont : labelFont, new Vector3(x, 0f, z));
    }

    // the old "press ESC to go back to main menu" hint is replaced by the note on the table: hide any leftover text object that still says it
    void RemoveEscHint()
    {
        foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { string l = t.text == null ? "" : t.text.ToLowerInvariant(); if (l.Contains("esc") && l.Contains("menu")) t.gameObject.SetActive(false); }
        foreach (var t in FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { string l = t.text == null ? "" : t.text.ToLowerInvariant(); if (l.Contains("esc") && l.Contains("menu")) t.gameObject.SetActive(false); }
        foreach (var t in FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { string l = t.text == null ? "" : t.text.ToLowerInvariant(); if (l.Contains("esc") && l.Contains("menu")) t.gameObject.SetActive(false); }
    }

    // table note: back to the main menu, keeping what was typed (unlike DISCARD)
    void BackToMenu()
    {
        if (submitting || slots == null) return;
        SaveAll();
        Cursor.visible = true;
        ScreenFade.ToBlack(0.5f, () => SceneManager.LoadScene(SaveSystem.MenuScene));
    }

    bool submitting;

    // DISCARD: wipes everything typed into this slot and returns to the main menu (post-it START / SETTINGS / ...)
    void Discard(SlotPage s)
    {
        if (submitting || slots == null) return;
        SaveAll();
        string k = "rg_slot" + s.idx;
        foreach (var x in new[] { "n", "h", "g", "c", "e", "x" }) PlayerPrefs.DeleteKey(k + x);
        PlayerPrefs.Save();
        pen.SetCanvas(penRefCanvas); pen.locked = true;
        foreach (var sl in slots) if (sl.hinge != null) WorldUtil.Kill(sl.hinge.gameObject);
        slots = null; slotsActive = false; cur = 0;
        Cursor.visible = true;
        if (menu != null) menu.Show();
        else SceneManager.LoadScene(SaveSystem.MenuScene);   // back to the StartMenu scene
    }

    void Submit(SlotPage s)
    {
        if (submitting) return;
        if (string.IsNullOrWhiteSpace(s.nameIn.text)) { s.msg.text = "Write your name!"; return; }
        if (s.jobPick < 0) { s.msg.text = "Pick a job!"; return; }
        if (s.perkPick < 0) { s.msg.text = "Pick a perk!"; return; }
        int grade;
        if (!int.TryParse(s.gradeIn.text, out grade)) grade = EduScore[s.eduIdx];
        grade = Mathf.Clamp(grade, 0, 100);
        SaveAll();
        ResumeSession.OfferedJobs = s.jobs;
        ResumeSession.OfferedTexts = s.jobTexts;
        ResumeSession.SelectOffer(s.jobPick);
        ResumeSession.CandidateName = s.nameIn.text.Trim();
        ResumeSession.Hobby = string.IsNullOrWhiteSpace(s.hobbyIn.text) ? "napping" : s.hobbyIn.text.Trim();
        ResumeSession.RealEducation = grade;
        ResumeSession.EducationScore = grade;
        ResumeSession.Experience = Exp[s.expIdx];
        ResumeSession.SelectedPerk = s.perks[s.perkPick];
        ResumeSession.SetCharacter(s.charIdx);
        PlayerInventory.Reset();            // slot-0 starter resume is rebuilt now that the real name is known
        Items.ItemTextures.ClearCache();    // so its texture is regenerated from this form
        submitting = true;
        StartCoroutine(SubmitRoutine(s));
    }

    // photographs the finished form (it becomes the starter resume's texture), then loads the world
    System.Collections.IEnumerator SubmitRoutine(SlotPage s)
    {
        pen.FlushAll();
        yield return ResumeSnapshot.Capture(s);
        SaveSystem.StartNew(s.idx, s.jobPick);      // this resume page becomes a save slot
        if (Submitted != null) Submitted();
        WorldLauncher.Begin(nextScene);
    }

    // ================= PHOTO ID =================
    // recolours the 3D hands/sleeves to match the slot's chosen character (default colours when none)
    void ApplyCharacter(SlotPage s)
    {
        if (pen == null) return;
        if (s.charIdx < 0) pen.SetColors(ResumeSession.DefaultSkin, ResumeSession.DefaultOutfit);
        else pen.SetColors(CharacterLibrary.All[s.charIdx].skin, CharacterLibrary.All[s.charIdx].outfit);
    }

    void SetCharacter(SlotPage s, int idx)
    {
        s.charIdx = idx;
        s.photo.texture = idx >= 0 ? CharacterLibrary.Portrait(idx) : null;
        s.photo.enabled = idx >= 0; s.photoHint.gameObject.SetActive(idx < 0);
        if (slots != null && slots[cur] == s) ApplyCharacter(s);
    }

    void BuildPhotoBox(SlotPage s, int saved)
    {
        paper = s.paper;
        Label("PHOTO ID", L + W - 100f, -96, 100, 24, 20, TextAnchor.MiddleLeft, Dark, true, labelFont);
        var b = Button("", L + W - 100f, -122, 100, 124, Cream, Dark, 18, () => OpenPicker(s));
        var go = new GameObject("Photo", typeof(RawImage));
        var r = go.GetComponent<RectTransform>(); r.SetParent(b, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(4, 4); r.offsetMax = new Vector2(-4, -4);
        s.photo = go.GetComponent<RawImage>(); s.photo.raycastTarget = false;
        s.photoHint = Inner(b, "+\nCLICK TO\nADD PHOTO", 16, TextAnchor.MiddleCenter, new Color(Ink.r, Ink.g, Ink.b, 0.55f), labelFont);
        s.photoHint.raycastTarget = false;
        SetCharacter(s, saved >= 0 && saved < CharacterLibrary.All.Length ? saved : -1);
    }

    void OpenPicker(SlotPage s)
    {
        if (s.picker == null) BuildPicker(s);
        s.pickTmp = s.charIdx; s.pickFemale = s.charIdx >= 4;
        s.picker.SetActive(true); s.pickerSync();
    }

    // full-sheet overlay: MALE/FEMALE toggle on top, 4 characters in the middle, big preview, BACK / CONFIRM
    void BuildPicker(SlotPage s)
    {
        var root = new GameObject("Photo Picker", typeof(Image));
        var rr = root.GetComponent<RectTransform>(); rr.SetParent(s.paper, false);
        rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0.96f, 0.93f, 0.82f);
        Outline(root, Dark, 3);
        s.picker = root;
        paper = rr;

        Label("CHOOSE PHOTO ID", L, -22, W, 50, 38, TextAnchor.MiddleCenter, Ink, true, handwritingFont);
        var mBtn = Button("MALE", -130f, -96, 125, 48, Cream, Dark, 26, () => { s.pickFemale = false; s.pickerSync(); }).GetComponent<Image>();
        var fBtn = Button("FEMALE", 5f, -96, 125, 48, Cream, Dark, 26, () => { s.pickFemale = true; s.pickerSync(); }).GetComponent<Image>();

        var cards = new Image[4]; var faces = new RawImage[4];
        for (int i = 0; i < 4; i++)
        {
            int n = i;
            float cx = L + i * 134f;
            var c = Button("", cx, -190, 126, 154, Cream, Dark, 18, () => { s.pickTmp = (s.pickFemale ? 4 : 0) + n; s.pickerSync(); });
            cards[i] = c.GetComponent<Image>();
            var go = new GameObject("Face", typeof(RawImage));
            var r = go.GetComponent<RectTransform>(); r.SetParent(c, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(114, 142); r.anchoredPosition = new Vector2(0, -6);
            faces[i] = go.GetComponent<RawImage>(); faces[i].raycastTarget = false;
        }

        Box(-104f, -368, 208, 258, Dark);
        var pg = new GameObject("Preview", typeof(RawImage));
        var pr = pg.GetComponent<RectTransform>(); pr.SetParent(rr, false);
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1f); pr.pivot = new Vector2(0.5f, 1f);
        pr.sizeDelta = new Vector2(200, 250); pr.anchoredPosition = new Vector2(0, -372);
        var preview = pg.GetComponent<RawImage>(); preview.raycastTarget = false;
        var hint = Label("", L, -642, W, 30, 22, TextAnchor.MiddleCenter, new Color(0.85f, 0.1f, 0.15f), true, labelFont);

        Button("BACK", L, -750, 250, 54, Cream, Dark, 28, () => s.picker.SetActive(false));
        Button("CONFIRM", L + 278f, -750, 250, 54, Selp, Dark, 28, () =>
        {
            if (s.pickTmp < 0) { hint.text = "Pick a character!"; return; }
            SetCharacter(s, s.pickTmp); s.picker.SetActive(false);
        });

        s.pickerSync = () =>
        {
            mBtn.color = s.pickFemale ? Cream : Sel; fBtn.color = s.pickFemale ? Sel : Cream;
            for (int i = 0; i < 4; i++)
            {
                int idx = (s.pickFemale ? 4 : 0) + i;
                faces[i].texture = CharacterLibrary.Portrait(idx);
                cards[i].color = s.pickTmp == idx ? Sel : Cream;
            }
            preview.texture = s.pickTmp >= 0 ? CharacterLibrary.Portrait(s.pickTmp) : null; preview.enabled = s.pickTmp >= 0;
            hint.text = "";
        };
        paper = s.paper;
    }

    void Highlight(List<Image> l, int sel, Color c, List<GameObject> ticks) { for (int i = 0; i < l.Count; i++) { l[i].color = i == sel ? c : Cream; ticks[i].SetActive(false); }
        var bx = ticks[sel].transform.parent as RectTransform;
        pen.PokeTick(bx, () => ticks[sel].SetActive(true)); }

    GameObject Check(float x, float y, float sz)
    {
        var b = Box(x, y, sz, sz, Color.white);
        Outline(b.gameObject, Dark, 2);
        b.GetComponent<Image>().raycastTarget = false;
        var tk = new GameObject("Tick");
        tk.transform.SetParent(b, false);
        tk.AddComponent<RectTransform>().sizeDelta = Vector2.zero;
        Bar(tk.transform, new Vector2(-sz * 0.17f, -sz * 0.1f), new Vector2(sz * 0.15f, sz * 0.4f), 45f);
        Bar(tk.transform, new Vector2(sz * 0.1f, sz * 0.05f), new Vector2(sz * 0.15f, sz * 0.78f), -45f);
        tk.SetActive(false);
        return tk;
    }

    void Bar(Transform p, Vector2 pos, Vector2 size, float rot)
    {
        var go = new GameObject("Bar", typeof(Image));
        var r = go.GetComponent<RectTransform>(); r.SetParent(p, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size; r.localRotation = Quaternion.Euler(0f, 0f, rot);
        var im = go.GetComponent<Image>(); im.color = Ink; im.raycastTarget = false;
    }

    InputField Field(string label, float x, float y, float w, string hint, string init, bool numeric)
    {
        Label(label, x, y, w, 24, 20, TextAnchor.MiddleLeft, Dark, true, labelFont);
        var box = Box(x, y - 26, w, 44, Cream);
        Outline(box.gameObject, Dark, 2);
        var hidden = Inner(box, "", 30, TextAnchor.MiddleLeft, Color.clear, handwritingFont); hidden.raycastTarget = false;
        var shown = Inner(box, "", 30, TextAnchor.MiddleLeft, Ink, handwritingFont); shown.raycastTarget = false;
        var ph = Inner(box, hint, 26, TextAnchor.MiddleLeft, new Color(Ink.r, Ink.g, Ink.b, 0.35f), handwritingFont); ph.raycastTarget = false;
        var inp = box.gameObject.AddComponent<InputField>();
        inp.targetGraphic = box.GetComponent<Image>(); inp.textComponent = hidden; inp.placeholder = null;
        inp.customCaretColor = true; inp.caretColor = Color.clear; inp.selectionColor = Color.clear;
        inp.characterLimit = numeric ? 3 : 28; inp.lineType = InputField.LineType.SingleLine;
        inp.onValidateInput += (t, i, c) => numeric ? (char.IsDigit(c) ? c : '\0') : ((c == '<' || c == '>') ? '\0' : c);
        inp.text = init;
        pen.Add(new WriteField { display = shown, hint = ph, get = () => inp.text, written = init });
        return inp;
    }

    void Stepper(string label, float x, float y, float w, Func<string> show, Action<int> step)
    {
        Label(label, x, y, w, 24, 20, TextAnchor.MiddleLeft, Dark, true, labelFont);
        var box = Box(x + 44, y - 26, w - 88, 44, Cream);
        Outline(box.gameObject, Dark, 2);
        var v = Inner(box, "", 28, TextAnchor.MiddleCenter, Ink, handwritingFont); v.raycastTarget = false;
        pen.Add(new WriteField { display = v, get = show, centered = true, written = show() });
        Button("<", x, y - 26, 40, 44, Orange, Dark, 26, () => step(-1));
        Button(">", x + w - 40, y - 26, 40, 44, Orange, Dark, 26, () => step(1));
    }

    RectTransform Button(string text, float x, float y, float w, float h, Color bg, Color fg, int size, Action click)
    {
        var r = Box(x, y, w, h, bg);
        Outline(r.gameObject, Dark, 2);
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = r.GetComponent<Image>();
        var cb = b.colors; cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f); cb.pressedColor = new Color(0.65f, 0.65f, 0.65f); b.colors = cb;
        b.onClick.AddListener(() => click());
        Inner(r, text, size, TextAnchor.MiddleCenter, fg, labelFont).raycastTarget = false;
        return r;
    }

    Text Label(string s, float x, float y, float w, float h, int size, TextAnchor a, Color c, bool bold, Font f)
    {
        var go = new GameObject("Label", typeof(Text));
        var r = go.GetComponent<RectTransform>(); r.SetParent(paper, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0, 1);
        r.sizeDelta = new Vector2(w, h); r.anchoredPosition = new Vector2(x, y);
        var t = go.GetComponent<Text>(); Style(t, s, size, a, c, f); t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyle.Bold;
        return t;
    }

    Text Inner(RectTransform box, string s, int size, TextAnchor a, Color c, Font f)
    {
        var go = new GameObject("Text", typeof(Text));
        var r = go.GetComponent<RectTransform>(); r.SetParent(box, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(10, 2); r.offsetMax = new Vector2(-10, -2);
        var t = go.GetComponent<Text>(); Style(t, s, size, a, c, f);
        return t;
    }

    void Style(Text t, string s, int size, TextAnchor a, Color c, Font f)
    {
        t.text = s; t.font = f; t.fontSize = size; t.alignment = a; t.color = c; t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
    }

    RectTransform Box(float x, float y, float w, float h, Color c)
    {
        var go = new GameObject("Box", typeof(Image));
        var r = go.GetComponent<RectTransform>(); r.SetParent(paper, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0, 1);
        r.sizeDelta = new Vector2(w, h); r.anchoredPosition = new Vector2(x, y);
        go.GetComponent<Image>().color = c;
        return r;
    }

    void Outline(GameObject g, Color c, float d) { var o = g.AddComponent<Outline>(); o.effectColor = c; o.effectDistance = new Vector2(d, -d); }
}
