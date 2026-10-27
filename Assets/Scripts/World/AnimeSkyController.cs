using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Procedural sky, day/night cycle, directional lights and atmospheric fog for Unity 6 URP.
[ExecuteAlways, DisallowMultipleComponent]
public sealed class AnimeSkyController : MonoBehaviour
{
    const int StyleVersion = 6;
    [SerializeField, HideInInspector] int styleVersion;

    [Header("Cycle")]
    [Min(30)] public float dayLengthSeconds = 900f;
    [Range(0,1)] public float timeOfDay = 0.32f; // 0 midnight, .25 sunrise, .5 noon, .75 sunset
    public bool animateInEditMode = false;
    public bool useUnscaledTime = true;

    [Header("Shader")]
    public Shader skyShader;
    public Material skyMaterial;
    public bool assignAsGlobalSkybox = true;

    [Header("Clouds")]
    [Range(0,1)] public float cloudCoverage = 0.4f;
    [Range(1,30)] public float cloudScale = 5.5f;
    [Range(0,1)] public float cloudDetail = 0.6f;
    [Range(0.2f,3f)] public float cloudDensity = 1f;
    [Range(0,1)] public float highCloudAmount = 0.25f;
    [Range(0.2f,2f)] public float cloudShape = 0.85f;
    [Range(0.001f,0.25f)] public float cloudEdgeSoftness = 0.07f;
    public Vector2 cloudSpeed = new Vector2(0.006f,0.002f);
    [Range(0,1)] public float cloudOpacity = 1f;
    [Tooltip("Metres above the camera. Higher + thinner = more distant, less dome-like sky.")]
    public float cloudBaseAltitude = 2600f;
    public float cloudTopAltitude = 3400f;

    [Header("Lighting")]
    public Light sunLight;
    public Light moonLight;
    [Min(0)] public float daySunIntensity = 1.15f;
    [Min(0)] public float nightMoonIntensity = 0.35f;
    public Color dayLightColor = new Color(1f,0.91f,0.78f);
    public Color dawnLightColor = new Color(1f,0.55f,0.38f);
    public Color nightLightColor = new Color(0.36f,0.48f,0.82f);
    [Range(0,2)] public float ambientDayIntensity = 1f;
    [Range(0,1)] public float ambientNightIntensity = 0.45f;

    [Header("Fog")]
    public bool controlFog = true;
    [Min(0)] public float fogDensityDay = 0.00075f;
    [Min(0)] public float fogDensityNight = 0.00095f;
    public Color fogDay = new Color(0.60f,0.76f,0.90f);
    public Color fogDawn = new Color(0.70f,0.45f,0.42f);
    public Color fogNight = new Color(0.060f,0.085f,0.160f);

    [Header("Palette")]
    public Color skyTopDay = new Color(0.06f,0.27f,0.70f);
    public Color skyMiddleDay = new Color(0.20f,0.56f,0.92f);
    public Color skyHorizonDay = new Color(0.70f,0.88f,0.98f);
    public Color skyBottomDay = new Color(0.30f,0.45f,0.62f);
    public Color cloudLit = new Color(1f,0.985f,0.95f);
    public Color cloudShadow = new Color(0.56f,0.70f,0.94f);
    public Color cloudSunset = new Color(1f,0.62f,0.50f);

    static readonly int Top=Shader.PropertyToID("_SkyColorTop"), Middle=Shader.PropertyToID("_SkyColorMiddle"),
        Horizon=Shader.PropertyToID("_SkyColorHorizon"), Bottom=Shader.PropertyToID("_SkyColorBottom"),
        Lit=Shader.PropertyToID("_CloudColorLit"), Shadow=Shader.PropertyToID("_CloudColorShadow"),
        Sunset=Shader.PropertyToID("_CloudColorSunset"), Coverage=Shader.PropertyToID("_CloudCoverage"),
        Scale=Shader.PropertyToID("_CloudScale"), Detail=Shader.PropertyToID("_CloudDetail"),
        Density=Shader.PropertyToID("_CloudDensity"),
        HighCloudAmount=Shader.PropertyToID("_HighCloudAmount"), CloudShape=Shader.PropertyToID("_CloudShape"),
        Edge=Shader.PropertyToID("_CloudEdgeSoftness"), Speed=Shader.PropertyToID("_CloudSpeed"),
        Opacity=Shader.PropertyToID("_CloudOpacity"), CloudBase=Shader.PropertyToID("_CloudBase"), CloudTop=Shader.PropertyToID("_CloudTop"), SunDir=Shader.PropertyToID("_SunDirection"),
        MoonDir=Shader.PropertyToID("_MoonDirection"), SunCol=Shader.PropertyToID("_SunColor"),
        MoonCol=Shader.PropertyToID("_MoonColor"), SunPower=Shader.PropertyToID("_SunIntensity"),
        Night=Shader.PropertyToID("_NightFactor"), Tod=Shader.PropertyToID("_TimeOfDay"),
        Atmosphere=Shader.PropertyToID("_AtmosphereStrength");

