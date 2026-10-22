using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SoundEntry
{
    public AudioClip clip;
    public string category;
    public string group;
    public string[] tags;

    public bool Has(string tag)
    {
        if (tags == null) return false;
        for (int i = 0; i < tags.Length; i++)
            if (tags[i] == tag) return true;
        return false;
    }
}

public class SoundCatalog : ScriptableObject
{
    public SoundEntry[] entries = new SoundEntry[0];

    static SoundCatalog instance;
    static readonly List<SoundEntry> none = new List<SoundEntry>(0);
    Dictionary<string, List<SoundEntry>> index;

    public static SoundCatalog Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<SoundCatalog>("SoundCatalog");
            return instance;
        }
    }

    void OnEnable()
    {
        index = null;
    }

    static string Key(string category, string group)
    {
        return (category ?? "").ToLowerInvariant() + "/" + (group ?? "").ToLowerInvariant();
    }

    void Build()
    {
        index = new Dictionary<string, List<SoundEntry>>();
        for (int i = 0; i < entries.Length; i++)
        {
            SoundEntry e = entries[i];
            if (e == null || e.clip == null) continue;
            string k = Key(e.category, e.group);
            List<SoundEntry> l;
            if (!index.TryGetValue(k, out l))
            {
                l = new List<SoundEntry>();
                index[k] = l;
            }
            l.Add(e);
        }
    }

    public List<SoundEntry> Get(string category, string group = "")
    {
        if (index == null) Build();
        List<SoundEntry> l;
        return index.TryGetValue(Key(category, group), out l) ? l : none;
    }

    public List<SoundEntry> Query(string category, string group, string tag)
    {
        List<SoundEntry> r = new List<SoundEntry>();
        string c = category.ToLowerInvariant();
        string g = group == null ? null : group.ToLowerInvariant();
        for (int i = 0; i < entries.Length; i++)
        {
            SoundEntry e = entries[i];
            if (e == null || e.clip == null || e.category != c) continue;
            if (g != null && e.group != g) continue;
            if (tag != null && !e.Has(tag)) continue;
            r.Add(e);
        }
        return r;
    }

    public SoundEntry Find(string category, string group, string tag)
    {
        List<SoundEntry> l = Query(category, group, tag);
        return l.Count > 0 ? l[0] : null;
    }

    public static AudioClip Pick(List<SoundEntry> list, ref int last)
    {
        int n = list == null ? 0 : list.Count;
        if (n == 0) return null;
        int i = UnityEngine.Random.Range(0, n);
        if (n > 1 && i == last) i = (i + 1 + UnityEngine.Random.Range(0, n - 1)) % n;
        last = i;
        return list[i].clip;
    }

    public static AudioClip PickClip(AudioClip[] list, ref int last)
    {
        int n = list == null ? 0 : list.Length;
        if (n == 0) return null;
        int i = UnityEngine.Random.Range(0, n);
        if (n > 1 && i == last) i = (i + 1 + UnityEngine.Random.Range(0, n - 1)) % n;
        last = i;
        return list[i];
    }
}
