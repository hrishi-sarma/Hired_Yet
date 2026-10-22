using System.Collections.Generic;
using UnityEngine;

public class Pedestrian : AgentBase
{
    const float BlockStop = 0.85f;
    const float FollowRange = 1.8f;

    static readonly List<IAgent> nearby = new List<IAgent>(16);

    PedestrianGraph graph;
    PedEdge edge;
    float s;
    float lateral;
    float laneSign = 1f;
    float laneChangeTime;
    Vector2 blend;
    float baseSpeed = 1.3f;
    float speed;
    float yaw;
    float stateTimer;
    float nextSense;
    float nextClear;
    float lookTimer;
    float lookOffset;
    float lookGoal;
    float yieldScale = 1f;
    float blockedTime;
    float idleAtS = -1f;
    float blockDist = 99f;
    float leaderSpeed = 99f;
    float doorS;
    bool clearPlus = true;
    bool clearMinus = true;
    bool sideTried;
    bool crossClear;
    bool prepared;
    bool countedCrossing;
    bool countedWaiting;
    DoorPoint door;
    Vector3 alertTarget;
    float bodyScale = 1f;
    NpcAnimator anim;

    public PedState State { get; private set; }

    /// <summary>Custom NPC profile this pedestrian was built from (null = built-in pedestrian).</summary>
    public NpcProfile Profile { get; set; }
    public int ProfileIndex { get; set; } = -1;

    public float CurrentSpeed
    {
        get { return speed; }
    }

    public bool CanBePickpocketed
    {
        get { return State == PedState.Walk || State == PedState.Idle; }
    }

    public PedEdge CurrentEdge
    {
        get { return edge; }
    }

    public float CurrentS
    {
        get { return s; }
    }

    protected override bool DespawnBlocked
    {
        get { return countedCrossing; }
    }

    System.Random Rng
    {
        get { return NpcDirector.Instance.Rng; }
    }

    float Roll()
    {
        return (float)Rng.NextDouble();
    }

    public void Prepare(PedestrianGraph g, PedEdge e, float startS, bool fromDoor)
    {
        graph = g;
        edge = e;
        s = startS;
        laneSign = 1f;
        lateral = fromDoor ? e.innerSign * e.lane : e.lane;
        prepared = true;
    }

    readonly NpcVoice voice = new NpcVoice();

    protected override void OnSpawn()
    {
        NpcDirector d = NpcDirector.Instance;
        PedestrianBehavior b = d.settings.pedestrian;
        if (!prepared)
        {
            graph = d.PedGraph;
            SnapToGraph();
        }
        prepared = false;
        baseSpeed = Mathf.Lerp(b.speedMin, b.speedMax, Roll());
        bodyScale = 1f;
        if (Profile != null)
        {
            bodyScale = Profile.RollScale(Roll());
            if (Profile.overrideSpeed) baseSpeed = Mathf.Lerp(Profile.speedMin, Profile.speedMax, Roll());
        }
        ApplyScale(bodyScale);
        if (anim == null) anim = GetComponent<NpcAnimator>();
        if (anim != null) anim.ResetState(bodyScale, Rng);
        State = PedState.Walk;
        blend = Vector2.zero;
        stateTimer = 0f;
        nextSense = 0f;
        nextClear = 0f;
        lookOffset = 0f;
        lookGoal = 0f;
        lookTimer = 0f;
        blockedTime = 0f;
        blockDist = 99f;
        leaderSpeed = 99f;
        sideTried = false;
        laneChangeTime = -10f;
        countedCrossing = false;
        countedWaiting = false;
        door = null;
        idleAtS = -1f;
        if (edge == null) return;
        speed = baseSpeed * 0.7f;
        yaw = Mathf.Atan2(edge.dir.x, edge.dir.y) * Mathf.Rad2Deg;
        PlanEdge();
        ApplyPose(0f);
    }

