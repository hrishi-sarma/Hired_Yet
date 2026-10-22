using UnityEngine;

/// <summary>
/// Sound slots for one species. Drag any number of AudioClips into each list in the Inspector;
/// a random clip is picked each time. Leave a list empty to disable that sound.
/// </summary>
[System.Serializable]
public class AnimalSoundSet
{
    [Tooltip("Random ambient calls (cat meows, deer grunts, rabbit squeaks...). One is picked at random each time.")]
    public AudioClip[] ambientSounds = new AudioClip[0];
    [Tooltip("Played when the animal reacts: deer/rabbit when startled and fleeing, cat when you pat it (purr/meow), dog when you pat it or come near (bark/happy).")]
    public AudioClip[] reactionSounds = new AudioClip[0];
    [Tooltip("Clips with 'pet'/'pat' in their name. Played ONLY while the animal is being patted.")]
    public AudioClip[] patSounds = new AudioClip[0];
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("Random pitch range for variety (min, max).")]
    public Vector2 pitchRange = new Vector2(0.92f, 1.08f);
    [Tooltip("Seconds between ambient calls (min, max).")]
    public Vector2 ambientInterval = new Vector2(8f, 20f);
    [Min(0.1f)] public float minDistance = 2f;
    [Min(1f)] public float maxDistance = 30f;
}

/// <summary>Runtime-tweakable settings for the independent animal simulation.</summary>
[System.Serializable]
public class AnimalAISettings
{
    [Header("Population")]
    [Min(0)] public int deerCount = 5;
    [Min(0)] public int rabbitCount = 8;
    [Min(0)] public int catCount = 6;
    [Min(0)] public int dogCount = 5;
    [Min(1)] public int spawnAttemptsPerTick = 18;
    [Min(0.5f)] public float spawnInterval = 1.25f;
    [Min(5f)] public float spawnRadiusMin = 14f;
    [Min(10f)] public float spawnRadiusMax = 78f;
    [Min(20f)] public float despawnRadius = 115f;
    [Min(1f)] public float minAnimalSpacing = 2.0f;
    public bool avoidVisibleSpawns = true;

    [Header("Wildlife movement")]
    [Min(0.1f)] public float deerWalkSpeed = 1.45f;
    [Min(1f)] public float deerRunSpeed = 5.4f;
    [Min(1f)] public float deerFleeDistance = 13f;
    [Min(0.1f)] public float rabbitWalkSpeed = 1.15f;
    [Min(1f)] public float rabbitRunSpeed = 4.7f;
    [Min(1f)] public float rabbitFleeDistance = 11f;
    [Min(0.1f)] public float catWalkSpeed = 0.72f;
    [Min(0.1f)] public float catRunSpeed = 1.65f;
    [Min(0.1f)] public float dogWalkSpeed = 1.5f;
    [Min(1f)] public float dogRunSpeed = 4.8f;
    [Min(0.1f)] public float turnSpeed = 7f;
    [Min(0.5f)] public float idleMinSeconds = 3f;
    [Min(1f)] public float idleMaxSeconds = 8f;
    [Min(1f)] public float wanderMinSeconds = 2f;
    [Min(1f)] public float wanderMaxSeconds = 6f;

    [Header("Model size (1 = default)")]
    [Min(0.2f)] public float deerScale = 1.5f;
    [Min(0.2f)] public float rabbitScale = 1f;
    [Min(0.2f)] public float catScale = 1f;
    [Min(0.2f)] public float dogScale = 1f;

    [Header("Sounds (optional, multiple clips per slot)")]
    public AnimalSoundSet deerSounds = new AnimalSoundSet();
    public AnimalSoundSet rabbitSounds = new AnimalSoundSet();
    public AnimalSoundSet catSounds = new AnimalSoundSet();
    public AnimalSoundSet dogSounds = new AnimalSoundSet();

