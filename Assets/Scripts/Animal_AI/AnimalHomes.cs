using System.Collections.Generic;
using UnityEngine;

public enum HomeKind { PetHouse, Burrow, DeerBed }

/// <summary>A place an animal belongs to. The same home always produces the same animal (breed, personality)
/// and remembers how much that animal trusts the player, even after it despawns and respawns.</summary>
public sealed class HomeSpot
{
    public HomeKind kind;
    public AnimalSpecies species;
    public int id;
    public int seed;
    public Vector3 center;      // pets: front porch, rabbits: burrow mouth, deer: bed
    public Vector3 front;       // direction the home faces
    public Vector3[] spots;     // pets: 0 porch, 1 sun A, 2 sun B, 3 bed
    public float radius;        // how far the animal roams from `center`
    public float trust;
    public AnimalAgent occupant;
    public GameObject marker;

    public const int Porch = 0, SunA = 1, SunB = 2, Bed = 3;

    public Vector3 SpotOrCenter(int i)
    {
        return (spots != null && i >= 0 && i < spots.Length) ? spots[i] : center;
    }
}

/// <summary>
/// Builds the animals' homes out of the generated world: every suburb lot with a house can own a cat or a dog
/// (front porch, two sunny lawn spots, a bed by the wall), rabbits get burrows at forest trees, deer get beds in
/// forest clearings and the pond gets drinking spots. Built once per world, deterministic from the world seed.
/// </summary>
public static class AnimalHomes
{
    public static readonly List<HomeSpot> Pets = new List<HomeSpot>();
    public static readonly List<HomeSpot> Burrows = new List<HomeSpot>();
    public static readonly List<HomeSpot> Beds = new List<HomeSpot>();
    public static readonly List<Vector3> PondSpots = new List<Vector3>();
    public static Vector3 PondCenter;
    public static bool Built { get; private set; }

    public static void Clear()
    {
        Pets.Clear(); Burrows.Clear(); Beds.Clear(); PondSpots.Clear();
        Built = false;
    }

    static int Hash(int a, int b)
    {
        unchecked
        {
            int h = a * 73856093 ^ b * 19349663;
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return h & 0x7fffffff;
        }
    }

    public static void Build(WorldContext ctx, AnimalAISettings s)
    {
        if (Built) return;
        Built = true;
        Pets.Clear(); Burrows.Clear(); Beds.Clear(); PondSpots.Clear();
        if (ctx == null || ctx.zoneMap == null || s == null) return;
        int seed = ctx.settings != null ? ctx.settings.seed : 1;
        BuildPets(ctx, s, seed);
        BuildBurrows(ctx, s, seed);
        BuildBeds(ctx, s, seed);
        BuildPond(ctx);
        Debug.Log("Animal homes: " + Pets.Count + " pet houses, " + Burrows.Count + " burrows, " + Beds.Count + " deer beds, " + PondSpots.Count + " pond spots.");
    }

    // ------------------------------------------------------------------ pets
    static bool SpotFree(WorldContext ctx, Vector2 p, float y, ZoneType zone)
    {
        if (ctx.zoneMap.ZoneAt(p) != zone) return false;
        if (ctx.occupancy == null) return true;
        OrientedBox box = new OrientedBox(p, new Vector2(0.45f, 0.45f), 0f, y - 0.2f, y + 0.9f);
        return ctx.occupancy.IsFree(box, PlacementLayer.Prop, 0.05f);
    }

    // Tries the wanted point first, then a few nearby ones, so garden props / bushes never block a spot.
    static bool FindFree(WorldContext ctx, Vector2 want, float y, float f, ZoneType zone, out Vector3 result)
    {
        float[] ox = { 0f, 1.4f, -1.4f, 2.8f, -2.8f, 0.7f, -0.7f };
        float[] oz = { 0f, 0.5f, 0.5f, 0f, 0f, -0.5f, -0.5f };
        for (int i = 0; i < ox.Length; i++)
        {
            Vector2 p = new Vector2(want.x + ox[i], want.y + oz[i] * f);
            if (SpotFree(ctx, p, y, zone)) { result = new Vector3(p.x, y, p.y); return true; }
        }
        result = new Vector3(want.x, y, want.y);
        return false;
    }

