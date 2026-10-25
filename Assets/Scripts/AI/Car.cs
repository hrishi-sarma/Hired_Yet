using System.Collections.Generic;
using UnityEngine;

public class Car : AgentBase
{
    float bodyLength = 4.2f;
    float HalfLength { get { return bodyLength * 0.5f; } }

    public void SetBodyLength(float value)
    {
        bodyLength = Mathf.Clamp(value, 1f, 20f);
        if (lights != null) lights.Configure(bodyLength);
    }
    const float BaseAccel = 2.5f;
    const float ComfortBrake = 4f;
    const float MinGap = 3f;
    const float Headway = 1.5f;
    const float SignalLook = 75f;
    const float LeftLimit = 1f;
    const float RightLimit = -4.6f;

    static readonly List<IAgent> agentBuf = new List<IAgent>(16);
    static readonly List<IAgent> passBuf = new List<IAgent>(16);

    RoadPath path;
    float s;
    float speed;
    RoadPath planned;
    bool reserved;
    bool prepared;
    IntersectionController holdCtl;
    float nextSense;
    float nextRequest;
    float obstacleGap = 999f;
    float playerGap = 999f;
    float yaw;
    float startS;
    float startSpeed;
    RoadPath startPath;

    DriverProfile profile;
    int clipIndex;
    int honksDone;
    int chargesDone;
    float blockedTime;
    float stillTime;
    float nextHonkAt;
    float resumeAt;
    float honkUntil;
    float snarlUntil;
    float chargeUntil;
    float boostUntil;
    float overtakeStart;
    float nextOvertakeTry;
    float lat;
    float latGoal;
    float latVel;
    bool charging;
    bool overtaking;
    bool blockerIsPlayer;
    float blockerRadius = 0.45f;
    Vector2 blockerPos;
    Vector2 lastBlockerPos;
    Vector2 passTarget;

    // --- traffic system state ---
    DriverMind mind;
    CarLights lights;
    int lastHorn = -1;
    readonly List<RoadPath> route = new List<RoadPath>(24);
    int routeIdx;
    LaneNode dest;
    ZoneType home = ZoneType.Suburb;
    bool returning;
    bool far;
    float farAcc;
    float nextLod;
    float nextMood;
    SignalState sigState = SignalState.Green;
    bool sigPermit;
    bool sigDecided;
    bool sigRun;
    float runningRedUntil;
    Car leaderCar;
    Car prevLeader;
    Car overtakeLeader;
    float leaderGap = 999f;
    float leaderSpeed;
    float slowLeaderTime;
    float stuckLeaderTime;
    float nextStuckHonk;
    float oncomingGap = 999f;
    float lastNearMiss;
    float abortUntil;
    float impSeed;
    float sleepUntil;
    float nextSleep;
    bool headForgot;
    bool brakeOn;
    bool notedBlocked;
    Vector2 heading = Vector2.up;

    AudioSource audioSrc;
    readonly CarEngineVoice engine = new CarEngineVoice();
    float honkStart;
    float honkEnd;
    float honkVol;
    bool honkPlaying;

    public static event System.Action<Car> Honked;

    public CarState State { get; private set; }

    // debug / tuning read-outs
    public DriverMind Mind { get { return mind; } }
    public bool Far { get { return far; } }
    public SignalState Signal { get { return sigState; } }
    public Vector2 Heading { get { return heading; } }
    public Vector2 DestPos { get { return dest != null ? dest.pos : Pos2D; } }
    public bool HasDest { get { return dest != null; } }
    public ZoneType DestZone { get { return dest != null ? dest.zone : ZoneType.Open; } }
    public bool Returning { get { return returning; } }
    public int RouteLeft { get { return Mathf.Max(0, route.Count - routeIdx); } }
    public RoadPath RouteAt(int i) { int k = routeIdx + i; return k >= 0 && k < route.Count ? route[k] : null; }
    float OtherHalf { get { return HalfLength; } }

    protected override bool KeepAlive(float sq)
    {
        NpcDirector d = NpcDirector.Instance;
        if (d == null) return false;
        TrafficTuning tt = d.settings.car.traffic;
        return SecondsSinceSeen < tt.followSeenSeconds && sq < tt.followRadius * tt.followRadius;
    }

    public string Personality
    {
        get { return mind != null ? mind.Label() : (profile != null ? profile.name : ""); }
    }

    public float PathS
    {
        get { return s; }
    }

    public float Speed
    {
        get { return speed; }
    }

    public RoadPath Path
    {
        get { return path; }
    }

    protected override bool DespawnBlocked
    {
        get { return path != null && path.kind == PathKind.Connector; }
    }

    public void Prepare(RoadPath lane, float startPos, float startVel)
    {
        startPath = lane;
        startS = startPos;
        startSpeed = startVel;
        prepared = true;
    }

