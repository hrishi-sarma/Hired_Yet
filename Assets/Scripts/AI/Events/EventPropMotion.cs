using System.Collections.Generic;
using UnityEngine;
using FPAnimation;

public enum PropMode { None, Carry, CarryEnd, Shake, Spin, Drag }

/// <summary>A prop part the player picks up with one hand while the event runs (tool, jug, mushroom...).
/// It follows the first-person hand in world space, so it moves with every animation, and returns to its rest pose afterwards.</summary>
public class HeldPart : MonoBehaviour
{
    bool right;
    Vector3 lp, restP, restS;
    Quaternion lr, restR;
    float tilt, pulse, blend, vel;
    Collider[] cols;
    bool wasOn, both;

    public void Set(bool rightHand, Vector3 handPos, Vector3 handEuler, float tiltOnEffort, float pulseOnKick)
    {
        right = rightHand; lp = handPos; lr = Quaternion.Euler(handEuler); tilt = tiltOnEffort; pulse = pulseOnKick;
        restP = transform.localPosition; restR = transform.localRotation; restS = transform.localScale;
        cols = GetComponentsInChildren<Collider>();
    }

    /// <summary>Held in both hands: follows the midpoint of the two palms and squashes with FPEvents.EventSqueeze.</summary>
    public void SetBoth(Vector3 palmPos, float pulseOnKick)
    {
        Set(true, palmPos, Vector3.zero, 0f, pulseOnKick);
        both = true;
    }

    public void Drive(bool want, float intensity, float kick)
    {
        Transform hand = right ? FPEvents.HandR : FPEvents.HandL, hl = FPEvents.HandL, hr = FPEvents.HandR;
        if (both ? (hl == null || hr == null) : hand == null) want = false;
        blend = Mathf.SmoothDamp(blend, want ? 1f : 0f, ref vel, want ? 0.28f : 0.4f);
        bool on = blend > 0.003f;
        if (on != wasOn)
        {
            for (int i = 0; i < cols.Length; i++) if (cols[i] != null) cols[i].enabled = !on;
            if (!on) { transform.localPosition = restP; transform.localRotation = restR; transform.localScale = restS; }
            wasOn = on;
        }
        if (!on) return;
        Transform p = transform.parent;
        Vector3 hp = both ? (hl.TransformPoint(lp) + hr.TransformPoint(lp)) * 0.5f : hand.TransformPoint(lp);
        Quaternion hq = both ? Quaternion.Slerp(hl.rotation, hr.rotation, 0.5f) * lr : hand.rotation * lr * Quaternion.Euler(tilt * intensity, 0f, 0f);
        transform.SetPositionAndRotation(Vector3.Lerp(p.TransformPoint(restP), hp, blend), Quaternion.Slerp(p.rotation * restR, hq, blend));
        if (both)
        {
            float sq = Mathf.Clamp01(FPEvents.EventSqueeze) * blend;
            transform.localScale = new Vector3(restS.x * (1f + 0.3f * sq), restS.y * (1f - 0.4f * sq), restS.z * (1f + 0.3f * sq));
        }
        else transform.localScale = new Vector3(restS.x, restS.y * (1f - pulse * kick * blend), restS.z);
    }
}

/// <summary>Drives how the prop reacts to the player's animation: carried in both hands, one end lifted, shaken per hit,
/// valve wheels turned, boulder rolled, and the player stepping along with a pushed prop so hands stay on it.</summary>
[DefaultExecutionOrder(500)]
public class EventPropMotion : MonoBehaviour
{
    PropMode mode;
    bool push, roll;
    float length = 1f, width = 1f, height = 1f;
    Transform body;
    WorldEvent ev;
    HeldPart[] helds;
    Transform[] wheels;
    float[] wheelCur, wheelGoal;
    Collider[] cols;
    Vector3 startPos, smPos, smVel, shakeOff, shakeVel;
    float blend, bv, shakeRot, shakeRotV, kickV;
    int lastKick, wheelIdx, endSign = 1;
    bool carryOn;
    bool suppressPhysicalColliders;

    public bool Blocked { get; private set; }
    public float Contact { get; private set; }
    public float Length { get { return length; } }
    public float Width { get { return width; } }

