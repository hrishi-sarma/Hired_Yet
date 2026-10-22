#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

/// <summary>
/// PUT THIS FILE IN: Assets/Editor/TerminalSceneBuilder.cs
/// Open via  Tools > Interview Game > Resume Terminal Builder.
/// Builds a new scene with the complete terminal UI and wires TerminalController.
/// </summary>
public class TerminalSceneBuilder : EditorWindow
{
    // ---------- palette ----------
    static readonly Color Bg = Hex("#040704");
    static readonly Color Panel = Hex("#08100A");
    static readonly Color Accent = Hex("#33FF66");
    static readonly Color Dim = Hex("#1E9945");
    static readonly Color Bright = Hex("#D8FFE2");

    // ---------- window options ----------
    TMP_FontAsset font;
    float fontSize = 30f;
    bool scanlines = true;
    bool vignette = true;
    bool addToBuildSettings = true;
    string sceneName = "ResumeTerminal";

    [MenuItem("Tools/Interview Game/Resume Terminal Builder")]
    static void Open()
    {
        GetWindow<TerminalSceneBuilder>("Terminal Builder");
    }

    void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Resume Terminal Scene Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        font = (TMP_FontAsset)EditorGUILayout.ObjectField(
            new GUIContent("Custom Font (optional)", "Any TMP Font Asset. Leave empty to use the TMP default font. You can change it later on the Canvas > TerminalTheme component."),
            font, typeof(TMP_FontAsset), false);
        if (font == null)
            EditorGUILayout.HelpBox("No font set: the TMP default font will be used. A monospace font (VT323, Share Tech Mono, IBM Plex Mono, Consolas) looks far more like a terminal.", MessageType.Info);

        fontSize = EditorGUILayout.Slider("Base Font Size", fontSize, 18f, 48f);
        scanlines = EditorGUILayout.Toggle("CRT Scanlines", scanlines);
        vignette = EditorGUILayout.Toggle("Screen Vignette", vignette);
        addToBuildSettings = EditorGUILayout.Toggle("Add To Build Settings", addToBuildSettings);
        sceneName = EditorGUILayout.TextField("Scene Name", sceneName);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Build Terminal Scene", GUILayout.Height(34))) Build();
    }

    // =====================================================================
    // BUILD
    // =====================================================================
    static void CreateEventSystem()
    {
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
#endif
    }

#if ENABLE_INPUT_SYSTEM
    [MenuItem("Tools/Interview Game/Fix EventSystem For Input System")]
    static void FixEventSystem()
    {
        var es = Object.FindObjectOfType<EventSystem>();
        if (es == null)
        {
            CreateEventSystem();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return;
        }

        var legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy != null) Object.DestroyImmediate(legacy);

        if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            var module = es.gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        EditorUtility.SetDirty(es.gameObject);
        EditorSceneManager.MarkSceneDirty(es.gameObject.scene);
        Debug.Log("EventSystem now uses InputSystemUIInputModule. Save the scene.");
    }
