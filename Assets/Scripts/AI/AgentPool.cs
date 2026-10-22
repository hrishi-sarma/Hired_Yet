using System.Collections.Generic;
using UnityEngine;

public class AgentBase : MonoBehaviour, IAgent
{
    AgentKind kind;
    float radius = 0.3f;
    float height = 1.75f;
    float baseRadius = 0.3f;
    float baseHeight = 1.75f;
    float lastVisibleTime;
    float nextCheck;
    float blockedSince = -1f;
    bool everSeen;
    readonly Vector3[] samples = new Vector3[3];

    public AgentKind Kind
    {
        get { return kind; }
    }

    public Transform Tr
    {
        get { return transform; }
    }

    public Vector2 Pos2D
    {
        get
        {
            Vector3 p = transform.position;
            return new Vector2(p.x, p.z);
        }
    }

    public float Radius
    {
        get { return radius; }
    }

    public float Height
    {
        get { return height; }
    }

    public Bounds WorldBounds
    {
        get { return new Bounds(transform.position + Vector3.up * (height * 0.5f), new Vector3(radius * 2f, height, radius * 2f)); }
    }

    public bool Pooled { get; internal set; }

    protected virtual bool DespawnBlocked
    {
        get { return false; }
    }

    /// <summary>True while the agent must not be removed (e.g. the player is following a car).</summary>
    protected virtual bool KeepAlive(float sqDistToPlayer)
    {
        return false;
    }

    protected float SecondsSinceSeen
    {
        get { return Time.time - lastVisibleTime; }
    }

    public void Setup(AgentKind agentKind, float agentRadius, float agentHeight)
    {
        kind = agentKind;
        radius = agentRadius;
        height = agentHeight;
        baseRadius = agentRadius;
        baseHeight = agentHeight;
        Pooled = true;
    }

    /// <summary>Uniformly scales the agent and its registry size (used for per-spawn NPC size variation).</summary>
    public void ApplyScale(float k)
    {
        k = Mathf.Max(0.1f, k);
        transform.localScale = new Vector3(k, k, k);
        radius = baseRadius * k;
        height = baseHeight * k;
    }

    public void Activate(Vector3 position, float yawDeg)
    {
        NpcDirector d = NpcDirector.Instance;
        if (d == null) return;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDeg, 0f));
        gameObject.SetActive(true);
        lastVisibleTime = Time.time;
        nextCheck = Time.time + (float)d.Rng.NextDouble() * d.settings.visibilityCheckInterval;
        blockedSince = -1f;
        everSeen = false;
        d.Registry.Add(this);
        OnSpawn();
    }

    public void Despawn()
    {
        NpcDirector d = NpcDirector.Instance;
        if (d != null && d.Pool != null) d.Pool.Release(gameObject);
    }

    internal void Deactivate()
    {
        NpcDirector d = NpcDirector.Instance;
        if (d != null && d.Registry != null) d.Registry.Remove(this);
        OnDespawn();
        Pooled = true;
        gameObject.SetActive(false);
    }

    protected virtual void OnSpawn() { }

    public virtual void OnDespawn() { }

    protected virtual void OnAgentUpdate(float dt) { }

    void Update()
    {
        if (Pooled) return;
        NpcDirector d = NpcDirector.Instance;
        if (d == null || !d.Running || d.Player == null) return;
        OnAgentUpdate(Time.deltaTime);
        if (Pooled) return;
        d.Registry.Move(this);
        if (Time.time >= nextCheck) CheckVisibility(d);
    }

    void CheckVisibility(NpcDirector d)
    {
        AiSettings s = d.settings;
        Vector3 p = transform.position;
        Vector3 pp = d.Player.position;
        float dx = p.x - pp.x;
        float dz = p.z - pp.z;
        float sq = dx * dx + dz * dz;
        bool keep = KeepAlive(sq);
        bool hard = sq > s.activeRadius * s.activeRadius && !keep;
        bool visible = false;

        if (!hard)
        {
            if (VisibilityTester.BudgetLeft < 1)
            {
                nextCheck = Time.time + 0.05f;
                return;
            }
            samples[0] = p + Vector3.up * (height * 0.5f);
            samples[1] = p + Vector3.up * (height * 0.92f);
            samples[2] = p + Vector3.up * 0.15f;
            visible = d.PlayerCamera != null
                ? VisibilityTester.IsVisible(d.PlayerCamera, WorldBounds, transform, samples)
                : false;
        }

        nextCheck = Time.time + s.visibilityCheckInterval;

        if (visible)
        {
            lastVisibleTime = Time.time;
            blockedSince = -1f;
            everSeen = true;
            return;
        }

        if (keep) return;
        bool graceOver = Time.time - lastVisibleTime >= s.despawnGraceSeconds;
        bool farEnough = sq > d.KeepRadius * d.KeepRadius;
        if (!hard && !(everSeen && graceOver && farEnough)) return;

        if (DespawnBlocked)
        {
            if (blockedSince < 0f) blockedSince = Time.time;
            if (Time.time - blockedSince < 10f) return;
        }

        Despawn();
    }
}

