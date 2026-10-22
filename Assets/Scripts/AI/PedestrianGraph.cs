using System.Collections.Generic;
using UnityEngine;

public enum PedEdgeKind { Sidewalk, Crosswalk }

public class PedNode
{
    public Vector2 pos;
    public float y;
    public BlockInfo block;
    public readonly List<PedEdge> outgoing = new List<PedEdge>(4);
}

public class DoorPoint
{
    public Vector2 position;
    public Vector2 normal;
    public PedEdge edge;
    public float s;
    public BlockInfo block;
}

public class PedEdge
{
    public PedEdgeKind kind;
    public PedNode from;
    public PedNode to;
    public PedEdge reverse;
    public bool primary;
    public Vector2 a;
    public Vector2 b;
    public Vector2 dir;
    public Vector2 left;
    public float length;
    public float sideY;
    public float roadY;
    public float curb;
    public float lane;
    public float innerSign;
    public BlockInfo block;
    public ZoneType zone;
    public CrosswalkId crosswalk = CrosswalkId.None;
    public readonly List<DoorPoint> doors = new List<DoorPoint>(2);
    public int stamp;

    public Vector2 Point(float s)
    {
        return a + dir * s;
    }

    public float Project(Vector2 p)
    {
        return Mathf.Clamp(Vector2.Dot(p - a, dir), 0f, length);
    }

    public float Height(float s)
    {
        if (kind == PedEdgeKind.Sidewalk) return sideY;
        float d = Mathf.Min(s, length - s);
        float t = Mathf.Clamp01((d - (curb - 0.15f)) / 0.3f);
        return Mathf.Lerp(sideY, roadY, t);
    }

    public bool OnRoad(float s)
    {
        return kind == PedEdgeKind.Crosswalk && s > curb && s < length - curb;
    }

    public float DoorS(DoorPoint d)
    {
        return d.edge == this ? d.s : length - d.s;
    }
}

public class PedestrianGraph
{
    public const float SidewalkLane = 0.55f;
    public const float CrosswalkLane = 0.8f;
    const float Cell = 16f;

    public readonly List<PedEdge> Sidewalks = new List<PedEdge>();
    public readonly List<PedEdge> Crosswalks = new List<PedEdge>();
    public readonly List<DoorPoint> Doors = new List<DoorPoint>();
    public readonly List<PedNode> Nodes = new List<PedNode>();

    readonly Dictionary<long, List<PedEdge>> grid = new Dictionary<long, List<PedEdge>>();
    readonly List<PedEdge> scratch = new List<PedEdge>(32);
    int stampCounter;

    class PointIndex
    {
        readonly Dictionary<long, List<Vector2>> cells = new Dictionary<long, List<Vector2>>();

        static long Key(int x, int z)
        {
            return ((long)x << 32) | (long)(uint)z;
        }

        public void Add(Vector2 p)
        {
            long k = Key(Mathf.FloorToInt(p.x / 8f), Mathf.FloorToInt(p.y / 8f));
            List<Vector2> list;
            if (!cells.TryGetValue(k, out list))
            {
                list = new List<Vector2>(4);
                cells[k] = list;
            }
            list.Add(p);
        }

        public bool Has(Vector2 p, float tol)
        {
            int cx = Mathf.FloorToInt(p.x / 8f);
            int cz = Mathf.FloorToInt(p.y / 8f);
            for (int z = cz - 1; z <= cz + 1; z++)
            {
                for (int x = cx - 1; x <= cx + 1; x++)
                {
                    List<Vector2> list;
                    if (!cells.TryGetValue(Key(x, z), out list)) continue;
                    for (int i = 0; i < list.Count; i++)
                        if ((list[i] - p).sqrMagnitude <= tol * tol) return true;
                }
            }
            return false;
        }
    }

    static long BlockKey(ZoneType zone, int ix, int iz)
    {
        return ((long)(int)zone << 48) | ((long)(ix & 0xFFFFFF) << 24) | (long)(iz & 0xFFFFFF);
    }

