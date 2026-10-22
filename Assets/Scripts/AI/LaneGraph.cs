using System.Collections.Generic;
using UnityEngine;

public enum PathKind { Lane, Connector }

public class RoadPath
{
    public PathKind kind;
    public Vector2[] pts;
    public float[] cum;
    public float[] ys;
    public float length;
    public float speed;
    public ZoneType zone;
    public LaneNode node;
    public RoadPath next;
    public RoadPath inLane;
    public int turn;
    public Vector2 startDir;
    public Vector2 endDir;
    public Vector2 min;
    public Vector2 max;
    public CrosswalkId inCw = CrosswalkId.None;
    public CrosswalkId outCw = CrosswalkId.None;
    public CrosswalkId endCw = CrosswalkId.None;
    public readonly List<Car> cars = new List<Car>(4);
    public readonly List<RoadPath> outs = new List<RoadPath>(8);
    public readonly List<RoadPath> conflicts = new List<RoadPath>(8);
    public int stamp;
    public int axis = -1;
    public bool isRamp;

    public void Finish()
    {
        int n = pts.Length;
        cum = new float[n];
        min = pts[0];
        max = pts[0];
        for (int i = 1; i < n; i++)
        {
            cum[i] = cum[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);
            min = Vector2.Min(min, pts[i]);
            max = Vector2.Max(max, pts[i]);
        }
        length = cum[n - 1];
        startDir = (pts[1] - pts[0]).normalized;
        endDir = (pts[n - 1] - pts[n - 2]).normalized;
    }

    int Segment(float s)
    {
        int lo = 0;
        int hi = pts.Length - 2;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (cum[mid] <= s) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }

    public void Sample(float s, out Vector2 pos, out Vector2 dir, out float y)
    {
        s = Mathf.Clamp(s, 0f, length);
        int i = Segment(s);
        float seg = cum[i + 1] - cum[i];
        float t = seg > 0.0001f ? (s - cum[i]) / seg : 0f;
        pos = Vector2.Lerp(pts[i], pts[i + 1], t);
        Vector2 d = pts[i + 1] - pts[i];
        dir = d.sqrMagnitude > 0.000001f ? d.normalized : startDir;
        y = Mathf.Lerp(ys[i], ys[i + 1], t);
    }

    public float Project(Vector2 p, out float dist)
    {
        float best = float.MaxValue;
        float bs = 0f;
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector2 a = pts[i];
            Vector2 d = pts[i + 1] - a;
            float l2 = d.sqrMagnitude;
            float t = l2 > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, d) / l2) : 0f;
            float dd = (a + d * t - p).sqrMagnitude;
            if (dd < best)
            {
                best = dd;
                bs = cum[i] + Mathf.Sqrt(l2) * t;
            }
        }
        dist = Mathf.Sqrt(best);
        return bs;
    }
}

public class Arm
{
    public LaneNode node;
    public Vector2 dir;
    public float width;
    public float stopD;
    public float sw;
    public ZoneType zone;
    public CrosswalkId crosswalk = CrosswalkId.None;
    public readonly List<RoadPath> inLanes = new List<RoadPath>(2);
    public readonly List<RoadPath> outLanes = new List<RoadPath>(2);
}

public class LaneNode
{
    public Vector2 pos;
    public readonly List<Arm> arms = new List<Arm>(4);
    public readonly List<RoadPath> connectors = new List<RoadPath>(24);
    public IntersectionController controller = new IntersectionController();
    public SignalPlan signal;
    public ZoneType zone;
}

public class LaneGraph
{
    public const float Gap = 1.9f;
    const float Cell = 32f;

    public readonly List<RoadPath> Lanes = new List<RoadPath>();
    public readonly List<RoadPath> Connectors = new List<RoadPath>();
    public readonly List<LaneNode> Nodes = new List<LaneNode>();
    public readonly List<LaneNode> CityNodes = new List<LaneNode>();
    public readonly List<LaneNode> SuburbNodes = new List<LaneNode>();
    public readonly List<LaneNode> OtherNodes = new List<LaneNode>();

    readonly Dictionary<long, List<RoadPath>> grid = new Dictionary<long, List<RoadPath>>();
    readonly List<RoadPath> scratch = new List<RoadPath>(32);
    int stampCounter;

