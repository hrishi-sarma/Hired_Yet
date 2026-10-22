using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One spawnable NPC type. Add as many as you like to AiSettings > Npc Profiles.
/// </summary>
[System.Serializable]
public class NpcProfile
{
    [Tooltip("Label for the inspector list only.")]
    public string name = "NPC";
    public bool enabled = true;
    [Tooltip("Humanoid model prefab. Should have an Animator with the model's Avatar (the FBX import provides this).")]
    public GameObject prefab;
    [Tooltip("How often this NPC is picked relative to the other enabled profiles.")]
    [Min(0f)] public float spawnWeight = 1f;

    [Header("Size")]
    [Tooltip("If above 0 the model is resized to this height in metres (e.g. 1.75). 0 keeps the prefab's native size.")]
    [Min(0f)] public float fitToHeight = 0f;
    [Tooltip("Random scale multiplier range applied on every spawn. 1 = unchanged.")]
    public Vector2 scaleRange = new Vector2(1f, 1f);

    [Header("Pace")]
    [Tooltip("Off = use the global pedestrian speed range from Pedestrian settings.")]
    public bool overrideSpeed = false;
    [Min(0.2f)] public float speedMin = 1.1f;
    [Min(0.2f)] public float speedMax = 1.6f;

    [Header("Animation (optional)")]
    [Tooltip("Leave empty to use the default set from AiSettings. Empty clips inside this set also fall back to the default.")]
    public NpcAnimationSet animationSet;

    [Header("Advanced")]
    [Tooltip("Removes colliders/rigidbodies found on the prefab so they don't fight the NPC's own collider.")]
    public bool stripPrefabPhysics = true;

    public bool IsValid
    {
        get { return enabled && prefab != null && spawnWeight > 0f; }
    }

    public float RollScale(float t01)
    {
        return Mathf.Lerp(scaleRange.x, scaleRange.y, t01);
    }

    public void Sanitize()
    {
        spawnWeight = Mathf.Max(0f, spawnWeight);
        fitToHeight = Mathf.Max(0f, fitToHeight);
        scaleRange.x = Mathf.Max(0.1f, scaleRange.x);
        scaleRange.y = Mathf.Max(scaleRange.x, scaleRange.y);
        speedMin = Mathf.Max(0.2f, speedMin);
        speedMax = Mathf.Max(speedMin, speedMax);
    }

    /// <summary>Weighted random pick among valid profiles. Returns -1 when none are usable.</summary>
    public static int Pick(List<NpcProfile> list, System.Random rng)
    {
        if (list == null) return -1;
        float total = 0f;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].IsValid) total += list[i].spawnWeight;
        if (total <= 0f) return -1;
        float roll = (float)rng.NextDouble() * total;
        int last = -1;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null || !list[i].IsValid) continue;
            last = i;
            roll -= list[i].spawnWeight;
            if (roll <= 0f) return i;
        }
        return last;
    }
}
