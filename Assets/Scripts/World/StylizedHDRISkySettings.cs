using UnityEngine;

[CreateAssetMenu(fileName = "StylizedHDRISkySettings", menuName = "World/Stylized HDRI Sky Settings")]
public class StylizedHDRISkySettings : ScriptableObject
{
    [Header("Environment maps (equirectangular panoramas recommended)")]
    public Texture2D dayHDRI;
    public Texture2D nightHDRI;
    public Texture2D dawnHDRI;
    public Texture2D duskHDRI;

    [Header("Cycle")]
    [Range(0f, 24f)] public float startingHour = 10f;
    public bool runDayNightCycle = true;
    [Min(1f)] public float fullDayLengthMinutes = 12f;
    [Range(0f, 24f)] public float sunriseHour = 6f;
    [Range(0f, 24f)] public float sunsetHour = 18f;

    [Header("Cloud layer")]
    public bool enableMovingProceduralClouds = true;
    [Range(0f, 1f)] public float cloudOpacity = 0.42f;
    [Range(0.1f, 8f)] public float cloudScale = 2.2f;
    [Range(0f, 1f)] public float cloudCoverage = 0.48f;
    [Range(0f, 0.2f)] public float cloudSpeed = 0.012f;
    [Range(0f, 1f)] public float cloudDetail = 0.45f;

    [Header("Sun and moon")]
    public bool createSunLight = true;
    public bool createMoonLight = true;
    [Range(0f, 3f)] public float middaySunIntensity = 1.25f;
    [Range(0f, 1f)] public float moonIntensity = 0.08f;
    [Range(0f, 2f)] public float sunDiskBrightness = 1f;
    [Range(0.1f, 5f)] public float sunDiskSize = 1.1f;

    [Header("Ambient lighting")]
    [Range(0f, 2f)] public float daytimeAmbientIntensity = 1f;
    [Range(0f, 2f)] public float nighttimeAmbientIntensity = 0.35f;
    [Range(0f, 1f)] public float exposure = 1f;
    [Range(0f, 0.08f)] public float fogDensity = 0.008f;
    public bool animateFog = true;

    [Header("Optional stars")]
    public bool showProceduralStars = true;
    [Range(0f, 2f)] public float starBrightness = 0.8f;
}