    static void BuildPets(WorldContext ctx, AnimalAISettings s, int seed)
    {
        for (int i = 0; i < ctx.lots.Count; i++)
        {
            LotInfo lot = ctx.lots[i];
            if (lot == null || lot.zone != ZoneType.Suburb || lot.empty) continue;
            int h = Hash(seed, i);
            float roll = (h & 0xFFFF) / 65535f;
            AnimalSpecies sp;
            if (roll < s.petCatLotChance) sp = AnimalSpecies.Cat;
            else if (roll < s.petCatLotChance + s.petDogLotChance) sp = AnimalSpecies.Dog;
            else continue;

            // The house sits toward lot.facingZ (the street side); the lawn strip in front of it is the pet's patch.
            float f = lot.facingZ >= 0 ? 1f : -1f;
            float setback = lot.buildingType != null ? Mathf.Max(1.5f, lot.buildingType.frontSetback) : 3f;
            float frontEdge = lot.center.y + f * lot.rect.height * 0.5f;
            float houseFront = frontEdge - f * setback;
            float cx = lot.center.x;
            float y = lot.baseY;

            Vector3 porch, sunA, sunB, bed;
            if (!FindFree(ctx, new Vector2(cx, houseFront + f * Mathf.Min(1.4f, setback * 0.5f)), y, f, ZoneType.Suburb, out porch)) continue;
            float side = (h & 1) == 0 ? 1f : -1f;
            if (!FindFree(ctx, new Vector2(cx - 2.8f, houseFront + f * 1.7f), y, f, ZoneType.Suburb, out sunA)) sunA = porch;
            if (!FindFree(ctx, new Vector2(cx + 2.8f, houseFront + f * 1.7f), y, f, ZoneType.Suburb, out sunB)) sunB = porch;
            if (!FindFree(ctx, new Vector2(cx + side * 1.3f, houseFront + f * 0.9f), y, f, ZoneType.Suburb, out bed)) bed = porch;

            HomeSpot home = new HomeSpot();
            home.kind = HomeKind.PetHouse;
            home.species = sp;
            home.id = Pets.Count;
            home.seed = h ^ (i * 131 + 17);
            home.center = porch;
            home.front = new Vector3(0f, 0f, f);
            home.spots = new Vector3[] { porch, sunA, sunB, bed };
            home.radius = sp == AnimalSpecies.Dog ? s.dogRoamRadius : s.catRoamRadius;
            home.trust = sp == AnimalSpecies.Dog ? 0.12f : 0.04f;
            Pets.Add(home);
        }
    }

