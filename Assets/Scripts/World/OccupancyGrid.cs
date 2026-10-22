using System.Collections.Generic;
using UnityEngine;

public class OccupancyGrid
{
    public const float Epsilon = 0.01f;

    readonly float cell;
    readonly float half;
    readonly int n;
    readonly List<SpawnRecord>[] buckets;
    readonly List<SpawnRecord> records = new List<SpawnRecord>();
    int stampCounter;

    public OccupancyGrid(float worldSize, float cellSize)
    {
        cell = Mathf.Max(1f, cellSize);
        half = worldSize * 0.5f;
        n = Mathf.CeilToInt(worldSize / cell) + 1;
        buckets = new List<SpawnRecord>[n * n];
    }

    public List<SpawnRecord> Records
    {
        get { return records; }
    }

    int Index(float v)
    {
        return Mathf.Clamp(Mathf.FloorToInt((v + half) / cell), 0, n - 1);
    }

    static bool Pair(PlacementLayer a, PlacementLayer b, PlacementLayer x, PlacementLayer y)
    {
        return (a == x && b == y) || (a == y && b == x);
    }

    public static bool Conflicts(PlacementLayer a, PlacementLayer b)
    {
        if (Pair(a, b, PlacementLayer.Lot, PlacementLayer.Building)) return false;
        if (Pair(a, b, PlacementLayer.Lot, PlacementLayer.Prop)) return false;
        if (Pair(a, b, PlacementLayer.Lot, PlacementLayer.Tree)) return false;
        if (Pair(a, b, PlacementLayer.Prop, PlacementLayer.Sidewalk)) return false;
        return true;
    }

    public bool IsFree(OrientedBox box, PlacementLayer layer, float padding)
    {
        OrientedBox test = padding != 0f ? box.Expanded(padding) : box;
        Rect r = test.Aabb;
        int x0 = Index(r.xMin);
        int x1 = Index(r.xMax);
        int z0 = Index(r.yMin);
        int z1 = Index(r.yMax);
        stampCounter++;
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                List<SpawnRecord> list = buckets[z * n + x];
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    SpawnRecord rec = list[i];
                    if (rec.stamp == stampCounter) continue;
                    rec.stamp = stampCounter;
                    if (!Conflicts(layer, rec.layer)) continue;
                    if (OrientedBox.Overlap(test, rec.box, Epsilon)) return false;
                }
            }
        }
        return true;
    }

    public void Add(OrientedBox box, PlacementLayer layer, GameObject owner)
    {
        SpawnRecord rec = new SpawnRecord(box, layer, owner);
        records.Add(rec);
        Rect r = box.Aabb;
        int x0 = Index(r.xMin);
        int x1 = Index(r.xMax);
        int z0 = Index(r.yMin);
        int z1 = Index(r.yMax);
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int id = z * n + x;
                if (buckets[id] == null) buckets[id] = new List<SpawnRecord>();
                buckets[id].Add(rec);
            }
        }
    }

    public bool TryReserve(OrientedBox box, PlacementLayer layer, float padding, GameObject owner)
    {
        if (!IsFree(box, layer, padding)) return false;
        Add(box, layer, owner);
        return true;
    }
}
