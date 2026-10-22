using UnityEngine;

public static class NoiseLib
{
    public static float Fbm(float x, float y, int octaves, float lacunarity, float gain)
    {
        float sum = 0f;
        float amp = 1f;
        float norm = 0f;
        float f = 1f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Mathf.PerlinNoise(x * f + i * 17.3f, y * f + i * 31.7f) * amp;
            norm += amp;
            amp *= gain;
            f *= lacunarity;
        }
        return norm > 0f ? sum / norm : 0f;
    }

    public static float Ridged(float x, float y, int octaves, float lacunarity, float gain)
    {
        float sum = 0f;
        float amp = 1f;
        float norm = 0f;
        float f = 1f;
        float weight = 1f;
        for (int i = 0; i < octaves; i++)
        {
            float n = Mathf.PerlinNoise(x * f + i * 13.1f, y * f + i * 27.9f);
            n = 1f - Mathf.Abs(n * 2f - 1f);
            n *= n;
            n *= weight;
            weight = Mathf.Clamp01(n * 2f);
            sum += n * amp;
            norm += amp;
            amp *= gain;
            f *= lacunarity;
        }
        return norm > 0f ? sum / norm : 0f;
    }

    public static Vector2 Warp(float x, float y, float strength, float frequency, Vector2 offset)
    {
        float wx = Mathf.PerlinNoise(x * frequency + offset.x, y * frequency + offset.y) - 0.5f;
        float wy = Mathf.PerlinNoise(x * frequency + offset.x + 71.3f, y * frequency + offset.y + 19.1f) - 0.5f;
        return new Vector2(x + wx * 2f * strength, y + wy * 2f * strength);
    }
}