public class AgentPool
{
    NpcDirector director;
    Transform root;
    Transform poolRoot;
    Transform activeRoot;
    readonly Stack<AgentBase>[] free = { new Stack<AgentBase>(), new Stack<AgentBase>() };
    readonly Dictionary<int, Stack<AgentBase>> customFree = new Dictionary<int, Stack<AgentBase>>();

    public void Init(NpcDirector owner)
    {
        director = owner;
        root = new GameObject("AI Agents").transform;
        root.SetParent(owner.transform, false);
        activeRoot = new GameObject("Active").transform;
        activeRoot.SetParent(root, false);
        poolRoot = new GameObject("Pool").transform;
        poolRoot.SetParent(root, false);
    }

    public void Prewarm(int pedestrians, int cars)
    {
        for (int i = 0; i < pedestrians; i++) Park(Build(AgentKind.Pedestrian, PickProfile()));
        for (int i = 0; i < cars; i++) Park(Build(AgentKind.Car, -1));
    }

    void Park(AgentBase a)
    {
        a.transform.SetParent(poolRoot, false);
        Pedestrian ped = a as Pedestrian;
        StackFor(a.Kind, ped != null ? ped.ProfileIndex : -1).Push(a);
    }

    Stack<AgentBase> StackFor(AgentKind kind, int profileIndex)
    {
        if (kind != AgentKind.Pedestrian || profileIndex < 0) return free[(int)kind];
        Stack<AgentBase> st;
        if (!customFree.TryGetValue(profileIndex, out st))
        {
            st = new Stack<AgentBase>();
            customFree[profileIndex] = st;
        }
        return st;
    }

    int PickProfile()
    {
        return NpcProfile.Pick(director.settings.npcProfiles, director.Rng);
    }

    public AgentBase Get(AgentKind kind)
    {
        int profile = kind == AgentKind.Pedestrian ? PickProfile() : -1;
        Stack<AgentBase> st = StackFor(kind, profile);
        AgentBase a = null;
        while (st.Count > 0 && a == null) a = st.Pop();
        if (a == null) a = Build(kind, profile);
        a.Pooled = false;
        a.transform.SetParent(activeRoot, false);
        Recolor(a);
        return a;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;
        AgentBase a = go.GetComponent<AgentBase>();
        if (a == null)
        {
            WorldUtil.Kill(go);
            return;
        }
        if (a.Pooled) return;
        a.Deactivate();
        Park(a);
    }

    public void Clear()
    {
        free[0].Clear();
        free[1].Clear();
        customFree.Clear();
        if (root != null) WorldUtil.Kill(root.gameObject);
        root = null;
        poolRoot = null;
        activeRoot = null;
    }

