using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
[AddComponentMenu("Procedural World/NPC Director")]
public class NpcDirector : MonoBehaviour
{
    public static NpcDirector Instance { get; private set; }

    public AiSettings settings = new AiSettings();

    public WorldContext Ctx { get; private set; }
    public Transform Player { get; private set; }
    public Camera PlayerCamera { get; private set; }
    public AgentRegistry Registry { get; private set; }
    public AgentPool Pool { get; private set; }
    public CrosswalkRegistry Crosswalks { get; private set; }
    public PedestrianGraph PedGraph { get; private set; }
    public PedestrianSpawner Pedestrians { get; private set; }
    public LaneGraph LaneGraph { get; private set; }
    public TrafficSpawner Traffic { get; private set; }
    public WorldEventDirector Events { get; private set; }
    public System.Random Rng { get; private set; }
    public InteractionSystem Interaction { get; private set; }
    public bool Running { get; private set; }
    public float KeepRadius { get; private set; }

    public event Action Began;
    public event Action Stopped;

    GameObject hud;
    static readonly RaycastHit[] groundHits = new RaycastHit[16];

    void OnValidate()
    {
        if (settings != null) settings.Sanitize();
    }

    void OnDestroy()
    {
        StopAndClear();
    }

    public void Begin(WorldContext ctx, Transform player)
    {
        if (ctx == null || player == null)
        {
            Debug.LogWarning("NpcDirector.Begin needs a world context and a player.");
            return;
        }
        StopAndClear();
        settings.Sanitize();
        Instance = this;
        Ctx = ctx;
        Player = player;
        PlayerCamera = FindCamera();
        float keep = settings.neverDespawnWithin;
        if (ctx.zoneMap != null) keep = Mathf.Max(keep, ctx.zoneMap.suburbRadius * 2f * settings.despawnSuburbFraction);
        KeepRadius = Mathf.Min(keep, settings.activeRadius - 10f);
        Registry = new AgentRegistry(8f);
        Crosswalks = new CrosswalkRegistry();
        Rng = new WorldRng(ctx.settings.seed, "ai").Raw;
        HornBank.Reset();
        TrafficMood.Reset();
        PedGraph = PedestrianGraph.Build(ctx, Crosswalks);
        Pedestrians = new PedestrianSpawner(this, PedGraph);
        LaneGraph = LaneGraph.Build(ctx, PedGraph, Crosswalks);
        Traffic = new TrafficSpawner(this, LaneGraph);
        Pool = new AgentPool();
        Pool.Init(this);
        Pool.Prewarm(settings.prewarmPedestrians, settings.prewarmCars);
        hud = new GameObject("AI HUD");
        hud.transform.SetParent(transform, false);
        Interaction = hud.AddComponent<InteractionSystem>();
        hud.AddComponent<TrafficDebug>();
        // Initialize the unified inventory before binding the world-drop manager. This makes
        // dropped inventory items register with the same interaction prompt used by events.
        InterviewInventoryBridge.EnsureInitialized();
        Items.WorldItemManager.Ensure();
        Running = true;
        Events = new WorldEventDirector(this);
        Events.Build();
        Pedestrians.Populate(settings.initialPedestrians, settings.initialRadiusMin, settings.initialRadiusMax);
        Traffic.Populate(settings.initialCars, settings.initialRadiusMin, settings.initialRadiusMax);
        if (Began != null) Began();
        if (settings.logEvents) Debug.Log("NpcDirector started, seed " + ctx.settings.seed);
    }

    /// <summary>Edit-mode only (Start Menu baker): gives a temporary director what AgentPool.Build needs, without starting the simulation.</summary>
    public void PrepareForBake(WorldContext ctx)
    {
        settings.Sanitize();
        Ctx = ctx;
        Rng = new WorldRng(ctx.settings.seed, "cast").Raw;
    }

    public void StopAndClear()
    {
        bool was = Running;
        Running = false;
        if (Pool != null)
        {
            Pool.Clear();
            Pool = null;
        }
        if (hud != null)
        {
            WorldUtil.Kill(hud);
            hud = null;
        }
        Interaction = null;
        if (Events != null) Events.Clear();
        Events = null;
        Pedestrians = null;
        PedGraph = null;
        Traffic = null;
        LaneGraph = null;
        if (Registry != null) Registry.Clear();
        if (Crosswalks != null) Crosswalks.Reset();
        Ctx = null;
        if (Instance == this) Instance = null;
        if (was && Stopped != null) Stopped();
    }

    Transform FindPlayer()
    {
        if (WorldLauncher.Player != null) return WorldLauncher.Player;
        GameObject go = null;
        try
        {
            go = GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
        }
        return go != null ? go.transform : null;
    }

    Camera FindCamera()
    {
        Camera c = Player != null ? Player.GetComponentInChildren<Camera>(true) : null;
        if (c == null) c = Camera.main;
        return c;
    }

    void Update()
    {
        if (!Running) return;
        if (Player == null)
        {
            Player = FindPlayer();
            if (Player == null) return;
            PlayerCamera = null;
        }
        if (PlayerCamera == null || !PlayerCamera.isActiveAndEnabled) PlayerCamera = FindCamera();
        VisibilityTester.BeginFrame(settings.linecastsPerFrame);
        if (Pedestrians != null) Pedestrians.Tick(Time.deltaTime);
        TrafficSignals.Refresh();
        if (Traffic != null) Traffic.Tick(Time.deltaTime);
    }

    public float GroundY(float x, float z, float fromY)
    {
        float y = Ctx != null ? Ctx.HeightAt(x, z) : 0f;
        int n = Physics.RaycastNonAlloc(new Vector3(x, fromY, z), Vector3.down, groundHits, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            Collider c = groundHits[i].collider;
            if (c == null) continue;
            if (Player != null && c.transform.IsChildOf(Player)) continue;
            if (c.GetComponentInParent<AgentBase>() != null) continue;
            if (groundHits[i].distance < best)
            {
                best = groundHits[i].distance;
                y = groundHits[i].point.y;
            }
        }
        return y;
    }

    void OnDrawGizmosSelected()
    {
        if (!settings.drawGizmos || !Application.isPlaying || Player == null) return;
        Vector3 p = Player.position;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(p, settings.spawnRadiusMin);
        Gizmos.DrawWireSphere(p, settings.spawnRadiusMax);
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(p, settings.activeRadius);
        if (PedGraph != null) PedGraph.DrawGizmos(p, 60f);
        if (LaneGraph != null) LaneGraph.DrawGizmos(p, 60f);
        if (Registry == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < Registry.All.Count; i++)
        {
            Bounds b = Registry.All[i].WorldBounds;
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
