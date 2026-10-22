using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class ZoneDensity
{
    [Range(0f, 2f)] public float suburb = 0.4f;
    [Range(0f, 2f)] public float city = 1f;
    [Range(0f, 2f)] public float highway = 0.3f;

    public ZoneDensity() { }

    public ZoneDensity(float suburb, float city, float highway)
    {
        this.suburb = suburb;
        this.city = city;
        this.highway = highway;
    }

    public float For(ZoneType zone)
    {
        switch (zone)
        {
            case ZoneType.Suburb: return suburb;
            case ZoneType.City: return city;
            case ZoneType.Highway: return highway;
        }
        return 0f;
    }
}

[System.Serializable]
public class PedestrianBehavior
{
    [Tooltip("Chance per node decision that a walker stops and idles.")]
    [Range(0f, 1f)] public float idleChance = 0.15f;
    public float idleSecondsMin = 3f;
    public float idleSecondsMax = 12f;
    [Tooltip("Chance per node decision that a walker heads for a house door.")]
    [Range(0f, 1f)] public float enterHouseChance = 0.06f;
    [Tooltip("Weight of choosing a crosswalk at a node (straight 0.6, turn 0.2, cross 0.2 by default).")]
    [Range(0f, 1f)] public float crossChance = 0.2f;
    public float curbWaitMax = 20f;
    public float speedMin = 1.1f;
    public float speedMax = 1.6f;
    public float crossSpeed = 1.5f;
    public float yieldSideSeconds = 3f;
    public float yieldTurnAroundSeconds = 8f;
}

[System.Serializable]
public class DriverProfile
{
    public string name = "Normal";
    [Range(0f, 10f)] public float weight = 1f;
    public float speedFactor = 1f;
    public float accelFactor = 1f;
    public int honkCount = 3;
    public float honkFirstDelay = 1f;
    public float honkDuration = 0.5f;
    public float honkInterval = 1.4f;
    [Range(0f, 1f)] public float honkVolume = 0.8f;
    public float honkPitch = 1f;
    public float patience = 5f;
    public bool overtakeHonk = false;
    public float overtakeSpeed = 7f;
    public float boostSeconds = 0f;
    public float boostFactor = 1f;
    public int chargeCount = 0;
    public int chargeAfterHonks = 2;
    public float chargeSpeed = 5f;
    [Tooltip("Chance (0-1) of running a red light when clear. -1 = derive from personality name.")]
    public float runRedChance = -1f;
    [Tooltip("0 = long following gap, 1 = tailgater. -1 = derive.")]
    public float tailgate = -1f;
    [Tooltip("0 = never overtakes slow cars, 1 = overtakes at first chance. -1 = derive.")]
    public float overtakeEagerness = -1f;

    public void Resolve()
    {
        if (runRedChance >= 0f && tailgate >= 0f && overtakeEagerness >= 0f) return;
        float r;
        float t;
        float e;
        switch (name)
        {
            case "Patient": r = 0f; t = 0f; e = 0.1f; break;
            case "Polite": r = 0f; t = 0.1f; e = 0.15f; break;
            case "Normal": r = 0.01f; t = 0.3f; e = 0.4f; break;
            case "Impatient": r = 0.12f; t = 0.6f; e = 0.8f; break;
            case "Angry": r = 0.15f; t = 0.7f; e = 0.8f; break;
            case "Road Rager": r = 0.3f; t = 0.9f; e = 1f; break;
            case "Menace": r = 0.25f; t = 0.8f; e = 0.9f; break;
            case "Psycho": r = 0.5f; t = 1f; e = 1f; break;
            case "Tapper": r = 0.05f; t = 0.4f; e = 0.5f; break;
            case "Sleepy": r = 0.03f; t = 0.1f; e = 0.1f; break;
            default:
                float a = Mathf.Clamp01((6f - patience) / 6f);
                r = a * a * 0.2f;
                t = a;
                e = a;
                break;
        }
        if (runRedChance < 0f) runRedChance = r;
        if (tailgate < 0f) tailgate = t;
        if (overtakeEagerness < 0f) overtakeEagerness = e;
    }