    static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    static long NodeKey(Vector2 p)
    {
        return ((long)Mathf.RoundToInt(p.x * 10f) << 32) | (long)(uint)Mathf.RoundToInt(p.y * 10f);
    }

    public static LaneGraph Build(WorldContext ctx, PedestrianGraph peds, CrosswalkRegistry crosswalks = null)
    {
        LaneGraph g = new LaneGraph();
        if (ctx == null || ctx.roads == null) return g;
        Dictionary<long, LaneNode> map = new Dictionary<long, LaneNode>();
        List<BlockInfo> blocks = ctx.roads.blocks;

        for (int i = 0; i < ctx.roads.lines.Count; i++)
        {
            RoadLine l = ctx.roads.lines[i];
            Vector2 mid = (l.a + l.b) * 0.5f;
            BlockInfo near = null;
            float bd = float.MaxValue;
            for (int k = 0; k < blocks.Count; k++)
            {
                float d = (blocks[k].center - mid).sqrMagnitude;
                if (d < bd)
                {
                    bd = d;
                    near = blocks[k];
                }
            }
            if (near == null) continue;
            float sw = near.sidewalkWidth;
            ZoneType zone = near.zone;
            float stopD = l.width * 0.5f + sw * 0.5f + Gap;
            if ((l.b - l.a).magnitude < stopD * 2f + 4f) continue;
            int lanes = zone == ZoneType.City ? 2 : 1;
            float speed = zone == ZoneType.City ? 10f : 8f;
            LaneNode na = g.GetNode(map, l.a);
            LaneNode nb = g.GetNode(map, l.b);
            Arm armA = new Arm();
            armA.node = na;
            armA.dir = (l.b - l.a).normalized;
            armA.width = l.width;
            armA.stopD = stopD;
            armA.sw = sw;
            armA.zone = zone;
            na.arms.Add(armA);
            Arm armB = new Arm();
            armB.node = nb;
            armB.dir = -armA.dir;
            armB.width = l.width;
            armB.stopD = stopD;
            armB.sw = sw;
            armB.zone = zone;
            nb.arms.Add(armB);
            for (int k = 0; k < lanes; k++)
            {
                float off = l.width * 0.5f * (2 * k + 1) / (2f * lanes);
                g.AddGridLane(l, armA, armB, off, stopD, speed, zone, true);
                g.AddGridLane(l, armB, armA, off, stopD, speed, zone, false);
            }
        }

        AssignCrosswalks(g, peds);
        AddHighway(g, ctx);

        for (int i = 0; i < g.Nodes.Count; i++) g.BuildConnectors(g.Nodes[i]);
        for (int i = 0; i < g.Lanes.Count; i++) g.Index(g.Lanes[i]);
        g.ClassifyNodes();
        TrafficSignals.Setup(g, new System.Random(l0Seed(ctx)), crosswalks);
        return g;
    }

    static int l0Seed(WorldContext ctx)
    {
        return ctx != null && ctx.settings != null ? ctx.settings.seed * 31 + 7 : 7;
    }

    void ClassifyNodes()
    {
        for (int i = 0; i < Nodes.Count; i++)
        {
            LaneNode n = Nodes[i];
            n.zone = n.arms.Count > 0 ? n.arms[0].zone : ZoneType.Open;
            if (n.arms.Count < 2 || n.connectors.Count == 0) continue;
            if (n.zone == ZoneType.City) CityNodes.Add(n);
            else if (n.zone == ZoneType.Suburb) SuburbNodes.Add(n);
            else OtherNodes.Add(n);
        }
        Debug.Log("LaneGraph: " + Lanes.Count + " lanes, " + Nodes.Count + " nodes (city " + CityNodes.Count + ", suburb " + SuburbNodes.Count + ").");
    }

    LaneNode GetNode(Dictionary<long, LaneNode> map, Vector2 p)
    {
        long k = NodeKey(p);
        LaneNode n;
        if (!map.TryGetValue(k, out n))
        {
            n = new LaneNode();
            n.pos = p;
            map[k] = n;
            Nodes.Add(n);
        }
        return n;
    }

