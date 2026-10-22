using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Applies a coherent, lightweight stylized look to a URP scene.
/// Intended for Unity 6 URP and WebGL: restrained color grading, matte lighting,
/// soft shadows and distance haze; no screen-space outlines or expensive effects.
/// WorldGenerator creates this component automatically if it is missing.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Procedural World/Visual Style/Stylized World Setup")]
public sealed class StylizedWorldSetup : MonoBehaviour
{
    [Header("Global look")]
    public bool applyOnEnable = true;
    public bool enableDistanceFog = true;
    [Range(0f, 0.01f)] public float fogDensity = 0.0014f;
    public Color fogColor = new Color(0.66f, 0.76f, 0.82f, 1f);
    public Color skyAmbient = new Color(0.57f, 0.68f, 0.79f, 1f);
    public Color horizonAmbient = new Color(0.76f, 0.77f, 0.72f, 1f);
    public Color groundAmbient = new Color(0.31f, 0.35f, 0.29f, 1f);

    [Header("Lighting and shadows")]
    [Range(0f, 1f)] public float shadowStrength = 0.78f;
    [Range(20f, 250f)] public float shadowDistance = 115f;
    [Range(0.5f, 1.5f)] public float directionalIntensity = 1.05f;
    public Color sunlightColor = new Color(1f, 0.91f, 0.78f, 1f);

    [Header("URP post processing (lightweight)")]
    [Range(-30f, 30f)] public float contrast = 12f;
    [Range(-30f, 30f)] public float saturation = 8f;
    [Range(0f, 0.25f)] public float vignette = 0.08f;
    [Range(0f, 0.2f)] public float bloom = 0.035f;

    Volume volume;
    VolumeProfile ownedProfile;

    void OnEnable()
    {
        if (applyOnEnable) ApplyNow();
    }

    void OnDestroy()
    {
        if (ownedProfile != null)
        {
            if (Application.isPlaying) WorldUtil.Kill(ownedProfile);
            else DestroyImmediate(ownedProfile);
        }
    }

    [ContextMenu("Apply Stylized World Look Now")]
    public void ApplyNow()
    {
        ApplyEnvironment();
        ApplyLights();
        ApplyCameras();
        ApplyPostProcessing();
    }

    void ApplyEnvironment()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyAmbient;
        RenderSettings.ambientEquatorColor = horizonAmbient;
        RenderSettings.ambientGroundColor = groundAmbient;
        RenderSettings.ambientIntensity = 1.0f;
        RenderSettings.reflectionIntensity = 0.35f;
        RenderSettings.fog = enableDistanceFog;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
    }

    void ApplyLights()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        Light sun = null;
        for (int i = 0; i < lights.Length; i++)
        {
            Light l = lights[i];
            if (l == null || !l.enabled) continue;
            if (l.type == LightType.Directional)
            {
                // Day/night system owns its sun & moon; never override them.
                if (DayNightLightGuard.IsDayNightLight(l)) continue;
                if (sun == null || l.intensity > sun.intensity) sun = l;
                l.color = sunlightColor;
                l.intensity = directionalIntensity;
                l.shadows = LightShadows.Soft;
                l.shadowStrength = shadowStrength;
                l.shadowBias = 0.045f;
                l.shadowNormalBias = 0.32f;
                l.shadowNearPlane = 0.2f;
            }
            else if (l.type == LightType.Point || l.type == LightType.Spot)
            {
                // Keep decorative/local lights restrained for WebGL and avoid harsh pools.
                l.shadows = LightShadows.None;
                l.intensity = Mathf.Min(l.intensity, 1.5f);
            }
        }
        if (sun != null && RenderSettings.sun == null) RenderSettings.sun = sun;
        QualitySettings.shadowDistance = shadowDistance;
        QualitySettings.shadows = UnityEngine.ShadowQuality.All;
        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Medium;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
    }

    void ApplyCameras()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam == null || cam.targetTexture != null) continue;
            UniversalAdditionalCameraData data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None; // predictable cost on WebGL
            data.stopNaN = true;
        }
    }

    void ApplyPostProcessing()
    {
        volume = GetComponent<Volume>();
        if (volume == null) volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 1f;

        if (ownedProfile == null)
        {
            ownedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            ownedProfile.name = "Runtime_StylizedWorld_Profile";
            ownedProfile.hideFlags = HideFlags.DontSave;
        }
        volume.sharedProfile = ownedProfile;

        ColorAdjustments colors = GetOrAdd<ColorAdjustments>(ownedProfile);
        colors.active = true;
        colors.postExposure.overrideState = true;
        colors.postExposure.value = 0.05f;
        colors.contrast.overrideState = true;
        colors.contrast.value = contrast;
        colors.saturation.overrideState = true;
        colors.saturation.value = saturation;
        colors.colorFilter.overrideState = true;
        colors.colorFilter.value = new Color(1f, 0.99f, 0.96f, 1f);

        Tonemapping tone = GetOrAdd<Tonemapping>(ownedProfile);
        tone.active = true;
        tone.mode.overrideState = true;
        tone.mode.value = TonemappingMode.Neutral;

        Vignette vignetteOverride = GetOrAdd<Vignette>(ownedProfile);
        vignetteOverride.active = vignette > 0.001f;
        vignetteOverride.intensity.overrideState = true;
        vignetteOverride.intensity.value = vignette;
        vignetteOverride.smoothness.overrideState = true;
        vignetteOverride.smoothness.value = 0.72f;

        Bloom bloomOverride = GetOrAdd<Bloom>(ownedProfile);
        bloomOverride.active = bloom > 0.001f;
        bloomOverride.threshold.overrideState = true;
        bloomOverride.threshold.value = 1.15f;
        bloomOverride.intensity.overrideState = true;
        bloomOverride.intensity.value = bloom;
        bloomOverride.scatter.overrideState = true;
        bloomOverride.scatter.value = 0.55f;
    }

    static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component;
        if (!profile.TryGet(out component)) component = profile.Add<T>(true);
        return component;
    }
}