    static DriverProfile P(string name, float weight, float speedF, float accelF, int honks, float first, float dur, float interval, float vol, float pitch, float patience, bool overHonk, float overSpeed, float boostSec, float boostF, int charges, int chargeAfter, float chargeSpeed)
    {
        DriverProfile d = new DriverProfile();
        d.name = name;
        d.weight = weight;
        d.speedFactor = speedF;
        d.accelFactor = accelF;
        d.honkCount = honks;
        d.honkFirstDelay = first;
        d.honkDuration = dur;
        d.honkInterval = interval;
        d.honkVolume = vol;
        d.honkPitch = pitch;
        d.patience = patience;
        d.overtakeHonk = overHonk;
        d.overtakeSpeed = overSpeed;
        d.boostSeconds = boostSec;
        d.boostFactor = boostF;
        d.chargeCount = charges;
        d.chargeAfterHonks = chargeAfter;
        d.chargeSpeed = chargeSpeed;
        return d;
    }

    public static DriverProfile[] Defaults()
    {
        return new DriverProfile[]
        {
            P("Patient", 2f, 0.9f, 1f, 0, 0f, 0.2f, 0f, 0.45f, 1.15f, 6f, true, 5f, 0f, 1f, 0, 0, 0f),
            P("Polite", 2f, 1f, 1f, 1, 1.5f, 0.22f, 2f, 0.5f, 1.12f, 4f, false, 6f, 0f, 1f, 0, 0, 0f),
            P("Normal", 3f, 1f, 1f, 3, 1f, 0.5f, 1.4f, 0.8f, 1f, 5f, false, 7f, 0f, 1f, 0, 0, 0f),
            P("Impatient", 1.5f, 1.1f, 1.2f, 4, 0.4f, 0.4f, 0.8f, 0.9f, 1.05f, 3f, false, 9f, 3f, 1.15f, 0, 0, 0f),
            P("Angry", 1.2f, 1.1f, 1.3f, 3, 0.5f, 1.4f, 0.7f, 1f, 0.85f, 4.5f, false, 12f, 8f, 1.5f, 0, 0, 0f),
            P("Road Rager", 0.8f, 1.2f, 1.5f, 5, 0.3f, 2.2f, 0.5f, 1f, 0.78f, 9f, false, 14f, 10f, 1.6f, 0, 0, 0f),
            P("Menace", 0.6f, 1.1f, 1.5f, 4, 0.5f, 1f, 0.6f, 1f, 0.9f, 6f, false, 12f, 6f, 1.4f, 1, 2, 5f),
            P("Psycho", 0.3f, 1.25f, 1.8f, 5, 0.3f, 1.8f, 0.4f, 1f, 0.8f, 10f, false, 15f, 10f, 1.6f, 1, 1, 8f),
            P("Tapper", 1f, 1f, 1.1f, 8, 0.6f, 0.12f, 0.35f, 0.8f, 1.2f, 4.5f, false, 8f, 0f, 1f, 0, 0, 0f),
            P("Sleepy", 1f, 0.85f, 0.8f, 1, 4f, 0.3f, 1f, 0.5f, 0.95f, 8f, false, 3.5f, 0f, 1f, 0, 0, 0f)
        };
    }

    public static DriverProfile Pick(DriverProfile[] list, System.Random rng)
    {
        if (list == null || list.Length == 0) list = Defaults();
        float total = 0f;
        for (int i = 0; i < list.Length; i++) total += Mathf.Max(0f, list[i].weight);
        if (total <= 0f) return list[0];
        float r = (float)rng.NextDouble() * total;
        for (int i = 0; i < list.Length; i++)
        {
            r -= Mathf.Max(0f, list[i].weight);
            if (r <= 0f) return list[i];
        }
        return list[list.Length - 1];
    }
}

[System.Serializable]
public class TrafficTuning
{
    public bool signals = true;
    [Tooltip("Scales every driver's chance of running a red light.")]
    public float redRunScale = 1f;
    [Tooltip("Cars farther than this from the player are simulated roughly.")]
    public float farSimRadius = 70f;
    public float farTick = 0.2f;
    [Tooltip("A car seen recently and within this radius never despawns (player following it).")]
    public float followRadius = 380f;
    public float followSeenSeconds = 25f;
    [Range(0f, 1f)] public float drunkChanceLate = 0.12f;
    [Range(0f, 1f)] public float sleepyChanceLate = 0.1f;
    public bool debugView = false;
    public Key debugKey = Key.F9;
    public float debugRadius = 90f;
}