#endif

    void Build()
    {
#if !ENABLE_INPUT_SYSTEM
        EditorUtility.DisplayDialog("Input System",
            "The new Input System isn't active.\n\n1. Window > Package Manager > install 'Input System'.\n2. Project Settings > Player > Active Input Handling = 'Input System Package (New)'.\n3. Let Unity restart, then run the builder again.", "OK");
        return;
#endif
        if (TMP_Settings.instance == null)
        {
            EditorUtility.DisplayDialog("TextMeshPro", "Import TMP Essentials first:\nWindow > TextMeshPro > Import TMP Essential Resources", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Bg;
        }

        // ----- EventSystem (new Input System UI module) -----
        CreateEventSystem();

        // ----- Canvas -----
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // ----- Background + safe area -----
        var bg = NewRect("Background", canvasGo.transform);
        Stretch(bg);
        NewImage(bg.gameObject, Bg, false);

        var safe = NewRect("SafeArea", bg);
        Stretch(safe);

        // ----- Terminal frame (percentage margins => scales with any screen) -----
        var frame = NewRect("TerminalFrame", safe);
        frame.anchorMin = new Vector2(0.025f, 0.04f);
        frame.anchorMax = new Vector2(0.975f, 0.96f);
        frame.offsetMin = Vector2.zero;
        frame.offsetMax = Vector2.zero;
        NewImage(frame.gameObject, Panel, false);
        AddOutline(frame.gameObject, A(Accent, 0.85f), 2f);
        AddOutline(frame.gameObject, A(Accent, 0.12f), 7f);

        var frameLayout = frame.gameObject.AddComponent<VerticalLayoutGroup>();
        frameLayout.padding = new RectOffset(30, 30, 18, 26);
        frameLayout.spacing = 12f;
        frameLayout.childControlWidth = true;
        frameLayout.childControlHeight = true;
        frameLayout.childForceExpandWidth = true;
        frameLayout.childForceExpandHeight = false;

        // ----- Header -----
        var header = NewRect("Header", frame);
        var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;
        headerLayout.spacing = 16f;
        Le(header.gameObject, prefH: 38f);

        var title = NewText("Title", header, "applicant@hiring-terminal:~$ resume.exe", 22f, Bright, TextAlignmentOptions.MidlineLeft);
        Le(title.gameObject, flexW: 1f, minW: 40f);
        var status = NewText("Status", header, "SECURE LINK // ONLINE", 20f, Dim, TextAlignmentOptions.MidlineRight);
        Le(status.gameObject, prefW: 300f);

        NewDivider(frame);

        // ----- Log scroll -----
        var scrollRt = NewRect("LogScroll", frame);
        Le(scrollRt.gameObject, minH: 120f, flexH: 1f);
        var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();

        var viewport = NewRect("Viewport", scrollRt);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        var logRt = NewRect("LogText", viewport);
        logRt.anchorMin = new Vector2(0f, 1f);
        logRt.anchorMax = new Vector2(1f, 1f);
        logRt.pivot = new Vector2(0f, 1f);
        logRt.anchoredPosition = Vector2.zero;
        logRt.sizeDelta = new Vector2(0f, 100f);
        var logText = logRt.gameObject.AddComponent<TextMeshProUGUI>();
        logText.richText = true;
        logText.fontSize = fontSize;
        logText.color = Accent;
        logText.alignment = TextAlignmentOptions.TopLeft;
        logText.lineSpacing = 5f;
        logText.overflowMode = TextOverflowModes.Overflow;
        logText.raycastTarget = true;
        logText.text = "";
        var fitter = logRt.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        var clickHandler = logRt.gameObject.AddComponent<LogClickHandler>();

        // Scrollbar
        var sbRt = NewRect("Scrollbar", scrollRt);
        sbRt.anchorMin = new Vector2(1f, 0f);
        sbRt.anchorMax = new Vector2(1f, 1f);
        sbRt.pivot = new Vector2(1f, 1f);
        sbRt.sizeDelta = new Vector2(10f, 0f);
        sbRt.anchoredPosition = Vector2.zero;
        var sbImg = NewImage(sbRt.gameObject, A(Dim, 0.15f), true);
        var scrollbar = sbRt.gameObject.AddComponent<Scrollbar>();
        var slide = NewRect("Sliding Area", sbRt);
        Stretch(slide);
        var handle = NewRect("Handle", slide);
        Stretch(handle);
        var handleImg = NewImage(handle.gameObject, A(Dim, 0.9f), true);
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImg;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        var sbColors = scrollbar.colors;
        sbColors.highlightedColor = Color.white;
        sbColors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        scrollbar.colors = sbColors;

        scroll.content = logRt;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false;
        scroll.scrollSensitivity = 30f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = -4f;

        NewDivider(frame);

        // ----- Input row -----
        var row = NewRect("InputRow", frame);
        NewImage(row.gameObject, new Color(0f, 0f, 0f, 0.35f), false);
        AddOutline(row.gameObject, A(Dim, 0.6f), 1.5f);
        var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(18, 18, 0, 0);
        rowLayout.spacing = 12f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;
        Le(row.gameObject, prefH: 68f);

        var prompt = NewText("Prompt", row, ">", fontSize + 2f, Bright, TextAlignmentOptions.Midline);
        Le(prompt.gameObject, prefW: 30f);

        // Input field
        var inputRt = NewRect("InputField", row);
        Le(inputRt.gameObject, flexW: 1f, minW: 100f);
        var inputBg = NewImage(inputRt.gameObject, new Color(0f, 0f, 0f, 0f), true);

        var textArea = NewRect("Text Area", inputRt);
        Stretch(textArea);
        textArea.offsetMin = new Vector2(6f, 4f);
        textArea.offsetMax = new Vector2(-6f, -4f);
        textArea.gameObject.AddComponent<RectMask2D>();

        var placeholder = NewText("Placeholder", textArea, "", fontSize, A(Dim, 0.6f), TextAlignmentOptions.MidlineLeft);
        Stretch(placeholder.rectTransform);
        var inputText = NewText("Text", textArea, "", fontSize, Bright, TextAlignmentOptions.MidlineLeft);
        Stretch(inputText.rectTransform);

        var input = inputRt.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = inputBg;
        input.textViewport = textArea;
        input.textComponent = inputText;
        input.placeholder = placeholder;
        input.transition = Selectable.Transition.None;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.characterLimit = 0;
        input.customCaretColor = true;
        input.caretColor = Bright;
        input.caretWidth = 4;
        input.caretBlinkRate = 0.8f;
        input.selectionColor = A(Accent, 0.30f);

        var counter = NewText("CounterText", row, "", 20f, Dim, TextAlignmentOptions.MidlineRight);
        counter.richText = true;
        Le(counter.gameObject, prefW: 380f);

        // ----- Overlays (non-interactive, on top of everything) -----
        string genDir = "Assets/Terminal/Generated";
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Terminal/Generated"));

        if (scanlines)
        {
            Sprite sl = MakeScanlineSprite(genDir + "/Scanlines.png");
            var rt = NewRect("Scanlines", canvasGo.transform);
            Stretch(rt);
            var img = NewImage(rt.gameObject, Color.white, false);
            img.sprite = sl;
            img.type = Image.Type.Tiled;
        }
        if (vignette)
        {
            Sprite vg = MakeVignetteSprite(genDir + "/Vignette.png");
            var rt = NewRect("Vignette", canvasGo.transform);
            Stretch(rt);
            var img = NewImage(rt.gameObject, Color.white, false);
            img.sprite = vg;
            img.type = Image.Type.Simple;
        }

        // ----- Theme (custom font slot + responsiveness) -----
        var theme = canvasGo.AddComponent<TerminalTheme>();
        theme.customFont = font;
        theme.safeArea = safe;
        theme.ApplyFont();

        // ----- Controller wiring -----
        var ctrlGo = new GameObject("Controller");
        var ctrl = ctrlGo.AddComponent<TerminalController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("logText").objectReferenceValue = logText;
        so.FindProperty("scrollRect").objectReferenceValue = scroll;
        so.FindProperty("inputField").objectReferenceValue = input;
        so.FindProperty("counterText").objectReferenceValue = counter;
        so.FindProperty("logClickHandler").objectReferenceValue = clickHandler;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ----- Save -----
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/" + sceneName + ".unity");
        EditorSceneManager.SaveScene(scene, path);

        if (addToBuildSettings)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Any(s => s.path == path)) list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = canvasGo;

        EditorUtility.DisplayDialog("Terminal Builder",
            "Scene created:\n" + path +
            "\n\nReminders:\n- interview_data.json must be in Assets/Resources/\n- 'InterviewRoom' must be in Build Settings\n- Change the font anytime on Canvas > TerminalTheme.", "OK");
    }

    // =====================================================================
    // HELPERS
    // =====================================================================
    static Color Hex(string h)
    {
        Color c;
        ColorUtility.TryParseHtmlString(h, out c);
        return c;
    }

    static Color A(Color c, float a)
    {
        c.a = a;
        return c;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Image NewImage(GameObject go, Color color, bool raycast)
    {
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    static void AddOutline(GameObject go, Color color, float distance)
    {
        var o = go.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(distance, -distance);
        o.useGraphicAlpha = false;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    static LayoutElement Le(GameObject go, float minW = -1f, float minH = -1f, float prefW = -1f, float prefH = -1f, float flexW = -1f, float flexH = -1f)
    {
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = minW;
        le.minHeight = minH;
        le.preferredWidth = prefW;
        le.preferredHeight = prefH;
        le.flexibleWidth = flexW;
        le.flexibleHeight = flexH;
        return le;
    }

    static void NewDivider(Transform parent)
    {
        var rt = NewRect("Divider", parent);
        NewImage(rt.gameObject, A(Dim, 0.55f), false);
        Le(rt.gameObject, prefH: 2f, flexH: 0f);
    }

    // ---------- generated textures ----------
    static Sprite MakeScanlineSprite(string assetPath)
    {
        // 256x4 strip: 2 clear rows + 2 dark rows. Wide so the tiled Image needs few quads.
        var tex = new Texture2D(256, 4, TextureFormat.RGBA32, false);
        for (int y = 0; y < 4; y++)
        {
            float a = y < 2 ? 0f : 0.20f;
            for (int x = 0; x < 256; x++) tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
        }
        tex.Apply();
        return SaveSprite(tex, assetPath, FilterMode.Point, TextureWrapMode.Repeat);
    }

    static Sprite MakeVignetteSprite(string assetPath)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (float)(size - 1) - 0.5f) * 2f;
                float ny = (y / (float)(size - 1) - 0.5f) * 2f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.SmoothStep(0f, 0.70f, Mathf.InverseLerp(0.55f, 1.40f, d));
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        }
        tex.Apply();
        return SaveSprite(tex, assetPath, FilterMode.Bilinear, TextureWrapMode.Clamp);
    }

    static Sprite SaveSprite(Texture2D tex, string assetPath, FilterMode filter, TextureWrapMode wrap)
    {
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), assetPath), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = filter;
        importer.wrapMode = wrap;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }
}
#endif