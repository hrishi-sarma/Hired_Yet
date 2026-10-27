using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rain behaviour for AnimalAgent: animals walk to the nearest tree / building (pets use their porch) and wait there
/// while it rains. When the rain is over, wet DOGS (full-body shake) and CATS (head + paw shake, then a lick) dry themselves.
/// Rabbits with a burrow dive into it (existing Hide errand). Birds/butterflies are handled in FlyerWorldController / AmbientLife.
/// </summary>
public sealed partial class AnimalAgent
{
    enum RainPhase { None, Seek, Shelter, Drying }
    RainPhase rainPhase;
    Vector3 rainTarget, rainFace;
    float rainStart, rainReactAt = -1f, wetTime, shakeStart, shakeEnd, lastRainTick = -10f;
    bool rainGaveUp, rabbitRainDone;

    // ---- shared shelter cache (trees + buildings from the world's occupancy records)
    static WorldContext sShelterCtx;
    static int sShelterCount = -1;
    static readonly List<SpawnRecord> sShelters = new List<SpawnRecord>();

    static void RefreshShelters(WorldContext ctx)
    {
        if (ctx == null || ctx.occupancy == null) { sShelters.Clear(); return; }
        List<SpawnRecord> recs = ctx.occupancy.Records;
        if (sShelterCtx == ctx && sShelterCount == recs.Count) return;
        sShelterCtx = ctx; sShelterCount = recs.Count;
        sShelters.Clear();
        for (int i = 0; i < recs.Count; i++)
        {
            SpawnRecord r = recs[i];
            if (r == null) continue;
            if (r.layer == PlacementLayer.Tree) sShelters.Add(r);
            else if (r.layer == PlacementLayer.Building && r.box.half.x > 1.5f && r.box.half.y > 1.5f) sShelters.Add(r);
        }
    }

    bool FindShelter(out Vector3 spot, out Vector3 face)
    {
        spot = transform.position; face = transform.forward;
        if (IsDomestic && home != null && HomeDistance() < 40f)
        {
            spot = home.SpotOrCenter(HomeSpot.Porch); face = home.front;
            return true;
        }
        RefreshShelters(director.Ctx);
        Vector2 p = new Vector2(transform.position.x, transform.position.z);
        float best = 75f * 75f; bool found = false;
        for (int i = 0; i < sShelters.Count; i++)
        {
            SpawnRecord r = sShelters[i];
            OrientedBox b = r.box;
            Vector2 d = p - b.center;
            if (d.sqrMagnitude > (75f + b.half.x + b.half.y) * (75f + b.half.x + b.half.y)) continue;
            Vector2 pt, n;
            if (r.layer == PlacementLayer.Tree)
            {
                n = d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
                pt = b.center + n * (Mathf.Max(b.half.x, b.half.y) + 0.7f);
            }
            else
            {
                float lx = Vector2.Dot(d, b.axisX), lz = Vector2.Dot(d, b.axisZ);
                float cx = Mathf.Clamp(lx, -b.half.x, b.half.x), cz = Mathf.Clamp(lz, -b.half.y, b.half.y);
                Vector2 ln = new Vector2(lx - cx, lz - cz);
                if (ln.sqrMagnitude < 0.0001f)
                {   // inside the footprint: leave through the nearest side
                    float ex = b.half.x - Mathf.Abs(lx), ez = b.half.y - Mathf.Abs(lz);
                    if (ex < ez) { cx = Mathf.Sign(lx) * b.half.x; ln = new Vector2(Mathf.Sign(lx), 0f); }
                    else { cz = Mathf.Sign(lz) * b.half.y; ln = new Vector2(0f, Mathf.Sign(lz)); }
                }
                ln.Normalize();
                Vector2 edge = b.center + b.axisX * cx + b.axisZ * cz;
                n = b.axisX * ln.x + b.axisZ * ln.y;
                pt = edge + n * 0.9f;
            }
            float dd = (pt - p).sqrMagnitude;
            if (dd >= best) continue;
            best = dd; found = true;
            spot = new Vector3(pt.x, 0f, pt.y); face = new Vector3(n.x, 0f, n.y);
        }
        if (!found) return false;
        float y;
        if (!TryGround(spot.x, spot.z, out y)) return false;
        spot.y = y;
        return true;
    }

