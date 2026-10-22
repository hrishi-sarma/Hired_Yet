using System.Collections.Generic;
using UnityEngine;

public interface IAgent
{
    AgentKind Kind { get; }
    Transform Tr { get; }
    Vector2 Pos2D { get; }
    float Radius { get; }
    Bounds WorldBounds { get; }
    bool Pooled { get; }
    void OnDespawn();
}

public class AgentRegistry
{
    readonly float cell;
    readonly Dictionary<long, List<IAgent>> grid = new Dictionary<long, List<IAgent>>();
    readonly Dictionary<IAgent, long> keys = new Dictionary<IAgent, long>();
    readonly List<IAgent> all = new List<IAgent>();
    readonly int[] counts = new int[2];

    public AgentRegistry(float cellSize)
    {
        cell = Mathf.Max(1f, cellSize);
    }

    public List<IAgent> All
    {
        get { return all; }
    }

    long KeyOf(Vector2 p)
    {
        int cx = Mathf.FloorToInt(p.x / cell);
        int cz = Mathf.FloorToInt(p.y / cell);
        return ((long)cx << 32) | (long)(uint)cz;
    }

    static long KeyOf(int cx, int cz)
    {
        return ((long)cx << 32) | (long)(uint)cz;
    }

    public void Add(IAgent agent)
    {
        if (agent == null || keys.ContainsKey(agent)) return;
        long k = KeyOf(agent.Pos2D);
        List<IAgent> list;
        if (!grid.TryGetValue(k, out list))
        {
            list = new List<IAgent>(4);
            grid[k] = list;
        }
        list.Add(agent);
        keys[agent] = k;
        all.Add(agent);
        counts[(int)agent.Kind]++;
    }

    public void Remove(IAgent agent)
    {
        long k;
        if (agent == null || !keys.TryGetValue(agent, out k)) return;
        List<IAgent> list;
        if (grid.TryGetValue(k, out list)) list.Remove(agent);
        keys.Remove(agent);
        all.Remove(agent);
        counts[(int)agent.Kind]--;
    }

    public void Move(IAgent agent)
    {
        long old;
        if (!keys.TryGetValue(agent, out old)) return;
        long k = KeyOf(agent.Pos2D);
        if (k == old) return;
        List<IAgent> from;
        if (grid.TryGetValue(old, out from)) from.Remove(agent);
        List<IAgent> to;
        if (!grid.TryGetValue(k, out to))
        {
            to = new List<IAgent>(4);
            grid[k] = to;
        }
        to.Add(agent);
        keys[agent] = k;
    }

    public void Query(Vector2 pos, float radius, List<IAgent> results)
    {
        results.Clear();
        int x0 = Mathf.FloorToInt((pos.x - radius) / cell);
        int x1 = Mathf.FloorToInt((pos.x + radius) / cell);
        int z0 = Mathf.FloorToInt((pos.y - radius) / cell);
        int z1 = Mathf.FloorToInt((pos.y + radius) / cell);
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                List<IAgent> list;
                if (!grid.TryGetValue(KeyOf(x, z), out list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    IAgent a = list[i];
                    float rr = radius + a.Radius;
                    if ((a.Pos2D - pos).sqrMagnitude <= rr * rr) results.Add(a);
                }
            }
        }
    }

    public int Count(AgentKind kind)
    {
        return counts[(int)kind];
    }

    public void Clear()
    {
        grid.Clear();
        keys.Clear();
        all.Clear();
        counts[0] = 0;
        counts[1] = 0;
    }
}