    protected override void OnSpawn()
    {
        NpcDirector d = NpcDirector.Instance;
        planned = null;
        reserved = false;
        holdCtl = null;
        obstacleGap = 999f;
        playerGap = 999f;
        profile = DriverProfile.Pick(d.settings.car.drivers, d.Rng);
        clipIndex = d.Rng.Next(100000);
        honksDone = 0;
        chargesDone = 0;
        blockedTime = 0f;
        stillTime = 0f;
        nextHonkAt = profile.honkFirstDelay;
        resumeAt = 0f;
        honkUntil = 0f;
        snarlUntil = 0f;
        chargeUntil = 0f;
        boostUntil = 0f;
        nextOvertakeTry = 0f;
        lat = 0f;
        latGoal = 0f;
        latVel = 0f;
        charging = false;
        overtaking = false;
        honkPlaying = false;
        nextSense = 0f;
        nextRequest = 0f;
        State = CarState.Drive;
        route.Clear();
        routeIdx = 0;
        dest = null;
        returning = false;
        far = false;
        farAcc = 0f;
        nextLod = 0f;
        nextMood = 0f;
        sigPermit = false;
        sigDecided = false;
        sigRun = false;
        runningRedUntil = 0f;
        leaderCar = null;
        prevLeader = null;
        overtakeLeader = null;
        slowLeaderTime = 0f;
        stuckLeaderTime = 0f;
        abortUntil = 0f;
        oncomingGap = 999f;
        notedBlocked = false;
        brakeOn = false;
        impSeed = Roll() * 6.28f;
        TrafficTuning tt = d.settings.car.traffic;
        Impairment imp = Impairment.None;
        if (TrafficClock.Late)
        {
            float r = Roll();
            if (r < tt.drunkChanceLate) imp = Impairment.Drunk;
            else if (r < tt.drunkChanceLate + tt.sleepyChanceLate) imp = Impairment.Sleepy;
        }
        if (mind == null) mind = new DriverMind();
        mind.Init(profile, imp);
        sleepUntil = 0f;
        nextSleep = Time.time + 20f + Roll() * 40f;
        headForgot = imp != Impairment.None && Roll() < 0.15f;
        if (lights == null)
        {
            lights = GetComponent<CarLights>();
            if (lights == null) lights = gameObject.AddComponent<CarLights>();
            lights.Configure(bodyLength);
        }
        path = null;
        if (prepared)
        {
            path = startPath;
            s = startS;
            speed = startSpeed;
        }
        else if (d.LaneGraph != null)
        {
            Vector3 p = transform.position;
            float ss;
            float dist;
            RoadPath lane = d.LaneGraph.Nearest(new Vector2(p.x, p.z), out ss, out dist);
            if (lane != null && dist < 20f)
            {
                path = lane;
                s = Mathf.Clamp(ss, 0.5f, Mathf.Max(0.5f, lane.length - 6f));
                speed = 0f;
            }
        }
        prepared = false;
        if (path == null) return;
        path.cars.Add(this);
        home = path.zone == ZoneType.Highway ? ZoneType.Suburb : path.zone;
        Pose(true, 0f);
    }

    public override void OnDespawn()
    {
        if (path != null) path.cars.Remove(this);
        if (reserved && path != null)
        {
            RoadPath c = path.kind == PathKind.Lane ? planned : path;
            if (c != null) c.node.controller.Release(this);
        }
        if (holdCtl != null) holdCtl.Release(this);
        if (audioSrc != null) audioSrc.Stop();
        engine.Stop();
        HornBank.End(this);
        if (lights != null) lights.Set(false, false, 0, false);
        route.Clear();
        dest = null;
        leaderCar = null;
        prevLeader = null;
        overtakeLeader = null;
        holdCtl = null;
        reserved = false;
        planned = null;
        path = null;
        prepared = false;
        honkPlaying = false;
        overtaking = false;
        charging = false;
    }

    float Roll()
    {
        return (float)NpcDirector.Instance.Rng.NextDouble();
    }

    protected override void OnAgentUpdate(float dt)
    {
        if (path == null)
        {
            Despawn();
            return;
        }
        NpcDirector d = NpcDirector.Instance;
        TrafficTuning tt = d.settings.car.traffic;
        if (Time.time >= nextLod) UpdateLod(d, tt);
        if (far)
        {
            farAcc += dt;
            if (farAcc < tt.farTick) { FarGlide(); return; }
            dt = Mathf.Min(farAcc, 0.5f);
            farAcc = 0f;
        }
        else if (Time.time >= nextSense) Sense(d);
        float a = Accel(d, tt);
        if (path == null) return;
        float before = speed;
        speed = Mathf.Max(0f, speed + a * dt);
        if (!far && playerGap < HardStop(d) && !Nudging(d)) speed = 0f;
        float ds = (before + speed) * 0.5f * dt;
        obstacleGap -= ds;
        playerGap -= ds;
        if (far) CoarseBlock(dt);
        else UpdateBlock(d, dt);
        Advance(ds);
        if (path == null) return;
        if (holdCtl != null && path.kind == PathKind.Lane && s > HalfLength + 0.5f)
        {
            holdCtl.Release(this);
            holdCtl = null;
        }
        Impair(dt);
        float prevLat = lat;
        // Never carry an overtake/drift offset through an intersection. Connector curves
        // are narrow and their geometry already defines the vehicle's turning path.
        if (path.kind == PathKind.Connector)
        {
            overtaking = false;
            overtakeLeader = null;
            latGoal = 0f;
            latOffset = 0f;
        }
        float goal = path.kind == PathKind.Connector ? 0f : latGoal + latOffset;
        lat = far ? goal : Mathf.MoveTowards(lat, goal, (path.kind == PathKind.Connector ? 3f : 1.6f) * dt);
        latVel = dt > 0.0001f ? (lat - prevLat) / dt : 0f;
        if (Time.time >= nextMood) MoodTick(d);
        brakeOn = a < -0.5f || speed < 0.25f;
        if (!far && a <= -7f && speed > 3f && Time.time - lastNearMiss > 4f) NearMiss(d);
        if (far)
        {
            if (honkPlaying) StopHonk();
            engine.Stop();
        }
        else
        {
            TickHonkAudio();
            engine.Tick(transform, speed, a);
            Mark(d);
        }
        UpdateState();
        UpdateLights();
        Pose(false, Time.deltaTime);   // always smooth yaw; spawn already snaps in Pose(true, 0)
    }