    // ------------------------------------------------------------------ rabbits: a burrow beside some forest trees
    static void BuildBurrows(WorldContext ctx, AnimalAISettings s, int seed)
    {
        if (ctx.occupancy == null) return;
        List<SpawnRecord> recs = ctx.occupancy.Records;
        for (int i = 0; i < recs.Count && Burrows.Count < s.maxBurrows; i++)
        {
            SpawnRecord r = recs[i];
            if (r == null || r.layer != PlacementLayer.Tree) continue;
            int h = Hash(seed ^ 0x2f1, i);
            if ((h & 0xFFFF) / 65535f > s.burrowTreeChance) continue;
            Vector2 c = r.box.center;
            if (ctx.zoneMap.ZoneAt(c) != ZoneType.Forest) continue;
            float ang = ((h >> 4) & 0xFFF) / 4095f * Mathf.PI * 2f;
            float rad = Mathf.Max(r.box.half.x, r.box.half.y) + 1.1f + ((h >> 16) & 0xFF) / 255f * 0.8f;
            Vector2 p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
            if (ctx.zoneMap.ZoneAt(p) != ZoneType.Forest) continue;
            if (ctx.NormalAt(p.x, p.y).y < 0.94f) continue;
            OrientedBox box = new OrientedBox(p, new Vector2(0.4f, 0.4f), 0f, ctx.HeightAt(p.x, p.y) - 0.2f, ctx.HeightAt(p.x, p.y) + 0.6f);
            if (!ctx.occupancy.IsFree(box, PlacementLayer.Tree, 0.1f)) continue;
            if (ctx.roads != null && ctx.roads.InFootprint(p, 6f)) continue;

            HomeSpot home = new HomeSpot();
            home.kind = HomeKind.Burrow;
            home.species = AnimalSpecies.Rabbit;
            home.id = Burrows.Count;
            home.seed = h ^ 0x51ed;
            home.center = new Vector3(p.x, ctx.HeightAt(p.x, p.y), p.y);
            Vector2 toTree = (c - p).normalized;
            home.front = new Vector3(-toTree.x, 0f, -toTree.y);   // the hole opens away from the tree
            home.spots = new Vector3[] { home.center };
            home.radius = s.rabbitRoamRadius;
            home.trust = 0.03f;
            Burrows.Add(home);
        }
    }

    // ------------------------------------------------------------------ deer: beds in clearings
    static void BuildBeds(WorldContext ctx, AnimalAISettings s, int seed)
    {
        System.Random rng = new System.Random(seed ^ 0x9157);
        float half = ctx.Size * 0.5f - 60f;
        if (half <= 0f) return;
        for (int attempt = 0; attempt < 2500 && Beds.Count < s.maxDeerBeds; attempt++)
        {
            float x = (float)(rng.NextDouble() * 2.0 - 1.0) * half;
            float z = (float)(rng.NextDouble() * 2.0 - 1.0) * half;
            Vector2 p = new Vector2(x, z);
            if (ctx.zoneMap.ZoneAt(p) != ZoneType.Forest) continue;
            if (ctx.NormalAt(x, z).y < 0.96f) continue;
            float y = ctx.HeightAt(x, z);
            if (ctx.occupancy != null)
            {
                OrientedBox box = new OrientedBox(p, new Vector2(1.3f, 1.3f), 0f, y - 0.2f, y + 1.5f);
                if (!ctx.occupancy.IsFree(box, PlacementLayer.Tree, 0.8f)) continue;
            }
            if (ctx.roads != null && ctx.roads.InFootprint(p, 10f)) continue;
            bool near = false;
            for (int k = 0; k < Beds.Count && !near; k++)
            {
                Vector3 b = Beds[k].center;
                if ((b.x - x) * (b.x - x) + (b.z - z) * (b.z - z) < 3.5f * 3.5f) near = true;
            }
            if (near) continue;

            HomeSpot home = new HomeSpot();
            home.kind = HomeKind.DeerBed;
            home.species = AnimalSpecies.Deer;
            home.id = Beds.Count;
            home.seed = Hash(seed ^ 0x77, attempt);
            home.center = new Vector3(x, y, z);
            home.front = new Vector3(Mathf.Cos(attempt), 0f, Mathf.Sin(attempt));
            home.spots = new Vector3[] { home.center };
            home.radius = s.deerRoamRadius;
            home.trust = 0.02f;
            Beds.Add(home);
        }
    }

