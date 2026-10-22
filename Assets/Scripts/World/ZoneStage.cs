using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Procedural World/Stages/2 Zones And Highway")]
public class ZoneStage : WorldStage
{
    [Header("Layout")]
    public float centerDistance = 270f;
    [Range(0f, 45f)] public float angleJitter = 15f;
    public float suburbRadius = 148f;
    public float cityRadius = 142f;
    public float cityGrowth = 85f;
    [Range(0f, 0.6f)] public float edgeNoise = 0.25f;
    public float edgeNoiseFrequency = 0.008f;
    public float edgeWarpStrength = 35f;
    public float edgeWarpFrequency = 0.01f;

    [Header("Mountain / Forest / Open")]
    [Range(0f, 1f)] public float mountainHeight = 0.24f;
    public float forestRingWidth = 150f;
    public float highwayZoneHalfWidth = 14f;
    public float highwayForestCorridor = 90f;
    [Range(0f, 1f)] public float forestPatchThreshold = 0.58f;
    public float forestPatchFrequency = 0.006f;

    [Header("Highway Path")]
    public HighwaySettings highway = new HighwaySettings();

    [Header("Gizmos")]
    public bool showZoneGizmos = true;
    public float gizmoCell = 24f;

    public override int Order
    {
        get { return StageOrder.Zones; }
    }

    public override string StageName
    {
        get { return "Zones And Highway"; }
    }

