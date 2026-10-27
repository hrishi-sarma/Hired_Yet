using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Runs the interview. Put this on the interviewer object (the Sphere) that has a trigger collider.
/// - Player walks into the trigger -> "PRESS E" prompt. E starts the interview.
/// - Player is frozen, cursor unlocked. The UI is a minimal HUD built at runtime: NO full-screen panel,
///   the interviewer stays visible. Top: suspicion bar + timer. Bottom: a short subtitle-style log + answer controls.
/// - 10 questions: job questions from the JSON (QuestionDrawer) with 2-3 of them swapped for
///   questions about the player's own resume: name, hobby, education score (QuestionMixer).
/// - Wrong / evasive / missing answers raise suspicion. Result goes to InterviewResult.
/// - Layout is anchor based, follows the safe area and switches landscape / portrait automatically.
/// New Input System only.
/// </summary>
[RequireComponent(typeof(Collider))]
public partial class InterviewUI : MonoBehaviour
{
    [Header("Zone")]
    [SerializeField] string playerTag = "Player";
    [SerializeField] bool autoStart = false;

    [Header("Seating")]
    [SerializeField] InterviewerRig rig;
    [SerializeField] Transform seatPoint;
    [SerializeField] float seatDistance = 1.5f;
    [SerializeField] float seatedCameraDrop = 0.5f;
    [SerializeField] float sitDuration = 1.3f;

    [Header("Cognitive Test")]
    [SerializeField] bool runCognitiveTest = true;
    [SerializeField] int cognitivePassesNeeded = 2;   // of the 3 minigames

    [Header("Rules")]
    [SerializeField] float startSuspicion = 20f;
    [SerializeField][Range(0.1f, 1f)] float passBelowFraction = 0.6f; // pass if final suspicion < max * this
    [SerializeField] float answerTimeLimit = 20f;                      // 0 = no time limit
    [SerializeField] float verdictPause = 0.9f;
    [SerializeField] int minPersonalQuestions = 2;                     // name / hobby / grade questions per run
    [SerializeField] int maxPersonalQuestions = 3;
    [SerializeField] float personalPenaltyMultiplier = 1.25f;          // lying about yourself is extra suspicious
    [SerializeField] string nextSceneName = "";                        // optional scene to load after the result

    [Header("Interviewer Behavior")]
    [SerializeField][Range(0f, 100f)] float passScore = 60f;
    [SerializeField] int maxCounterQuestions = 3;
    [SerializeField][Range(0f, 0.6f)] float baseCounterChance = 0.16f;
    [SerializeField] int minGapBetweenCounters = 2;
    [SerializeField][Range(0.3f, 0.95f)] float slowFraction = 0.6f;
    [SerializeField] float fastSecondsYesNo = 1f;
    [SerializeField] float fastSecondsShort = 2f;
    [SerializeField] float slowPenalty = 4f;
    [SerializeField] float fastPenalty = 3f;
    [SerializeField] float contradictionPenalty = 8f;
    [SerializeField] bool proofEnabled = true;
    [SerializeField][Range(0.2f, 0.9f)] float proofSuspicionTrigger = 0.5f;
    [SerializeField][Range(0f, 1f)] float resumeRequestChance = 0.6f;

    [Header("Suspicion Effects")]
    [SerializeField] bool cameraShake = true;
    [SerializeField][Range(0f, 0.9f)] float shakeStart = 0.45f;
    [SerializeField] float maxShakeDegrees = 1.2f;
    [SerializeField] float maxShakeShift = 0.03f;

    [Header("Look")]
    [Tooltip("Drag any normal font file (.ttf / .otf) here. It is used for the whole interview UI.")]
    [SerializeField] Font uiFont;
    [Tooltip("Optional TMP font asset. Used only when no UI Font is set (or as glyph fallback).")]
    [SerializeField] TMP_FontAsset font;
    [SerializeField] float charsPerSecond = 110f;
    [SerializeField][Range(0.6f, 1.6f)] float uiScale = 1f;            // multiplies every size (text and boxes)
    [SerializeField] bool scanlines = true;                            // only drawn on the small black strips

    enum State { Idle, Typing, Asking, Feedback, Result }

    // monochrome palette (same as the resume terminal)
    const string HexNormal = "#1A2FA0";
    const string HexDim = "#4A5599";
    const string HexBright = "#0A144D";
    const string HexBg = "#FFF8E1";
    const string HexDark = "#D5CAA0";
    const string HexRed = "#D6281E";          // used ONLY for the REJECTED result
    const string HexRedBright = "#FF6A5A";
    const int SegmentCount = 40;
    const int TimerSegmentCount = 24;

    // reference sizes (1920x1080 landscape / 1080x1920 portrait), all multiplied by uiScale
    const float HudRowHeight = 42f;
    const float TimerRowHeight = 42f;
    const float LogHeight = 220f;
    const float ControlsHeight = 58f;
    const float BlockGap = 6f;
    const float ResultBackHeight = 520f;
    const float MoodRowHeight = 36f;

    Color cNormal;
    Color cDim;
    Color cBright;
    Color cBg;
    Color cDark;
    Color stripColor;

    // ---- ui refs ----
    GameObject canvasGo;
    CanvasScaler canvasScaler;
    RectTransform safeRt;
    RectTransform fxRt;
    RectTransform suspBlock;
    RectTransform timerBlock;
    RectTransform moodBlock;
    TextMeshProUGUI moodText;
    RectTransform bottomBlock;
    RectTransform resultBlock;
    GameObject promptRoot;
    GameObject interviewRoot;
    GameObject resultRoot;
    GameObject yesNoRow;
    GameObject shortRow;
    GameObject timerGroup;
    TMP_InputField input;
    TextMeshProUGUI logText;
    TextMeshProUGUI timerLabel;
    TextMeshProUGUI timerValue;
    TextMeshProUGUI counterText;
    TextMeshProUGUI suspicionLabel;
    TextMeshProUGUI suspicionValue;
    TextMeshProUGUI resultTitle;
    TextMeshProUGUI resultDetail;
    Image[] segments;
    Image[] timerSegments;
    Image logBack;
    RectTransform logPanel;
    Image resultBack;
    GlitchButton yesBtn;
    GlitchButton noBtn;
    GlitchButton maybeBtn;
    GlitchButton continueBtn;
    Vector2Int lastScreen = Vector2Int.zero;
    Rect lastSafe = Rect.zero;

    // ---- log ----
    readonly StringBuilder committed = new StringBuilder();
    string tail = "";

    // ---- interview state ----
    State state = State.Idle;
    bool inZone;
    bool closing;
    Camera sitCam;
    Vector3 standPos, standCamPos;
    Quaternion standRot, standCamRot;
    Vector3 seatedPos;
    Quaternion seatedRot, seatedCamRot;
    Vector3 seatedCamPos;
    readonly List<Collider> offCols = new List<Collider>();
    Rigidbody rbHeld;
    static readonly string[] SeatLines =
    {
        "Welcome! Please, take a seat.",
        "You're here. Take a seat. Quickly.",
        "Take a seat. Slowly. Where I can see you.",
        "Hi. Take a seat, I guess."
    };
    bool finished;
    JobData job;
    InterviewConfig cfg;
    List<QuestionData> questions;
    QuestionData current;
    int index;
    int asked;
    int correct;
    int hedgeStreak;
    float suspicion;
    float shownSuspicion;
    float multiplier = 1f;
    float timeLeft;

    // ---- fx ----
    float flashAlpha;
    Color flashColor;
    float glitchTime;
    float glitchPower;

    Mood mood;
    Perk perk;
    InterviewReport report;
    QuestionData pressureQuestion;
    QuestionData proofQuestion;
    readonly List<AnswerEntry> history = new List<AnswerEntry>();
    readonly List<string> impressionLines = new List<string>();
    int counterCount;
    int sinceCounter;
    int slowCount;
    int fastCount;
    int warnLevel;
    int proofIndex;
    bool impatient;
    bool proofDone;
    bool resumeReqDone;
    // hand-over of documents (resume question + follow-up): documents must be held in hand, then given
    bool handoverMode, handoverResume, handoverOther; string handoverKey;
    Items.ItemHold hold;
    int resumeReqIndex = -1;
    bool requestMode;
    QuestionData requestQuestion;
    bool luckyUsed;
    bool remarked;
    bool maybeAllowed;
    bool ansTimedOut;
    float effectiveLimit;
    float askStart;
    float ansElapsed;
    string ansRaw = "";
    string ansShown = "";
    string yesLabelCur;
    string noLabelCur;
    Camera shakeCam;
    Vector3 camBasePos;
    Quaternion camBaseRot;
    bool shaking;
    float impulse;
    float shakeSeed;

    // ---- player freeze ----
    Transform playerRoot;
    readonly List<MonoBehaviour> frozen = new List<MonoBehaviour>();
    CursorLockMode previousLock;

