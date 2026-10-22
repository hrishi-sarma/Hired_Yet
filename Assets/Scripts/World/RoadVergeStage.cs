using UnityEngine;

[AddComponentMenu("Procedural World/Stages/Road Verge")]
public class RoadVergeStage : WorldStage
{
    public float vergeWidth = 14f;
    public float solidWidth = 2.5f;
    [Range(0f, 1f)] public float strength = 0.92f;
    public float edgeNoiseScale = 0.07f;

    public override int Order { get { return StageOrder.Lots + 50; } }
    public override string StageName { get { return "Road Verge"; } }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / Mathf.Max(0.0001f, b - a));
        return t * t * (3f - 2f * t);
    }

    public override void Run(WorldContext c)
    {
        if (c.terrain == null) return;
        TerrainData td = c.terrain.terrainData;
        if (td == null || td.alphamapLayers < 2) return;

        int ar = td.alphamapResolution;
        float size = c.Size;
        float half = size * 0.5f;
        float step = size / (ar - 1);
        float[,] mask = new float[ar, ar];
        float maxBand = vergeWidth * 1.25f;

        System.Collections.Generic.List<SpawnRecord> records = c.occupancy.Records;
        for (int r = 0; r < records.Count; r++)
        {
            SpawnRecord rec = records[r];
            if (rec.layer != PlacementLayer.Road && rec.layer != PlacementLayer.Sidewalk) continue;
            OrientedBox box = rec.box;
            Rect aabb = box.Expanded(maxBand).Aabb;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((aabb.xMin + half) / step), 0, ar - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((aabb.xMax + half) / step), 0, ar - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((aabb.yMin + half) / step), 0, ar - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt((aabb.yMax + half) / step), 0, ar - 1);
            for (int y = y0; y <= y1; y++)
            {
                float wz = y * step - half;
                for (int x = x0; x <= x1; x++)
                {
                    float wx = x * step - half;
                    Vector2 d = new Vector2(wx, wz) - box.center;
                    float lx = Mathf.Abs(Vector2.Dot(d, box.axisX)) - box.half.x;
                    float lz = Mathf.Abs(Vector2.Dot(d, box.axisZ)) - box.half.y;
                    float dist = new Vector2(Mathf.Max(lx, 0f), Mathf.Max(lz, 0f)).magnitude;
                    if (dist >= maxBand) continue;
                    float noise = NoiseLib.Fbm(wx * edgeNoiseScale + 71f, wz * edgeNoiseScale + 133f, 3, 2f, 0.5f);
                    float band = vergeWidth * Mathf.Lerp(0.65f, 1.25f, noise);
                    float w = 1f - Smooth(solidWidth, band, dist);
                    if (w > mask[y, x]) mask[y, x] = w;
                }
            }
        }

        float[,,] a = td.GetAlphamaps(0, 0, ar, ar);
        int layers = a.GetLength(2);
        for (int y = 0; y < ar; y++)
        for (int x = 0; x < ar; x++)
        {
            float m = mask[y, x] * strength;
            if (m <= 0.001f) continue;
            for (int l = 0; l < layers; l++)
            {
                if (l == 1) a[y, x, l] = a[y, x, l] * (1f - m) + m;
                else a[y, x, l] *= 1f - m;
            }
        }
        td.SetAlphamaps(0, 0, a);

        int dw = td.detailWidth;
        int dh = td.detailHeight;
        if (dw > 0 && dh > 0 && td.detailPrototypes != null && td.detailPrototypes.Length > 0)
        {
            int[,] details = td.GetDetailLayer(0, 0, dw, dh, 0);
            for (int y = 0; y < dh; y++)
            {
                int my = Mathf.Clamp(Mathf.RoundToInt(y / (float)(dh - 1) * (ar - 1)), 0, ar - 1);
                for (int x = 0; x < dw; x++)
                {
                    int mx = Mathf.Clamp(Mathf.RoundToInt(x / (float)(dw - 1) * (ar - 1)), 0, ar - 1);
                    float m = mask[my, mx] * strength;
                    if (m > 0.001f) details[y, x] = Mathf.RoundToInt(details[y, x] * (1f - m));
                }
            }
            td.SetDetailLayer(0, 0, 0, details);
        }
    }
}