    bool RainBusy { get { return rainPhase != RainPhase.None; } }

    // Returns true while the animal is sheltering / drying itself.
    bool UpdateRain(float dt, float playerDistance, Vector3 fromPlayer)
    {
        lastRainTick = Time.time;
        bool raining = WeatherController.Raining;
        float now = Time.time;
        bool dryer = Species == AnimalSpecies.Dog || Species == AnimalSpecies.Cat;

        if (!raining && rainPhase != RainPhase.Drying)
        {
            rainGaveUp = false; rabbitRainDone = false; rainReactAt = -1f;
            if (rainPhase != RainPhase.None || wetTime > 0f)
            {
                bool wetNow = wetTime > 2f;
                wetTime = 0f;
                if (dryer && wetNow && !sleeping && !hidden)
                {
                    rainPhase = RainPhase.Drying;
                    shakeStart = now + RandomRange(0.6f, 3.5f);
                    shakeEnd = shakeStart + (Species == AnimalSpecies.Dog ? 4.2f : 4.6f);
                }
                else rainPhase = RainPhase.None;
            }
            if (rainPhase == RainPhase.None) return false;
        }

        if (sleeping || hidden || now < patUntil || motion == Motion.Pat || motion == Motion.Flee) return false;

        if (raining)
        {
            if (rainPhase == RainPhase.Drying) { rainPhase = RainPhase.None; }   // it started again
            if (wantSleep || errand == Errand.GoSleep || errand == Errand.Hide || errand == Errand.Porch) return false;
            if (errand != Errand.None) CancelErrand();

            // rabbits: use the existing burrow errand
            if (Species == AnimalSpecies.Rabbit && home != null && settings.rabbitsUseBurrows && settings.enableRoutines)
            {
                if (!rabbitRainDone)
                {
                    rabbitRainDone = true;
                    Vector3 to = BurrowMouth() - transform.position;
                    if (to.magnitude < 60f) StartErrand(Errand.Hide, BurrowMouth(), false);
                }
                return false;
            }

            if (rainPhase == RainPhase.None)
            {
                if (rainGaveUp) return false;
                if (rainReactAt < 0f) rainReactAt = now + RandomRange(0.5f, 5f);   // not everyone moves at once
                wetTime += dt * 0.5f;
                if (now < rainReactAt) return false;
                if (FindShelter(out rainTarget, out rainFace)) { rainPhase = RainPhase.Seek; rainStart = now; }
                else { rainGaveUp = true; return false; }
            }

            if (rainPhase == RainPhase.Seek)
            {
                wetTime += dt;
                Vector3 to = rainTarget - transform.position; to.y = 0f;
                float d = to.magnitude;
                if (d <= 0.9f || now - rainStart > 45f) { rainPhase = RainPhase.Shelter; motion = Motion.Idle; }
                else
                {
                    moveDirection = to / d;
                    motion = (Species != AnimalSpecies.Deer && d > 10f) || (Species == AnimalSpecies.Deer && d > 25f) ? Motion.Run : Motion.Walk;
                    if (Species == AnimalSpecies.Cat) catIdle = false;
                    return true;
                }
            }

            // sheltered: wait out the rain
            motion = Motion.Idle;
            if (Species == AnimalSpecies.Cat) { catIdle = true; catIdlePose = 0; catPoseTimer = Mathf.Max(catPoseTimer, 5f); }
            else if (Species == AnimalSpecies.Dog) dogIdlePose = 1;
            FaceDirection(rainFace, dt);
            return true;
        }

        // rain is over: dry off
        motion = Motion.Idle;
        if (Species == AnimalSpecies.Cat) catIdle = true;
        if (now >= shakeEnd)
        {
            rainPhase = RainPhase.None;
            if (Species == AnimalSpecies.Cat) { catIdlePose = 2; catPoseTimer = RandomRange(5f, 8f); groomSide = rng.Next(2); }
            return false;
        }
        return true;
    }

