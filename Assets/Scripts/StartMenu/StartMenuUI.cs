using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Creates the text-only PostItMenu on top of the StartMenu world once it is ready.
/// Font and credits are copied from the resume scene's ResumeFormUI by the builder tool.
/// </summary>
[AddComponentMenu("Start Menu/Start Menu UI")]
public class StartMenuUI : MonoBehaviour
{
    public Font font;
    [TextArea(3, 8)] public string creditsText = "A Job Interview Game\n\nThanks for playing!";
    [Tooltip("Scene that holds the resume generator (filled by the builder). Stage 3 replaces this with the in-scene transition.")]
    public string resumeScene = "";

    public Texture2D logo;

    // The scene stores its own creditsText copy, so the menu shows this fixed text until real credits exist.
    const string CreditsShown = "WIP";

    public static bool EnteredFromMenu;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { EnteredFromMenu = false; }

#if UNITY_EDITOR
    const string LogoPath = "Assets/Image/Logo.png";

    void OnValidate()
    {
        if (logo != null) return;
        logo = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
        if (logo != null) UnityEditor.EditorUtility.SetDirty(this);
    }

    [UnityEditor.InitializeOnLoadMethod]
    static void DisableUnitySplash()
    {
        if (UnityEditor.PlayerSettings.SplashScreen.show) UnityEditor.PlayerSettings.SplashScreen.show = false;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ShowLogoSplash()
    {
        var ui = FindFirstObjectByType<StartMenuUI>();
        if (ui == null) return;
        Texture2D tex = ui.logo;
#if UNITY_EDITOR
        if (tex == null) tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
#endif
        if (tex == null) tex = Resources.Load<Texture2D>("Logo");
        if (tex == null) return;
        var go = new GameObject("Logo Splash", typeof(Canvas), typeof(CanvasScaler));
        go.AddComponent<LogoSplash>().Run(tex);
    }

    PostItMenu menu;
    bool leaving;
    Image fade;

    IEnumerator Start()
    {
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        // wait for the world (the loading screen covers it) so the menu doesn't sit under the loader
        if (FindFirstObjectByType<MenuWorldBootstrap>() != null)
            while (!MenuWorldBootstrap.Ready) yield return null;
        yield return null;

        if (font == null) font = SaveSystem.UiFont;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }
        // same global state ResumeFormUI used to set up for the menu
        SaveSystem.MenuScene = SceneManager.GetActiveScene().name; SaveSystem.Active = -1; SaveSystem.Data = null; SaveSystem.Pending = null;
        SaveSystem.UiFont = font; Time.timeScale = 1f; AudioListener.pause = false;
        menu = gameObject.AddComponent<PostItMenu>();
        menu.Init(font, font, OnStart, CreditsShown);   // credits are a placeholder for now
    }

    void OnStart()
    {
        if (leaving) return;
        if (string.IsNullOrEmpty(resumeScene) || !Application.CanStreamedLevelBeLoaded(resumeScene))
        {
            Debug.LogError("StartMenuUI: resume scene '" + resumeScene + "' missing or not in Build Settings (run Tools > Start Menu > Add Menu UI To Open Scene).");
            return;
        }
        leaving = true;
        StartCoroutine(Leave());
    }

    // menu fades out (PostItMenu.HideAll runs right after this callback), screen fades to black, resume scene loads
    // and starts with the eye-opening animation; loading the scene unloads the whole world (no cost while writing).
    IEnumerator Leave()
    {
        MenuAudio.FadeOut(0.7f);
        var go = new GameObject("Fade", typeof(Canvas), typeof(Image));
        var cv = go.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 500;
        fade = go.GetComponent<Image>(); fade.color = Color.clear; fade.raycastTarget = true;
        for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / 0.7f);
            fade.color = new Color(0f, 0f, 0f, k * k * (3f - 2f * k));
            yield return null;
        }
        fade.color = Color.black;
        EnteredFromMenu = true;
        var op = SceneManager.LoadSceneAsync(resumeScene);
        while (!op.isDone) yield return null;
    }
}

public class LogoSplash : MonoBehaviour
{
    const float FadeIn = 1f, Hold = 1.6f, FadeOut = 1f, Reveal = 0.6f;

    public void Run(Texture2D tex) { StartCoroutine(Play(tex)); }

    static float Step() { return Mathf.Min(Time.unscaledDeltaTime, 0.05f); }

    IEnumerator Play(Texture2D tex)
    {
        DontDestroyOnLoad(gameObject);
        var cv = GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 32500;
        var sc = GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f); sc.matchWidthOrHeight = 0.5f;

        var bg = new GameObject("Black", typeof(Image)); var br = bg.GetComponent<RectTransform>(); br.SetParent(transform, false);
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = br.offsetMax = Vector2.zero;
        var bi = bg.GetComponent<Image>(); bi.color = Color.black; bi.raycastTarget = true;

        var lg = new GameObject("Logo", typeof(RawImage)); var lr = lg.GetComponent<RectTransform>(); lr.SetParent(transform, false);
        lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f); lr.anchoredPosition = Vector2.zero;
        float k = Mathf.Min(1000f / tex.width, 500f / tex.height);
        lr.sizeDelta = new Vector2(tex.width * k, tex.height * k);
        var li = lg.GetComponent<RawImage>(); li.texture = tex; li.raycastTarget = false; li.color = new Color(1f, 1f, 1f, 0f);

        for (float t = 0f; t < FadeIn; t += Step()) { li.color = new Color(1f, 1f, 1f, Ease(t / FadeIn)); yield return null; }
        li.color = Color.white;
        for (float t = 0f; t < Hold; t += Step()) yield return null;
        for (float t = 0f; t < FadeOut; t += Step()) { li.color = new Color(1f, 1f, 1f, 1f - Ease(t / FadeOut)); yield return null; }
        li.color = new Color(1f, 1f, 1f, 0f);
        for (float t = 0f; t < Reveal; t += Step()) { bi.color = new Color(0f, 0f, 0f, 1f - Ease(t / Reveal)); yield return null; }
        WorldUtil.Kill(gameObject);
    }

    static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
