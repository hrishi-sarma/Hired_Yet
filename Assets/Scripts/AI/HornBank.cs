using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The single authority for car horns. Picks varied clips from CarBehavior.honkClips (or built-in procedural
/// horns when the list is empty) and guarantees two DIFFERENT horns never sound together.
/// </summary>
public static class HornBank
{
    struct Active
    {
        public Car owner;
        public int clip;
        public float until;
    }

    static readonly List<Active> active = new List<Active>(6);
    static AudioClip[] synth;
    static int lastClip = -1;

    public static AudioClip[] Clips(CarBehavior b)
    {
        if (b.honkClips != null)
        {
            for (int i = 0; i < b.honkClips.Length; i++)
                if (b.honkClips[i] != null) return b.honkClips;
        }
        AudioClip[] cat = CarSounds.Horns();
        if (cat != null) return cat;
        if (synth == null) synth = BuildSynth();
        return synth;
    }

    static void Purge()
    {
        for (int i = active.Count - 1; i >= 0; i--)
            if (Time.time >= active[i].until || active[i].owner == null || active[i].owner.Pooled) active.RemoveAt(i);
    }

    public static void End(Car car)
    {
        for (int i = active.Count - 1; i >= 0; i--)
            if (active[i].owner == car) active.RemoveAt(i);
    }

    public static void Reset()
    {
        active.Clear();
        lastClip = -1;
    }

    /// <summary>Returns a clip to play, or null when the horn must stay silent right now.</summary>
    public static AudioClip TryBegin(Car car, CarBehavior b, float dur, ref int carLast)
    {
        AudioClip[] clips = Clips(b);
        int n = clips.Length;
        if (n == 0) return null;
        End(car);
        Purge();
        int pick = -1;
        if (active.Count > 0)
        {
            if (active.Count >= Mathf.Max(1, b.maxSimultaneousHorns)) return null;
            pick = active[0].clip;
        }
        else
        {
            for (int tries = 0; tries < 8; tries++)
            {
                int c = Random.Range(0, n);
                if (clips[c] == null) continue;
                pick = c;
                if (n < 3 || (c != carLast && c != lastClip)) break;
            }
        }
        if (pick < 0 || pick >= n || clips[pick] == null) return null;
        Active a;
        a.owner = car;
        a.clip = pick;
        a.until = Time.time + Mathf.Max(0.2f, dur);
        active.Add(a);
        lastClip = pick;
        carLast = pick;
        return clips[pick];
    }

    static AudioClip[] BuildSynth()
    {
        return new AudioClip[]
        {
            Tone("Horn_Sedan", 420f, 525f, 0.55f, 0.6f),
            Tone("Horn_Hatch", 480f, 600f, 0.4f, 0.5f),
            Tone("Horn_Truck", 190f, 240f, 0.85f, 0.7f),
            Tone("Horn_Auto", 700f, 700f, 0.7f, 0.4f),
            Tone("Horn_Van", 340f, 410f, 0.6f, 0.65f)
        };
    }

    static AudioClip Tone(string name, float f1, float f2, float square, float len)
    {
        int rate = 22050;
        int n = (int)(rate * len);
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Clamp01(t / 0.012f) * Mathf.Clamp01((len - t) / 0.05f);
            float a = Wave(f1, t, square);
            float c = Wave(f2, t, square);
            d[i] = (a + c) * 0.25f * env;
        }
        AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }

    static float Wave(float f, float t, float square)
    {
        float s = Mathf.Sin(2f * Mathf.PI * f * t);
        return Mathf.Sign(s) * square + s * (1f - square);
    }
}