    void AddGridLane(RoadLine l, Arm from, Arm to, float off, float stopD, float speed, ZoneType zone, bool forward)
    {
        Vector2 a = from.node.pos;
        Vector2 b = to.node.pos;
        Vector2 d = (b - a).normalized;
        Vector2 left = new Vector2(-d.y, d.x);
        RoadPath lane = new RoadPath();
        lane.kind = PathKind.Lane;
        lane.pts = new Vector2[] { a + d * stopD + left * off, b - d * stopD + left * off };
        lane.ys = new float[] { l.roadTop + 0.02f, l.roadTop + 0.02f };
        lane.speed = speed;
        lane.zone = zone;
        lane.node = to.node;
        lane.Finish();
        from.outLanes.Add(lane);
        to.inLanes.Add(lane);
        Lanes.Add(lane);
    }

    static void AssignCrosswalks(LaneGraph g, PedestrianGraph peds)
    {
        if (peds == null) return;
        for (int i = 0; i < g.Nodes.Count; i++)
        {
            LaneNode n = g.Nodes[i];
            for (int a = 0; a < n.arms.Count; a++)
            {
                Arm arm = n.arms[a];
                Vector2 expect = n.pos + arm.dir * (arm.width * 0.5f + arm.sw * 0.5f);
                for (int c = 0; c < peds.Crosswalks.Count; c++)
                {
                    PedEdge e = peds.Crosswalks[c];
                    if (((e.a + e.b) * 0.5f - expect).sqrMagnitude < 1.44f)
                    {
                        arm.crosswalk = e.crosswalk;
                        break;
                    }
                }
                for (int k = 0; k < arm.inLanes.Count; k++) arm.inLanes[k].endCw = arm.crosswalk;
            }
        }
    }

    static void AddHighway(LaneGraph g, WorldContext ctx)
    {
        int gridCount = g.Nodes.Count;
        HighwayPath path = ctx.roads.highway;
        float[] hs = ctx.roads.highwayHeights;
        if (path == null || hs == null || path.points.Count < 3) return;
        float width = 12f;
        if (ctx.generator != null)
        {
            RoadStage rs = ctx.generator.GetComponent<RoadStage>();
            if (rs != null) width = rs.highway.width;
        }
        float off = width * 0.25f;
        int n = path.points.Count;
        List<Vector2> fwd = new List<Vector2>(n);
        List<float> fy = new List<float>(n);
        for (int i = 0; i < n; i++)
        {
            fwd.Add(path.points[i]);
            fy.Add(hs[i]);
        }
        List<Vector2> rev = new List<Vector2>(fwd);
        List<float> ry = new List<float>(fy);
        rev.Reverse();
        ry.Reverse();
        RoadPath f = OffsetLane(fwd, fy, off);
        RoadPath r = OffsetLane(rev, ry, off);
        g.Lanes.Add(f);
        g.Lanes.Add(r);
        LaneNode start = new LaneNode();
        start.pos = fwd[0];
        Arm sa = new Arm();
        sa.node = start;
        sa.dir = f.startDir;
        sa.width = width;
        sa.zone = ZoneType.Highway;
        sa.inLanes.Add(r);
        sa.outLanes.Add(f);
        start.arms.Add(sa);
        r.node = start;
        LaneNode end = new LaneNode();
        end.pos = fwd[n - 1];
        Arm ea = new Arm();
        ea.node = end;
        ea.dir = r.startDir;
        ea.width = width;
        ea.zone = ZoneType.Highway;
        ea.inLanes.Add(f);
        ea.outLanes.Add(r);
        end.arms.Add(ea);
        f.node = end;
        g.Nodes.Add(start);
        g.Nodes.Add(end);
        LinkRamp(g, gridCount, start, f, r);
        LinkRamp(g, gridCount, end, r, f);
    }