    public override void OnDespawn()
    {
        voice.Reset();
        PickpocketTarget pt = GetComponent<PickpocketTarget>();
        if (pt != null) pt.Unflag();
        NpcDirector d = NpcDirector.Instance;
        if (d != null && d.Crosswalks != null && edge != null)
        {
            if (countedCrossing) d.Crosswalks.LeaveCrossing(edge.crosswalk);
            if (countedWaiting) d.Crosswalks.EndWaiting(edge.crosswalk);
        }
        countedCrossing = false;
        countedWaiting = false;
        edge = null;
        door = null;
        prepared = false;
    }

    void SnapToGraph()
    {
        edge = null;
        if (graph == null) return;
        Vector3 p = transform.position;
        float ss;
        float dist;
        PedEdge e = graph.Nearest(new Vector2(p.x, p.z), out ss, out dist);
        if (e == null || dist > 8f) return;
        if (Roll() < 0.5f)
        {
            e = e.reverse;
            ss = e.length - ss;
        }
        s = Mathf.Clamp(ss, 0.2f, Mathf.Max(0.2f, e.length - 0.2f));
        edge = e;
        laneSign = 1f;
        lateral = e.lane;
    }

    /// <summary>Voice line when this NPC catches the player pickpocketing (NPCs are otherwise silent).</summary>
    public void PlayCaughtVoice() { voice.Caught(transform); }

    public void Alert(Vector3 target, float seconds)
    {
        if (State == PedState.Cross || State == PedState.WaitAtCurb || State == PedState.EnterHouse) return;
        if (anim == null) anim = GetComponent<NpcAnimator>();
        if (anim != null) seconds = anim.AlertSeconds(seconds);
        State = PedState.Alert;
        alertTarget = target;
        stateTimer = seconds;
        speed = 0f;
    }

    protected override void OnAgentUpdate(float dt)
    {
        if (edge == null)
        {
            Despawn();
            return;
        }
        NpcDirector d = NpcDirector.Instance;
        if (Time.time >= nextSense) Sense(d);
        voice.Tick(transform);
        switch (State)
        {
            case PedState.Walk: UpdateWalk(d, dt); break;
            case PedState.Idle: UpdateIdle(dt); break;
            case PedState.WaitAtCurb: UpdateWait(d, dt); break;
            case PedState.Cross: UpdateCross(d, dt); break;
            case PedState.Yield: UpdateYield(dt); break;
            case PedState.EnterHouse: UpdateEnter(dt); break;
            case PedState.Alert: UpdateAlert(dt); break;
        }
        if (edge == null) return;
        ApplyPose(dt);
        if (anim != null) anim.Tick(State == PedState.Alert, speed, dt);
    }

    void Sense(NpcDirector d)
    {
        nextSense = Time.time + 0.1f + Roll() * 0.05f;
        blockDist = 99f;
        leaderSpeed = 99f;
        clearPlus = true;
        clearMinus = true;
        Vector2 p = Pos2D;
        Vector2 dir = edge.dir;
        float lane = edge.lane;
        d.Registry.Query(p, 5.5f, nearby);
        for (int i = 0; i < nearby.Count; i++)
        {
            IAgent a = nearby[i];
            if (ReferenceEquals(a, this)) continue;
            Vector2 rel = a.Pos2D - p;
            float ahead = Vector2.Dot(rel, dir);
            float side = Vector2.Dot(rel, edge.left);
            if (a.Kind == AgentKind.Car)
            {
                if (State != PedState.Cross) continue;
                Bounds cb = a.WorldBounds;
                Vector2 f = p + dir * 0.9f;
                if (cb.Contains(new Vector3(f.x, cb.center.y, f.y))) blockDist = Mathf.Min(blockDist, 0.5f);
                continue;
            }
            if (ahead > 0.05f && ahead < FollowRange && Mathf.Abs(side) < 0.5f)
            {
                blockDist = Mathf.Min(blockDist, ahead);
                Pedestrian o = a as Pedestrian;
                float os = 0f;
                if (o != null && o.edge != null && Vector2.Dot(o.edge.dir, dir) > 0.3f) os = o.speed;
                leaderSpeed = Mathf.Min(leaderSpeed, os);
            }
            if (ahead > -1.2f && ahead < 5f)
            {
                if (Mathf.Abs(side - (lane - lateral)) < 0.6f) clearPlus = false;
                if (Mathf.Abs(side - (-lane - lateral)) < 0.6f) clearMinus = false;
            }
        }
        Vector3 pp = d.Player.position;
        Vector2 prel = new Vector2(pp.x - p.x, pp.z - p.y);
        float pa = Vector2.Dot(prel, dir);
        float ps = Vector2.Dot(prel, edge.left);
        if (Mathf.Abs(pp.y - transform.position.y) < 2.5f)
        {
            if (pa > 0f && pa < 1.5f && Mathf.Abs(ps) < 0.65f)
            {
                blockDist = Mathf.Min(blockDist, pa - 0.25f);
                leaderSpeed = 0f;
            }
            if (pa > -1.2f && pa < 5f)
            {
                if (Mathf.Abs(ps - (lane - lateral)) < 0.8f) clearPlus = false;
                if (Mathf.Abs(ps - (-lane - lateral)) < 0.8f) clearMinus = false;
            }
        }
    }

