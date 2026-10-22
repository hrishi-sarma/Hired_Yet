using System;
using System.Collections;
using System.Collections.Generic;
using EasyPeasyFirstPersonController;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Controls the in-interview 2D Cognitive Ability Assessment.
/// Styled as a nostalgic Green Chalkboard with a Wooden Frame,
/// chalk-written questions in the Caveat font, casual fun gameplay,
/// and interactive mini-games including Maze Runner and Flow Dot Connection.
/// </summary>
public partial class CognitiveAssessmentController : MonoBehaviour
{
    public static CognitiveAssessmentController Instance { get; private set; }

    [Header("UI Styling")]
    [SerializeField] TMP_FontAsset chalkFont;
    [SerializeField] float defaultLevelTime = 62f;
    [SerializeField] int levelsToPassGame = 2;   // a minigame counts as passed when this many of its 3 levels are cleared
    [HideInInspector] public bool manageWorldControls = true;   // false when the interview already freezes the player
    float levelTimeLimit = 62f;

    // Green Chalkboard & Chalk Palette
    static readonly Color BoardGreen = new Color(0.12f, 0.25f, 0.17f, 0.98f);    // #1F3F2B Matte chalkboard green
    static readonly Color BoardInner = new Color(0.09f, 0.20f, 0.13f, 1f);        // #173321
    static readonly Color WoodFrame = new Color(0.52f, 0.31f, 0.16f, 1f);         // #854F29 Warm wooden frame
    static readonly Color WoodHighlight = new Color(0.72f, 0.45f, 0.24f, 1f);     // #B8733D Beveled wood
    static readonly Color ChalkWhite = new Color(0.98f, 0.97f, 0.93f, 1f);        // #FAF7ED
    static readonly Color ChalkYellow = new Color(1.0f, 0.92f, 0.38f, 1f);        // #FFEA61
    static readonly Color ChalkCyan = new Color(0.55f, 0.88f, 0.96f, 1f);          // #8CE0F5
    static readonly Color ChalkPink = new Color(1.0f, 0.58f, 0.65f, 1f);          // #FF94A6
    static readonly Color ChalkGreen = new Color(0.65f, 0.92f, 0.60f, 1f);         // #A6EBA0
    static readonly Color ChalkOrange = new Color(1.0f, 0.72f, 0.35f, 1f);        // #FFB859

    const string HexWhite = "#FAF7ED";
    const string HexYellow = "#FFEA61";
    const string HexCyan = "#8CE0F5";
    const string HexGreen = "#A6EBA0";
    const string HexPink = "#FF94A6";

    // UI Hierarchy
    GameObject overlayRoot;
    RectTransform chalkboardRt;
    TextMeshProUGUI headerTitle;
    TextMeshProUGUI roundBadge;
    TextMeshProUGUI levelBadge;
    TextMeshProUGUI promptLabel;
    TextMeshProUGUI feedbackText;
    RectTransform gameViewport;

    // Session Execution
    bool isRunning = false;
    List<CognitiveGameType> selectedGames = new List<CognitiveGameType>();
    int currentRoundIndex = 0;   // 0..2
    int currentLevelIndex = 0;   // 0..2
    float levelTimeRemaining;
    bool levelResolved = false;

    Action<CognitiveRoundResult> onCompletedCallback;
    CognitiveRoundResult runResult;
    FirstPersonController pausedPlayer;
    CursorLockMode previousCursorLock;
    bool previousCursorVisible;

    // read by the interview HUD, which shows the single shared timer
    public bool IsRunning { get { return isRunning; } }
    public float LevelTimeRemaining { get { return Mathf.Max(0f, levelTimeRemaining); } }
    public float LevelTimeLimit { get { return levelTimeLimit; } }
    public int RoundNumber { get { return currentRoundIndex + 1; } }

    void Awake()
    {
        Instance = this;
        EnsureChalkFont();
    }

    static bool FontAssetUsable(TMP_FontAsset fa)
    {
        if (fa == null) return false;
        try
        {
            Texture2D[] atlases = fa.atlasTextures;
            return atlases != null && atlases.Length > 0 && atlases[0] != null;
        }
        catch
        {
            return false;
        }
    }

