using UnityEngine;

/// <summary>Independent wandering/fleeing animal; deliberately does not implement IAgent or enter AgentRegistry.</summary>
[DisallowMultipleComponent]
public sealed partial class AnimalAgent : MonoBehaviour, IInteractable
{
    enum Motion { Idle, Walk, Run, Flee, Pat, Sleep }

    public AnimalSpecies Species { get; private set; }
    public Vector3 Position { get { return transform.position; } }
    public float Range { get { if (settings == null) return 0f; return Species == AnimalSpecies.Cat ? settings.catPatRange : (Species == AnimalSpecies.Dog ? settings.dogPatRange : 0f); } }
    public string Prompt { get { return Species == AnimalSpecies.Dog ? "[ PRESS E ] Pat the dog" : "[ PRESS E ] Pat the cat"; } }
    public bool CanInteract
    {
        get
        {
            bool petable = (Species == AnimalSpecies.Cat && catIdle) || Species == AnimalSpecies.Dog;
            if (!petable || Time.time < patUntil) return false;
            InteractionSystem sys = InteractionSystem.Instance;
            return sys != null && !sys.Suppressed && director != null && director.Player != null;
        }
    }

    AnimalWorldController owner;
    NpcDirector director;
    AnimalAISettings settings;
    AnimalRig rig;
    System.Random rng;
    ZoneType homeZone;
    Motion motion = Motion.Idle;
    Vector3 moveDirection;
    float motionTimer;
    float idleTimer;
    float gait;
    float fleeCooldown;
    float patUntil;
    bool catIdle;
    int catIdlePose;
    float catPoseTimer;
    float catLookPhase;
    bool registered;
    InteractionSystem registeredSystem;
    CapsuleCollider bodyCollider;
    Rigidbody bodyRigidbody;
    float animalHeight;
    float collisionRadius;
    float modelScale = 1f;
    AudioSource audioSrc;
    AnimalSoundSet sounds;
    float voiceFreeAt;        // earliest time this animal may vocalise again
    AudioClip lastClip;       // never play the same clip twice in a row
    float nextCallTime;
    readonly AnimalCaller caller = new AnimalCaller();
    float lastPlayerDistance;
    static readonly RaycastHit[] groundHits = new RaycastHit[16];