    /// <summary>Joins a highway end node to the nearest free grid node with a lane pair, so traffic can leave the highway.</summary>
    static void LinkRamp(LaneGraph g, int gridCount, LaneNode h, RoadPath leave, RoadPath enter)
    {
        Vector2 hp = leave.pts[0];
        LaneNode best = null;
        float bd = 600f * 600f;
        Vector2 bdir = Vector2.zero;
        for (int i = 0; i < gridCount; i++)
        {
            LaneNode n = g.Nodes[i];
            if (n.arms.Count < 1 || n.arms.Count > 3) continue;
            Vector2 v = hp - n.pos;
            float d2 = v.sqrMagnitude;
            if (d2 >= bd || d2 < 25f) continue;
            Vector2 dir = v.normalized;
            bool free = true;
            for (int a = 0; a < n.arms.Count && free; a++)
                if (Vector2.Dot(n.arms[a].dir, dir) > 0.6f) free = false;
            if (!free) continue;
            bd = d2;
            best = n;
            bdir = dir;
        }
        if (best == null)
        {
            Debug.LogWarning("LaneGraph: highway end at " + hp + " has no grid node to link to; it stays a dead end.");
            return;
        }
        Arm ref0 = best.arms[0];
        float gy = ref0.inLanes.Count > 0 ? ref0.inLanes[0].ys[ref0.inLanes[0].ys.Length - 1] : (ref0.outLanes.Count > 0 ? ref0.outLanes[0].ys[0] : leave.ys[0]);
        float stopD = ref0.stopD;
        const float off = 1.75f;
        Vector2 gEdge = best.pos + bdir * stopD;
        Vector2 leftB = new Vector2(-bdir.y, bdir.x);
        // B: grid -> highway, A: highway -> grid (left-hand traffic, same convention as grid lanes)
        // Tangent-matched curves: leave the highway along its own heading, arrive at the grid along its street axis (no zigzag).
        RoadPath rampB = RampCurve(gEdge + leftB * off, bdir, leave.pts[0], leave.startDir, gy, leave.ys[0]);
        RoadPath rampA = RampCurve(enter.pts[enter.pts.Length - 1], enter.endDir, gEdge - leftB * off, -bdir, enter.ys[enter.ys.Length - 1], gy);
        rampB.node = h;
        rampA.node = best;
        Arm ga = new Arm();
        ga.node = best;
        ga.dir = bdir;
        ga.width = 7f;
        ga.stopD = stopD;
        ga.sw = ref0.sw;
        ga.zone = ZoneType.Highway;
        ga.inLanes.Add(rampA);
        ga.outLanes.Add(rampB);
        best.arms.Add(ga);
        Arm ha = new Arm();
        ha.node = h;
        ha.dir = -bdir;
        ha.width = 7f;
        ha.zone = ZoneType.Highway;
        ha.inLanes.Add(rampB);
        ha.outLanes.Add(rampA);
        h.arms.Add(ha);
        g.Lanes.Add(rampA);
        g.Lanes.Add(rampB);
        Debug.Log("LaneGraph: linked highway end to grid node " + best.pos + " (" + Mathf.Sqrt(bd).ToString("F0") + "m).");
    }

    static RoadPath RampCurve(Vector2 p0, Vector2 t0, Vector2 p1, Vector2 t1, float y0, float y1)
    {
        float dist = Vector2.Distance(p0, p1);
        float h = Mathf.Clamp(dist * 0.4f, 3f, 60f);
        Vector2 c0 = p0 + t0 * h;
        Vector2 c1 = p1 - t1 * h;
        int n = Mathf.Clamp(Mathf.CeilToInt(dist / 8f), 6, 40);
        RoadPath l = new RoadPath();
        l.kind = PathKind.Lane;
        l.pts = new Vector2[n + 1];
        l.ys = new float[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n, u = 1f - t;
            l.pts[i] = u * u * u * p0 + 3f * u * u * t * c0 + 3f * u * t * t * c1 + t * t * t * p1;
            l.ys[i] = Mathf.Lerp(y0, y1, t);
        }
        l.speed = 14f;
        l.zone = ZoneType.Highway;
        l.isRamp = true;
        l.Finish();
        return l;
    }

    static RoadPath RampLane(Vector2 p0, Vector2 p1, float y0, float y1)
    {
        RoadPath l = new RoadPath();
        l.kind = PathKind.Lane;
        l.pts = new Vector2[] { p0, p1 };
        l.ys = new float[] { y0, y1 };
        l.speed = 14f;
        l.zone = ZoneType.Highway;
        l.Finish();
        return l;
    }