[System.Serializable]
public class CarBehavior
{
    public float resumeDelay = 0.7f;
    public bool disableOvertake = false;
    public float chargeStopDistance = 0.9f;
    public bool nudge = false;
    public float nudgeAfterSeconds = 8f;
    public float nudgeSpeed = 0.8f;
    public AudioClip[] honkClips;
    [Range(0f, 1f)] public float honkVolume = 0.8f;
    public DriverProfile[] drivers = DriverProfile.Defaults();
    [Tooltip("Max cars honking at once. Overlapping horns always use the same clip.")]
    public int maxSimultaneousHorns = 3;
    public TrafficTuning traffic = new TrafficTuning();
}

[System.Serializable]
public class EventSettings
{
    public bool enabled = true;
    public float itemChanceScale = 1f;
    public float minDistanceFromPlayer = 50f;
    public float minDistanceBetween = 80f;
    public float introSeconds = 2.2f;
    public int attempts = 2;
    [Tooltip("Events per zone. Each event family (carry, hold-help, mash, timing, sequence) spawns at most once per session.")]
    [Min(0)] public int suburbEvents = 1;
    [Min(0)] public int forestEvents = 2;
    [Min(0)] public int cityEvents = 2;
    public float interactRange = 2.6f;
    [Tooltip("Standalone loose objects (no NPC, not slot events): backpacks are scattered in the forest, wallets in city and suburb.")]
    [Min(0)] public int backpackMin = 3;
    [Min(0)] public int backpackMax = 6;
    [Min(0)] public int walletMin = 3;
    [Min(0)] public int walletMax = 6;
    [Min(0f)] public float standaloneSpacing = 20f;
    [Tooltip("Library of per-event configs. Create one with World > Events > Create Default Event Assets. Empty uses built-in defaults.")]
    public WorldEventLibrary library;
    [Tooltip("Minimum distance in metres from an event footprint to the nearest traffic lane centre.")]
    public float laneClearance = 2.5f;
    [Tooltip("Minimum distance in metres from an event footprint to the sidewalk centre line, keeping a walkable gap.")]
    public float sidewalkClearance = 0.9f;
    [Tooltip("Maximum ground height difference across an event footprint.")]
    public float maxGroundDelta = 0.4f;
    [Tooltip("How many different events are tried in a zone before giving up.")]
    [Min(1)] public int configTries = 6;
}

[System.Serializable]
public class PickpocketSettings
{
    [Range(0f, 1f)] public float pickpocketChance = 0.08f;
    public int maxPickpocketTargets = 3;
    [Range(0f, 1f)] public float successChance = 0.5f;
    public float triggerRadius = 1.6f;
    public float alertSeconds = 2f;
    public int failSuspicionPenalty = 0;
}

[System.Serializable]
public class CustomVehicleModel
{
    [Tooltip("Optional label shown in the Inspector.")]
    public string displayName = "Custom Vehicle";

    [Tooltip("Prefab used for this traffic vehicle. The prefab's authored scale is preserved.")]
    public GameObject prefab;

    [Tooltip("Additional multiplier applied after importing/instantiating the prefab. 1 = authored prefab size.")]
    [Range(0.25f, 4f)] public float scale = 1f;

    public void Sanitize()
    {
        scale = Mathf.Clamp(scale, 0.25f, 4f);
    }
}

[System.Serializable]
public class AiSettings
{
    [Header("Caps")]
    public int maxPedestrians = 40;
    public int maxCars = 24;

    [Header("Spawn / despawn")]
    public float spawnRadiusMin = 35f;
    public float spawnRadiusMax = 140f;
    public float activeRadius = 160f;
    public float despawnGraceSeconds = 2f;
    public float neverDespawnWithin = 0f;
    [Tooltip("Agents that left view stay while closer than this fraction of the suburb width (diameter).")]
    [Range(0f, 1f)] public float despawnSuburbFraction = 0.25f;
    public float spawnAttemptsPerSecond = 6f;
    [Header("Initial population (spawned around the player at load, visible)")]
    public int initialPedestrians = 24;
    public int initialCars = 10;
    public float initialRadiusMin = 10f;
    public float initialRadiusMax = 90f;
    public float visibilityCheckInterval = 0.25f;

