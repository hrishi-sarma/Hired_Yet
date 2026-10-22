using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns deer/rabbits only in forest zones and cats only in suburb zones.
/// This system is separate from AgentPool and AgentRegistry so existing car/pedestrian AI is untouched.
/// </summary>
[DisallowMultipleComponent]
public sealed class AnimalWorldController : MonoBehaviour
{
    readonly List<AnimalAgent> animals = new List<AnimalAgent>(32);
    NpcDirector director;
    AnimalAISettings settings;
    System.Random rng;
    Transform animalRoot;
    float spawnTimer;
    int nextSeed = 1;
    bool shuttingDown;

    public List<AnimalAgent> Animals { get { return animals; } }
    public Transform MarkerRoot { get { return animalRoot; } }
    public bool IsShuttingDown { get { return shuttingDown; } }

    public void Initialize(NpcDirector owner, AnimalAISettings cfg)
    {
        director = owner;
        settings = cfg ?? new AnimalAISettings();
        settings.Sanitize();
        int seed = 17389;
        if (director != null && director.Ctx != null && director.Ctx.settings != null)
            seed ^= director.Ctx.settings.seed;
        rng = new System.Random(seed);
        shuttingDown = false;
        spawnTimer = settings.spawnInterval;
        GameObject root = new GameObject("Animal AI Agents");
        animalRoot = root.transform;
        animalRoot.SetParent(transform, false);
    }

    void Update()
    {
        if (shuttingDown || director == null || !director.Running || director.Ctx == null || director.Player == null) return;
        PruneDestroyed();
        if (settings.useHomes && !AnimalHomes.Built) AnimalHomes.Build(director.Ctx, settings);
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;
        spawnTimer = settings.spawnInterval;
        TryMaintainPopulation(AnimalSpecies.Deer, settings.deerCount);
        TryMaintainPopulation(AnimalSpecies.Rabbit, settings.rabbitCount);
        TryMaintainPopulation(AnimalSpecies.Cat, settings.catCount);
        TryMaintainPopulation(AnimalSpecies.Dog, settings.dogCount);
    }

    void TryMaintainPopulation(AnimalSpecies species, int target)
    {
        if (target <= 0 || Count(species) >= target) return;
        int attempts = settings.spawnAttemptsPerTick;
        for (int i = 0; i < attempts; i++)
        {
            if (TrySpawn(species)) return;
        }
    }