    /// <summary>Horizontal distance from a world point to the prop's footprint (0 when inside it).</summary>
    public float Gap(Vector3 world)
    {
        Vector3 l = transform.InverseTransformPoint(world);
        float dx = Mathf.Max(Mathf.Abs(l.x) - width * 0.5f, 0f), dz = Mathf.Max(Mathf.Abs(l.z) - length * 0.5f, 0f);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public void Setup(Transform b, PropMode m, bool pushing, bool rolling, Vector3 size)
    {
        body = b; mode = m; push = pushing; roll = rolling;
        width = Mathf.Max(0.2f, size.x); height = Mathf.Max(0.2f, size.y); length = Mathf.Max(0.2f, size.z);
        helds = GetComponentsInChildren<HeldPart>();
        cols = b.GetComponentsInChildren<Collider>();
        List<Transform> w = new List<Transform>();
        foreach (Transform t in b.GetComponentsInChildren<Transform>()) if (t.name.StartsWith("Wheel")) w.Add(t);
        wheels = w.ToArray();
        wheelCur = new float[wheels.Length];
        wheelGoal = new float[wheels.Length];
    }

    void SetCols(bool on)
    {
        // Grocery and delivery-box event props must never become physical blockers, even when
        // Carry() restores its normal colliders after the player sets the prop down.
        if (suppressPhysicalColliders) on = false;
        for (int i = 0; i < cols.Length; i++) if (cols[i] != null) cols[i].enabled = on;
    }

    public void DisablePhysicalColliders()
    {
        suppressPhysicalColliders = true;
        RemovePhysicalColliders();
    }

    void RemovePhysicalColliders()
    {
        // Search from the event prop root (not only Body) to catch prefab-wrapper/root colliders.
        Collider[] all = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            all[i].enabled = false;
            WorldUtil.Kill(all[i]);
        }
        cols = new Collider[0];
    }

    void LateUpdate()
    {
        if (suppressPhysicalColliders) RemovePhysicalColliders(); // catches any collider added later by runtime helpers
        if (body == null) return;
        if (ev == null)
        {
            ev = GetComponentInParent<WorldEvent>();
            if (ev == null) return;
            startPos = transform.position;
        }
        NpcDirector d = NpcDirector.Instance;
        if (d == null || d.Player == null) return;
        float dt = Time.deltaTime;
        bool working = ev.Working;
        int kicks = FPEvents.EventKicks;
        bool kicked = working && kicks != lastKick;
        lastKick = kicks;
        if (kicked) kickV = 1f;
        kickV = Mathf.MoveTowards(kickV, 0f, dt * 6f);
        for (int i = 0; i < helds.Length; i++) if (helds[i] != null) helds[i].Drive(working, FPEvents.EventIntensity, kickV);

        switch (mode)
        {
            case PropMode.Carry: Carry(d.Player, ev.Holding, dt, false); break;
            case PropMode.CarryEnd: Carry(d.Player, ev.Holding, dt, true); break;
            case PropMode.Shake: Shake(kicked, dt); break;
            case PropMode.Spin: Spin(kicked, dt); break;
            case PropMode.Drag: Drag(d, ev.Holding, dt); break;
        }
        if (roll) body.localRotation = Quaternion.Euler(Vector3.Dot(transform.position - startPos, transform.forward) / (height * 0.5f) * Mathf.Rad2Deg, 0f, 0f);
        if (push) PushFollow(d.Player, working, dt);
    }

