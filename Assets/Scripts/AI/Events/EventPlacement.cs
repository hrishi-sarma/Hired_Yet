using System.Collections.Generic;
using UnityEngine;

public class EventPlacement
{
    static readonly Collider[] hits = new Collider[24];

    readonly NpcDirector director;
    readonly Vector2[] pts = new Vector2[5];

    public readonly Dictionary<string, int> Rejects = new Dictionary<string, int>();

    public EventPlacement(NpcDirector owner)
    {
        director = owner;
    }

    public static float TravelFor(WorldEventConfig c)
    {
        if (c.id == "fallen_log") return 0f;
        if (c.travelDistance > 0.01f) return c.travelDistance;
        switch (c.id)
        {
            case "stalled_car": return 5f;
            case "runaway_cart": return 6f;
            case "fallen_log": return 3f;
            case "boulder": return 2.5f;
        }
        return 0f;
    }

    public static Vector3 TravelDir(float yaw)
    {
        return Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
    }

    public static float PropYaw(WorldEventConfig c, float yaw)
    {
        return yaw + (TravelFor(c) > 0.01f ? 90f : 20f);
    }

    public static Vector3 PropOffset(WorldEventConfig c, float yaw)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        if (TravelFor(c) > 0.01f) return q * new Vector3(Mathf.Max(0.15f, c.propSize.z * 0.25f), 0f, -(Mathf.Max(0.15f, c.propSize.x * 0.5f) + 0.95f));   // NPC stands beside the prop so the way to its rear stays clear
        return q * new Vector3(0.9f, 0f, 0.35f);
    }

    public List<OrientedBox> Footprint(WorldEventConfig c, Vector3 pos, float yaw)
    {
        List<OrientedBox> l = new List<OrientedBox>(3);
        float y0 = pos.y - 0.1f;
        l.Add(new OrientedBox(new Vector2(pos.x, pos.z), new Vector2(0.45f, 0.45f), yaw, y0, pos.y + 2f));

        Vector3 pc = pos + PropOffset(c, yaw);
        float hx = Mathf.Max(0.15f, c.propSize.x * 0.5f);
        float hz = Mathf.Max(0.15f, c.propSize.z * 0.5f);
        float py = PropYaw(c, yaw);
        float top = pos.y + Mathf.Max(0.3f, c.propSize.y);
        l.Add(new OrientedBox(new Vector2(pc.x, pc.z), new Vector2(hx, hz), py, y0, top));

        float travel = TravelFor(c);
        if (travel > 0.01f)
        {
            Vector3 mid = pc + TravelDir(yaw) * (travel * 0.5f);
            l.Add(new OrientedBox(new Vector2(mid.x, mid.z), new Vector2(hx, travel * 0.5f + hz), py, y0, top));
        }
        return l;
    }

    bool Reject(string reason)
    {
        int n;
        Rejects.TryGetValue(reason, out n);
        Rejects[reason] = n + 1;
        return false;
    }

    void Corners(OrientedBox b)
    {
        Vector2 ax = b.axisX * b.half.x;
        Vector2 az = b.axisZ * b.half.y;
        pts[0] = b.center + ax + az;
        pts[1] = b.center + ax - az;
        pts[2] = b.center - ax + az;
        pts[3] = b.center - ax - az;
        pts[4] = b.center;
    }

    public bool Validate(WorldEventConfig c, Vector3 pos, float yaw, PedEdge edge, out List<OrientedBox> boxes)
    {
        EventSettings st = director.settings.events;
        OccupancyGrid occ = director.Ctx.occupancy;
        boxes = Footprint(c, pos, yaw);

        for (int i = 0; i < boxes.Count; i++)
            if (!occ.IsFree(boxes[i], PlacementLayer.Prop, 0.3f)) return Reject("occupied");

        float minG = float.MaxValue;
        float maxG = float.MinValue;
        for (int i = 0; i < boxes.Count; i++)
        {
            Corners(boxes[i]);
            for (int k = 0; k < 5; k++)
            {
                float g = director.GroundY(pts[k].x, pts[k].y, pos.y + 3f);
                if (g < minG) minG = g;
                if (g > maxG) maxG = g;

                if (edge != null)
                {
                    float along = Vector2.Dot(pts[k] - edge.a, edge.dir);
                    if (along < 1f || along > edge.length - 1f) return Reject("edge end");
                    Vector2 q = edge.Point(Mathf.Clamp(along, 0f, edge.length));
                    if ((pts[k] - q).magnitude < st.sidewalkClearance) return Reject("sidewalk blocked");
                    if (director.LaneGraph != null)
                    {
                        float s;
                        float dist;
                        RoadPath lane = director.LaneGraph.Nearest(pts[k], out s, out dist);
                        if (lane != null && dist < st.laneClearance) return Reject("too close to road");
                    }
                }
            }
        }
        if (maxG - minG > st.maxGroundDelta) return Reject("slope");

        for (int i = 0; i < boxes.Count; i++)
            if (BlockedByCollider(boxes[i])) return Reject("collider");
        return true;
    }

    bool BlockedByCollider(OrientedBox b)
    {
        float lo = b.minY + 0.3f;
        float hy = (b.maxY - lo) * 0.5f;
        if (hy < 0.05f) return false;
        Vector3 center = new Vector3(b.center.x, lo + hy, b.center.y);
        Quaternion rot = Quaternion.Euler(0f, b.yawDeg, 0f);
        int n = Physics.OverlapBoxNonAlloc(center, new Vector3(b.half.x, hy, b.half.y), hits, rot, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        Transform player = director.Player;
        for (int i = 0; i < n; i++)
        {
            Collider col = hits[i];
            if (col == null || col is TerrainCollider) continue;
            if (col.bounds.size.y < 0.5f) continue;
            if (player != null && col.transform.IsChildOf(player)) continue;
            return true;
        }
        return false;
    }

    public void Reserve(List<OrientedBox> boxes, GameObject owner)
    {
        OccupancyGrid occ = director.Ctx.occupancy;
        for (int i = 0; i < boxes.Count; i++) occ.Add(boxes[i], PlacementLayer.Prop, owner);
    }
}