    public void Initialize(AnimalWorldController controller, AnimalSpecies species, NpcDirector npcDirector, AnimalAISettings cfg, int seed, HomeSpot homeSpot = null)
    {
        owner = controller;
        Species = species;
        director = npcDirector;
        settings = cfg;
        rng = new System.Random(seed);
        homeZone = (species == AnimalSpecies.Cat || species == AnimalSpecies.Dog) ? ZoneType.Suburb : ZoneType.Forest;
        animalHeight = species == AnimalSpecies.Deer ? 1.25f : (species == AnimalSpecies.Rabbit ? 0.6f : (species == AnimalSpecies.Dog ? 0.8f : 0.6f));
        collisionRadius = species == AnimalSpecies.Deer ? 0.38f : (species == AnimalSpecies.Rabbit ? 0.24f : (species == AnimalSpecies.Dog ? 0.3f : 0.26f));

        modelScale = settings.ScaleFor(species);
        animalHeight *= modelScale;
        collisionRadius *= modelScale;

        // Ignore Raycast keeps these colliders out of NpcDirector.GroundY raycasts while
        // leaving normal physical collision behavior enabled by Unity's layer matrix.
        SetLayerRecursively(gameObject, 2);
        bodyCollider = gameObject.AddComponent<CapsuleCollider>();
        bodyCollider.direction = 1;
        bodyCollider.radius = collisionRadius;
        bodyCollider.height = Mathf.Max(animalHeight, collisionRadius * 2f);
        bodyCollider.center = new Vector3(0f, bodyCollider.height * 0.5f, 0f);
        bodyRigidbody = gameObject.AddComponent<Rigidbody>();
        bodyRigidbody.isKinematic = true;
        bodyRigidbody.useGravity = false;
        bodyRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        bodyRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        rig = AnimalModelFactory.Build(species, transform, rng);
        modelScale *= rig.sizeMul;             // breed size on top of the species scale
        rig.visual.localScale = Vector3.one * modelScale;
        SetupAudio();
        moveDirection = transform.forward;
        motionTimer = RandomRange(settings.wanderMinSeconds, settings.wanderMaxSeconds);
        idleTimer = RandomRange(settings.idleMinSeconds, settings.idleMaxSeconds);
        if (Species == AnimalSpecies.Cat)
        {
            catIdle = true;
            PickCatIdlePose();
            RegisterInteractable();
        }
        else if (Species == AnimalSpecies.Dog) RegisterInteractable();
        InitLife(homeSpot, seed);
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++) SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
    }

    void RegisterInteractable()
    {
        InteractionSystem sys = InteractionSystem.Instance;
        if (sys == null || registered) return;
        registeredSystem = sys;
        registeredSystem.Register(this);
        registered = true;
    }

    void UnregisterInteractable()
    {
        if (registered && registeredSystem != null) registeredSystem.Unregister(this);
        registered = false;
        registeredSystem = null;
    }

    void OnDestroy()
    {
        ReleaseHome();
        UnregisterInteractable();
    }

    public void Interact()
    {
        if (!CanInteract) return;
        float patSeconds = Species == AnimalSpecies.Dog ? settings.dogPatSeconds : settings.catPatSeconds;
        patUntil = Time.time + patSeconds;
        Trust += Species == AnimalSpecies.Dog ? 0.1f : 0.2f;
        CancelErrand(); escorting = false;
        FPAnimation.FPEvents.RaisePatStarted(transform, Species == AnimalSpecies.Dog, patSeconds);
        wakeUntil = patUntil + 12f;
        PlayPat();
        catIdle = false;
        motion = Motion.Pat;
        motionTimer = patSeconds;
        if (director != null && director.Player != null)
        {
            Vector3 toPlayer = director.Player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f) moveDirection = toPlayer.normalized;
        }
    }

    void Update()
    {
        if (director == null || !director.Running || director.Player == null || director.Ctx == null)
        {
            WorldUtil.Kill(gameObject);
            return;
        }
        if (owner == null || owner.IsShuttingDown) return;

        Vector3 playerPos = director.Player.position;
        Vector3 fromPlayer = transform.position - playerPos;
        fromPlayer.y = 0f;
        float playerDistance = fromPlayer.magnitude;
        if (playerDistance > settings.despawnRadius)
        {
            owner.Remove(this);
            WorldUtil.Kill(gameObject);
            return;
        }

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        lastPlayerDistance = playerDistance;
        if (hidden && UpdateHidden(playerDistance)) return;      // rabbit inside its burrow
        UpdateLifeShared(dt, playerDistance);
        alertPose = false;
        if (errand == Errand.None) resting = false;
        UpdateSleep(playerDistance);
        UpdateAmbientSound();
        ScanCars();
        if (sleeping && haveThreat && threat.ttc < 3f) { sleeping = false; wakeUntil = Time.time + RandomRange(10f, 20f); }
        if (sleeping) { /* asleep: no wandering, no fleeing until disturbed */ }
        else if (UpdateVehicleAvoid(dt)) { /* dodging / barking at a car */ }
        else if (UpdateSocial(dt, playerDistance)) { /* playing / chasing / following another animal */ }
        else if (UpdateReactions(playerDistance, fromPlayer, dt)) { /* alert / curious / startled by the player */ }
        else if (Species == AnimalSpecies.Dog && UpdateEscort(playerDistance, fromPlayer, dt)) { /* trotting along with the player */ }
        else if (UpdateRoutine(dt, playerDistance, fromPlayer)) { /* heading home / sunbathing / drinking / resting */ }
        else if (Species == AnimalSpecies.Cat) UpdateCatBehavior(playerDistance, fromPlayer, dt);
        else if (Species == AnimalSpecies.Dog) UpdateDogBehavior(playerDistance, fromPlayer, dt);
        else UpdateWildlifeBehavior(playerDistance, fromPlayer, dt);
        MoveAndAnimate(dt, playerDistance);
        if (registeredSystem == null && ((Species == AnimalSpecies.Cat && catIdle) || Species == AnimalSpecies.Dog)) RegisterInteractable();
    }

    void UpdateWildlifeBehavior(float playerDistance, Vector3 fromPlayer, float dt)
    {
        bool sprint = PlayerSprinting;
        float fleeDistance = EffFlee() * (sprint ? 2.3f : 1f);
        if (playerDistance < fleeDistance && fleeCooldown <= 0f)
        {
            if (motion != Motion.Flee)
            {
                PlayReaction(); AlertNeighbours(fromPlayer.sqrMagnitude > 0.01f ? fromPlayer.normalized : transform.forward);
                if (settings.enableReactions) Trust -= sprint ? 0.12f : 0.04f;
            }
            Vector3 away = fromPlayer.sqrMagnitude > 0.01f ? fromPlayer.normalized : transform.forward;
            if (motion != Motion.Flee && TryBurrowDash(away)) { fleeCooldown = 0.8f; return; }
            Vector3 side = Vector3.Cross(Vector3.up, away) * RandomRange(-0.32f, 0.32f);
            moveDirection = (away + side).normalized;
            motion = Motion.Flee;
            motionTimer = RandomRange(3.5f, 5.5f);
            fleeCooldown = 0.8f;
        }
        fleeCooldown = Mathf.Max(0f, fleeCooldown - dt);
        motionTimer -= dt;
        if (motionTimer <= 0f)
        {
            if (motion == Motion.Flee && playerDistance < fleeDistance * 1.6f)
            {
                Vector3 away = fromPlayer.sqrMagnitude > 0.01f ? fromPlayer.normalized : transform.forward;
                moveDirection = (away + Vector3.Cross(Vector3.up, away) * RandomRange(-0.45f, 0.45f)).normalized;
                motion = Motion.Flee;
                motionTimer = RandomRange(1.8f, 3.2f);
            }
            else if (motion == Motion.Idle)
            {
                ChooseWanderDirection();
                motion = Motion.Walk;
                motionTimer = RandomRange(settings.wanderMinSeconds, settings.wanderMaxSeconds);
            }
            else
            {
                motion = Motion.Idle;
                motionTimer = RandomRange(settings.idleMinSeconds, settings.idleMaxSeconds);
            }
        }
    }

    void UpdateCatBehavior(float playerDistance, Vector3 fromPlayer, float dt)
    {
        if (Time.time < patUntil)
        {
            motion = Motion.Pat;
            catIdle = false;
            return;
        }
        if (motion == Motion.Pat)
        {
            catIdle = true;
            motion = Motion.Idle;
            PickCatIdlePose();
            motionTimer = RandomRange(settings.idleMinSeconds, settings.idleMaxSeconds);
        }

        motionTimer -= dt;
        if (motionTimer <= 0f)
        {
            if (motion == Motion.Walk || motion == Motion.Run)
            {
                motion = Motion.Idle;
                catIdle = true;
                PickCatIdlePose();
                motionTimer = RandomRange(settings.idleMinSeconds, settings.idleMaxSeconds);
            }
            else
            {
                ChooseWanderDirection();
                motion = RandomValue() < 0.12f ? Motion.Run : Motion.Walk;
                catIdle = false;
                motionTimer = RandomRange(2.0f, 5.0f);
            }
        }
        if (catIdle && playerDistance < 5f && fromPlayer.sqrMagnitude > 0.01f)
        {
            Vector3 toPlayer = -fromPlayer.normalized;
            float targetYaw = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
            Quaternion target = Quaternion.Euler(0f, targetYaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, dt * 1.2f);
        }
    }

    // Dogs are energetic: mostly running/zooming, short breaks, drawn toward the player and circling.
    void UpdateDogBehavior(float playerDistance, Vector3 fromPlayer, float dt)
    {
        if (Time.time < patUntil)
        {   // being patted: stop, turn toward the player and enjoy it
            motion = Motion.Pat;
            if (fromPlayer.sqrMagnitude > 0.01f)
            {
                Vector3 toPlayer = -fromPlayer.normalized;
                Quaternion face = Quaternion.Euler(0f, Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, face, dt * 4f);
            }
            return;
        }
        if (motion == Motion.Pat)
        {
            motion = Motion.Idle;
            motionTimer = RandomRange(0.6f, 1.6f);
        }
        if (playerDistance < 10f && Time.time > nextBarkTime)
        {
            nextBarkTime = Time.time + RandomRange(6f, 14f);
            PlayReaction();
        }
        motionTimer -= dt;
        if (motionTimer > 0f) return;

        bool wasIdle = motion == Motion.Idle;
        if (!wasIdle && RandomValue() < 0.3f)
        {
            motion = Motion.Idle;                                   // quick breather
            motionTimer = RandomRange(1.0f, 2.8f);
            return;
        }
        ChooseWanderDirection();
        if (playerDistance > 4f && playerDistance < 22f && RandomValue() < 0.4f)
        {   // run toward the player
            Vector3 toPlayer = -fromPlayer.normalized;
            moveDirection = (toPlayer + Vector3.Cross(Vector3.up, toPlayer) * RandomRange(-0.5f, 0.5f)).normalized;
        }
        else if (playerDistance <= 4f && fromPlayer.sqrMagnitude > 0.01f)
        {   // too close: circle around the player
            Vector3 away = fromPlayer.normalized;
            moveDirection = (Vector3.Cross(Vector3.up, away) * (RandomValue() < 0.5f ? 1f : -1f) + away * 0.3f).normalized;
        }
        motion = RandomValue() < 0.72f ? Motion.Run : Motion.Walk;
        motionTimer = RandomRange(1.3f, 3.6f);
    }

    void PickCatIdlePose()
    {
        // Five idle animations: 0 loaf, 1 sit & look around, 2 grooming (paw wash), 3 stretch, 4 sleepy loaf.
        int next = rng.Next(5);
        if (next == catIdlePose) next = (next + 1 + rng.Next(4)) % 5;
        catIdlePose = next;
        groomSide = rng.Next(2);
        catPoseTimer = RandomRange(5.0f, 9.0f);
        catLookPhase = RandomRange(0f, Mathf.PI * 2f);
    }

    void ChooseWanderDirection()
    {
        float a = RandomRange(-180f, 180f);
        moveDirection = Quaternion.Euler(0f, a, 0f) * transform.forward;
        moveDirection.y = 0f;
        if (moveDirection.sqrMagnitude < 0.01f) moveDirection = transform.forward;
        moveDirection.Normalize();
        moveDirection = LeashDirection(moveDirection);
    }

    void MoveAndAnimate(float dt, float playerDistance)
    {
        bool moving = motion == Motion.Walk || motion == Motion.Run || motion == Motion.Flee;
        float speed = 0f;
        if (moving)
        {
            if (Species == AnimalSpecies.Deer) speed = motion == Motion.Walk ? settings.deerWalkSpeed : settings.deerRunSpeed;
            else if (Species == AnimalSpecies.Rabbit) speed = motion == Motion.Walk ? settings.rabbitWalkSpeed : settings.rabbitRunSpeed;
            else if (Species == AnimalSpecies.Dog) speed = motion == Motion.Walk ? settings.dogWalkSpeed : settings.dogRunSpeed;
            else speed = motion == Motion.Run ? settings.catRunSpeed : settings.catWalkSpeed;
            speed *= socialSpeedMul * personalitySpeed * CarSpeedBoost();

            Vector3 candidateDir = moveDirection;
            if (!CanMoveInDirection(candidateDir, speed * dt + 0.35f))
            {
                Vector3 sideA = Quaternion.Euler(0f, 55f, 0f) * candidateDir;
                Vector3 sideB = Quaternion.Euler(0f, -55f, 0f) * candidateDir;
                if (CanMoveInDirection(sideA, speed * dt + 0.35f)) candidateDir = sideA;
                else if (CanMoveInDirection(sideB, speed * dt + 0.35f)) candidateDir = sideB;
                else
                {
                    motion = Motion.Idle;
                    motionTimer = RandomRange(0.8f, 1.8f);
                    moving = false;
                }
                moveDirection = candidateDir.normalized;
            }
            if (moving)
            {
                Quaternion face = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, face, dt * settings.turnSpeed);
                Vector3 next = transform.position + moveDirection * speed * dt;
                float y;
                if (TryGround(next.x, next.z, out y))
                {
                    next.y = y;
                    if (bodyRigidbody != null) bodyRigidbody.MovePosition(next);
                    else transform.position = next;
                }
                else
                {
                    motion = Motion.Idle;
                    motionTimer = RandomRange(1f, 2f);
                    moving = false;
                }
            }
        }

        bool fast = motion == Motion.Flee || motion == Motion.Run;
        // Measure how fast the animal is really travelling and drive the gait from that,
        // so limb cycles always match the distance covered (no sliding feet, even when blocked).
        {
            Vector3 pos = transform.position;
            float rdt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 delta = pos - lastPos;
            delta.y = 0f;
            float inst = hasLastPos ? delta.magnitude / rdt : 0f;
            lastPos = pos;
            hasLastPos = true;
            measuredSpeed = Ease(measuredSpeed, inst, 1f - Mathf.Exp(-12f * rdt));
        }
        if (moving) AdvanceGait(dt, fast);

        if (motion != lastMotion)
        {
            lastMotion = motion;
            stateClock = 0f;
            if (Species == AnimalSpecies.Rabbit && motion == Motion.Idle) PickRabbitAction();
            if (Species == AnimalSpecies.Dog && motion == Motion.Idle) { dogIdlePose = dogBowNext ? 2 : rng.Next(4); dogBowNext = false; if (escorting) dogIdlePose = 1; }
        }
        stateClock += dt;
        if (Species == AnimalSpecies.Rabbit && motion == Motion.Idle)
        {
            rabbitActionTimer -= dt;
            if (rabbitActionTimer <= 0f) PickRabbitAction();
        }
        if (Species == AnimalSpecies.Cat && catIdle)
        {
            catPoseTimer -= dt;
            if (catPoseTimer <= 0f) PickCatIdlePose();
        }

        BuildTarget(moving, fast);
        BlendPose(dt);
        ApplyPose();
    }

    // ---------------------------------------------------------------------------------
    // Procedural animation. BuildTarget fills `target` with the desired pose, BlendPose eases
    // `cur` toward it (so every transition is smooth) and ApplyPose writes it to the rig.
    // All leg angles are WORLD tilts (0 = hanging straight down, negative = foot forward);
    // legs only swing around X, so left/right limbs can never cross over each other.
    // The visual root is rested on the lowest foot/belly point every frame, and "planted"
    // legs are stretched/shrunk so the paw always lands on the ground: no floating, no sinking.
    // ---------------------------------------------------------------------------------
    sealed class Pose
    {
        public float pitch, roll, hop, neck, nyaw, head, hyaw, hroll, tyaw, tpitch, tcurl, earL, earR, tongue, eye = 1f;
        public bool tuft;
        public float[] leg = new float[4];
        public bool[] plant = new bool[4];
        public void Clear()
        {
            pitch = roll = hop = neck = nyaw = head = hyaw = hroll = tyaw = tpitch = tcurl = earL = earR = tongue = 0f;
            eye = 1f; tuft = false;
            for (int i = 0; i < 4; i++) { leg[i] = 0f; plant[i] = false; }
        }
    }

    readonly Pose target = new Pose();
    readonly Pose cur = new Pose();
    readonly float[] plantAmt = new float[4];
    Motion lastMotion = Motion.Pat;
    float stateClock;
    int rabbitAction;
    float rabbitActionTimer;
    int groomSide;
    int dogIdlePose;
    float nextBarkTime;
    float seedPhase = -1f;

    Vector3 lastPos;
    bool hasLastPos;
    float measuredSpeed;
    float gaitAmp = 20f;

    // Phase advances in proportion to ground speed: one foot stroke covers 2*L*sin(amp) of ground.
    void AdvanceGait(float dt, bool fast)
    {
        float v = measuredSpeed;
        if (Species == AnimalSpecies.Rabbit)
        {
            float hopLen = (fast ? 1.0f : 0.35f) * modelScale;   // distance covered per hop
            gait += dt * Mathf.PI * v / hopLen;                    // hop = half a sin cycle
            return;
        }
        float legLen = rig.legLength[0] * modelScale;
        float pref = Species == AnimalSpecies.Deer ? (fast ? 13f : 10f) : (Species == AnimalSpecies.Dog ? (fast ? 16f : 11f) : (fast ? 17f : 12f));
        float maxAmp = fast ? 45f : 32f;
        float sinA = Mathf.PI * v / (pref * legLen);
        float sMax = Mathf.Sin(maxAmp * Mathf.Deg2Rad);
        float w = pref;
        if (sinA > sMax)
        {
            sinA = sMax;
            w = Mathf.PI * v / (legLen * sinA);   // very fast: cadence rises to keep feet planted
        }
        gaitAmp = Mathf.Asin(Mathf.Clamp(sinA, 0f, 0.95f)) * Mathf.Rad2Deg;
        gait += dt * w;
    }

    static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    static float Ease(float a, float b, float k) { return a + (b - a) * k; }

    // Occasional quick ear twitch (degrees).
    float Flick(float time, float offset)
    {
        float s = Mathf.Sin(time * 1.9f + offset);
        return s > 0.93f ? (s - 0.93f) * 380f : 0f;
    }

    // ------------------------------------------------------------------ sounds
    void SetupAudio()
    {
        sounds = settings.SoundsFor(Species);
        AnimalSounds.Fill(sounds, Species == AnimalSpecies.Deer ? "deers" : (Species == AnimalSpecies.Rabbit ? "bunnies" : (Species == AnimalSpecies.Cat ? "cats" : "dogs")));
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.loop = false;
        audioSrc.priority = 190;
        SpatialAudio.Configure(audioSrc, sounds.minDistance, sounds.maxDistance);   // strong 3D: inverse falloff, air absorption, behind-you filter
        ScheduleAmbient(true);
    }

    void ScheduleAmbient(bool first)
    {
        float lo = Mathf.Max(0.5f, sounds.ambientInterval.x), hi = Mathf.Max(lo, sounds.ambientInterval.y);
        nextCallTime = Time.time + RandomRange(first ? 0f : lo, hi);
    }

    /// <summary>Random clip, but never the one that played last (when there is a choice).</summary>
    AudioClip PickClip(AudioClip[] list, System.Random r)
    {
        if (list == null || list.Length == 0) return null;
        int start = r.Next(list.Length);
        AudioClip fallback = null;
        for (int i = 0; i < list.Length; i++)
        {
            AudioClip c = list[(start + i) % list.Length];
            if (c == null) continue;
            if (c != lastClip) return c;
            fallback = c;
        }
        return fallback;
    }

    // kind: 0 = ambient call (already paced by AnimalCaller), 1 = reaction (startled, alerted...), 2 = pat
    const int KindAmbient = 0, KindReaction = 1, KindPat = 2;

    void PlayClip(AudioClip clip, int kind = KindAmbient)
    {
        if (clip == null || audioSrc == null) return;
        if (sleeping || motion == Motion.Sleep) return;   // asleep: silent
        bool cat = Species == AnimalSpecies.Cat;
        if (cat)
        {
            // cats never spam: one meow at a time, with a real pause after each one (shorter pause after a pat, which is feedback for the player)
            if (Time.time < voiceFreeAt) return;
            if (kind == KindReaction && !AnimalSounds.CalmReady()) return;   // never over another cat
        }
        float pitch = RandomRange(Mathf.Min(sounds.pitchRange.x, sounds.pitchRange.y), Mathf.Max(sounds.pitchRange.x, sounds.pitchRange.y));
        audioSrc.pitch = pitch;
        audioSrc.PlayOneShot(clip, AnimalSounds.Vol(sounds));
        lastClip = clip;
        if (cat)
        {
            if (kind == KindReaction) AnimalSounds.CalmUsed();
            float len = clip.length / Mathf.Max(0.1f, pitch);
            voiceFreeAt = Time.time + len + (kind == KindPat ? RandomRange(1.5f, 2.5f) : RandomRange(5f, 9f));
        }
    }

    // ------------------------------------------------------------ social behaviour
    // Animals sometimes interact: dogs play tag with dogs and chase (then bow to) cats, cats greet or chase
    // each other, rabbits play tag with binky jumps, deer follow each other or play tag. Startled wildlife
    // alerts neighbours so herds/groups bolt together. The player always takes priority.
    enum SocialKind { None, DogTag, DogChase, CatGreet, CatTag, RabbitTag, DeerFollow, DeerTag }
    enum SocialRole { None, Chaser, Runner, Follower, Greeter }

    SocialKind socialKind = SocialKind.None;
    SocialRole socialRole = SocialRole.None;
    AnimalAgent partner;
    float socialUntil, swapLock, pauseUntil, nextSocialCheck, socialCooldownUntil, binkyUntil, nextBinky;
    float socialSpeedMul = 1f;
    bool arrived, dogBowNext;

    bool IsDomestic { get { return Species == AnimalSpecies.Cat || Species == AnimalSpecies.Dog; } }

    bool AvailableForSocial()
    {
        return !sleeping && socialRole == SocialRole.None && Time.time >= socialCooldownUntil && Time.time >= patUntil
            && Time.time >= wakeUntil && !hidden && errand == Errand.None && !escorting && motion != Motion.Flee && motion != Motion.Pat && motion != Motion.Sleep;
    }

    // Returns true while this animal is steering itself for a social activity.
    bool UpdateSocial(float dt, float playerDistance)
    {
        if (settings.socialChance <= 0f) return false;
        float fleeDist = Species == AnimalSpecies.Deer ? settings.deerFleeDistance : settings.rabbitFleeDistance;
        bool playerTooClose = IsDomestic ? playerDistance < 3f : playerDistance < fleeDist;

        if (socialRole == SocialRole.None)
        {
            if (!playerTooClose && Time.time >= nextSocialCheck && owner != null)
            {
                nextSocialCheck = Time.time + RandomRange(1.5f, 3.5f);
                if (AvailableForSocial() && RandomValue() < settings.socialChance) TryBeginSocial();
            }
            if (socialRole == SocialRole.None) return false;
        }

        bool follow = socialKind == SocialKind.DeerFollow;
        bool valid = partner != null && !partner.sleeping && Time.time < socialUntil && !playerTooClose
            && motion != Motion.Flee && motion != Motion.Pat && partner.motion != Motion.Flee && partner.motion != Motion.Pat
            && (follow || partner.partner == this);
        if (!valid) { EndSocial(true); return false; }

        Vector3 to = partner.transform.position - transform.position;
        to.y = 0f;
        float d = to.magnitude;
        Vector3 dirTo = d > 0.01f ? to / d : transform.forward;
        socialSpeedMul = 1f;
        if (IsDomestic && Species == AnimalSpecies.Cat) catIdle = false;

        if (Time.time < pauseUntil)
        {   // short breather after a catch
            motion = Motion.Idle;
            FaceDirection(dirTo, dt);
            return true;
        }

        switch (socialRole)
        {
            case SocialRole.Chaser:
            {
                float stop = Species == AnimalSpecies.Deer ? 2.2f : 1.7f;
                if (socialKind == SocialKind.DogChase) stop = 2.6f;
                if (d < stop && Time.time > swapLock)
                {
                    if (socialKind == SocialKind.DogChase) { dogBowNext = true; EndSocial(true); return false; }
                    // tag! swap roles
                    socialRole = SocialRole.Runner; partner.socialRole = SocialRole.Chaser;
                    swapLock = partner.swapLock = Time.time + 1.4f;
                    pauseUntil = Time.time + 0.2f; partner.pauseUntil = Time.time + 0.5f;
                    if (Species == AnimalSpecies.Rabbit) { binkyUntil = Time.time + 0.9f; }
                    return true;
                }
                moveDirection = dirTo;
                motion = Motion.Run;
                socialSpeedMul = socialKind == SocialKind.DogChase ? 0.62f : (Species == AnimalSpecies.Cat ? 1.5f : 1f);
                break;
            }
            case SocialRole.Runner:
            {
                Vector3 away = -dirTo;
                Vector3 perp = Vector3.Cross(Vector3.up, away);
                moveDirection = (away + perp * Mathf.Sin(Time.time * 2.3f + seedPhase) * 0.8f).normalized;
                motion = d > 14f ? Motion.Walk : Motion.Run;
                socialSpeedMul = socialKind == SocialKind.DogChase ? 1.7f : (Species == AnimalSpecies.Cat ? 1.5f : 0.95f);
                if (Species == AnimalSpecies.Rabbit && Time.time > nextBinky && d < 6f)
                { binkyUntil = Time.time + 0.8f; nextBinky = Time.time + RandomRange(1.6f, 3.2f); }
                break;
            }
            case SocialRole.Follower:
            {
                if (d > 4.5f) { moveDirection = dirTo; motion = d > 12f ? Motion.Run : Motion.Walk; }
                else if (d < 3f) motion = Motion.Idle;   // graze beside the leader
                break;
            }
            default: // Greeter
            {
                if (d > 1.3f) { moveDirection = dirTo; motion = Motion.Walk; }
                else
                {
                    motion = Motion.Idle;
                    if (Species == AnimalSpecies.Cat) catIdle = true;
                    FaceDirection(dirTo, dt);
                    if (!arrived)
                    {
                        arrived = true;
                        socialUntil = Time.time + RandomRange(2.5f, 4.5f);
                        if (partner != null) { partner.arrived = true; partner.socialUntil = socialUntil; }
                    }
                }
                break;
            }
        }
        return true;
    }

    void FaceDirection(Vector3 dir, float dt)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion face = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, face, dt * 5f);
    }

    void TryBeginSocial()
    {
        float range = Species == AnimalSpecies.Deer ? 18f : (Species == AnimalSpecies.Rabbit ? 10f : (Species == AnimalSpecies.Dog ? 14f : 9f));
        AnimalAgent best = null;
        float bestD = range * range;
        System.Collections.Generic.List<AnimalAgent> all = owner.Animals;
        for (int i = 0; i < all.Count; i++)
        {
            AnimalAgent o = all[i];
            if (o == null || o == this || !o.AvailableForSocial()) continue;
            bool ok = (o.Species == Species) || (Species == AnimalSpecies.Dog && o.Species == AnimalSpecies.Cat);
            if (!ok) continue;
            if (o.IsDomestic && director.Player != null && (o.transform.position - director.Player.position).sqrMagnitude < 9f) continue;
            float d2 = (o.transform.position - transform.position).sqrMagnitude;
            if (d2 < bestD) { bestD = d2; best = o; }
        }
        if (best == null) return;

        float now = Time.time;
        SocialKind kind;
        SocialRole mine = SocialRole.Chaser, theirs = SocialRole.Runner;
        float dur;
        if (Species == AnimalSpecies.Dog && best.Species == AnimalSpecies.Cat) { kind = SocialKind.DogChase; dur = 8f; }
        else if (Species == AnimalSpecies.Dog) { kind = SocialKind.DogTag; dur = RandomRange(14f, 26f); }
        else if (Species == AnimalSpecies.Rabbit) { kind = SocialKind.RabbitTag; dur = RandomRange(7f, 14f); }
        else if (Species == AnimalSpecies.Cat)
        {
            if (RandomValue() < 0.5f) { kind = SocialKind.CatGreet; mine = theirs = SocialRole.Greeter; dur = 14f; }
            else { kind = SocialKind.CatTag; dur = RandomRange(5f, 9f); }
        }
        else
        {
            if (RandomValue() < 0.6f) { kind = SocialKind.DeerFollow; mine = SocialRole.Follower; theirs = SocialRole.None; dur = RandomRange(15f, 25f); }
            else { kind = SocialKind.DeerTag; dur = RandomRange(6f, 10f); }
        }

        socialKind = kind; socialRole = mine; partner = best; socialUntil = now + dur; swapLock = now + 1.5f; arrived = false;
        if (theirs != SocialRole.None)
        {
            best.socialKind = kind; best.socialRole = theirs; best.partner = this; best.socialUntil = now + dur;
            best.swapLock = now + 1.5f; best.arrived = false;
            if (best.Species == AnimalSpecies.Cat) best.catIdle = false;
        }
    }

    void EndSocial(bool propagate)
    {
        AnimalAgent p = partner;
        bool wasFollow = socialKind == SocialKind.DeerFollow;
        socialKind = SocialKind.None; socialRole = SocialRole.None; partner = null;
        socialSpeedMul = 1f;
        socialCooldownUntil = Time.time + RandomRange(25f, 60f);
        if (motion != Motion.Flee && motion != Motion.Pat && motion != Motion.Sleep)
        {
            motion = Motion.Idle;
            motionTimer = RandomRange(1f, 3f);
        }
        if (Species == AnimalSpecies.Cat) { catIdle = true; PickCatIdlePose(); }
        if (propagate && p != null && !wasFollow && p.partner == this) p.EndSocial(false);
    }

    /// <summary>Another animal bolted nearby: wake up and run too.</summary>
    public void StartleFrom(Vector3 away)
    {
        if (IsDomestic || motion == Motion.Flee) return;
        if (socialRole != SocialRole.None) EndSocial(true);
        sleeping = false;
        wakeUntil = Time.time + RandomRange(25f, 45f);
        moveDirection = (away + Vector3.Cross(Vector3.up, away) * RandomRange(-0.4f, 0.4f)).normalized;
        motion = Motion.Flee;
        motionTimer = RandomRange(3f, 5f);
        fleeCooldown = 0.8f;
    }

    void AlertNeighbours(Vector3 away)
    {
        if (owner == null) return;
        System.Collections.Generic.List<AnimalAgent> all = owner.Animals;
        for (int i = 0; i < all.Count; i++)
        {
            AnimalAgent o = all[i];
            if (o == null || o == this || o.IsDomestic) continue;
            if ((o.transform.position - transform.position).sqrMagnitude < 9f * 9f) o.StartleFrom(away);
        }
    }

    // ------------------------------------------------------------------ sleeping
    bool sleeping, wantSleep, lastNight, sleepInit;
    float sleepSwitchAt, wakeUntil, sleepSide = 1f;
    int sleepVariant;

    // Animals lie down at night (staggered so they don't all drop at once) and wake when the player gets close.
    void UpdateSleep(float playerDistance)
    {
        bool night = AnimalSleepClock.IsNight(settings);
        if (!sleepInit) { sleepInit = true; lastNight = night; wantSleep = night; sleepSwitchAt = 0f; }
        if (night != lastNight) { lastNight = night; sleepSwitchAt = Time.time + RandomRange(1f, 30f); }
        if (Time.time >= sleepSwitchAt) wantSleep = night;

        float wakeDist = Species == AnimalSpecies.Deer ? 7f : (Species == AnimalSpecies.Rabbit ? 5f : (Species == AnimalSpecies.Dog ? 1.5f : 1.3f));
        bool disturbed = playerDistance < wakeDist;
        if (sleeping && disturbed) wakeUntil = Time.time + RandomRange(25f, 45f);
        bool allowed = Time.time >= wakeUntil && Time.time >= patUntil && motion != Motion.Flee && motion != Motion.Pat;
        bool should = wantSleep && allowed && !disturbed && SleepSpotReady();
        if (should && !sleeping)
        {
            if (socialRole != SocialRole.None) EndSocial(true);
            sleeping = true;
            motion = Motion.Sleep;
            sleepVariant = rng.Next(3);
            sleepSide = rng.Next(2) == 0 ? -1f : 1f;
            if (Species == AnimalSpecies.Cat) catIdle = true;   // sleeping cats can still be patted
        }
        else if (!should && sleeping)
        {
            sleeping = false;
            EndErrand();
            if (motion == Motion.Sleep) { motion = Motion.Idle; motionTimer = RandomRange(1f, 3f); }
        }
    }

    // Three sleep variants per species. Legs are "planted" so paws/belly always rest on the ground.
    void BuildSleepTarget(Pose t, float time)
    {
        float br = Mathf.Sin(time * 1.6f);       // slow breathing
        t.eye = 0.05f;
        t.pitch = br * 0.7f;
        int v = sleepVariant % 3;
        switch (Species)
        {
            case AnimalSpecies.Deer:
                for (int i = 0; i < 4; i++) { t.plant[i] = true; t.leg[i] = -75f; }
                t.earL = t.earR = -28f;
                if (v == 0) { t.neck = -5f; t.head = 16f + br * 1.5f; }                     // lying, head up, dozing
                else if (v == 1) { t.neck = 55f; t.nyaw = sleepSide * 105f; t.head = 5f; }  // head tucked back along the flank
                else { t.neck = 70f; t.head = -10f; }                                       // chin resting on the ground
                break;
            case AnimalSpecies.Rabbit:
                if (v == 2)
                {   // flopped on its side
                    t.roll = sleepSide * 72f;
                    t.leg[0] = t.leg[1] = -30f; t.leg[2] = t.leg[3] = 30f;
                    t.head = 10f; t.earL = t.earR = -10f;
                    t.roll += br * 1.5f;
                }
                else
                {   // loaf; v1 tucks the head down with ears flat
                    for (int i = 0; i < 4; i++) t.plant[i] = true;
                    t.head = v == 0 ? 12f : 38f;
                    t.earL = t.earR = v == 0 ? -30f : -65f;
                }
                break;
            default: // cat and dog
            {
                bool dog = Species == AnimalSpecies.Dog;
                if (v == 0)
                {   // curled up, nose toward the flank, tail wrapped
                    for (int i = 0; i < 4; i++) t.plant[i] = true;
                    t.leg[0] = t.leg[1] = -45f;
                    t.head = 42f; t.hyaw = sleepSide * 50f;
                    t.tpitch = -10f; t.tcurl = -35f; t.tyaw = sleepSide * 110f;
                }
                else if (v == 1)
                {   // stretched out on its side
                    t.roll = sleepSide * 78f + br * 1.5f;
                    t.leg[0] = t.leg[1] = -35f; t.leg[2] = t.leg[3] = 28f;
                    t.head = 14f; t.tpitch = -30f; t.tyaw = 20f;
                }
                else
                {   // chin on paws
                    for (int i = 0; i < 4; i++) t.plant[i] = true;
                    t.leg[0] = t.leg[1] = -50f;
                    t.head = 38f;
                    t.tpitch = -10f; t.tcurl = -20f; t.tyaw = sleepSide * 70f;
                }
                t.earL = t.earR = dog ? -12f : -8f;
                break;
            }
        }
    }

    void UpdateAmbientSound()
    {
        if (sounds == null || sleeping) return;
        if (Species == AnimalSpecies.Cat && motion == Motion.Pat) return;
        if (!caller.Tick(sounds, lastPlayerDistance)) return;
        PlayClip(PickClip(sounds.ambientSounds, rng));
    }

    /// <summary>Pet/pat clips: only ever played from the moment of patting.</summary>
    void PlayPat()
    {
        if (sounds == null) return;
        AudioClip[] list = (sounds.patSounds != null && sounds.patSounds.Length > 0) ? sounds.patSounds : sounds.reactionSounds;
        PlayClip(PickClip(list, rng), KindPat);
    }

    void PlayReaction()
    {
        if (sounds == null) return;
        PlayClip(PickClip(sounds.reactionSounds, rng), KindReaction);
    }

    void PickRabbitAction()
    {
        // 0 eat (most common), 1 alert look-around, 2 wash face, 3 sniff
        float r = RandomValue();
        int next = r < 0.42f ? 0 : (r < 0.62f ? 1 : (r < 0.84f ? 2 : 3));
        rabbitAction = next;
        rabbitActionTimer = next == 0 ? RandomRange(3.0f, 6.0f) : RandomRange(1.8f, 3.5f);
    }

    void BuildTarget(bool moving, bool fast)
    {
        Pose t = target;
        t.Clear();
        if (seedPhase < 0f) seedPhase = RandomRange(0f, 50f);
        float time = Time.time + seedPhase;
        if (Mathf.Repeat(time, 4.3f) < 0.13f) t.eye = 0.08f; // blink

        if ((motion == Motion.Sleep || resting) && !moving)
        {
            BuildSleepTarget(t, time);
            if (!sleeping) t.eye = Mathf.Max(t.eye, Mathf.Repeat(time, 4.3f) < 0.13f ? 0.08f : 0.5f);   // resting: drowsy, eyes half open
            return;
        }
        if (Species == AnimalSpecies.Deer) BuildDeerTarget(t, moving, fast, time);
        else if (Species == AnimalSpecies.Rabbit) BuildRabbitTarget(t, moving, fast, time);
        else if (Species == AnimalSpecies.Dog) BuildDogTarget(t, moving, fast, time);
        else BuildCatTarget(t, moving, fast, time);
    }

    void BuildDeerTarget(Pose t, bool moving, bool fast, float time)
    {
        if (moving)
        {
            float amp = gaitAmp;
            for (int i = 0; i < 4; i++)
                t.leg[i] = Mathf.Sin(gait + ((i == 0 || i == 3) ? 0f : Mathf.PI)) * amp;
            t.neck = fast ? -12f : -4f + Mathf.Sin(gait * 2f) * 2.5f;
            t.head = fast ? 10f : 4f;
            t.tpitch = fast ? 35f : 0f;
            t.tyaw = Mathf.Sin(time * (fast ? 12f : 4f)) * (fast ? 14f : 6f);
            t.pitch = fast ? Mathf.Sin(gait * 2f) * 2f : 0f;
            t.earL = t.earR = fast ? -18f : 0f;
            return;
        }

        if (alertPose)
        {   // head high, ears forward, tail up: staring at the player
            t.neck = -14f; t.head = 6f; t.hyaw = Mathf.Sin(time * 0.7f) * 10f;
            t.earL = 12f + Flick(time, 0f) * 0.4f; t.earR = 12f + Flick(time, 1.7f) * 0.4f;
            t.tpitch = 30f;
            return;
        }

        // Grazing cycle: lower the head to the grass, chew, lift, look around, repeat.
        float c = Mathf.Repeat(stateClock, 8f);
        float w;
        if (c < 1.1f) w = Smooth01(c / 1.1f);
        else if (c < 5.8f) w = 1f;
        else if (c < 6.9f) w = 1f - Smooth01((c - 5.8f) / 1.1f);
        else w = 0f;
        t.neck = 122f * w - 8f * (1f - w);
        t.head = -42f * w + 8f * (1f - w) + (w > 0.95f ? Mathf.Sin(time * 7f) * 3f : 0f);
        t.hyaw = Mathf.Sin(time * 0.9f) * 22f * (1f - w);
        t.earL = Flick(time, 0f) - 6f * w;
        t.earR = Flick(time, 1.7f) - 6f * w;
        t.tyaw = Mathf.Sin(time * 3f) * 8f;
        t.tpitch = 6f * (1f - w);
        t.pitch = 2f * w;
    }

    void BuildRabbitTarget(Pose t, bool moving, bool fast, float time)
    {
        if (moving)
        {
            float u = Mathf.Sin(gait);
            float a = Mathf.Abs(u);
            t.hop = a * (fast ? 0.17f : 0.07f);
            if (Time.time < binkyUntil) { t.hop *= 2.1f; t.roll = Mathf.Sin(gait * 2f) * 22f; }
            t.leg[0] = t.leg[1] = -(fast ? 40f : 25f) * a;
            t.leg[2] = t.leg[3] = (fast ? 45f : 28f) * a;
            t.pitch = -Mathf.Cos(gait) * (fast ? 9f : 4f);
            t.head = fast ? -4f : 0f;
            t.earL = t.earR = fast ? -35f : -6f;
            t.tpitch = 10f;
            return;
        }

        t.earL = Flick(time, 0.4f);
        t.earR = Flick(time, 2.1f);
        if (alertPose)
        {
            t.head = -12f; t.hyaw = Mathf.Sin(time * 0.9f) * 8f;
            t.earL += 16f; t.earR += 16f;
            return;
        }
        switch (rabbitAction)
        {
            case 0: // eat grass
                t.head = 48f + Mathf.Sin(time * 16f) * 4f;
                t.pitch = 4f;
                t.tuft = true;
                t.earL += -10f; t.earR += -10f;
                break;
            case 1: // alert, scanning around
                t.head = -8f;
                t.hyaw = Mathf.Sin(time * 1.1f) * 26f;
                t.earL += 10f; t.earR += 10f;
                break;
            case 2: // wash face with both front paws
                t.head = 20f + Mathf.Sin(time * 9f) * 4f;
                t.leg[0] = -125f + Mathf.Sin(time * 10f) * 14f;
                t.leg[1] = -125f + Mathf.Sin(time * 10f + Mathf.PI) * 14f;
                t.eye = Mathf.Min(t.eye, 0.35f);
                t.earL += -22f; t.earR += -22f;
                break;
            default: // sniff
                t.head = 10f + Mathf.Sin(time * 14f) * 3f;
                t.hyaw = Mathf.Sin(time * 1.8f) * 14f;
                t.earL += Mathf.Sin(time * 5f) * 8f;
                t.earR += Mathf.Sin(time * 5f + 1f) * 8f;
                break;
        }
    }

    void BuildDogTarget(Pose t, bool moving, bool fast, float time)
    {
        t.tongue = 0.35f;
        if (moving)
        {
            float amp = gaitAmp;
            for (int i = 0; i < 4; i++)
                t.leg[i] = Mathf.Sin(gait + ((i == 0 || i == 3) ? 0f : Mathf.PI)) * amp;
            t.pitch = Mathf.Sin(gait * 2f) * (fast ? 4f : 1.5f);
            t.head = (fast ? -6f : 0f) + Mathf.Sin(gait * 2f) * 3f;
            t.tyaw = Mathf.Sin(time * 13f) * (fast ? 24f : 18f);
            t.tpitch = fast ? 18f : 8f;
            t.earL = t.earR = (fast ? -20f : 0f) + Mathf.Sin(gait * 2f + 1f) * (fast ? 10f : 3f);
            t.tongue = fast ? 1f : 0.5f;
            return;
        }

        if (motion == Motion.Pat)   // happy sit: head lifted into the hand, eyes closed, big wag
        {
            for (int i = 0; i < 4; i++) t.plant[i] = true;
            t.pitch = -26f;
            t.leg[0] = t.leg[1] = 0f;
            t.leg[2] = t.leg[3] = -55f;
            t.head = 8f + Mathf.Sin(time * 3f) * 3f;
            t.hyaw = Mathf.Sin(time * 1.5f) * 6f;
            t.eye = 0.12f;
            t.tyaw = Mathf.Sin(time * 14f) * 32f;
            t.tongue = 1f;
            t.earL = t.earR = -6f;
            return;
        }
        t.earL = Flick(time, 0.5f);
        t.earR = Flick(time, 2.3f);
        t.tyaw = Mathf.Sin(time * 12f) * 22f;
        t.tongue = 0.8f + Mathf.Sin(time * 9f) * 0.2f;   // panting
        switch (dogIdlePose)
        {
            case 0: // stand, pant, look around
                t.head = -3f;
                t.hyaw = Mathf.Sin(time * 1.3f) * 30f;
                t.tpitch = 10f;
                break;
            case 1: // sit
                for (int i = 0; i < 4; i++) t.plant[i] = true;
                t.pitch = -26f;
                t.leg[0] = t.leg[1] = 0f;
                t.leg[2] = t.leg[3] = -55f;
                t.head = 14f;
                t.hyaw = Mathf.Sin(time * 1.1f) * 25f;
                t.tyaw = Mathf.Sin(time * 9f) * 28f;
                break;
            case 2: // play bow: chest down, rear up, tail high
                for (int i = 0; i < 4; i++) t.plant[i] = true;
                t.pitch = 24f + Mathf.Sin(time * 6f) * 3f;
                t.leg[0] = t.leg[1] = -50f;
                t.head = -24f;
                t.tpitch = 30f;
                t.tongue = 1f;
                break;
            default: // sniff the ground
                t.head = 42f + Mathf.Sin(time * 9f) * 3f;
                t.hyaw = Mathf.Sin(time * 2f) * 12f;
                t.pitch = 5f;
                t.tongue = 0.2f;
                break;
        }
    }

    void BuildCatTarget(Pose t, bool moving, bool fast, float time)
    {
        if (moving)
        {
            float amp = gaitAmp;
            for (int i = 0; i < 4; i++)
                t.leg[i] = Mathf.Sin(gait + ((i == 0 || i == 3) ? 0f : Mathf.PI)) * amp;
            t.head = fast ? 6f : 0f;
            t.tpitch = 18f;
            t.tyaw = Mathf.Sin(time * 2.2f) * 10f;
            t.pitch = fast ? Mathf.Sin(gait * 2f) * 3f : 0f;
            return;
        }

        t.earL = Flick(time, 0.9f);
        t.earR = Flick(time, 2.6f);
        for (int i = 0; i < 4; i++) t.plant[i] = true;

        if (motion == Motion.Pat) // being petted: sits up, eyes closed, happy tail
        {
            SitPose(t);
            t.head = 6f + Mathf.Sin(time * 9f) * 1.5f;
            t.eye = 0.12f;
            t.tpitch = 25f;
            t.tyaw = 30f + Mathf.Sin(time * 3.2f) * 14f;
            return;
        }

        switch (catIdlePose)
        {
            case 0: // loaf
                LoafPose(t);
                t.head = 5f + Mathf.Sin(time * 1.2f) * 1.5f;
                t.pitch = Mathf.Sin(time * 1.8f) * 0.8f;
                t.eye = Mathf.Min(t.eye, 0.75f);
                t.tyaw = 70f + Mathf.Sin(time * 0.7f) * 8f;
                break;
            case 1: // sit and look around, tail swishing
                SitPose(t);
                t.head = 15f;
                t.hyaw = Mathf.Sin(time * 0.9f) * 35f;
                t.tyaw = 30f + Mathf.Sin(time * 1.6f) * 18f;
                break;
            case 2: // grooming: lick a raised front paw
                SitPose(t);
                {
                    int raised = groomSide;
                    t.plant[raised] = false;
                    t.leg[raised] = -115f + Mathf.Sin(time * 7f) * 14f;
                    t.head = 40f + Mathf.Sin(time * 7f) * 4f;
                    t.hyaw = groomSide == 0 ? -12f : 12f;
                    t.eye = Mathf.Min(t.eye, 0.5f);
                    t.tyaw = 30f;
                }
                break;
            case 3: // stretch: chest low, rear up, tail high
                t.pitch = 26f + Mathf.Sin(time * 1.5f) * 1.5f;
                t.leg[0] = t.leg[1] = -50f;
                t.head = -26f;
                t.tpitch = 25f;
                t.tyaw = Mathf.Sin(time * 2f) * 8f;
                break;
            default: // sleepy loaf, head resting, slow breathing
                LoafPose(t);
                t.head = 38f;
                t.pitch = Mathf.Sin(time * 1.1f) * 1.2f;
                t.eye = 0.1f;
                t.earL -= 8f; t.earR -= 8f;
                t.tyaw = 70f;
                break;
        }
    }

    static void LoafPose(Pose t)
    {
        t.leg[0] = t.leg[1] = -45f; // front paws tucked forward, hind legs folded under
        t.tpitch = -10f;
        t.tcurl = -20f;
    }

    static void SitPose(Pose t)
    {
        t.pitch = -22f;
        t.leg[0] = t.leg[1] = 0f;
        t.leg[2] = t.leg[3] = -55f;
    }

    void BlendPose(float dt)
    {
        float k = 1f - Mathf.Exp(-9f * dt);
        float kl = 1f - Mathf.Exp(-45f * dt);
        Pose c = cur, t = target;
        c.pitch = Ease(c.pitch, t.pitch, k);
        c.roll = Ease(c.roll, t.roll, k);
        c.hop = t.hop;
        c.neck = Ease(c.neck, t.neck, k);
        c.head = Ease(c.head, t.head, k);
        c.hyaw = Ease(c.hyaw, t.hyaw, k);
        c.hroll = Ease(c.hroll, t.hroll, k);
        c.nyaw = Ease(c.nyaw, t.nyaw, k);
        c.tyaw = Ease(c.tyaw, t.tyaw, k);
        c.tpitch = Ease(c.tpitch, t.tpitch, k);
        c.tcurl = Ease(c.tcurl, t.tcurl, k);
        c.earL = Ease(c.earL, t.earL, kl);
        c.earR = Ease(c.earR, t.earR, kl);
        c.eye = Ease(c.eye, t.eye, kl);
        c.tongue = Ease(c.tongue, t.tongue, k);
        float kp = 1f - Mathf.Exp(-6f * dt);
        for (int i = 0; i < 4; i++)
        {
            c.leg[i] = Ease(c.leg[i], t.leg[i], kl);
            plantAmt[i] = Ease(plantAmt[i], t.plant[i] ? 1f : 0f, kp);
        }
    }

    // Rests the visual root so its lowest foot / belly point touches the ground exactly.
    void Settle()
    {
        float low = float.MaxValue;
        for (int i = 0; i < rig.feet.Length; i++)
            if (rig.feet[i] != null) low = Mathf.Min(low, rig.feet[i].position.y);
        for (int i = 0; i < rig.contacts.Length; i++)
            if (rig.contacts[i] != null) low = Mathf.Min(low, rig.contacts[i].position.y);
        if (low == float.MaxValue) return;
        rig.visual.position += Vector3.up * (transform.position.y - low);
    }

    void ApplyPose()
    {
        Pose c = cur;
        rig.visual.localPosition = Vector3.zero;
        rig.visual.localRotation = Quaternion.Euler(c.pitch, 0f, c.roll);

        for (int i = 0; i < 4; i++)
        {
            rig.legs[i].localRotation = Quaternion.Euler(c.leg[i] - c.pitch, 0f, 0f);
            rig.legs[i].localScale = new Vector3(1f, Mathf.Lerp(1f, 0.35f, plantAmt[i]), 1f);
        }
        if (rig.neck != null) rig.neck.localRotation = Quaternion.Euler(0f, c.nyaw, 0f) * rig.neckHome * Quaternion.Euler(c.neck, 0f, 0f);
        rig.head.localRotation = rig.headHome * Quaternion.Euler(c.head, c.hyaw, c.hroll);
        rig.tail.localRotation = Quaternion.Euler(0f, c.tyaw, 0f) * rig.tailHome * Quaternion.Euler(c.tpitch, 0f, 0f);
        for (int i = 0; i < rig.tailSegments.Length; i++)
        {
            float wave = Mathf.Sin(Time.time * 2.6f - i * 0.9f) * 6f;
            rig.tailSegments[i].localRotation = rig.tailSegmentHomeRotations[i] * Quaternion.Euler(c.tcurl + wave, 0f, 0f);
        }
        rig.ears[0].localRotation = rig.earHome[0] * Quaternion.Euler(c.earL, 0f, 0f);
        rig.ears[1].localRotation = rig.earHome[1] * Quaternion.Euler(c.earR, 0f, 0f);
        for (int i = 0; i < 2; i++)
            if (rig.eyes[i] != null) rig.eyes[i].localScale = new Vector3(1f, Mathf.Max(0.08f, c.eye), 1f);
        if (rig.tuft != null) rig.tuft.gameObject.SetActive(target.tuft);
        if (rig.tongue != null) rig.tongue.localScale = Vector3.one * Mathf.Max(0.001f, c.tongue);

        Settle();
        for (int pass = 0; pass < 2; pass++)
        {
            bool any = false;
            for (int i = 0; i < 4; i++)
            {
                if (plantAmt[i] < 0.001f) continue;
                any = true;
                Vector3 down = rig.legs[i].TransformDirection(Vector3.down);
                float cs = Mathf.Max(0.35f, -down.y);
                float hipHeight = rig.legs[i].position.y - transform.position.y;
                float want = Mathf.Clamp(hipHeight / (rig.legLength[i] * modelScale * cs), 0.35f, 2.2f);
                rig.legs[i].localScale = new Vector3(1f, Mathf.Lerp(1f, want, plantAmt[i]), 1f);
            }
            if (!any) break;
            Settle();
        }
        rig.visual.localPosition += Vector3.up * c.hop;
    }

    bool CanMoveInDirection(Vector3 direction, float distance)
    {
        if (direction.sqrMagnitude < 0.001f) return false;
        Vector3 p = transform.position;
        Vector3 next = p + direction.normalized * distance;
        if (director.Ctx.zoneMap == null || director.Ctx.zoneMap.ZoneAt(next.x, next.z) != homeZone) return false;
        if (homeZone == ZoneType.Forest && director.Ctx.NormalAt(next.x, next.z).y < 0.88f) return false;
        if (owner != null && owner.IsCrowded(this, next, collisionRadius * 1.9f)) return false;
        Vector3 origin = p + Vector3.up * Mathf.Max(0.35f, animalHeight * 0.45f);
        RaycastHit hit;
        if (Physics.SphereCast(origin, collisionRadius * 0.7f, direction.normalized, out hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && director.Player != null && hit.collider.transform.IsChildOf(director.Player)) return true;
            if (hit.collider != null && Time.time < carEvadeUntil && hit.collider.GetComponentInParent<AgentBase>() != null) return true; // don't let the car we're escaping block the escape
            if (hit.collider != null && hit.collider.GetComponentInParent<AnimalAgent>() != null) return false;
            return false;
        }
        return true;
    }

    bool TryGround(float x, float z, out float y)
    {
        y = director.Ctx.HeightAt(x, z);
        Vector3 origin = new Vector3(x, y + 25f, z);
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            Collider c = groundHits[i].collider;
            if (c == null || c.transform.IsChildOf(transform)) continue;
            if (c.GetComponentInParent<AnimalAgent>() != null || c.GetComponentInParent<AgentBase>() != null) continue;
            if (director.Player != null && c.transform.IsChildOf(director.Player)) continue;
            if (groundHits[i].normal.y < 0.55f) continue;
            if (Mathf.Abs(groundHits[i].point.y - y) > 1.25f) continue;
            if (groundHits[i].distance < best)
            {
                best = groundHits[i].distance;
                y = groundHits[i].point.y;
            }
        }
        return director.Ctx.zoneMap != null && director.Ctx.zoneMap.ZoneAt(x, z) == homeZone;
    }

    float RandomRange(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    float RandomValue() { return (float)rng.NextDouble(); }
}