    public static PedestrianGraph Build(WorldContext ctx, CrosswalkRegistry registry)
    {
        PedestrianGraph g = new PedestrianGraph();
        if (ctx == null || ctx.roads == null) return g;

        PointIndex walks = new PointIndex();
        PointIndex roads = new PointIndex();
        List<SpawnRecord> records = ctx.occupancy.Records;
        for (int i = 0; i < records.Count; i++)
        {
            SpawnRecord r = records[i];
            if (r.layer == PlacementLayer.Sidewalk) walks.Add(r.box.center);
            else if (r.layer == PlacementLayer.Road) roads.Add(r.box.center);
        }

        List<BlockInfo> blocks = ctx.roads.blocks;
        Dictionary<long, PedNode[]> corners = new Dictionary<long, PedNode[]>();
        Dictionary<long, PedEdge[]> rings = new Dictionary<long, PedEdge[]>();
        Dictionary<long, BlockInfo> lookup = new Dictionary<long, BlockInfo>();

        for (int i = 0; i < blocks.Count; i++)
        {
            BlockInfo b = blocks[i];
            long key = BlockKey(b.zone, b.ix, b.iz);
            lookup[key] = b;
            float o = b.lotRect.width * 0.5f + b.sidewalkWidth * 0.5f;
            PedNode sw = g.NewNode(b.center + new Vector2(-o, -o), b);
            PedNode se = g.NewNode(b.center + new Vector2(o, -o), b);
            PedNode ne = g.NewNode(b.center + new Vector2(o, o), b);
            PedNode nw = g.NewNode(b.center + new Vector2(-o, o), b);
            corners[key] = new PedNode[] { sw, se, ne, nw };
            PedEdge[] ring = new PedEdge[4];
            if (walks.Has(b.center + new Vector2(0f, -o), 0.5f)) ring[0] = g.AddSidewalk(sw, se, b);
            if (walks.Has(b.center + new Vector2(0f, o), 0.5f)) ring[1] = g.AddSidewalk(nw, ne, b);
            if (walks.Has(b.center + new Vector2(-o, 0f), 0.5f)) ring[2] = g.AddSidewalk(sw, nw, b);
            if (walks.Has(b.center + new Vector2(o, 0f), 0.5f)) ring[3] = g.AddSidewalk(se, ne, b);
            rings[key] = ring;
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            BlockInfo b = blocks[i];
            PedNode[] c = corners[BlockKey(b.zone, b.ix, b.iz)];
            BlockInfo e;
            if (lookup.TryGetValue(BlockKey(b.zone, b.ix + 1, b.iz), out e) && roads.Has((b.center + e.center) * 0.5f, 0.4f))
            {
                PedNode[] ec = corners[BlockKey(e.zone, e.ix, e.iz)];
                g.AddCrosswalk(c[1], ec[0], b, registry);
                g.AddCrosswalk(c[2], ec[3], b, registry);
            }
            BlockInfo n;
            if (lookup.TryGetValue(BlockKey(b.zone, b.ix, b.iz + 1), out n) && roads.Has((b.center + n.center) * 0.5f, 0.4f))
            {
                PedNode[] nc = corners[BlockKey(n.zone, n.ix, n.iz)];
                g.AddCrosswalk(c[3], nc[0], b, registry);
                g.AddCrosswalk(c[2], nc[1], b, registry);
            }
        }

        for (int i = 0; i < records.Count; i++)
        {
            SpawnRecord r = records[i];
            if (r.layer != PlacementLayer.Building) continue;
            OrientedBox bx = r.box;
            if (Mathf.Abs(bx.axisZ.y) < 0.7f) continue;
            BlockInfo blk = null;
            for (int k = 0; k < blocks.Count; k++)
            {
                if (blocks[k].lotRect.Contains(bx.center))
                {
                    blk = blocks[k];
                    break;
                }
            }
            if (blk == null) continue;
            bool south = bx.center.y < blk.center.y;
            if (south != (bx.axisZ.y < 0f)) continue;
            PedEdge f = rings[BlockKey(blk.zone, blk.ix, blk.iz)][south ? 0 : 1];
            if (f == null) continue;
            Vector2 front = bx.center + bx.axisZ * bx.half.y;
            float s = Mathf.Clamp(Vector2.Dot(front - f.a, f.dir), 0.8f, Mathf.Max(0.8f, f.length - 0.8f));
            DoorPoint d = new DoorPoint();
            d.edge = f;
            d.s = s;
            d.position = f.Point(s);
            d.normal = bx.axisZ;
            d.block = blk;
            f.doors.Add(d);
            f.reverse.doors.Add(d);
            g.Doors.Add(d);
        }

        for (int i = 0; i < g.Sidewalks.Count; i++) g.Index(g.Sidewalks[i]);
        return g;
    }

    PedNode NewNode(Vector2 pos, BlockInfo b)
    {
        PedNode n = new PedNode();
        n.pos = pos;
        n.y = b.sidewalkTop;
        n.block = b;
        Nodes.Add(n);
        return n;
    }

    PedEdge NewEdge(PedEdgeKind kind, PedNode from, PedNode to, BlockInfo b)
    {
        PedEdge e = new PedEdge();
        e.kind = kind;
        e.from = from;
        e.to = to;
        e.a = from.pos;
        e.b = to.pos;
        Vector2 d = to.pos - from.pos;
        e.length = d.magnitude;
        e.dir = d / Mathf.Max(0.0001f, e.length);
        e.left = new Vector2(-e.dir.y, e.dir.x);
        e.sideY = b.sidewalkTop;
        e.roadY = b.roadTop;
        e.curb = b.sidewalkWidth * 0.5f;
        e.block = b;
        e.zone = b.zone;
        e.lane = kind == PedEdgeKind.Sidewalk ? SidewalkLane : CrosswalkLane;
        if (kind == PedEdgeKind.Sidewalk)
        {
            Vector2 mid = (e.a + e.b) * 0.5f;
            e.innerSign = Vector2.Dot(e.left, b.center - mid) > 0f ? 1f : -1f;
        }
        return e;
    }