    bool initialized;

    // Proper GLSL/HLSL-style smoothstep. (Mathf.SmoothStep is a lerp between two values,
    // which previously left "night" at ~0.68 at noon and made daytime clouds dark.)
    static float Sstep(float a, float b, float x)
    {
        x = Mathf.Clamp01((x - a) / (b - a));
        return x * x * (3f - 2f * x);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureController()
    {
        if (FindFirstObjectByType<AnimeSkyController>() != null) return;
        var go = new GameObject("Anime Sky System (Auto)");
        go.AddComponent<AnimeSkyController>();
        DontDestroyOnLoad(go);
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Anime Sky/Create or Select Controller")]
    static void CreateOrSelect()
    {
        var c = FindFirstObjectByType<AnimeSkyController>();
        if (c == null) c = new GameObject("Anime Sky System").AddComponent<AnimeSkyController>();
        Selection.activeGameObject = c.gameObject;
    }
#endif

    void OnEnable() { Initialize(); ApplySky(); }
    void OnValidate()
    {
        dayLengthSeconds = Mathf.Max(30,dayLengthSeconds);
        if (isActiveAndEnabled) { Initialize(); ApplySky(); }
    }

    void Update()
    {
        if (!Application.isPlaying && !animateInEditMode) { ApplySky(); return; }
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (Application.isPlaying) timeOfDay = Mathf.Repeat(timeOfDay + dt/dayLengthSeconds,1f);
        ApplySky();
    }

    /// Existing scenes keep old serialized values, so the new look is applied once automatically.
    void UpgradeStyleIfNeeded()
    {
        if (styleVersion >= StyleVersion) return;
        cloudCoverage=0.4f; cloudScale=5.5f; cloudDetail=0.6f; cloudDensity=1f; highCloudAmount=0.25f;
        cloudShape=0.85f; cloudEdgeSoftness=0.07f; cloudSpeed=new Vector2(0.006f,0.002f); cloudOpacity=1f;
        cloudBaseAltitude=2600f; cloudTopAltitude=3400f;
        skyTopDay=new Color(0.06f,0.27f,0.70f); skyMiddleDay=new Color(0.20f,0.56f,0.92f);
        skyHorizonDay=new Color(0.70f,0.88f,0.98f); skyBottomDay=new Color(0.30f,0.45f,0.62f);
        cloudLit=new Color(1f,0.985f,0.95f); cloudShadow=new Color(0.56f,0.70f,0.94f); cloudSunset=new Color(1f,0.62f,0.50f);
        fogDay=new Color(0.60f,0.76f,0.90f); fogDawn=new Color(0.70f,0.45f,0.42f);
        // v6: night readability (moon/ambient lift, thinner and lighter night fog). Only these fields are reset.
        nightMoonIntensity=0.35f; nightLightColor=new Color(0.36f,0.48f,0.82f);
        ambientNightIntensity=0.45f; fogDensityNight=0.00095f; fogNight=new Color(0.060f,0.085f,0.160f);
        styleVersion = StyleVersion;
    }

    void Initialize()
    {
        UpgradeStyleIfNeeded();
        if (initialized && skyMaterial != null) return;
        if (skyShader == null) skyShader = Shader.Find("Custom/AnimeProceduralSkybox");
        if (skyMaterial == null && skyShader != null)
        {
            skyMaterial = new Material(skyShader) { name="Anime Procedural Sky (Runtime)",
                hideFlags=Application.isPlaying ? HideFlags.DontSave : HideFlags.HideAndDontSave };
        }
        if (sunLight == null) sunLight = FindDirectionalLight();
        if (sunLight == null)
        {
            var go = new GameObject("Anime Sky Sun Light");
            sunLight = go.AddComponent<Light>(); sunLight.type=LightType.Directional;
            if (Application.isPlaying) DontDestroyOnLoad(go);
        }
        if (moonLight == null)
        {
            var go = new GameObject("Anime Sky Moon Light");
            moonLight = go.AddComponent<Light>(); moonLight.type=LightType.Directional;
            moonLight.color=nightLightColor; moonLight.intensity=0;
            if (Application.isPlaying) DontDestroyOnLoad(go);
        }
        initialized = skyMaterial != null;
    }

    static Light FindDirectionalLight()
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l != null && l.type==LightType.Directional) return l;
        return null;
    }

    void ApplySky()
    {
        if (skyMaterial == null) return;
        if (assignAsGlobalSkybox && RenderSettings.skybox != skyMaterial) RenderSettings.skybox=skyMaterial;

        float angle=(timeOfDay*360f-90f)*Mathf.Deg2Rad;
        Vector3 sunDir=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0.18f).normalized;
        Vector3 moonDir=-sunDir;
        float daylight=Mathf.Clamp01((sunDir.y+0.12f)/0.45f);
        float night=1f-Sstep(0.02f,0.32f,daylight);
        float dawn=Mathf.Clamp01(1f-Mathf.Abs(sunDir.y)/0.38f)*(1f-night);

