using System.Collections.Generic;
using UnityEngine;

public class SoundPool
{
    readonly Transform root;
    readonly int max;
    readonly List<AudioSource> sources = new List<AudioSource>();
    readonly List<int> priority = new List<int>();
    readonly List<float> started = new List<float>();

    public SoundPool(Transform root, int max)
    {
        this.root = root;
        this.max = Mathf.Max(1, max);
    }

    public int Active
    {
        get
        {
            int n = 0;
            for (int i = 0; i < sources.Count; i++)
                if (sources[i] != null && sources[i].isPlaying) n++;
            return n;
        }
    }

    int Acquire(int prio)
    {
        for (int i = 0; i < sources.Count; i++)
            if (sources[i] != null && !sources[i].isPlaying) return i;
        if (sources.Count < max)
        {
            GameObject go = new GameObject("Voice" + sources.Count);
            go.transform.SetParent(root, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.rolloffMode = AudioRolloffMode.Linear;
            sources.Add(s);
            priority.Add(0);
            started.Add(0f);
            return sources.Count - 1;
        }
        int best = -1;
        for (int i = 0; i < sources.Count; i++)
        {
            if (priority[i] > prio) continue;
            if (best < 0 || priority[i] < priority[best] || (priority[i] == priority[best] && started[i] < started[best])) best = i;
        }
        return best;
    }

    public AudioSource Play(AudioClip clip, Vector3 pos, string category, float volume, float pitch, int prio, float minDist, float maxDist, bool spatial)
    {
        if (clip == null) return null;
        if (spatial && (pos - AudioDirector.Focus).sqrMagnitude > maxDist * maxDist) return null;
        int slot = Acquire(prio);
        if (slot < 0) return null;
        AudioSource s = sources[slot];
        s.Stop();
        s.transform.position = pos;
        s.clip = clip;
        s.loop = false;
        s.volume = Mathf.Clamp01(volume * AudioDirector.Volume(category));
        s.pitch = Mathf.Max(0.05f, pitch);
        if (spatial) SpatialAudio.Configure(s, minDist, maxDist);
        else
        {
            s.spatialBlend = 0f;
            s.minDistance = minDist;
            s.maxDistance = Mathf.Max(maxDist, minDist + 1f);
        }
        s.Play();
        priority[slot] = prio;
        started[slot] = Time.unscaledTime;
        return s;
    }

    public void StopAll()
    {
        for (int i = 0; i < sources.Count; i++)
            if (sources[i] != null) sources[i].Stop();
    }
}