    void Recolor(AgentBase a)
    {
        System.Random rng = director.Rng;
        if (a.Kind == AgentKind.Pedestrian)
        {
            Pedestrian ped = a as Pedestrian;
            if ((ped == null || ped.ProfileIndex < 0) && a.transform.Find("Body") != null)
                AiPrimitives.RecolorPedestrian(a.transform, AiPrimitives.RandomShirt(rng), AiPrimitives.RandomSkin(rng));
        }
        else if (a.transform.Find("Body") != null)
        {
            AiPrimitives.RecolorCar(a.transform, AiPrimitives.RandomCar(rng));
        }
    }

    static bool HasModel(ModelSlot slot, System.Random rng)
    {
        return slot != null && slot.PickVariant(rng) != null;
    }

    /// <summary>Edit-mode helper for the Start Menu baker: builds one fully modelled agent under <paramref name="parent"/> (no pooling).</summary>
    public GameObject BuildForBake(NpcDirector owner, AgentKind kind, Transform parent, out int profileIndex)
    {
        director = owner;
        poolRoot = parent;
        AgentBase a = Build(kind, kind == AgentKind.Pedestrian ? PickProfile() : -1);
        Recolor(a);
        Pedestrian ped = a as Pedestrian;
        profileIndex = ped != null ? ped.ProfileIndex : -1;
        a.gameObject.SetActive(true);   // Build() leaves pooled agents inactive; baked menu agents must be live
        return a.gameObject;
    }

    AgentBase Build(AgentKind kind, int profileIndex)
    {
        System.Random rng = director.Rng;
        AiSettings s = director.settings;
        GameObject go;
        float radius;
        float height;
        float carLength = AiPrimitives.CarSize.z;

        NpcProfile profile = null;
        if (kind == AgentKind.Pedestrian && profileIndex >= 0 && profileIndex < s.npcProfiles.Count) profile = s.npcProfiles[profileIndex];
        if (profile != null && !profile.IsValid) profile = null;

        if (profile != null)
        {
            radius = AiPrimitives.PedestrianRadius;
            go = BuildCustomPedestrian(profile, radius, out height);
        }
        else if (kind == AgentKind.Pedestrian)
        {
            height = 1.65f + (float)rng.NextDouble() * 0.2f;
            radius = AiPrimitives.PedestrianRadius;
            if (HasModel(s.pedestrianSlot, rng)) go = BuildFromSlot(s.pedestrianSlot, new Vector3(radius * 2f, height, radius * 2f), true);
            else go = AiPrimitives.BuildPedestrian(AiPrimitives.RandomShirt(rng), AiPrimitives.RandomSkin(rng), height, poolRoot);
        }
        else
        {
            CustomVehicleModel customVehicle = PickCustomVehicle(s.customVehicles, rng);
            if (customVehicle != null)
            {
                Vector3 dimensions;
                go = BuildCustomCar(customVehicle, out dimensions);
                height = Mathf.Max(0.1f, dimensions.y);
                radius = Mathf.Max(0.1f, Mathf.Max(dimensions.x, dimensions.z) * 0.5f);
                carLength = Mathf.Max(1f, dimensions.z);
            }
            else if (HasModel(s.carSlot, rng))
            {
                Vector3 targetSize = AiPrimitives.CarSize * Mathf.Clamp(s.carSlotScale, 0.25f, 4f);
                go = BuildFromSlot(s.carSlot, targetSize, false);
                height = targetSize.y;
                radius = Mathf.Max(targetSize.x, targetSize.z) * 0.5f;
                carLength = targetSize.z;
            }
            else
            {
                height = AiPrimitives.CarSize.y;
                radius = AiPrimitives.CarSize.z * 0.5f;
                go = AiPrimitives.BuildCar(AiPrimitives.RandomCar(rng), poolRoot);
                carLength = AiPrimitives.CarSize.z;
            }
        }

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;

        AgentBase a = kind == AgentKind.Pedestrian ? (AgentBase)go.AddComponent<Pedestrian>() : go.AddComponent<Car>();
        a.Setup(kind, radius, height);
        Car builtCar = a as Car;
        if (builtCar != null) builtCar.SetBodyLength(carLength);
        Pedestrian built = a as Pedestrian;
        if (built != null && profile != null)
        {
            built.Profile = profile;
            built.ProfileIndex = profileIndex;
        }
        go.SetActive(false);
        return a;
    }

