using UnityEngine;

[System.Serializable]
public class TerrainPainterSettings
{
    [Tooltip("Slope where soil starts to appear.")]
    public float dirtSlopeStart = 12f;
    [Tooltip("Slope where exposed rock becomes dominant.")]
    public float rockSlopeStart = 26f;
    public float rockSlopeEnd = 42f;
    [Tooltip("Normalized terrain height at which snow begins. Raise/lower to tune snowline.")]
    [Range(0f, 1f)] public float snowHeight = 0.70f;
    [Range(0.01f, 0.5f)] public float snowBlend = 0.12f;
    public float snowMaxSlope = 54f;
    [Tooltip("Rock becomes more common above this normalized height, even on gentler mountain slopes.")]
    [Range(0f, 1f)] public float mountainRockHeight = 0.44f;
    [Range(0.01f, 0.5f)] public float mountainRockBlend = 0.20f;
    public float patchFrequency = 0.02f;
}

public static class TerrainPainter
{
    static float Step(float a, float b, float v)
    {
        if (b <= a) return v >= b ? 1f : 0f;
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((v - a) / (b - a)));
    }

    public static void Paint(TerrainData td, WorldContext c, TerrainPainterSettings s)
    {
        int ar = td.alphamapResolution;
        float[,,] a = new float[ar, ar, 4];
        float maxH = Mathf.Max(1f, td.size.y);
        for (int y = 0; y < ar; y++)
        {
            float v = y / (float)(ar - 1);
            for (int x = 0; x < ar; x++)
            {
                float u = x / (float)(ar - 1);
                float h = td.GetInterpolatedHeight(u, v) / maxH;
                float slope = td.GetSteepness(u, v);
                float patch = NoiseLib.Fbm(u * c.Size * s.patchFrequency, v * c.Size * s.patchFrequency, 4, 2f, 0.5f);

                // Exposed rock is driven by both slope and altitude, so broad mountain faces
                // do not remain grass-green simply because their local slope is shallow.
                float slopeRock = Step(s.rockSlopeStart, s.rockSlopeEnd, slope);
                float altitudeRock = Step(s.mountainRockHeight, s.mountainRockHeight + s.mountainRockBlend, h) * 0.92f;
                float rock = Mathf.Max(slopeRock, altitudeRock);
                float dirtSlope = Step(s.dirtSlopeStart, s.rockSlopeStart, slope) * 0.72f;
                float dirtPatch = Step(0.55f, 0.78f, patch) * 0.46f;
                float dirt = Mathf.Max(dirtSlope, dirtPatch) * (1f - rock);
                float grass = Mathf.Max(0f, 1f - rock - dirt);

                // Snow is concentrated at high altitude and fades out on very steep cliff faces.
                float snow = Step(s.snowHeight, s.snowHeight + s.snowBlend, h);
                snow *= 1f - Step(s.snowMaxSlope - 8f, s.snowMaxSlope, slope);
                // Keep snow patch edges natural rather than perfectly contouring the heightmap.
                float snowNoise = NoiseLib.Fbm(u * c.Size * 0.035f + 431f, v * c.Size * 0.035f + 917f, 3, 2f, 0.5f);
                snow *= Mathf.Lerp(0.78f, 1f, snowNoise);
                grass *= 1f - snow;
                dirt *= 1f - snow;
                rock *= 1f - snow;

                float sum = grass + dirt + rock + snow;
                if (sum <= 0.0001f) { grass = 1f; sum = 1f; }
                a[y, x, 0] = grass / sum;
                a[y, x, 1] = dirt / sum;
                a[y, x, 2] = rock / sum;
                a[y, x, 3] = snow / sum;
            }
        }
        td.SetAlphamaps(0, 0, a);
    }
}
