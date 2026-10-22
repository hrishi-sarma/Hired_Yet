using UnityEngine;

/// <summary>Streaming speech clean-up for 16 kHz mono audio: DC/rumble removal, adaptive noise gate, automatic gain, soft limiter.</summary>
public sealed class AudioClean
{
    const int Frame = 320;              // 20 ms at 16 kHz
    const float HpA = 0.965f;           // ~90 Hz high-pass
    const float Target = 0.12f;         // speech RMS target (about -18 dBFS)
    const float MaxGain = 24f, MinGain = 0.8f;

    readonly float[] fr = new float[Frame];
    int fn;
    float hpX, hpY;
    float floor = -1f, gain = 1f, gate = 0.3f;
    int hang;

    public short[] Process(float[] src, int count)
    {
        short[] o = new short[(fn + count) / Frame * Frame];
        int n = 0;
        for (int i = 0; i < count; i++)
        {
            float x = src[i];
            hpY = HpA * (hpY + x - hpX);
            hpX = x;
            fr[fn++] = hpY;
            if (fn == Frame) { Flush(o, n); n += Frame; fn = 0; }
        }
        return o;
    }

    void Flush(short[] o, int at)
    {
        float e = 0f;
        for (int i = 0; i < Frame; i++) e += fr[i] * fr[i];
        float rms = Mathf.Sqrt(e / Frame);

        if (floor < 0f) floor = Mathf.Max(rms, 0.0005f);
        else if (rms < floor) floor = floor * 0.9f + rms * 0.1f;
        else floor = Mathf.Min(floor * 1.001f + 1e-6f, rms);
        floor = Mathf.Clamp(floor, 0.0005f, 0.03f);

        bool speech = rms > floor * 2.5f + 0.004f;
        if (speech) hang = 10; else if (hang > 0) hang--;
        bool open = hang > 0;

        gate += ((open ? 1f : 0.25f) - gate) * (open ? 0.5f : 0.1f);
        if (speech)
        {
            float want = Mathf.Clamp(Target / Mathf.Max(rms, 1e-4f), MinGain, MaxGain);
            gain += (want - gain) * (want < gain ? 0.4f : 0.03f);
        }

        float g = gain * gate;
        for (int i = 0; i < Frame; i++)
        {
            float v = fr[i] * g;
            float a = Mathf.Abs(v);
            if (a > 0.8f) v = Mathf.Sign(v) * (0.8f + 0.2f * (float)System.Math.Tanh((a - 0.8f) / 0.2f));
            o[at + i] = (short)(Mathf.Clamp(v, -1f, 1f) * 32767f);
        }
    }
}