    void EnsureChalkFont()
    {
        if (FontAssetUsable(chalkFont)) return;
        chalkFont = null;

        TMP_FontAsset loaded = Resources.Load<TMP_FontAsset>("Fonts/Caveat-Bold SDF");
        if (FontAssetUsable(loaded))
        {
            chalkFont = loaded;
            return;
        }

        Font ttf = Resources.Load<Font>("Fonts/Caveat-Bold");
#if UNITY_EDITOR
        if (ttf == null)
        {
            ttf = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Caveat/static/Caveat-Bold.ttf");
            if (ttf == null) ttf = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Caveat/Caveat-VariableFont_wght.ttf");
        }
#endif
        if (ttf != null)
        {
            try
            {
                chalkFont = TMP_FontAsset.CreateFontAsset(
                    ttf, 90, 8,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                    1024, 1024,
                    AtlasPopulationMode.Dynamic);
            }
            catch (Exception e)
            {
                Debug.LogWarning("CognitiveAssessmentController: could not build Caveat font at runtime. " + e.Message);
                chalkFont = null;
            }
        }

        if (!FontAssetUsable(chalkFont))
            chalkFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    public void Setup(TMP_FontAsset interviewFont, Transform parent, int siblingIndex = -1)
    {
        // The controller can survive a menu/world transition on some scene setups. Its old overlay
        // may then reference a destroyed SafeArea RectTransform from the previous InterviewCanvas.
        // Rebuild the overlay whenever its parent is no longer the requested live parent.
        if (parent == null) return;
        if (overlayRoot != null)
        {
            Transform oldParent = overlayRoot.transform.parent;
            if (oldParent == null || oldParent != parent)
            {
                GameObject oldOverlay = overlayRoot;
                overlayRoot = null;
                chalkboardRt = null;
                headerTitle = null;
                roundBadge = null;
                levelBadge = null;
                promptLabel = null;
                feedbackText = null;
                gameViewport = null;
                if (oldOverlay != null) WorldUtil.Kill(oldOverlay);
            }
        }

        try
        {
            if (FontAssetUsable(interviewFont)) chalkFont = interviewFont;   // same font as the interview UI, everywhere
            else EnsureChalkFont();
            BuildChalkboardOverlay(parent, siblingIndex);
        }
        catch (Exception e)
        {
            Debug.LogWarning("CognitiveAssessmentController: overlay setup recovered from an error. " + e.Message);
        }

        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (overlayRoot != null) WorldUtil.Kill(overlayRoot);
        overlayRoot = null;
        chalkboardRt = null;
        headerTitle = null;
        roundBadge = null;
        levelBadge = null;
        promptLabel = null;
        feedbackText = null;
        gameViewport = null;
    }

    // Interview palette (same colours as InterviewUI): cream paper, ink blue, boxy dark outlines, taped strip.
    static readonly Color Ink = new Color(0.102f, 0.184f, 0.627f, 1f);        // #1A2FA0
    static readonly Color InkDim = new Color(0.290f, 0.333f, 0.600f, 1f);     // #4A5599
    static readonly Color PillYellow = new Color(1f, 0.886f, 0.478f, 1f);     // #FFE27A
    static readonly Color PillBlue = new Color(0.620f, 0.878f, 1f, 1f);       // #9EE0FF

    void BuildChalkboardOverlay(Transform parentCanvas, int siblingIndex)
    {
        if (overlayRoot != null) return;

        EnsureChalkFont();

        overlayRoot = new GameObject("CognitiveAssessmentOverlay", typeof(RectTransform));
        overlayRoot.transform.SetParent(parentCanvas, false);
        if (siblingIndex >= 0) overlayRoot.transform.SetSiblingIndex(siblingIndex);   // sits UNDER the interview HUD
        overlayRoot.SetActive(false);
        RectTransform rt = overlayRoot.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // light dim over the 3D room (blocks clicks to the world, HUD stays above it)
        Image dimBack = overlayRoot.AddComponent<Image>();
        dimBack.color = new Color(0f, 0f, 0f, 0.38f);
        dimBack.raycastTarget = true;

        // ---- paper container (same look as the interview strips: cream paper, dark outline, hard shadow, tape) ----
        GameObject boardObj = new GameObject("PaperBoard", typeof(RectTransform), typeof(Image));
        boardObj.transform.SetParent(overlayRoot.transform, false);
        chalkboardRt = boardObj.GetComponent<RectTransform>();
        chalkboardRt.anchorMin = chalkboardRt.anchorMax = new Vector2(0.5f, 0.5f);
        chalkboardRt.pivot = new Vector2(0.5f, 0.5f);
        chalkboardRt.anchoredPosition = new Vector2(0f, -14f);
        chalkboardRt.sizeDelta = new Vector2(848f, 588f);
        Image paper = boardObj.GetComponent<Image>();
        paper.color = UIKit.Paper;
        paper.raycastTarget = true;
        UIKit.Boxy(paper);
        UIKit.Tape(chalkboardRt, 0f, 1.5f);

        // header: title | round pill | level pill
        GameObject header = new GameObject("Header", typeof(RectTransform));
        header.transform.SetParent(boardObj.transform, false);
        RectTransform hRt = header.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 1f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = new Vector2(0f, -22f);
        hRt.sizeDelta = new Vector2(0f, 34f);

        headerTitle = CreateChromeText(header.transform, "COGNITIVE TEST", 15f, 24f, TextAlignmentOptions.Left, Ink, FontStyles.Bold, 2f);
        SetRect(headerTitle.rectTransform, 0f, 0f, 0.36f, 1f, 22f, 0f, 0f, 0f);

        roundBadge = MakePill(header.transform, "RoundPill", PillYellow, 0.53f, 0.79f, 0f, -8f);
        levelBadge = MakePill(header.transform, "LevelPill", PillBlue, 0.80f, 1f, 0f, -22f);
        roundBadge.text = "TEST 1/3";
        levelBadge.text = "LEVEL 1/3";

        // instruction line (ink on paper, shrinks instead of overflowing)
        GameObject promptBar = new GameObject("PromptBar", typeof(RectTransform));
        promptBar.transform.SetParent(boardObj.transform, false);
        RectTransform pRt = promptBar.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0f, 1f);
        pRt.anchorMax = new Vector2(1f, 1f);
        pRt.pivot = new Vector2(0.5f, 1f);
        pRt.anchoredPosition = new Vector2(0f, -62f);
        pRt.sizeDelta = new Vector2(0f, 50f);

        promptLabel = CreateChromeText(promptBar.transform, "", 14f, 22f, TextAlignmentOptions.Center, Ink, FontStyles.Bold, 0f);
        promptLabel.enableWordWrapping = true;
        SetRect(promptLabel.rectTransform, 0f, 0f, 1f, 1f, 24f, 0f, -24f, 0f);

        // play area: dark chalk surface with a boxy outline (games are laid out for this exact rect)
        GameObject vpFrame = new GameObject("ViewportFrame", typeof(RectTransform), typeof(Image));
        vpFrame.transform.SetParent(boardObj.transform, false);
        RectTransform vfRt = vpFrame.GetComponent<RectTransform>();
        vfRt.anchorMin = Vector2.zero;
        vfRt.anchorMax = Vector2.one;
        vfRt.offsetMin = new Vector2(16f, 42f);
        vfRt.offsetMax = new Vector2(-16f, -114f);
        Image vfImg = vpFrame.GetComponent<Image>();
        vfImg.color = UIKit.Dark;
        vfImg.raycastTarget = false;

        GameObject viewport = new GameObject("GameViewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(boardObj.transform, false);
        gameViewport = viewport.GetComponent<RectTransform>();
        gameViewport.anchorMin = new Vector2(0f, 0f);
        gameViewport.anchorMax = new Vector2(1f, 1f);
        gameViewport.offsetMin = new Vector2(20f, 46f);
        gameViewport.offsetMax = new Vector2(-20f, -118f);
        Image vpImg = viewport.GetComponent<Image>();
        vpImg.color = BoardInner;
        viewport.GetComponent<Mask>().showMaskGraphic = true;

        // feedback strip under the play area (dark strip like the interview log)
        GameObject footer = new GameObject("Footer", typeof(RectTransform), typeof(Image));
        footer.transform.SetParent(boardObj.transform, false);
        RectTransform fRt = footer.GetComponent<RectTransform>();
        fRt.anchorMin = new Vector2(0f, 0f);
        fRt.anchorMax = new Vector2(1f, 0f);
        fRt.pivot = new Vector2(0.5f, 0f);
        fRt.anchoredPosition = new Vector2(0f, 8f);
        fRt.offsetMin = new Vector2(16f, 8f);
        fRt.offsetMax = new Vector2(-16f, 36f);
        Image fImg = footer.GetComponent<Image>();
        fImg.color = new Color(0.07f, 0.10f, 0.09f, 1f);
        fImg.raycastTarget = false;
        UIKit.Boxy(fImg, 3f, false);

        feedbackText = CreateChromeText(footer.transform, "", 13f, 20f, TextAlignmentOptions.Center, ChalkGreen, FontStyles.Bold, 0f);
        SetRect(feedbackText.rectTransform, 0f, 0f, 1f, 1f, 12f, 0f, -196f, 0f);

        // SKIP button: gives up the current puzzle (counts as a failed level)
        GameObject skip = new GameObject("SkipBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        skip.transform.SetParent(footer.transform, false);
        SetRect(skip.GetComponent<RectTransform>(), 1f, 0f, 1f, 1f, -184f, 2f, -6f, -2f);
        Image skImg = skip.GetComponent<Image>();
        skImg.color = PillYellow; skImg.raycastTarget = true;
        UIKit.Boxy(skImg, 3f, true);
        skip.GetComponent<Button>().onClick.AddListener(SkipLevel);
        TextMeshProUGUI skTxt = CreateChromeText(skip.transform, "SKIP PUZZLE >>", 12f, 18f, TextAlignmentOptions.Center, Ink, FontStyles.Bold, 1f);
        SetRect(skTxt.rectTransform, 0f, 0f, 1f, 1f, 6f, 0f, -6f, 0f);
    }

    void SkipLevel()
    {
        if (!isRunning || levelResolved) return;
        OnLevelFailed("SKIPPED - counts as failed");
    }

    static void SetRect(RectTransform rt, float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    /// <summary>Chrome-only text: auto-shrinks, never wraps unless asked, never blocks clicks. Uses the interview font.</summary>
    TextMeshProUGUI CreateChromeText(Transform parent, string initial, float minSize, float maxSize, TextAlignmentOptions align, Color col, FontStyles style, float spacing)
    {
        TextMeshProUGUI t = CreateChalkText(parent, initial, maxSize, align, col);
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.enableAutoSizing = true;
        t.fontSizeMin = minSize;
        t.fontSizeMax = maxSize;
        t.fontStyle = style;
        t.characterSpacing = spacing;
        return t;
    }

    /// <summary>Flat boxy pill (same style as the interview HUD blocks) with centered ink text.</summary>
    TextMeshProUGUI MakePill(Transform parent, string name, Color fill, float minX, float maxX, float leftPad, float rightPad)
    {
        GameObject pill = new GameObject(name, typeof(RectTransform), typeof(Image));
        pill.transform.SetParent(parent, false);
        RectTransform rt = pill.GetComponent<RectTransform>();
        SetRect(rt, minX, 0.06f, maxX, 0.94f, leftPad, 0f, rightPad, 0f);
        Image img = pill.GetComponent<Image>();
        img.color = fill;
        img.raycastTarget = false;
        UIKit.Boxy(img, 3f, false);

        TextMeshProUGUI t = CreateChromeText(pill.transform, "", 12f, 20f, TextAlignmentOptions.Center, Ink, FontStyles.Bold, 1f);
        SetRect(t.rectTransform, 0f, 0f, 1f, 1f, 8f, 0f, -8f, 0f);
        return t;
    }

    TextMeshProUGUI CreateChalkText(Transform parent, string initial, float size, TextAlignmentOptions align, Color col)
    {
        GameObject go = new GameObject("ChalkText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (FontAssetUsable(chalkFont)) t.font = chalkFont;
        t.fontSize = size;
        t.alignment = align;
        t.color = col;
        t.text = initial;
        t.enableWordWrapping = true;
        return t;
    }

    // =========================================================================
    // Assessment Session Flow
    // =========================================================================

    public void StartCognitiveAssessment(System.Random rng, Action<CognitiveRoundResult> onComplete)
    {
        onCompletedCallback = onComplete;
        runResult = new CognitiveRoundResult();
        runResult.gamePassed = new bool[3];

        selectedGames = PickGames(rng);   // 3 distinct games from the full pool, none repeated from the last playthrough

        currentRoundIndex = 0;
        currentLevelIndex = 0;

        overlayRoot.SetActive(true);
        isRunning = true;
        PauseWorldControls();

        StartCoroutine(RunAssessmentSequence());
    }

    IEnumerator RunAssessmentSequence()
    {
        for (currentRoundIndex = 0; currentRoundIndex < selectedGames.Count; currentRoundIndex++)
        {
            CognitiveGameType gameType = selectedGames[currentRoundIndex];
            roundBadge.text = $"{currentRoundIndex + 1}/3  {FormatGameTitle(gameType)}";

            int clearedBefore = runResult.levelsCleared;
            for (currentLevelIndex = 0; currentLevelIndex < 3; currentLevelIndex++)
            {
                levelBadge.text = $"LEVEL {currentLevelIndex + 1}/3";
                feedbackText.text = "";
                ClearViewport();

                yield return StartMiniGameLevel(gameType, currentLevelIndex);
                yield return new WaitForSecondsRealtime(0.8f);
            }

            bool gamePassed = runResult.levelsCleared - clearedBefore >= levelsToPassGame;
            runResult.gamesTotal++;
            if (gamePassed) runResult.gamesPassed++;
            if (currentRoundIndex < runResult.gamePassed.Length) runResult.gamePassed[currentRoundIndex] = gamePassed;
            ClearViewport();
            feedbackText.text = gamePassed
                ? "<color=" + HexGreen + ">GAME " + (currentRoundIndex + 1) + " PASSED</color>"
                : "<color=" + HexPink + ">GAME " + (currentRoundIndex + 1) + " FAILED</color>";
            yield return new WaitForSecondsRealtime(1.1f);
        }

        feedbackText.text = "<color=" + HexYellow + ">TEST COMPLETE: " + runResult.gamesPassed + "/" + runResult.gamesTotal + " GAMES PASSED</color>";
        yield return new WaitForSecondsRealtime(1.3f);

        overlayRoot.SetActive(false);
        isRunning = false;
        ResumeWorldControls();

        onCompletedCallback?.Invoke(runResult);
    }

    void PauseWorldControls()
    {
        if (manageWorldControls)
        {
            pausedPlayer = FindFirstObjectByType<FirstPersonController>();
            if (pausedPlayer != null) pausedPlayer.enabled = false;
        }
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void ResumeWorldControls()
    {
        if (pausedPlayer != null) pausedPlayer.enabled = true;
        pausedPlayer = null;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    string FormatGameTitle(CognitiveGameType t)
    {
        switch (t)
        {
            case CognitiveGameType.MazeRunner: return "HEDGE MAZE";
            case CognitiveGameType.FlowLineMatch: return "DOT CONNECTOR";
            case CognitiveGameType.PatternSequence: return "CARTOON PATTERNS";
            case CognitiveGameType.BalanceScales: return "SEESAW BALANCE";
            case CognitiveGameType.OddOneOut: return "ODD ONE OUT";
            case CognitiveGameType.FruitMath: return "FRUIT MATH";
            case CognitiveGameType.MemoryGrid: return "CHALK STARS";
            case CognitiveGameType.CircuitWire: return "GARDEN PIPES";
            case CognitiveGameType.CartoonColorMatch: return "BALLOON POP";
            case CognitiveGameType.DirectionCompass: return "BUNNY HOPS";
            default: return ExtraTitle(t);
        }
    }

    void ClearViewport()
    {
        for (int i = gameViewport.childCount - 1; i >= 0; i--)
        {
            WorldUtil.Kill(gameViewport.GetChild(i).gameObject);
        }
    }

    IEnumerator StartMiniGameLevel(CognitiveGameType type, int level)
    {
        levelResolved = false;
        if (type == CognitiveGameType.MazeRunner) levelTimeLimit = 85f;
        else if (type == CognitiveGameType.FlowLineMatch) levelTimeLimit = 95f;
        else if (type == CognitiveGameType.MemoryGrid) levelTimeLimit = 70f;
        else levelTimeLimit = ExtraTime(type);
        levelTimeLimit *= Difficulty.CognitiveTimeMul;   // experience level = difficulty
        levelTimeRemaining = levelTimeLimit;
        runResult.totalPuzzlesAttempted++;
        levelToken++;

        if (!SetupGenerated(type, level)) switch (type)
        {
            case CognitiveGameType.MazeRunner:
                SetupMazeRunner(level);
                break;
            case CognitiveGameType.FlowLineMatch:
                SetupFlowLineMatch(level);
                break;
            case CognitiveGameType.MemoryGrid:
                SetupMemoryGrid(level);
                break;
        }

        while (!levelResolved)
        {
            levelTimeRemaining -= PauseMenu.Dt;
            float tClamped = Mathf.Max(0f, levelTimeRemaining);

            if (levelTimeRemaining <= 0f)
            {
                OnLevelFailed("Time ran out! Keep going!");
                break;
            }
            yield return null;
        }
    }

    void OnLevelPassed(string note = "GREAT JOB!")
    {
        if (levelResolved) return;
        levelResolved = true;
        runResult.levelsCleared++;
        runResult.netSuspicionDelta -= 1.5f; // Cleared fun level drops suspicion
        feedbackText.text = $"<color={HexGreen}>OK: {note} (-1.5 SUSPICION)</color>";
    }

    void OnLevelFailed(string note = "NICE TRY!")
    {
        if (levelResolved) return;
        levelResolved = true;
        runResult.levelsFailed++;
        runResult.netSuspicionDelta += 2.0f; // Gentle penalty for casual fun
        feedbackText.text = $"<color={HexPink}>X: {note}</color>";
    }

    // =========================================================================
    // 1. Maze Runner Mini-Game (Image 1 - WASD / Arrows Top-Down Hedge Maze)
    // =========================================================================
    void SetupMazeRunner(int level)
    {
        promptLabel.text = "Guide the little hero through the hedge maze to the glowing star!  WASD or Arrow Keys";

        int mazeSize = ((level == 0) ? 9 : (level == 1 ? 13 : 15)) + Difficulty.MazeExtra;
        System.Random mazeRng = new System.Random(Environment.TickCount + level * 917);
        Vector2Int playerStart;
        Vector2Int trophyGoal;
        int[,] maze = GenerateHedgeMaze(mazeSize, mazeSize, mazeRng, out playerStart, out trophyGoal);

        int rows = maze.GetLength(0);
        int cols = maze.GetLength(1);

        float boardSize = Mathf.Min(430f, 28f * cols);
        float tileSize = boardSize / cols;
        float startX = -boardSize * 0.5f + tileSize * 0.5f;
        float startY = boardSize * 0.5f - tileSize * 0.5f;

        GameObject mazeRoot = new GameObject("MazeContainer", typeof(RectTransform), typeof(Image));
        mazeRoot.transform.SetParent(gameViewport, false);
        RectTransform mrt = mazeRoot.GetComponent<RectTransform>();
        mrt.anchoredPosition = Vector2.zero;
        mrt.sizeDelta = new Vector2(boardSize + 12f, boardSize + 12f);
        Image lawn = mazeRoot.GetComponent<Image>();
        lawn.sprite = UIKit.Rounded(10);
        lawn.type = Image.Type.Sliced;
        lawn.color = new Color(0.42f, 0.74f, 0.28f, 1f); // sunny cartoon grass

        Color hedge = new Color(0.20f, 0.46f, 0.16f, 1f);
        Color hedgeTop = new Color(0.32f, 0.62f, 0.22f, 1f);
        Color pathGrass = new Color(0.55f, 0.82f, 0.34f, 1f);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float x = startX + c * tileSize;
                float y = startY - r * tileSize;
                bool wall = maze[r, c] == 1;

                GameObject tile = new GameObject($"Tile_{r}_{c}", typeof(RectTransform), typeof(Image));
                tile.transform.SetParent(mazeRoot.transform, false);
                RectTransform trt = tile.GetComponent<RectTransform>();
                trt.anchoredPosition = new Vector2(x, y);
                trt.sizeDelta = wall
                    ? new Vector2(tileSize + 1.5f, tileSize + 1.5f)
                    : new Vector2(tileSize * 0.92f, tileSize * 0.92f);

                Image img = tile.GetComponent<Image>();
                img.sprite = UIKit.Rounded(wall ? 6 : 3);
                img.type = Image.Type.Sliced;
                img.color = wall ? hedge : pathGrass;

                if (wall)
                {
                    GameObject shine = new GameObject("HedgeShine", typeof(RectTransform), typeof(Image));
                    shine.transform.SetParent(tile.transform, false);
                    RectTransform srt = shine.GetComponent<RectTransform>();
                    srt.anchorMin = new Vector2(0.12f, 0.45f);
                    srt.anchorMax = new Vector2(0.88f, 0.92f);
                    srt.offsetMin = Vector2.zero;
                    srt.offsetMax = Vector2.zero;
                    Image si = shine.GetComponent<Image>();
                    si.sprite = UIKit.Rounded(4);
                    si.type = Image.Type.Sliced;
                    si.color = hedgeTop;
                    si.raycastTarget = false;
                }
            }
        }

        CreateMazeSparkle(mazeRoot.transform, startX + trophyGoal.x * tileSize, startY - trophyGoal.y * tileSize, tileSize, ChalkYellow, "*");

        GameObject playerObj = new GameObject("MazePlayer", typeof(RectTransform), typeof(Image));
        playerObj.transform.SetParent(mazeRoot.transform, false);
        RectTransform prt = playerObj.GetComponent<RectTransform>();
        prt.sizeDelta = new Vector2(tileSize * 0.78f, tileSize * 0.78f);
        prt.anchoredPosition = new Vector2(startX + playerStart.x * tileSize, startY - playerStart.y * tileSize);

        Image pImg = playerObj.GetComponent<Image>();
        pImg.sprite = UIKit.Rounded(16);
        pImg.color = new Color(1f, 0.95f, 0.55f, 1f);

        CreateMazeEye(playerObj.transform, new Vector2(-tileSize * 0.14f, tileSize * 0.08f), tileSize * 0.14f);
        CreateMazeEye(playerObj.transform, new Vector2(tileSize * 0.14f, tileSize * 0.08f), tileSize * 0.14f);

        StartCoroutine(RunMazeMovement(prt, maze, playerStart, trophyGoal, startX, startY, tileSize));
    }

    void CreateMazeEye(Transform parent, Vector2 pos, float size)
    {
        GameObject eye = new GameObject("Eye", typeof(RectTransform), typeof(Image));
        eye.transform.SetParent(parent, false);
        RectTransform rt = eye.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        Image img = eye.GetComponent<Image>();
        img.sprite = UIKit.Rounded(8);
        img.color = new Color(0.18f, 0.12f, 0.06f, 1f);
        img.raycastTarget = false;
    }

    void CreateMazeSparkle(Transform parent, float x, float y, float tileSize, Color col, string glyph)
    {
        GameObject glow = new GameObject("GoalGlow", typeof(RectTransform), typeof(Image));
        glow.transform.SetParent(parent, false);
        RectTransform glrt = glow.GetComponent<RectTransform>();
        glrt.anchoredPosition = new Vector2(x, y);
        glrt.sizeDelta = new Vector2(tileSize * 1.15f, tileSize * 1.15f);
        Image gi = glow.GetComponent<Image>();
        gi.sprite = UIKit.Rounded(16);
        gi.color = new Color(col.r, col.g, col.b, 0.35f);

        GameObject goalObj = new GameObject("TrophyGoal", typeof(RectTransform));
        goalObj.transform.SetParent(parent, false);
        RectTransform grt = goalObj.GetComponent<RectTransform>();
        grt.anchoredPosition = new Vector2(x, y);
        grt.sizeDelta = new Vector2(tileSize, tileSize);

        TextMeshProUGUI goalTxt = CreateChalkText(goalObj.transform, glyph, tileSize * 0.9f, TextAlignmentOptions.Center, col);
        goalTxt.rectTransform.anchorMin = Vector2.zero;
        goalTxt.rectTransform.anchorMax = Vector2.one;
        goalTxt.rectTransform.offsetMin = Vector2.zero;
        goalTxt.rectTransform.offsetMax = Vector2.zero;
    }

    int[,] GenerateHedgeMaze(int rows, int cols, System.Random rng, out Vector2Int start, out Vector2Int goal)
    {
        int[,] maze = new int[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                maze[r, c] = 1;

        start = new Vector2Int(1, 1);
        maze[start.y, start.x] = 0;

        Vector2Int[] step = { new Vector2Int(0, -2), new Vector2Int(0, 2), new Vector2Int(-2, 0), new Vector2Int(2, 0) };
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            Vector2Int cur = stack.Peek();
            List<Vector2Int> options = new List<Vector2Int>(4);
            for (int i = 0; i < step.Length; i++)
            {
                Vector2Int n = cur + step[i];
                if (n.x > 0 && n.x < cols - 1 && n.y > 0 && n.y < rows - 1 && maze[n.y, n.x] == 1)
                    options.Add(n);
            }

            if (options.Count == 0)
            {
                stack.Pop();
                continue;
            }

            Vector2Int next = options[rng.Next(options.Count)];
            Vector2Int wall = cur + new Vector2Int((next.x - cur.x) / 2, (next.y - cur.y) / 2);
            maze[wall.y, wall.x] = 0;
            maze[next.y, next.x] = 0;
            stack.Push(next);
        }

        goal = FindFarthestOpenCell(maze, start);
        return maze;
    }

    Vector2Int FindFarthestOpenCell(int[,] maze, Vector2Int start)
    {
        int rows = maze.GetLength(0);
        int cols = maze.GetLength(1);
        int[,] dist = new int[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                dist[r, c] = -1;

        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(start);
        dist[start.y, start.x] = 0;
        Vector2Int far = start;
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (q.Count > 0)
        {
            Vector2Int cur = q.Dequeue();
            if (dist[cur.y, cur.x] > dist[far.y, far.x]) far = cur;
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector2Int n = cur + dirs[i];
                if (n.x < 0 || n.y < 0 || n.x >= cols || n.y >= rows) continue;
                if (maze[n.y, n.x] != 0 || dist[n.y, n.x] >= 0) continue;
                dist[n.y, n.x] = dist[cur.y, cur.x] + 1;
                q.Enqueue(n);
            }
        }

        return far;
    }

    IEnumerator RunMazeMovement(RectTransform playerRt, int[,] maze, Vector2Int currentPos, Vector2Int goalPos, float startX, float startY, float tileSize)
    {
        int rows = maze.GetLength(0);
        int cols = maze.GetLength(1);
        float moveCooldown = 0f;

        while (!levelResolved)
        {
            moveCooldown -= PauseMenu.Dt;
            Keyboard kb = Keyboard.current;
            Vector2Int dir = Vector2Int.zero;

            if (kb != null && moveCooldown <= 0f)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir = new Vector2Int(0, -1);
                else if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir = new Vector2Int(0, 1);
                else if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir = new Vector2Int(-1, 0);
                else if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir = new Vector2Int(1, 0);

                if (dir != Vector2Int.zero)
                {
                    Vector2Int nextPos = currentPos + dir;
                    if (nextPos.x >= 0 && nextPos.x < cols && nextPos.y >= 0 && nextPos.y < rows && maze[nextPos.y, nextPos.x] == 0)
                    {
                        currentPos = nextPos;
                        playerRt.anchoredPosition = new Vector2(startX + currentPos.x * tileSize, startY - currentPos.y * tileSize);
                        moveCooldown = 0.11f;

                        if (currentPos == goalPos)
                        {
                            OnLevelPassed("MAZE ESCAPED!");
                            yield break;
                        }
                    }
                }
            }
            yield return null;
        }
    }

    // =========================================================================
    // 2. Connect the Dots Mini-Game (Image 2 - Flow / Non-Crossing Colored Lines)
    // =========================================================================
    void SetupFlowLineMatch(int level)
    {
        promptLabel.text = "Drag from a colored dot to its twin. Lines cannot cross!  [Mouse]";

        int gridSize;
        Vector2Int[] starts;
        Vector2Int[] ends;
        Color[] colors = {
            new Color(0.20f, 0.48f, 1.00f),
            new Color(0.92f, 0.18f, 0.22f),
            new Color(0.22f, 0.78f, 0.32f),
            new Color(1.00f, 0.58f, 0.12f),
            new Color(0.58f, 0.28f, 0.92f)
        };

        if (level == 0)
        {
            gridSize = 5;
            starts = new[] { new Vector2Int(0, 0), new Vector2Int(4, 0), new Vector2Int(2, 2) };
            ends   = new[] { new Vector2Int(0, 3), new Vector2Int(4, 4), new Vector2Int(3, 4) };
        }
        else if (level == 1)
        {
            gridSize = 6;
            starts = new[] { new Vector2Int(0, 0), new Vector2Int(2, 1), new Vector2Int(5, 0), new Vector2Int(0, 5) };
            ends   = new[] { new Vector2Int(0, 2), new Vector2Int(2, 3), new Vector2Int(3, 4), new Vector2Int(5, 5) };
        }
        else
        {
            // 6x6 layout inspired by the reference screenshot
            gridSize = 6;
            starts = new[] {
                new Vector2Int(0, 0),
                new Vector2Int(2, 1),
                new Vector2Int(3, 1),
                new Vector2Int(0, 5),
                new Vector2Int(5, 3)
            };
            ends = new[] {
                new Vector2Int(0, 2),
                new Vector2Int(2, 3),
                new Vector2Int(1, 5),
                new Vector2Int(5, 2),
                new Vector2Int(5, 5)
            };
        }

        int pairCount = starts.Length;
        float boardSize = 360f;
        float cellSize = boardSize / gridSize;
        float startX = -boardSize * 0.5f + cellSize * 0.5f;
        float startY = boardSize * 0.5f - cellSize * 0.5f;

        GameObject paper = new GameObject("FlowPaper", typeof(RectTransform), typeof(Image));
        paper.transform.SetParent(gameViewport, false);
        RectTransform paperRt = paper.GetComponent<RectTransform>();
        paperRt.anchoredPosition = Vector2.zero;
        paperRt.sizeDelta = new Vector2(boardSize + 28f, boardSize + 28f);
        Image paperImg = paper.GetComponent<Image>();
        paperImg.sprite = UIKit.Rounded(12);
        paperImg.type = Image.Type.Sliced;
        paperImg.color = new Color(0.97f, 0.95f, 0.88f, 1f);

        GameObject gridRoot = new GameObject("FlowGridContainer", typeof(RectTransform));
        gridRoot.transform.SetParent(paper.transform, false);
        RectTransform grt = gridRoot.GetComponent<RectTransform>();
        grt.anchorMin = new Vector2(0.5f, 0.5f);
        grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.anchoredPosition = Vector2.zero;
        grt.sizeDelta = new Vector2(boardSize, boardSize);

        int[,] occupancy = new int[gridSize, gridSize];
        int[,] endpointOf = new int[gridSize, gridSize];
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                occupancy[r, c] = -1;
                endpointOf[r, c] = -1;

                GameObject cell = new GameObject($"Cell_{r}_{c}", typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(gridRoot.transform, false);
                RectTransform crt = cell.GetComponent<RectTransform>();
                crt.anchoredPosition = CellToLocal(c, r, startX, startY, cellSize);
                crt.sizeDelta = new Vector2(cellSize - 3f, cellSize - 3f);
                Image ci = cell.GetComponent<Image>();
                ci.sprite = UIKit.Rounded(4);
                ci.color = new Color(0.93f, 0.91f, 0.82f, 1f);
                ci.raycastTarget = false;
            }
        }

        for (int p = 0; p < pairCount; p++)
        {
            occupancy[starts[p].y, starts[p].x] = p;
            occupancy[ends[p].y, ends[p].x] = p;
            endpointOf[starts[p].y, starts[p].x] = p;
            endpointOf[ends[p].y, ends[p].x] = p;
        }

        List<Vector2Int>[] paths = new List<Vector2Int>[pairCount];
        bool[] complete = new bool[pairCount];
        GameObject[] pathHolders = new GameObject[pairCount];
        for (int p = 0; p < pairCount; p++)
        {
            paths[p] = new List<Vector2Int>();
            pathHolders[p] = new GameObject("Path_" + p, typeof(RectTransform));
            pathHolders[p].transform.SetParent(gridRoot.transform, false);
            RectTransform ph = pathHolders[p].GetComponent<RectTransform>();
            ph.anchorMin = Vector2.zero;
            ph.anchorMax = Vector2.one;
            ph.offsetMin = Vector2.zero;
            ph.offsetMax = Vector2.zero;
        }

        for (int p = 0; p < pairCount; p++)
        {
            CreateFlowDot(gridRoot.transform, CellToLocal(starts[p].x, starts[p].y, startX, startY, cellSize), cellSize * 0.62f, colors[p]);
            CreateFlowDot(gridRoot.transform, CellToLocal(ends[p].x, ends[p].y, startX, startY, cellSize), cellSize * 0.62f, colors[p]);
        }

        StartCoroutine(RunFlowDrag(grt, gridSize, cellSize, startX, startY, occupancy, endpointOf, paths, complete, pathHolders, colors, pairCount));
    }

    static Vector2 CellToLocal(int c, int r, float startX, float startY, float cellSize)
    {
        return new Vector2(startX + c * cellSize, startY - r * cellSize);
    }

    void CreateFlowDot(Transform parent, Vector2 pos, float size, Color col)
    {
        GameObject dot = new GameObject("FlowDot", typeof(RectTransform), typeof(Image));
        dot.transform.SetParent(parent, false);
        RectTransform rt = dot.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        Image img = dot.GetComponent<Image>();
        img.sprite = UIKit.Rounded(18);
        img.color = col;
        img.raycastTarget = false;
    }

    IEnumerator RunFlowDrag(
        RectTransform gridRt,
        int gridSize,
        float cellSize,
        float startX,
        float startY,
        int[,] occupancy,
        int[,] endpointOf,
        List<Vector2Int>[] paths,
        bool[] complete,
        GameObject[] pathHolders,
        Color[] colors,
        int pairCount)
    {
        int drawing = -1;
        Canvas canvas = overlayRoot.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        while (!levelResolved)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 sp = mouse.position.ReadValue();
                bool inside = RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRt, sp, cam, out Vector2 local);
                Vector2Int cell = LocalToCell(local, startX, startY, cellSize, gridSize);

                if (mouse.leftButton.wasPressedThisFrame && inside && InGrid(cell, gridSize))
                    drawing = BeginFlowPath(cell, occupancy, endpointOf, paths, complete, pathHolders, colors, startX, startY, cellSize);
                else if (mouse.leftButton.isPressed && drawing >= 0 && inside && InGrid(cell, gridSize))
                    ContinueFlowPath(drawing, cell, occupancy, endpointOf, paths, complete, pathHolders, colors, startX, startY, cellSize, pairCount);
                else if (mouse.leftButton.wasReleasedThisFrame)
                    drawing = -1;
            }
            yield return null;
        }
    }

    static bool InGrid(Vector2Int cell, int gridSize)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < gridSize && cell.y < gridSize;
    }

    static Vector2Int LocalToCell(Vector2 local, float startX, float startY, float cellSize, int gridSize)
    {
        int c = Mathf.RoundToInt((local.x - startX) / cellSize);
        int r = Mathf.RoundToInt((startY - local.y) / cellSize);
        return new Vector2Int(c, r);
    }

    int BeginFlowPath(
        Vector2Int cell,
        int[,] occupancy,
        int[,] endpointOf,
        List<Vector2Int>[] paths,
        bool[] complete,
        GameObject[] pathHolders,
        Color[] colors,
        float startX, float startY, float cellSize)
    {
        int color = occupancy[cell.y, cell.x];
        if (color < 0) return -1;

        int keepUntil = IndexOfCell(paths[color], cell);
        if (keepUntil >= 0)
        {
            bool shortened = keepUntil < paths[color].Count - 1;
            TruncateFlowPath(color, keepUntil, occupancy, endpointOf, paths);
            if (shortened) complete[color] = false;
        }
        else
        {
            complete[color] = false;
            ClearFlowPath(color, occupancy, endpointOf, paths);
            paths[color].Add(cell);
            occupancy[cell.y, cell.x] = color;
        }

        RedrawFlowPath(pathHolders[color], paths[color], colors[color], startX, startY, cellSize);
        return color;
    }

    void ContinueFlowPath(
        int color,
        Vector2Int cell,
        int[,] occupancy,
        int[,] endpointOf,
        List<Vector2Int>[] paths,
        bool[] complete,
        GameObject[] pathHolders,
        Color[] colors,
        float startX, float startY, float cellSize,
        int pairCount)
    {
        if (complete[color] || paths[color].Count == 0) return;
        Vector2Int last = paths[color][paths[color].Count - 1];
        if (cell == last) return;

        int backIdx = IndexOfCell(paths[color], cell);
        if (backIdx >= 0)
        {
            TruncateFlowPath(color, backIdx, occupancy, endpointOf, paths);
            complete[color] = false;
            RedrawFlowPath(pathHolders[color], paths[color], colors[color], startX, startY, cellSize);
            return;
        }

        if (Mathf.Abs(cell.x - last.x) + Mathf.Abs(cell.y - last.y) != 1) return;

        int occ = occupancy[cell.y, cell.x];
        int ep = endpointOf[cell.y, cell.x];
        if (occ >= 0 && occ != color) return;
        if (ep >= 0 && ep != color) return;

        paths[color].Add(cell);
        occupancy[cell.y, cell.x] = color;
        RedrawFlowPath(pathHolders[color], paths[color], colors[color], startX, startY, cellSize);

        if (ep == color && paths[color].Count > 1)
        {
            complete[color] = true;
            int done = 0;
            for (int i = 0; i < pairCount; i++) if (complete[i]) done++;
            feedbackText.text = $"<color={HexYellow}>CONNECTED {done}/{pairCount}</color>";
            if (done >= pairCount) OnLevelPassed("ALL DOTS CONNECTED WITHOUT CROSSING!");
        }
    }

    static int IndexOfCell(List<Vector2Int> path, Vector2Int cell)
    {
        for (int i = 0; i < path.Count; i++)
            if (path[i] == cell) return i;
        return -1;
    }

    void ClearFlowPath(int color, int[,] occupancy, int[,] endpointOf, List<Vector2Int>[] paths)
    {
        for (int i = 0; i < paths[color].Count; i++)
        {
            Vector2Int p = paths[color][i];
            if (endpointOf[p.y, p.x] != color)
                occupancy[p.y, p.x] = -1;
        }
        paths[color].Clear();
    }

    void TruncateFlowPath(int color, int keepUntil, int[,] occupancy, int[,] endpointOf, List<Vector2Int>[] paths)
    {
        for (int i = paths[color].Count - 1; i > keepUntil; i--)
        {
            Vector2Int p = paths[color][i];
            if (endpointOf[p.y, p.x] != color)
                occupancy[p.y, p.x] = -1;
            paths[color].RemoveAt(i);
        }
    }

    void RedrawFlowPath(GameObject holder, List<Vector2Int> path, Color col, float startX, float startY, float cellSize)
    {
        for (int i = holder.transform.childCount - 1; i >= 0; i--)
            WorldUtil.Kill(holder.transform.GetChild(i).gameObject);

        float thickness = cellSize * 0.34f;
        for (int i = 1; i < path.Count; i++)
        {
            Vector2 a = CellToLocal(path[i - 1].x, path[i - 1].y, startX, startY, cellSize);
            Vector2 b = CellToLocal(path[i].x, path[i].y, startX, startY, cellSize);
            DrawFlowConnectorLine(holder.transform, a, b, col, thickness);
        }
    }

    void DrawFlowConnectorLine(Transform parent, Vector2 p1, Vector2 p2, Color col, float thickness)
    {
        GameObject line = new GameObject("FlowLine", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(parent, false);
        RectTransform lrt = line.GetComponent<RectTransform>();

        Vector2 dir = p2 - p1;
        float dist = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        lrt.anchoredPosition = (p1 + p2) * 0.5f;
        lrt.sizeDelta = new Vector2(dist + thickness * 0.55f, thickness);
        lrt.localEulerAngles = new Vector3(0f, 0f, angle);

        Image img = line.GetComponent<Image>();
        img.sprite = UIKit.Rounded(8);
        img.color = col;
        img.raycastTarget = false;
    }

    // =========================================================================
    // 3. Cartoon / Emoji Pattern Sequence
    // =========================================================================
    void SetupMemoryGrid(int level)
    {
        promptLabel.text = $"Watch the glowing chalk stars ({level + 2} flashes), then click them in order!";

        int steps = level + 2;
        List<int> sequence = new List<int>();
        System.Random rng = new System.Random(Environment.TickCount + level * 50);
        for (int i = 0; i < steps; i++) sequence.Add(rng.Next(4));

        GameObject starRoot = new GameObject("StarMemoryGrid", typeof(RectTransform));
        starRoot.transform.SetParent(gameViewport, false);
        RectTransform srt = starRoot.GetComponent<RectTransform>();
        srt.anchoredPosition = Vector2.zero;
        srt.sizeDelta = new Vector2(280f, 140f);

        Image[] stars = new Image[4];
        Color[] starColors = { ChalkYellow, ChalkCyan, ChalkPink, ChalkGreen };

        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            float x = (i - 1.5f) * 68f;

            GameObject starBtn = new GameObject("Star_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            starBtn.transform.SetParent(starRoot.transform, false);
            RectTransform srt2 = starBtn.GetComponent<RectTransform>();
            srt2.anchoredPosition = new Vector2(x, 0f);
            srt2.sizeDelta = new Vector2(58f, 58f);

            Image img = starBtn.GetComponent<Image>();
            img.sprite = UIKit.Rounded(10);
            img.color = BoardInner;
            stars[i] = img;

            TextMeshProUGUI starTxt = CreateChalkText(starBtn.transform, "*", 64f, TextAlignmentOptions.Center, starColors[i]);
            starTxt.rectTransform.anchorMin = Vector2.zero;
            starTxt.rectTransform.anchorMax = Vector2.one;
            starTxt.rectTransform.offsetMin = Vector2.zero;
            starTxt.rectTransform.offsetMax = Vector2.zero;

            Button b = starBtn.GetComponent<Button>();
            b.onClick.AddListener(() => OnStarClicked(idx, sequence));
        }

        StartCoroutine(PlayStarSequence(stars, starColors, sequence));
    }

    int starMemoryIndex = 0;
    bool starInputAllowed = false;

    IEnumerator PlayStarSequence(Image[] stars, Color[] starColors, List<int> sequence)
    {
        starInputAllowed = false;
        starMemoryIndex = 0;
        yield return new WaitForSecondsRealtime(0.4f);

        for (int s = 0; s < sequence.Count; s++)
        {
            int idx = sequence[s];
            stars[idx].color = starColors[idx];
            yield return new WaitForSecondsRealtime(0.45f);
            stars[idx].color = BoardInner;
            yield return new WaitForSecondsRealtime(0.2f);
        }

        starInputAllowed = true;
        feedbackText.text = "<color=" + HexYellow + ">YOUR TURN! TAP THE STARS!</color>";
    }

    void OnStarClicked(int clickedIdx, List<int> sequence)
    {
        if (!starInputAllowed || levelResolved) return;

        if (clickedIdx == sequence[starMemoryIndex])
        {
            starMemoryIndex++;
            feedbackText.text = $"<color={HexYellow}>STAR {starMemoryIndex}/{sequence.Count} CORRECT!</color>";
            if (starMemoryIndex >= sequence.Count)
            {
                OnLevelPassed("MEMORY PATTERN CLEARED!");
            }
        }
        else
        {
            OnLevelFailed("Oops, pattern missed! Nice try!");
        }
    }

    // =========================================================================
    // Generic Chalkboard Choice Buttons
    // =========================================================================
    void CreateChalkChoices(string[] choices, int correctIndex)
    {
        GameObject grid = new GameObject("ChalkChoiceGrid", typeof(RectTransform));
        grid.transform.SetParent(gameViewport, false);
        RectTransform grt = grid.GetComponent<RectTransform>();
        grt.anchoredPosition = new Vector2(0f, -32f);
        grt.sizeDelta = new Vector2(580f, 150f);

        int count = choices.Length;
        for (int i = 0; i < count; i++)
        {
            int idx = i;
            float x = (i % 2 == 0) ? -145f : 145f;
            float y = (count <= 2) ? 0f : (i < 2 ? 30f : -30f);

            GameObject btnObj = new GameObject("ChalkChoice_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(grid.transform, false);
            RectTransform brt = btnObj.GetComponent<RectTransform>();
            brt.anchoredPosition = new Vector2(x, y);
            brt.sizeDelta = new Vector2(265f, 48f);

            Image img = btnObj.GetComponent<Image>();
            img.sprite = UIKit.Rounded(10);
            img.type = Image.Type.Sliced;
            img.color = BoardInner;

            // Chalk outline
            GameObject outline = new GameObject("Outline", typeof(RectTransform), typeof(Image));
            outline.transform.SetParent(btnObj.transform, false);
            RectTransform ort = outline.GetComponent<RectTransform>();
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.one;
            ort.offsetMin = Vector2.zero;
            ort.offsetMax = Vector2.zero;
            Image oi = outline.GetComponent<Image>();
            oi.sprite = UIKit.Ring(10, 2);
            oi.type = Image.Type.Sliced;
            oi.color = new Color(ChalkWhite.r, ChalkWhite.g, ChalkWhite.b, 0.45f);

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (idx == correctIndex) OnLevelPassed("SUPER JOB!");
                else OnLevelFailed("Oops, not quite! Keep going!");
            });

            TextMeshProUGUI txt = CreateChalkText(btnObj.transform, choices[i], 22f, TextAlignmentOptions.Left, ChalkWhite);
            txt.rectTransform.anchorMin = Vector2.zero;
            txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = new Vector2(16f, 0f);
            txt.rectTransform.offsetMax = new Vector2(-16f, 0f);
        }

        StartCoroutine(ChalkChoiceKeyListener(correctIndex, count));
    }

    IEnumerator ChalkChoiceKeyListener(int correctIndex, int totalOptions)
    {
        while (!levelResolved)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) CheckChoice(0, correctIndex);
                else if (kb.digit2Key.wasPressedThisFrame || kb.bKey.wasPressedThisFrame) CheckChoice(1, correctIndex);
                else if (totalOptions >= 3 && (kb.digit3Key.wasPressedThisFrame || kb.cKey.wasPressedThisFrame)) CheckChoice(2, correctIndex);
                else if (totalOptions >= 4 && (kb.digit4Key.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) CheckChoice(3, correctIndex);
            }
            yield return null;
        }
    }

    void CheckChoice(int chosen, int target)
    {
        if (chosen == target) OnLevelPassed("SUPER JOB!");
        else OnLevelFailed("Nice try!");
    }
}
