using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Baked-world night support. The generator keeps window / lamp glow materials and street-lamp positions in static, runtime-only
/// registries; a baked scene would lose them (dark windows, dead lamps at night). StartMenuBaker snapshots them here and this
/// component re-registers them at Play. It also gives the menu long-range shadows (the camera is far from the town) and caps the
/// fog so the valley stays readable at night. Everything is restored when the menu scene unloads.
/// </summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public class MenuNightData : MonoBehaviour
{
    [Header("Captured by Bake World")]
    public Material[] glowMaterials = new Material[0];
    public Color[] glowColors = new Color[0];
    public float[] glowStrength = new float[0];
    public float[] glowMax = new float[0];
    public Vector3[] lampPositions = new Vector3[0];
    public float[] lampRanges = new float[0];

    [Header("Menu look")]
    public float shadowDistance = 900f;
    public int shadowCascades = 4;
    public int shadowResolution = 4096;
    [Tooltip("Optional upper limit for exponential fog density (0 = off, i.e. same fog as in game).")]
    public float maxFogDensity = 0f;

    UniversalRenderPipelineAsset urp;
    float oldDist; int oldCascades, oldRes; bool saved;

    void Awake()
    {
        for (int i = 0; i < glowMaterials.Length; i++)
            StylizedMaterialFactory.ImportGlow(glowMaterials[i], glowColors[i], glowStrength[i], glowMax[i]);
        for (int i = 0; i < lampPositions.Length; i++)
            StreetLampLights.Register(lampPositions[i], lampRanges[i]);
    }

    void Start()
    {
        urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            oldDist = urp.shadowDistance; oldCascades = urp.shadowCascadeCount; saved = true;
            try { oldRes = (int)urp.mainLightShadowmapResolution; } catch { oldRes = 0; }
            ApplyShadows();
        }
        StartCoroutine(Reapply());
    }

    System.Collections.IEnumerator Reapply()
    {
        // WorldShadowFix only ever raises values, but re-assert once its shadow pass has run
        yield return new WaitForSecondsRealtime(2f);
        ApplyShadows();
    }

    void ApplyShadows()
    {
        if (urp == null) return;
        urp.shadowDistance = shadowDistance;
        urp.shadowCascadeCount = Mathf.Clamp(shadowCascades, 1, 4);
        try { urp.mainLightShadowmapResolution = shadowResolution; } catch { }
        QualitySettings.shadowDistance = shadowDistance;
    }

    void LateUpdate()
    {
        if (maxFogDensity > 0f && RenderSettings.fog && RenderSettings.fogMode != FogMode.Linear && RenderSettings.fogDensity > maxFogDensity)
            RenderSettings.fogDensity = maxFogDensity;
    }

    void OnDestroy()
    {
        if (!saved || urp == null) return;
        urp.shadowDistance = oldDist;
        urp.shadowCascadeCount = oldCascades;
        if (oldRes > 0) try { urp.mainLightShadowmapResolution = oldRes; } catch { }
    }
}