    // =====================================================================
    // lifecycle
    // =====================================================================
    void BuildFontFromFile()
    {
        if (uiFont == null) return;
        try
        {
            TMP_FontAsset made = TMP_FontAsset.CreateFontAsset(uiFont, 90, 9,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (made == null) return;
            made.name = uiFont.name + " (runtime)";
            made.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (font != null) made.fallbackFontAssetTable.Add(font);                      // glyphs the file lacks
            if (TMP_Settings.defaultFontAsset != null) made.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
            font = made;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("InterviewUI: could not build a font from '" + uiFont.name + "': " + e.Message);
        }
    }

    void Awake()
    {
        Collider c = GetComponent<Collider>();
        c.isTrigger = true;
        BuildFontFromFile();
        BuildUI();
        FindRig();
        if (interviewRoot != null) interviewRoot.SetActive(false);
        if (promptRoot != null) promptRoot.SetActive(false);
    }

    void OnDestroy()
    {
        VoiceRelease();     // root cause of the stale-RectTransform errors: voice events outlived this UI
        Items.InventoryUI.Locked = false;
        Items.InventoryUI.NoDrop = false;
        // Drop references before deferred destruction; no later callback should reuse this canvas.
        GameObject oldCanvas = canvasGo;
        canvasGo = null;
        canvasScaler = null;
        safeRt = null;
        fxRt = null;
        suspBlock = null;
        timerBlock = null;
        moodBlock = null;
        bottomBlock = null;
        resultBlock = null;
        promptRoot = null;
        interviewRoot = null;
        resultRoot = null;
        yesNoRow = null;
        shortRow = null;
        timerGroup = null;
        logPanel = null;
        if (oldCanvas != null) WorldUtil.Kill(oldCanvas);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        inZone = true;
        if (playerRoot == null) playerRoot = other.transform.root;
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;
        if (state == State.Idle) inZone = false;
    }

    bool IsPlayer(Collider c)
    {
        if (c.GetComponentInParent<CharacterController>() != null) return true;
        if (!string.IsNullOrEmpty(playerTag))
        {
            if (c.CompareTag(playerTag)) return true;
            if (c.transform.root.CompareTag(playerTag)) return true;
        }
        return false;
    }

    void Update()
    {
        if (PauseMenu.Paused) return;
        float dt = PauseMenu.Dt;
        ApplyLayout(false);
        UpdateFx(dt);
        Keyboard kb = Keyboard.current;

        promptRoot.SetActive(state == State.Idle && inZone && !finished);

        if (state == State.Idle)
        {
            if (inZone && !finished && (autoStart || (kb != null && kb.eKey.wasPressedThisFrame))) Begin();
            return;
        }

        UpdateSuspicionBar(dt);
        VoiceTick();

        if (cognitiveActive) UpdateCognitiveHud();
        else if (state == State.Asking)
        {
            bool yesno = current.type == "yesno";
            if (yesno && requestMode)
            {
                if (handoverMode) HandoverTick(kb); else HandleRequestKeys(kb);
            }
            else if (yesno)
            {
                if (kb != null)
                {
                    if (kb.yKey.wasPressedThisFrame) { yesBtn.Pulse(); AnswerYesNo("yes"); }
                    else if (kb.nKey.wasPressedThisFrame) { noBtn.Pulse(); AnswerYesNo("no"); }
                    else if (maybeAllowed && kb.mKey.wasPressedThisFrame) { maybeBtn.Pulse(); AnswerYesNo("maybe"); }
                }
            }
            else if (!input.isFocused)
            {
                input.ActivateInputField();
            }

            if (state == State.Asking) UpdateTimer(dt);
        }
        else if (state == State.Typing || state == State.Feedback) RenderIdleTimer();
        else if (state == State.Result)
        {
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
                Close();
        }
    }

    /// <summary>Countdown readout: T-MINUS label, draining segment bar, mm:ss.t clock. Last 5 s: inverted blinking block.</summary>
    void UpdateTimer(float dt)
    {
        if (chatMode || effectiveLimit <= 0f)
        {
            RenderIdleTimer();
            return;
        }
        if (handoverMode && Items.InventoryUI.IsOpen) { RenderTimer(Mathf.Max(0f, timeLeft), effectiveLimit, "T-MINUS"); return; }   // clock waits while you dig through your bag
        timeLeft -= dt;
        RenderTimer(Mathf.Max(0f, timeLeft), effectiveLimit, "T-MINUS");
        if (!midNudged && effectiveLimit > 0f && timeLeft < effectiveLimit * 0.35f) { midNudged = true; WaitPop(); }
        if (timeLeft <= 0f) OnTimeUp();
    }

    bool idleTimer;

    /// <summary>Shared countdown readout (answers AND cognitive minigames).</summary>
    void RenderTimer(float t, float limit, string label)
    {
        idleTimer = false;
        timerGroup.SetActive(true);
        bool urgent = t <= 5f && limit > 0f;
        bool blink = urgent && (Mathf.FloorToInt(Time.unscaledTime * 5f) % 2 == 0);

        int whole = Mathf.FloorToInt(t);
        int tenth = Mathf.FloorToInt((t - whole) * 10f) % 10;
        string clock = (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00") + "." + tenth;

        timerLabel.text = urgent ? "<color=" + HexBright + ">!! " + label + "</color>" : "<color=" + HexDim + ">" + label + "</color>";
        if (urgent && blink) timerValue.text = "<mark=" + HexNormal + "><color=" + HexBg + "> " + clock + " </color></mark>";
        else timerValue.text = "<color=" + (urgent ? HexBright : HexNormal) + ">" + clock + "</color>";

        int lit = limit > 0f ? Mathf.CeilToInt(Mathf.Clamp01(t / limit) * timerSegments.Length) : 0;
        for (int i = 0; i < timerSegments.Length; i++)
        {
            Color c = cDark;
            if (i < lit) c = (urgent && blink) ? UIKit.Hex("#F03A2E") : UIKit.Hex("#FF9F1C");
            timerSegments[i].color = c;
        }
    }

    /// <summary>Nothing is being timed right now (intro, remarks...): show a labelled empty timer instead of a blank bar.</summary>
    void RenderIdleTimer()
    {
        if (idleTimer) return;
        idleTimer = true;
        timerGroup.SetActive(true);
        timerLabel.text = "<color=" + HexDim + ">TIMER</color>";
        timerValue.text = "<color=" + HexDim + ">--:--</color>";
        for (int i = 0; i < timerSegments.Length; i++) timerSegments[i].color = cDark;
    }

    void UpdateCognitiveHud()
    {
        if (cognitive == null) return;
        RenderTimer(cognitive.LevelTimeRemaining, cognitive.LevelTimeLimit, "T-MINUS");
        string c = "TEST " + Mathf.Clamp(cognitive.RoundNumber, 1, 3) + "/3";
        if (counterText.text != c) SetCounter(c);
    }

    void UpdateFx(float dt)
    {
        if (flashAlpha > 0f)
        {
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 1.4f);
            logBack.color = Color.Lerp(stripColor, flashColor, flashAlpha);
        }
        if (state != State.Idle && glitchTime <= 0f)
        {
            float sus = Mathf.Clamp01(suspicion / MaxSuspicion());
            if (sus > 0.4f)
            {
                float rate = Mathf.Lerp(0.15f, 3f, (sus - 0.4f) / 0.6f);
                if (Random.value < rate * dt)
                {
                    glitchTime = Random.Range(0.06f, 0.18f);
                    glitchPower = Mathf.Lerp(2f, 9f, sus);
                }
            }
        }
        if (glitchTime > 0f)
        {
            glitchTime -= dt;
            if (glitchTime <= 0f) fxRt.anchoredPosition = Vector2.zero;
            else fxRt.anchoredPosition = new Vector2(Random.Range(-glitchPower, glitchPower), Random.Range(-glitchPower, glitchPower) * 0.4f);
        }
    }

    void UpdateSuspicionBar(float dt)
    {
        if (rig != null) rig.SetSuspicion(suspicion / MaxSuspicion());
        SuspicionPop();
        shownSuspicion = Mathf.MoveTowards(shownSuspicion, suspicion, 60f * dt);
        float max = MaxSuspicion();
        float t = Mathf.Clamp01(shownSuspicion / max);
        int lit = Mathf.RoundToInt(t * segments.Length);
        for (int i = 0; i < segments.Length; i++)
        {
            Color c = cDark;
            if (i < lit) c = Color.Lerp(UIKit.Hex("#5ED84A"), UIKit.Hex("#F03A2E"), i / (float)(segments.Length - 1));   // green -> red
            segments[i].color = c;
        }
        suspicionValue.text = Mathf.RoundToInt(shownSuspicion).ToString("000") + "/" + Mathf.RoundToInt(max);
        suspicionLabel.color = (t > 0.8f && Random.value < 0.15f) ? cBright : cNormal;
        suspicionLabel.text = "SUSPICION";
    }

    // =====================================================================
    // interview flow
    // =====================================================================
    bool PrepareData()
    {
        ResumeSession.EnsureLoaded();
        InterviewInventoryBridge.EnsureInitialized();
        InterviewInventoryBridge.BeginInterview();
        SaveSystem.InterviewStarted();   // snapshot inventory + position (items taken during the interview are returned on rejection)
        if (!ResumeSession.Ready)
        {
            Debug.LogError("InterviewUI: interview_data.json not found in a Resources folder.");
            return false;
        }

        InterviewDatabase db = ResumeSession.Database;
        cfg = db.config;
        if (cfg == null || cfg.draw == null || cfg.suspicion == null)
        {
            Debug.LogError("InterviewUI: config / draw / suspicion missing from interview_data.json.");
            return false;
        }

        job = ResumeSession.SelectedJob;
        if (job == null)
        {
            // scene opened directly (testing): fake a resume
            job = db.jobs[Random.Range(0, db.jobs.Length)];
            ResumeSession.SelectedJob = job;
            ResumeSession.SelectedText = job.displayVariants[Random.Range(0, job.displayVariants.Length)];
            Debug.Log("InterviewUI: no resume found, using random test job '" + job.id + "'.");
        }
        if (string.IsNullOrEmpty(ResumeSession.CandidateName))
        {
            ResumeSession.CandidateName = "Test Candidate";
            ResumeSession.Hobby = "chess";
            ResumeSession.EducationScore = 75;
            ResumeSession.RealEducation = 75;
        }

        multiplier = 1f;
        if (cfg.strengthMultipliers != null && cfg.strengthMultipliers.Length > 0)
        {
            int idx = Mathf.Clamp(job.strength - 1, 0, cfg.strengthMultipliers.Length - 1);
            multiplier = cfg.strengthMultipliers[idx];
        }
        return true;
    }

    float MaxSuspicion()
    {
        return (cfg != null && cfg.suspicion != null && cfg.suspicion.maxSuspicion > 0) ? cfg.suspicion.maxSuspicion : 100f;
    }

    enum CounterKind { None, Pressure, Dodge, Contradiction }

    class AnswerEntry
    {
        public QuestionData q;
        public string raw;
        public string shown;
        public AnswerKind kind;
        public int number;
        public bool rechecked;
        public float extra;
        public string note = "";
    }

    static QuestionData Pseudo(string id)
    {
        QuestionData q = new QuestionData();
        q.id = id;
        q.type = "yesno";
        q.bucket = "counter";
        q.difficulty = "easy";
        q.text = "";
        q.correct = "yes";
        q.accepted = new string[0];
        q.tags = new string[0];
        return q;
    }

    static bool IsGrade(QuestionData q)
    {
        return q.tags != null && q.tags.Length > 1 && q.tags[1] == "grade";
    }

    float BehaviorScale()
    {
        return perk == Perk.PokerFace ? 0.5f : 1f;
    }

    bool misconduct, conductSpoken;
    int conductStrikes;

    bool Dead()
    {
        return misconduct || suspicion >= MaxSuspicion();
    }

    // offensive language outside small talk: the interviewer says it once, then the interview ends
    IEnumerator ConductExit()
    {
        if (!misconduct || conductSpoken) yield break;
        conductSpoken = true;
        if (rig != null) rig.Trigger(RigEvent.Warn);
        Pop(Cartoon.Steam);
        yield return Speak(Persona.Offended(3, ResumeSession.CandidateName));
    }

    string QLabel()
    {
        return "Q " + (index + 1).ToString("00") + "/" + questions.Count.ToString("00");
    }

    void SetCounter(string s)
    {
        counterText.text = s;
    }

    // ---------------------------------------------------------------- cartoon FX + live mood
    CartoonFX fx;
    float lastSuspFrac, lastExclaimT;
    bool moodShiftPending, midNudged;

    void InitFx()
    {
        if (fx != null) { fx.Clear(); return; }
        RectTransform layer = UIKit.NewRect("CartoonLayer", fxRt);
        UIKit.Stretch(layer);
        layer.SetAsFirstSibling();
        fx = layer.gameObject.AddComponent<CartoonFX>();
        fx.Init(layer, rig, delegate { return sitCam != null ? sitCam : Camera.main; });
    }

    void Pop(Cartoon c, int level = 1, bool force = false) { if (fx != null) fx.Pop(c, level, force); }

    void Feel(MoodEvent e)
    {
        Mood old = mood;
        mood = InterviewerPersonality.Feel(e, suspicion / MaxSuspicion());
        if (mood == old) return;
        if (rig != null) rig.SetMood(mood);
        RefreshMoodTag();
        moodShiftPending = true;
        switch (mood)
        {
            case Mood.Patient: Pop(Cartoon.Heart, 2); break;
            case Mood.Impatient: Pop(Cartoon.Steam); break;
            case Mood.Paranoid: Pop(Cartoon.Question); break;
            default: Pop(Cartoon.Zzz); break;
        }
    }

    void GreetPop()
    {
        if (Random.value < 0.4f) return;
        switch (mood)
        {
            case Mood.Patient: Pop(Cartoon.Heart, 2); break;
            case Mood.Impatient: Pop(Cartoon.Steam); break;
            case Mood.Paranoid: Pop(Cartoon.Question); break;
            default: Pop(Cartoon.Dots); break;
        }
    }

    // reaction to the pace of the player (slow / long pauses)
    void WaitPop()
    {
        switch (mood)
        {
            case Mood.Patient: Pop(Cartoon.Dots); break;
            case Mood.Impatient: Pop(Cartoon.Steam); break;
            case Mood.Paranoid: Pop(Cartoon.Question); break;
            default: Pop(Cartoon.Zzz); break;
        }
    }

    void CartoonReact(AnswerKind kind)
    {
        if (Random.value > 0.45f) return;   // most answers get no sticker; the "!" on suspicion rises covers the big moments
        switch (kind)
        {
            case AnswerKind.Correct:
                if (mood == Mood.Patient) Pop(Random.value < 0.5f ? Cartoon.Heart : Cartoon.Sparkle);
                break;
            case AnswerKind.Wrong:
                if (mood == Mood.Patient) Pop(Cartoon.Sweat);
                else if (mood == Mood.Impatient) Pop(Cartoon.Anger);
                else if (mood == Mood.Bored) Pop(Cartoon.Zzz);
                break;
            case AnswerKind.Hedge:
                Pop(Cartoon.Question);
                break;
            default:
                if (mood == Mood.Impatient) Pop(Cartoon.Anger); else Pop(Cartoon.Dots);
                break;
        }
    }

    void SuspicionPop()
    {
        float frac = suspicion / MaxSuspicion();
        float rise = frac - lastSuspFrac;
        if (rise < 0f) { lastSuspFrac = frac; return; }
        if (rise < 0.04f || Time.unscaledTime - lastExclaimT < 2f) return;
        int lv = rise >= 0.12f ? 3 : (rise >= 0.06f ? 2 : 1);
        Pop(Cartoon.Exclaim, lv);
        lastSuspFrac = frac; lastExclaimT = Time.unscaledTime;
    }

    void RefreshMoodTag()
    {
        string s = "MOOD " + InterviewerPersonality.Tag(mood) + (Persona.Cur != null ? "  |  " + Persona.Title : "");
        if (impatient) s += " / IMPATIENT";
        moodText.text = s;
    }

    void Begin()
    {
        if (!PrepareData()) return;
        InterviewRoomFixer.Run(transform.position);   // lamp / desk props: make sure nothing floats beside the desk

        List<QuestionData> jobQuestions = QuestionDrawer.Draw(job, cfg.draw);
        questions = QuestionMixer.Mix(jobQuestions, minPersonalQuestions, maxPersonalQuestions,
            ResumeSession.CandidateName, ResumeSession.Hobby, ResumeSession.EducationScore);
        if (questions.Count == 0)
        {
            Debug.LogError("InterviewUI: no questions for job '" + job.id + "'.");
            return;
        }

        Persona.Begin();
        mood = WorldTimer.AdjustMood(Persona.StartMood(InterviewerPersonality.Roll()));   // punctuality (HARD / EXPERT) nudges the starting mood
        InterviewerPersonality.ResetContext();
        InterviewerPersonality.BeginMood(mood);
        moodShiftPending = false;
        perk = ResumeSession.SelectedPerk;
        report = new InterviewReport();
        misconduct = false; conductSpoken = false; conductStrikes = 0;
        history.Clear();
        counterCount = 0;
        sinceCounter = 0;
        slowCount = 0;
        fastCount = 0;
        warnLevel = 0;
        impatient = false;
        proofDone = false;
        resumeReqDone = false;
        requestMode = false;
        luckyUsed = false;
        remarked = false;
        proofIndex = Mathf.Clamp(Random.Range(4, 7), 1, Mathf.Max(1, questions.Count - 2));
        effectiveLimit = answerTimeLimit > 0f
            ? answerTimeLimit * InterviewerPersonality.TimerMultiplier(mood) + (perk == Perk.QuickThinker ? 6f : 0f)
            : 0f;
        pressureQuestion = Pseudo("pressure");
        proofQuestion = Pseudo("proof");
        requestQuestion = Pseudo("request");
        resumeReqIndex = Random.value < resumeRequestChance ? Random.Range(2, Mathf.Max(3, questions.Count - 2)) : -1;
        if (resumeReqIndex == proofIndex) resumeReqIndex = Mathf.Clamp(resumeReqIndex + 1, 1, Mathf.Max(1, questions.Count - 2));
        ProofCheck.Prepare(job);

        impressionLines.Clear();
        FirstImpression.ArrivedLate = WorldTimer.IsLate;
        float start = FirstImpression.Compute(startSuspicion, impressionLines);
        suspicion = Mathf.Clamp(start, 0f, MaxSuspicion());
        shownSuspicion = suspicion;
        hedgeStreak = 0;
        index = 0;
        asked = 0;
        correct = 0;
        committed.Length = 0;
        tail = "";
        logText.text = "";

        EnsureEventSystem();
        FreezePlayer();
        Items.InventoryUI.Locked = true; Items.InventoryUI.NoDrop = true;   // no inventory until the resume is requested
        closing = false;
        if (rig == null) FindRig();
        if (rig != null) rig.SetActive(true, mood);
        InitFx();
        lastSuspFrac = suspicion / MaxSuspicion();

        counterText.text = "";
        RefreshMoodTag();
        resultRoot.SetActive(false);
        HideGrade();
        bottomBlock.gameObject.SetActive(true);
        timerBlock.gameObject.SetActive(true);
        moodBlock.gameObject.SetActive(true);
        HideControls();
        interviewRoot.SetActive(true);
        UpdateSuspicionBar(0f);

        StartCoroutine(RunInterview());
    }

    IEnumerator RunInterview()
    {
        yield return SitSequence();
        yield return Intro();
        yield return SmallTalkStage();
        if (misconduct) { Finish(true); yield break; }

        // the resume is always the first question
        yield return ResumeRequest();
        if (Dead()) { yield return ConductExit(); Finish(true); yield break; }

        if (runCognitiveTest)
        {
            yield return CognitiveStage();
            if (cognitiveFailed)
            {
                Finish(true);
                yield break;
            }
        }

        while (true)
        {
            current = questions[index];
            SetCounter(QLabel());
            committed.Length = 0;
            tail = "";
            Refresh();

            yield return AskQuestion(current.text, current, true, null, null);
            if (misconduct) { yield return ConductExit(); Finish(true); yield break; }

            AnswerEntry e = new AnswerEntry();
            e.q = current;
            e.raw = ansRaw;
            e.shown = ansShown;
            e.number = index + 1;
            e.kind = Evaluate(current, ansRaw);
            float elapsed = ansElapsed;
            bool timedOut = ansTimedOut;
            remarked = false;

            CounterKind plan = PlanCounter(e);
            if (plan == CounterKind.Pressure) yield return Pressure(e);

            ApplyAnswer(e.q, e.kind, true, e.extra, e.note, "");
            history.Add(e);
            NoteAnswer(e);
            yield return new WaitForSecondsRealtime(verdictPause * 0.6f);

            if (!Dead()) yield return Behavior(elapsed, timedOut, e.q.type == "yesno");
            if (!Dead()) yield return React(e.kind, plan);
            if (!Dead() && plan == CounterKind.Dodge) yield return Dodge(e);
            else if (!Dead() && plan == CounterKind.Contradiction) yield return Contradiction(e);
            if (!Dead()) yield return Warnings();

            sinceCounter++;
            yield return new WaitForSecondsRealtime(verdictPause * 0.4f);

            if (Dead())
            {
                yield return ConductExit();
                Finish(true);
                yield break;
            }
            if (index + 1 >= questions.Count)
            {
                Finish(false);
                yield break;
            }
            index++;

            // documents are never requested again after the opening resume question (+ its follow-up)
            if (Dead())
            {
                yield return ConductExit();
                Finish(true);
                yield break;
            }
        }
    }

    IEnumerator Intro()
    {
        state = State.Typing;
        HideControls();
        committed.Length = 0;
        tail = "";
        Refresh();
        Commit("<color=" + HexBright + ">FIRST IMPRESSION</color>");
        for (int i = 0; i < impressionLines.Count; i++)
            Commit("<color=" + HexDim + ">" + Clean(impressionLines[i]) + "</color>");
        Commit("<color=" + HexNormal + ">STARTING SUSPICION : " + Mathf.RoundToInt(suspicion).ToString("000") + "</color>");
        yield return WaitOrKey(5f);

        committed.Length = 0;
        tail = "";
        Refresh();
        GreetPop();
        yield return Speak(Persona.Line(Remark.Greeting, mood));
        yield return WaitOrKey(3f);
        string punctual = WorldTimer.TakeRemark();   // late / too early / on time (HARD & EXPERT only)
        if (punctual != null) { yield return Speak(punctual); yield return WaitOrKey(2.5f); }
    }

    // =====================================================================
    // COGNITIVE TEST (3 chalkboard minigames, pass 2 of 3 to reach the questionnaire)
    // =====================================================================
    CognitiveAssessmentController cognitive;
    bool cognitiveFailed;
    bool cognitiveActive;

    IEnumerator CognitiveStage()
    {
        cognitiveFailed = false;
        state = State.Typing;
        HideControls();
        committed.Length = 0;
        tail = "";
        Refresh();

        yield return Speak("Alright. Let's test your cognitive abilities.");
        yield return WaitOrKey(2.5f);
        yield return Speak("Three short puzzles. Pass at least " + cognitivePassesNeeded + " of them and we move on to the questions.");
        Commit("<color=" + HexDim + ">[ press any key to continue ]</color>");
        yield return WaitOrKey(7f);   // time to read; any key skips

        if (cognitive == null) cognitive = FindFirstObjectByType<CognitiveAssessmentController>();
        if (cognitive == null) cognitive = gameObject.AddComponent<CognitiveAssessmentController>();
        cognitive.manageWorldControls = false;     // the interview already froze the player
        if (safeRt == null || interviewRoot == null) yield break;
        cognitive.Setup(font, safeRt, interviewRoot.transform.GetSiblingIndex());

        CognitiveRoundResult result = null;
        bool done = false;
        cognitiveActive = true;                    // the HUD timer now shows the minigame countdown
        bottomBlock.gameObject.SetActive(false);   // log strip hidden while the games run; suspicion / timer / mood stay
        cognitive.StartCognitiveAssessment(new System.Random(Random.Range(0, int.MaxValue)), r => { result = r; done = true; });
        while (!done) yield return null;
        cognitiveActive = false;
        bottomBlock.gameObject.SetActive(true);
        SetCounter("");
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (result == null) yield break;
        bool passed = result.gamesPassed >= cognitivePassesNeeded;
        report.cognitiveRan = true;
        report.cognitivePassed = result.gamesPassed;
        report.cognitiveTotal = result.gamesTotal;
        report.cognitiveFailed = !passed;
        cognitiveFailed = !passed;
        suspicion = Mathf.Clamp(suspicion + result.netSuspicionDelta, 0f, MaxSuspicion());

        committed.Length = 0;
        tail = "";
        Refresh();
        Commit("<color=" + HexBright + ">COGNITIVE SCORE</color>");
        Commit("<color=" + HexNormal + ">TESTS PASSED   : " + result.gamesPassed + "/" + result.gamesTotal + "</color>");
        Commit("<color=" + HexDim + ">LEVELS CLEARED : " + result.levelsCleared + "/" + (result.levelsCleared + result.levelsFailed) + "</color>");
        Commit("<color=" + HexDim + ">[ press any key to continue ]</color>");
        yield return WaitOrKey(6f);   // time to read the score; any key skips

        if (passed) yield return Speak("Good. Sharp enough. Now for the actual questions.");
        else yield return Speak("That was... not great. I'm afraid we can't continue.");
        yield return WaitOrKey(passed ? 4f : 4.5f);
    }

    IEnumerator WaitOrKey(float seconds)
    {
        yield return null;
        float t = 0f;
        while (t < seconds)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) yield break;
            t += PauseMenu.Dt;
            yield return null;
        }
    }

    IEnumerator Speak(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        remarked = true;
        yield return TypeLine("INTERVIEWER> ", text, HexDim, HexBright);
        yield return new WaitForSecondsRealtime(0.35f);
    }

    IEnumerator AskQuestion(string text, QuestionData q, bool allowMaybe, string yesLabel, string noLabel)
    {
        state = State.Typing;
        current = q;
        HideControls();
        bool hard = q != null && q.difficulty != null && q.difficulty.ToLowerInvariant().Contains("hard");
        if (hard ? Random.value < 0.6f : Random.value < 0.08f) Pop(Cartoon.Question);
        yield return TypeLine("INTERVIEWER> ", text, HexDim, HexBright);
        ShowControls(allowMaybe, yesLabel, noLabel);
        while (state == State.Asking) yield return null;
    }

    void ShowControls(bool allowMaybe, string yesLabel, string noLabel)
    {
        bool yesno = current.type == "yesno";
        yesNoRow.SetActive(yesno);
        shortRow.SetActive(!yesno);
        yesLabelCur = yesLabel;
        noLabelCur = noLabel;
        if (yesno)
        {
            if (requestMode) SetupRequestButtons();
            else
            {
                maybeAllowed = allowMaybe;
                yesBtn.gameObject.SetActive(true);   // the show-resume/proof request hides these when an item slot is empty;
                noBtn.gameObject.SetActive(true);    // restore them for every normal question
                maybeBtn.gameObject.SetActive(allowMaybe);
                yesBtn.SetLabel(yesLabel != null ? "[Y] " + yesLabel : "[Y] YES");
                noBtn.SetLabel(noLabel != null ? "[N] " + noLabel : "[N] NO");
                yesBtn.Reveal(0f);
                noBtn.Reveal(0.08f);
                if (allowMaybe) maybeBtn.Reveal(0.16f);
            }
        }
        else
        {
            input.text = "";
            input.interactable = true;
            input.ActivateInputField();
        }
        askStart = Time.unscaledTime;
        timeLeft = effectiveLimit;
        midNudged = false;
        timerGroup.SetActive(true);
        state = State.Asking;
        VoiceOnQuestion();
    }

    void HideControls()
    {
        yesNoRow.SetActive(false);
        shortRow.SetActive(false);
        VoiceOnHide();
    }

    void AnswerYesNo(string choice)
    {
        if (state != State.Asking || current == null || current.type != "yesno") return;
        if (handoverMode) { if (choice == "yes") HandoverAction(1); else if (choice == "no") HandoverAction(2); return; }
        if (requestMode) choice = choice == "yes" ? "1" : (choice == "no" ? "2" : (choice == "maybe" ? "3" : choice));
        Submit(choice, false);
    }

    void OnShortSubmit(string raw)
    {
        if (state != State.Asking || current == null || current.type == "yesno") return;
        if (AnswerEvaluator.Normalize(raw).Length == 0)
        {
            input.ActivateInputField();
            return;
        }
        Submit(raw, false);
    }

    void OnTimeUp()
    {
        string typed = current.type == "yesno" ? "" : input.text;
        Submit(typed, true);
    }

    void Submit(string raw, bool timedOut)
    {
        if (state != State.Asking) return;
        state = State.Feedback;
        if (!chatMode && Profanity.Is(raw)) misconduct = true;   // offensive answer in the questionnaire: rejected on the spot
        ansRaw = raw;
        ansTimedOut = timedOut;
        ansElapsed = Time.unscaledTime - askStart;

        string shown;
        if (AnswerEvaluator.Normalize(raw).Length == 0) shown = "(no response)";
        else if (requestMode) shown = RequestRules.ShownName(raw);
        else if (current.type == "yesno")
        {
            if (raw == "yes" && yesLabelCur != null) shown = yesLabelCur;
            else if (raw == "no" && noLabelCur != null) shown = noLabelCur;
            else shown = raw.ToUpperInvariant();
        }
        else shown = raw;
        ansShown = shown;

        HideControls();
        input.DeactivateInputField();
        Commit("<color=" + HexBright + ">> " + Clean(shown) + "</color>");
    }

    AnswerKind Evaluate(QuestionData q, string raw)
    {
        if (q.type == "yesno")
        {
            if (string.IsNullOrEmpty(raw)) return AnswerKind.None;
            return AnswerEvaluator.EvaluateYesNo(q, raw);
        }
        if (AnswerEvaluator.Normalize(raw).Length == 0) return AnswerKind.None;
        return AnswerEvaluator.EvaluateShort(q, raw, cfg.parsing);
    }

    float ApplyAnswer(QuestionData q, AnswerKind kind, bool main, float extra, string note, string tagOverride)
    {
        float delta = AnswerEvaluator.Delta(kind, q, cfg.suspicion, multiplier * InterviewerPersonality.PenaltyMultiplier(mood), hedgeStreak);
        if (delta > 0f && q.bucket == "personal")
        {
            if (perk != Perk.GoodMemory) delta *= personalPenaltyMultiplier;
            if (IsGrade(q)) delta *= 1f + ResumeSession.Embellishment / 30f;
        }
        if (kind == AnswerKind.Hedge && perk == Perk.SmoothTalker) delta *= 0.5f;

        bool forgiven = false;
        if (kind == AnswerKind.Wrong && perk == Perk.LuckyCharm && !luckyUsed)
        {
            luckyUsed = true;
            forgiven = true;
            delta = 0f;
        }
        delta += extra;

        hedgeStreak = kind == AnswerKind.Hedge ? hedgeStreak + 1 : 0;
        RigReact(kind);
        InterviewerPersonality.Note(kind, Mathf.Max(0f, suspicion + delta) / MaxSuspicion(), asked);
        if (main)
        {
            asked++;
            if (kind == AnswerKind.Correct) correct++;
        }
        suspicion = Mathf.Clamp(suspicion + delta, 0f, MaxSuspicion());

        bool good = kind == AnswerKind.Correct && string.IsNullOrEmpty(tagOverride);
        string text;
        if (!string.IsNullOrEmpty(tagOverride)) text = tagOverride;
        else if (kind == AnswerKind.Correct) text = main ? "consistent" : "follow-up cleared";
        else if (kind == AnswerKind.Hedge) text = " ! EVASIVE ";
        else if (kind == AnswerKind.None) text = " ! NO RESPONSE ";
        else text = " ! MISMATCH ";

        CommitVerdict(good, text, delta, forgiven ? "lucky charm" : note);
        return delta;
    }

    void CommitVerdict(bool good, string text, float delta, string note)
    {
        int d = Mathf.RoundToInt(delta);
        string dtxt = (d >= 0 ? "+" : "") + d;
        string suffix = string.IsNullOrEmpty(note) ? "" : " (" + note + ")";
        if (good)
        {
            Commit("<color=" + HexNormal + ">[ OK ]</color> <color=" + HexDim + ">" + text + "  suspicion " + dtxt + suffix + "</color>");
            flashColor = cNormal;
            flashAlpha = 0.12f;
        }
        else
        {
            Commit("<mark=" + HexNormal + "><color=" + HexBg + ">" + text + "</color></mark> <color=" + HexDim + ">suspicion " + dtxt + suffix + "</color>");
            flashColor = cBright;
            flashAlpha = 0.35f;
            glitchTime = 0.3f;
            glitchPower = 8f;
            impulse = Mathf.Max(impulse, 0.6f);
        }
    }

    void AddBehaviorSuspicion(string tag, float amount)
    {
        if (rig != null) rig.Trigger(RigEvent.NoAnswer);
        float a = amount * BehaviorScale();
        suspicion = Mathf.Clamp(suspicion + a, 0f, MaxSuspicion());
        CommitVerdict(false, tag, a, "");
    }

    void NoteAnswer(AnswerEntry e)
    {
        if (e.kind == AnswerKind.Correct) return;
        string tag = e.kind == AnswerKind.Wrong ? (e.q.bucket == "personal" ? "LIE" : "MISMATCH") : (e.kind == AnswerKind.Hedge ? "EVASIVE" : "SILENT");
        report.notes.Add("Q" + e.number.ToString("00") + " " + tag + " " + Debrief.Short(e.q.text, 30));
    }

    bool CanCounter()
    {
        return counterCount < maxCounterQuestions && sinceCounter >= minGapBetweenCounters && index + 1 < questions.Count && !Dead();
    }

    bool HasRecheck(AnswerEntry exclude)
    {
        for (int i = 0; i < history.Count; i++)
        {
            AnswerEntry h = history[i];
            if (h == exclude || h.rechecked) continue;
            if (h.kind == AnswerKind.Correct || h.kind == AnswerKind.Wrong) return true;
        }
        return false;
    }

    AnswerEntry PickRecheck(AnswerEntry exclude)
    {
        List<AnswerEntry> pool = new List<AnswerEntry>();
        for (int i = 0; i < history.Count; i++)
        {
            AnswerEntry h = history[i];
            if (h == exclude || h.rechecked) continue;
            if (h.kind != AnswerKind.Correct && h.kind != AnswerKind.Wrong) continue;
            pool.Add(h);
            if (h.q.bucket == "personal") pool.Add(h);
        }
        return pool.Count == 0 ? null : pool[Random.Range(0, pool.Count)];
    }

    CounterKind PlanCounter(AnswerEntry e)
    {
        if (!CanCounter()) return CounterKind.None;
        if (e.kind == AnswerKind.Hedge) return Random.value < 0.55f ? CounterKind.Dodge : CounterKind.None;
        if (e.kind == AnswerKind.None) return CounterKind.None;

        float chance = baseCounterChance * InterviewerPersonality.CounterFactor(mood) * (1f + suspicion / MaxSuspicion());
        if (Random.value >= chance) return CounterKind.None;

        bool canPressure = e.q.type == "yesno";
        bool canContra = index >= 2 && HasRecheck(null);
        if (canPressure && canContra) return Random.value < 0.5f ? CounterKind.Pressure : CounterKind.Contradiction;
        if (canPressure) return CounterKind.Pressure;
        if (canContra) return CounterKind.Contradiction;
        return CounterKind.None;
    }

    void BeginCounter()
    {
        counterCount++;
        sinceCounter = 0;
        report.counters++;
        SetCounter("FOLLOW-UP");
    }

    IEnumerator Pressure(AnswerEntry e)
    {
        if (rig != null) rig.Trigger(RigEvent.Pressure);
        BeginCounter();
        yield return AskQuestion(Persona.Line(Remark.Pressure, mood), pressureQuestion, false, "KEEP IT", "CHANGE");
        bool change = !ansTimedOut && ansRaw == "no";
        if (change)
        {
            e.raw = e.raw == "yes" ? "no" : "yes";
            e.shown = e.raw.ToUpperInvariant();
            e.kind = Evaluate(e.q, e.raw);
            e.extra = 3f * BehaviorScale();
            e.note = "changed answer";
            Commit("<color=" + HexDim + ">final answer: " + e.shown + "</color>");
        }
        else if (e.kind == AnswerKind.Correct)
        {
            e.extra = -2f;
            e.note = "held firm";
        }
        else if (e.kind == AnswerKind.Wrong)
        {
            e.extra = 3f * BehaviorScale();
            e.note = "doubled down";
        }
    }

    IEnumerator Dodge(AnswerEntry e)
    {
        if (rig != null) rig.Trigger(RigEvent.Pressure);
        BeginCounter();
        yield return AskQuestion(Persona.Line(Remark.Dodge, mood) + " " + e.q.text, e.q, false, null, null);
        AnswerKind k = Evaluate(e.q, ansRaw);
        ApplyAnswer(e.q, k, false, 0f, "", "");
    }

    IEnumerator Contradiction(AnswerEntry latest)
    {
        if (rig != null) rig.Trigger(RigEvent.Pressure);
        AnswerEntry t = PickRecheck(latest);
        if (t == null) yield break;
        BeginCounter();
        t.rechecked = true;

        Feel(MoodEvent.Contradiction);
        Pop(Cartoon.Exclaim, 2);
        string intro = Persona.Line(Remark.Contradiction, mood).Replace("{0}", Debrief.Short(t.shown, 24));
        yield return AskQuestion(intro + " " + t.q.text, t.q, true, null, null);

        AnswerKind k = Evaluate(t.q, ansRaw);
        bool same = t.q.type == "yesno" ? t.raw == ansRaw : t.kind == k;
        if (k == AnswerKind.Hedge || k == AnswerKind.None)
        {
            ApplyAnswer(t.q, k, false, 0f, "", "");
        }
        else if (same)
        {
            ApplyAnswer(t.q, k, false, 0f, k == AnswerKind.Wrong ? "doubled down" : "", "");
        }
        else
        {
            report.contradictions++;
            report.notes.Add("CONTRADICTION " + Debrief.Short(t.q.text, 28));
            ApplyAnswer(t.q, k, false, contradictionPenalty * multiplier, "story changed", " ! CONTRADICTION ");
            yield return Speak(mood == Mood.Paranoid ? "Aha. Your story just changed." : "That is not what you said earlier.");
        }
    }

    IEnumerator Behavior(float elapsed, bool timedOut, bool yesno)
    {
        bool slow = timedOut || (effectiveLimit > 0f && elapsed > effectiveLimit * slowFraction);
        bool fast = !timedOut && elapsed < (yesno ? fastSecondsYesNo : fastSecondsShort);

        if (slow)
        {
            slowCount++;
            report.slow++;
            Feel(MoodEvent.Slow);
            WaitPop();
            if (slowCount == 2)
            {
                yield return Speak(Persona.Line(Remark.SlowMild, mood));
            }
            else if (slowCount >= 3)
            {
                if (!impatient)
                {
                    impatient = true;
                    effectiveLimit *= 0.8f;
                    RefreshMoodTag();
                }
                yield return Speak(Persona.Line(Remark.Impatient, mood));
                AddBehaviorSuspicion(" ! IMPATIENT ", slowCount == 3 ? slowPenalty : slowPenalty * 0.6f);
            }
        }
        else if (fast)
        {
            fastCount++;
            report.fast++;
            Feel(MoodEvent.Fast);
            if (Random.value < 0.25f) Pop(Cartoon.Question);
            yield return Speak(Persona.Line(Remark.Fast, mood));
            if (fastCount >= 2) AddBehaviorSuspicion(" ! TOO QUICK ", fastPenalty);
        }
    }

    IEnumerator React(AnswerKind kind, CounterKind plan)
    {
        if (!remarked && moodShiftPending && Random.value < 0.6f)
        {
            moodShiftPending = false;
            yield return Speak(Persona.Shift(mood));
        }
        moodShiftPending = false;
        if (remarked || plan != CounterKind.None) yield break;
        float chance = kind == AnswerKind.Correct ? 0.22f : 0.5f;
        if (Random.value >= chance) yield break;
        Remark r = Remark.Wrong;
        if (kind == AnswerKind.Correct) r = Remark.Correct;
        else if (kind == AnswerKind.Hedge) r = Remark.Hedge;
        else if (kind == AnswerKind.None) r = Remark.NoAnswer;
        yield return Speak(Persona.Line(r, mood));
    }

    IEnumerator Warnings()
    {
        float t = suspicion / MaxSuspicion();
        if (warnLevel < 1 && t >= 0.6f)
        {
            warnLevel = 1;
            if (rig != null) rig.Trigger(RigEvent.Warn);
            yield return Speak(Persona.Line(Remark.Warn1, mood));
        }
        else if (warnLevel < 2 && t >= 0.8f)
        {
            warnLevel = 2;
            if (rig != null) rig.Trigger(RigEvent.Warn);
            yield return Speak(Persona.Line(Remark.Warn2, mood));
        }
    }

    bool ShouldProof()
    {
        if (!proofEnabled || proofDone) return false;
        if (index >= questions.Count - 1) return false;
        return index >= proofIndex || suspicion >= MaxSuspicion() * proofSuspicionTrigger;
    }

    bool ShouldResumeRequest()
    {
        if (!proofEnabled || resumeReqDone || resumeReqIndex < 0) return false;
        if (index >= questions.Count - 1) return false;
        return index >= resumeReqIndex;
    }

    IEnumerator Proof()
    {
        proofDone = true;
        RequestRules.Shown.Clear();
        yield return RunRequest(RequestRules.ForProof(job));
    }

    IEnumerator ResumeRequest()
    {
        resumeReqDone = true;
        RequestRules.Shown.Clear();
        RequestRules.GaveAny = false;
        yield return RunRequest(RequestRules.ForResume());
        // the interviewer MAY follow up for an additional document, only right after the resume question
        if (RequestRules.GaveAny && !Dead() && Random.value < Mathf.Clamp01(0.4f + 0.4f * suspicion / MaxSuspicion())) yield return RunExtras();
    }

    IEnumerator RunRequest(InterviewRequest req)
    {
        SetCounter(req.label);
        committed.Length = 0;
        tail = "";
        Refresh();

        string ask = Persona.Line(Remark.ProofAsk, mood).Replace("{0}", req.item);
        yield return AskHandover(ask, req.tag == "resume");

        float delta;
        string result;
        bool good;
        string tag;
        Remark reply;
        if (ansTimedOut || string.IsNullOrEmpty(ansRaw))
        {
            delta = cfg.suspicion.noAnswer * multiplier;
            result = "NO RESPONSE";
            tag = " ! NO RESPONSE ";
            good = false;
            reply = Remark.ProofBad;
        }
        else if (ansRaw == "none")
        {
            float chance = Mathf.Clamp(0.55f - suspicion / MaxSuspicion() * 0.4f, 0.15f, 0.55f);
            if (Random.value < chance) { delta = 2f; result = "BLUFF WORKED"; tag = "bluff accepted"; good = true; reply = Remark.BluffOk; }
            else { delta = 12f; result = "BLUFF CALLED"; tag = " ! BLUFF CALLED "; good = false; reply = Remark.BluffBad; }
        }
        else
        {
            Items.ItemInstance given = RequestRules.Handed;
            RequestOutcome o = (req.tag == "resume" && !handoverOther) ? RequestRules.JudgeItem(req, given, job) : RequestRules.JudgeExtra(given, job);
            if (given != null) { RequestRules.GaveAny = true; TakeFromPlayer(given); }   // handed over = gone from the inventory
            delta = o.delta;
            result = o.result;
            tag = o.tag;
            good = o.good;
            reply = o.reply;
        }
        if (delta > 0f) delta *= multiplier * InterviewerPersonality.PenaltyMultiplier(mood);

        suspicion = Mathf.Clamp(suspicion + delta, 0f, MaxSuspicion());
        string shortResult = req.shortName + (good ? " OK" : " FAIL");
        report.proofResult = string.IsNullOrEmpty(report.proofResult) ? shortResult : report.proofResult + ", " + shortResult;
        report.proofFailed = report.proofFailed || !good;
        report.notes.Add(req.label + ": " + result);
        Feel(good ? MoodEvent.ProofOk : MoodEvent.ProofBad);
        if (good) Pop(Cartoon.Sparkle); else Pop(Cartoon.Exclaim, 2);
        CommitVerdict(good, tag, delta, "");
        yield return new WaitForSecondsRealtime(verdictPause * 0.6f);
        if (!Dead()) yield return Speak(Persona.Line(reply, mood));
        yield return new WaitForSecondsRealtime(verdictPause * 0.4f);
        if (RequestRules.PendingBluff != null && !Dead()) yield return RunBluff(RequestRules.PendingBluff);
    }

    IEnumerator AskPick(string text, string[] labels, string hint)
    {
        RequestRules.PickLabels = labels;
        RequestRules.PickHint = hint;
        requestMode = true;
        yield return AskQuestion(text, requestQuestion, false, null, null);
        requestMode = false;
        RequestRules.PickLabels = null;
        RequestRules.PickHint = null;
    }

    bool HasRemainingItems()
    {
        return Items.Inventory.Current.Count > 0;
    }

    // Stage 8a steps 3-5: "Any additional documents?" -> pick extras one by one, N = done.
    IEnumerator RunExtras()
    {
        if (Dead() || !HasRemainingItems()) yield break;
        SetCounter("EXTRAS");
        yield return AskPick("Any additional documents to show?", new[] { "YES", "NO" }, "press 1 for yes, 2 / N for no");
        if (ansTimedOut || ansRaw != "1") yield break;
        while (!Dead() && HasRemainingItems())
        {
            yield return AskHandover("Which one?", false);
            if (ansTimedOut || ansRaw != "give" || RequestRules.Handed == null) break;
            Items.ItemInstance item = RequestRules.Handed;
            RequestOutcome o = RequestRules.JudgeExtra(item, job);
            TakeFromPlayer(item);
            float delta = o.delta;
            if (delta > 0f) delta *= multiplier * InterviewerPersonality.PenaltyMultiplier(mood);
            suspicion = Mathf.Clamp(suspicion + delta, 0f, MaxSuspicion());
            report.notes.Add("EXTRA: " + o.result);
            Feel(o.good ? MoodEvent.ProofOk : MoodEvent.ProofBad);
            if (o.good) Pop(Cartoon.Sparkle); else Pop(Cartoon.Exclaim, 2);
            CommitVerdict(o.good, o.tag, delta, "");
            yield return new WaitForSecondsRealtime(verdictPause * 0.6f);
            if (!Dead()) yield return Speak(Persona.Line(o.reply, mood));
            yield return new WaitForSecondsRealtime(verdictPause * 0.4f);
            if (RequestRules.PendingBluff != null && !Dead()) yield return RunBluff(RequestRules.PendingBluff);
        }
    }

    // Stage 8f: item carries someone else's name -> Explain honestly / Bluff / Stay silent. One roll per item.
    IEnumerator RunBluff(Items.ItemInstance item)
    {
        RequestRules.PendingBluff = null;
        if (item == null || InterviewInventoryBridge.HasBluffRoll(item)) yield break;
        int chance = Items.BluffConfig.Chance(item, mood);
        string hint = Items.BluffConfig.ShowHint ? " " + chance + "%" : "";
        SetCounter("DOC");
        yield return AskPick(InterviewInventoryBridge.QuestionFor(item), new[] { "EXPLAIN", "BLUFF" + hint, "SILENT" }, "press 1 explain honestly, 2 bluff, 3 / N stay silent");
        if (Dead()) yield break;

        float delta = 0f; bool good = true; string result, tag; Remark reply;
        if (!ansTimedOut && ansRaw == "2")
        {
            bool ok = Random.Range(0, 100) < chance;
            InterviewInventoryBridge.SetBluffRoll(item, ok);
            if (ok)
            {
                delta = InterviewInventoryBridge.BluffReduction(item);   // reduced by one tier
                result = "BLUFF WORKED"; tag = "bluff accepted: " + Items.BluffConfig.Line(item); reply = Remark.BluffOk;
                Feel(MoodEvent.Polite);
            }
            else
            {
                delta = Items.SuspicionConfig.BluffFailExtra;
                good = false; result = "BLUFF CALLED"; tag = " ! BLUFF CALLED "; reply = Remark.BluffBad;
                Feel(MoodEvent.ProofBad);
            }
            Debug.Log("[Interview] bluff on " + item.def.name + ": chance " + chance + "% -> " + (ok ? "success" : "fail") + " (" + delta + ")");
        }
        else if (!ansTimedOut && ansRaw == "1") { InterviewInventoryBridge.SetBluffRoll(item, false); result = "EXPLAINED"; tag = "explained honestly"; reply = Remark.ProofOk; }
        else { InterviewInventoryBridge.SetBluffRoll(item, false); good = false; result = "SILENT"; tag = " ... SILENCE ... "; reply = Remark.Hedge; }

        InterviewInventoryBridge.AdjustRound((int)delta);
        if (delta > 0f) delta *= multiplier * InterviewerPersonality.PenaltyMultiplier(mood);
        suspicion = Mathf.Clamp(suspicion + delta, 0f, MaxSuspicion());
        report.notes.Add("BLUFF: " + result);
        if (good) Pop(Cartoon.Sparkle); else Pop(Cartoon.Exclaim, 2);
        CommitVerdict(good, tag, delta, "");
        yield return new WaitForSecondsRealtime(verdictPause * 0.6f);
        if (!Dead()) yield return Speak(Persona.Line(reply, mood));
        yield return new WaitForSecondsRealtime(verdictPause * 0.4f);
    }

    // ---------------- document hand-over ----------------
    Items.ItemInstance HeldNow()
    {
        if (hold == null) hold = FindAnyObjectByType<Items.ItemHold>();
        return hold != null ? hold.Current : null;
    }

    IEnumerator AskHandover(string text, bool resumeMode)
    {
        RequestRules.Handed = null;
        handoverMode = true; handoverResume = resumeMode; handoverOther = false; handoverKey = null;
        Items.InventoryUI.Locked = false;           // inventory opens only once documents are requested
        requestMode = true;
        yield return AskQuestion(text, requestQuestion, false, null, null);
        requestMode = false; handoverMode = false;
        Items.InventoryUI.RequestClose();
        Items.InventoryUI.Locked = true;
    }

    void HandoverTick(Keyboard kb)
    {
        Items.ItemInstance held = HeldNow();
        bool heldResume = held != null && held.def.docType == Items.DocType.Resume;
        string key;
        if (handoverResume && !handoverOther) key = heldResume ? "give" : (InterviewInventoryBridge.HasResume ? "equipRes" : "choose");
        else key = held != null ? "give" : "equipDoc";
        if (key != handoverKey)
        {
            handoverKey = key;
            bool skip = !handoverResume || handoverOther;   // "none" is allowed when no resume is involved
            string yes = null, no = null, hint = null;
            switch (key)
            {
                case "give": yes = "[E] GIVE " + (heldResume ? "RESUME" : Debrief.Short(held.def.name.ToUpperInvariant(), 14)); hint = "press E to hand it over"; if (skip) no = handoverResume ? "[N] NO DOCUMENT" : "[N] NO MORE"; break;
                case "equipRes": yes = "[TAB] EQUIP YOUR RESUME"; hint = "open your inventory (TAB) and hold your resume in hand"; break;
                case "equipDoc": yes = "[TAB] EQUIP DOCUMENT"; hint = "open your inventory (TAB) and hold the document in hand"; no = handoverResume ? "[N] NO DOCUMENT" : "[N] NO MORE"; break;
                case "choose": yes = "[O] SHOW OTHER DOCUMENT"; no = "[N] NO DOCUMENT"; hint = "you have no resume"; break;
            }
            yesBtn.gameObject.SetActive(true); yesBtn.SetLabel(yes); yesBtn.Reveal(0f);
            noBtn.gameObject.SetActive(no != null); if (no != null) { noBtn.SetLabel(no); noBtn.Reveal(0.08f); }
            maybeBtn.gameObject.SetActive(false);
            if (hint != null) Commit("<color=" + HexDim + ">   " + hint + "</color>");
        }
        if (kb == null) return;
        if (kb.eKey.wasPressedThisFrame || kb.oKey.wasPressedThisFrame) HandoverAction(1);
        else if (kb.nKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) HandoverAction(2);
    }

    void HandoverAction(int which)
    {
        if (state != State.Asking) return;
        string key = handoverKey;
        if (which == 1)
        {
            yesBtn.Pulse();
            if (key == "give") { RequestRules.Handed = HeldNow(); if (RequestRules.Handed != null) Submit("give", false); }
            else if (key == "equipRes" || key == "equipDoc") Items.InventoryUI.RequestOpen();
            else if (key == "choose") { handoverOther = true; handoverKey = null; }   // now equip any document to give
        }
        else
        {
            // "no document" is offered when there is no resume, or when no resume is involved (follow-ups)
            bool noneAllowed = key == "choose" || key == "equipDoc" || (key == "give" && (!handoverResume || handoverOther));
            if (noneAllowed) { noBtn.Pulse(); Submit("none", false); }
        }
    }

    void TakeFromPlayer(Items.ItemInstance item)
    {
        if (item == null) return;
        var inv = Items.Inventory.Current; int s = inv.IndexOf(item);
        if (s >= 0) inv.Remove(s);                                  // ItemHold un-equips it automatically
        if (hold != null && hold.Current == item) hold.Unequip();
        Items.InventoryUI.RequestClose();
    }

    void SetupRequestButtons()
    {
        maybeAllowed = false;
        if (handoverMode) { handoverKey = null; maybeBtn.gameObject.SetActive(false); return; }   // HandoverTick fills the buttons
        if (RequestRules.PickLabels != null)
        {
            SetPickButton(yesBtn, 0, 0f); SetPickButton(noBtn, 1, 0.08f); SetPickButton(maybeBtn, 2, 0.16f);
            Commit("<color=" + HexDim + ">   " + (RequestRules.PickHint ?? "press a number to choose") + "</color>");
            return;
        }
        SetRequestButton(yesBtn, 0, "[1] ", 0f);
        SetRequestButton(noBtn, 1, "[2] ", 0.08f);
        SetRequestButton(maybeBtn, 2, "[3] ", 0.16f);
        Commit("<color=" + HexDim + ">   " + (RequestRules.PickHint ?? "press 1-3 to show it, N for nothing / at home") + "</color>");
    }

    void SetPickButton(GlitchButton b, int i, float delay)
    {
        string[] l = RequestRules.PickLabels;
        b.gameObject.SetActive(i < l.Length);
        if (i >= l.Length) return;
        b.SetLabel("[" + (i + 1) + "] " + l[i]);
        b.Reveal(delay);
    }

    void SetRequestButton(GlitchButton b, int slot, string prefix, float delay)
    {
        Items.ItemInstance item = InterviewInventoryBridge.Get(slot);
        if (RequestRules.Shown.Contains(slot)) item = null;
        string label = item != null ? Debrief.Short(item.def.name.ToUpperInvariant(), 14) : null;
        b.gameObject.SetActive(label != null);
        if (label == null) return;
        b.SetLabel(prefix + label);
        b.Reveal(delay);
    }

    bool RequestSlotUsable(int slot)
    {
        if (RequestRules.PickLabels != null) return slot >= 0 && slot < RequestRules.PickLabels.Length;
        if (RequestRules.Shown.Contains(slot)) return false;
        return slot >= 0 && slot < Items.Inventory.SlotCount &&
               InterviewInventoryBridge.Get(slot) != null;
    }

    void HandleRequestKeys(Keyboard kb)
    {
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame && RequestSlotUsable(0)) { yesBtn.Pulse(); AnswerYesNo("1"); }
        else if (kb.digit2Key.wasPressedThisFrame && RequestSlotUsable(1)) { noBtn.Pulse(); AnswerYesNo("2"); }
        else if (kb.digit3Key.wasPressedThisFrame && RequestSlotUsable(2)) { maybeBtn.Pulse(); AnswerYesNo("3"); }
        else if (kb.nKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) AnswerYesNo("none");
    }

    void Finish(bool forcedFail)
    {
        report.forced = forcedFail;
        report.misconduct = misconduct;
        report.suspicion = suspicion;
        report.max = MaxSuspicion();
        report.passFraction = passBelowFraction;
        report.passScore = passScore;
        report.asked = asked;
        report.correct = correct;
        report.job = job;
        report.mood = mood;
        report.embellishment = ResumeSession.Embellishment;
        report.realEducation = ResumeSession.RealEducation;
        report.claimedEducation = ResumeSession.EducationScore;

        string title;
        bool passed;
        string detail = Debrief.Build(report, out title, out passed);
        InterviewResult.Report = title + "\n" + detail;
        if (!SaveSystem.FinalizeRun() && SaveSystem.Data != null)     // history first, then the resumable slot is removed; idempotent
            Debug.LogError("InterviewUI: final report could not be saved to History.");
        if (rig != null) rig.SetOutcome(passed);
        if (passed) Pop(Cartoon.Heart, 2, true);
        else Pop(Cartoon.Exclaim, 3, true);

        state = State.Result;
        HideControls();
        bottomBlock.gameObject.SetActive(false);
        timerBlock.gameObject.SetActive(false);
        moodBlock.gameObject.SetActive(false);
        counterText.text = "";

        string col = passed ? HexNormal : HexRed;
        resultTitle.text = "<color=" + col + ">" + title + "</color>";
        if (passed) continueBtn.SetTextColors(cNormal, cBright);
        else continueBtn.SetTextColors(UIKit.Hex(HexRed), UIKit.Hex(HexRedBright));
        resultDetail.text = "<color=" + col + ">" + detail + "</color>";
        resultRoot.SetActive(true);
        ShowGrade(InterviewResult.Rating);
        continueBtn.Reveal(0.3f);

        if (!passed)
        {
            glitchTime = 0.5f;
            glitchPower = 10f;
            impulse = 1f;
        }
    }

    void StartShake()
    {
        shakeCam = null;
        if (playerRoot != null) shakeCam = playerRoot.GetComponentInChildren<Camera>();
        if (shakeCam == null) shakeCam = Camera.main;
        if (shakeCam == null) return;
        camBasePos = shakeCam.transform.localPosition;
        camBaseRot = shakeCam.transform.localRotation;
        shaking = true;
        impulse = 0f;
        shakeSeed = Random.value * 100f;
    }

    void StopShake()
    {
        if (shaking && shakeCam != null)
        {
            shakeCam.transform.localPosition = camBasePos;
            shakeCam.transform.localRotation = camBaseRot;
        }
        shaking = false;
    }

    void LateUpdate()
    {
        if (!shaking || shakeCam == null) return;
        float t = suspicion / MaxSuspicion();
        float level = cameraShake ? Mathf.Clamp01((t - shakeStart) / Mathf.Max(0.01f, 1f - shakeStart)) : 0f;
        float mag = Mathf.Max(level, impulse);
        impulse = Mathf.MoveTowards(impulse, 0f, PauseMenu.Dt * 1.5f);

        float time = Time.unscaledTime * (6f + 10f * mag);
        float nx = Mathf.PerlinNoise(shakeSeed, time) - 0.5f;
        float ny = Mathf.PerlinNoise(shakeSeed + 10f, time) - 0.5f;
        float nz = Mathf.PerlinNoise(shakeSeed + 20f, time) - 0.5f;
        Transform tr = shakeCam.transform;
        tr.localRotation = camBaseRot * Quaternion.Euler(ny * maxShakeDegrees * 2f * mag, nx * maxShakeDegrees * 2f * mag, nz * maxShakeDegrees * mag);
        tr.localPosition = camBasePos + new Vector3(nx, ny, 0f) * maxShakeShift * 2f * mag;
    }

    void Close()
    {
        if (closing) return;
        closing = true;
        Items.InventoryUI.Locked = false; Items.InventoryUI.NoDrop = false;
        VoiceUnload();
        interviewRoot.SetActive(false);
        StopShake();
        if (rig != null) rig.SetActive(false, mood);
        if (fx != null) fx.Clear();
        StartCoroutine(CloseSeq());
    }

    IEnumerator CloseSeq()
    {
        yield return StandUp();
        UnfreezePlayer();
        state = State.Idle;
        finished = true;
        inZone = false;
        closing = false;

        if (SaveSystem.Active >= 0 && SaveSystem.Data != null)
        {
            SaveSystem.CompleteGame();   // HIRED and REJECTED both end the run and return to the main menu (never reloads the room)
            yield break;
        }

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            if (Application.CanStreamedLevelBeLoaded(nextSceneName))
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
            else
                Debug.LogWarning("InterviewUI: scene '" + nextSceneName + "' is not in Build Settings.");
        }
    }

    // =====================================================================
    // log
    // =====================================================================
    IEnumerator TypeLine(string prefix, string text, string prefixColor, string textColor)
    {
        text = Clean(text);
        bool iv = rig != null && prefix.StartsWith("INTERVIEWER");
        if (iv) rig.SetSpeaking(true);
        int total = prefix.Length + text.Length;
        float t = 0f;
        int shown = 0;

        yield return null; // lets the key press that started this line pass

        while (shown < total)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame)
            {
                shown = total;
            }
            else
            {
                t += PauseMenu.Dt * charsPerSecond;
                shown = Mathf.Min(total, Mathf.FloorToInt(t));
            }
            tail = Partial(prefix, text, prefixColor, textColor, shown);
            Refresh();
            if (shown < total) yield return null;
        }
        tail = "";
        Commit(Partial(prefix, text, prefixColor, textColor, total));
        if (iv) rig.SetSpeaking(false);
    }

    static string Partial(string prefix, string text, string pc, string tc, int shown)
    {
        if (shown <= prefix.Length)
            return "<color=" + pc + ">" + prefix.Substring(0, shown) + "</color>";
        return "<color=" + pc + ">" + prefix + "</color><color=" + tc + ">" + text.Substring(0, shown - prefix.Length) + "</color>";
    }

    void Commit(string richLine)
    {
        committed.Append(richLine).Append('\n');
        if (committed.Length > 8000)
        {
            int cut = committed.ToString().IndexOf('\n', 4000);
            if (cut > 0) committed.Remove(0, cut + 1);
        }
        Refresh();
    }

    void Refresh()
    {
        // This callback can arrive during scene teardown / save reload. Unity objects that
        // have been destroyed compare equal to null, so never touch their RectTransforms.
        if (this == null || logText == null || logPanel == null)
            return;

        RectTransform panel = logPanel;
        TextMeshProUGUI text = logText;
        if (panel == null || text == null || text.rectTransform == null)
            return;

        string full = committed.ToString() + tail + voiceLine;
        text.text = full;

        // The strip only exists while there is text, and is only as tall as the text.
        bool has = full.Length > 0;
        panel.gameObject.SetActive(has);
        if (!has) return;

        float maxH = S(LogHeight);
        float h = maxH;
        float w = text.rectTransform.rect.width;
        if (w > 1f)
        {
            float textH = text.GetPreferredValues(full.TrimEnd('\n'), w, 0f).y;
            h = Mathf.Clamp(textH + S(22f), S(44f), maxH);
        }
        panel.sizeDelta = new Vector2(0f, h);
    }

    static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("<", "").Replace(">", "");
    }

    // =====================================================================
    // player freeze / cursor / event system
    // =====================================================================
    void FindRig()
    {
        if (rig == null) rig = GetComponentInChildren<InterviewerRig>();
        if (rig == null) rig = FindFirstObjectByType<InterviewerRig>();
        if (rig != null) return;
        Animator best = null;
        float bd = 6f;
        foreach (Animator a in FindObjectsByType<Animator>(FindObjectsSortMode.None))
        {
            if (!HasHead(a.transform)) continue;
            if (playerRoot != null && a.transform.IsChildOf(playerRoot)) continue;
            float d = Vector3.Distance(a.transform.position, transform.position);
            if (d < bd) { bd = d; best = a; }
        }
        if (best != null) rig = best.gameObject.AddComponent<InterviewerRig>();
    }

    static bool HasHead(Transform r)
    {
        foreach (Transform t in r.GetComponentsInChildren<Transform>()) if (t.name == "Head") return true;
        return false;
    }

    static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }

    Vector3 InterviewerPos() { return rig != null ? rig.transform.position : transform.position; }

    Vector3 ResolveSeat()
    {
        Vector3 p = playerRoot != null ? playerRoot.position : transform.position;
        Vector3 ip = InterviewerPos();
        Transform best = seatPoint;
        Vector3 bestPos = Vector3.zero;
        if (best != null) bestPos = best.position;
        else
        {
            float bd = 5f;
            foreach (Transform t0 in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!IsChairName(t0.name)) continue;
                if (playerRoot != null && t0.IsChildOf(playerRoot)) continue;
                Transform t = t0;
                while (t.parent != null && IsChairName(t.parent.name)) t = t.parent;
                Vector3 c = t.position;
                Renderer[] rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    Bounds bb = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) bb.Encapsulate(rs[i].bounds);
                    c = bb.center;
                }
                if (Vector3.Distance(Flat(c), Flat(ip)) < 0.8f) continue;
                float d = Vector3.Distance(Flat(c), Flat(p));
                if (d < bd) { bd = d; bestPos = c; best = t; }
            }
        }
        if (best == null)
        {
            Vector3 dir = Flat(ip - p);
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            bestPos = ip - dir * seatDistance;
        }
        return new Vector3(bestPos.x, p.y, bestPos.z);
    }

    static bool IsChairName(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("chair") || n.Contains("seat") || n.Contains("stool");
    }

    void RigReact(AnswerKind kind)
    {
        if (rig == null) return;
        if (kind == AnswerKind.Correct) rig.Trigger(RigEvent.Correct);
        else if (kind == AnswerKind.Wrong) rig.Trigger(RigEvent.Wrong);
        else if (kind == AnswerKind.Hedge) rig.Trigger(RigEvent.Hedge);
        else rig.Trigger(RigEvent.NoAnswer);
        Feel(kind == AnswerKind.Correct ? MoodEvent.Correct : (kind == AnswerKind.Wrong ? MoodEvent.Wrong : (kind == AnswerKind.Hedge ? MoodEvent.Hedge : MoodEvent.NoAnswer)));
        CartoonReact(kind);
    }

    IEnumerator SitSequence()
    {
        state = State.Typing;
        HideControls();
        committed.Length = 0;
        tail = "";
        Refresh();
        Vector3 seat = ResolveSeat();
        if (rig != null) rig.GreetAndOffer(seat + Vector3.up * 0.45f);
        yield return Speak(InterviewerPersonality.SeatLine(mood));
        yield return new WaitForSecondsRealtime(0.4f);
        yield return SitDown(seat);
        StartShake();
        yield return new WaitForSecondsRealtime(0.5f);
    }

    static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    IEnumerator SitDown(Vector3 seat)
    {
        if (playerRoot == null) yield break;
        sitCam = playerRoot.GetComponentInChildren<Camera>();
        if (sitCam == null) sitCam = Camera.main;
        if (sitCam == null) yield break;
        standPos = playerRoot.position; standRot = playerRoot.rotation;
        standCamPos = sitCam.transform.localPosition; standCamRot = sitCam.transform.localRotation;

        offCols.Clear();
        foreach (Collider c in playerRoot.GetComponentsInChildren<Collider>())
            if (c.enabled) { c.enabled = false; offCols.Add(c); }
        rbHeld = playerRoot.GetComponentInChildren<Rigidbody>();
        if (rbHeld != null && !rbHeld.isKinematic) rbHeld.isKinematic = true; else rbHeld = null;

        Vector3 ip = InterviewerPos();
        Vector3 look = Flat(ip - seat);
        seatedPos = seat;
        seatedRot = look.sqrMagnitude > 0.001f ? Quaternion.LookRotation(look.normalized, Vector3.up) : standRot;
        seatedCamPos = standCamPos + Vector3.down * seatedCameraDrop;
        float headY = rig != null ? rig.HeadPosition.y : ip.y + 1.5f;
        float camY = sitCam.transform.position.y - seatedCameraDrop;
        float pitch = Mathf.Atan2(headY - camY, Mathf.Max(0.3f, look.magnitude)) * Mathf.Rad2Deg;
        seatedCamRot = Quaternion.Euler(-Mathf.Clamp(pitch, -15f, 25f), 0f, 0f);

        // approach the chair from its SIDE, never straight through the back
        Vector3 fwd = look.sqrMagnitude > 0.001f ? look.normalized : Flat(standRot * Vector3.forward).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        Vector3 rel = Flat(standPos - seat);
        float lat = Vector3.Dot(rel, right);
        float side = Mathf.Abs(lat) > 0.15f ? Mathf.Sign(lat) : (Random.value < 0.5f ? -1f : 1f);
        Vector3 lane = right * side * seatSideOffset;
        float depth = Mathf.Clamp(Vector3.Dot(rel, fwd), -1.3f, 0.5f);
        sitPath[0] = standPos;
        sitPath[1] = new Vector3(seat.x, standPos.y, seat.z) + lane + fwd * depth;
        sitPath[2] = new Vector3(seat.x, standPos.y, seat.z) + lane;
        sitPath[3] = seatedPos;
        float total = 0f;
        for (int i = 0; i < 3; i++) total += Flat(sitPath[i + 1] - sitPath[i]).magnitude;
        float dur = Mathf.Clamp(total / 1.5f, sitDuration, 3.5f);
        sitDur = dur;

        float t = 0f;
        while (t < 1f)
        {
            t += PauseMenu.Dt / Mathf.Max(0.2f, dur);
            ApplySeatBlend(Smooth(t));
            yield return null;
        }
        ApplySeatBlend(1f);
    }

    [SerializeField] float seatSideOffset = 0.9f;
    readonly Vector3[] sitPath = new Vector3[4];
    float sitDur = 1.3f;

    void ApplySeatBlend(float e)
    {
        float[] len = new float[3];
        float total = 0f;
        for (int i = 0; i < 3; i++) { len[i] = Flat(sitPath[i + 1] - sitPath[i]).magnitude; total += len[i]; }
        float d = e * total; int s = 0;
        while (s < 2 && d > len[s]) { d -= len[s]; s++; }
        float u = len[s] > 0.001f ? Mathf.Clamp01(d / len[s]) : 1f;
        playerRoot.position = Vector3.Lerp(sitPath[s], sitPath[s + 1], u);

        Vector3 h0 = Flat(sitPath[1] - sitPath[0]);
        Vector3 h1 = Flat(sitPath[2] - sitPath[1]);
        Quaternion r0 = h0.magnitude > 0.05f ? Quaternion.LookRotation(h0.normalized, Vector3.up) : standRot;
        Quaternion r1 = h1.magnitude > 0.05f ? Quaternion.LookRotation(h1.normalized, Vector3.up) : r0;
        Quaternion rot;
        if (s == 0) rot = Quaternion.Slerp(standRot, r0, Mathf.Clamp01(u * 3f));
        else if (s == 1) rot = Quaternion.Slerp(r0, r1, Mathf.Clamp01(u * 3f));
        else rot = Quaternion.Slerp(r1, seatedRot, Smooth(u));
        playerRoot.rotation = rot;

        float cb = s == 2 ? Smooth(u) : 0f;
        Vector3 cp = Vector3.Lerp(standCamPos, seatedCamPos, cb);
        cp.y -= Mathf.Sin(cb * Mathf.PI) * 0.06f;
        cp.y += Mathf.Sin(d * 7f) * 0.012f * (1f - cb);
        sitCam.transform.localPosition = cp;
        sitCam.transform.localRotation = Quaternion.Slerp(standCamRot, seatedCamRot, cb);
    }

    IEnumerator StandUp()
    {
        if (playerRoot != null && sitCam != null && offCols.Count > 0)
        {
            float t = 1f;
            while (t > 0f)
            {
                t -= PauseMenu.Dt / Mathf.Max(0.2f, sitDur * 0.8f);
                ApplySeatBlend(Smooth(Mathf.Max(0f, t)));
                yield return null;
            }
            playerRoot.position = standPos; playerRoot.rotation = standRot;
            sitCam.transform.localPosition = standCamPos; sitCam.transform.localRotation = standCamRot;
        }
        for (int i = 0; i < offCols.Count; i++) if (offCols[i] != null) offCols[i].enabled = true;
        offCols.Clear();
        if (rbHeld != null) { rbHeld.isKinematic = false; rbHeld = null; }
    }

    void FreezePlayer()
    {
        previousLock = Cursor.lockState;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        frozen.Clear();
        if (playerRoot == null) return;

        Rigidbody rb = playerRoot.GetComponentInChildren<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        MonoBehaviour[] all = playerRoot.GetComponentsInChildren<MonoBehaviour>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].enabled)
            {
                all[i].enabled = false;
                frozen.Add(all[i]);
            }
        }
    }

    void UnfreezePlayer()
    {
        for (int i = 0; i < frozen.Count; i++)
        {
            if (frozen[i] != null) frozen[i].enabled = true;
        }
        frozen.Clear();

        Cursor.lockState = previousLock;
        Cursor.visible = previousLock == CursorLockMode.None;
    }

    void EnsureEventSystem()
    {
        EventSystem es = FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject go = new GameObject("EventSystem", typeof(EventSystem));
            InputSystemUIInputModule created = go.AddComponent<InputSystemUIInputModule>();
            created.AssignDefaultActions();
            return;
        }
        if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            StandaloneInputModule old = es.GetComponent<StandaloneInputModule>();
            if (old != null) WorldUtil.Kill(old);
            InputSystemUIInputModule added = es.gameObject.AddComponent<InputSystemUIInputModule>();
            added.AssignDefaultActions();
        }
    }

    // =====================================================================
    // UI construction (minimal HUD, no full-screen panel, no borders)
    // =====================================================================
    float S(float v)
    {
        return v * uiScale;
    }

    static void PlaceTop(RectTransform rt, float xMin, float xMax, float top, float height)
    {
        rt.anchorMin = new Vector2(xMin, 1f);
        rt.anchorMax = new Vector2(xMax, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.sizeDelta = new Vector2(0f, height);
    }

    static void PlaceBottom(RectTransform rt, float xMin, float xMax, float bottom, float height)
    {
        rt.anchorMin = new Vector2(xMin, 0f);
        rt.anchorMax = new Vector2(xMax, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, bottom);
        rt.sizeDelta = new Vector2(0f, height);
    }

    /// <summary>Row anchored to the top edge of its parent, stretched horizontally with padding.</summary>
    RectTransform TopRow(RectTransform parent, string name, float top, float height, float padX)
    {
        RectTransform rt = UIKit.NewRect(name, parent);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(padX, -(top + height));
        rt.offsetMax = new Vector2(-padX, -top);
        return rt;
    }

    /// <summary>
    /// Positions the HUD for the current screen. Landscape: suspicion top-left, timer top-right, log bottom-center (half width).
    /// Portrait: everything uses nearly the full width and the HUD rows stack. Also follows the device safe area.
    /// </summary>
    void ApplyLayout(bool force)
    {
        if (Screen.width <= 0 || Screen.height <= 0 || safeRt == null) return;

        Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
        Rect safe = Screen.safeArea;
        if (!force && screen == lastScreen && safe == lastSafe) return;
        lastScreen = screen;
        lastSafe = safe;

        bool portrait = screen.y > screen.x;
        canvasScaler.referenceResolution = portrait ? new Vector2(1080f, 1920f) : new Vector2(1920f, 1080f);

        safeRt.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
        safeRt.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
        safeRt.offsetMin = Vector2.zero;
        safeRt.offsetMax = Vector2.zero;

        float margin = S(12f);
        float bottom = S(24f);
        float bottomH = S(LogHeight + BlockGap + ControlsHeight + ExtraVoiceH());
        float resultH = S(ResultBackHeight + BlockGap + ControlsHeight);

        if (portrait)
        {
            PlaceTop(suspBlock, 0.02f, 0.98f, margin, S(HudRowHeight));
            PlaceTop(timerBlock, 0.02f, 0.98f, margin + S(HudRowHeight) + S(6f), S(TimerRowHeight));
            PlaceTop(moodBlock, 0.02f, 0.98f, margin + S(HudRowHeight) + S(TimerRowHeight) + S(12f), S(MoodRowHeight));
            PlaceBottom(bottomBlock, 0.02f, 0.98f, bottom, bottomH);
            PlaceBottom(resultBlock, 0.02f, 0.98f, bottom, resultH);
        }
        else
        {
            PlaceTop(suspBlock, 0.015f, 0.36f, margin, S(HudRowHeight));
            PlaceTop(timerBlock, 0.64f, 0.985f, margin, S(TimerRowHeight));
            PlaceTop(moodBlock, 0.64f, 0.985f, margin + S(TimerRowHeight) + S(6f), S(MoodRowHeight));
            PlaceBottom(bottomBlock, 0.25f, 0.75f, bottom, bottomH);
            PlaceBottom(resultBlock, 0.27f, 0.73f, bottom, resultH);
        }
    }

    void BuildUI()
    {
        cNormal = UIKit.Hex(HexNormal);
        cDim = UIKit.Hex(HexDim);
        cBright = UIKit.Hex(HexBright);
        cBg = UIKit.Hex(HexBg);
        cDark = UIKit.Hex(HexDark);
        stripColor = UIKit.Paper;   // cream paper strip
        flashColor = cNormal;

        canvasGo = new GameObject("InterviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvasScaler = canvasGo.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        safeRt = UIKit.NewRect("SafeArea", canvasGo.transform);
        UIKit.Stretch(safeRt);

        float pad = S(10f);

        // ---- world prompt ----
        RectTransform prompt = UIKit.NewRect("Prompt", safeRt);
        UIKit.Place(prompt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, S(90f)), new Vector2(S(520f), S(44f)));
        UIKit.Boxy(UIKit.AddImage(prompt, null, UIKit.Hex("#FFB347")));
        prompt.localRotation = Quaternion.Euler(0f, 0f, -1f);
        promptRoot = prompt.gameObject;
        TextMeshProUGUI promptText = UIKit.AddText(prompt, "Text", "[ PRESS E TO START INTERVIEW ]", S(25), cNormal,
            TextAlignmentOptions.Center, font, FontStyles.Bold, 2f);
        UIKit.Stretch(promptText.rectTransform);

        // ---- interview root (also the jitter target) ----
        RectTransform ir = UIKit.NewRect("Interview", safeRt);
        UIKit.Stretch(ir);
        voiceReady = VoiceInput.IsSupported();
        interviewRoot = ir.gameObject;
        fxRt = ir;

        // ---- HUD: suspicion (label + bar + value) ----
        suspBlock = UIKit.NewRect("Suspicion", ir);
        UIKit.Boxy(UIKit.AddImage(suspBlock, null, UIKit.Hex("#FFE27A")));
        suspBlock.localRotation = Quaternion.Euler(0f, 0f, 0.6f);

        RectTransform sRow = TopRow(suspBlock, "Row", S(4f), S(34f), pad);
        suspicionLabel = UIKit.AddText(sRow, "Label", "SUSPICION", S(21), cNormal, TextAlignmentOptions.MidlineLeft, font, FontStyles.Bold, 2f);
        UIKit.Place(suspicionLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(S(158f), S(34f)));
        suspicionValue = UIKit.AddText(sRow, "Value", "000/100", S(21), cNormal, TextAlignmentOptions.MidlineRight, font, FontStyles.Bold);
        UIKit.Place(suspicionValue.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(S(108f), S(34f)));

        RectTransform bar = UIKit.NewRect("Bar", sRow);
        bar.anchorMin = new Vector2(0f, 0.5f);
        bar.anchorMax = new Vector2(1f, 0.5f);
        bar.offsetMin = new Vector2(S(166f), -S(5f));
        bar.offsetMax = new Vector2(-S(116f), S(5f));
        segments = BuildSegments(bar, SegmentCount);

        // ---- HUD: question counter + cyberpunk countdown ----
        timerBlock = UIKit.NewRect("Timer", ir);
        UIKit.Boxy(UIKit.AddImage(timerBlock, null, UIKit.Hex("#9EE0FF")));
        timerBlock.localRotation = Quaternion.Euler(0f, 0f, -0.6f);

        counterText = UIKit.AddText(timerBlock, "Counter", "", S(21), cNormal, TextAlignmentOptions.MidlineLeft, font, FontStyles.Bold);
        UIKit.Place(counterText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(pad, 0f), new Vector2(S(150f), S(34f)));

        RectTransform tg = UIKit.NewRect("TimerGroup", timerBlock);
        UIKit.Stretch(tg, S(10f + 150f), pad, 0f, 0f);
        timerGroup = tg.gameObject;

        timerLabel = UIKit.AddText(tg, "Label", "T-MINUS", S(21), cDim, TextAlignmentOptions.MidlineLeft, font, FontStyles.Bold, 2f);
        UIKit.Place(timerLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(S(160f), S(34f)));
        timerValue = UIKit.AddText(tg, "Value", "", S(25), cNormal, TextAlignmentOptions.MidlineRight, font, FontStyles.Bold);
        UIKit.Place(timerValue.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(S(150f), S(36f)));

        RectTransform tBar = UIKit.NewRect("Bar", tg);
        tBar.anchorMin = new Vector2(0f, 0.5f);
        tBar.anchorMax = new Vector2(1f, 0.5f);
        tBar.offsetMin = new Vector2(S(168f), -S(5f));
        tBar.offsetMax = new Vector2(-S(158f), S(5f));
        timerSegments = BuildSegments(tBar, TimerSegmentCount);

        moodBlock = UIKit.NewRect("Mood", ir);
        UIKit.Boxy(UIKit.AddImage(moodBlock, null, UIKit.Hex("#FFB3D1")));
        moodBlock.localRotation = Quaternion.Euler(0f, 0f, 0.5f);
        moodText = UIKit.AddText(moodBlock, "Text", "", S(19), cDim, TextAlignmentOptions.MidlineLeft, font, FontStyles.Bold, 1f);
        UIKit.Stretch(moodText.rectTransform, pad, pad, 0f, 0f);
        moodText.enableAutoSizing = true; moodText.fontSizeMin = S(12f); moodText.fontSizeMax = S(19f);
        moodText.overflowMode = TextOverflowModes.Ellipsis; moodText.enableWordWrapping = false;

        // ---- bottom block: subtitle-style log on a thin black strip + controls below ----
        bottomBlock = UIKit.NewRect("Bottom", ir);

        RectTransform logRt = UIKit.NewRect("LogBack", bottomBlock);
        // sits right above the controls; its height follows the text (see Refresh)
        logRt.anchorMin = new Vector2(0f, 0f);
        logRt.anchorMax = new Vector2(1f, 0f);
        logRt.pivot = new Vector2(0.5f, 0f);
        logRt.anchoredPosition = new Vector2(0f, S(ControlsHeight + BlockGap + ExtraVoiceH()));
        logRt.sizeDelta = new Vector2(0f, S(LogHeight));
        logPanel = logRt;
        logBack = UIKit.Boxy(UIKit.AddImage(logRt, null, stripColor));
        logRt.localRotation = Quaternion.Euler(0f, 0f, -0.4f);
        UIKit.Tape(logRt, 0f, 3f);
        AddScanlines(logRt);

        RectTransform logArea = UIKit.NewRect("LogArea", logRt);
        UIKit.Stretch(logArea, S(20f), S(20f), S(8f), S(8f));
        logArea.gameObject.AddComponent<RectMask2D>();
        logText = UIKit.AddText(logArea, "Log", "", S(25), cNormal, TextAlignmentOptions.BottomLeft, font);
        UIKit.Stretch(logText.rectTransform);

        RectTransform controls = UIKit.NewRect("Controls", bottomBlock);
        controls.anchorMin = new Vector2(0f, 0f);
        controls.anchorMax = new Vector2(1f, 0f);
        controls.pivot = new Vector2(0.5f, 0f);
        controls.anchoredPosition = Vector2.zero;
        controls.sizeDelta = new Vector2(0f, S(ControlsHeight));

        // yes / no / maybe (flat black glitch buttons, left aligned under the log)
        RectTransform yn = UIKit.NewRect("YesNoRow", controls);
        UIKit.Stretch(yn);
        yesNoRow = yn.gameObject;
        Vector2 btnSize = new Vector2(S(230f), S(ControlsHeight));
        yesBtn = MakeButton(yn, "YesBtn", "[Y] YES", new Vector2(0f, 0f), btnSize, delegate { AnswerYesNo("yes"); });
        noBtn = MakeButton(yn, "NoBtn", "[N] NO", new Vector2(S(244f), 0f), btnSize, delegate { AnswerYesNo("no"); });
        maybeBtn = MakeButton(yn, "MaybeBtn", "[M] MAYBE", new Vector2(S(488f), 0f), btnSize, delegate { AnswerYesNo("maybe"); });

        // typed answer: ">" prompt + borderless input with a block caret
        RectTransform sr = UIKit.NewRect("ShortRow", controls);
        UIKit.Stretch(sr);
        UIKit.Boxy(UIKit.AddImage(sr, null, UIKit.Cream));
        shortRow = sr.gameObject;
        BuildInput(sr);
        BuildVoiceRow(controls);

        // ---- result block: same spot as the log, small ----
        resultBlock = UIKit.NewRect("Result", ir);
        resultRoot = resultBlock.gameObject;

        RectTransform rb = UIKit.NewRect("ResultBack", resultBlock);
        rb.anchorMin = new Vector2(0f, 1f);
        rb.anchorMax = new Vector2(1f, 1f);
        rb.pivot = new Vector2(0.5f, 1f);
        rb.anchoredPosition = Vector2.zero;
        rb.sizeDelta = new Vector2(0f, S(ResultBackHeight));
        resultBack = UIKit.Boxy(UIKit.AddImage(rb, null, UIKit.Paper));
        rb.localRotation = Quaternion.Euler(0f, 0f, 0.5f);
        UIKit.Tape(rb, 0f, -3f);
        resultBack.raycastTarget = true;

        resultTitle = UIKit.AddText(rb, "Title", "", S(40), cNormal, TextAlignmentOptions.Center, font, FontStyles.Bold, 4f);
        resultTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        resultTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        resultTitle.rectTransform.offsetMin = new Vector2(S(10f), -S(66f));
        resultTitle.rectTransform.offsetMax = new Vector2(-S(10f), -S(8f));

        resultDetail = UIKit.AddText(rb, "Detail", "", S(30), cNormal, TextAlignmentOptions.TopLeft, font);
        resultDetail.enableAutoSizing = true; resultDetail.fontSizeMax = S(30f); resultDetail.fontSizeMin = S(18f);   // big and readable, shrinks only if it must
        resultDetail.rectTransform.anchorMin = new Vector2(0f, 1f);
        resultDetail.rectTransform.anchorMax = new Vector2(1f, 1f);
        resultDetail.rectTransform.offsetMin = new Vector2(S(20f), -S(ResultBackHeight - 10f));
        resultDetail.rectTransform.offsetMax = new Vector2(-S(20f), -S(70f));

        continueBtn = MakeButton(resultBlock, "ContinueBtn", "[ CONTINUE ]", Vector2.zero, new Vector2(S(290f), S(ControlsHeight)), delegate { if (state == State.Result) Close(); });
        UIKit.Place(continueBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(S(290f), S(ControlsHeight)));

        resultRoot.SetActive(false);
        BuildGradeBadge(ir);
        ApplyLayout(true);
    }

    Image[] BuildSegments(RectTransform bar, int count)
    {
        Image[] result = new Image[count];
        for (int i = 0; i < count; i++)
        {
            RectTransform seg = UIKit.NewRect("Seg" + i, bar);
            seg.anchorMin = new Vector2(i / (float)count, 0f);
            seg.anchorMax = new Vector2((i + 1) / (float)count, 1f);
            seg.offsetMin = new Vector2(1.5f, 0f);
            seg.offsetMax = new Vector2(-1.5f, 0f);
            result[i] = UIKit.Boxy(UIKit.AddImage(seg, null, cDark), 1.5f, false);
        }
        return result;
    }

    void AddScanlines(RectTransform parent)
    {
        return;   // no CRT scanlines in the paper theme
        RectTransform sl = UIKit.NewRect("Scanlines", parent);
        UIKit.Stretch(sl);
        Image img = UIKit.AddImage(sl, UIKit.Scanlines(), UIKit.A(Color.white, 0.45f), false); // subtle, so it does not blur the text
        img.type = Image.Type.Tiled;
    }

    /// <summary>Flat button: solid black, green text, no border, no rounded corners.</summary>
    GlitchButton MakeButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size, System.Action onClick)
    {
        RectTransform rt = UIKit.NewRect(name, parent);
        UIKit.Place(rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), pos, size);
        Color bc = name == "YesBtn" ? UIKit.Hex("#7EE87A") : name == "NoBtn" ? UIKit.Hex("#FF8A80") : name == "MaybeBtn" ? UIKit.Hex("#FFE066") : UIKit.Cream;
        rt.localRotation = Quaternion.Euler(0f, 0f, name == "YesBtn" ? -1.5f : name == "NoBtn" ? 1.2f : name == "MaybeBtn" ? -1f : 0f);
        Image fill = UIKit.Boxy(UIKit.AddImage(rt, null, bc, true), 3f);

        TextMeshProUGUI t = UIKit.AddText(rt, "Label", label, S(27), cNormal, TextAlignmentOptions.Center, font, FontStyles.Bold, 2f);
        UIKit.Stretch(t.rectTransform);

        GlitchButton gb = rt.gameObject.AddComponent<GlitchButton>();
        gb.Setup(t, fill, label, cNormal, cBright, bc, onClick);
        return gb;
    }

    void BuildInput(RectTransform parent)
    {
        TextMeshProUGUI promptChar = UIKit.AddText(parent, "Prompt", ">", S(31), cBright, TextAlignmentOptions.MidlineLeft, font, FontStyles.Bold);
        UIKit.Place(promptChar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(S(14f), 0f), new Vector2(S(28f), S(40f)));

        RectTransform inputRt = UIKit.NewRect("Input", parent);
        UIKit.Stretch(inputRt, S(46f), S(14f), S(6f), S(6f));
        UIKit.AddImage(inputRt, null, new Color(0f, 0f, 0f, 0f), true); // invisible hit area, no box

        RectTransform area = UIKit.NewRect("TextArea", inputRt);
        UIKit.Stretch(area);
        area.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI txt = UIKit.AddText(area, "Text", "", S(27), cNormal, TextAlignmentOptions.MidlineLeft, font);
        UIKit.Stretch(txt.rectTransform);
        TextMeshProUGUI ph = UIKit.AddText(area, "Placeholder", "type answer, ENTER to send_", S(25), cDim, TextAlignmentOptions.MidlineLeft, font);
        UIKit.Stretch(ph.rectTransform);

        // add the component last, with the object inactive, so its references are valid when it wakes
        inputRt.gameObject.SetActive(false);
        input = inputRt.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = txt;
        input.placeholder = ph;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 60;
        input.richText = false;
        input.onFocusSelectAll = false;
        input.customCaretColor = true;
        input.caretColor = cNormal;
        input.caretWidth = 10;                              // chunky block-style caret
        input.selectionColor = UIKit.A(cNormal, 0.35f);
        input.onValidateInput = ValidateChar;
        input.onSubmit.AddListener(OnShortSubmit);
        inputRt.gameObject.SetActive(true);
    }

    // same character rules as the resume terminal: letters, digits, - ' . % and single spaces
    char ValidateChar(string text, int charIndex, char c)
    {
        if (c == ' ')
        {
            if (charIndex == 0) return '\0';
            if (text[charIndex - 1] == ' ') return '\0';
            return c;
        }
        return char.IsLetterOrDigit(c) || c == '-' || c == '\'' || c == '.' || c == '%' ? c : '\0';
    }
}