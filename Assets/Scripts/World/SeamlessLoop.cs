using UnityEngine;

public sealed class SeamlessLoop
{
    readonly AudioSource[] src = new AudioSource[2];
    readonly AudioClip clip;
    float a, b, c;
    int cur;
    bool xfading;
    bool playing;

    public bool Playing { get { return playing; } }

    public SeamlessLoop(Transform parent, AudioClip clip)
    {
        this.clip = clip;
        for (int i = 0; i < 2; i++)
        {
            GameObject go = new GameObject("WeatherLoop" + i);
            go.transform.SetParent(parent, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 0f;
            s.priority = 100;
            s.clip = clip;
            s.volume = 0f;
            src[i] = s;
        }
        FindBody(clip, out a, out b);
        c = Mathf.Min(1.5f, (b - a) * 0.3f);
    }

    static void FindBody(AudioClip clip, out float start, out float end)
    {
        start = 0f;
        end = clip.length;
        int ch = clip.channels;
        int freq = clip.frequency;
        int win = Mathf.Max(1, freq / 20);
        int n = clip.samples / win;
        bool ok = n >= 8 && clip.length <= 180f && clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.loadState == AudioDataLoadState.Loaded;
        float[] data = null;
        if (ok)
        {
            data = new float[clip.samples * ch];
            ok = clip.GetData(data, 0);
        }
        if (!ok)
        {
            start = Mathf.Min(0.05f, clip.length * 0.1f);
            end = clip.length * 0.85f;
            return;
        }
        float[] rms = new float[n];
        float[] sorted = new float[n];
        for (int w = 0; w < n; w++)
        {
            double sum = 0.0;
            int i0 = w * win * ch;
            int i1 = i0 + win * ch;
            for (int i = i0; i < i1; i++) sum += data[i] * data[i];
            rms[w] = Mathf.Sqrt((float)(sum / (win * ch)));
            sorted[w] = rms[w];
        }
        System.Array.Sort(sorted);
        float thr = sorted[n / 2] * 0.75f;
        int first = 0, last = n - 1;
        while (first < n - 1 && rms[first] < thr) first++;
        while (last > first && rms[last] < thr) last--;
        start = first * win / (float)freq + 0.05f;
        end = (last + 1) * win / (float)freq - 0.1f;
        if (end - start < 1f)
        {
            start = 0f;
            end = clip.length;
        }
    }

    public void Start(float volume)
    {
        if (playing) return;
        playing = true;
        xfading = false;
        cur = 0;
        float span = Mathf.Max(0f, b - c - a - 0.1f);
        float t = a + Random.value * span;
        src[0].time = Mathf.Clamp(t, 0f, Mathf.Max(0f, clip.length - 0.05f));
        src[0].volume = volume;
        src[0].Play();
    }

    public void Stop()
    {
        if (!playing) return;
        playing = false;
        xfading = false;
        src[0].Stop();
        src[1].Stop();
    }

    public void Update(float volume)
    {
        if (!playing) return;
        if (AudioListener.pause || Time.timeScale <= 0f) return;
        AudioSource s = src[cur];
        if (!xfading)
        {
            s.volume = volume;
            if (!s.isPlaying || s.time >= b - c)
            {
                AudioSource n = src[1 - cur];
                n.time = a;
                n.volume = 0f;
                n.Play();
                xfading = true;
            }
            return;
        }
        AudioSource o = src[1 - cur];
        float p = c > 0.01f ? Mathf.Clamp01((s.time - (b - c)) / c) : 1f;
        if (!s.isPlaying) p = 1f;
        float k = p * Mathf.PI * 0.5f;
        s.volume = volume * Mathf.Cos(k);
        o.volume = volume * Mathf.Sin(k);
        if (p >= 1f || s.time >= b)
        {
            s.Stop();
            cur = 1 - cur;
            xfading = false;
            o.volume = volume;
        }
    }
}