    bool LaneClear(float sign)
    {
        return sign > 0f ? clearPlus : clearMinus;
    }

    float FollowSpeed(float desired)
    {
        if (blockDist < BlockStop) return 0f;
        if (blockDist < FollowRange && leaderSpeed < desired) return Mathf.Max(0f, leaderSpeed);
        return desired;
    }

    void Move(float dt, float target)
    {
        float acc = target > speed ? 2.5f : 6f;
        speed = Mathf.MoveTowards(speed, target, acc * dt);
        s += speed * dt;
        if (s >= edge.length) Arrive();
    }

    void Arrive()
    {
        float over = s - edge.length;
        PedEdge next = ChooseNext(edge.to, edge);
        SwitchTo(next, over);
    }

    PedEdge ChooseNext(PedNode node, PedEdge arrived)
    {
        PedestrianBehavior b = NpcDirector.Instance.settings.pedestrian;
        List<PedEdge> outs = node.outgoing;
        int cross = 0;
        int ring = 0;
        for (int i = 0; i < outs.Count; i++)
        {
            if (outs[i] == arrived.reverse) continue;
            if (outs[i].kind == PedEdgeKind.Crosswalk) cross++;
            else ring++;
        }
        if (cross + ring == 0) return arrived.reverse;
        float pc = Mathf.Clamp01(b.crossChance * (arrived.zone == ZoneType.City ? 1.5f : 1f));
        bool wantCross = cross > 0 && (ring == 0 || Roll() < pc);
        if (wantCross)
        {
            // Smarter routing: prefer a crosswalk whose walk signal is on (or that has no signal) over one the pedestrian would have to wait at.
            CrosswalkRegistry reg = NpcDirector.Instance.Crosswalks;
            if (Roll() < 0.8f)
            {
                for (int i = 0; i < outs.Count; i++)
                {
                    PedEdge o = outs[i];
                    if (o == arrived.reverse || o.kind != PedEdgeKind.Crosswalk) continue;
                    if (reg.WalkAllowed(o.crosswalk)) return o;
                }
            }
        }
        int count = wantCross ? cross : ring;
        int pick = Rng.Next(count);
        for (int i = 0; i < outs.Count; i++)
        {
            if (outs[i] == arrived.reverse) continue;
            if ((outs[i].kind == PedEdgeKind.Crosswalk) != wantCross) continue;
            if (pick-- == 0) return outs[i];
        }
        return arrived.reverse;
    }

    void SwitchTo(PedEdge next, float over)
    {
        Vector2 old = Pos2D;
        edge = next;
        s = Mathf.Clamp(over, 0f, next.length * 0.5f);
        laneSign = 1f;
        blend = old - (next.Point(s) + next.left * lateral);
        PlanEdge();
    }

