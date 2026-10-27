using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Punctuality timer for HARD / EXPERT only (EASY / NORMAL: everything here is inert).
/// Starts when the open world is ready, pauses with the game, persists in the save (SaveData.worldElapsed).
/// Late arrival: more preliminary suspicion, an impatient interviewer and a remark. On time: friendlier mood. Too early: a remark.
/// More than TooLateGrace seconds after the timer ends, the door turns the player away.
/// </summary>
public static class WorldTimer
{
    //                                     EASY NORMAL HARD   EXPERT
    static readonly float[] limits = { 0f, 0f, 900f, 600f };      // seconds allowed to reach the door
    public const float TooLateGrace = 300f;                      // 5 min past the deadline = rejected at the door
    const float EarlyFraction = 0.25f;                           // reaching the door in the first 25% of the allowance = "too early"

    static float elapsed;
    static string runKey = "\u0001";
    static bool ticking;
    static GameObject ticker;

    public static bool Active { get { return Difficulty.Level >= 2; } }
    public static float Limit { get { return Active ? limits[Difficulty.Level] : 0f; } }

    static void Sync()
    {
        string k = SaveSystem.Data != null ? SaveSystem.Data.runId : "";
        if (k == runKey) return;
        runKey = k; ticking = false;
        elapsed = SaveSystem.Data != null ? Mathf.Max(0f, SaveSystem.Data.worldElapsed) : 0f;
    }

    public static float Elapsed { get { Sync(); return elapsed; } }
    public static float Remaining { get { return Limit - Elapsed; } }
    public static float LateSeconds { get { return Active ? Mathf.Max(0f, Elapsed - Limit) : 0f; } }
    public static bool IsLate { get { return Active && Elapsed > Limit; } }
    public static bool TooLate { get { return Active && LateSeconds > TooLateGrace; } }
    public static bool IsEarly { get { return Active && Elapsed < Limit * EarlyFraction; } }
    public static bool Running { get { return ticking; } }

    /// <summary>Called by SaveSystem.WorldReady: the player is in the open world, the clock starts.</summary>
    public static void StartWorld()
    {
        if (!Active) return;
        Sync();
        ticking = true;
        if (ticker == null)
        {
            ticker = new GameObject("WorldTimer");
            ticker.AddComponent<WorldTimerTicker>();
            ticker.AddComponent<WorldTimerHud>();
        }
    }

    /// <summary>The player walked through the door: the clock stops (value is kept for the interview + saved).</summary>
    public static void Stop() { ticking = false; }

    internal static void Tick(float dt) { if (ticking && !WorldLauncher.Launching) elapsed += dt; }

    // ---------- interview effects ----------
    /// <summary>Extra preliminary suspicion for arriving late (replaces the flat +6).</summary>
    public static int LatePenalty
    {
        get
        {
            float p = 6f + LateSeconds / 60f * 1.5f;
            if (Difficulty.Level >= 3) p *= 1.25f;
            return Mathf.RoundToInt(Mathf.Min(p, 14f));
        }
    }

    /// <summary>Late = likely impatient, on time = likely friendlier. Called once before the interviewer's mood starts.</summary>
    public static Mood AdjustMood(Mood m)
    {
        if (!Active) return m;
        if (IsLate)
        {
            float chance = Mathf.Clamp01(0.45f + LateSeconds / TooLateGrace * 0.4f);
            return Random.value < chance ? Mood.Impatient : m;
        }
        if (!IsEarly && m != Mood.Patient && Random.value < 0.4f) return Mood.Patient;
        return m;
    }

    static int lastLate = -1, lastEarly = -1, lastOk = -1;

    static readonly string[] LateLines =
    {
        "You're late. I don't have all day.",
        "{m} minutes behind schedule. That's not a great start.",
        "I was starting to think you weren't coming.",
        "Punctuality matters here. You've already cost me time.",
        "Late. Let's not make it a habit, shall we?",
        "My calendar says you were due a while ago.",
        "You kept me waiting. Noted.",
        "Traffic, was it? Everyone has a story. Sit.",
        "Arriving late to an interview. Bold choice.",
        "Let's try to use the time you left us wisely."
    };
    static readonly string[] EarlyLines =
    {
        "You're early. Very early. Eager, or just nervous?",
        "I wasn't expecting you for a while. Keen, aren't you?",
        "Early birds make me curious. Why the rush?",
        "Well, you're certainly ahead of schedule.",
        "Showing up this early is either dedication or desperation.",
        "I hadn't even finished my coffee. Fine, sit."
    };
    static readonly string[] OnTimeLines =
    {
        "Right on time. I appreciate that.",
        "Good, you're punctual. Let's begin.",
        "Prompt. That's a good sign.",
        "Not a minute wasted. I like it."
    };

    static string Pick(string[] a, ref int last)
    {
        int i = Random.Range(0, a.Length);
        if (a.Length > 1 && i == last) i = (i + 1) % a.Length;
        last = i;
        return a[i];
    }