    public static CustomVehicleModel PickCustomVehicle(List<CustomVehicleModel> vehicles, System.Random rng)
    {
        if (vehicles == null || vehicles.Count == 0) return null;

        int validCount = 0;
        for (int i = 0; i < vehicles.Count; i++)
            if (vehicles[i] != null && vehicles[i].prefab != null) validCount++;

        if (validCount == 0) return null;

        int pick = rng.Next(validCount);
        for (int i = 0; i < vehicles.Count; i++)
        {
            CustomVehicleModel entry = vehicles[i];
            if (entry == null || entry.prefab == null) continue;
            if (pick-- == 0) return entry;
        }
        return null;
    }

    GameObject BuildCustomCar(CustomVehicleModel vehicle, out Vector3 dimensions)
    {
        GameObject root = new GameObject("Car");
        root.transform.SetParent(poolRoot, false);

        GameObject model = Object.Instantiate(vehicle.prefab, root.transform, false);
        model.name = string.IsNullOrWhiteSpace(vehicle.displayName) ? vehicle.prefab.name : vehicle.displayName;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = model.transform.localScale * Mathf.Clamp(vehicle.scale, 0.25f, 4f);

        Bounds localBounds;
        if (!TryGetLocalRendererBounds(root.transform, out localBounds))
        {
            dimensions = AiPrimitives.CarSize * Mathf.Clamp(vehicle.scale, 0.25f, 4f);
            BoxCollider fallback = root.AddComponent<BoxCollider>();
            fallback.size = dimensions;
            fallback.center = new Vector3(0f, dimensions.y * 0.5f, 0f);
            return root;
        }

        // Recenter the model and put its lowest visible point on the vehicle root's ground plane.
        model.transform.localPosition += new Vector3(-localBounds.center.x, -localBounds.min.y, -localBounds.center.z);
        if (!TryGetLocalRendererBounds(root.transform, out localBounds))
            localBounds = new Bounds(new Vector3(0f, AiPrimitives.CarSize.y * 0.5f, 0f), AiPrimitives.CarSize);

        dimensions = new Vector3(
            Mathf.Max(0.1f, localBounds.size.x),
            Mathf.Max(0.1f, localBounds.size.y),
            Mathf.Max(0.1f, localBounds.size.z));

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = localBounds.center;
        col.size = dimensions;
        return root;
    }

    static bool TryGetLocalRendererBounds(Transform root, out Bounds bounds)
    {
        return VehicleBounds.TryGetLocalBodyBounds(root, out bounds);
    }

    GameObject BuildCustomPedestrian(NpcProfile profile, float radius, out float height)
    {
        GameObject go = new GameObject("Pedestrian");
        go.transform.SetParent(poolRoot, false);

        GameObject model = Object.Instantiate(profile.prefab, go.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        NpcStylizedPainter.Apply(model, director.Rng);

        if (profile.stripPrefabPhysics)
        {
            Collider[] cols = model.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) WorldUtil.Kill(cols[i]);
            Rigidbody[] rbs = model.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++) WorldUtil.Kill(rbs[i]);
        }

        float native = MeasureHeight(model);
        float fit = profile.fitToHeight > 0.1f && native > 0.05f ? profile.fitToHeight / native : 1f;
        if (fit != 1f) model.transform.localScale *= fit;
        height = native > 0.05f ? native * fit : 1.75f;

        CapsuleCollider col = go.AddComponent<CapsuleCollider>();
        col.direction = 1;
        col.radius = radius;
        col.height = height;
        col.center = new Vector3(0f, height * 0.5f, 0f);

        Animator anim = model.GetComponentInChildren<Animator>(true);
        if (anim == null) anim = model.AddComponent<Animator>();
        if (anim.avatar == null)
            Debug.LogWarning("NPC profile '" + profile.name + "': prefab has no Avatar on its Animator. Set the model's Rig to Humanoid so animations can play.", profile.prefab);

