using System.Collections.Generic;
using UnityEngine;

public class TrafficSpawner
{
    readonly NpcDirector director;
    readonly LaneGraph graph;
    readonly List<RoadPath> candidates = new List<RoadPath>(32);
    readonly List<IAgent> near = new List<IAgent>(8);
    readonly float[] approach;
    float budget;

    public event System.Action<Car> Spawned;

    public LaneGraph Graph
    {
        get { return graph; }
    }

    public TrafficSpawner(NpcDirector owner, LaneGraph g)
    {
        director = owner;
        graph = g;
        approach = new float[owner.Crosswalks.Count + 1];
        owner.Crosswalks.TrafficApproaching = IsApproaching;
    }

    bool IsApproaching(CrosswalkId id)
    {
        return id.value >= 0 && id.value < approach.Length && Time.time < approach[id.value];
    }

    public void Mark(CrosswalkId id)
    {
        if (id.value >= 0 && id.value < approach.Length) approach[id.value] = Time.time + 0.4f;
    }

    public void Tick(float dt)
    {
        if (director.Player == null || graph.Lanes.Count == 0) return;
        AiSettings st = director.settings;
        if (director.Registry.Count(AgentKind.Car) >= CarCap(st))
        {
            budget = 0f;
            return;
        }
        budget = Mathf.Min(budget + dt * st.spawnAttemptsPerSecond * 0.5f * Mathf.Max(0.4f, TrafficClock.Density), 3f);
        while (budget >= 1f)
        {
            budget -= 1f;
            TrySpawn(st.spawnRadiusMin, st.spawnRadiusMax, true);
        }
    }

    /// <summary>Car limit for the current hour: busy at rush hour, nearly empty late at night.</summary>
    int CarCap(AiSettings st)
    {
        return Mathf.Max(4, Mathf.RoundToInt(st.maxCars * TrafficClock.Density));
    }

    public void Populate(int count, float minR, float maxR)
    {
        count = Mathf.Max(2, Mathf.RoundToInt(count * TrafficClock.Density));
        if (director.Player == null) return;
        int made = 0;
        for (int i = 0; i < count * 40 && made < count; i++)
            if (TrySpawn(minR, maxR, false)) made++;
    }

    bool TrySpawn(float minR, float maxR, bool hidden)
    {
        AiSettings st = director.settings;
        if (director.Registry.Count(AgentKind.Car) >= CarCap(st)) return false;
        System.Random rng = director.Rng;
        Vector3 pp = director.Player.position;
        float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
        float min2 = minR * minR;
        float max2 = maxR * maxR;
        float dist = Mathf.Sqrt(Mathf.Lerp(min2, max2, (float)rng.NextDouble()));
        Vector2 probe = new Vector2(pp.x + Mathf.Cos(ang) * dist, pp.z + Mathf.Sin(ang) * dist);
        graph.LanesNear(probe, 24f, candidates);
        if (candidates.Count == 0) return false;
        RoadPath lane = candidates[rng.Next(candidates.Count)];
        if (rng.NextDouble() > Mathf.Clamp01(st.carDensity.For(lane.zone) * TrafficClock.ZoneNight(lane.zone))) return false;
        float d;
        float s = Mathf.Clamp(lane.Project(probe, out d), 4f, Mathf.Max(4f, lane.length - 8f));
        if (lane.length < 12f) return false;
        Vector2 p;
        Vector2 dir;
        float y;
        lane.Sample(s, out p, out dir, out y);
        float dx = p.x - pp.x;
        float dz = p.y - pp.z;
        float sq = dx * dx + dz * dz;
        if (sq < min2 || sq > max2) return false;
        for (int i = 0; i < lane.cars.Count; i++)
            if (Mathf.Abs(lane.cars[i].PathS - s) < 14f) return false;
        director.Registry.Query(p, 4f, near);
        if (near.Count > 0) return false;
        Vector3 world = new Vector3(p.x, y, p.y);
        if (hidden && VisibilityTester.CanSeeSpawn(world, 1f)) return false;
        return Spawn(lane, s, lane.speed * 0.7f, world, dir) != null;
    }

    Car Spawn(RoadPath lane, float s, float speed, Vector3 world, Vector2 dir)
    {
        AgentBase a = director.Pool.Get(AgentKind.Car);
        Car c = a as Car;
        if (c == null)
        {
            director.Pool.Release(a.gameObject);
            return null;
        }
        c.Prepare(lane, s, speed);
        c.Activate(world, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg);
        if (Spawned != null) Spawned(c);
        return c;
    }

}