    /// <summary>One punctuality remark for the interviewer to say after the greeting (null = nothing to say).</summary>
    public static string TakeRemark()
    {
        if (!Active) return null;
        if (IsLate) return Pick(LateLines, ref lastLate).Replace("{m}", Mathf.Max(1, Mathf.RoundToInt(LateSeconds / 60f)).ToString());
        if (IsEarly) return Pick(EarlyLines, ref lastEarly);
        return Random.value < 0.4f ? Pick(OnTimeLines, ref lastOk) : null;
    }

    /// <summary>Door interaction while TooLate: the run ends as a rejection without an interview.</summary>
    public static void RejectAtDoor()
    {
        Stop();
        InterviewResult.Passed = false;
        InterviewResult.Rating = "F";
        InterviewResult.Score = 0f;
        InterviewResult.Outcome = "REJECTED";
        InterviewResult.Report = "[ REJECTED ]\nYou arrived far too late. The interviewer had already left.";
        SaveSystem.FinalizeRun();
        SaveSystem.CompleteGame();
    }
}

public class WorldTimerTicker : MonoBehaviour
{
    void Update() { WorldTimer.Tick(Time.deltaTime); }
}

/// <summary>Top-centre clock, only visible while the inventory is open. Styled like the inventory folio (paper, tape, chunky outline, same font).
/// Badge colour: green > white > red > pulsating once time is up.</summary>
public class WorldTimerHud : MonoBehaviour
{
    GameObject panel;
    RectTransform panelRt;
    Image badge;
    Text label, time;
    Font font;
    bool fontApplied;

    static readonly Color Ink = UIKit.Dark;
    static readonly Color PaperC = new Color(1f, .96f, .84f, 1f);

    static Image Box(Transform parent, string name, Color c, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size;
        var im = go.GetComponent<Image>(); im.color = c; im.raycastTarget = false;
        return im;
    }

    Text Label(Transform parent, string name, int size, Vector2 pos, Vector2 sz)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = sz;
        var t = go.GetComponent<Text>(); t.fontSize = size; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter; t.color = Ink; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }

    // same font the inventory folio uses (it reads InterviewUI's uiFont)
    void ResolveFont()
    {
        if (font != null) return;
        try
        {
            var inv = FindFirstObjectByType<Items.InventoryUI>(FindObjectsInactive.Include);
            if (inv == null) return;
            var f = typeof(Items.InventoryUI).GetField("uiFont", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            font = f != null ? f.GetValue(inv) as Font : null;
        }
        catch (System.Exception) { }
    }

    void Build()
    {
        ResolveFont();
        var cg = new GameObject("WorldTimerCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        cg.transform.SetParent(transform, false);
        var canvas = cg.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 125;
        var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = .5f;

        var p = Box(cg.transform, "TimerPanel", PaperC, Vector2.zero, new Vector2(340, 74));
        panelRt = p.rectTransform;
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(.5f, 1f); panelRt.pivot = new Vector2(.5f, .5f); panelRt.anchoredPosition = new Vector2(0f, -52f);
        UIKit.Boxy(p, 4f, true);
        UIKit.Tape(panelRt, 0f, -2f);
        panel = p.gameObject;

        var head = Box(panelRt, "LabelStrip", UIKit.Hex("#FFE27A"), new Vector2(-82, 0), new Vector2(150, 52));
        UIKit.Boxy(head, 3f, false);
        label = Label(head.transform, "Label", 17, Vector2.zero, new Vector2(146, 46));
        badge = Box(panelRt, "TimeBadge", UIKit.Hex("#7CE08A"), new Vector2(82, 0), new Vector2(150, 52));
        UIKit.Boxy(badge, 3f, false);
        time = Label(badge.transform, "Time", 32, Vector2.zero, new Vector2(146, 48));
        fontApplied = font != null;
    }

    void Update()
    {
        bool show = WorldTimer.Active && WorldTimer.Running && Items.InventoryUI.IsOpen;
        if (!show) { if (panel != null && panel.activeSelf) panel.SetActive(false); return; }
        if (panel == null) Build();
        if (!panel.activeSelf) panel.SetActive(true);
        if (!fontApplied) { ResolveFont(); if (font != null) { label.font = font; time.font = font; fontApplied = true; } }

        float rem = WorldTimer.Remaining, lim = WorldTimer.Limit;
        Color c; string head, t;
        float tilt = 1.2f, scale = 1f;
        if (rem <= 0f)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
            c = Color.Lerp(UIKit.Hex("#FF8A80"), UIKit.Hex("#FF3D3D"), k);
            scale = 1f + 0.05f * k; tilt = 1.2f + Mathf.Sin(Time.unscaledTime * 14f) * 0.8f;
            head = WorldTimer.TooLate ? "TOO LATE!" : "RUNNING LATE";
            t = WorldTimer.TooLate ? "--:--" : "+" + Fmt(-rem);
        }
        else
        {
            float f = rem / lim;
            c = f > 0.5f ? UIKit.Hex("#7CE08A") : (f > 0.2f ? Color.white : UIKit.Hex("#FF8A80"));
            head = "TIME LEFT"; t = Fmt(rem);
        }
        label.text = head; time.text = t; badge.color = c;
        panelRt.localScale = Vector3.one * scale;
        panelRt.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }

    static string Fmt(float s) { int t = Mathf.Max(0, Mathf.CeilToInt(s)); return (t / 60).ToString("00") + ":" + (t % 60).ToString("00"); }
}