    // ---------------------------------------------------------------- dry-off shakes (called from the Build*Target idle branches)
    bool BuildShake(Pose t, float time)
    {
        if (rainPhase != RainPhase.Drying || Time.time - lastRainTick > 0.25f)
        {
            if (rainPhase == RainPhase.Drying) { rainPhase = RainPhase.None; wetTime = 0f; }
            return false;
        }
        float s = Time.time - shakeStart;
        if (s < 0f) { t.head = 6f; t.tpitch = -8f; t.earL = t.earR = -12f; t.eye = 0.6f; for (int i = 0; i < 4; i++) t.plant[i] = true; return true; }   // "here it comes"
        t.snap = true;
        for (int i = 0; i < 4; i++) t.plant[i] = true;
        if (Species == AnimalSpecies.Dog)
        {
            // head first (0-0.7s), then three whole-body twist bursts that fade, tail whipping, ears flapping
            t.eye = 0.25f; t.tongue = 0.5f;
            t.head = 4f;
            if (s < 0.7f) { t.hyaw = Mathf.Sin(s * 90f) * 38f; t.hroll = Mathf.Sin(s * 90f) * 8f; t.twist = Mathf.Sin(s * 60f) * 6f; }
            else
            {
                float u = s - 0.7f, cyc = u % 1.15f; int burst = Mathf.Min(2, (int)(u / 1.15f));
                float amp = cyc < 0.9f ? Mathf.Lerp(26f, 15f, burst / 2f) * Mathf.Sin(cyc / 0.9f * Mathf.PI) : 0f;
                if (u > 3.45f) amp = 0f;
                float w = Mathf.Sin(u * 58f);
                t.twist = w * amp;
                t.roll = Mathf.Sin(u * 58f + 1.2f) * amp * 0.18f;
                t.hyaw = -w * amp * 0.9f;
                t.pitch = Mathf.Sin(u * 116f) * amp * 0.06f;
                t.leg[0] = t.leg[1] = Mathf.Sin(u * 58f) * amp * 0.12f;
            }
            t.tyaw = Mathf.Sin(s * 40f) * 45f;
            t.earL = Mathf.Sin(s * 95f) * 38f - 10f; t.earR = Mathf.Sin(s * 95f + 1.5f) * 38f - 10f;
        }
        else
        {
            // cat: quick head flicks, shake each front paw, tiny shiver, ends in a tail puff
            t.eye = 0.35f;
            SitPose(t);
            t.earL = t.earR = -18f + Mathf.Sin(s * 80f) * 10f;
            t.tcurl = 20f; t.tpitch = 30f; t.tyaw = 25f + Mathf.Sin(s * 12f) * 10f;
            if (s < 1.3f)
            {
                float f = s < 0.5f || (s > 0.75f && s < 1.2f) ? 1f : 0f;
                t.hyaw = Mathf.Sin(s * 85f) * 42f * f; t.hroll = Mathf.Sin(s * 85f + 1f) * 10f * f;
                t.head = 10f;
                t.twist = Mathf.Sin(s * 70f) * 5f * f;
            }
            else
            {
                int side = s < 2.7f ? 0 : 1;
                float u = (s - 1.3f) % 1.4f;
                if (s > 4.1f) u = 1.4f;
                if (u < 1.1f)
                {   // lift one paw and flick it dry
                    t.plant[side] = false;
                    t.leg[side] = -85f + Mathf.Sin(s * 62f) * 32f;
                    t.head = 22f; t.hyaw = side == 0 ? -10f : 10f;
                    t.twist = Mathf.Sin(s * 62f) * 3f;
                }
                else t.head = 15f;
            }
        }
        return true;
    }
}