    void PlanEdge()
    {
        idleAtS = -1f;
        door = null;
        if (edge.kind != PedEdgeKind.Sidewalk) return;
        PedestrianBehavior b = NpcDirector.Instance.settings.pedestrian;
        if (edge.doors.Count > 0 && Roll() < b.enterHouseChance)
        {
            int n = edge.doors.Count;
            int start = Rng.Next(n);
            for (int i = 0; i < n; i++)
            {
                DoorPoint dp = edge.doors[(start + i) % n];
                float ds = edge.DoorS(dp);
                if (ds <= s + 1.5f) continue;
                door = dp;
                doorS = ds;
                break;
            }
            if (door != null)
            {
                State = PedState.EnterHouse;
                laneSign = edge.innerSign;
                blockedTime = 0f;
                return;
            }
        }
        if (edge.length > 6f && Roll() < b.idleChance)
        {
            float lo = s + 2f;
            float hi = edge.length - 2f;
            if (hi > lo) idleAtS = Mathf.Lerp(lo, hi, Roll());
        }
    }

    void UpdateWalk(NpcDirector d, float dt)
    {
        PedestrianBehavior b = d.settings.pedestrian;
        if (edge.kind == PedEdgeKind.Crosswalk && s < edge.curb && s >= edge.curb - 0.42f)
        {
            BeginWait(d);
            return;
        }
        if (blockDist < BlockStop) blockedTime += dt;
        else blockedTime = 0f;
        if (blockedTime > 0.25f && (edge.kind == PedEdgeKind.Sidewalk || s < edge.curb))
        {
            State = PedState.Yield;
            stateTimer = 0f;
            sideTried = false;
            yieldScale = Mathf.Lerp(0.8f, 1.4f, Roll());
            speed = 0f;
            return;
        }
        if (edge.kind == PedEdgeKind.Sidewalk)
        {
            if (blockDist < 2f && leaderSpeed < baseSpeed - 0.1f && Time.time - laneChangeTime > 1.5f && LaneClear(-laneSign))
            {
                laneSign = -laneSign;
                laneChangeTime = Time.time;
            }
            else if (laneSign < 0f && Time.time - laneChangeTime > 2f && blockDist > 2.5f && LaneClear(1f))
            {
                laneSign = 1f;
                laneChangeTime = Time.time;
            }
            if (idleAtS >= 0f && s >= idleAtS)
            {
                StartIdle(b);
                return;
            }
        }
        Move(dt, FollowSpeed(baseSpeed));
    }

    void StartIdle(PedestrianBehavior b)
    {
        State = PedState.Idle;
        stateTimer = Mathf.Lerp(b.idleSecondsMin, b.idleSecondsMax, Roll());
        speed = 0f;
        idleAtS = -1f;
        lookTimer = 0f;
        lookGoal = 0f;
    }

    void UpdateIdle(float dt)
    {
        speed = 0f;
        stateTimer -= dt;
        lookTimer -= dt;
        if (lookTimer <= 0f)
        {
            lookGoal = Mathf.Lerp(-75f, 75f, Roll());
            lookTimer = Mathf.Lerp(1.2f, 3f, Roll());
        }
        lookOffset = Mathf.MoveTowards(lookOffset, lookGoal, 60f * dt);
        if (stateTimer <= 0f) State = PedState.Walk;
    }

    void BeginWait(NpcDirector d)
    {
        State = PedState.WaitAtCurb;
        stateTimer = 0f;
        speed = 0f;
        lookTimer = 0f;
        lookGoal = 0f;
        nextClear = 0f;
        crossClear = false;
        countedWaiting = true;
        d.Crosswalks.BeginWaiting(edge.crosswalk);
    }

    void UpdateWait(NpcDirector d, float dt)
    {
        PedestrianBehavior b = d.settings.pedestrian;
        speed = 0f;
        stateTimer += dt;
        lookTimer -= dt;
        if (lookTimer <= 0f)
        {
            lookGoal = (Roll() < 0.5f ? -1f : 1f) * Mathf.Lerp(45f, 80f, Roll());
            lookTimer = Mathf.Lerp(0.8f, 1.4f, Roll());
        }
        lookOffset = Mathf.MoveTowards(lookOffset, lookGoal, 150f * dt);
        float maxWait = d.Crosswalks.IsSignalled(edge.crosswalk) ? Mathf.Max(b.curbWaitMax, 70f) : b.curbWaitMax; // signalised: wait for the walk phase
        if (stateTimer >= maxWait)
        {
            d.Crosswalks.EndWaiting(edge.crosswalk);
            countedWaiting = false;
            Reverse();
            State = PedState.Walk;
            return;
        }
        if (stateTimer < 1f) return;
        if (Time.time >= nextClear)
        {
            crossClear = d.Crosswalks.WalkAllowed(edge.crosswalk) && d.Crosswalks.Clear(edge.crosswalk);
            nextClear = Time.time + 0.2f;
        }
        if (!crossClear) return;
        d.Crosswalks.EndWaiting(edge.crosswalk);
        countedWaiting = false;
        d.Crosswalks.EnterCrossing(edge.crosswalk);
        countedCrossing = true;
        State = PedState.Cross;
        laneSign = 1f;
    }

