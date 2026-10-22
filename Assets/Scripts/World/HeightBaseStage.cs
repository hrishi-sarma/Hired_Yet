using UnityEngine;

[AddComponentMenu("Procedural World/Stages/1 Terrain Heights")]
public class HeightBaseStage : WorldStage
{
    [Header("Rolling Hills (normalized height 0..1)")]
    [Range(0f, 0.5f)] public float baseLevel = 0.12f;
    [Range(0f, 0.3f)] public float hillAmplitude = 0.05f;
    public float hillFrequency = 0.004f;
    [Range(1, 8)] public int hillOctaves = 5;
    public float lacunarity = 2f;
    [Range(0.1f, 0.9f)] public float gain = 0.5f;
    [Range(0f, 0.02f)] public float detailAmplitude = 0.003f;
    public float detailFrequency = 0.05f;

    [Header("Domain Warp")]
    public float warpStrength = 60f;
    public float warpFrequency = 0.003f;

    [Header("Mountain Ring")]
    public float mountainWidth = 240f;
    [Range(0f, 1f)] public float mountainHeightMin = 0.45f;
    [Range(0f, 1f)] public float mountainHeightMax = 0.95f;
    public float ridgeFrequency = 0.006f;
    [Range(1, 8)] public int ridgeOctaves = 5;
    public float edgeWarp = 60f;
    public AnimationCurve mountainProfile = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.3f), new Keyframe(0.5f, 0.28f), new Keyframe(1f, 1f, 2.2f, 0f));

    [Header("Smoothing (thermal erosion)")]
    [Range(0, 40)] public int erosionIterations = 8;
    public float talus = 0.0035f;
    [Range(0f, 1f)] public float erosionRate = 0.4f;

    [Header("Test Plateau (flat area at the world center, set size 0 to disable)")]
    public float testPlateauSize = 160f;
    public float testPlateauBlend = 60f;

    public override int Order
    {
        get { return StageOrder.HeightBase; }
    }

    public override string StageName
    {
        get { return "Terrain Heights"; }
    }

    public override void Run(WorldContext c)
    {
        WorldRng rng = c.Rng("terrain");
        Vector2 offHill = new Vector2(rng.Range(2000f, 9000f), rng.Range(2000f, 9000f));
        Vector2 offWarp = new Vector2(rng.Range(2000f, 9000f), rng.Range(2000f, 9000f));
        Vector2 offRidge = new Vector2(rng.Range(2000f, 9000f), rng.Range(2000f, 9000f));
        Vector2 offEdge = new Vector2(rng.Range(2000f, 9000f), rng.Range(2000f, 9000f));
        Vector2 offDetail = new Vector2(rng.Range(2000f, 9000f), rng.Range(2000f, 9000f));

        int res = c.Res;
        float half = c.Size * 0.5f;
        float texel = c.TexelSize;
        float width = Mathf.Max(1f, mountainWidth);

        for (int zi = 0; zi < res; zi++)
        {
            float wz = zi * texel - half;
            for (int xi = 0; xi < res; xi++)
            {
                float wx = xi * texel - half;
                Vector2 w = NoiseLib.Warp(wx, wz, warpStrength, warpFrequency, offWarp);

                float hills = NoiseLib.Fbm(w.x * hillFrequency + offHill.x, w.y * hillFrequency + offHill.y, hillOctaves, lacunarity, gain);
                float detail = NoiseLib.Fbm(wx * detailFrequency + offDetail.x, wz * detailFrequency + offDetail.y, 3, 2f, 0.5f);

                float ax2 = wx * wx;
                float az2 = wz * wz;
                float radius = Mathf.Sqrt(Mathf.Sqrt(ax2 * ax2 + az2 * az2));
                float edge = half - radius;
                edge += (NoiseLib.Fbm(wx * 0.004f + offEdge.x, wz * 0.004f + offEdge.y, 3, 2f, 0.5f) - 0.5f) * 2f * edgeWarp;
                float m = Mathf.Clamp01(1f - edge / width);
                float profile = mountainProfile.Evaluate(m);

                float ridge = Mathf.Clamp01(NoiseLib.Ridged(w.x * ridgeFrequency + offRidge.x, w.y * ridgeFrequency + offRidge.y, ridgeOctaves, 2f, 0.5f) * 1.5f);
                float peak = Mathf.Lerp(mountainHeightMin, mountainHeightMax, ridge);

                float h = baseLevel + (hills - 0.5f) * 2f * hillAmplitude + (detail - 0.5f) * 2f * detailAmplitude + profile * peak;
                c.heights[zi, xi] = Mathf.Clamp01(h);
            }
        }

        Thermal(c.heights, res);

        if (testPlateauSize > 0f)
        {
            float s = testPlateauSize * 0.5f;
            c.Flatten(new Rect(-s, -s, s * 2f, s * 2f), baseLevel * c.settings.maxTerrainHeight, testPlateauBlend);
        }
    }

    void Thermal(float[,] h, int res)
    {
        if (erosionIterations <= 0) return;
        float[,] delta = new float[res, res];
        int[] dx = { 1, -1, 0, 0 };
        int[] dz = { 0, 0, 1, -1 };
        for (int it = 0; it < erosionIterations; it++)
        {
            System.Array.Clear(delta, 0, delta.Length);
            for (int z = 1; z < res - 1; z++)
            {
                for (int x = 1; x < res - 1; x++)
                {
                    float hc = h[z, x];
                    for (int k = 0; k < 4; k++)
                    {
                        float diff = hc - h[z + dz[k], x + dx[k]];
                        if (diff <= talus) continue;
                        float amount = erosionRate * 0.25f * (diff - talus);
                        delta[z, x] -= amount;
                        delta[z + dz[k], x + dx[k]] += amount;
                    }
                }
            }
            for (int z = 1; z < res - 1; z++)
                for (int x = 1; x < res - 1; x++)
                    h[z, x] = Mathf.Clamp01(h[z, x] + delta[z, x]);
        }
    }
}