    static RoadPath OffsetLane(List<Vector2> c, List<float> y, float off)
    {
        int n = c.Count;
        RoadPath lane = new RoadPath();
        lane.kind = PathKind.Lane;
        lane.pts = new Vector2[n];
        lane.ys = new float[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 t = i == 0 ? c[1] - c[0] : (i == n - 1 ? c[n - 1] - c[n - 2] : c[i + 1] - c[i - 1]);
            t = t.sqrMagnitude > 0f ? t.normalized : Vector2.up;
            lane.pts[i] = c[i] + new Vector2(-t.y, t.x) * off;
            lane.ys[i] = y[i] + 0.02f;
        }
        lane.speed = 22f;
        lane.zone = ZoneType.Highway;
        lane.Finish();
        return lane;
    }

    static Vector2 Control(Vector2 p0, Vector2 d0, Vector2 p2, Vector2 d1)
    {
        if (Vector2.Dot(d0, d1) < -0.9f) return (p0 + p2) * 0.5f + d0 * 5f;
        float den = Cross(d0, d1);
        if (Mathf.Abs(den) < 0.05f) return (p0 + p2) * 0.5f;
        float t = Mathf.Clamp(Cross(p2 - p0, d1) / den, 0.5f, 60f);
        return p0 + d0 * t;
    }

