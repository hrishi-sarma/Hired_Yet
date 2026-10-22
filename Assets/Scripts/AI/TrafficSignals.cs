using UnityEngine;

/// <summary>Two-phase signal plan for one intersection. Pure function of time, so far-away cars can query it for free.</summary>
public class SignalPlan
{
    public float offset;
    public float green = 16f;
    public const float Amber = 3f;
    public const float AllRed = 1.8f;

    float Half { get { return green + Amber + AllRed; } }

    public SignalState Get(int axis, out float remaining)
    {
        remaining = 99f;
        if (TrafficSignals.Flashing) return SignalState.Flash;
        float half = Half;
        float t = Mathf.Repeat(Time.time + offset, half * 2f);
        int active = t < half ? 0 : 1;
        float local = t - active * half;
        if (axis != active)
        {
            remaining = half - local + (active == 0 ? 0f : 0f);
            return SignalState.Red;
        }
        if (local < green)
        {
            remaining = green - local;
            return SignalState.Green;
        }
        if (local < green + Amber)
        {
            remaining = green + Amber - local;
            return SignalState.Amber;
        }
        remaining = half - local + 0f;
        return SignalState.Red;
    }
}

public static class TrafficSignals
{
    public static bool Flashing { get; private set; }
    static float nextRefresh;
    static GameObject lightRoot;

    /// <summary>Call once per frame; caches clock-dependent state.</summary>
    public static void Refresh()
    {
        if (Time.time < nextRefresh) return;
        nextRefresh = Time.time + 1f;
        Flashing = TrafficClock.Late;
    }

    public static void Setup(LaneGraph g, System.Random rng, CrosswalkRegistry crosswalks = null)
    {
        int made = 0;
        if (lightRoot != null) WorldUtil.Kill(lightRoot);
        lightRoot = new GameObject("TrafficLights (runtime)");
        for (int i = 0; i < g.Nodes.Count; i++)
        {
            LaneNode n = g.Nodes[i];
            if (n.arms.Count < 3) continue;
            bool city = false;
            bool ax0 = false;
            bool ax1 = false;
            for (int a = 0; a < n.arms.Count; a++)
            {
                Arm arm = n.arms[a];
                if (arm.zone == ZoneType.City) city = true;
                if (arm.zone == ZoneType.Highway) { city = false; break; }
                int axis = AxisOf(arm.dir);
                if (axis == 0) ax0 = true; else ax1 = true;
            }
            if (!city || !ax0 || !ax1) continue;
            SignalPlan plan = new SignalPlan();
            plan.offset = (float)rng.NextDouble() * 40f;
            plan.green = 13f + (float)rng.NextDouble() * 7f;
            n.signal = plan;
            for (int a = 0; a < n.arms.Count; a++)
            {
                Arm arm = n.arms[a];
                int axis = AxisOf(arm.dir);
                for (int k = 0; k < arm.inLanes.Count; k++) arm.inLanes[k].axis = axis;
                Crosswalk cw = crosswalks != null ? crosswalks.Get(arm.crosswalk) : null;
                if (cw != null && cw.plan == null)
                {
                    cw.plan = plan;
                    cw.carAxis = axis;
                    float cy = arm.inLanes.Count > 0 ? arm.inLanes[0].ys[arm.inLanes[0].ys.Length - 1] : 0f;
                    Vector2 cd = (cw.b - cw.a).normalized;
                    Vector2 perp = new Vector2(-cd.y, cd.x);
                    // Walk/don't-walk head at each end of the crosswalk, set to the side away from the junction centre, facing across.
                    Vector2 pa = cw.a + perp * (Vector2.Dot(perp, cw.a - n.pos) >= 0f ? 1.3f : -1.3f);
                    Vector2 pb = cw.b + perp * (Vector2.Dot(perp, cw.b - n.pos) >= 0f ? 1.3f : -1.3f);
                    PedestrianSignalHead.Create(lightRoot.transform, pa, cy + 0.15f, cd, crosswalks, cw.id);
                    PedestrianSignalHead.Create(lightRoot.transform, pb, cy + 0.15f, -cd, crosswalks, cw.id);
                }
                if (arm.inLanes.Count == 0) continue;
                // Head on the kerb side (left of travel) at the stop line, facing the approaching drivers.
                RoadPath lane = arm.inLanes[0];
                Vector2 travel = -arm.dir;
                Vector2 left = new Vector2(-travel.y, travel.x);
                Vector2 pos = n.pos + arm.dir * arm.stopD + left * (arm.width * 0.5f + 0.7f);
                TrafficLightHead.Create(lightRoot.transform, pos, lane.ys[lane.ys.Length - 1], arm.dir, plan, axis);
            }
            made++;
        }
        Debug.Log("TrafficSignals: " + made + " signalised intersections.");
    }

    public static int AxisOf(Vector2 dir)
    {
        return Mathf.Abs(dir.x) >= Mathf.Abs(dir.y) ? 0 : 1;
    }

    /// <summary>Signal state facing the end of an approach lane. Green when the lane is unsignalised.</summary>
    public static SignalState StateFor(RoadPath lane, out float remaining)
    {
        remaining = 99f;
        if (lane == null || lane.node == null || lane.node.signal == null || lane.axis < 0) return SignalState.Green;
        return lane.node.signal.Get(lane.axis, out remaining);
    }
}