        Color top=Color.Lerp(new Color(.012f,.028f,.075f),skyTopDay,daylight);
        Color mid=Color.Lerp(new Color(.04f,.07f,.15f),skyMiddleDay,daylight);
        Color hor=Color.Lerp(new Color(.09f,.12f,.22f),skyHorizonDay,daylight);
        Color bot=Color.Lerp(new Color(.025f,.035f,.08f),skyBottomDay,daylight);
        mid=Color.Lerp(mid,new Color(.80f,.46f,.62f),dawn*.45f);
        hor=Color.Lerp(hor,new Color(1f,.66f,.46f),dawn*.70f);
        top=Color.Lerp(top,new Color(.24f,.26f,.58f),dawn*.30f);

        // Clouds: bright white/soft-blue in daytime, warm pink at dawn/dusk, nearly sky-dark at night.
        Color litC=Color.Lerp(cloudLit,cloudSunset,dawn*.55f);
        Color shC=Color.Lerp(cloudShadow,new Color(.60f,.48f,.78f),dawn*.55f);
        litC=Color.Lerp(litC,new Color(.15f,.19f,.32f),night);
        shC=Color.Lerp(shC,new Color(.035f,.05f,.10f),night);

        skyMaterial.SetColor(Top,top); skyMaterial.SetColor(Middle,mid);
        skyMaterial.SetColor(Horizon,hor); skyMaterial.SetColor(Bottom,bot);
        skyMaterial.SetColor(Lit,litC); skyMaterial.SetColor(Shadow,shC);
        skyMaterial.SetColor(Sunset,Color.Lerp(cloudSunset,new Color(.42f,.28f,.50f),night));
        skyMaterial.SetFloat(Coverage,cloudCoverage); skyMaterial.SetFloat(Scale,cloudScale);
        skyMaterial.SetFloat(Detail,cloudDetail); skyMaterial.SetFloat(Density,cloudDensity);
        skyMaterial.SetFloat(Edge,cloudEdgeSoftness);
        skyMaterial.SetFloat(HighCloudAmount,highCloudAmount);
        skyMaterial.SetFloat(CloudShape,cloudShape);
        skyMaterial.SetVector(Speed,new Vector4(cloudSpeed.x,cloudSpeed.y,0,0));
        skyMaterial.SetFloat(Opacity,cloudOpacity);
        skyMaterial.SetFloat(CloudBase,cloudBaseAltitude); skyMaterial.SetFloat(CloudTop,Mathf.Max(cloudTopAltitude,cloudBaseAltitude+50f));
        skyMaterial.SetVector(SunDir,sunDir); skyMaterial.SetVector(MoonDir,moonDir);
        skyMaterial.SetColor(SunCol,Color.Lerp(new Color(1f,.42f,.24f),new Color(1f,.91f,.70f),daylight));
        skyMaterial.SetColor(MoonCol,new Color(.72f,.83f,1f));
        skyMaterial.SetFloat(SunPower,Mathf.Lerp(.15f,1.45f,daylight));
        skyMaterial.SetFloat(Night,night); skyMaterial.SetFloat(Tod,timeOfDay);
        skyMaterial.SetFloat(Atmosphere,Mathf.Lerp(.18f,.55f,daylight));

        if (sunLight != null)
        {
            sunLight.transform.rotation=Quaternion.LookRotation(-sunDir,Vector3.up);
            sunLight.color=Color.Lerp(dawnLightColor,dayLightColor,daylight);
            sunLight.intensity=daySunIntensity*daylight; sunLight.enabled=daylight>.015f;
        }
        if (moonLight != null)
        {
            moonLight.transform.rotation=Quaternion.LookRotation(-moonDir,Vector3.up);
            moonLight.color=nightLightColor; moonLight.intensity=nightMoonIntensity*night;
            moonLight.enabled=night>.02f;
        }
        RenderSettings.ambientMode=AmbientMode.Flat;
        Color amb=Color.Lerp(new Color(.13f,.16f,.30f),new Color(.54f,.61f,.70f),daylight);
        amb=Color.Lerp(amb,new Color(.40f,.38f,.62f),dawn*.30f); // cool blue-violet shadows at dawn/dusk
        RenderSettings.ambientLight=amb;
        RenderSettings.ambientIntensity=Mathf.Lerp(ambientNightIntensity,ambientDayIntensity,daylight);
        if (controlFog)
        {
            RenderSettings.fog=true; RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogDensity=Mathf.Lerp(fogDensityNight,fogDensityDay,daylight);
            RenderSettings.fogColor=Color.Lerp(Color.Lerp(fogNight,fogDay,daylight),fogDawn,dawn*.75f);
        }
    }
}