    PedEdge AddSidewalk(PedNode a, PedNode b, BlockInfo blk)
    {
        PedEdge f = NewEdge(PedEdgeKind.Sidewalk, a, b, blk);
        PedEdge r = NewEdge(PedEdgeKind.Sidewalk, b, a, blk);
        f.reverse = r;
        r.reverse = f;
        f.primary = true;
        a.outgoing.Add(f);
        b.outgoing.Add(r);
        Sidewalks.Add(f);
        return f;
    }

    void AddCrosswalk(PedNode a, PedNode b, BlockInfo blk, CrosswalkRegistry registry)
    {
        if (a.outgoing.Count == 0 || b.outgoing.Count == 0) return;
        float dist = Vector2.Distance(a.pos, b.pos);
        PedEdge f = NewEdge(PedEdgeKind.Crosswalk, a, b, blk);
        PedEdge r = NewEdge(PedEdgeKind.Crosswalk, b, a, blk);
        CrosswalkId id = registry != null ? registry.Register(a.pos, b.pos, Mathf.Max(1f, dist - blk.sidewalkWidth)) : CrosswalkId.None;
        f.crosswalk = id;
        r.crosswalk = id;
        f.reverse = r;
        r.reverse = f;
        f.primary = true;
        a.outgoing.Add(f);
        b.outgoing.Add(r);
        Crosswalks.Add(f);
    }

    static long CellKey(int x, int z)
    {
        return ((long)x << 32) | (long)(uint)z;
    }

    void Index(PedEdge e)
    {
        long last = long.MinValue;
        for (float t = 0f; t <= e.length + 0.01f; t += 6f)
        {
            Vector2 p = e.Point(Mathf.Min(t, e.length));
            IndexCell(e, p, ref last);
        }
        IndexCell(e, e.b, ref last);
    }

    void IndexCell(PedEdge e, Vector2 p, ref long last)
    {
        long k = CellKey(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell));
        if (k == last) return;
        last = k;
        List<PedEdge> list;
        if (!grid.TryGetValue(k, out list))
        {
            list = new List<PedEdge>(8);
            grid[k] = list;
        }
        if (!list.Contains(e)) list.Add(e);
    }

    public int EdgesNear(Vector2 p, float radius, List<PedEdge> results)
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
                List<PedEdge> list;
                if (!grid.TryGetValue(CellKey(x, z), out list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    PedEdge e = list[i];
                    if (e.stamp == stampCounter) continue;
                    e.stamp = stampCounter;
                    results.Add(e);
                }
            }
        }
        return results.Count;
    }

    public PedEdge Nearest(Vector2 p, out float s, out float dist)
    {
        s = 0f;
        dist = float.MaxValue;
        EdgesNear(p, 12f, scratch);
        PedEdge best = null;
        for (int i = 0; i < scratch.Count; i++)
        {
            PedEdge e = scratch[i];
            float ps = e.Project(p);
            float d = Vector2.Distance(e.Point(ps), p);
            if (d < dist)
            {
                dist = d;
                s = ps;
                best = e;
            }
        }
        return best;
    }

    public void DrawGizmos(Vector3 center, float radius)
    {
        Vector2 c = new Vector2(center.x, center.z);
        EdgesNear(c, radius, scratch);
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
        for (int i = 0; i < scratch.Count; i++)
        {
            PedEdge e = scratch[i];
            Gizmos.DrawLine(new Vector3(e.a.x, e.sideY + 0.1f, e.a.y), new Vector3(e.b.x, e.sideY + 0.1f, e.b.y));
            for (int k = 0; k < e.doors.Count; k++)
            {
                if (e.doors[k].edge != e) continue;
                Vector2 dp = e.doors[k].position;
                Gizmos.DrawWireSphere(new Vector3(dp.x, e.sideY + 0.3f, dp.y), 0.25f);
            }
        }
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        float r2 = radius * radius;
        for (int i = 0; i < Crosswalks.Count; i++)
        {
            PedEdge e = Crosswalks[i];
            if (((e.a + e.b) * 0.5f - c).sqrMagnitude > r2) continue;
            Gizmos.DrawLine(new Vector3(e.a.x, e.roadY + 0.2f, e.a.y), new Vector3(e.b.x, e.roadY + 0.2f, e.b.y));
        }
    }
}
