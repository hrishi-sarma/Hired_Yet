using UnityEngine;

/// <summary>
/// Personality, trust, reactions to the player, homes and daily routines for AnimalAgent.
/// (Partial class: the movement / pose / social / sleep code stays in AnimalAgent.cs.)
/// </summary>
public sealed partial class AnimalAgent
{
    // ====================================================================== personality
    float boldness = 0.5f, curiosity = 0.5f, energy = 0.5f;
    float personalitySpeed = 1f;
    float localTrust;
    HomeSpot home;

    public float Boldness { get { return boldness; } }
    public float Curiosity { get { return curiosity; } }
    public float Energy { get { return energy; } }
    public HomeSpot Home { get { return home; } }

    /// <summary>0..1. How much this animal trusts the player. Stored on the home so it survives despawn/respawn.</summary>
    public float Trust
    {
        get { return home != null ? home.trust : localTrust; }
        set
        {
            value = Mathf.Clamp01(value);
            if (home != null) home.trust = value; else localTrust = value;
        }
    }

    static float Unit(int seed, int salt)
    {
        unchecked
        {
            uint h = (uint)(seed * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    void InitLife(HomeSpot homeSpot, int seed)
    {
        home = homeSpot;
        if (home != null) home.occupant = this;
        if (settings.enablePersonality)
        {
            boldness = 0.15f + 0.7f * Unit(seed, 11);
            curiosity = 0.15f + 0.7f * Unit(seed, 23);
            energy = 0.15f + 0.7f * Unit(seed, 37);
            if (Species == AnimalSpecies.Dog) { boldness += 0.15f; energy += 0.2f; }
            else if (Species == AnimalSpecies.Cat) curiosity += 0.1f;
            else if (Species == AnimalSpecies.Rabbit) boldness -= 0.1f;
            boldness = Mathf.Clamp01(boldness); curiosity = Mathf.Clamp01(curiosity); energy = Mathf.Clamp01(energy);
            personalitySpeed = Mathf.Lerp(0.88f, 1.14f, energy);
        }
        localTrust = Species == AnimalSpecies.Dog ? 0.12f : (Species == AnimalSpecies.Cat ? 0.04f : 0.02f);

        if (home != null && settings.enableRoutines && AnimalSleepClock.IsNight(settings))
        {   // spawned during the night: it is already at its bed / in its burrow
            if (Species == AnimalSpecies.Rabbit && settings.rabbitsUseBurrows)
                EnterBurrow(Time.time + 99999f, true);
            else
            {
                errand = Errand.GoSleep; errandTarget = SleepSpot(); errandArrived = true; errandStart = Time.time;
            }
        }
    }

    void ReleaseHome()
    {
        if (home != null && home.occupant == this) home.occupant = null;
    }

    // ---------------------------------------------------------------- player tracking (shared by all animals, once per frame)
    static float sPlayerSpeed;
    static Vector3 sPlayerVel, sLastPlayerPos;
    static int sSpeedFrame = -1;
    static bool sHaveLast;

    static void TrackPlayer(Vector3 p)
    {
        if (sSpeedFrame == Time.frameCount) return;
        sSpeedFrame = Time.frameCount;
        float dt = Time.deltaTime;
        if (sHaveLast && dt > 0.0001f)
        {
            Vector3 d = p - sLastPlayerPos;
            d.y = 0f;
            float v = d.magnitude / dt;
            if (v > 40f) { sPlayerSpeed = 0f; sPlayerVel = Vector3.zero; }   // teleport
            else
            {
                float k = 1f - Mathf.Exp(-8f * dt);
                sPlayerSpeed = Mathf.Lerp(sPlayerSpeed, v, k);
                sPlayerVel = Vector3.Lerp(sPlayerVel, d / dt, k);
            }
        }
        sLastPlayerPos = p;
        sHaveLast = true;
    }

    /// <summary>Used by birds: true while the player is sprinting (so flocks scatter from farther away).</summary>
    public static bool PlayerIsSprinting(AnimalAISettings s) { return s != null && s.enableReactions && sPlayerSpeed > s.playerSprintSpeed; }

    bool PlayerSprinting { get { return settings.enableReactions && sPlayerSpeed > settings.playerSprintSpeed; } }
    bool PlayerCalm { get { return sPlayerSpeed < settings.playerCalmSpeed; } }

    /// <summary>Distance at which this animal bolts, after personality and trust.</summary>
    float EffFlee()
    {
        float baseD = Species == AnimalSpecies.Deer ? settings.deerFleeDistance : settings.rabbitFleeDistance;
        if (!settings.enableReactions) return baseD;
        float d = baseD * Mathf.Lerp(1.15f, 0.35f, Trust) * Mathf.Lerp(1.2f, 0.8f, boldness);
        if (FPAnimation.FPEvents.Sitting) d *= Mathf.Lerp(1f, 0.65f, FPAnimation.FPEvents.SitBlend);   // a still, seated player is less threatening
        return Mathf.Max(3f, d);
    }

    void UpdateLifeShared(float dt, float playerDistance)
    {
        TrackPlayer(director.Player.position);
        if (!settings.enableReactions) return;
        float range = Species == AnimalSpecies.Deer ? 16f : (Species == AnimalSpecies.Rabbit ? 12f : (Species == AnimalSpecies.Cat ? 8f : 14f));
        if (playerDistance > range || motion == Motion.Flee) return;
        float gain = dt / settings.trustSecondsToFull * (0.55f + curiosity * 0.9f);
        if (IsDomestic)
        {
            if (sPlayerSpeed < 2.5f) Trust += gain * 0.6f;
        }
        else if (PlayerCalm && playerDistance > EffFlee() * 0.9f) Trust += gain;
    }

    // ====================================================================== reactions to the player
    bool alertPose;
    float alertUntil, alertCooldownUntil, curiousUntil, curiousCooldownUntil;

    // Wild animals: alert freeze before fleeing, curious approach once they trust the player. Cats: curious approach / startle.
    bool UpdateReactions(float playerDistance, Vector3 fromPlayer, float dt)
    {
        if (!settings.enableReactions) return false;
        if (motion == Motion.Pat || Time.time < patUntil || motion == Motion.Flee) return false;
        Vector3 toPlayer = fromPlayer.sqrMagnitude > 0.01f ? -fromPlayer.normalized : transform.forward;
        if (Species == AnimalSpecies.Cat) return CatReactions(playerDistance, fromPlayer, toPlayer, dt);
        if (IsDomestic) return false;

        float eff = EffFlee();
        float alertDist = eff * settings.alertDistanceMultiplier;
        float now = Time.time;

        if (alertUntil > now)
        {
            if (playerDistance >= eff && playerDistance < alertDist * 1.15f)
            {
                alertPose = true; motion = Motion.Idle; FaceDirection(toPlayer, dt); return true;
            }
            alertUntil = 0f;
        }
        bool calmAndTrusted = PlayerCalm && Trust > 0.35f;
        if (playerDistance < alertDist && playerDistance >= eff && now >= alertCooldownUntil && !calmAndTrusted)
        {
            alertUntil = now + RandomRange(1.6f, 3.0f) * (1.25f - 0.5f * boldness);
            alertCooldownUntil = alertUntil + RandomRange(5f, 9f);
            if (Species == AnimalSpecies.Rabbit && RandomValue() < 0.5f) PlayReaction();   // thump
            alertPose = true; motion = Motion.Idle; FaceDirection(toPlayer, dt);
            return true;
        }

        // curious approach: the more it trusts you and the more curious it is, the closer it comes while you stand still
        if (wantSleep || !PlayerCalm || Trust * (0.5f + curiosity) < 0.2f) return false;
        float stop = eff + 0.9f;
        if (playerDistance > stop + 0.3f && playerDistance < 18f && now >= curiousCooldownUntil)
        {
            if (curiousUntil <= 0f) curiousUntil = now + RandomRange(6f, 12f);
            if (now > curiousUntil) { curiousUntil = 0f; curiousCooldownUntil = now + RandomRange(12f, 25f); return false; }
            Vector3 side = Vector3.Cross(Vector3.up, toPlayer) * Mathf.Sin(now * 0.8f + seedPhase) * 0.25f;
            moveDirection = (toPlayer + side).normalized;
            motion = Motion.Walk;
            return true;
        }
        if (playerDistance <= stop + 0.3f && playerDistance > eff)
        {   // close enough: watch the player (stare ~60% of the time, otherwise carry on grazing)
            if (Mathf.Repeat(now * 0.07f + seedPhase, 1f) < 0.6f)
            {
                alertPose = true; motion = Motion.Idle; FaceDirection(toPlayer, dt);
                return true;
            }
        }
        return false;
    }

    float catApproachUntil, catApproachCooldown;

    bool CatReactions(float playerDistance, Vector3 fromPlayer, Vector3 toPlayer, float dt)
    {
        float now = Time.time;
        if (wantSleep || errand == Errand.GoSleep) return false;
        // startled by a sprinting player nearby, unless it knows you
        if (PlayerSprinting && playerDistance < 5f && Trust < 0.5f && now >= catApproachCooldown)
        {
            Vector3 away = fromPlayer.sqrMagnitude > 0.01f ? fromPlayer.normalized : transform.forward;
            moveDirection = (away + Vector3.Cross(Vector3.up, away) * RandomRange(-0.5f, 0.5f)).normalized;
            catIdle = false; motion = Motion.Run; motionTimer = RandomRange(1.8f, 3.0f);
            catApproachCooldown = now + 6f;
            CancelErrand();
            return false;
        }
        float stop = Mathf.Lerp(3.6f, 1.7f, Trust);
        if (Trust * (0.4f + curiosity) < 0.15f || !PlayerCalm || playerDistance > 10f || now < catApproachCooldown) return false;
        if (catApproachUntil <= 0f) catApproachUntil = now + RandomRange(10f, 18f);
        if (now > catApproachUntil) { catApproachUntil = 0f; catApproachCooldown = now + RandomRange(30f, 60f); return false; }
        if (playerDistance > stop)
        {
            moveDirection = toPlayer; motion = Motion.Walk; catIdle = false;
            CancelErrand();
            return true;
        }
        // arrived: sit and look up at the player (ready to be petted)
        motion = Motion.Idle; catIdle = true;
        if (catIdlePose != 1 && catIdlePose != 0) { catIdlePose = 1; catPoseTimer = RandomRange(5f, 9f); }
        FaceDirection(toPlayer, dt);
        return true;
    }

    // Dog escort: a dog that knows you trots along beside you for a while when you walk past.
    bool escorting;
    float escortUntil, nextEscortCheck;

    bool UpdateEscort(float playerDistance, Vector3 fromPlayer, float dt)
    {
        if (!settings.enableReactions || Time.time < patUntil || motion == Motion.Pat || wantSleep) { escorting = false; return false; }
        float now = Time.time;
        Vector3 toPlayer = fromPlayer.sqrMagnitude > 0.01f ? -fromPlayer.normalized : transform.forward;
        if (!escorting)
        {
            if (now < nextEscortCheck) return false;
            nextEscortCheck = now + RandomRange(2f, 5f);
            if (playerDistance < 24f && playerDistance > 3f && sPlayerSpeed > 1.2f && Trust >= 0.15f
                && RandomValue() < (0.25f + 0.5f * energy) * (0.4f + Trust))
            {
                escorting = true;
                escortUntil = now + RandomRange(20f, 38f) * (0.6f + Trust * 0.6f);
                CancelErrand();
                PlayReaction();
            }
            else return false;
        }
        if (now > escortUntil || playerDistance > 45f || (home != null && HomeDistance() > home.radius * 2f))
        {
            escorting = false;
            nextEscortCheck = now + RandomRange(35f, 70f);
            motion = Motion.Idle; motionTimer = 1f;
            return false;
        }
        SteerEscort(playerDistance, toPlayer, dt);
        return true;
    }

    void SteerEscort(float playerDistance, Vector3 toPlayer, float dt)
    {
        Vector3 pv = sPlayerVel; pv.y = 0f;
        if (playerDistance > 7f) { moveDirection = toPlayer; motion = Motion.Run; return; }
        if (sPlayerSpeed > 1f)
        {   // trot alongside, a little ahead and to one side
            Vector3 dir = pv.normalized;
            float side = (GetInstanceID() & 1) == 0 ? 1f : -1f;
            Vector3 goal = director.Player.position + dir * 2.4f + Vector3.Cross(Vector3.up, dir) * side * 2.0f;
            Vector3 to = goal - transform.position; to.y = 0f;
            if (to.magnitude > 0.9f) { moveDirection = to.normalized; motion = to.magnitude > 3.5f ? Motion.Run : Motion.Walk; }
            else { moveDirection = dir; motion = Motion.Walk; }
        }
        else
        {   // the player stopped: sit and look up
            motion = Motion.Idle;
            FaceDirection(toPlayer, dt);
        }
    }

    // ====================================================================== homes and daily routines
    enum Errand { None, GoSleep, Sunbathe, Porch, Rest, Drink, Hide }
    Errand errand;
    Vector3 errandTarget, errandFace;
    float errandStart, errandDwellUntil, nextRoutineCheck;
    bool errandArrived, errandUrgent, resting, sleepAnywhere;

    float HomeDistance()
    {
        if (home == null) return 0f;
        Vector3 d = transform.position - home.center; d.y = 0f;
        return d.magnitude;
    }

    Vector3 SleepSpot()
    {
        if (home == null) return transform.position;
        return IsDomestic ? home.SpotOrCenter(HomeSpot.Bed) : home.center;
    }

    Vector3 BurrowMouth()
    {
        return home.center + home.front * 0.35f;
    }

    float PersonalSpace()
    {
        return IsDomestic ? 1.8f : EffFlee() * 1.05f;
    }

    bool SleepSpotReady()
    {
        if (home == null || !settings.enableRoutines || sleepAnywhere) return true;
        if (Species == AnimalSpecies.Rabbit && settings.rabbitsUseBurrows) return false;   // rabbits sleep inside the burrow
        return errand == Errand.GoSleep && errandArrived;
    }

    void StartErrand(Errand e, Vector3 target, bool urgent)
    {
        errand = e; errandTarget = target; errandUrgent = urgent;
        errandArrived = false; errandStart = Time.time;
        Vector3 f = Vector3.zero;
        if (e == Errand.Porch || e == Errand.GoSleep) f = home != null ? home.front : transform.forward;
        else if (e == Errand.Drink) f = AnimalHomes.PondCenter - target;
        else f = new Vector3(Mathf.Cos(Time.time), 0f, Mathf.Sin(Time.time));
        f.y = 0f;
        errandFace = f.sqrMagnitude > 0.01f ? f.normalized : transform.forward;
    }

    void CancelErrand()
    {
        if (errand == Errand.None) return;
        errand = Errand.None; errandUrgent = false; errandArrived = false; resting = false;
    }

    void EndErrand()
    {
        CancelErrand();
        if (motion != Motion.Flee && motion != Motion.Sleep) { motion = Motion.Idle; motionTimer = RandomRange(0.6f, 1.8f); }
        if (Species == AnimalSpecies.Cat) { catIdle = true; PickCatIdlePose(); }
    }

    float DwellFor(Errand e)
    {
        switch (e)
        {
            case Errand.Sunbathe: return RandomRange(40f, 100f);
            case Errand.Porch: return AnimalSleepClock.Phase(settings) == DayPhase.Dusk ? 150f : RandomRange(12f, 25f);
            case Errand.Rest: return AnimalSleepClock.Phase(settings) == DayPhase.Dusk ? 150f : RandomRange(30f, 80f);
            case Errand.Drink: return RandomRange(14f, 28f);
            default: return 5f;
        }
    }

    void OnErrandArrive()
    {
        float dwell = DwellFor(errand);
        errandDwellUntil = Time.time + dwell;
        switch (errand)
        {
            case Errand.Hide:
                EnterBurrow(errandUrgent ? Time.time + RandomRange(15f, 35f) : Time.time + RandomRange(20f, 50f), wantSleep);
                return;
            case Errand.Sunbathe:
                if (Species == AnimalSpecies.Cat) { catIdle = true; catIdlePose = RandomValue() < 0.5f ? 0 : 4; catPoseTimer = dwell + 30f; }
                break;
            case Errand.Porch:
                if (Species == AnimalSpecies.Cat)
                {
                    catIdle = true;
                    bool dawn = AnimalSleepClock.Phase(settings) == DayPhase.Dawn;
                    catIdlePose = dawn ? 3 : 1;           // morning stretch / evening sit
                    catPoseTimer = dawn ? dwell : dwell + 30f;
                }
                else StartRest();
                break;
            case Errand.Rest:
                StartRest();
                break;
            case Errand.GoSleep:
                if (Species == AnimalSpecies.Cat) catIdle = true;
                break;
        }
    }

    void StartRest()
    {
        resting = true;
        sleepVariant = RandomValue() < 0.5f ? 0 : 2;
        sleepSide = RandomValue() < 0.5f ? -1f : 1f;
    }

    bool UpdateRoutine(float dt, float playerDistance, Vector3 fromPlayer)
    {
        if (home == null || !settings.enableRoutines || hidden) return false;
        if (!wantSleep) sleepAnywhere = false;
        bool urgentHide = errand == Errand.Hide && errandUrgent;
        if (!urgentHide && (motion == Motion.Flee || Time.time < patUntil || escorting))
        {
            CancelErrand();
            return false;
        }
        bool nearPlayer = playerDistance < PersonalSpace();
        if (!urgentHide && errand != Errand.None && nearPlayer && !(IsDomestic && errand == Errand.GoSleep))
        {
            CancelErrand();
            return false;
        }

        float now = Time.time;
        if (errand == Errand.None)
        {
            if (nearPlayer) return false;
            if (wantSleep && !sleeping)
            {
                if (Species == AnimalSpecies.Rabbit && settings.rabbitsUseBurrows) StartErrand(Errand.Hide, BurrowMouth(), false);
                else StartErrand(Errand.GoSleep, SleepSpot(), false);
                if (errand == Errand.Hide) errandUrgent = false;
            }
            else if (now >= nextRoutineCheck && now >= wakeUntil)
            {
                nextRoutineCheck = now + RandomRange(7f, 16f);
                DecideRoutine(playerDistance);
            }
            if (errand == Errand.None) return false;
        }

        if (!errandArrived)
        {
            Vector3 to = errandTarget - transform.position; to.y = 0f;
            float d = to.magnitude;
            float arrive = errand == Errand.Hide ? 0.55f : 0.9f;
            if (d <= arrive)
            {
                errandArrived = true;
                OnErrandArrive();
                if (hidden) return true;
            }
            else
            {
                if (now - errandStart > 80f)
                {   // stuck: give up (sleep right here if it is bedtime)
                    if (errand == Errand.GoSleep || (errand == Errand.Hide && wantSleep)) sleepAnywhere = true;
                    CancelErrand();
                    return false;
                }
                moveDirection = to / d;
                if (errandUrgent) motion = Motion.Flee;
                else
                {
                    bool run = Species == AnimalSpecies.Dog && energy > 0.35f && d > 8f && !wantSleep;
                    motion = run ? Motion.Run : Motion.Walk;
                }
                if (Species == AnimalSpecies.Cat) catIdle = false;
                return true;
            }
        }

        // dwelling at the spot
        motion = Motion.Idle;
        FaceDirection(errandFace, dt);
        if (errand != Errand.GoSleep && now >= errandDwellUntil) EndErrand();
        return true;
    }

    void DecideRoutine(float playerDistance)
    {
        DayPhase ph = AnimalSleepClock.Phase(settings);
        float r = RandomValue();
        switch (Species)
        {
            case AnimalSpecies.Cat:
                if (ph == DayPhase.Dawn && r < 0.55f) TryErrand(Errand.Porch, home.SpotOrCenter(HomeSpot.Porch));
                else if (ph == DayPhase.Day && r < 0.40f) TryErrand(Errand.Sunbathe, home.SpotOrCenter(RandomValue() < 0.5f ? HomeSpot.SunA : HomeSpot.SunB));
                else if (ph == DayPhase.Dusk && r < 0.70f) TryErrand(Errand.Porch, home.SpotOrCenter(HomeSpot.Porch));
                break;
            case AnimalSpecies.Dog:
                if (ph == DayPhase.Day && r < 0.22f) TryErrand(Errand.Rest, home.SpotOrCenter(HomeSpot.Porch));
                else if (ph == DayPhase.Dusk && r < 0.70f) TryErrand(Errand.Rest, home.SpotOrCenter(HomeSpot.Porch));
                break;
            case AnimalSpecies.Rabbit:
                if (settings.rabbitsUseBurrows && ph == DayPhase.Day && r < 0.15f && playerDistance > 8f)
                    TryErrand(Errand.Hide, BurrowMouth());
                break;
            default: // deer
            {
                if ((ph == DayPhase.Dawn || ph == DayPhase.Dusk) && r < 0.45f)
                {
                    Vector3 spot;
                    if (AnimalHomes.NearestPondSpot(transform.position, 110f, rng, out spot)) TryErrand(Errand.Drink, spot);
                }
                else if (ph == DayPhase.Day && r < 0.35f) TryErrand(Errand.Rest, home.center);
                break;
            }
        }
    }

    void TryErrand(Errand e, Vector3 target)
    {
        Vector3 d = target - transform.position; d.y = 0f;
        if (d.sqrMagnitude > 110f * 110f) return;
        StartErrand(e, target, false);
    }

    // Wander leash: animals with a home drift back toward it instead of walking away forever.
    Vector3 LeashDirection(Vector3 dir)
    {
        if (home == null || !settings.useHomes) return dir;
        float R = Mathf.Max(5f, home.radius);
        Vector3 toHome = home.center - transform.position; toHome.y = 0f;
        float d = toHome.magnitude;
        if (d < R * 0.6f || d < 0.01f) return dir;
        float t = Mathf.Clamp01((d - R * 0.6f) / (R * 0.4f));
        Vector3 res = Vector3.Slerp(dir, toHome / d, t);
        return res.sqrMagnitude > 0.001f ? res.normalized : dir;
    }

    // ---------------------------------------------------------------- burrows (rabbits hide/sleep inside)
    bool hidden;
    float hideUntil, hideStart;
    bool hideForNight;

    void SetVisible(bool v)
    {
        if (rig != null && rig.visual != null) rig.visual.gameObject.SetActive(v);
        if (bodyCollider != null) bodyCollider.enabled = v;
    }

    void EnterBurrow(float until, bool forNight)
    {
        hidden = true; hideStart = Time.time; hideUntil = until; hideForNight = forNight;
        errand = Errand.None; errandUrgent = false; resting = false;
        motion = Motion.Idle; sleeping = false;
        if (socialRole != SocialRole.None) EndSocial(true);
        SetVisible(false);
        if (home != null) transform.position = home.center + home.front * 0.2f;
    }

    bool UpdateHidden(float playerDistance)
    {
        if (!hidden) return false;
        float now = Time.time;
        if (hideForNight && !AnimalSleepClock.IsNight(settings))
        {   // morning: come out at a staggered time
            hideForNight = false;
            hideUntil = now + RandomRange(0f, 25f);
        }
        if (!hideForNight && WeatherController.Raining) hideUntil = Mathf.Max(hideUntil, now + 2f);   // stay in the burrow while it rains
        if (!hideForNight && now >= hideUntil && playerDistance > 5f) { Emerge(false); return false; }
        if (playerDistance < 2.2f && now - hideStart > 2f) { Emerge(true); return false; }
        return true;
    }

    void Emerge(bool startled)
    {
        hidden = false;
        SetVisible(true);
        Vector3 front = home != null ? home.front : transform.forward;
        Vector3 p = home != null ? home.center + front * 0.9f : transform.position;
        float y;
        if (TryGround(p.x, p.z, out y)) p.y = y;
        transform.position = p;
        transform.rotation = Quaternion.LookRotation(front.sqrMagnitude > 0.01f ? front : Vector3.forward, Vector3.up);
        hasLastPos = false;
        if (startled)
        {
            moveDirection = (front + Vector3.Cross(Vector3.up, front) * RandomRange(-0.6f, 0.6f)).normalized;
            motion = Motion.Flee; motionTimer = RandomRange(2.5f, 4f); fleeCooldown = 0.8f;
            wakeUntil = Time.time + RandomRange(20f, 40f);
            PlayReaction();
        }
        else { motion = Motion.Idle; motionTimer = RandomRange(1f, 3f); }
    }

    /// <summary>A startled rabbit runs for its burrow if that is not toward the player.</summary>
    bool TryBurrowDash(Vector3 away)
    {
        if (Species != AnimalSpecies.Rabbit || home == null || !settings.rabbitsUseBurrows || !settings.enableRoutines) return false;
        Vector3 to = BurrowMouth() - transform.position; to.y = 0f;
        float d = to.magnitude;
        if (d > 20f) return false;
        if (d < 0.6f) { EnterBurrow(Time.time + RandomRange(15f, 35f), false); return true; }
        if (Vector3.Dot(to / d, away) < -0.2f) return false;
        StartErrand(Errand.Hide, BurrowMouth(), true);
        motion = Motion.Flee;
        return true;
    }
}
