using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable NPC animation collection. Each list can contain multiple clips;
/// one entry is selected randomly when an NPC's animation set is resolved.
/// Playback speed belongs to each entry, so every clip can be tuned independently.
/// </summary>
[CreateAssetMenu(menuName = "AI/NPC Animation Set", fileName = "NpcAnimationSet")]
public class NpcAnimationSet : ScriptableObject
{
    [Serializable]
    public class AnimationOption
    {
        public AnimationClip clip;

        [Tooltip("Playback speed for this specific clip. For Walk, this multiplies the movement-based speed.")]
        [Range(0.1f, 3f)]
        public float playbackSpeed = 1f;
    }

    [Header("Idle Animations")]
    [Tooltip("Add as many idle clips as you want. One is randomly chosen for each NPC.")]
    public List<AnimationOption> idleAnimations = new List<AnimationOption>();

    [Header("Walk Animations")]
    [Tooltip("Use in-place walk clips. One is randomly chosen for each NPC.")]
    public List<AnimationOption> walkAnimations = new List<AnimationOption>();

    [Header("Confrontation Animations")]
    [Tooltip("Played when a pickpocket attempt fails. One is randomly chosen for each NPC.")]
    public List<AnimationOption> confrontationAnimations = new List<AnimationOption>();

    [Header("Legacy Single Clips")]
    [Tooltip("Backward compatibility for older assets. Used only when the corresponding list above is empty.")]
    public AnimationClip idle;
    [Tooltip("Legacy walk clip. Used only when Walk Animations is empty.")]
    public AnimationClip walk;
    [Tooltip("Legacy confrontation clip. Used only when Confrontation Animations is empty.")]
    public AnimationClip confrontation;

    [Header("Blending")]
    [Min(0.01f)] public float crossfadeSeconds = 0.2f;

    [Header("Walk")]
    [Tooltip("Movement speed (m/s) the walk clip looks right at. Playback speed = actual speed / this value.")]
    [Min(0.1f)] public float walkReferenceSpeed = 1.4f;
    [Tooltip("Limits how much the movement-based walk rate can be sped up / slowed down before the per-clip multiplier.")]
    public Vector2 walkPlaybackRange = new Vector2(0.4f, 2.2f);

    [Header("Confrontation")]
    public bool loopConfrontation = false;
    public bool extendAlertToClip = true;
    [Min(0f)] public float maxAlertSeconds = 6f;

    void OnValidate()
    {
        crossfadeSeconds = Mathf.Max(0.01f, crossfadeSeconds);
        walkReferenceSpeed = Mathf.Max(0.1f, walkReferenceSpeed);
        walkPlaybackRange.x = Mathf.Max(0.05f, walkPlaybackRange.x);
        walkPlaybackRange.y = Mathf.Max(walkPlaybackRange.x, walkPlaybackRange.y);
        ClampSpeeds(idleAnimations);
        ClampSpeeds(walkAnimations);
        ClampSpeeds(confrontationAnimations);
    }

    static void ClampSpeeds(List<AnimationOption> options)
    {
        if (options == null) return;
        foreach (AnimationOption option in options)
        {
            if (option != null)
                option.playbackSpeed = Mathf.Clamp(option.playbackSpeed, 0.1f, 3f);
        }
    }

