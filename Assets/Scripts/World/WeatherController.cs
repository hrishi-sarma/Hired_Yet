using UnityEngine;

public enum WeatherType { Clear, LightRain, Fog, MorningFog = Fog, Thunderstorm = 3, Rain = LightRain }

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public class WeatherController : MonoBehaviour
{
    public static WeatherController Instance { get; private set; }

    public bool weatherEnabled = true;
    public WeatherType weather = WeatherType.Clear;
    [Range(0f, 1f)] public float intensityCap = 0.9f;
    [Min(0.5f)] public float fadeSeconds = 6f;
    [Range(50, 1500)] public int maxRainParticles = 600;
    [Range(0f, 1f)] public float windBoost = 0.3f;
    public float fogDensity = 0.02f;
    public float rainHazeDensity = 0.004f;
    [Range(0f, 1f)] public float rainVolume = 0.7f;
    [Range(0f, 1f)] public float animalDuck = 0.4f;
    [Min(0.5f)] public float audioFadeSeconds = 4f;
    public bool lightning = true;                       // only used by Thunderstorm (normal rain never has lightning)
    public bool puddles = true;
    [Tooltip("Daytime haze during rain / thunderstorms (fog weather itself uses fogDensity, which is higher). Nights have none.")] public float rainDayFogDensity = 0.009f;
    public float stormDayFogDensity = 0.012f;

    [Header("Automatic weather cycle: clear -> rain / thunderstorm -> fog -> clear (all fades are gradual)")]
    public bool autoWeather = true;
    public Vector2 firstClearSeconds = new Vector2(150f, 360f);   // before the very first rain
    public Vector2 clearSeconds = new Vector2(240f, 540f);
    public Vector2 rainSeconds = new Vector2(150f, 300f);
    public Vector2 fogSeconds = new Vector2(120f, 240f);
    [Range(0f, 1f)] public float stormChance = 0.35f;
    [Min(1f)] public float cycleFadeSeconds = 45f;

    const float RainLife = 0.9f;

    static readonly int IdTop = Shader.PropertyToID("_SkyColorTop");
    static readonly int IdMid = Shader.PropertyToID("_SkyColorMiddle");
    static readonly int IdHor = Shader.PropertyToID("_SkyColorHorizon");
    static readonly int IdBot = Shader.PropertyToID("_SkyColorBottom");
    static readonly int IdLit = Shader.PropertyToID("_CloudColorLit");
    static readonly int IdShadow = Shader.PropertyToID("_CloudColorShadow");
    static readonly int IdSunset = Shader.PropertyToID("_CloudColorSunset");
    static readonly int IdCover = Shader.PropertyToID("_CloudCoverage");
    static readonly int IdDensity = Shader.PropertyToID("_CloudDensity");

    float rainW, fogW, stormW;          // independent weights so one type can fade out while the next fades in
    float fade = 6f, phaseEnd = -1f;
    WeatherLightning lightningSys;
    Camera cam;
    ParticleSystem rain;
    Material rainMat;
    bool suppressed;
    float nextCheck;
    float nextTint;
    bool hasWritten;
    float baseD, wroteD;
    Color baseC, wroteC, baseA, wroteA;
    Light sun;
    float sunBase, sunWrote;
    AtmosphereEffects atm;
    float rayBase;
    SeamlessLoop rainLoop;
    float audioLevel;
    float nextFind;
    ValleyFogDriver valley;
    float valleyBase = -1f;
    AudioClip rainClip, thunderClip;
    bool clipsTried, ducked;
    float nextClipTry;
    WeatherPuddles puddleSys;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedEnsure;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedEnsure;
        Ensure();
    }

    // The controller is scene-bound (it caches scene objects), so the one created in the start menu died on load.
    // Re-create it every time a scene loads (start menu -> game).
    static void OnSceneLoadedEnsure(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) { Ensure(); }

    static void Ensure()
    {
        if (Instance != null || FindFirstObjectByType<WeatherController>() != null) return;
        new GameObject("WeatherController").AddComponent<WeatherController>();
    }

    /// <summary>True while it is raining / storming (or the rain is still fading out). Used to block events such as the paper gust.</summary>
    public static bool Raining
    {
        get
        {
            WeatherController i = Instance;
            return i != null && i.weatherEnabled && (i.rainW > 0.02f || i.weather == WeatherType.LightRain || i.weather == WeatherType.Thunderstorm);
        }
    }

    public static void Set(WeatherType type)
    {
        if (Instance == null) return;
        Instance.weather = type;
        Instance.fade = Instance.fadeSeconds;                 // manual change: the normal (short) fade
        Instance.phaseEnd = Time.time + Instance.Duration(type);   // the automatic cycle carries on from here
    }

    public static void SetAuto(bool on)
    {
        if (Instance == null) return;
        Instance.autoWeather = on;
        Instance.phaseEnd = -1f;
    }

    float Duration(WeatherType t)
    {
        Vector2 v = t == WeatherType.Clear ? clearSeconds : t == WeatherType.Fog ? fogSeconds : rainSeconds;
        return Random.Range(Mathf.Min(v.x, v.y), Mathf.Max(v.x, v.y));
    }

    void AutoTick()
    {
        if (!autoWeather || !weatherEnabled) { phaseEnd = -1f; return; }
        if (suppressed) return;
        float now = Time.time;
        if (phaseEnd < 0f)
        {
            phaseEnd = now + (weather == WeatherType.Clear ? Random.Range(firstClearSeconds.x, firstClearSeconds.y) : Duration(weather));
            return;
        }
        if (now < phaseEnd) return;
        WeatherType next;
        if (weather == WeatherType.Clear) next = Random.value < stormChance ? WeatherType.Thunderstorm : WeatherType.LightRain;
        else if (weather == WeatherType.Fog) next = WeatherType.Clear;
        else next = WeatherType.Fog;                           // after any rain comes fog
        weather = next;
        fade = Mathf.Max(fadeSeconds, cycleFadeSeconds);
        phaseEnd = now + Duration(next);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDisable()
    {
        Restore();
        Wind.Boost = 0f;
        if (ducked) { AudioDirector.SetVolume("animals", 1f); ducked = false; }
        if (rainLoop != null) rainLoop.Stop();
        audioLevel = 0f;
        if (lightningSys != null) lightningSys.Stop();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (rainMat != null) Destroy(rainMat);
    }

    static bool Differs(Color a, Color b)
    {
        return a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a;
    }

    static float Bell(float x, float c, float w)
    {
        float d = (x - c) / w;
        return Mathf.Exp(-d * d);
    }

    void LateUpdate()
    {
        AutoTick();
        bool rainType = weather == WeatherType.LightRain || weather == WeatherType.Thunderstorm;
        bool fogType = weather == WeatherType.Fog;
        bool want = weatherEnabled && weather != WeatherType.Clear;
        bool drying = puddleSys != null && puddleSys.Busy;
        bool lit = lightningSys != null && lightningSys.Busy;
        if (!want && rainW <= 0f && fogW <= 0f && !drying && audioLevel <= 0f && !lit) return;

        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 0.5f;
            suppressed = FindAnyObjectByType<InterviewUI>() != null;
        }

        bool on = want && !suppressed;
        float rate = suppressed ? 0.5f : Mathf.Max(0.5f, fade);
        float step = Time.deltaTime / rate;
        rainW = Mathf.MoveTowards(rainW, on && rainType ? intensityCap : 0f, step);
        fogW = Mathf.MoveTowards(fogW, on && fogType ? intensityCap : 0f, step);
        stormW = Mathf.MoveTowards(stormW, on && weather == WeatherType.Thunderstorm ? 1f : 0f, step);
        if (cam == null) cam = Camera.main;

        if (Mathf.Max(rainW, fogW) <= 0.0005f)
        {
            rainW = 0f; fogW = 0f; stormW = 0f;
            Restore();
            StopRain();
            Wind.Boost = 0f;
            if (lightningSys != null) { if (lightningSys.Busy) lightningSys.Tick(false, cam); else lightningSys.Stop(); }
        }
        else
        {
            float F = lightningSys != null ? lightningSys.Flash : 0f;
            ApplyAtmosphere(rainW, fogW, stormW, F);
            ApplySky(rainW, fogW, stormW, F);
            if (rainW > 0.001f)
            {
                UpdateRain(rainW, stormW);
                Wind.Boost = Mathf.Min(1f, windBoost * rainW * (1f + 0.8f * stormW));
            }
            else { StopRain(); Wind.Boost = 0f; }
            UpdateLightning();
        }

        UpdateAudio(on && rainType);
        if (puddles && (rainW > 0f || drying))
        {
            if (puddleSys == null)
            {
                puddleSys = new GameObject("WeatherPuddles").AddComponent<WeatherPuddles>();
                puddleSys.transform.SetParent(transform, false);
            }
            puddleSys.Tick(rainW, Time.deltaTime, cam);
        }
    }

    void ApplyAtmosphere(float wr, float wf, float ws, float F)
    {
        float d0 = RenderSettings.fogDensity;
        Color c0 = RenderSettings.fogColor;
        Color a0 = RenderSettings.ambientLight;
        if (!hasWritten || d0 != wroteD) baseD = d0;
        if (!hasWritten || Differs(c0, wroteC)) baseC = c0;
        if (!hasWritten || Differs(a0, wroteA)) baseA = a0;

        float lum = baseC.grayscale;
        float d = baseD;
        Color c = baseC;
        float k = 1f;
        if (wf > 0f)
        {
            d = Mathf.Lerp(d, Mathf.Max(baseD, fogDensity), wf);
            c = Color.Lerp(c, new Color(lum * 1.15f, lum * 1.18f, lum * 1.2f, 1f), 0.85f * wf);
            k -= 0.12f * wf;
        }
        if (wr > 0f)
        {
            d = Mathf.Max(d * (1f + 0.8f * wr), rainHazeDensity * (1f + 0.6f * ws) * wr);
            float day = Mathf.Clamp01((lum - 0.12f) / 0.25f);                // 0 at night, 1 in daylight (from the base fog brightness)
            float dayFog = Mathf.Lerp(rainDayFogDensity, stormDayFogDensity, ws) * wr * day;
            d = Mathf.Max(d, dayFog);                                        // fades with wr, so rain -> fog weather cross-fades smoothly
            float g = 1f - 0.75f * ws;                                       // thunderstorms are darker and greyer
            c = Color.Lerp(c, new Color(lum * 0.3f * g, lum * 0.33f * g, lum * 0.38f * g, 1f), 0.85f * wr);
            k *= 1f - (0.3f + 0.32f * ws) * wr;
        }
        wroteD = d;
        wroteC = c;
        wroteA = new Color(baseA.r * k, baseA.g * k, baseA.b * k, baseA.a);

        if (F > 0.01f)                                                       // lightning: whole-scene flash, scaled by that strike's brightness
        {
            wroteA = new Color(wroteA.r + 0.55f * F, wroteA.g + 0.6f * F, wroteA.b + 0.75f * F, wroteA.a);
            wroteC = Color.Lerp(wroteC, new Color(0.75f, 0.8f, 0.92f, 1f), F * 0.45f);
        }

        RenderSettings.fogDensity = wroteD;
        RenderSettings.fogColor = wroteC;
        RenderSettings.ambientLight = wroteA;
        hasWritten = true;

        if (sun == null) sun = RenderSettings.sun;
        if (sun != null)
        {
            if (sun.intensity != sunWrote || sunBase <= 0f && sunWrote <= 0f) sunBase = sun.intensity;
            sunWrote = sunBase * (1f - 0.45f * wf) * (1f - 0.7f * wr) * (1f - 0.92f * ws);
            if (F > 0.01f && sunBase > 0.05f) sunWrote += sunBase * 0.5f * F;   // directional flash (shadows flicker too); never at night
            sun.intensity = sunWrote;
        }

        if ((atm == null || valley == null) && Time.unscaledTime >= nextFind)
        {
            nextFind = Time.unscaledTime + 2f;
            if (atm == null) atm = FindFirstObjectByType<AtmosphereEffects>();
            if (valley == null) valley = FindFirstObjectByType<ValleyFogDriver>();
        }
        if (valley != null)
        {
            if (valleyBase < 0f) valleyBase = valley.opacity;
            valley.opacity = valleyBase * (1f - 0.55f * wr);
        }
        if (atm != null)
        {
            if (rayBase <= 0f) rayBase = atm.rayStrength;
            atm.rayStrength = rayBase * (1f - Mathf.Clamp01(Mathf.Max(wr, wf) * 1.2f));
        }
    }

    void ApplySky(float wr, float wf, float ws, float F)
    {
        Material sky = RenderSettings.skybox;
        if (sky == null || !sky.HasProperty(IdLit)) return;
        if (wf > 0.001f)
        {
            float t = 0.85f * wf;
            FogBlend(sky, IdTop, t); FogBlend(sky, IdMid, t); FogBlend(sky, IdHor, t); FogBlend(sky, IdBot, t);
            sky.SetColor(IdLit, Color.Lerp(sky.GetColor(IdLit), wroteC, 0.8f * wf));
            sky.SetColor(IdShadow, Color.Lerp(sky.GetColor(IdShadow), wroteC, 0.8f * wf));
            sky.SetColor(IdSunset, Color.Lerp(sky.GetColor(IdSunset), wroteC, 0.8f * wf));
            sky.SetFloat(IdCover, Mathf.Lerp(sky.GetFloat(IdCover), 0.85f, 0.6f * wf));
        }
        if (wr > 0.001f)
        {
            float t = 0.85f * wr;
            RainBlend(sky, IdTop, t); RainBlend(sky, IdMid, t); RainBlend(sky, IdHor, t); RainBlend(sky, IdBot, t);
            Color rainFog = wroteC * 0.95f; rainFog.a = 1f;                  // horizon = fog colour, so the mountains fade into the sky instead of standing out pale
            sky.SetColor(IdHor, Color.Lerp(sky.GetColor(IdHor), rainFog, 0.85f * wr));
            sky.SetColor(IdBot, Color.Lerp(sky.GetColor(IdBot), rainFog, 0.85f * wr));
            float dk = 1f - 0.4f * ws;
            sky.SetColor(IdLit, Color.Lerp(sky.GetColor(IdLit), new Color(0.07f * dk, 0.075f * dk, 0.09f * dk, 1f), 0.92f * wr));
            sky.SetColor(IdShadow, Color.Lerp(sky.GetColor(IdShadow), new Color(0.02f * dk, 0.022f * dk, 0.03f * dk, 1f), 0.92f * wr));
            sky.SetColor(IdSunset, Color.Lerp(sky.GetColor(IdSunset), new Color(0.05f * dk, 0.05f * dk, 0.06f * dk, 1f), 0.92f * wr));
            sky.SetFloat(IdCover, Mathf.Lerp(sky.GetFloat(IdCover), 0.95f, wr));
            sky.SetFloat(IdDensity, Mathf.Lerp(sky.GetFloat(IdDensity), 1.6f, wr));
        }
        if (ws > 0.001f && wr > 0.001f)                                      // thunderstorm: black, heavy cloud deck even at midday
        {
            float s = ws * Mathf.Clamp01(wr / Mathf.Max(0.05f, intensityCap));
            Color blk = new Color(0.012f, 0.013f, 0.018f, 1f);
            sky.SetColor(IdTop, Color.Lerp(sky.GetColor(IdTop), blk, 0.9f * s));
            sky.SetColor(IdMid, Color.Lerp(sky.GetColor(IdMid), blk * 1.3f, 0.88f * s));
            sky.SetColor(IdHor, Color.Lerp(sky.GetColor(IdHor), wroteC * 0.9f, 0.95f * s));
            sky.SetColor(IdBot, Color.Lerp(sky.GetColor(IdBot), wroteC * 0.9f, 0.95f * s));
            sky.SetColor(IdLit, Color.Lerp(sky.GetColor(IdLit), blk * 1.6f, 0.95f * s));
            sky.SetColor(IdShadow, Color.Lerp(sky.GetColor(IdShadow), blk * 0.5f, 0.95f * s));
            sky.SetColor(IdSunset, Color.Lerp(sky.GetColor(IdSunset), blk, 0.95f * s));
            sky.SetFloat(IdCover, Mathf.Lerp(sky.GetFloat(IdCover), 1f, s));
            sky.SetFloat(IdDensity, Mathf.Lerp(sky.GetFloat(IdDensity), 1.7f, s));   // high density amplified the cloud noise into grain
        }
        if (F > 0.01f)                                                       // clouds and sky light up with the strike
        {
            sky.SetColor(IdLit, Color.Lerp(sky.GetColor(IdLit), new Color(0.9f, 0.93f, 1f, 1f), 0.75f * F));
            sky.SetColor(IdShadow, Color.Lerp(sky.GetColor(IdShadow), new Color(0.5f, 0.55f, 0.72f, 1f), 0.6f * F));
            Color sk = new Color(0.5f, 0.56f, 0.75f, 1f);
            sky.SetColor(IdTop, Color.Lerp(sky.GetColor(IdTop), sk, 0.35f * F));
            sky.SetColor(IdMid, Color.Lerp(sky.GetColor(IdMid), sk, 0.4f * F));
            sky.SetColor(IdHor, Color.Lerp(sky.GetColor(IdHor), sk, 0.4f * F));
        }
    }

    void FogBlend(Material sky, int id, float t)
    {
        sky.SetColor(id, Color.Lerp(sky.GetColor(id), wroteC, t));
    }

    void RainBlend(Material sky, int id, float t)
    {
        Color c = sky.GetColor(id);
        float g = c.grayscale;
        sky.SetColor(id, Color.Lerp(c, new Color(g * 0.42f, g * 0.45f, g * 0.5f, c.a), t));
    }

    void Restore()
    {
        if (hasWritten)
        {
            if (RenderSettings.fogDensity == wroteD) RenderSettings.fogDensity = baseD;
            if (!Differs(RenderSettings.fogColor, wroteC)) RenderSettings.fogColor = baseC;
            if (!Differs(RenderSettings.ambientLight, wroteA)) RenderSettings.ambientLight = baseA;
            if (sun != null && sun.intensity == sunWrote) sun.intensity = sunBase;
            sunBase = 0f; sunWrote = 0f;
            hasWritten = false;
        }
        if (atm != null && rayBase > 0f) { atm.rayStrength = rayBase; rayBase = 0f; }
        if (valley != null && valleyBase >= 0f) { valley.opacity = valleyBase; valleyBase = -1f; }
    }

    void UpdateLightning()
    {
        bool storm = lightning && !suppressed && weather == WeatherType.Thunderstorm && stormW > 0.55f && rainW > 0.5f * intensityCap;
        if (lightningSys == null)
        {
            if (!storm) return;
            lightningSys = new GameObject("WeatherLightning").AddComponent<WeatherLightning>();
            lightningSys.transform.SetParent(transform, false);
        }
        if (storm && lightningSys.thunderClip == null)
        {
            LoadClips();
            lightningSys.thunderClip = thunderClip;
            if (clipsTried && thunderClip == null) Debug.LogWarning("WeatherController: Assets/Audio/Weather/thunder.mp3 not in the sound catalog. Run Tools > Audio > Rebuild Sound Catalog.");
        }
        lightningSys.Tick(storm, cam);
    }

    void LoadClips()
    {
        if (clipsTried || Time.unscaledTime < nextClipTry) return;
        nextClipTry = Time.unscaledTime + 3f;
        SoundCatalog cat = SoundCatalog.Instance;
        if (cat == null) return;
        clipsTried = true;
        rainClip = Pick(cat, "rain");
        thunderClip = Pick(cat, "thunder");
        if (rainClip == null) Debug.LogWarning("WeatherController: Assets/Audio/Weather/rain.mp3 not in the sound catalog. Run Tools > Audio > Rebuild Sound Catalog.");
    }

    static AudioClip Pick(SoundCatalog cat, string name)
    {
        System.Collections.Generic.List<SoundEntry> l = cat.Query("weather", null, name);
        if (l.Count == 0) return null;
        for (int i = 0; i < l.Count; i++)
            if (l[i].clip.name.ToLowerInvariant() == name) return l[i].clip;
        return l[0].clip;
    }

    void UpdateAudio(bool rainWanted)
    {
        float rate = suppressed ? 0.7f : Mathf.Max(audioFadeSeconds, fade * 0.9f);
        audioLevel = Mathf.MoveTowards(audioLevel, rainWanted ? 1f : 0f, Time.unscaledDeltaTime / rate);
        if (audioLevel > 0.001f)
        {
            LoadClips();
            if (rainClip != null && rainLoop == null)
            {
                if (rainClip.loadState == AudioDataLoadState.Unloaded) rainClip.LoadAudioData();
                if (rainClip.loadState != AudioDataLoadState.Loading && rainClip.loadState != AudioDataLoadState.Unloaded)
                    rainLoop = new SeamlessLoop(transform, rainClip);
            }
            if (rainLoop != null)
            {
                float sm = audioLevel * audioLevel * (3f - 2f * audioLevel);
                float vol = Mathf.Min(1f, rainVolume * (1f + 0.35f * stormW)) * sm * AudioDirector.Volume("ambient");
                if (!rainLoop.Playing) rainLoop.Start(vol);
                rainLoop.Update(vol);
            }
            AudioDirector.SetVolume("animals", Mathf.Lerp(1f, animalDuck, audioLevel));
            ducked = true;
        }
        else
        {
            audioLevel = 0f;
            if (rainLoop != null && rainLoop.Playing) rainLoop.Stop();
            if (ducked) { AudioDirector.SetVolume("animals", 1f); ducked = false; }
        }
    }

    void EnsureRain()
    {
        if (rain != null) return;
        GameObject go = new GameObject("WeatherRain");
        go.transform.SetParent(transform, false);
        rain = go.AddComponent<ParticleSystem>();
        rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = rain.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = RainLife;
        main.startSpeed = 0f;
        main.startSize = 0.035f;
        main.gravityModifier = 0f;
        main.maxParticles = Mathf.RoundToInt(maxRainParticles * 1.6f);

        ParticleSystem.ShapeModule shape = rain.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(28f, 0.5f, 28f);

        ParticleSystem.VelocityOverLifetimeModule vel = rain.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = Wind.Dir.x * 2f;
        vel.y = -24f;
        vel.z = Wind.Dir.y * 2f;

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        rainMat = new Material(sh);

        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.025f;
        pr.lengthScale = 1.5f;
        pr.sharedMaterial = rainMat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
    }

    void UpdateRain(float w, float ws)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        EnsureRain();
        Camera rainCam = PhotoMode.FreeCamera;               // follow the freecam while photo mode is on, the player cam otherwise
        if (rainCam == null) rainCam = cam;
        rain.transform.position = rainCam.transform.position + Vector3.up * 10f;

        if (Time.unscaledTime >= nextTint)
        {
            nextTint = Time.unscaledTime + 0.25f;
            float k = Mathf.Lerp(1f, 0.45f, StylizedVehiclePainter.CurrentNight);
            rainMat.color = new Color(0.78f * k, 0.86f * k, k, 0.4f + 0.12f * ws);
            ParticleSystem.MainModule mm = rain.main;
            mm.startSize = 0.035f * (1f + 0.4f * ws);
            ParticleSystem.VelocityOverLifetimeModule vm = rain.velocityOverLifetime;
            vm.x = Wind.Dir.x * 2f * (1f + 2f * ws);
            vm.y = -24f - 6f * ws;
            vm.z = Wind.Dir.y * 2f * (1f + 2f * ws);
        }

        ParticleSystem.EmissionModule em = rain.emission;
        em.rateOverTime = maxRainParticles / RainLife * w * (1f + 0.6f * ws);
        if (!rain.isPlaying) rain.Play();
    }

    void StopRain()
    {
        if (rain != null && rain.isEmitting) rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
