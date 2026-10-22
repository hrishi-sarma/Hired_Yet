using System.Collections.Generic;
using UnityEngine;

/// <summary>Destination trips: A* over lanes (time cost), returning the connector chain to follow.</summary>
public static class TrafficRouter
{
    class Rec
    {
        public float g;
        public RoadPath prev;
        public RoadPath via;
        public bool closed;
    }

    static readonly Dictionary<RoadPath, Rec> recs = new Dictionary<RoadPath, Rec>(512);
    static readonly List<KeyValuePair<float, RoadPath>> heap = new List<KeyValuePair<float, RoadPath>>(256);
    static readonly List<RoadPath> rev = new List<RoadPath>(64);
    const int MaxExpand = 7000;
    const float TopSpeed = 22f;

    static void Push(float f, RoadPath l)
    {
        heap.Add(new KeyValuePair<float, RoadPath>(f, l));
        int i = heap.Count - 1;
        while (i > 0)
        {
            int p = (i - 1) >> 1;
            if (heap[p].Key <= heap[i].Key) break;
            KeyValuePair<float, RoadPath> t = heap[p];
            heap[p] = heap[i];
            heap[i] = t;
            i = p;
        }
    }

    static RoadPath Pop()
    {
        RoadPath top = heap[0].Value;
        int last = heap.Count - 1;
        heap[0] = heap[last];
        heap.RemoveAt(last);
        int i = 0;
        while (true)
        {
            int l = i * 2 + 1;
            int r = l + 1;
            int m = i;
            if (l < heap.Count && heap[l].Key < heap[m].Key) m = l;
            if (r < heap.Count && heap[r].Key < heap[m].Key) m = r;
            if (m == i) break;
            KeyValuePair<float, RoadPath> t = heap[m];
            heap[m] = heap[i];
            heap[i] = t;
            i = m;
        }
        return top;
    }

    static float H(RoadPath lane, LaneNode dest)
    {
        return Vector2.Distance(lane.pts[lane.pts.Length - 1], dest.pos) / TopSpeed;
    }

    /// <summary>Fills conns with the connector chain from the end of 'from' to dest. False if unreachable.</summary>
    public static bool Plan(RoadPath from, LaneNode dest, List<RoadPath> conns)
    {
        conns.Clear();
        if (from == null || dest == null) return false;
        recs.Clear();
        heap.Clear();
        Rec r0 = new Rec();
        recs[from] = r0;
        Push(H(from, dest), from);
        int expanded = 0;
        RoadPath goal = null;
        while (heap.Count > 0 && expanded < MaxExpand)
        {
            RoadPath cur = Pop();
            Rec rc = recs[cur];
            if (rc.closed) continue;
            rc.closed = true;
            expanded++;
            if (cur.node == dest)
            {
                goal = cur;
                break;
            }
            for (int i = 0; i < cur.outs.Count; i++)
            {
                RoadPath c = cur.outs[i];
                RoadPath nx = c.next;
                if (nx == null) continue;
                float cost = c.length / Mathf.Max(2f, c.speed) + nx.length / Mathf.Max(2f, nx.speed);
                if (c.turn == 2) cost += 120f;
                else if (c.turn != 0) cost += 3f;
                float g = rc.g + cost;
                Rec rn;
                if (!recs.TryGetValue(nx, out rn))
                {
                    rn = new Rec();
                    recs[nx] = rn;
                }
                else if (rn.closed || rn.g <= g) continue;
                rn.g = g;
                rn.prev = cur;
                rn.via = c;
                Push(g + H(nx, dest), nx);
            }
        }
        if (goal == null) return false;
        rev.Clear();
        RoadPath at = goal;
        while (at != from)
        {
            Rec r = recs[at];
            rev.Add(r.via);
            at = r.prev;
        }
        for (int i = rev.Count - 1; i >= 0; i--) conns.Add(rev[i]);
        return conns.Count > 0;
    }

    static List<LaneNode> PickList(LaneGraph g, ZoneType zone)
    {
        if (zone == ZoneType.City) return g.CityNodes;
        if (zone == ZoneType.Suburb) return g.SuburbNodes;
        return g.OtherNodes;
    }

    /// <summary>Choose a trip target: mostly the other zone (suburb to city), or back home when returning.</summary>
    public static LaneNode PickDestination(LaneGraph g, System.Random rng, ZoneType here, ZoneType home, bool returning, LaneNode avoid)
    {
        List<LaneNode> list;
        double r = rng.NextDouble();
        if (returning) list = PickList(g, home);
        else if (here == ZoneType.City && TrafficClock.NightFactor > 0.5f) list = g.CityNodes; // nights: city cars keep cruising the city
        else if (r < 0.6)
            list = (here == ZoneType.City || home == ZoneType.City) && here != ZoneType.Suburb ? g.SuburbNodes : g.CityNodes;
        else list = PickList(g, here);
        if (list.Count == 0) list = g.CityNodes.Count > 0 ? g.CityNodes : g.SuburbNodes;
        if (list.Count == 0) return null;
        for (int i = 0; i < 6; i++)
        {
            LaneNode n = list[rng.Next(list.Count)];
            if (n != avoid) return n;
        }
        return list[rng.Next(list.Count)];
    }
}
