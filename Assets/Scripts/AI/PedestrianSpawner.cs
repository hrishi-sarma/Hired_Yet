using System.Collections.Generic;
using UnityEngine;

public class PedestrianSpawner
{
    readonly NpcDirector director;
    readonly PedestrianGraph graph;
    readonly List<PedEdge> candidates = new List<PedEdge>(32);
    readonly List<IAgent> near = new List<IAgent>(8);
    float budget;

    public event System.Action<Pedestrian> Spawned;

    public PedestrianGraph Graph
    {
        get { return graph; }
    }

    public PedestrianSpawner(NpcDirector owner, PedestrianGraph g)
    {
        director = owner;
        graph = g;
        Spawned += OnSpawned;
    }

    void OnSpawned(Pedestrian p)
    {
        PickpocketSettings st = director.settings.pickpocket;
        if (PlayerInventory.PickpocketLocked || PickpocketTarget.ActiveCount >= st.maxPickpocketTargets) return;
        if (director.Rng.NextDouble() >= st.pickpocketChance) return;
        // Stage 9: the carried item comes from a random NPC spawn table (duplicates of held items are skipped).
        Items.ItemInstance item = Items.ItemPickups.Roll(Items.ItemPickups.RandomNpcSource(director.Rng), director.Rng);
        if (item != null) PickpocketTarget.Flag(p, item);
    }

    public void Tick(float dt)
    {
        if (director.Player == null || graph.Sidewalks.Count == 0) return;
        AiSettings st = director.settings;
        if (director.Registry.Count(AgentKind.Pedestrian) >= st.maxPedestrians)
        {
            budget = 0f;
            return;
        }
        budget = Mathf.Min(budget + dt * st.spawnAttemptsPerSecond, 4f);
        while (budget >= 1f)
        {
            budget -= 1f;
            TrySpawn(st.spawnRadiusMin, st.spawnRadiusMax, true);
        }
    }

    public void Populate(int count, float minR, float maxR)
    {
        if (director.Player == null) return;
        int made = 0;
        for (int i = 0; i < count * 40 && made < count; i++)
            if (TrySpawn(minR, maxR, false)) made++;
    }

    bool TrySpawn(float minR, float maxR, bool hidden)
    {
        AiSettings st = director.settings;
        if (director.Registry.Count(AgentKind.Pedestrian) >= st.maxPedestrians) return false;
        System.Random rng = director.Rng;
        Vector3 pp = director.Player.position;
        float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
        float min2 = minR * minR;
        float max2 = maxR * maxR;
        float dist = Mathf.Sqrt(Mathf.Lerp(min2, max2, (float)rng.NextDouble()));
        Vector2 probe = new Vector2(pp.x + Mathf.Cos(ang) * dist, pp.z + Mathf.Sin(ang) * dist);
        graph.EdgesNear(probe, 14f, candidates);
        if (candidates.Count == 0) return false;
        PedEdge e = candidates[rng.Next(candidates.Count)];
        if (rng.NextDouble() > Mathf.Clamp01(st.pedestrianDensity.For(e.zone))) return false;
        float s;
        bool fromDoor = false;
        if (e.doors.Count > 0 && rng.NextDouble() < 0.2)
        {
            s = e.doors[rng.Next(e.doors.Count)].s;
            fromDoor = true;
        }
        else s = Mathf.Clamp(e.Project(probe), 0.8f, Mathf.Max(0.8f, e.length - 0.8f));
        if (rng.NextDouble() < 0.5)
        {
            e = e.reverse;
            s = e.length - s;
        }
        Vector2 pos = e.Point(s) + e.left * (fromDoor ? e.innerSign * e.lane : e.lane);
        float dx = pos.x - pp.x;
        float dz = pos.y - pp.z;
        float sq = dx * dx + dz * dz;
        if (sq < min2 || sq > max2) return false;
        director.Registry.Query(pos, 1.2f, near);
        if (near.Count > 0) return false;
        Vector3 world = new Vector3(pos.x, e.Height(s), pos.y);
        if (hidden && VisibilityTester.CanSeeSpawn(world, 0.3f)) return false;
        return Spawn(e, s, fromDoor, world) != null;
    }

    Pedestrian Spawn(PedEdge e, float s, bool fromDoor, Vector3 world)
    {
        AgentBase a = director.Pool.Get(AgentKind.Pedestrian);
        Pedestrian p = a as Pedestrian;
        if (p == null)
        {
            director.Pool.Release(a.gameObject);
            return null;
        }
        p.Prepare(graph, e, s, fromDoor);
        p.Activate(world, Mathf.Atan2(e.dir.x, e.dir.y) * Mathf.Rad2Deg);
        if (Spawned != null) Spawned(p);
        return p;
    }

}
