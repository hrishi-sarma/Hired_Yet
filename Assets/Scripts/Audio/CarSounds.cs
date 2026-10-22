using UnityEngine;

/// <summary>Catalog-driven car audio: horn clips, generated accel/decel engine variants, per-car engine voice.</summary>
public static class CarSounds
{
    public const float EngineRange = 35f;
    public const int MaxEngines = 8;
    public static int LiveEngines;
    public static long GeneratedBytes;

    static AudioClip[] horns, engine;
    static bool hornsDone, engineDone;

    public static AudioClip[] Horns()
    {
        if (hornsDone) return horns;
        SoundCatalog c = SoundCatalog.Instance;
        if (c == null) return null;
        hornsDone = true;
        System.Collections.Generic.List<SoundEntry> l = c.Query("cars", "horns", null);
        if (l.Count == 0) return null;
        horns = new AudioClip[l.Count];
        for (int i = 0; i < l.Count; i++) horns[i] = l[i].clip;
        return horns;
    }

    /// <summary>[0]=idle, [1]=accel, [2]=decel (generated from idle, cached). Null when no idle clip exists.</summary>
    public static AudioClip[] Engine()
    {
        if (engineDone) return engine;
        SoundCatalog c = SoundCatalog.Instance;
        if (c == null) return null;
        engineDone = true;
        System.Collections.Generic.List<SoundEntry> l = c.Query("cars", "engines", "idle");
        if (l.Count == 0) l = c.Query("cars", "engines", null);
        if (l.Count == 0) return null;
        AudioClip idle = l[0].clip;
        AudioClip acc = Resample(idle, 1.5f, 1.15f, "_accel");
        AudioClip dec = Resample(idle, 0.75f, 0.8f, "_decel");
        engine = new AudioClip[] { idle, acc != null ? acc : idle, dec != null ? dec : idle };
        return engine;
    }

    static AudioClip Resample(AudioClip src, float rate, float gain, string suffix)
    {
        try
        {
            int ch = src.channels;
            int frames = Mathf.Min(src.samples, (int)(src.frequency * 6f));
            if (frames < 64) return null;
            float[] d = new float[frames * ch];
            if (!src.GetData(d, 0)) return null;
            int outFrames = Mathf.Max(32, (int)(frames / rate));
            float[] o = new float[outFrames];
            for (int i = 0; i < outFrames; i++)
            {
                float p = i * rate;
                int a = Mathf.Min((int)p, frames - 1), b = Mathf.Min(a + 1, frames - 1);
                float f = p - a;
                float m = 0f;
                for (int k = 0; k < ch; k++) m += Mathf.Lerp(d[a * ch + k], d[b * ch + k], f);
                o[i] = Mathf.Clamp(m / ch * gain, -1f, 1f);
            }
            AudioClip r = AudioClip.Create(src.name + suffix, outFrames, 1, src.frequency, false);
            GeneratedBytes += (long)outFrames * 4;
            r.SetData(o, 0);
            return r;
        }
        catch (System.Exception)
        {
            return null;
        }
    }
}

/// <summary>Looping engine source for one car; exists only while the car is near the listener.</summary>
public sealed class CarEngineVoice
{
    AudioSource src;
    int state = -1;
    float holdUntil, vol;

    public void Tick(Transform t, float speed, float accel)
    {
        if (AudioDirector.Player == null || !AudioDirector.WorldActive) { Stop(); return; }
        float r = CarSounds.EngineRange;
        if ((t.position - AudioDirector.Focus).sqrMagnitude > r * r) { Stop(); return; }
        if (src == null || !src.isPlaying)
        {
            if (CarSounds.LiveEngines >= CarSounds.MaxEngines && (src == null || src.clip == null)) return;
            AudioClip[] e = CarSounds.Engine();
            if (e == null) return;
            if (src == null)
            {
                src = t.gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                SpatialAudio.Configure(src, 3f, r);
                src.priority = 160;
            }
            if (src.clip == null) CarSounds.LiveEngines++;
            state = -1;
            vol = 0f;
            src.volume = 0f;
        }
        int want = speed < 0.4f ? 0 : (accel > 0.3f ? 1 : (accel < -0.5f ? 2 : 0));
        if (state != want && Time.time >= holdUntil)
        {
            AudioClip c = CarSounds.Engine()[want];
            if (src.clip != c)
            {
                src.clip = c;
                src.Play();
                src.time = Random.value * Mathf.Max(0f, c.length - 0.05f);
            }
            else if (!src.isPlaying) src.Play();
            state = want;
            holdUntil = Time.time + 0.6f;
        }
        src.pitch = Mathf.Lerp(0.9f, 1.3f, Mathf.Clamp01(speed / 15f));
        float target = 0.25f + 0.35f * Mathf.Clamp01(speed / 12f);
        vol = Mathf.MoveTowards(vol, target, Time.deltaTime * 0.8f);
        src.volume = vol * AudioDirector.Volume("cars");
    }

    public void Stop()
    {
        if (src == null) return;
        if (src.clip != null) CarSounds.LiveEngines = Mathf.Max(0, CarSounds.LiveEngines - 1);
        src.Stop();
        src.clip = null;
        state = -1;
    }
}
