using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a world object the player can sit on and registers itself, so the sitting system never has to scan the scene.
/// Benches: anchor with a local seat-top centre. Fallen logs: horizontal cylinder primitives (local Y is the log axis).
/// Only objects that were explicitly given this component can be sat on: trees, stumps and the world-event carrying log never are.
/// </summary>
public sealed class SitSpot : MonoBehaviour
{
    public enum Kind { Bench, Log }

    public struct Seat
    {
        public SitSpot spot;
        public Vector3 pos;      // where the player's root goes (feet height is decided by the caller)
        public float yaw;        // facing, degrees
        public float topY;       // world height of the surface the hips rest on
        public float sq;         // flat distance squared from the query point
    }

    public static readonly List<SitSpot> All = new List<SitSpot>();

    public Kind kind;
    public Vector3 localCenter;      // Bench: centre of the seat top in local space
    public float halfWidth = 0.8f;   // Bench: half length of the seat along local X

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public static SitSpot AddBench(GameObject go, Vector3 seatTopCenterLocal, float halfWidth)
    {
        if (go == null) return null;
        SitSpot s = go.GetComponent<SitSpot>();
        if (s == null) s = go.AddComponent<SitSpot>();
        s.kind = Kind.Bench; s.localCenter = seatTopCenterLocal; s.halfWidth = halfWidth;
        return s;
    }

    /// <summary>Fallen log built from a Unity cylinder primitive lying on its side.</summary>
    public static SitSpot AddLog(GameObject go)
    {
        if (go == null) return null;
        SitSpot s = go.GetComponent<SitSpot>();
        if (s == null) s = go.AddComponent<SitSpot>();
        s.kind = Kind.Log;
        return s;
    }

    float LogRadius { get { Vector3 l = transform.lossyScale; return 0.5f * Mathf.Max(Mathf.Abs(l.x), Mathf.Abs(l.z)); } }
    float LogHalfLength { get { return Mathf.Abs(transform.lossyScale.y); } }

    /// <summary>Best seat on this object for a player standing at 'from', or false if it is too far / on the wrong side.</summary>
    public bool TrySeat(Vector3 from, float range, out Seat seat)
    {
        seat = default(Seat);
        seat.spot = this;
        Vector3 p;
        if (kind == Kind.Bench)
        {
            Vector3 local = transform.InverseTransformPoint(from);
            if (local.z < localCenter.z - 0.35f) return false;           // behind the backrest
            float lim = Mathf.Max(0f, halfWidth - 0.3f);
            float lx = Mathf.Clamp(local.x, -lim, lim);                  // sit where you are standing, at either end if you like
            p = transform.TransformPoint(new Vector3(lx, localCenter.y, localCenter.z));
            Vector3 fwd = transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            seat.yaw = Quaternion.LookRotation(fwd.normalized, Vector3.up).eulerAngles.y;
            seat.topY = p.y;
        }
        else
        {
            Vector3 axis = transform.up;
            if (Mathf.Abs(axis.y) > 0.5f) return false;                  // upright pieces are not seats
            float r = LogRadius, half = LogHalfLength;
            Vector3 center = transform.position;
            axis.y = 0f; axis.Normalize();
            float lim = Mathf.Max(0f, half - 0.4f);
            float t = Mathf.Clamp(Vector3.Dot(from - center, axis), -lim, lim);
            Vector3 onAxis = center + axis * t;
            Vector3 perp = Vector3.Cross(Vector3.up, axis);
            Vector3 d = from - onAxis; d.y = 0f;
            float side = Vector3.Dot(d, perp) >= 0f ? 1f : -1f;          // sit on the side you came from, facing out
            Vector3 face = perp * side;
            float off = r * 0.55f;
            p = onAxis + face * off;
            seat.yaw = Quaternion.LookRotation(face, Vector3.up).eulerAngles.y;
            seat.topY = center.y + Mathf.Sqrt(Mathf.Max(0.0001f, r * r - off * off));
        }
        Vector3 flat = p - from; flat.y = 0f;
        seat.sq = flat.sqrMagnitude;
        seat.pos = p;
        return seat.sq <= range * range;
    }
}
