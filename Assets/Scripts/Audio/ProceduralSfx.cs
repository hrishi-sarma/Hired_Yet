using System;
using UnityEngine;

public enum Surface { Concrete, Asphalt, Grass, Dirt, Wood, Indoor, Metal, Water }

/// <summary>Runtime-synthesised one-shot clips (no audio files needed). Everything is mono 44.1 kHz and cached by the caller.</summary>
public static class ProceduralSfx
{
    public const int Rate = 44100;

    // len, aHi, aLo, attack, noiseDecay, noiseGain, thumpHz, thumpDecay, thumpGain, ringHz, ringDecay, ringGain, crackles, bubbles
    static readonly float[][] Steps =
    {
        new[] { .16f, 1f,  .12f, .001f, .025f, .8f, 110f, .04f, .5f,  0f,   0f,   0f,  0, 0 }, // Concrete
        new[] { .16f, .6f, .05f, .002f, .035f, .7f, 85f,  .05f, .6f,  0f,   0f,   0f,  2, 0 }, // Asphalt
        new[] { .22f, .35f,.03f, .020f, .070f, .7f, 70f,  .05f, .25f, 0f,   0f,   0f,  4, 0 }, // Grass
        new[] { .18f, .5f, .04f, .004f, .050f, .6f, 80f,  .05f, .45f, 0f,   0f,   0f,  8, 0 }, // Dirt
        new[] { .22f, .5f, .06f, .001f, .020f, .35f,140f, .06f, .6f,  210f, .09f, .35f, 0, 0 }, // Wood
        new[] { .13f, .25f,.03f, .004f, .035f, .5f, 75f,  .05f, .7f,  0f,   0f,   0f,  0, 0 }, // Indoor
        new[] { .25f, 1f,  .2f,  .001f, .015f, .5f, 120f, .03f, .3f,  640f, .12f, .4f, 0, 0 }, // Metal
        new[] { .35f, .5f, .05f, .010f, .120f, .6f, 0f,   .05f, 0f,   0f,   0f,   0f,  0, 3 }, // Water
    };

    public static AudioClip[] Footsteps(Surface s, int variants)
    {
        AudioClip[] r = new AudioClip[variants];
        for (int i = 0; i < variants; i++) r[i] = Step(s, 1000 + (int)s * 97 + i * 31);
        return r;
    }

    static float J(System.Random r, float v, float amt)
    {
        return v * (1f + ((float)r.NextDouble() * 2f - 1f) * amt);
    }

    static AudioClip Step(Surface s, int seed)
    {
        float[] p = Steps[(int)s];
        System.Random r = new System.Random(seed);
        float len = J(r, p[0], .08f), aHi = p[1], aLo = p[2], att = p[3];
        float nd = J(r, p[4], .15f), ng = p[5], tf = J(r, p[6], .12f), td = p[7], tg = p[8];
        float rf = J(r, p[9], .06f), rd = p[10], rg = p[11];
        int n = (int)(len * Rate);
        float[] d = new float[n];
        float lpA = 0f, lpB = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float x = (float)r.NextDouble() * 2f - 1f;
            lpA += aHi * (x - lpA);
            lpB += aLo * (x - lpB);
            float band = lpA - lpB;
            float env = (att > 0f ? Mathf.Clamp01(t / att) : 1f) * Mathf.Exp(-t / nd);
            float v = band * env * ng;
            if (tg > 0f) v += Mathf.Sin(6.2832f * tf * t * (1f + .5f * Mathf.Exp(-t / .02f))) * Mathf.Exp(-t / td) * tg;
            if (rg > 0f) v += Mathf.Sin(6.2832f * rf * t) * Mathf.Exp(-t / rd) * rg;
            d[i] = v;
        }
        int cr = (int)p[12];
        for (int c = 0; c < cr; c++)
        {
            int st = (int)(r.NextDouble() * n * .8f), cl = (int)(.0015f * Rate);
            float a = .25f + (float)r.NextDouble() * .35f;
            for (int k = 0; k < cl && st + k < n; k++)
                d[st + k] += ((float)r.NextDouble() * 2f - 1f) * a * Mathf.Exp(-k / (cl * .35f));
        }
        int bb = (int)p[13];
        for (int b = 0; b < bb; b++)
        {
            int st = (int)((.03f + b * .07f + (float)r.NextDouble() * .03f) * Rate), bl = (int)(.05f * Rate);
            float f0 = 300f + (float)r.NextDouble() * 200f;
            for (int k = 0; k < bl && st + k < n; k++)
            {
                float t = k / (float)Rate;
                d[st + k] += Mathf.Sin(6.2832f * (f0 * t + 6000f * t * t)) * Mathf.Exp(-t / .02f) * .3f;
            }
        }
        return Make("step_" + s + "_" + seed, d);
    }

    /// <summary>Normalises to 0.9 peak, fades the last 3 ms (no click) and wraps in an AudioClip.</summary>
    public static AudioClip Make(string name, float[] d)
    {
        float peak = .0001f;
        for (int i = 0; i < d.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
        float g = .9f / peak;
        int f = Mathf.Min(d.Length, (int)(.003f * Rate));
        for (int i = 0; i < d.Length; i++)
        {
            d[i] *= g;
            int fromEnd = d.Length - 1 - i;
            if (fromEnd < f) d[i] *= fromEnd / (float)f;
        }
        AudioClip c = AudioClip.Create(name, d.Length, 1, Rate, false);
        c.SetData(d, 0);
        return c;
    }
}