    // ------------------------------------------------------------------ pond shore
    static void BuildPond(WorldContext ctx)
    {
        if (!PondStage.HasPond) return;
        PondStage stage = Object.FindFirstObjectByType<PondStage>();
        float R = stage != null ? stage.radius : 15f;
        PondCenter = PondStage.Center;
        float water = PondCenter.y;
        for (int ring = 0; ring < 3; ring++)
        {
            float rr = R + 0.8f + ring * 0.9f;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2f / 16f + ring * 0.2f;
                float x = PondCenter.x + Mathf.Cos(a) * rr;
                float z = PondCenter.z + Mathf.Sin(a) * rr;
                if (ctx.zoneMap.ZoneAt(x, z) != ZoneType.Forest) continue;   // deer can only walk in forest zones
                float gy = ctx.HeightAt(x, z);
                if (gy < water - 0.05f || gy > water + 1.2f) continue;
                if (ctx.NormalAt(x, z).y < 0.9f) continue;
                if (ctx.occupancy != null)
                {
                    OrientedBox box = new OrientedBox(new Vector2(x, z), new Vector2(0.6f, 0.6f), 0f, gy - 0.2f, gy + 1.2f);
                    if (!ctx.occupancy.IsFree(box, PlacementLayer.Tree, 0.1f)) continue;
                }
                PondSpots.Add(new Vector3(x, gy, z));
            }
            if (PondSpots.Count >= 6) break;
        }
    }

    public static bool NearestPondSpot(Vector3 from, float maxDistance, System.Random rng, out Vector3 spot)
    {
        spot = Vector3.zero;
        if (PondSpots.Count == 0) return false;
        float best = maxDistance * maxDistance;
        bool found = false;
        int start = rng != null ? rng.Next(PondSpots.Count) : 0;
        for (int n = 0; n < PondSpots.Count; n++)
        {
            Vector3 p = PondSpots[(start + n) % PondSpots.Count];
            float d = (p.x - from.x) * (p.x - from.x) + (p.z - from.z) * (p.z - from.z);
            // prefer nearby shore points but not always the very closest, so a herd spreads along the bank
            if (d < best) { best = d * 0.97f; spot = p; found = true; }
        }
        return found;
    }

    // ------------------------------------------------------------------ lookup used by the spawner
    public static List<HomeSpot> ListFor(AnimalSpecies species)
    {
        if (species == AnimalSpecies.Rabbit) return Burrows;
        if (species == AnimalSpecies.Deer) return Beds;
        return Pets;
    }

    /// <summary>Free homes of this species whose anchor lies between rMin and rMax from `from`.</summary>
    public static void Candidates(AnimalSpecies species, Vector3 from, float rMin, float rMax, List<HomeSpot> result)
    {
        result.Clear();
        List<HomeSpot> list = ListFor(species);
        float a = rMin * rMin, b = rMax * rMax;
        for (int i = 0; i < list.Count; i++)
        {
            HomeSpot h = list[i];
            if (h.occupant != null || (h.kind == HomeKind.PetHouse && h.species != species)) continue;
            float d = (h.center.x - from.x) * (h.center.x - from.x) + (h.center.z - from.z) * (h.center.z - from.z);
            if (d >= a && d <= b) result.Add(h);
        }
    }

    // ------------------------------------------------------------------ visible burrow hole
    public static void EnsureMarker(HomeSpot home, Transform parent)
    {
        if (home == null || home.kind != HomeKind.Burrow || home.marker != null || parent == null) return;
        GameObject root = new GameObject("Burrow");
        root.transform.SetParent(parent, false);
        root.transform.position = home.center;
        root.transform.rotation = Quaternion.LookRotation(home.front.sqrMagnitude > 0.01f ? home.front : Vector3.forward, Vector3.up);
        MakePart(root.transform, "Mound", new Vector3(0f, 0.04f, -0.1f), new Vector3(1.05f, 0.34f, 0.95f), new Color(0.40f, 0.30f, 0.20f));
        MakePart(root.transform, "Hole", new Vector3(0f, 0.09f, 0.22f), new Vector3(0.46f, 0.13f, 0.34f), new Color(0.07f, 0.05f, 0.04f));
        home.marker = root;
    }

    static void MakePart(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        Collider c = go.GetComponent<Collider>();
        if (c != null) WorldUtil.Kill(c);
        go.layer = 2;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) { r.sharedMaterial = AnimalMaterialFactory.Flat(color, 0.1f); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
    }
}