    [Header("Density by zone")]
    public ZoneDensity pedestrianDensity = new ZoneDensity(0.4f, 1f, 0f);
    public ZoneDensity carDensity = new ZoneDensity(0.4f, 1f, 0.3f);

    [Header("Budgets")]
    public int linecastsPerFrame = 12;
    public int prewarmPedestrians = 12;
    public int prewarmCars = 8;

    [Header("Visuals (empty slot = primitive)")]
    public ModelSlot pedestrianSlot = new ModelSlot();
    public ModelSlot carSlot = new ModelSlot();

    [Tooltip("Additional scale for the legacy Car Slot. This is applied to its fitted target size, after ModelFitter's fixed-size fitting.")]
    [Range(0.25f, 4f)] public float carSlotScale = 1f;

    [Header("Custom Vehicle Prefabs (per-vehicle scale)")]
    [Tooltip("If this list contains valid prefabs, traffic randomly chooses from them instead of the legacy Car Slot. Each entry has its own scale slider.")]
    public List<CustomVehicleModel> customVehicles = new List<CustomVehicleModel>();

    [Header("Custom NPCs (leave list empty to keep the built-in primitive/slot pedestrians)")]
    [Tooltip("Animations used by every NPC unless its profile assigns its own set.")]
    public NpcAnimationSet defaultAnimations;
    public List<NpcProfile> npcProfiles = new List<NpcProfile>();

    [Header("Behavior")]
    public PedestrianBehavior pedestrian = new PedestrianBehavior();
    public CarBehavior car = new CarBehavior();
    public EventSettings events = new EventSettings();
    public PickpocketSettings pickpocket = new PickpocketSettings();

    [Header("Debug")]
    public bool drawGizmos = true;
    public bool logEvents = false;

    public void Sanitize()
    {
        maxPedestrians = Mathf.Max(0, maxPedestrians);
        maxCars = Mathf.Max(0, maxCars);
        spawnRadiusMin = Mathf.Max(5f, spawnRadiusMin);
        spawnRadiusMax = Mathf.Max(spawnRadiusMin + 5f, spawnRadiusMax);
        activeRadius = Mathf.Max(spawnRadiusMax + 10f, activeRadius);
        despawnGraceSeconds = Mathf.Max(0f, despawnGraceSeconds);
        neverDespawnWithin = Mathf.Max(0f, neverDespawnWithin);
        spawnAttemptsPerSecond = Mathf.Max(1f, spawnAttemptsPerSecond);
        visibilityCheckInterval = Mathf.Clamp(visibilityCheckInterval, 0.05f, 1f);
        linecastsPerFrame = Mathf.Max(1, linecastsPerFrame);
        prewarmPedestrians = Mathf.Max(0, prewarmPedestrians);
        prewarmCars = Mathf.Max(0, prewarmCars);
        if (npcProfiles == null) npcProfiles = new List<NpcProfile>();
        for (int i = 0; i < npcProfiles.Count; i++)
        {
            if (npcProfiles[i] == null) npcProfiles[i] = new NpcProfile();
            npcProfiles[i].Sanitize();
        }
        if (pedestrianSlot == null) pedestrianSlot = new ModelSlot();
        if (carSlot == null) carSlot = new ModelSlot();
        carSlotScale = Mathf.Clamp(carSlotScale, 0.25f, 4f);
        if (customVehicles == null) customVehicles = new List<CustomVehicleModel>();
        for (int i = 0; i < customVehicles.Count; i++)
        {
            if (customVehicles[i] == null) customVehicles[i] = new CustomVehicleModel();
            customVehicles[i].Sanitize();
        }
        if (pedestrianDensity == null) pedestrianDensity = new ZoneDensity(0.4f, 1f, 0f);
        if (carDensity == null) carDensity = new ZoneDensity(0.4f, 1f, 0.3f);
        if (pedestrian == null) pedestrian = new PedestrianBehavior();
        if (pickpocket == null) pickpocket = new PickpocketSettings();
        pedestrian.speedMin = Mathf.Max(0.2f, pedestrian.speedMin);
        pedestrian.speedMax = Mathf.Max(pedestrian.speedMin, pedestrian.speedMax);
        pedestrian.idleSecondsMax = Mathf.Max(pedestrian.idleSecondsMin, pedestrian.idleSecondsMax);
    }
}
