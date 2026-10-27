using UnityEngine;

/// <summary>
/// A single butterfly that now and then drifts into the camera's view during the day, flutters around nearby for a while,
/// then flies off and is removed. Never more than one at a time, and a fixed gap (default 60 s) passes after one has
/// left before the next one arrives. Independent of the ambient flyer population.
/// </summary>
[DisallowMultipleComponent]
public sealed class ButterflyVisitor : MonoBehaviour
{
    public float gapSeconds = 60f;                 // pause between one butterfly leaving and the next arriving
    public Vector2 firstDelay = new Vector2(25f, 70f);
    public Vector2 stayRange = new Vector2(18f, 28f);
    public float maxNightToAppear = 0.25f;         // only in daylight
    public float scale = 1.9f;

    enum Phase { Wait, Visit, Leave }

    NpcDirector director;
    System.Random rng;
    Phase phase = Phase.Wait;
    float timer;

    GameObject bfly; FlyerRig rig;
    Vector3 target, vel; float yaw, flapPhase, seedPhase, retarget, stay, leaveT; Vector3 leaveDir;

    public void Initialize(NpcDirector owner)
    {
        director = owner;
        rng = new System.Random(918273 ^ System.Environment.TickCount);
        timer = R(firstDelay.x, firstDelay.y);
    }

    float Ground(float x, float z) { return director != null && director.Ctx != null ? director.Ctx.HeightAt(x, z) : -10000f; }
    float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    static float Ease(float a, float b, float k) { return a + (b - a) * k; }

    void OnDestroy() { if (bfly != null) WorldUtil.Kill(bfly); }

    void Update()
    {
        if (director != null && (!director.Running || director.Ctx == null)) { Despawn(); return; }   // game world: stop with the director; menu: director is null
        Camera cam = Camera.main;
        if (cam == null) { Despawn(); return; }
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;
        float night = StylizedMaterialFactory.CurrentNight;

        if (phase == Phase.Wait)
        {
            timer -= dt;
            if (timer <= 0f && night <= maxNightToAppear && !WeatherController.Raining) Spawn(cam);
            return;
        }

        if (bfly == null) { phase = Phase.Wait; timer = gapSeconds; return; }
        Vector3 camPos = cam.transform.position;

        if (phase == Phase.Visit)
        {
            stay -= dt;
            if (stay <= 0f || night > 0.6f || WeatherController.Raining) BeginLeave(camPos);
            else Wander(cam, dt);
        }
        else Depart(camPos, dt);

        // pose + wings
        flapPhase += dt;
        Pose(dt);
    }

    void Spawn(Camera cam)
    {
        bfly = new GameObject("Visiting Butterfly");
        bfly.transform.SetParent(transform, false);
        rig = FlyerModelFactory.Build(FlyerSpecies.Butterfly, bfly.transform, rng, 1f);
        rig.visual.localScale = Vector3.one * (rig.visual.localScale.x * scale);
        SetLayer(bfly, 2);
        seedPhase = R(0f, 50f);
        // enters from the side, in front of the camera
        Vector3 f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
        Vector3 r = Vector3.Cross(Vector3.up, f);
        float side = rng.Next(2) * 2f - 1f;
        Vector3 p = cam.transform.position + f * R(7f, 9f) + r * side * R(5f, 7f);
        p.y = Mathf.Max(Ground(p.x, p.z) + 0.8f, cam.transform.position.y - 0.4f);
        bfly.transform.position = p;
        yaw = Mathf.Atan2(-r.x * side, -r.z * side) * Mathf.Rad2Deg;
        vel = Vector3.zero; retarget = 0f;
        stay = R(stayRange.x, stayRange.y);
        phase = Phase.Visit;
    }

    void Wander(Camera cam, float dt)
    {
        Transform t = bfly.transform;
        retarget -= dt;
        if (retarget <= 0f)
        {
            retarget = R(1.4f, 3.2f);
            Vector3 f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
            Vector3 r = Vector3.Cross(Vector3.up, f);
            target = cam.transform.position + f * R(2.2f, 5.5f) + r * R(-2.8f, 2.8f);
            target.y = cam.transform.position.y + R(-0.7f, 1.2f);
        }
        Vector3 pos = t.position;
        float ground = Ground(pos.x, pos.z);
        float burst = 0.55f + 0.45f * Mathf.Sin(flapPhase * 3.1f + seedPhase);
        Vector3 want = (target - pos); float d = want.magnitude;
        want = d > 0.01f ? want / d : Vector3.zero;
        Vector3 wantVel = want * Mathf.Min(1.9f, 0.6f + d * 0.6f) * burst;
        vel = Vector3.Lerp(vel, wantVel, 1f - Mathf.Exp(-2.2f * dt));
        Vector3 next = pos + vel * dt;
        next.y += Mathf.Sin(flapPhase * 4.2f + seedPhase) * 0.18f * dt * 4f;
        if (next.y < ground + 0.35f) next.y = ground + 0.35f;
        t.position = next;
        FaceVelocity(dt, 4f);
    }

    void BeginLeave(Vector3 camPos)
    {
        Vector3 away = bfly.transform.position - camPos; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = bfly.transform.forward;
        leaveDir = (Quaternion.Euler(0f, R(-40f, 40f), 0f) * away.normalized);
        leaveT = 0f; phase = Phase.Leave;
    }

    void Depart(Vector3 camPos, float dt)
    {
        Transform t = bfly.transform;
        leaveT += dt;
        float burst = 0.7f + 0.3f * Mathf.Sin(flapPhase * 3.1f + seedPhase);
        Vector3 wantVel = leaveDir * 3.0f * burst + Vector3.up * 0.7f;
        vel = Vector3.Lerp(vel, wantVel, 1f - Mathf.Exp(-1.6f * dt));
        t.position += vel * dt;
        FaceVelocity(dt, 3f);
        Vector3 d = t.position - camPos;
        if (d.magnitude > 45f || leaveT > 16f) { Despawn(); }
    }

    void Despawn()
    {
        if (bfly != null) { WorldUtil.Kill(bfly); bfly = null; rig = null; }
        if (phase != Phase.Wait) { phase = Phase.Wait; timer = gapSeconds; }   // the gap starts when the butterfly is gone
    }

    void FaceVelocity(float dt, float rate)
    {
        Vector3 h = vel; h.y = 0f;
        if (h.sqrMagnitude > 0.0004f) yaw = Mathf.LerpAngle(yaw, Mathf.Atan2(h.x, h.z) * Mathf.Rad2Deg, 1f - Mathf.Exp(-rate * dt));
        bfly.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    void Pose(float dt)
    {
        if (rig == null || rig.wings == null || rig.wings[0] == null) return;
        float ang = 18f + Mathf.Sin(flapPhase * 42f + seedPhase) * 48f;
        rig.wings[0].localRotation = Quaternion.Euler(0f, 0f, -ang);
        rig.wings[1].localRotation = Quaternion.Euler(0f, 0f, ang);
        if (rig.body != null) rig.body.localRotation = Quaternion.Euler(Mathf.Sin(flapPhase * 42f + seedPhase + 1f) * 10f - 8f, 0f, 0f);
    }

    static void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
        foreach (var col in go.GetComponentsInChildren<Collider>(true)) WorldUtil.Kill(col);
    }
}
