using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

[DisallowMultipleComponent]
[AddComponentMenu("Procedural World/World Generator")]
public class WorldGenerator : MonoBehaviour
{
    public WorldSettings settings = new WorldSettings();
    public bool generateOnPlay = false;
    public bool showOccupancy = true;
    public bool showClipIssues = true;
    public bool combineMeshes = true;
    public float combineChunkSize = 128f;

    public WorldContext Context { get; private set; }
    public float Progress { get; private set; }
    public string CurrentStage { get; private set; }
    public bool LastGenerateOk { get; private set; }

    void Start()
    {
        if (generateOnPlay && Application.isPlaying) StartCoroutine(GenerateRoutine());
    }

    void OnValidate()
    {
        settings.worldSize = Mathf.Max(64f, settings.worldSize);
        settings.maxTerrainHeight = Mathf.Max(1f, settings.maxTerrainHeight);
        int res = Mathf.Clamp(settings.heightmapResolution, 129, 1025);
        settings.heightmapResolution = Mathf.ClosestPowerOfTwo(res - 1) + 1;
    }

    public void NewSeed()
    {
        settings.seed = UnityEngine.Random.Range(1, int.MaxValue);
    }

    public WorldStage[] GetOrderedStages()
    {
        WorldStage[] stages = GetComponents<WorldStage>();
        Array.Sort(stages, (a, b) => a.Order.CompareTo(b.Order));
        return stages;
    }

    public void Generate()
    {
        IEnumerator routine = GenerateRoutine();
        while (routine.MoveNext()) { }
    }

    public IEnumerator GenerateRoutine()
    {
        EnsureStylizedWorldSetup();
        LastGenerateOk = false;
        Progress = 0f;
        CurrentStage = "starting";
        Clear();
        GameObject rootGo = new GameObject("World");
        rootGo.transform.SetParent(transform, false);
        Context = new WorldContext(settings, rootGo.transform, this);
        MaterialFactory.Reset(settings.defaultMaterial);

        Stopwatch total = Stopwatch.StartNew();
        if (GetComponent<RoadVergeStage>() == null) gameObject.AddComponent<RoadVergeStage>();
        if (GetComponent<ForestMistStage>() == null) gameObject.AddComponent<ForestMistStage>();
        if (GetComponent<ValleyFogStage>() == null) gameObject.AddComponent<ValleyFogStage>();
        if (GetComponent<SurfaceDetailStage>() == null) gameObject.AddComponent<SurfaceDetailStage>();
        if (GetComponent<PondStage>() == null) gameObject.AddComponent<PondStage>();
        if (GetComponent<AmbientLifeStage>() == null) gameObject.AddComponent<AmbientLifeStage>();
        if (GetComponent<CinematicOutlineStage>() == null) gameObject.AddComponent<CinematicOutlineStage>();
        if (GetComponent<WindowLightStage>() == null) gameObject.AddComponent<WindowLightStage>();
        if (GetComponent<CityBorderStage>() == null) gameObject.AddComponent<CityBorderStage>();
        if (GetComponent<FloraStage>() == null) gameObject.AddComponent<FloraStage>();
        if (GetComponent<CityPlazaStage>() == null) gameObject.AddComponent<CityPlazaStage>();
        if (GetComponent<BuildingDetailStage>() == null) gameObject.AddComponent<BuildingDetailStage>();
        if (GetComponent<RooftopPropStage>() == null) gameObject.AddComponent<RooftopPropStage>();
        if (GetComponent<InterviewDoorStage>() == null) gameObject.AddComponent<InterviewDoorStage>();
        WorldStage[] stages = GetOrderedStages();
        for (int i = 0; i < stages.Length; i++)
        {
            WorldStage stage = stages[i];
            if (stage == null || !stage.stageEnabled) continue;
            CurrentStage = stage.StageName;
            Progress = (float)i / stages.Length;
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                stage.Run(Context);
            }
            catch (Exception e)
            {
                Debug.LogError("World stage '" + stage.StageName + "' failed: " + e);
                yield break;
            }
            Debug.Log("World stage '" + stage.StageName + "' done in " + sw.ElapsedMilliseconds + " ms");
            Progress = (float)(i + 1) / stages.Length;
            yield return null;
        }
        if (combineMeshes)
        {
            CurrentStage = "combining meshes";
            yield return null;
            Stopwatch cs = Stopwatch.StartNew();
            WorldCombiner.Run(Context, combineChunkSize);
            Debug.Log("World combine done in " + cs.ElapsedMilliseconds + " ms");
        }
        Progress = 1f;
        LastGenerateOk = true;
        // Shadows used to be set up only by WorldBootstrap / MenuWorldBootstrap, so a world generated in the editor
        // (Scene view, Play on the scene directly, Bake) had none. Do it here as well.
        if (!Application.isPlaying)
        {
            try { WorldShadowFix.ApplyNow(); }
            catch (Exception e) { Debug.LogWarning("World shadows could not be applied: " + e.Message); }
        }
        else StartCoroutine(ShadowFallback());
        Debug.Log("World generated in " + total.ElapsedMilliseconds + " ms, seed " + settings.seed + ", objects reserved: " + Context.occupancy.Records.Count);
    }

    // Play mode: if no bootstrap runs the shadow pass shortly after generation (scene played directly), run it here.
    IEnumerator ShadowFallback()
    {
        int before = WorldShadowFix.ApplyCount;
        yield return new WaitForSecondsRealtime(4f);
        if (WorldShadowFix.ApplyCount != before) yield break;
        yield return WorldShadowFix.Apply();
        StartCoroutine(WorldShadowFix.Followup());
    }

    void EnsureStylizedWorldSetup()
    {
        StylizedWorldSetup style = GetComponent<StylizedWorldSetup>();
        if (style == null) style = gameObject.AddComponent<StylizedWorldSetup>();
        style.ApplyNow();
    }

    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith("World")) continue;
            child.name = "World(destroyed)";
            WorldUtil.Kill(child.gameObject);
        }
        Context = null;
        StreetLampLights.Clear();
        MaterialFactory.Reset(settings.defaultMaterial);
    }

    void OnDrawGizmosSelected()
    {
        if (Context == null) return;
        if (showOccupancy) WorldGizmos.DrawOccupancy(Context);
        ClipValidator v = GetComponent<ClipValidator>();
        if (showClipIssues && v != null) WorldGizmos.DrawIssues(v.LastReport);
    }
}
