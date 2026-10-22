using System.Collections.Generic;
using UnityEngine;

/// <summary>Catalog-driven NPC audio: general voices (tag npc), event voices (tag event) and the alien voice (tag alien).</summary>
public static class NpcSounds
{
    static List<SoundEntry> voices, events, aliens;
    static bool ready;
    static int lastVoice = -1, lastEvent = -1, lastAlien = -1;

    static void Load()
    {
        if (ready) return;
        SoundCatalog c = SoundCatalog.Instance;
        if (c == null) return;
        ready = true;
        voices = new List<SoundEntry>();
        events = new List<SoundEntry>();
        aliens = new List<SoundEntry>();
        List<SoundEntry> all = c.Get("npcs");
        for (int i = 0; i < all.Count; i++)
        {
            SoundEntry e = all[i];
            if (e.Has("alien")) aliens.Add(e);
            else if (e.Has("event")) events.Add(e);
            else voices.Add(e);
        }
    }

    public static AudioClip Voice()
    {
        Load();
        return SoundCatalog.Pick(voices, ref lastVoice);
    }

    /// <summary>Clip for an event's intro: alien clip for alien_tourist, event clip otherwise (null when none).</summary>
    public static AudioClip Event(string id)
    {
        Load();
        if (id == "alien_tourist" && aliens != null && aliens.Count > 0) return SoundCatalog.Pick(aliens, ref lastAlien);
        return SoundCatalog.Pick(events, ref lastEvent);
    }
}

/// <summary>NPCs are silent by default; they only speak during events (EventAudio) or when they catch the player pickpocketing.</summary>
public sealed class NpcVoice
{
    float pitch;

    public void Reset() { pitch = 0f; }

    /// <summary>Intentionally silent - no idle chatter.</summary>
    public void Tick(Transform t) { }

    /// <summary>Played once when a pickpocket attempt fails and the NPC notices.</summary>
    public void Caught(Transform t)
    {
        if (!AudioDirector.WorldActive || t == null) return;
        if (pitch <= 0f) pitch = Random.Range(0.85f, 1.2f);
        AudioDirector.PlayAt(NpcSounds.Voice(), t.position + Vector3.up * 1.6f, "npcs", 0.8f, pitch, 1, 2f, 18f);
    }
}