    void UpdateCross(NpcDirector d, float dt)
    {
        PedestrianBehavior b = d.settings.pedestrian;
        if (s >= edge.length - edge.curb + 0.05f)
        {
            d.Crosswalks.LeaveCrossing(edge.crosswalk);
            countedCrossing = false;
            State = PedState.Walk;
            return;
        }
        float cs = Mathf.Max(baseSpeed, b.crossSpeed);
        if (d.Crosswalks.WalkPhase(edge.crosswalk) != 1) cs *= 1.45f; // walk phase ending: hurry across
        Move(dt, FollowSpeed(cs));
    }

    void UpdateYield(float dt)
    {
        PedestrianBehavior b = NpcDirector.Instance.settings.pedestrian;
        speed = 0f;
        stateTimer += dt;
        if (blockDist >= BlockStop)
        {
            State = PedState.Walk;
            blockedTime = 0f;
            return;
        }
        if (!sideTried && edge.kind == PedEdgeKind.Sidewalk && stateTimer > b.yieldSideSeconds * yieldScale)
        {
            sideTried = true;
            if (LaneClear(-laneSign))
            {
                laneSign = -laneSign;
                laneChangeTime = Time.time;
                State = PedState.Walk;
                return;
            }
        }
        if (stateTimer > b.yieldTurnAroundSeconds * yieldScale && CanReverse())
        {
            Reverse();
            State = PedState.Walk;
            blockedTime = 0f;
        }
    }

    bool CanReverse()
    {
        if (edge.reverse == null) return false;
        if (edge.kind == PedEdgeKind.Sidewalk) return true;
        return s < edge.curb;
    }

    void Reverse()
    {
        PedEdge r = edge.reverse;
        if (r == null) return;
        s = edge.length - s;
        lateral = -lateral;
        edge = r;
        laneSign = 1f;
        PlanEdge();
        if (State == PedState.EnterHouse) State = PedState.Walk;
        door = null;
    }

    void UpdateEnter(float dt)
    {
        if (door == null)
        {
            State = PedState.Walk;
            return;
        }
        if (s >= doorS - 0.4f)
        {
            Despawn();
            return;
        }
        if (blockDist < BlockStop) blockedTime += dt;
        else blockedTime = 0f;
        if (blockedTime > 5f)
        {
            door = null;
            State = PedState.Walk;
            return;
        }
        Move(dt, FollowSpeed(baseSpeed));
    }

    void UpdateAlert(float dt)
    {
        speed = 0f;
        stateTimer -= dt;
        if (stateTimer <= 0f) State = PedState.Walk;
    }

    void ApplyPose(float dt)
    {
        lateral = Mathf.MoveTowards(lateral, laneSign * edge.lane, 1.2f * dt);
        blend = Vector2.MoveTowards(blend, Vector2.zero, 1.3f * dt);
        if (State != PedState.Idle && State != PedState.WaitAtCurb) lookOffset = Mathf.MoveTowards(lookOffset, 0f, 180f * dt);
        Vector2 p = edge.Point(s) + edge.left * lateral + blend;
        transform.position = new Vector3(p.x, edge.Height(s), p.y);
        float goal;
        if (State == PedState.Alert)
        {
            Vector3 to = alertTarget - transform.position;
            goal = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        }
        else goal = Mathf.Atan2(edge.dir.x, edge.dir.y) * Mathf.Rad2Deg + lookOffset;
        yaw = Mathf.MoveTowardsAngle(yaw, goal, 420f * dt);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }
}