    /// <summary>Far cars only think every farTick; between ticks slide them along their path at the current speed so motion stays smooth.</summary>
    void FarGlide()
    {
        float vs = Mathf.Min(s + speed * farAcc, path.length - 0.01f);
        Vector2 p, dir; float y;
        path.Sample(vs, out p, out dir, out y);
        Vector2 left = new Vector2(-dir.y, dir.x);
        p += left * lat;
        transform.position = new Vector3(p.x, y, p.y);
        float goal = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        yaw = Mathf.MoveTowardsAngle(yaw, goal, 240f * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // ---------------- LOD ----------------

    void UpdateLod(NpcDirector d, TrafficTuning tt)
    {
        nextLod = Time.time + 0.5f;
        if (d.Player == null) return;
        Vector3 p = transform.position;
        Vector3 pp = d.Player.position;
        float dx = p.x - pp.x;
        float dz = p.z - pp.z;
        float sq = dx * dx + dz * dz;
        float r = tt.farSimRadius * (far ? 0.9f : 1.1f);
        bool nowFar = sq > r * r;
        if (far && !nowFar)
        {
            nextSense = 0f;
            farAcc = 0f;
        }
        far = nowFar;
    }

    /// <summary>Far cars only keep a coarse notion of being stuck; no honking, overtaking or pedestrian sensing.</summary>
    void CoarseBlock(float dt)
    {
        obstacleGap = 999f;
        playerGap = 999f;
        overtaking = false;
        charging = false;
        latGoal = 0f;
        if (speed < 0.3f && leaderGap < 8f) blockedTime += dt;
        else blockedTime = 0f;
    }

    // ---------------- impairment / mood ----------------

    float latOffset;

    void Impair(float dt)
    {
        latOffset = 0f;
        if (mind.impair == Impairment.None || path.kind != PathKind.Lane || overtaking || far) return;
        if (mind.impair == Impairment.Drunk)
        {
            if (speed > 1f) latOffset = Mathf.Sin(Time.time * 0.8f + impSeed) * 0.5f;
        }
        else
        {
            if (sleepUntil > 0f)
            {
                latOffset = 0.9f;
                if (Time.time >= sleepUntil)
                {
                    sleepUntil = 0f;
                    speed *= 0.75f;
                    mind.Note(MemEvent.NearMiss);
                    TrafficMood.Add(Pos2D, 0.1f);
                }
            }
            else if (Time.time >= nextSleep)
            {
                sleepUntil = Time.time + 1.4f;
                nextSleep = Time.time + 25f + Roll() * 40f;
            }
        }
    }

    void MoodTick(NpcDirector d)
    {
        nextMood = Time.time + 0.5f;
        Vector2 p = Pos2D;
        float area = TrafficMood.Get(p);
        bool waiting = path.kind == PathKind.Lane && !reserved && !sigPermit && sigState == SignalState.Red;
        bool blocked = blockedTime > 2f || stuckLeaderTime > 3f;
        mind.Tick(0.5f, area, blocked, TrafficClock.Rush);
        if (blocked && mind.stress > 0.5f) TrafficMood.Add(p, 0.02f);
        if (far || !(blocked || waiting) || area < 0.45f || speed > 2f) return;
        if (Time.time < honkUntil) return;
        if (Roll() < 0.07f * (0.3f + mind.aggro) * area)
        {
            CarBehavior b = d.settings.car;
            Honk(b, Mathf.Max(0.2f, profile.honkDuration * 0.5f), profile.honkVolume * 0.85f, profile.honkPitch);
        }
    }

    void NearMiss(NpcDirector d)
    {
        lastNearMiss = Time.time;
        mind.Note(MemEvent.NearMiss);
        TrafficMood.Add(Pos2D, 0.2f);
        if (Roll() < 0.3f + mind.aggro * 0.6f && Time.time >= honkUntil)
            Honk(d.settings.car, profile.honkDuration * 0.7f, profile.honkVolume, profile.honkPitch);
    }

    // ---------------- sensing ----------------

    float HardStop(NpcDirector d)
    {
        return charging ? d.settings.car.chargeStopDistance * 0.5f : 1.5f;
    }

    void Sense(NpcDirector d)
    {
        float interval = 0.1f;
        if (mind.impair == Impairment.Drunk) interval = 0.3f;
        else if (mind.impair == Impairment.Sleepy) interval = sleepUntil > 0f ? 1.2f : 0.25f;
        nextSense = Time.time + interval;
        obstacleGap = 999f;
        playerGap = 999f;
        oncomingGap = 999f;
        Vector2 p2;
        Vector2 dir;
        float y;
        path.Sample(s, out p2, out dir, out y);
        float len = Mathf.Clamp(speed * speed / (2f * ComfortBrake) + 4f, 7f, 28f);
        float qlen = overtaking ? 48f : len;
        Vector3 pos = transform.position;
        Vector2 me = new Vector2(pos.x, pos.z);
        float best = 999f;
        if (d.Player != null)
        {
            Vector3 pp = d.Player.position;
            float pg = GapTo(me, dir, new Vector2(pp.x, pp.z), pp.y, y, 0.45f, len);
            if (pg < playerGap) playerGap = pg;
            if (pg < obstacleGap) obstacleGap = pg;
            if (pg < best)
            {
                best = pg;
                blockerPos = new Vector2(pp.x, pp.z);
                blockerIsPlayer = true;
                blockerRadius = 0.45f;
            }
        }
        d.Registry.Query(me + dir * (qlen * 0.5f), qlen * 0.5f + 3f, agentBuf);
        for (int i = 0; i < agentBuf.Count; i++)
        {
            IAgent a = agentBuf[i];
            if (a.Kind == AgentKind.Car)
            {
                Car oc = a as Car;
                if (!overtaking || oc == null || oc == this) continue;
                if (Vector2.Dot(oc.heading, dir) > -0.5f) continue;
                Vector2 rel = a.Pos2D - me;
                float ahead = Vector2.Dot(rel, dir);
                float lateral = Mathf.Abs(rel.x * dir.y - rel.y * dir.x);
                if (ahead > 0f && lateral < 2f)
                {
                    float og = ahead - HalfLength - oc.OtherHalf;
                    if (og < oncomingGap) oncomingGap = og;
                }
                continue;
            }
            if (a.Kind != AgentKind.Pedestrian) continue;
            float ag = GapTo(me, dir, a.Pos2D, a.WorldBounds.min.y, y, 0.35f, len);
            if (ag < playerGap) playerGap = ag;
            if (ag < obstacleGap) obstacleGap = ag;
            if (ag < best)
            {
                best = ag;
                blockerPos = a.Pos2D;
                blockerIsPlayer = false;
                blockerRadius = 0.35f;
            }
        }
        // Animals on the road: brake for them like for pedestrians.
        float reach = len + 8f;
        for (int i = 0; i < AnimalAgent.Active.Count; i++)
        {
            AnimalAgent an = AnimalAgent.Active[i];
            if (an == null || !an.BlocksCars) continue;
            Vector3 ap = an.Position;
            Vector2 a2 = new Vector2(ap.x, ap.z);
            if ((a2 - me).sqrMagnitude > reach * reach) continue;
            float ar = an.CarRadius;
            float ag = GapTo(me, dir, a2, ap.y, y, ar, len);
            if (ag < obstacleGap) obstacleGap = ag;
            if (ag < best)
            {
                best = ag;
                blockerPos = a2;
                blockerIsPlayer = false;
                blockerRadius = ar;
            }
        }
    }

    float GapTo(Vector2 me, Vector2 dir, Vector2 other, float otherY, float myY, float radius, float range)
    {
        if (Mathf.Abs(otherY - myY) > 2.5f) return 999f;
        Vector2 rel = other - me;
        float ahead = Vector2.Dot(rel, dir);
        if (ahead < 0f) return 999f;
        float lateral = Mathf.Abs(rel.x * dir.y - rel.y * dir.x);
        if (lateral > 1.05f + radius) return 999f;
        float g = ahead - HalfLength - radius;
        if (g > range) return 999f;
        return Mathf.Max(0f, g);
    }

    bool Blocking()
    {
        return playerGap < 12f && playerGap <= obstacleGap + 0.01f;
    }

    bool Nudging(NpcDirector d)
    {
        CarBehavior b = d.settings.car;
        return b.nudge && blockedTime > b.nudgeAfterSeconds && playerGap > 1.5f && Blocking();
    }

    // ---------------- signals ----------------

    /// <summary>True when this car may ask the intersection for a slot. Decides stop / go / run-the-light.</summary>
    bool SignalPermit(NpcDirector d, TrafficTuning tt, float toEnd)
    {
        if (!tt.signals || path.node == null || path.node.signal == null || path.axis < 0)
        {
            sigState = SignalState.Green;
            return true;
        }
        if (toEnd > SignalLook)
        {
            sigState = SignalState.Green;
            return false;
        }
        float rem;
        SignalState st = TrafficSignals.StateFor(path, out rem);
        sigState = st;
        if (sigPermit) return true;
        float brakeDist = speed * speed / (2f * ComfortBrake * 1.25f);
        bool canStop = brakeDist < toEnd - HalfLength + 1.2f || speed < 1.5f;
        switch (st)
        {
            case SignalState.Green:
            case SignalState.Flash:
                return true;
            case SignalState.Amber:
                if (!canStop || (mind.RunRedChance(tt.redRunScale, TrafficClock.Late, 0.5f) > 0.25f && toEnd < 25f && Roll() < 0.02f))
                {
                    sigPermit = true;
                    return true;
                }
                return false;
            default:
                if (!sigDecided && toEnd < 45f)
                {
                    sigDecided = true;
                    float quiet = 1f - Mathf.Clamp01(TrafficMood.Get(Pos2D) + (path.cars.Count > 2 ? 0.3f : 0f));
                    sigRun = Roll() < mind.RunRedChance(tt.redRunScale, TrafficClock.Late, quiet);
                    if (sigRun)
                    {
                        mind.Note(MemEvent.RanRed);
                        if (d.settings.logEvents) Debug.Log("Car running red: " + mind.Label());
                    }
                }
                if (sigRun || (!canStop && toEnd < HalfLength + 3f))
                {
                    sigPermit = true;
                    runningRedUntil = Time.time + 4f;
                    return true;
                }
                return false;
        }
    }

    // ---------------- longitudinal control ----------------

    float OvertakeSpeed()
    {
        if (overtakeLeader != null && !overtakeLeader.Pooled)
            return Mathf.Min(path.speed * 1.6f, Mathf.Max(path.speed * profile.speedFactor * 1.25f, overtakeLeader.speed + 5f));
        return Mathf.Min(path.speed * 1.5f, Mathf.Max(profile.overtakeSpeed, 3f));
    }

    float Accel(NpcDirector d, TrafficTuning tt)
    {
        CarBehavior cb = d.settings.car;
        float v0 = path.speed * profile.speedFactor * mind.SpeedMul;
        if (mind.impair == Impairment.Drunk) v0 *= 1f + 0.12f * Mathf.Sin(Time.time * 0.5f + impSeed);
        if (path.kind == PathKind.Lane && Time.time < boostUntil) v0 *= profile.boostFactor;
        if (overtaking) v0 = OvertakeSpeed();
        if (Time.time < abortUntil) return -6f;
        if (charging && playerGap < 30f)
        {
            float room = playerGap - cb.chargeStopDistance;
            float bd = speed * speed / 16f;
            if (room > bd + 0.3f && speed < profile.chargeSpeed) return 3.5f;
            return room > bd ? 0f : -9f;
        }
        float gap = 999f;
        float vl = 0f;
        Leader(ref gap, ref vl);
        float limitCap = 999f;
        if (path.kind == PathKind.Lane && !reserved)
        {
            float toEnd = path.length - s;
            if (toEnd < 40f && planned == null)
            {
                planned = Choose(path);
                if (planned == null)
                {
                    Despawn();
                    return 0f;
                }
            }
            bool ok = SignalPermit(d, tt, toEnd);
            if (toEnd < IntersectionController.RequestDistance && planned != null && ok && Time.time >= nextRequest)
            {
                nextRequest = Time.time + 0.15f;
                if (planned.node.controller.Request(this, planned)) reserved = true;
            }
            if (!reserved && toEnd < 60f)
            {
                float g = toEnd - HalfLength + 2f;
                if (g < gap)
                {
                    gap = g;
                    vl = 0f;
                }
            }
            if (sigState == SignalState.Flash && toEnd < 40f) limitCap = 6f;
        }
        bool nudging = Nudging(d);
        if (obstacleGap < gap && !nudging)
        {
            gap = obstacleGap;
            vl = 0f;
        }
        if (Time.time < resumeAt && gap > 0.5f)
        {
            gap = 0.5f;
            vl = 0f;
        }
        float limit = Mathf.Min(v0, limitCap);
        if (nudging) limit = Mathf.Min(limit, cb.nudgeSpeed);
        if (planned != null && path.kind == PathKind.Lane && path.length - s < 25f) limit = Mathf.Min(limit, Mathf.Max(planned.speed, 4f) + (path.length - s) * 0.3f);
        return Idm(speed, limit, gap, vl, BaseAccel * profile.accelFactor, Headway * mind.HeadwayMul);
    }

    static float Idm(float v, float v0, float gap, float vLead, float aMax, float headway)
    {
        float free = 1f - Mathf.Pow(v / Mathf.Max(0.1f, v0), 4f);
        float inter = 0f;
        if (gap < 500f)
        {
            float g = Mathf.Max(gap, 0.1f);
            float sStar = MinGap + Mathf.Max(0f, v * headway + v * (v - vLead) / (2f * Mathf.Sqrt(aMax * ComfortBrake)));
            inter = (sStar / g) * (sStar / g);
        }
        return Mathf.Clamp(aMax * (free - inter), -9f, aMax);
    }

    // ---------------- blocking, honking, overtaking ----------------

    void UpdateBlock(NpcDirector d, float dt)
    {
        CarBehavior b = d.settings.car;
        DriverProfile p = profile;
        if (overtaking) UpdateOvertake();
        bool blocking = Blocking() && (speed < 0.3f || charging);
        if (!blocking)
        {
            if (blockedTime > 0f)
            {
                blockedTime = 0f;
                stillTime = 0f;
                honksDone = 0;
                charging = false;
                notedBlocked = false;
                nextHonkAt = p.honkFirstDelay * mind.HonkTempo;
                if (!overtaking) resumeAt = Time.time + b.resumeDelay;
            }
            SlowLeader(d, b, dt);
            return;
        }
        slowLeaderTime = 0f;
        bool first = blockedTime <= 0f;
        blockedTime += dt;
        if (!notedBlocked && blockedTime > 3f)
        {
            notedBlocked = true;
            mind.Note(MemEvent.Blocked);
            TrafficMood.Add(Pos2D, 0.08f);
        }
        if (first) lastBlockerPos = blockerPos;
        if ((blockerPos - lastBlockerPos).sqrMagnitude > 0.25f)
        {
            lastBlockerPos = blockerPos;
            stillTime = 0f;
        }
        else stillTime += dt;
        if (overtaking) return;
        int honkCap = p.honkCount + (mind.stress > 0.6f ? 2 : 0);
        if (honksDone < honkCap && blockedTime >= nextHonkAt)
        {
            Honk(b, p.honkDuration, p.honkVolume, p.honkPitch);
            honksDone++;
            nextHonkAt = blockedTime + p.honkDuration + p.honkInterval * mind.HonkTempo * Mathf.Lerp(0.8f, 1.25f, Roll());
        }
        if (charging)
        {
            bool arrived = speed < 0.2f && playerGap <= b.chargeStopDistance + 0.4f;
            if (arrived || Time.time > chargeUntil)
            {
                charging = false;
                chargesDone++;
                snarlUntil = Time.time + 1f;
                Honk(b, p.honkDuration * 1.3f, 1f, p.honkPitch * 0.95f);
            }
            return;
        }
        if (blockerIsPlayer && chargesDone < p.chargeCount && honksDone >= Mathf.Min(p.chargeAfterHonks, p.honkCount) && Time.time >= snarlUntil && stillTime > 1f)
        {
            if (playerGap > b.chargeStopDistance + 0.6f)
            {
                charging = true;
                chargeUntil = Time.time + 3f;
                return;
            }
            chargesDone++;
        }
        bool chargesOk = !blockerIsPlayer || chargesDone >= p.chargeCount;
        if (!b.disableOvertake && chargesOk && stillTime >= mind.Patience && Time.time >= snarlUntil)
        {
            overtakeLeader = null;
            TryOvertake(d, b, p, 9f);
        }
    }

    /// <summary>Stuck behind a slow or stopped car: honk, then impatient drivers pull out and pass.</summary>
    void SlowLeader(NpcDirector d, CarBehavior b, float dt)
    {
        if (overtaking || path.kind != PathKind.Lane || leaderCar == null || leaderGap > 22f || leaderCar.Pooled)
        {
            slowLeaderTime = Mathf.Max(0f, slowLeaderTime - dt * 2f);
            stuckLeaderTime = 0f;
            return;
        }
        float v0 = path.speed * profile.speedFactor * mind.SpeedMul;
        bool slow = leaderSpeed < v0 * 0.7f && speed < v0 * 0.85f;
        bool stuck = leaderSpeed < 0.3f && speed < 0.3f;
        slowLeaderTime = slow ? slowLeaderTime + dt : Mathf.Max(0f, slowLeaderTime - dt * 2f);
        stuckLeaderTime = stuck ? stuckLeaderTime + dt : 0f;
        float rem;
        bool redAhead = path.node != null && path.node.signal != null && TrafficSignals.StateFor(path, out rem) != SignalState.Green && path.length - s < 80f;
        if (stuck && !redAhead && stuckLeaderTime > profile.honkFirstDelay * mind.HonkTempo + 1.8f && Time.time >= nextStuckHonk && Time.time >= honkUntil)
        {
            nextStuckHonk = Time.time + profile.honkInterval * mind.HonkTempo * 1.6f + Roll();
            if (Roll() < 0.35f + mind.aggro) Honk(b, profile.honkDuration, profile.honkVolume, profile.honkPitch);
        }
        if (b.disableOvertake || !slow || Time.time < nextOvertakeTry) return;
        if (slowLeaderTime < mind.OvertakeAfter()) return;
        if (stuck && (redAhead || stuckLeaderTime < mind.Patience * 1.5f)) return;
        if (!stuck && leaderSpeed < 0.3f) return;
        blockerPos = leaderCar.Pos2D;
        blockerRadius = 0.95f;
        overtakeLeader = leaderCar;
        TryOvertake(d, b, profile, 35f);
        if (!overtaking) overtakeLeader = null;
    }

    void TryOvertake(NpcDirector d, CarBehavior b, DriverProfile p, float minRemain)
    {
        // Suburb/open roads are narrow and often have houses, fences and parked props close
        // to the lane. Keep traffic in-lane there; only attempt passes on wider city/highway lanes.
        if (path == null || path.kind != PathKind.Lane ||
            (path.zone != ZoneType.City && path.zone != ZoneType.Highway) ||
            path.isRamp || path.length - s < minRemain || s < 3f) return;
        if (Time.time < nextOvertakeTry) return;
        nextOvertakeTry = Time.time + 0.4f;
        Vector2 pathPos;
        Vector2 dir;
        float y;
        path.Sample(s, out pathPos, out dir, out y);
        Vector2 left = new Vector2(-dir.y, dir.x);
        float bl = Vector2.Dot(blockerPos - pathPos, left);
        float need = 1.05f + blockerRadius + 0.3f;
        float lr = bl - need;
        float ll = bl + need;
        bool okR = lr >= RightLimit;
        bool okL = ll <= LeftLimit;
        float goal;
        if (okR && (!okL || Mathf.Abs(lr) <= Mathf.Abs(ll))) goal = lr;
        else if (okL) goal = ll;
        else return;
        if (!PassClear(d, goal, pathPos, dir, left)) return;
        overtaking = true;
        latGoal = goal;
        overtakeStart = Time.time;
        passTarget = blockerPos;
        charging = false;
        slowLeaderTime = 0f;
        if (d.settings.logEvents) Debug.Log("Car overtaking: " + mind.Label() + ", lateral goal " + goal.ToString("F1"));
        if (p.overtakeHonk || (overtakeLeader != null && mind.aggro > 0.4f && Roll() < 0.5f)) Honk(b, p.honkDuration * 0.6f, p.honkVolume, p.honkPitch);
    }

    bool PassClear(NpcDirector d, float goal, Vector2 me, Vector2 dir, Vector2 left)
    {
        d.Registry.Query(me + dir * 10f, 36f, passBuf);
        for (int i = 0; i < passBuf.Count; i++)
        {
            IAgent a = passBuf[i];
            if (ReferenceEquals(a, this)) continue;
            Vector2 rel = a.Pos2D - me;
            float ahead = Vector2.Dot(rel, dir);
            float rl = Vector2.Dot(rel, left) - goal;
            bool isCar = a.Kind == AgentKind.Car;
            float lim = isCar ? 2.1f : 1.45f;
            float hi = isCar ? 60f : 20f;
            if (ahead < -3.5f || ahead > hi) continue;
            if (Mathf.Abs(rl) < lim) return false;
        }
        if (d.Player != null)
        {
            Vector3 pp = d.Player.position;
            Vector2 rel = new Vector2(pp.x, pp.z) - me;
            float ahead = Vector2.Dot(rel, dir);
            float rl = Vector2.Dot(rel, left) - goal;
            if (ahead > -3.5f && ahead < 20f && Mathf.Abs(rl) < 1.55f) return false;
        }
        return true;
    }

    void UpdateOvertake()
    {
        Vector2 p;
        Vector2 dir;
        float y;
        path.Sample(s, out p, out dir, out y);
        float extra = 0f;
        if (overtakeLeader != null && !overtakeLeader.Pooled)
        {
            passTarget = overtakeLeader.Pos2D;
            extra = overtakeLeader.OtherHalf;
        }
        float past = Vector2.Dot(Pos2D - passTarget, dir);
        if (past < 0f && oncomingGap < 32f)
        {
            overtaking = false;
            overtakeLeader = null;
            latGoal = 0f;
            abortUntil = Time.time + 1f;
            return;
        }
        bool done = past > HalfLength + extra + 2f || Time.time - overtakeStart > 18f || path.kind != PathKind.Lane || path.length - s < 5f;
        if (!done) return;
        overtaking = false;
        overtakeLeader = null;
        latGoal = 0f;
        boostUntil = Time.time + profile.boostSeconds;
    }

    void Honk(CarBehavior b, float duration, float volume, float pitch)
    {
        float dur = duration * Mathf.Lerp(0.88f, 1.12f, Roll());
        AudioClip clip = HornBank.TryBegin(this, b, dur, ref lastHorn);
        if (clip == null) return;
        honkUntil = Time.time + Mathf.Max(0.3f, dur);
        PlayHonk(clip, dur, volume * b.honkVolume, pitch * Mathf.Lerp(0.97f, 1.03f, Roll()));
        TrafficMood.Add(Pos2D, 0.12f);
        if (leaderCar != null && !leaderCar.Pooled && leaderCar.mind != null && leaderGap < 14f) leaderCar.mind.Note(MemEvent.Honked);
        if (Honked != null) Honked(this);
    }

    void PlayHonk(AudioClip clip, float dur, float vol, float pitch)
    {
        if (audioSrc == null)
        {
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 1f;
            audioSrc.rolloffMode = AudioRolloffMode.Linear;
            audioSrc.minDistance = 4f;
            audioSrc.maxDistance = 70f;
        }
        audioSrc.Stop();
        audioSrc.clip = clip;
        audioSrc.pitch = Mathf.Max(0.1f, pitch);
        audioSrc.loop = dur > clip.length / audioSrc.pitch;
        audioSrc.volume = 0f;
        audioSrc.Play();
        honkStart = Time.time;
        honkEnd = Time.time + dur;
        honkVol = Mathf.Clamp01(vol);
        honkPlaying = true;
    }

    void StopHonk()
    {
        if (audioSrc != null) audioSrc.Stop();
        honkPlaying = false;
        HornBank.End(this);
    }

    void TickHonkAudio()
    {
        if (!honkPlaying || audioSrc == null) return;
        float t = Time.time;
        if (t >= honkEnd)
        {
            StopHonk();
            return;
        }
        float fadeIn = Mathf.Clamp01((t - honkStart) / 0.03f);
        float fadeOut = Mathf.Clamp01((honkEnd - t) / 0.1f);
        audioSrc.volume = honkVol * Mathf.Min(fadeIn, fadeOut);
    }

    // ---------------- following & routing ----------------

    void Leader(ref float gap, ref float vl)
    {
        float best = float.MaxValue;
        Car lead = null;
        bool passing = overtaking && Mathf.Abs(lat) > 1.3f;
        for (int i = 0; i < path.cars.Count; i++)
        {
            Car c = path.cars[i];
            if (c == this || c.s <= s) continue;
            if (passing && c == overtakeLeader) continue;
            if (c.s - s < best)
            {
                best = c.s - s;
                lead = c;
            }
        }
        if (lead != null)
        {
            gap = best - bodyLength;
            vl = lead.speed;
            SetLeader(lead, gap, vl);
            return;
        }
        float remain = path.length - s;
        RoadPath next = null;
        if (path.kind == PathKind.Connector) next = path.next;
        else if (reserved) next = planned;
        for (int hop = 0; hop < 2 && next != null; hop++)
        {
            float bs = float.MaxValue;
            Car nl = null;
            for (int i = 0; i < next.cars.Count; i++)
            {
                Car c = next.cars[i];
                if (c == this) continue;
                if (c.s < bs)
                {
                    bs = c.s;
                    nl = c;
                }
            }
            if (nl != null)
            {
                gap = remain + bs - bodyLength;
                vl = nl.speed;
                SetLeader(nl, gap, vl);
                return;
            }
            remain += next.length;
            next = next.kind == PathKind.Connector ? next.next : null;
        }
        SetLeader(null, 999f, 0f);
    }

    void SetLeader(Car lead, float gap, float vl)
    {
        leaderCar = lead;
        leaderGap = gap;
        leaderSpeed = vl;
        if (lead != prevLeader)
        {
            if (lead != null && !far && gap < 7f && speed > 3f && vl < speed * 0.85f)
            {
                mind.Note(MemEvent.CutOff);
                TrafficMood.Add(Pos2D, 0.1f);
                if (Roll() < mind.aggro * 0.8f && Time.time >= honkUntil && NpcDirector.Instance != null)
                    Honk(NpcDirector.Instance.settings.car, profile.honkDuration * 0.6f, profile.honkVolume, profile.honkPitch);
            }
            prevLeader = lead;
        }
    }

    /// <summary>Next connector: follow the planned trip; plan a new trip (often back home) when it ends.</summary>
    RoadPath Choose(RoadPath lane)
    {
        int n = lane.outs.Count;
        if (n == 0) return null;
        RoadPath c = NextOnRoute(lane);
        if (c != null) return c;
        NpcDirector d = NpcDirector.Instance;
        if (d != null && d.LaneGraph != null && (dest == null || lane.node == dest || routeIdx >= route.Count || !(routeIdx < route.Count && route[routeIdx].inLane == lane)))
        {
            NewTrip(d, lane);
            c = NextOnRoute(lane);
            if (c != null) return c;
        }
        float total = 0f;
        for (int i = 0; i < n; i++) total += Weight(lane.outs[i]);
        float r = Roll() * total;
        for (int i = 0; i < n; i++)
        {
            r -= Weight(lane.outs[i]);
            if (r <= 0f) return lane.outs[i];
        }
        return lane.outs[n - 1];
    }

    RoadPath NextOnRoute(RoadPath lane)
    {
        if (routeIdx < route.Count && route[routeIdx].inLane == lane)
        {
            RoadPath c = route[routeIdx];
            routeIdx++;
            return c;
        }
        return null;
    }

    void NewTrip(NpcDirector d, RoadPath lane)
    {
        LaneNode here = lane.node;
        bool arrivedAway = dest != null && dest.zone != home;
        returning = arrivedAway && Roll() < 0.75f;
        route.Clear();
        routeIdx = 0;
        for (int i = 0; i < 3; i++)
        {
            LaneNode t = TrafficRouter.PickDestination(d.LaneGraph, d.Rng, here != null ? here.zone : lane.zone, home, returning, here);
            if (t == null || t == here) continue;
            if (TrafficRouter.Plan(lane, t, route))
            {
                dest = t;
                return;
            }
        }
        dest = null;
        route.Clear();
    }

    static float Weight(RoadPath c)
    {
        return c.turn == 0 ? 0.6f : 0.2f;
    }

    void Advance(float ds)
    {
        s += ds;
        while (s >= path.length)
        {
            float over = s - path.length;
            if (path.kind == PathKind.Lane)
            {
                if (!reserved || planned == null)
                {
                    s = path.length - 0.01f;
                    speed = 0f;
                    return;
                }
                RoadPath nextConnector = planned;
                // A route connector must belong to this lane and lead to a valid next lane.
                // If a generated graph contains a stale/broken edge, stop safely instead of
                // advancing onto an invalid path and sending the car off-road.
                if (nextConnector.kind != PathKind.Connector ||
                    nextConnector.inLane != path || nextConnector.next == null ||
                    !path.outs.Contains(nextConnector))
                {
                    reserved = false;
                    planned = null;
                    s = path.length - 0.01f;
                    speed = 0f;
                    return;
                }
                path.cars.Remove(this);
                path = nextConnector;
                planned = null;
                s = over;
                path.cars.Add(this);
            }
            else
            {
                RoadPath conn = path;
                RoadPath nextLane = conn.next;
                if (nextLane == null)
                {
                    path.cars.Remove(this);
                    path = null;
                    Despawn();
                    return;
                }
                path.cars.Remove(this);
                path = nextLane;
                s = over;
                path.cars.Add(this);
                holdCtl = conn.node.controller;
                reserved = false;
                sigPermit = false;
                sigDecided = false;
                sigRun = false;
            }
        }
    }

    void Mark(NpcDirector d)
    {
        if (d.Traffic == null) return;
        if (path.kind == PathKind.Lane)
        {
            if (path.endCw.IsValid && (speed > 0.5f || reserved) && path.length - s < speed * 3f + 8f) d.Traffic.Mark(path.endCw);
        }
        else
        {
            if (path.inCw.IsValid) d.Traffic.Mark(path.inCw);
            if (path.outCw.IsValid) d.Traffic.Mark(path.outCw);
        }
    }

    void UpdateState()
    {
        float toEnd = path.length - s;
        if (charging) State = CarState.Charging;
        else if (overtaking) State = CarState.Overtaking;
        else if (blockedTime > 0f && Time.time < honkUntil) State = CarState.Honking;
        else if (speed > 0.3f) State = Time.time < runningRedUntil ? CarState.RunningRed : CarState.Drive;
        else if (obstacleGap < 6f) State = CarState.Blocked;
        else if (path.kind == PathKind.Lane && !reserved && path.node != null && path.node.signal != null && sigState == SignalState.Red && toEnd < SignalLook) State = CarState.WaitSignal;
        else if (path.kind == PathKind.Lane && !reserved && toEnd < IntersectionController.RequestDistance) State = CarState.WaitIntersection;
        else State = CarState.Drive;
    }

    // ---------------- lights ----------------

    static int TurnDir(int turn)
    {
        if (turn == -1 || turn == 2) return -1;
        return turn == 1 ? 1 : 0;
    }

    void UpdateLights()
    {
        if (lights == null) return;
        int ind = 0;
        if (overtaking || Mathf.Abs(latGoal) > 1.2f) ind = latGoal < 0f ? 1 : -1;
        else if (path.kind == PathKind.Connector) ind = TurnDir(path.turn);
        else if (planned != null && path.length - s < 40f) ind = TurnDir(planned.turn);
        bool head = CarLights.NightHeadlights && !headForgot;
        lights.Set(head, brakeOn, ind, blockedTime > 25f);
    }

    void Pose(bool snap, float dt)
    {
        Vector2 p;
        Vector2 dir;
        float y;
        path.Sample(s, out p, out dir, out y);
        heading = dir;
        Vector2 left = new Vector2(-dir.y, dir.x);
        p += left * lat;
        transform.position = new Vector3(p.x, y, p.y);
        Vector2 hv = dir * Mathf.Max(speed, 1.5f) + left * latVel;
        float goal = Mathf.Atan2(hv.x, hv.y) * Mathf.Rad2Deg;
        yaw = snap ? goal : Mathf.MoveTowardsAngle(yaw, goal, 240f * dt);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }
}