    [Header("Butterflies & birds")]
    [Min(0)] public int butterflyCount = 14;
    [Min(0)] public int birdCount = 16;
    [Min(1f)] public float birdAlertDistance = 7f;
    [Min(0.1f)] public float birdFlySpeed = 1f;
    [Min(0.3f)] public float birdScale = 1f;
    [Min(0.3f)] public float butterflyScale = 1f;
    [Tooltip("Sparrow, robin, blue jay, cardinal, pigeon: ambient chirps / coos; reaction plays on take-off.")]
    public AnimalSoundSet songbirdSounds = new AnimalSoundSet();
    [Tooltip("Crow caws; reaction plays on take-off.")]
    public AnimalSoundSet crowSounds = new AnimalSoundSet();

    [Header("Social behaviour")]
    [Tooltip("Chance per check (every 1.5-3.5 s) that an idle animal starts playing/chasing/following a neighbour. 0 disables.")]
    [Range(0f, 1f)] public float socialChance = 0.35f;

    [Header("Sleeping at night")]
    public bool sleepAtNight = true;
    [Tooltip("Night starts at this in-game hour (only used if AnimalSleepClock.GameHour is set by your day/night script).")]
    [Range(0f, 24f)] public float nightStartHour = 20f;
    [Range(0f, 24f)] public float nightEndHour = 6f;
    [Tooltip("Automatic mode: it is night when the sun is below this elevation angle (degrees).")]
    public float nightSunElevation = -3f;
    [Tooltip("Debug: make it night right now.")]
    public bool forceNight = false;

    [Header("Cat interaction")]
    [Min(0.5f)] public float catPatRange = 2.2f;
    [Min(0.5f)] public float catPatSeconds = 2.8f;
    [Min(0.5f)] public float dogPatRange = 2.6f;
    [Min(0.5f)] public float dogPatSeconds = 3.2f;

    [Header("Personality, trust & reactions to the player")]
    [Tooltip("Every animal gets its own boldness / curiosity / energy, so individuals behave differently.")]
    public bool enablePersonality = true;
    [Tooltip("Alert freeze before fleeing, sprint scares, trust that grows when you stand still, curious approach, dog escort.")]
    public bool enableReactions = true;
    [Min(1f)] public float playerSprintSpeed = 6f;
    [Min(0.2f)] public float playerCalmSpeed = 1.6f;
    [Min(10f)] public float trustSecondsToFull = 90f;
    [Min(1.1f)] public float alertDistanceMultiplier = 1.7f;

    [Header("Homes & daily routines")]
    [Tooltip("Pets belong to suburb houses, rabbits to burrows, deer to forest beds.")]
    public bool useHomes = true;
    public bool enableRoutines = true;
    public bool rabbitsUseBurrows = true;
    public bool showBurrowMarkers = true;
    [Range(0f, 1f)] public float petCatLotChance = 0.30f;
    [Range(0f, 1f)] public float petDogLotChance = 0.25f;
    [Min(5f)] public float catRoamRadius = 24f;
    [Min(5f)] public float dogRoamRadius = 36f;
    [Min(5f)] public float rabbitRoamRadius = 18f;
    [Min(10f)] public float deerRoamRadius = 50f;
    [Range(0f, 1f)] public float burrowTreeChance = 0.08f;
    [Min(0)] public int maxBurrows = 250;
    [Min(0)] public int maxDeerBeds = 90;

    public AnimalSoundSet SoundsFor(AnimalSpecies species)
    {
        if (species == AnimalSpecies.Deer) return deerSounds;
        if (species == AnimalSpecies.Rabbit) return rabbitSounds;
        if (species == AnimalSpecies.Cat) return catSounds;
        return dogSounds;
    }

    public float ScaleFor(AnimalSpecies species)
    {
        return Mathf.Max(0.2f, species == AnimalSpecies.Deer ? deerScale : (species == AnimalSpecies.Rabbit ? rabbitScale : (species == AnimalSpecies.Cat ? catScale : dogScale)));
    }

