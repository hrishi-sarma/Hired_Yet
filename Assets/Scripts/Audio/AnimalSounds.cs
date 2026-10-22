using System.Collections.Generic;
using UnityEngine;

/// <summary>Fills AnimalSoundSet slots from the SoundCatalog (Animals/<group>), only where the Inspector left them empty.</summary>
public static class AnimalSounds
{
    static readonly HashSet<AnimalSoundSet> done = new HashSet<AnimalSoundSet>();
    static readonly HashSet<AnimalSoundSet> calm = new HashSet<AnimalSoundSet>();   // cats: single meows, long gaps
    static float lastCalmT = -99f;

    /// <summary>Cats (and any set filled from a "cat*" group) use calm pacing instead of 2-5 call bursts.</summary>
    public static bool IsCalm(AnimalSoundSet s) { return s != null && calm.Contains(s); }

    /// <summary>Seconds that must pass between any two calm-animal calls anywhere (so several cats never meow over each other).</summary>
    public const float CalmGlobalGap = 4f;
    /// <summary>Minimum cooldown between a single cat's meows.</summary>
    public const float CalmMinCooldown = 14f;
    public static bool CalmReady() { return Time.time - lastCalmT >= CalmGlobalGap; }
    public static void CalmUsed() { lastCalmT = Time.time; }

    /// <summary>True for clip names containing the word "pet" or "pat" (pet, pat, petting, patting, cat_pat_01...).</summary>
    public static bool IsPat(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string[] parts = System.Text.RegularExpressions.Regex.Split(name.ToLowerInvariant(), "[^a-z]+");
        for (int i = 0; i < parts.Length; i++)
        {
            string w = parts[i];
            if (w == "pet" || w == "pets" || w == "petting" || w == "pat" || w == "pats" || w == "patting") return true;
        }
        return false;
    }

    static AudioClip[] Split(AudioClip[] src, List<AudioClip> pats)
    {
        if (src == null) return new AudioClip[0];
        List<AudioClip> keep = new List<AudioClip>();
        for (int i = 0; i < src.Length; i++)
        {
            if (src[i] == null) continue;
            if (IsPat(src[i].name)) pats.Add(src[i]); else keep.Add(src[i]);
        }
        return keep.ToArray();
    }

    public static void Fill(AnimalSoundSet set, string group, string tagOnly = null, string tagNot = null)
    {
        if (set == null || done.Contains(set)) return;
        done.Add(set);
        if (group != null && group.ToLowerInvariant().StartsWith("cat")) calm.Add(set);
        // pat/pet clips (even Inspector-assigned ones) are moved out of the ambient/reaction pools
        List<AudioClip> pats = new List<AudioClip>();
        set.ambientSounds = Split(set.ambientSounds, pats);
        set.reactionSounds = Split(set.reactionSounds, pats);
        if (set.patSounds != null) for (int i = 0; i < set.patSounds.Length; i++) if (set.patSounds[i] != null && !pats.Contains(set.patSounds[i])) pats.Add(set.patSounds[i]);
        bool hasPat = pats.Count > 0;
        bool hasAmb = set.ambientSounds != null && set.ambientSounds.Length > 0;
        bool hasRea = set.reactionSounds != null && set.reactionSounds.Length > 0;
        if (hasAmb && hasRea) { set.patSounds = pats.ToArray(); return; }
        SoundCatalog cat = SoundCatalog.Instance;
        if (cat == null) return;
        List<SoundEntry> all = cat.Query("animals", group, null);
        List<SoundEntry> pick = new List<SoundEntry>();
        if (tagOnly != null)
        {
            for (int i = 0; i < all.Count; i++) if (all[i].Has(tagOnly)) pick.Add(all[i]);
        }
        if (pick.Count == 0)
        {
            for (int i = 0; i < all.Count; i++) if (tagNot == null || !all[i].Has(tagNot)) pick.Add(all[i]);
            if (pick.Count == 0) pick = all;
        }
        List<AudioClip> amb = new List<AudioClip>(), rea = new List<AudioClip>();
        for (int i = 0; i < pick.Count; i++)
        {
            SoundEntry e = pick[i];
            if (e.clip == null) continue;
            if (IsPat(e.clip.name)) { if (!hasPat && !pats.Contains(e.clip)) pats.Add(e.clip); continue; }
            if (e.Has("react") || e.Has("alert") || e.Has("happy")) rea.Add(e.clip); else amb.Add(e.clip);
        }
        if (!hasAmb) set.ambientSounds = amb.ToArray();
        if (!hasRea) set.reactionSounds = rea.ToArray();
        set.patSounds = pats.ToArray();
    }

    const int MaxCalls = 6;
    static readonly float[] busy = new float[MaxCalls];

    /// <summary>Global cap on simultaneous animal calls (each call is assumed to occupy a voice for 2s).</summary>
    public static bool Budget()
    {
        float t = Time.time;
        for (int i = 0; i < MaxCalls; i++)
            if (busy[i] <= t) { busy[i] = t + 2f; return true; }
        return false;
    }

    /// <summary>Multiplier applied to bird calls while the player plays the guitar (many birds gather to watch).</summary>
    public const float GuitarBirdMul = 0.35f;

    public static float Vol(AnimalSoundSet s)
    {
        return s.volume * AudioDirector.Volume("animals");
    }

    public static float BirdVol(AnimalSoundSet s)
    {
        float v = Vol(s);
        return Items.GuitarPlayer.IsPlaying ? v * GuitarBirdMul : v;
    }
}

/// <summary>Burst scheduler: 2-5 calls spaced 0.4-1.2s, then the set's ambientInterval as cooldown; only while the listener is in range.</summary>
public sealed class AnimalCaller
{
    int left;
    float next = -1f;

    public bool Tick(AnimalSoundSet s, float dist)
    {
        if (!AudioDirector.WorldActive) return false;
        float t = Time.time;
        if (next < 0f) next = t + Random.Range(0f, Mathf.Max(0.5f, s.ambientInterval.y));
        if (t < next) return false;
        float lo = Mathf.Max(0.5f, s.ambientInterval.x), hi = Mathf.Max(lo, s.ambientInterval.y);
        if (dist > s.maxDistance)
        {
            left = 0;
            next = t + Random.Range(lo, hi);
            return false;
        }
        if (AnimalSounds.IsCalm(s))
        {
            // cats: one meow (sometimes a second one a few seconds later), long cooldown, never over another cat
            if (!AnimalSounds.CalmReady()) { next = t + Random.Range(1f, 3f); return false; }
            if (!AnimalSounds.Budget()) { next = t + 1f; return false; }
            if (left <= 0) left = Random.value < 0.2f ? 2 : 1;
            left--;
            AnimalSounds.CalmUsed();
            next = t + (left > 0 ? Random.Range(3f, 6f) : Mathf.Max(AnimalSounds.CalmMinCooldown, Random.Range(lo, hi)));
            return true;
        }
        if (!AnimalSounds.Budget()) { next = t + 0.5f; return false; }
        if (left <= 0) left = Random.Range(2, 6);
        left--;
        next = t + (left > 0 ? Random.Range(0.4f, 1.2f) : Random.Range(lo, hi));
        return true;
    }
}
