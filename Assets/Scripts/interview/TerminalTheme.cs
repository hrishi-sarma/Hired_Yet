using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lives on the Canvas of the resume terminal scene.
/// - Custom font slot: drop any TMP Font Asset into "Custom Font". Empty = TMP default font.
/// - Responsive: picks a landscape or portrait reference resolution, and keeps the UI
///   inside the device safe area (notches, rounded corners, system bars).
/// Put this file in a normal runtime folder (e.g. Assets/Scripts), NOT in an Editor folder.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class TerminalTheme : MonoBehaviour
{
    [Header("Font (leave empty to use the TMP default font)")]
    public TMP_FontAsset customFont;

    [Header("Responsive")]
    public RectTransform safeArea;
    public Vector2 landscapeReference = new Vector2(1920f, 1080f);
    public Vector2 portraitReference = new Vector2(1080f, 1920f);
    [Range(0f, 1f)] public float match = 0.5f;

    CanvasScaler scaler;
    Vector2Int lastScreen = Vector2Int.zero;
    Rect lastSafe = Rect.zero;

    void Awake()
    {
        ApplyFont();
        Responsive(true);
    }

    void OnEnable()
    {
        Responsive(true);
    }

    void Update()
    {
        Responsive(false);
    }

    [ContextMenu("Apply Font")]
    public void ApplyFont()
    {
        TMP_FontAsset font = customFont != null ? customFont : TMP_Settings.defaultFontAsset;
        if (font == null) return;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].font == font) continue;
            texts[i].font = font;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(texts[i]);
#endif
        }
    }

    void Responsive(bool force)
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
        Rect safe = Screen.safeArea;
        if (!force && screen == lastScreen && safe == lastSafe) return;
        lastScreen = screen;
        lastSafe = safe;

        if (scaler == null) scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            bool portrait = screen.y > screen.x;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.referenceResolution = portrait ? portraitReference : landscapeReference;
            scaler.matchWidthOrHeight = match;
        }

        if (safeArea != null)
        {
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= screen.x; min.y /= screen.y;
            max.x /= screen.x; max.y /= screen.y;
            safeArea.anchorMin = min;
            safeArea.anchorMax = max;
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            ApplyFont();
            Responsive(true);
        };
    }
#endif
}