    public void Sanitize()
    {
        if (deerSounds == null) deerSounds = new AnimalSoundSet();
        if (rabbitSounds == null) rabbitSounds = new AnimalSoundSet();
        if (catSounds == null) catSounds = new AnimalSoundSet();
        if (songbirdSounds == null) songbirdSounds = new AnimalSoundSet();
        if (crowSounds == null) crowSounds = new AnimalSoundSet();
        butterflyCount = Mathf.Max(0, butterflyCount);
        birdCount = Mathf.Max(0, birdCount);
        birdAlertDistance = Mathf.Max(1f, birdAlertDistance);
        birdFlySpeed = Mathf.Max(0.1f, birdFlySpeed);
        birdScale = Mathf.Max(0.3f, birdScale);
        butterflyScale = Mathf.Max(0.3f, butterflyScale);
        if (dogSounds == null) dogSounds = new AnimalSoundSet();
        dogCount = Mathf.Max(0, dogCount);
        dogWalkSpeed = Mathf.Max(0.1f, dogWalkSpeed);
        dogRunSpeed = Mathf.Max(dogWalkSpeed, dogRunSpeed);
        deerCount = Mathf.Max(0, deerCount);
        rabbitCount = Mathf.Max(0, rabbitCount);
        catCount = Mathf.Max(0, catCount);
        spawnAttemptsPerTick = Mathf.Max(1, spawnAttemptsPerTick);
        spawnInterval = Mathf.Max(0.5f, spawnInterval);
        spawnRadiusMin = Mathf.Max(5f, spawnRadiusMin);
        spawnRadiusMax = Mathf.Max(spawnRadiusMin + 5f, spawnRadiusMax);
        despawnRadius = Mathf.Max(spawnRadiusMax, despawnRadius);
        minAnimalSpacing = Mathf.Max(1f, minAnimalSpacing);
        deerWalkSpeed = Mathf.Max(0.1f, deerWalkSpeed);
        deerRunSpeed = Mathf.Max(deerWalkSpeed, deerRunSpeed);
        deerFleeDistance = Mathf.Max(1f, deerFleeDistance);
        rabbitWalkSpeed = Mathf.Max(0.1f, rabbitWalkSpeed);
        rabbitRunSpeed = Mathf.Max(rabbitWalkSpeed, rabbitRunSpeed);
        rabbitFleeDistance = Mathf.Max(1f, rabbitFleeDistance);
        catWalkSpeed = Mathf.Max(0.1f, catWalkSpeed);
        catRunSpeed = Mathf.Max(catWalkSpeed, catRunSpeed);
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
        idleMinSeconds = Mathf.Max(0.5f, idleMinSeconds);
        idleMaxSeconds = Mathf.Max(idleMinSeconds, idleMaxSeconds);
        wanderMinSeconds = Mathf.Max(0.5f, wanderMinSeconds);
        wanderMaxSeconds = Mathf.Max(wanderMinSeconds, wanderMaxSeconds);
        catPatRange = Mathf.Max(0.5f, catPatRange);
        playerSprintSpeed = Mathf.Max(1f, playerSprintSpeed);
        playerCalmSpeed = Mathf.Clamp(playerCalmSpeed, 0.2f, playerSprintSpeed);
        trustSecondsToFull = Mathf.Max(10f, trustSecondsToFull);
        alertDistanceMultiplier = Mathf.Max(1.1f, alertDistanceMultiplier);
        catRoamRadius = Mathf.Max(5f, catRoamRadius);
        dogRoamRadius = Mathf.Max(5f, dogRoamRadius);
        rabbitRoamRadius = Mathf.Max(5f, rabbitRoamRadius);
        deerRoamRadius = Mathf.Max(10f, deerRoamRadius);
        maxBurrows = Mathf.Max(0, maxBurrows);
        maxDeerBeds = Mathf.Max(0, maxDeerBeds);
        catPatSeconds = Mathf.Max(0.5f, catPatSeconds);
        dogPatRange = Mathf.Max(0.5f, dogPatRange);
        dogPatSeconds = Mathf.Max(0.5f, dogPatSeconds);
    }
}