    void Carry(Transform player, bool working, float dt, bool endLift)
    {
        Transform hl = FPEvents.HandL, hr = FPEvents.HandR;
        bool hands = hl != null && hr != null;
        blend = Mathf.SmoothDamp(blend, working && hands ? 1f : 0f, ref bv, endLift ? 0.8f : (!working && ev.FastDrop ? 0.14f : 0.55f));
        Vector3 f = player.forward;
        f.y = 0f;
        f = f.sqrMagnitude > 0.001f ? f.normalized : transform.forward;
        if (!working && blend < 0.01f) endSign = Vector3.Dot(player.position - transform.position, transform.forward) >= 0f ? 1 : -1;
        bool on = blend > 0.003f && hands;
        if (!on)
        {
            if (carryOn) { body.localPosition = Vector3.zero; body.localRotation = Quaternion.identity; SetCols(true); carryOn = false; }
            return;
        }
        if (!carryOn) { if (!endLift) SetCols(false); carryOn = true; smPos = body.position; smVel = Vector3.zero; }
        Vector3 m = (hl.position + hr.position) * 0.5f;
        float effort = FPEvents.EventIntensity * (1f - ev.Progress * 0.5f);
        float wob = (Mathf.PerlinNoise(Time.time * 8f, 3f) - 0.5f) * 0.05f * effort;
        if (endLift)
        {
            // far end stays on the ground, the end nearest the player rises to hand height
            // the end only rises while the hands are actually on it (no telekinesis): contact fades in over the last 0.3 m of reach
            Contact = Mathf.Clamp01((0.7f - Gap(m)) / 0.3f);
            float lift = Mathf.Clamp(m.y - transform.position.y - 0.1f + wob, 0f, length * 0.45f) * Mathf.Clamp01(FPEvents.EventLift * 1.4f) * Contact;
            float th = Mathf.Asin(lift / length) * Mathf.Rad2Deg * blend;
            Quaternion r = Quaternion.AngleAxis(-endSign * th, transform.right);
            // the end nearest the player stays where it is horizontally (the far end slides toward it), so it never swings into the player
            float shift = length * (1f - Mathf.Cos(th * Mathf.Deg2Rad));
            Vector3 hp = transform.position - endSign * transform.forward * (length * 0.5f) + r * (endSign * transform.forward * (length * 0.5f)) + endSign * transform.forward * shift;
            body.SetPositionAndRotation(hp, r * transform.rotation);
            return;
        }
        float depth = Mathf.Min(0.6f, 0.5f * Mathf.Max(0.3f, Mathf.Min(width, length))) + 0.14f;
        Vector3 target = m + f * depth + Vector3.up * (0.02f - height * 0.6f + wob);   // origin is the item's base: hang the stack below the hands so the view ahead stays open
        Vector3 des = Vector3.Lerp(transform.position, target, blend);
        smPos = Vector3.SmoothDamp(smPos, des, ref smVel, 0.06f);
        body.SetPositionAndRotation(smPos, Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(-f), blend));
    }

    // Log drag: the player grabs the end nearest to them (right hand), lifts it with the couch-style squat and stands up, then drags it.
    // The far end trails on the ground at the log's length. Both ends and the middle follow the terrain height, and the trailing
    // direction is turned away whenever it would pass through the player's body.
    bool dragOn;
    Vector3 dragRear, dragFrom, lastHeld;
    Quaternion dragFromRot;
    float dragBlend, dragVel, dragSettle, liftKeep;

    static float Ground(NpcDirector d, float x, float z, float hint)
    {
        return d.Ctx != null ? d.Ctx.HeightAt(x, z) : d.GroundY(x, z, hint);
    }

    static float SegDist(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 pp = new Vector2(p.x, p.z), aa = new Vector2(a.x, a.z), ab = new Vector2(b.x, b.z) - aa;
        float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(pp - aa, ab) / ab.sqrMagnitude) : 0f;
        return (pp - (aa + ab * t)).magnitude;
    }

    void Drag(NpcDirector d, bool working, float dt)
    {
        Transform pl = d.Player, hr = FPEvents.HandR;
        float r = width * 0.5f;
        if (working && !dragOn)
        {
            float sign = Vector3.Dot(pl.position - transform.position, transform.forward) >= 0f ? 1f : -1f;
            dragRear = transform.position - sign * transform.forward * (length * 0.5f);
            dragFrom = transform.position;
            dragFromRot = transform.rotation;
            dragBlend = 0f; dragVel = 0f; liftKeep = 0f;
            lastHeld = transform.position + sign * transform.forward * (length * 0.5f);
            SetCols(false);
            dragOn = true;
        }
        if (!dragOn) return;
        dragSettle = working ? 1f : Mathf.MoveTowards(dragSettle, 0f, dt * 1.6f);
        if (!working && dragSettle <= 0.001f)
        {
            SetCols(true);
            dragOn = false;
            return;
        }
        dragBlend = Mathf.SmoothDamp(dragBlend, 1f, ref dragVel, 0.35f);
        Vector3 f = pl.forward;
        f.y = 0f;
        f = f.sqrMagnitude > 0.001f ? f.normalized : transform.forward;
        Vector3 held = lastHeld;
        if (working)
        {
            held = hr != null ? hr.position + pl.right * 0.1f : pl.position + f * 0.45f + pl.right * 0.4f;
            lastHeld = held;
            liftKeep = Mathf.Clamp01(FPEvents.EventLift * 1.3f);
        }
        float lift = liftKeep * dragSettle;
        // trailing direction: keep it out of the player's body (turn it outward to the right until the segment clears)
        Vector3 toRear = dragRear - held;
        toRear.y = 0f;
        Vector3 back = toRear.sqrMagnitude > 1e-4f ? toRear.normalized : -f;
        float ang = Vector3.SignedAngle(-f, back, Vector3.up);
        Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * (-f);
        for (int i = 0; i < 36 && SegDist(pl.position, held, held + dir * length) < 0.55f; i++)
        {
            ang = Mathf.Max(-110f, ang - 5f);
            dir = Quaternion.AngleAxis(ang, Vector3.up) * (-f);
        }
        dragRear = held + dir * length;
        float hint = pl.position.y + 3f;
        float gH = Ground(d, held.x, held.z, hint), gR = Ground(d, dragRear.x, dragRear.z, hint);
        float yR = gR + r;
        float handY = hr != null ? hr.position.y - 0.02f : gH + 0.8f;
        float yH = Mathf.Lerp(gH + r, Mathf.Max(gH + r, handY), lift);
        yH = Mathf.Min(yH, yR + length * 0.5f);   // never steeper than 30 degrees
        // never let the middle dip into a bump in the terrain
        float raise = 0f;
        for (int i = 1; i <= 3; i++)
        {
            float t = i * 0.25f;
            Vector3 p = Vector3.Lerp(dragRear, held, t);
            float need = Ground(d, p.x, p.z, hint) + r - Mathf.Lerp(yR, yH, t);
            if (need > raise) raise = need;
        }
        Vector3 hP = new Vector3(held.x, yH + raise, held.z), rP = new Vector3(dragRear.x, yR + raise, dragRear.z);
        Quaternion rot = Quaternion.LookRotation((hP - rP).normalized, Vector3.up);
        Vector3 pos = (hP + rP) * 0.5f - rot * new Vector3(0f, r, 0f);
        transform.SetPositionAndRotation(Vector3.Lerp(dragFrom, pos, dragBlend), Quaternion.Slerp(dragFromRot, rot, dragBlend));
    }

    void Shake(bool kicked, float dt)
    {
        if (kicked)
        {
            shakeVel += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 0.2f)) * 0.9f;
            shakeRotV += Random.Range(-1f, 1f) * 45f;
        }
        shakeOff += shakeVel * dt;
        shakeVel += (-shakeOff * 220f - shakeVel * 12f) * dt;
        shakeRot += shakeRotV * dt;
        shakeRotV += (-shakeRot * 220f - shakeRotV * 12f) * dt;
        body.localPosition = shakeOff;
        body.localRotation = Quaternion.Euler(0f, 0f, shakeRot);
    }

    void Spin(bool kicked, float dt)
    {
        if (wheels.Length == 0) return;
        if (kicked) wheelGoal[wheelIdx++ % wheels.Length] += 90f;
        for (int i = 0; i < wheels.Length; i++)
        {
            wheelCur[i] = Mathf.MoveTowards(wheelCur[i], wheelGoal[i], 360f * dt);
            wheels[i].localRotation = Quaternion.Euler(0f, wheelCur[i], 0f);
        }
    }

    // the player steps along behind a pushed prop so the planted hands stay on it; the prop waits if the player is in front
    void PushFollow(Transform player, bool working, float dt)
    {
        Blocked = false;
        Vector3 rel = player.position - transform.position;
        rel.y = 0f;
        float along = Vector3.Dot(rel, transform.forward);
        float lat = Mathf.Abs(Vector3.Dot(rel, transform.right));
        if (lat > width * 0.5f + 0.7f) return;
        float front = length * 0.5f;
        if (along > front - 0.1f && along < front + 0.4f) { Blocked = true; return; }
        float gap = -front - along;
        if (!working || !ev.InPosition || gap < 0.85f || gap > 2.4f) return;
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null && cc.enabled) cc.Move(transform.forward * Mathf.Min(gap - 0.75f, 4f * dt));
    }
}
