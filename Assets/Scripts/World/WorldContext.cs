using System.Collections.Generic;
using UnityEngine;

public class WorldContext
{
    public readonly WorldSettings settings;
    public readonly Transform root;
    public readonly WorldGenerator generator;
    public readonly OccupancyGrid occupancy;
    public readonly float[,] heights;
    public ZoneType[,] zones;
    public Terrain terrain;
    public ZoneMap zoneMap;
    public RoadNetwork roads;
    public List<LotInfo> lots = new List<LotInfo>();

    readonly Dictionary<PlacementLayer, Transform> layerRoots = new Dictionary<PlacementLayer, Transform>();

    public WorldContext(WorldSettings settings, Transform root, WorldGenerator generator)
    {
        this.settings = settings;
        this.root = root;
        this.generator = generator;
        occupancy = new OccupancyGrid(settings.worldSize, settings.spatialCellSize);
        heights = new float[settings.heightmapResolution, settings.heightmapResolution];

        foreach (PlacementLayer layer in System.Enum.GetValues(typeof(PlacementLayer)))
        {
            GameObject go = new GameObject(LayerName(layer));
            go.transform.SetParent(root, false);
            layerRoots[layer] = go.transform;
        }
    }

    static string LayerName(PlacementLayer layer)
    {
        switch (layer)
        {
            case PlacementLayer.Road: return "Roads";
            case PlacementLayer.Sidewalk: return "Sidewalks";
            case PlacementLayer.Lot: return "Lots";
            case PlacementLayer.Building: return "Buildings";
            case PlacementLayer.Prop: return "Props";
        }
        return "Trees";
    }

    public int Res
    {
        get { return settings.heightmapResolution; }
    }

    public float Size
    {
        get { return settings.worldSize; }
    }

    public float TexelSize
    {
        get { return settings.worldSize / (settings.heightmapResolution - 1); }
    }

    public Transform LayerRoot(PlacementLayer layer)
    {
        return layerRoots[layer];
    }

    public WorldRng Rng(string channel)
    {
        return new WorldRng(settings.seed, channel);
    }

    public float HeightAt(float x, float z)
    {
        float max = Res - 1.001f;
        float fx = Mathf.Clamp((x + Size * 0.5f) / TexelSize, 0f, max);
        float fz = Mathf.Clamp((z + Size * 0.5f) / TexelSize, 0f, max);
        int xi = (int)fx;
        int zi = (int)fz;
        float tx = fx - xi;
        float tz = fz - zi;
        float h00 = heights[zi, xi];
        float h10 = heights[zi, xi + 1];
        float h01 = heights[zi + 1, xi];
        float h11 = heights[zi + 1, xi + 1];
        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz) * settings.maxTerrainHeight;
    }

    public Vector3 NormalAt(float x, float z)
    {
        float d = TexelSize;
        float hl = HeightAt(x - d, z);
        float hr = HeightAt(x + d, z);
        float hd = HeightAt(x, z - d);
        float hu = HeightAt(x, z + d);
        return new Vector3(hl - hr, 2f * d, hd - hu).normalized;
    }

    public void Flatten(Rect rectXZ, float heightMeters, float blend)
    {
        float target = Mathf.Clamp01(heightMeters / settings.maxTerrainHeight);
        float texel = TexelSize;
        float half = Size * 0.5f;
        float pad = Mathf.Max(0f, blend);
        int x0 = Mathf.Max(0, Mathf.FloorToInt((rectXZ.xMin - pad + half) / texel));
        int x1 = Mathf.Min(Res - 1, Mathf.CeilToInt((rectXZ.xMax + pad + half) / texel));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((rectXZ.yMin - pad + half) / texel));
        int z1 = Mathf.Min(Res - 1, Mathf.CeilToInt((rectXZ.yMax + pad + half) / texel));
        for (int zi = z0; zi <= z1; zi++)
        {
            float wz = zi * texel - half;
            float dz = Mathf.Max(Mathf.Max(rectXZ.yMin - wz, 0f), wz - rectXZ.yMax);
            for (int xi = x0; xi <= x1; xi++)
            {
                float wx = xi * texel - half;
                float dx = Mathf.Max(Mathf.Max(rectXZ.xMin - wx, 0f), wx - rectXZ.xMax);
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                float t;
                if (pad <= 0f) t = d <= 0f ? 1f : 0f;
                else t = 1f - Mathf.SmoothStep(0f, 1f, d / pad);
                if (t <= 0f) continue;
                heights[zi, xi] = Mathf.Lerp(heights[zi, xi], target, t);
            }
        }
    }
}
