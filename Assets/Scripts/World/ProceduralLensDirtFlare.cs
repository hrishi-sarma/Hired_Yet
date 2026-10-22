using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Subtle procedural lens dirt and a restrained horizontal flare streak.
/// No external textures are required. The effect fades unless a directional light
/// is close to the camera's view direction.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProceduralLensDirtFlare : MonoBehaviour
{
    [Range(0f, 0.35f)] public float flareOpacity = 0.12f;
    [Range(0f, 1f)] public float dayFlareOpacity = 0.55f;
    [Range(0.5f, 0.99f)] public float flareStartDot = 0.7f;
    [Range(0.8f, 1f)] public float flareFullDot = 0.95f;

    Camera targetCamera;
    Canvas canvas;
    RawImage flareImage;
    Texture2D flareTexture;
    float nextCameraSearch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        GameObject host = GameObject.Find("Procedural Lens Dirt Flare (Auto)");
        if (host == null) host = new GameObject("Procedural Lens Dirt Flare (Auto)");
        if (host.GetComponent<ProceduralLensDirtFlare>() == null)
            host.AddComponent<ProceduralLensDirtFlare>();
        DontDestroyOnLoad(host);
    }

    void Update()
    {
        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            if (Time.unscaledTime < nextCameraSearch) return;
            nextCameraSearch = Time.unscaledTime + 0.5f;
            Camera candidate = Camera.main;
            if (candidate == null)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                for (int i = 0; i < cameras.Length; i++)
                    if (cameras[i] != null && cameras[i].isActiveAndEnabled && cameras[i].targetTexture == null)
                    { candidate = cameras[i]; break; }
            }
            if (candidate != targetCamera) AttachTo(candidate);
        }
        if (targetCamera == null || canvas == null) return;

        Light sun = FindSun();
        if (sun == null)
        {
            flareImage.color = new Color(1f, 1f, 1f, 0f);
            return;
        }

        Vector3 sunDirection = -sun.transform.forward;
        float alignment = Vector3.Dot(targetCamera.transform.forward, sunDirection.normalized);
        float visible = SmoothStep(flareStartDot, flareFullDot, alignment);
        Vector3 viewport = targetCamera.WorldToViewportPoint(targetCamera.transform.position + sunDirection * 1000f);
        bool onScreen = viewport.z > 0f && viewport.x >= -0.1f && viewport.x <= 1.1f &&
                        viewport.y >= -0.1f && viewport.y <= 1.1f;
        float strength = onScreen ? visible : 0f;

        float day = Mathf.Clamp01(sun.intensity / 0.6f);
        flareImage.color = new Color(1f, 0.91f, 0.78f, Mathf.Lerp(flareOpacity, dayFlareOpacity, day) * strength);
        RectTransform flareRect = flareImage.rectTransform;
        flareRect.anchoredPosition = new Vector2(
            (viewport.x - 0.5f) * targetCamera.pixelWidth,
            (viewport.y - 0.5f) * targetCamera.pixelHeight);
        flareRect.sizeDelta = new Vector2(targetCamera.pixelWidth * Mathf.Lerp(0.56f, 0.9f, day), Mathf.Max(24f, targetCamera.pixelHeight * Mathf.Lerp(0.07f, 0.12f, day)));
    }

    void AttachTo(Camera cam)
    {
        if (canvas != null) WorldUtil.Kill(canvas.gameObject);
        targetCamera = cam;
        canvas = null;
        flareImage = null;
        if (cam == null) return;

        if (flareTexture == null) flareTexture = MakeFlareTexture(512, 64);

        GameObject canvasGo = new GameObject("Procedural Lens Dirt Canvas");
        canvasGo.transform.SetParent(cam.transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = Mathf.Max(cam.nearClipPlane + 0.05f, 0.1f);
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32000;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        flareImage = MakeImage("Sun Flare Streak", flareTexture, canvas.transform, false);
        flareImage.color = new Color(1f, 0.91f, 0.78f, 0f);
    }

    static RawImage MakeImage(string name, Texture texture, Transform parent, bool fullscreen)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RawImage image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = fullscreen ? Vector2.zero : new Vector2(0.5f, 0.5f);
        rect.anchorMax = fullscreen ? Vector2.one : new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        if (!fullscreen) rect.sizeDelta = new Vector2(600f, 80f);
        return image;
    }

    Light FindSun()
    {
        Light best = null;
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light l = lights[i];
            if (l == null || l.type != LightType.Directional || !l.isActiveAndEnabled || l.intensity <= 0.01f) continue;
            if (best == null || l.intensity > best.intensity) best = l;
        }
        return best;
    }

    static float SmoothStep(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / Mathf.Max(0.0001f, b - a));
        return t * t * (3f - 2f * t);
    }

    static Texture2D MakeFlareTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        tex.name = "ProceduralLensFlareStreak";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = x / (float)(width - 1);
            float v = y / (float)(height - 1);
            float horizontalFade = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 2f), 2.4f);
            float centerLine = Mathf.Exp(-Mathf.Abs(v - 0.5f) * 85f);
            float broadGlow = Mathf.Exp(-Mathf.Abs(v - 0.5f) * 12f);
            float alpha = Mathf.Clamp01(horizontalFade * (centerLine * 0.42f + broadGlow * 0.12f));
            pixels[y * width + x] = new Color32(255, 242, 216, (byte)(alpha * 150f));
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }

    void OnDestroy()
    {
        if (canvas != null) WorldUtil.Kill(canvas.gameObject);
        if (flareTexture != null) WorldUtil.Kill(flareTexture);
    }
}