    /// <summary>
    /// Resolves one random clip from each category. Per-NPC set entries take priority;
    /// missing categories fall back to the default set. Called per NPC by AgentPool.
    /// </summary>
    public static ResolvedAnimations Resolve(NpcAnimationSet primary, NpcAnimationSet fallback)
    {
        NpcAnimationSet cfg = primary != null ? primary : fallback;
        ResolvedAnimations r = new ResolvedAnimations();

        PickWithSpeed(primary, fallback, 0, out r.idle, out r.idlePlaybackSpeed);
        PickWithSpeed(primary, fallback, 1, out r.walk, out r.walkPlaybackMultiplier);
        PickWithSpeed(primary, fallback, 2, out r.confrontation, out r.confrontationPlaybackSpeed);

        // Keep the NPC animated if a category has no assigned clip anywhere.
        if (r.idle == null)
        {
            r.idle = r.walk != null ? r.walk : r.confrontation;
            r.idlePlaybackSpeed = r.walk != null ? r.walkPlaybackMultiplier : r.confrontationPlaybackSpeed;
        }
        if (r.walk == null)
        {
            r.walk = r.idle;
            r.walkPlaybackMultiplier = r.idlePlaybackSpeed;
        }
        if (r.confrontation == null)
        {
            r.confrontation = r.idle;
            r.confrontationPlaybackSpeed = r.idlePlaybackSpeed;
        }

        r.crossfade = cfg != null ? cfg.crossfadeSeconds : 0.2f;
        r.walkReferenceSpeed = cfg != null ? cfg.walkReferenceSpeed : 1.4f;
        r.walkPlaybackRange = cfg != null ? cfg.walkPlaybackRange : new Vector2(0.4f, 2.2f);
        r.loopConfrontation = cfg != null && cfg.loopConfrontation;
        r.extendAlertToClip = cfg == null || cfg.extendAlertToClip;
        r.maxAlertSeconds = cfg != null ? cfg.maxAlertSeconds : 6f;
        return r;
    }

    static void PickWithSpeed(
        NpcAnimationSet primary,
        NpcAnimationSet fallback,
        int slot,
        out AnimationClip clip,
        out float playbackSpeed)
    {
        clip = null;
        playbackSpeed = 1f;

        // If the primary set has one or more valid entries, choose only from it.
        if (TryPick(primary, slot, out clip, out playbackSpeed))
            return;

        // Otherwise use the default set for this category.
        if (TryPick(fallback, slot, out clip, out playbackSpeed))
            return;

        // Legacy single fields support assets created before list-based animations.
        NpcAnimationSet legacySource = primary != null && GetLegacy(primary, slot) != null
            ? primary
            : fallback;

        if (legacySource != null)
        {
            clip = GetLegacy(legacySource, slot);
            playbackSpeed = 1f;
        }
    }

    static bool TryPick(NpcAnimationSet set, int slot, out AnimationClip clip, out float playbackSpeed)
    {
        clip = null;
        playbackSpeed = 1f;
        if (set == null) return false;

        List<AnimationOption> options = GetList(set, slot);
        if (options == null || options.Count == 0) return false;

        // Ignore empty rows so a partially configured list is safe.
        int validCount = 0;
        for (int i = 0; i < options.Count; i++)
            if (options[i] != null && options[i].clip != null)
                validCount++;

        if (validCount == 0) return false;

        int chosenValidIndex = UnityEngine.Random.Range(0, validCount);
        for (int i = 0; i < options.Count; i++)
        {
            AnimationOption option = options[i];
            if (option == null || option.clip == null) continue;

            if (chosenValidIndex-- == 0)
            {
                clip = option.clip;
                playbackSpeed = Mathf.Clamp(option.playbackSpeed, 0.1f, 3f);
                return true;
            }
        }
        return false;
    }

    static List<AnimationOption> GetList(NpcAnimationSet set, int slot)
    {
        if (slot == 0) return set.idleAnimations;
        if (slot == 1) return set.walkAnimations;
        return set.confrontationAnimations;
    }

    static AnimationClip GetLegacy(NpcAnimationSet set, int slot)
    {
        return slot == 0 ? set.idle : slot == 1 ? set.walk : set.confrontation;
    }
}

public struct ResolvedAnimations
{
    public AnimationClip idle;
    public AnimationClip walk;
    public AnimationClip confrontation;
    public float crossfade;
    public float idlePlaybackSpeed;
    public float walkPlaybackMultiplier;
    public float confrontationPlaybackSpeed;
    public float walkReferenceSpeed;
    public Vector2 walkPlaybackRange;
    public bool loopConfrontation;
    public bool extendAlertToClip;
    public float maxAlertSeconds;

    public bool HasAny
    {
        get { return idle != null || walk != null || confrontation != null; }
    }
}