    bool TrySpawn(AnimalSpecies species)
    {
        WorldContext ctx = director.Ctx;
        if (ctx == null || ctx.zoneMap == null || ctx.Size <= 0f) return false;
        Vector3 player = director.Player.position;
        bool domestic = species == AnimalSpecies.Cat || species == AnimalSpecies.Dog;
        HomeSpot home = null;
        Vector3 homePos = Vector3.zero;
        bool useHome = settings.useHomes && AnimalHomes.Built && AnimalHomes.ListFor(species).Count > 0
            && (species != AnimalSpecies.Rabbit || settings.rabbitsUseBurrows);
        if (useHome && !PickHomeSpawn(species, player, out home, out homePos)) return false;
        float minR2 = settings.spawnRadiusMin * settings.spawnRadiusMin;
        float maxR2 = settings.spawnRadiusMax * settings.spawnRadiusMax;
        float distance = Mathf.Sqrt(Mathf.Lerp(minR2, maxR2, RandomValue()));
        float angle = RandomRange(0f, Mathf.PI * 2f);
        float x = player.x + Mathf.Cos(angle) * distance;
        float z = player.z + Mathf.Sin(angle) * distance;
        if (home != null) { x = homePos.x; z = homePos.z; }
        float halfWorld = ctx.Size * 0.5f;
        if (x < -halfWorld || x > halfWorld || z < -halfWorld || z > halfWorld) return false;

        ZoneType requiredZone = domestic ? ZoneType.Suburb : ZoneType.Forest;
        if (ctx.zoneMap.ZoneAt(x, z) != requiredZone) return false;
        if (!domestic && ctx.NormalAt(x, z).y < 0.94f) return false;

        float terrainY = ctx.HeightAt(x, z);
        float halfX = species == AnimalSpecies.Deer ? 0.55f : (species == AnimalSpecies.Rabbit ? 0.31f : (species == AnimalSpecies.Dog ? 0.45f : 0.36f));
        float halfZ = species == AnimalSpecies.Deer ? 0.72f : (species == AnimalSpecies.Rabbit ? 0.38f : (species == AnimalSpecies.Dog ? 0.65f : 0.42f));
        float height = species == AnimalSpecies.Deer ? 1.85f : (species == AnimalSpecies.Rabbit ? 0.78f : (species == AnimalSpecies.Dog ? 0.95f : 0.82f));
        float yaw = RandomRange(0f, 360f);
        OrientedBox box = new OrientedBox(new Vector2(x, z), new Vector2(halfX, halfZ), yaw, terrainY - 0.1f, terrainY + height);
        if (ctx.occupancy != null)
        {
            PlacementLayer layer = domestic ? PlacementLayer.Prop : PlacementLayer.Tree;
            if (!ctx.occupancy.IsFree(box, layer, 0.25f)) return false;
        }

        float y = director.GroundY(x, z, terrainY + 20f);
        Vector3 world = new Vector3(x, y, z);
        if (IsCrowded(null, world, settings.minAnimalSpacing)) return false;
        if (settings.avoidVisibleSpawns && VisibilityTester.CanSeeSpawn(world, Mathf.Max(halfX, halfZ))) return false;
        Vector3 checkCenter = world + Vector3.up * (height * 0.52f);
        if (Physics.CheckSphere(checkCenter, Mathf.Min(0.26f, halfX * 0.7f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;

        GameObject go = new GameObject(species.ToString());
        go.transform.SetParent(animalRoot, true);
        go.transform.SetPositionAndRotation(world, Quaternion.Euler(0f, yaw, 0f));
        AnimalAgent agent = go.AddComponent<AnimalAgent>();
        int animalSeed = rng.Next() ^ (nextSeed++ * 7919);
        if (home != null) animalSeed = home.seed;     // same home -> same animal (breed, personality)
        agent.Initialize(this, species, director, settings, animalSeed, home);
        if (home != null && settings.showBurrowMarkers) AnimalHomes.EnsureMarker(home, animalRoot);
        animals.Add(agent);
        return true;
    }

    readonly List<HomeSpot> homeScratch = new List<HomeSpot>(64);

    // Animals spawn at the home that belongs to them, at the spot that fits the time of day.
    bool PickHomeSpawn(AnimalSpecies species, Vector3 player, out HomeSpot home, out Vector3 pos)
    {
        home = null; pos = Vector3.zero;
        AnimalHomes.Candidates(species, player, settings.spawnRadiusMin, settings.spawnRadiusMax, homeScratch);
        if (homeScratch.Count == 0) return false;
        home = homeScratch[rng.Next(homeScratch.Count)];
        if (species == AnimalSpecies.Rabbit) { pos = home.center + home.front * 0.9f; return true; }
        if (species == AnimalSpecies.Deer) { pos = home.center; return true; }
        DayPhase ph = AnimalSleepClock.Phase(settings);
        int idx = HomeSpot.Porch;
        if (ph == DayPhase.Night) idx = HomeSpot.Bed;
        else if (ph == DayPhase.Day) idx = rng.Next(3);   // porch, or one of the two sunny spots
        pos = home.SpotOrCenter(idx);
        return true;
    }

    public bool IsCrowded(AnimalAgent requester, Vector3 position, float radius)
    {
        float sq = radius * radius;
        for (int i = 0; i < animals.Count; i++)
        {
            AnimalAgent other = animals[i];
            if (other == null || other == requester) continue;
            Vector3 delta = other.transform.position - position;
            delta.y = 0f;
            float separation = radius + (other.Species == AnimalSpecies.Deer ? 0.55f : 0.35f);
            if (delta.sqrMagnitude < Mathf.Max(sq, separation * separation)) return true;
        }
        return false;
    }

    /// <summary>Turns an event animal into a normal wandering animal at the given spot.</summary>
    public AnimalAgent Adopt(AnimalSpecies species, Vector3 world, float yaw)
    {
        GameObject go = new GameObject(species.ToString());
        go.transform.SetParent(animalRoot, true);
        go.transform.SetPositionAndRotation(world, Quaternion.Euler(0f, yaw, 0f));
        AnimalAgent agent = go.AddComponent<AnimalAgent>();
        agent.Initialize(this, species, director, settings, rng.Next() ^ (nextSeed++ * 7919), null);
        animals.Add(agent);
        return agent;
    }

    public void Remove(AnimalAgent animal)
    {
        if (animal != null) animals.Remove(animal);
    }

    int Count(AnimalSpecies species)
    {
        int count = 0;
        for (int i = 0; i < animals.Count; i++)
            if (animals[i] != null && animals[i].Species == species) count++;
        return count;
    }

    void PruneDestroyed()
    {
        for (int i = animals.Count - 1; i >= 0; i--)
            if (animals[i] == null) animals.RemoveAt(i);
    }

    float RandomValue() { return (float)rng.NextDouble(); }
    float RandomRange(float min, float max) { return min + RandomValue() * (max - min); }

    public void Shutdown()
    {
        if (shuttingDown) return;
        shuttingDown = true;
        for (int i = animals.Count - 1; i >= 0; i--)
            if (animals[i] != null) WorldUtil.Kill(animals[i].gameObject);
        animals.Clear();
        AnimalHomes.Clear();
        if (animalRoot != null) WorldUtil.Kill(animalRoot.gameObject);
        animalRoot = null;
        director = null;
    }

    void OnDestroy() { Shutdown(); }
}