    public override void Run(WorldContext c)
    {
        WorldRng rng = c.Rng("zones");
        float angle = (45f + 90f * rng.Range(0, 4) + rng.Range(-angleJitter, angleJitter)) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 suburbC = dir * centerDistance;
        Vector2 cityC = -suburbC;
        Vector2 offEdge = new Vector2(rng.Range(2000f, 9000f), rng.Range(2000f, 9000f));
        Vector2 offWarp = new Vector2(rng.Range(2000f, 9000f), rng.Range(2000f, 9000f));
        Vector2 offPatch = new Vector2(rng.Range(2000f, 9000f), rng.Range(2000f, 9000f));

        Vector2 hwStart = suburbC - dir * suburbRadius * highway.endInset;
        Vector2 hwEnd = cityC + dir * cityRadius * highway.endInset;
        HighwayPath path = HighwayPlanner.Plan(c, hwStart, hwEnd, highway, mountainHeight, c.Rng("highway"));

        int res = c.Res;
        float half = c.Size * 0.5f;
        float texel = c.TexelSize;
        float reach = Mathf.Max(highwayForestCorridor, highwayZoneHalfWidth);

        float[,] hw = new float[res, res];
        for (int zi = 0; zi < res; zi++)
            for (int xi = 0; xi < res; xi++)
                hw[zi, xi] = float.MaxValue;
        List<Vector2> pts = path.points;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[i + 1];
            int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach + half) / texel));
            int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach + half) / texel));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach + half) / texel));
            int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + reach + half) / texel));
            for (int zi = z0; zi <= z1; zi++)
            {
                for (int xi = x0; xi <= x1; xi++)
                {
                    float d = HighwayPath.SegmentDistance(new Vector2(xi * texel - half, zi * texel - half), a, b);
                    if (d < hw[zi, xi]) hw[zi, xi] = d;
                }
            }
        }

        float rsMax = suburbRadius * (1f + edgeNoise) + forestRingWidth + 20f;
        float rLow = (cityRadius + cityGrowth) * (1f + edgeNoise * 0.5f) + 20f;
        float cap = Mathf.Min(mountainHeight * 0.85f, c.HeightAt(cityC.x, cityC.y) + 0.02f);
        for (int zi = 0; zi < res; zi++)
        {
            float lz = zi * texel - half;
            for (int xi = 0; xi < res; xi++)
            {
                float hv = c.heights[zi, xi];
                if (hv <= cap) continue;
                float lx = xi * texel - half;
                Vector2 lp = new Vector2(lx, lz);
                float w = Mathf.Clamp01((rLow - Vector2.Distance(lp, cityC)) / 45f);
                if (w <= 0f) continue;
                w *= Mathf.Clamp01((hw[zi, xi] - highwayForestCorridor) / 40f);
                w *= Mathf.Clamp01((Vector2.Distance(lp, suburbC) - rsMax) / 40f);
                w *= Mathf.Clamp01((half - Mathf.Max(Mathf.Abs(lx), Mathf.Abs(lz)) - 70f) / 50f);
                if (w <= 0f) continue;
                w = w * w * (3f - 2f * w);
                c.heights[zi, xi] = Mathf.Lerp(hv, cap, w);
            }
        }

        c.zones = new ZoneType[res, res];
        int[] counts = new int[6];
        for (int zi = 0; zi < res; zi++)
        {
            float wz = zi * texel - half;
            for (int xi = 0; xi < res; xi++)
            {
                float wx = xi * texel - half;
                ZoneType zone;
                if (c.heights[zi, xi] > mountainHeight)
                {
                    zone = ZoneType.Mountain;
                }
                else
                {
                    Vector2 w = NoiseLib.Warp(wx, wz, edgeWarpStrength, edgeWarpFrequency, offWarp);
                    float n = (NoiseLib.Fbm(wx * edgeNoiseFrequency + offEdge.x, wz * edgeNoiseFrequency + offEdge.y, 3, 2f, 0.5f) - 0.5f) * 2f * edgeNoise;
                    float rs = suburbRadius * (1f + n);
                    float rc = cityRadius * (1f - n);
                    float rcx = (cityRadius + cityGrowth) * (1f - n);
                    float ds = Vector2.Distance(w, suburbC);
                    float dc = Vector2.Distance(w, cityC);
                    float hd = hw[zi, xi];
                    if (ds < rs && ds / rs <= dc / rc) zone = ZoneType.Suburb;
                    else if (dc < rc) zone = ZoneType.City;
                    else if (dc < rcx && hd > highwayForestCorridor && ds >= rs + forestRingWidth && Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz)) < half - 70f) zone = ZoneType.City;
                    else if (hd <= highwayZoneHalfWidth) zone = ZoneType.Highway;
                    else if (hd <= highwayForestCorridor || ds < rs + forestRingWidth) zone = ZoneType.Forest;
                    else if (NoiseLib.Fbm(wx * forestPatchFrequency + offPatch.x, wz * forestPatchFrequency + offPatch.y, 3, 2f, 0.5f) > forestPatchThreshold) zone = ZoneType.Forest;
                    else zone = ZoneType.Open;
                }
                c.zones[zi, xi] = zone;
                counts[(int)zone]++;
            }
        }

        c.zoneMap = new ZoneMap(c, suburbC, suburbRadius, cityC, cityRadius + cityGrowth, path);

        float total = res * res;
        Debug.Log("Zones: suburb at " + suburbC + ", city at " + cityC + ", highway " + Mathf.RoundToInt(path.length) + " m (" + path.points.Count + " points). Share: mountain " + Pct(counts[(int)ZoneType.Mountain], total) + ", open " + Pct(counts[(int)ZoneType.Open], total) + ", forest " + Pct(counts[(int)ZoneType.Forest], total) + ", suburb " + Pct(counts[(int)ZoneType.Suburb], total) + ", city " + Pct(counts[(int)ZoneType.City], total) + ", highway " + Pct(counts[(int)ZoneType.Highway], total));
    }

    static string Pct(int count, float total)
    {
        return (count * 100f / total).ToString("0.0") + "%";
    }

    static Color ZoneColor(ZoneType z)
    {
        switch (z)
        {
            case ZoneType.Mountain: return new Color(0.5f, 0.5f, 0.55f, 0.35f);
            case ZoneType.Open: return new Color(0.7f, 0.95f, 0.4f, 0.35f);
            case ZoneType.Forest: return new Color(0.1f, 0.5f, 0.15f, 0.45f);
            case ZoneType.Suburb: return new Color(1f, 0.6f, 0.15f, 0.55f);
            case ZoneType.City: return new Color(0.9f, 0.2f, 0.25f, 0.55f);
        }
        return new Color(1f, 0.95f, 0.1f, 0.8f);
    }

    void OnDrawGizmosSelected()
    {
        if (!showZoneGizmos) return;
        WorldGenerator g = GetComponent<WorldGenerator>();
        if (g == null || g.Context == null || g.Context.zoneMap == null) return;
        WorldContext c = g.Context;
        ZoneMap map = c.zoneMap;
        float cell = Mathf.Max(c.TexelSize, gizmoCell);
        float half = c.Size * 0.5f;
        for (float z = -half + cell * 0.5f; z < half; z += cell)
        {
            for (float x = -half + cell * 0.5f; x < half; x += cell)
            {
                Gizmos.color = ZoneColor(map.ZoneAt(x, z));
                Gizmos.DrawCube(new Vector3(x, c.HeightAt(x, z) + 1f, z), new Vector3(cell, 0.5f, cell));
            }
        }
        List<Vector2> p = map.highway.points;
        Gizmos.color = Color.magenta;
        for (int i = 0; i < p.Count - 1; i++)
        {
            Vector3 a = new Vector3(p[i].x, c.HeightAt(p[i].x, p[i].y) + 3f, p[i].y);
            Vector3 b = new Vector3(p[i + 1].x, c.HeightAt(p[i + 1].x, p[i + 1].y) + 3f, p[i + 1].y);
            Gizmos.DrawLine(a, b);
        }
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(new Vector3(map.suburbCenter.x, c.HeightAt(map.suburbCenter.x, map.suburbCenter.y) + 3f, map.suburbCenter.y), 10f);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(new Vector3(map.cityCenter.x, c.HeightAt(map.cityCenter.x, map.cityCenter.y) + 3f, map.cityCenter.y), 10f);
    }
}
