using System.Collections.Generic;
using UnityEngine;

public class AmbientZones : MonoBehaviour
{
    public float fadeIn = 6f;
    public float fadeOut = 8f;
    public float bedVolume = 0.6f;
    public float parkVolume = 0.7f;
    public float parkFadeDistance = 30f;
    public float parkFadeSeconds = 3f;
    [Range(0f, 1f)] public float parkDuck = 0.6f;
    public float highwayCityWeight = 0.3f;

    class Layer
    {
        public List<AudioClip> clips;
        public AudioSource src;
        public bool playing;
        public float vol;
        public float target;
        public float baseVol;
        public float seed;
        public float inTime;
        public float outTime;
        public int last = -1;
    }

    class Bed
    {
        public Layer day;
        public Layer night;
        public bool single;
    }

    Bed forest, suburb, city;
    Layer park;
    readonly List<Layer> all = new List<Layer>();
    bool built;
    Bounds parkBounds;
    bool parkFound;
    float nextParkFind;

    static List<AudioClip> Clips(List<SoundEntry> src, string tag, bool wantPark)
    {
        List<AudioClip> r = new List<AudioClip>();
        for (int i = 0; i < src.Count; i++)
        {
            SoundEntry e = src[i];
            if (e.Has("park") != wantPark) continue;
            if (tag == "day" && !e.Has("day")) continue;
            if (tag == "night" && !e.Has("night")) continue;
            if (tag == "neutral" && (e.Has("day") || e.Has("night"))) continue;
            r.Add(e.clip);
        }
        return r;
    }

    Layer NewLayer(List<AudioClip> clips, float vol, float tin, float tout)
    {
        Layer l = new Layer();
        l.clips = clips;
        l.baseVol = vol;
        l.seed = Random.value * 100f;
        l.inTime = tin;
        l.outTime = tout;
        GameObject go = new GameObject("Ambient" + all.Count);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = true;
        s.spatialBlend = 0f;
        s.volume = 0f;
        s.bypassReverbZones = true;
        l.src = s;
        all.Add(l);
        return l;
    }

    Bed MakeBed(SoundCatalog cat, string group)
    {
        List<SoundEntry> src = cat.Get("ambient", group);
        List<AudioClip> day = Clips(src, "day", false);
        List<AudioClip> night = Clips(src, "night", false);
        List<AudioClip> neutral = Clips(src, "neutral", false);
        Bed b = new Bed();
        if (day.Count == 0 && night.Count == 0)
        {
            if (neutral.Count == 0) return null;
            b.single = true;
            b.day = NewLayer(neutral, bedVolume, fadeIn, fadeOut);
            return b;
        }
        if (day.Count == 0) day = night;
        if (night.Count == 0) night = day;
        if (ReferenceEquals(day, night))
        {
            b.single = true;
            b.day = NewLayer(day, bedVolume, fadeIn, fadeOut);
            return b;
        }
        b.day = NewLayer(day, bedVolume, fadeIn, fadeOut);
        b.night = NewLayer(night, bedVolume, fadeIn, fadeOut);
        return b;
    }

    void Build()
    {
        SoundCatalog cat = SoundCatalog.Instance;
        if (cat == null) return;
        built = true;
        forest = MakeBed(cat, "forest");
        suburb = MakeBed(cat, "suburb");
        city = MakeBed(cat, "city");
        List<AudioClip> p = Clips(cat.Get("ambient", "city"), null, true);
        if (p.Count > 0) park = NewLayer(p, parkVolume, parkFadeSeconds, parkFadeSeconds);
    }

    void FindPark()
    {
        nextParkFind = Time.unscaledTime + 10f;
        GameObject go = GameObject.Find("CityPlaza");
        if (go == null) return;
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        parkBounds = b;
        parkFound = true;
    }

    float ParkWeight()
    {
        if (park == null || AudioDirector.Zone != ZoneType.City || AudioDirector.Player == null) return 0f;
        if (!parkFound)
        {
            if (Time.unscaledTime >= nextParkFind) FindPark();
            if (!parkFound) return 0f;
        }
        Vector3 p = AudioDirector.Player.position;
        float dx = Mathf.Max(parkBounds.min.x - p.x, 0f, p.x - parkBounds.max.x);
        float dz = Mathf.Max(parkBounds.min.z - p.z, 0f, p.z - parkBounds.max.z);
        float d = Mathf.Sqrt(dx * dx + dz * dz);
        float w = 1f - Mathf.Clamp01(d / Mathf.Max(1f, parkFadeDistance));
        return w * w * (3f - 2f * w);
    }

    static void SetTargets(Bed b, float w, float night)
    {
        if (b == null) return;
        if (b.single)
        {
            b.day.target = w;
            return;
        }
        b.day.target = w * Mathf.Sqrt(Mathf.Clamp01(1f - night));
        b.night.target = w * Mathf.Sqrt(Mathf.Clamp01(night));
    }

    void Start()
    {
        enabled = true;
    }

    void Update()
    {
        bool active = AudioDirector.WorldActive;
        if (active && !built) Build();
        if (!built) return;

        float wForest = 0f, wSuburb = 0f, wCity = 0f, wPark = 0f;
        if (active)
        {
            switch (AudioDirector.Zone)
            {
                case ZoneType.City: wCity = 1f; break;
                case ZoneType.Suburb: wSuburb = 1f; break;
                case ZoneType.Highway: wCity = highwayCityWeight; break;
                default: wForest = 1f; break;
            }
            wPark = ParkWeight();
            wCity *= 1f - parkDuck * wPark;
        }
        float n = AudioDirector.Night;
        SetTargets(forest, wForest, n);
        SetTargets(suburb, wSuburb, n);
        SetTargets(city, wCity, n);
        if (park != null) park.target = wPark;

        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < all.Count; i++) Step(all[i], dt);
    }

    void Step(Layer l, float dt)
    {
        float t = l.target > l.vol ? l.inTime : l.outTime;
        l.vol = Mathf.MoveTowards(l.vol, l.target, dt / Mathf.Max(0.1f, t));
        if (l.target <= 0f && l.vol <= 0.001f)
        {
            l.vol = 0f;
            if (l.playing)
            {
                l.src.Stop();
                l.src.clip = null;
                l.playing = false;
            }
            return;
        }
        if (!l.playing) Begin(l);
        if (!l.playing) return;
        float sv = l.vol * l.vol * (3f - 2f * l.vol);
        float swell = 0.88f + 0.12f * Mathf.PerlinNoise(Time.unscaledTime * 0.03f, l.seed);
        l.src.volume = Mathf.Clamp01(sv * swell * l.baseVol * AudioDirector.Volume("ambient"));
    }

    void Begin(Layer l)
    {
        if (l.clips == null || l.clips.Count == 0) return;
        int i = Random.Range(0, l.clips.Count);
        if (l.clips.Count > 1 && i == l.last) i = (i + 1) % l.clips.Count;
        l.last = i;
        AudioClip c = l.clips[i];
        if (c == null) return;
        l.src.clip = c;
        l.src.volume = 0f;
        l.src.time = Random.value * c.length * 0.9f;
        l.src.Play();
        l.playing = true;
    }
}