        NpcAnimator npcAnim = go.AddComponent<NpcAnimator>();
        npcAnim.Configure(anim, NpcAnimationSet.Resolve(profile.animationSet, director.settings.defaultAnimations));
        if (!npcAnim.Ready)
            Debug.LogWarning("NPC profile '" + profile.name + "': no animation clips found (assign a Default Animations set in AiSettings or an Animation Set on the profile).", profile.prefab);
        return go;
    }

    static float MeasureHeight(GameObject model)
    {
        Renderer[] rs = model.GetComponentsInChildren<Renderer>(true);
        bool any = false;
        Bounds b = new Bounds();
        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i] is ParticleSystemRenderer) continue;
            if (!any)
            {
                b = rs[i].bounds;
                any = true;
            }
            else b.Encapsulate(rs[i].bounds);
        }
        return any ? b.size.y : 0f;
    }

    GameObject BuildFromSlot(ModelSlot source, Vector3 size, bool pedestrian)
    {
        ModelSlot slot = source.WithFit(source.fit);
        slot.colliders = ColliderMode.None;
        slot.markStatic = false;

        GameObject go = new GameObject(pedestrian ? "Pedestrian" : "Car");
        go.transform.SetParent(poolRoot, false);
        ModelFitter.Spawn(slot, go.transform.position, 0f, size, go.transform, director.Rng);

        if (pedestrian)
        {
            CapsuleCollider col = go.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.radius = size.x * 0.5f;
            col.height = size.y;
            col.center = new Vector3(0f, size.y * 0.5f, 0f);
        }
        else
        {
            BoxCollider col = go.AddComponent<BoxCollider>();
            col.size = size;
            col.center = new Vector3(0f, size.y * 0.5f, 0f);
        }
        return go;
    }
}

/// <summary>
/// Measures a vehicle's real body for grounding. Prefabs often carry extra renderers (shadow planes, glow quads,
/// light cones, hidden LODs) that stick out below the wheels; counting them lifts the whole car off the road.
/// </summary>
public static class VehicleBounds
{
    static readonly string[] helperNames =
    {
        "shadow", "blob", "decal", "halo", "glow", "beam", "cone", "flare", "volumetric",
        "lightray", "light_ray", "godray", "outline", "highlight", "selection", "marker", "ground", "collider"
    };

    /// <summary>True for renderers that belong to the car body (wheels, panels, glass, lenses).</summary>
    public static bool IsBodyRenderer(Renderer r)
    {
        if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) return false;
        if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) return false;
        if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) return false;

        string n = r.transform.name.ToLowerInvariant();
        for (int i = 0; i < helperNames.Length; i++)
            if (n.Contains(helperNames[i])) return false;

        // A big, paper-thin sheet is a shadow/decal plane, not bodywork.
        Vector3 sz = r.bounds.size;
        if (sz.y < 0.03f && sz.x * sz.z > 1f) return false;
        return true;
    }

    /// <summary>Bounds of the body renderers in root's local space. Falls back to every renderer if the filter removes all.</summary>
    public static bool TryGetLocalBodyBounds(Transform root, out Bounds bounds)
    {
        Renderer[] all = root.GetComponentsInChildren<Renderer>(true);
        if (Encapsulate(root, all, true, out bounds)) return true;
        return Encapsulate(root, all, false, out bounds);
    }

    static bool Encapsulate(Transform root, Renderer[] renderers, bool bodyOnly, out Bounds bounds)
    {
        bool found = false;
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || r is ParticleSystemRenderer) continue;
            if (bodyOnly && !IsBodyRenderer(r)) continue;
            Bounds wb = r.bounds;
            Vector3 c = wb.center;
            Vector3 e = wb.extents;
            for (int k = 0; k < 8; k++)
            {
                Vector3 corner = c + new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z);
                Vector3 p = root.InverseTransformPoint(corner);
                if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                else bounds.Encapsulate(p);
            }
        }
        return found;
    }
}
