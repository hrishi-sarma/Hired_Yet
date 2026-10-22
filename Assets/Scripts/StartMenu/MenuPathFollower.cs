using UnityEngine;

/// <summary>
/// Stage 4B: tiny runtime driver for the pre-placed menu cast (cars, NPCs, dogs, cats). No AI, no pooling, never despawns.
/// Follows a baked polyline (closed loop, or open chain that wraps) with smooth per-frame motion and yaw.
/// NPCs play their idle/walk clips through NpcAnimator; animals swing their legs procedurally.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Start Menu/Menu Path Follower")]
public class MenuPathFollower : MonoBehaviour
{
    public enum Kind { Car, Walker, Animal }

    public Kind kind = Kind.Car;
    public Vector3[] pts = new Vector3[0];
    public float[] speeds = new float[0];      // metres/second at each point (lerped along segments)
    public bool closed = true;
    public float speedMul = 1f;
    public float startDistance;
    public float lookAhead = 3f;
    public float turnRate = 8f;

    [Header("Stop & go (0 = never rests)")]
    public float moveSeconds;
    public float restSeconds;

    [Header("NPC animation")]
    public NpcAnimationSet animSet;
    public NpcAnimationSet defaultAnimSet;

    [Header("Animal gait")]
    public Transform[] legs = new Transform[0];
    public Transform tail;
    public float legSwing = 28f;

    float[] cum;
    float total, dist, speed, timer;
    bool resting;
    NpcAnimator npcAnim;
    Quaternion[] legHome;
    Quaternion tailHome;
    float phase;

    void Awake()
    {
        int n = pts != null ? pts.Length : 0;
        if (n < 2) { enabled = false; return; }
        cum = new float[n];
        for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
        total = cum[n - 1] + (closed ? Vector3.Distance(pts[n - 1], pts[0]) : 0f);
        if (total < 0.5f) { enabled = false; return; }
        dist = Mathf.Repeat(startDistance, total);
        if (speeds == null || speeds.Length != n) { speeds = new float[n]; for (int i = 0; i < n; i++) speeds[i] = 1.2f; }
        timer = moveSeconds > 0f ? Random.value * moveSeconds : 0f;

        if (kind == Kind.Animal)
        {
            legHome = new Quaternion[legs.Length];
            for (int i = 0; i < legs.Length; i++) if (legs[i] != null) legHome[i] = legs[i].localRotation;
            if (tail != null) tailHome = tail.localRotation;
            phase = Random.value * 6.28f;
        }
        Place(dist, true);
    }

    void Start()
    {
        if (kind != Kind.Walker) return;
        Animator a = GetComponentInChildren<Animator>(true);
        if (a == null) return;
        ResolvedAnimations ra = NpcAnimationSet.Resolve(animSet, defaultAnimSet);
        npcAnim = gameObject.AddComponent<NpcAnimator>();
        npcAnim.Configure(a, ra);
        npcAnim.ResetState(transform.localScale.y, new System.Random(GetInstanceID()));
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float target = SpeedAt(dist) * speedMul;
        if (moveSeconds > 0f)
        {
            timer -= dt;
            if (timer <= 0f) { resting = !resting; timer = resting ? restSeconds * (0.6f + Random.value * 0.8f) : moveSeconds * (0.6f + Random.value * 0.8f); }
            if (resting) target = 0f;
        }
        speed = Mathf.MoveTowards(speed, target, (kind == Kind.Car ? 4f : 2f) * dt);

        dist += speed * dt;
        if (dist >= total)
        {
            if (closed) dist -= total;
            else { dist = 0f; Place(0f, true); }
        }
        Place(dist, false, dt);

        if (npcAnim != null) npcAnim.Tick(false, speed, dt);
        else if (kind == Kind.Animal) Gait(dt);
    }

    float SpeedAt(float d)
    {
        int i = Seg(d, out float t);
        int j = Next(i);
        return Mathf.Lerp(speeds[i], speeds[j], t);
    }

    int Next(int i) { return i + 1 < pts.Length ? i + 1 : (closed ? 0 : i); }

    int Seg(float d, out float t)
    {
        int lo = 0, hi = pts.Length - 1;
        while (lo < hi) { int mid = (lo + hi + 1) >> 1; if (cum[mid] <= d) lo = mid; else hi = mid - 1; }
        float end = lo + 1 < pts.Length ? cum[lo + 1] : total;
        float seg = end - cum[lo];
        t = seg > 0.0001f ? Mathf.Clamp01((d - cum[lo]) / seg) : 0f;
        return lo;
    }

    Vector3 PointAt(float d)
    {
        if (closed) d = Mathf.Repeat(d, total); else d = Mathf.Clamp(d, 0f, total);
        int i = Seg(d, out float t);
        return Vector3.Lerp(pts[i], pts[Next(i)], t);
    }

    void Place(float d, bool snap, float dt = 0f)
    {
        Vector3 p = PointAt(d);
        Vector3 ahead = PointAt(d + lookAhead);
        Vector3 dir = ahead - p; dir.y = 0f;
        Quaternion want = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : transform.rotation;
        transform.position = p;
        transform.rotation = snap ? want : Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-turnRate * dt));
    }

    void Gait(float dt)
    {
        phase += speed * dt * 5.5f;
        float k = Mathf.Clamp01(speed / 0.5f);
        for (int i = 0; i < legs.Length; i++)
        {
            if (legs[i] == null) continue;
            float s = Mathf.Sin(phase + ((i == 0 || i == 3) ? 0f : Mathf.PI));
            legs[i].localRotation = legHome[i] * Quaternion.Euler(s * legSwing * k, 0f, 0f);
        }
        if (tail != null) tail.localRotation = tailHome * Quaternion.Euler(0f, Mathf.Sin(Time.time * 2.6f + phase) * 14f, 0f);
    }
}