    void BuildConnectors(LaneNode node)
    {
        if (node.arms.Count == 0) return;
        bool dead = node.arms.Count == 1;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int a = 0; a < node.arms.Count; a++)
            {
                Arm arm = node.arms[a];
                for (int k = 0; k < arm.inLanes.Count; k++)
                {
                    RoadPath lane = arm.inLanes[k];
                    if (pass == 1 && lane.outs.Count > 0) continue;
                    for (int b = 0; b < node.arms.Count; b++)
                    {
                        Arm to = node.arms[b];
                        if (b == a && !dead) continue;
                        if (to.outLanes.Count == 0) continue;
                        MakeConnector(node, lane, k, arm.inLanes.Count, to, pass == 1 || dead);
                    }
                }
            }
        }
        List<RoadPath> list = node.connectors;
        for (int i = 0; i < list.Count; i++)
        {
            for (int j = i + 1; j < list.Count; j++)
            {
                if (list[i].inLane == list[j].inLane) continue;
                if (list[i].next == list[j].next || Near(list[i], list[j]))
                {
                    list[i].conflicts.Add(list[j]);
                    list[j].conflicts.Add(list[i]);
                }
            }
        }
    }

    void MakeConnector(LaneNode node, RoadPath lane, int k, int inCount, Arm to, bool relaxed)
    {
        Vector2 d0 = lane.endDir;
        int outCount = to.outLanes.Count;
        RoadPath outLane = to.outLanes[0];
        Vector2 d1 = outLane.startDir;
        Vector2 left0 = new Vector2(-d0.y, d0.x);
        float side = Vector2.Dot(left0, d1);
        int turn;
        int outIdx;
        if (Vector2.Dot(d0, d1) < -0.9f)
        {
            turn = 2;
            outIdx = 0;
        }
        else if (side > 0.5f)
        {
            turn = -1;
            outIdx = outCount - 1;
            if (!relaxed && k != inCount - 1) return;
        }
        else if (side < -0.5f)
        {
            turn = 1;
            outIdx = 0;
            if (!relaxed && k != 0) return;
        }
        else
        {
            turn = 0;
            outIdx = Mathf.Min(k, outCount - 1);
        }
        outLane = to.outLanes[outIdx];
        Vector2 p0 = lane.pts[lane.pts.Length - 1];
        Vector2 p2 = outLane.pts[0];
        Vector2 p1 = Control(p0, d0, p2, outLane.startDir);
        float approx = (p1 - p0).magnitude + (p2 - p1).magnitude;
        int n = Mathf.Clamp(Mathf.CeilToInt(approx / 1.5f), 4, 24);
        RoadPath c = new RoadPath();
        c.kind = PathKind.Connector;
        c.pts = new Vector2[n + 1];
        c.ys = new float[n + 1];
        float y0 = lane.ys[lane.ys.Length - 1];
        float y1 = outLane.ys[0];
        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / n;
            float u = 1f - t;
            c.pts[i] = u * u * p0 + 2f * u * t * p1 + t * t * p2;
            c.ys[i] = Mathf.Lerp(y0, y1, t);
        }
        c.speed = turn == 0 ? lane.speed : (turn == 2 ? 3.5f : 5f);
        c.zone = lane.zone;
        c.node = node;
        c.next = outLane;
        c.inLane = lane;
        c.turn = turn;
        c.inCw = lane.endCw;
        Arm outArm = to;
        c.outCw = outArm.crosswalk;
        c.Finish();
        lane.outs.Add(c);
        node.connectors.Add(c);
        Connectors.Add(c);
    }

    static bool Near(RoadPath a, RoadPath b)
    {
        if (a.min.x > b.max.x + 2.4f || b.min.x > a.max.x + 2.4f || a.min.y > b.max.y + 2.4f || b.min.y > a.max.y + 2.4f) return false;
        for (int i = 0; i < a.pts.Length; i++)
            for (int j = 0; j < b.pts.Length; j++)
                if ((a.pts[i] - b.pts[j]).sqrMagnitude < 5.76f) return true;
        return false;
    }

    static long CellKey(int x, int z)
    {
        return ((long)x << 32) | (long)(uint)z;
    }

    void Index(RoadPath lane)
    {
        long last = long.MinValue;
        for (float t = 0f; t <= lane.length + 0.01f; t += 8f)
        {
            Vector2 p;
            Vector2 d;
            float y;
            lane.Sample(t, out p, out d, out y);
            AddCell(lane, p, ref last);
        }
        Vector2 e = lane.pts[lane.pts.Length - 1];
        AddCell(lane, e, ref last);
    }

    void AddCell(RoadPath lane, Vector2 p, ref long last)
    {
        long k = CellKey(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell));
        if (k == last) return;
        last = k;
        List<RoadPath> list;
        if (!grid.TryGetValue(k, out list))
        {
            list = new List<RoadPath>(8);
            grid[k] = list;
        }
        if (!list.Contains(lane)) list.Add(lane);
    }

    public int LanesNear(Vector2 p, float radius, List<RoadPath> results)
    {
        results.Clear();
        stampCounter++;
        int x0 = Mathf.FloorToInt((p.x - radius) / Cell);
        int x1 = Mathf.FloorToInt((p.x + radius) / Cell);
        int z0 = Mathf.FloorToInt((p.y - radius) / Cell);
        int z1 = Mathf.FloorToInt((p.y + radius) / Cell);
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                List<RoadPath> list;
                if (!grid.TryGetValue(CellKey(x, z), out list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    RoadPath l = list[i];
                    if (l.stamp == stampCounter) continue;
                    l.stamp = stampCounter;
                    results.Add(l);
                }
            }
        }
        return results.Count;
    }

    public RoadPath Nearest(Vector2 p, out float s, out float dist)
    {
        s = 0f;
        dist = float.MaxValue;
        LanesNear(p, 20f, scratch);
        RoadPath best = null;
        for (int i = 0; i < scratch.Count; i++)
        {
            float d;
            float ps = scratch[i].Project(p, out d);
            if (d < dist)
            {
                dist = d;
                s = ps;
                best = scratch[i];
            }
        }
        return best;
    }

    public void DrawGizmos(Vector3 center, float radius)
    {
        Vector2 c = new Vector2(center.x, center.z);
        LanesNear(c, radius, scratch);
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.9f);
        for (int i = 0; i < scratch.Count; i++) DrawPath(scratch[i]);
        Gizmos.color = new Color(1f, 0.4f, 1f, 0.9f);
        float r2 = radius * radius;
        for (int i = 0; i < Connectors.Count; i++)
        {
            RoadPath p = Connectors[i];
            if ((p.pts[0] - c).sqrMagnitude > r2) continue;
            DrawPath(p);
        }
    }

    static void DrawPath(RoadPath p)
    {
        for (int i = 0; i < p.pts.Length - 1; i++)
            Gizmos.DrawLine(new Vector3(p.pts[i].x, p.ys[i] + 0.3f, p.pts[i].y), new Vector3(p.pts[i + 1].x, p.ys[i + 1] + 0.3f, p.pts[i + 1].y));
    }
}
