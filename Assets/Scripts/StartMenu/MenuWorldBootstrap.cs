using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Runs in the StartMenu scene: generates the world with a FIXED seed (same view every launch),
/// starts the traffic/NPC director around the camera focus point, frames the camera.
/// Replaces WorldBootstrap in this scene (the builder tool swaps it automatically).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WorldGenerator))]
[AddComponentMenu("Start Menu/Menu World Bootstrap")]
public class MenuWorldBootstrap : MonoBehaviour
{
    public int fixedSeed = 20260;
    public MenuCameraRig rig;
    public bool showLoadingScreen = true;
    [Tooltip("Set by Tools > Start Menu > Bake World: the world already exists in the scene, nothing is generated at play.")]
    public bool useBakedWorld = false;

    [Header("Menu traffic (all agents near the focus, simulated every frame = smooth)")]
    public int maxCars = 30;
    public int maxPedestrians = 20;
    public int initialCars = 18;
    public int initialPedestrians = 12;
    public float initialRadiusMax = 220f;
    public float spawnRadiusMax = 260f;
    public float activeRadius = 300f;
    public float farSimRadius = 450f;

    public static bool Ready { get; private set; }
    public static WorldContext Context { get; private set; }
    public static event Action BecameReady;

    Transform focus;

    IEnumerator Start()
    {
        Ready = false;
        if (rig == null) rig = FindFirstObjectByType<MenuCameraRig>();
        LoadingScreen.Kind = LoadingScreen.LoadKind.Game;
        LoadingScreen screen = showLoadingScreen ? LoadingScreen.Show() : null;
        if (screen != null) screen.SetProgress(0.05f, "preparing");
        yield return null;

        if (GetComponent<MenuAudio>() == null) gameObject.AddComponent<MenuAudio>();   // 4D: day birds / night forest only
        if (GetComponent<MenuNoGrade>() == null) gameObject.AddComponent<MenuNoGrade>();   // no colour correction in the menu, lens flare only

        if (useBakedWorld)
        {
            if (rig != null && rig.cam != null && !rig.cam.CompareTag("MainCamera")) rig.cam.tag = "MainCamera";
            ActivateCast();
            if (GetComponent<ButterflyVisitor>() == null)   // one butterfly drifts in front of the camera now and then (day only, 60 s gap)
            { var bv = gameObject.AddComponent<ButterflyVisitor>(); bv.scale = 4f; bv.firstDelay = new Vector2(8f, 20f); bv.Initialize(null); }
            if (screen != null) screen.SetProgress(0.5f, "casting shadows");
            yield return WorldShadowFix.Apply(k => { if (screen != null) screen.SetProgress(0.5f + 0.5f * k, "casting shadows"); });
            StartCoroutine(WorldShadowFix.Followup());
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Ready = true;
            if (BecameReady != null) BecameReady();
            if (screen != null) screen.Finish();
            yield break;
        }

        WorldGenerator gen = GetComponent<WorldGenerator>();
        gen.settings.seed = fixedSeed;
        IEnumerator routine = gen.GenerateRoutine();
        while (routine.MoveNext())
        {
            if (screen != null) screen.SetProgress(0.05f + 0.85f * gen.Progress, gen.CurrentStage);
            yield return null;
        }
        if (!gen.LastGenerateOk || gen.Context == null)
        {
            Debug.LogError("MenuWorldBootstrap: world generation failed, see the Console.");
            if (screen != null) screen.Finish();
            yield break;
        }
        Context = gen.Context;

        if (rig != null) rig.Frame(Context);
        Vector3 fp = rig != null && rig.Framed ? rig.FocusPoint : Vector3.zero;
        focus = new GameObject("Menu Focus").transform;
        focus.position = fp;

        if (rig != null && rig.cam != null && !rig.cam.CompareTag("MainCamera")) rig.cam.tag = "MainCamera";

        NpcDirector director = GetComponent<NpcDirector>();
        if (director == null) director = gameObject.AddComponent<NpcDirector>();
        ApplyTuning(director.settings);
        if (director.enabled) director.Begin(Context, focus);

        if (screen != null) screen.SetProgress(0.985f, "casting shadows");
        yield return WorldShadowFix.Apply(k => { if (screen != null) screen.SetProgress(0.985f + 0.012f * k, "casting shadows"); });
        StartCoroutine(WorldShadowFix.Followup());

        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Ready = true;
        if (BecameReady != null) BecameReady();
        if (screen != null) screen.Finish();
    }

    /// <summary>Older bakes left the pooled agents inactive (invisible). Activate every follower under 'Menu Cast'.</summary>
    static void ActivateCast()
    {
        foreach (var f in Resources.FindObjectsOfTypeAll<MenuPathFollower>())
        {
            if (f == null || !f.gameObject.scene.IsValid()) continue;
            if (f.kind == MenuPathFollower.Kind.Walker) { WorldUtil.Kill(f.gameObject); continue; }   // no NPCs in the menu
            if (!f.gameObject.activeSelf) f.gameObject.SetActive(true);
        }
    }

    void ApplyTuning(AiSettings s)
    {
        s.maxCars = maxCars; s.maxPedestrians = maxPedestrians;
        s.initialCars = initialCars; s.initialPedestrians = initialPedestrians;
        s.initialRadiusMax = initialRadiusMax; s.spawnRadiusMax = spawnRadiusMax; s.activeRadius = activeRadius;
        s.car.traffic.farSimRadius = farSimRadius;
    }

    void OnDestroy() { Ready = false; Context = null; }
}
