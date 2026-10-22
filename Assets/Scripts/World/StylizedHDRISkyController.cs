using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class StylizedHDRISkyController : MonoBehaviour
{
    private const string SkyShaderName = "Skybox/Stylized HDRI Moving Clouds";
    private static readonly int DayTex = Shader.PropertyToID("_DayTex");
    private static readonly int NightTex = Shader.PropertyToID("_NightTex");
    private static readonly int DawnTex = Shader.PropertyToID("_DawnTex");
    private static readonly int DuskTex = Shader.PropertyToID("_DuskTex");
    private static readonly int Hour = Shader.PropertyToID("_Hour");
    private static readonly int Sunrise = Shader.PropertyToID("_Sunrise");
    private static readonly int Sunset = Shader.PropertyToID("_Sunset");
    private static readonly int CloudOffset = Shader.PropertyToID("_CloudOffset");
    private static readonly int CloudOpacity = Shader.PropertyToID("_CloudOpacity");
    private static readonly int CloudScale = Shader.PropertyToID("_CloudScale");
    private static readonly int CloudCoverage = Shader.PropertyToID("_CloudCoverage");
    private static readonly int CloudDetail = Shader.PropertyToID("_CloudDetail");
    private static readonly int SunDirection = Shader.PropertyToID("_SunDirection");
    private static readonly int MoonDirection = Shader.PropertyToID("_MoonDirection");
    private static readonly int SunBrightness = Shader.PropertyToID("_SunBrightness");
    private static readonly int SunSize = Shader.PropertyToID("_SunSize");
    private static readonly int Exposure = Shader.PropertyToID("_Exposure");
    private static readonly int StarBrightness = Shader.PropertyToID("_StarBrightness");
    private static readonly int StarsEnabled = Shader.PropertyToID("_StarsEnabled");

    public StylizedHDRISkySettings settings;
    [Tooltip("Optional existing directional light to use as the sun. If empty, one is created.")]
    public Light sunLight;
    [Tooltip("Optional existing directional light to use as the moon. If empty, one is created.")]
    public Light moonLight;

    private Material skyMaterial;
    private float hour;
    private float cloudTravel;
    private float nextEnvironmentRefresh;
    private Light ownedSun;
    private Light ownedMoon;
    private static StylizedHDRISkyController instance;

    public float CurrentHour => hour;

    public void SetHour(float value)
    {
        hour = Mathf.Repeat(value, 24f);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            WorldUtil.Kill(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        Initialize();
    }

    private void OnEnable()
    {
        if (settings != null && skyMaterial == null)
            Initialize();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (skyMaterial != null)
        {
            RenderSettings.skybox = RenderSettings.skybox == skyMaterial ? null : RenderSettings.skybox;
            if (Application.isPlaying) WorldUtil.Kill(skyMaterial);
            else DestroyImmediate(skyMaterial);
        }
        if (ownedSun != null) WorldUtil.Kill(ownedSun.gameObject);
        if (ownedMoon != null) WorldUtil.Kill(ownedMoon.gameObject);
    }

    public void Initialize()
    {
        if (settings == null)
        {
            Debug.LogWarning("[Stylized HDRI Sky] No settings asset assigned. Create one via Assets > Create > World > Stylized HDRI Sky Settings, put it under Assets/Resources, and assign your HDRIs.", this);
            return;
        }

        Shader shader = Shader.Find(SkyShaderName);
        if (shader == null)
        {
            Debug.LogError("[Stylized HDRI Sky] Shader not found: " + SkyShaderName + ". Ensure StylizedHDRISky.shader is inside Assets.", this);
            return;
        }

        if (skyMaterial != null)
        {
            if (Application.isPlaying) WorldUtil.Kill(skyMaterial);
            else DestroyImmediate(skyMaterial);
        }

        skyMaterial = new Material(shader) { name = "Runtime Stylized HDRI Sky" };
        skyMaterial.SetTexture(DayTex, settings.dayHDRI != null ? settings.dayHDRI : Texture2D.blackTexture);
        skyMaterial.SetTexture(NightTex, settings.nightHDRI != null ? settings.nightHDRI : (settings.dayHDRI != null ? settings.dayHDRI : Texture2D.blackTexture));
        skyMaterial.SetTexture(DawnTex, settings.dawnHDRI != null ? settings.dawnHDRI : (settings.dayHDRI != null ? settings.dayHDRI : Texture2D.blackTexture));
        skyMaterial.SetTexture(DuskTex, settings.duskHDRI != null ? settings.duskHDRI : (settings.dayHDRI != null ? settings.dayHDRI : Texture2D.blackTexture));
        skyMaterial.SetFloat(Sunrise, settings.sunriseHour);
        skyMaterial.SetFloat(Sunset, settings.sunsetHour);
        skyMaterial.SetFloat(CloudOpacity, settings.enableMovingProceduralClouds ? settings.cloudOpacity : 0f);
        skyMaterial.SetFloat(CloudScale, settings.cloudScale);
        skyMaterial.SetFloat(CloudCoverage, settings.cloudCoverage);
        skyMaterial.SetFloat(CloudDetail, settings.cloudDetail);
        skyMaterial.SetFloat(SunBrightness, settings.sunDiskBrightness);
        skyMaterial.SetFloat(SunSize, settings.sunDiskSize);
        skyMaterial.SetFloat(Exposure, settings.exposure);
        skyMaterial.SetFloat(StarBrightness, settings.starBrightness);
        skyMaterial.SetFloat(StarsEnabled, settings.showProceduralStars ? 1f : 0f);

        RenderSettings.skybox = skyMaterial;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.ambientIntensity = settings.daytimeAmbientIntensity;
        RenderSettings.fog = settings.fogDensity > 0f;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = settings.fogDensity;

        hour = settings.startingHour;
        EnsureLights();
        UpdateSky(0f);
        DynamicGI.UpdateEnvironment();
    }

    private void EnsureLights()
    {
        if (settings.createSunLight && sunLight == null)
        {
            var go = new GameObject("Stylized Sky - Sun Light");
            go.transform.SetParent(transform, false);
            ownedSun = go.AddComponent<Light>();
            sunLight = ownedSun;
        }
        if (sunLight != null)
        {
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1f, 0.91f, 0.76f);
            sunLight.intensity = settings.middaySunIntensity;
            sunLight.shadows = LightShadows.Soft;
        }

        if (settings.createMoonLight && moonLight == null)
        {
            var go = new GameObject("Stylized Sky - Moon Light");
            go.transform.SetParent(transform, false);
            ownedMoon = go.AddComponent<Light>();
            moonLight = ownedMoon;
        }
        if (moonLight != null)
        {
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.48f, 0.62f, 1f);
            moonLight.intensity = settings.moonIntensity;
            moonLight.shadows = LightShadows.None;
        }
    }

    private void Update()
    {
        if (settings == null || skyMaterial == null) return;

        if (settings.runDayNightCycle)
            hour = Mathf.Repeat(hour + Time.deltaTime * 24f / (settings.fullDayLengthMinutes * 60f), 24f);

        cloudTravel += Time.deltaTime * settings.cloudSpeed;
        UpdateSky(Time.deltaTime);

        if (Time.time >= nextEnvironmentRefresh)
        {
            DynamicGI.UpdateEnvironment();
            nextEnvironmentRefresh = Time.time + 20f;
        }
    }

    private void UpdateSky(float deltaTime)
    {
        float sunAngle = ((hour - 6f) / 24f) * 360f;
        float elevation = Mathf.Sin((hour - 6f) / 12f * Mathf.PI);
        float azimuth = ((hour / 24f) * 360f) + 90f;
        Vector3 sunDir = new Vector3(Mathf.Cos(azimuth * Mathf.Deg2Rad), elevation, Mathf.Sin(azimuth * Mathf.Deg2Rad)).normalized;

        if (sunLight != null)
        {
            sunLight.transform.rotation = Quaternion.LookRotation(-sunDir, Vector3.up);
            float daylight = Mathf.Clamp01((elevation + 0.08f) / 0.42f);
            Color warm = Color.Lerp(new Color(1f, 0.34f, 0.20f), new Color(1f, 0.94f, 0.80f), Mathf.Clamp01(elevation * 2.2f + 0.25f));
            sunLight.color = warm;
            sunLight.intensity = settings.middaySunIntensity * daylight;
            sunLight.enabled = daylight > 0.015f;
        }

        if (moonLight != null)
        {
            moonLight.transform.rotation = Quaternion.LookRotation(sunDir, Vector3.up);
            float night = 1f - Mathf.Clamp01((elevation + 0.05f) / 0.25f);
            moonLight.intensity = settings.moonIntensity * night;
            moonLight.enabled = night > 0.03f;
        }

        skyMaterial.SetFloat(Hour, hour);
        skyMaterial.SetVector(CloudOffset, new Vector4(cloudTravel, cloudTravel * 0.17f, 0f, 0f));
        skyMaterial.SetVector(SunDirection, new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
        skyMaterial.SetVector(MoonDirection, new Vector4(-sunDir.x, -sunDir.y, -sunDir.z, 0f));

        float daylightAmount = Mathf.Clamp01((elevation + 0.12f) / 0.42f);
        RenderSettings.ambientIntensity = Mathf.Lerp(settings.nighttimeAmbientIntensity, settings.daytimeAmbientIntensity, daylightAmount);
        if (settings.animateFog)
        {
            RenderSettings.fogColor = Color.Lerp(new Color(0.10f, 0.13f, 0.25f), new Color(0.68f, 0.78f, 0.84f), daylightAmount);
            RenderSettings.fogDensity = settings.fogDensity * Mathf.Lerp(1.35f, 0.75f, daylightAmount);
        }
    }
}
