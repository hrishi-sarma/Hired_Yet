using UnityEngine;

public class NightGlowController : MonoBehaviour
{
    AnimeSkyController anime;
    StylizedHDRISkyController hdri;
    float nextSearch;
    float lastNight = -1f;
    float nextRefresh;
    bool startApplied;
    public bool randomizeStartTime = true;
    [Range(0f, 1f)] public float dayChance = 0.6f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<NightGlowController>() != null) return;
        GameObject go = new GameObject("NightGlowController");
        go.AddComponent<NightGlowController>();
        DontDestroyOnLoad(go); // keep driving window/lamp glow after scene switches
    }

    void OnEnable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        // New scene = new materials; force a full re-apply and re-find the sky controller.
        anime = null; hdri = null; lastNight = -1f; nextSearch = 0f; nextRefresh = 0f;
    }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    void Update()
    {
        if (anime == null && hdri == null)
        {
            if (Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + 1f;
            anime = FindFirstObjectByType<AnimeSkyController>();
            if (anime == null) hdri = FindFirstObjectByType<StylizedHDRISkyController>();
            if (anime == null && hdri == null) return;
        }

        if (!startApplied)
        {
            startApplied = true;
            if (randomizeStartTime && Application.isPlaying)
            {
                System.Random rng = new System.Random(System.Guid.NewGuid().GetHashCode() ^ System.Environment.TickCount);
                bool day = rng.NextDouble() < dayChance;
                float t01 = day ? 0.27f + (float)rng.NextDouble() * 0.46f : (float)rng.NextDouble();
                if (!day && t01 > 0.25f && t01 < 0.75f) t01 = t01 < 0.5f ? t01 - 0.3f : t01 + 0.3f;
                t01 = Mathf.Repeat(t01, 1f);
                if (anime != null) anime.timeOfDay = t01;
                else hdri.SetHour(t01 * 24f);
            }
        }

        float night;
        if (anime != null)
        {
            float angle = (anime.timeOfDay * 360f - 90f) * Mathf.Deg2Rad;
            float sunY = Mathf.Sin(angle);
            float daylight = Mathf.Clamp01((sunY + 0.12f) / 0.45f);
            night = 1f - Smooth(0.25f, 0.8f, daylight);
        }
        else
        {
            float elevation = Mathf.Sin((hdri.CurrentHour - 6f) / 12f * Mathf.PI);
            float daylight = Mathf.Clamp01((elevation + 0.12f) / 0.42f);
            night = 1f - Smooth(0.25f, 0.8f, daylight);
        }

        // Window materials are generated after the scene loads, so re-apply periodically too.
        bool refresh = Time.unscaledTime >= nextRefresh;
        if (refresh) nextRefresh = Time.unscaledTime + 2f;
        if (!refresh && Mathf.Abs(night - lastNight) < 0.004f) return;
        lastNight = night;
        StylizedMaterialFactory.SetNightGlow(night);
        ForestMistStage.ApplyNight(night);
        StylizedVehiclePainter.ApplyNight(night);
    }
}
