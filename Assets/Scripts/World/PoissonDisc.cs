using System.Collections.Generic;
using UnityEngine;

public static class PoissonDisc
{
    public static List<Vector2> Generate(Rect area, float radius, System.Random rng, int attempts)
    {
        List<Vector2> points = new List<Vector2>();
        float cell = radius / 1.41421356f;
        int gw = Mathf.Max(1, Mathf.CeilToInt(area.width / cell));
        int gh = Mathf.Max(1, Mathf.CeilToInt(area.height / cell));
        int[] grid = new int[gw * gh];
        for (int i = 0; i < grid.Length; i++) grid[i] = -1;
        List<int> active = new List<int>();
        float r2 = radius * radius;

        Vector2 first = new Vector2(area.xMin + (float)rng.NextDouble() * area.width, area.yMin + (float)rng.NextDouble() * area.height);
        points.Add(first);
        active.Add(0);
        grid[Mathf.Clamp((int)((first.y - area.yMin) / cell), 0, gh - 1) * gw + Mathf.Clamp((int)((first.x - area.xMin) / cell), 0, gw - 1)] = 0;

        while (active.Count > 0)
        {
            int ai = rng.Next(active.Count);
            Vector2 origin = points[active[ai]];
            bool found = false;
            for (int a = 0; a < attempts; a++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = radius * (1f + (float)rng.NextDouble());
                Vector2 cand = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
                if (cand.x < area.xMin || cand.x >= area.xMax || cand.y < area.yMin || cand.y >= area.yMax) continue;
                int cx = Mathf.Clamp((int)((cand.x - area.xMin) / cell), 0, gw - 1);
                int cy = Mathf.Clamp((int)((cand.y - area.yMin) / cell), 0, gh - 1);
                bool ok = true;
                for (int y = Mathf.Max(0, cy - 2); y <= Mathf.Min(gh - 1, cy + 2) && ok; y++)
                {
                    for (int x = Mathf.Max(0, cx - 2); x <= Mathf.Min(gw - 1, cx + 2); x++)
                    {
                        int id = grid[y * gw + x];
                        if (id < 0) continue;
                        if ((points[id] - cand).sqrMagnitude < r2)
                        {
                            ok = false;
                            break;
                        }
                    }
                }
                if (!ok) continue;
                points.Add(cand);
                grid[cy * gw + cx] = points.Count - 1;
                active.Add(points.Count - 1);
                found = true;
                break;
            }
            if (!found)
            {
                active[ai] = active[active.Count - 1];
                active.RemoveAt(active.Count - 1);
            }
        }
        return points;
    }
}
