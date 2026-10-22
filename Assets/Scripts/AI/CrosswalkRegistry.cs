using System.Collections.Generic;
using UnityEngine;

public readonly struct CrosswalkId : System.IEquatable<CrosswalkId>
{
    public readonly int value;

    public CrosswalkId(int value)
    {
        this.value = value;
    }

    public static readonly CrosswalkId None = new CrosswalkId(-1);

    public bool IsValid
    {
        get { return value >= 0; }
    }

    public bool Equals(CrosswalkId other)
    {
        return value == other.value;
    }

    public override bool Equals(object obj)
    {
        return obj is CrosswalkId && Equals((CrosswalkId)obj);
    }

    public override int GetHashCode()
    {
        return value;
    }

    public static bool operator ==(CrosswalkId a, CrosswalkId b)
    {
        return a.value == b.value;
    }

    public static bool operator !=(CrosswalkId a, CrosswalkId b)
    {
        return a.value != b.value;
    }
}

public class Crosswalk
{
    public CrosswalkId id;
    public Vector2 a;
    public Vector2 b;
    public float width;
    public int crossing;
    public int waiting;
    public SignalPlan plan;   // same plan the cars on the crossed road obey (null = unsignalised)
    public int carAxis = -1;  // signal axis of the cars on the crossed road
}

public class CrosswalkRegistry
{
    readonly List<Crosswalk> list = new List<Crosswalk>();

    public System.Func<CrosswalkId, bool> TrafficApproaching;

    public int Count
    {
        get { return list.Count; }
    }

    public List<Crosswalk> All
    {
        get { return list; }
    }

    public CrosswalkId Register(Vector2 a, Vector2 b, float width)
    {
        Crosswalk c = new Crosswalk();
        c.id = new CrosswalkId(list.Count);
        c.a = a;
        c.b = b;
        c.width = width;
        list.Add(c);
        return c.id;
    }

    public Crosswalk Get(CrosswalkId id)
    {
        if (id.value < 0 || id.value >= list.Count) return null;
        return list[id.value];
    }

    public void EnterCrossing(CrosswalkId id)
    {
        Crosswalk c = Get(id);
        if (c != null) c.crossing++;
    }

    public void LeaveCrossing(CrosswalkId id)
    {
        Crosswalk c = Get(id);
        if (c != null && c.crossing > 0) c.crossing--;
    }

    public void BeginWaiting(CrosswalkId id)
    {
        Crosswalk c = Get(id);
        if (c != null) c.waiting++;
    }

    public void EndWaiting(CrosswalkId id)
    {
        Crosswalk c = Get(id);
        if (c != null && c.waiting > 0) c.waiting--;
    }

    public bool Occupied(CrosswalkId id)
    {
        Crosswalk c = Get(id);
        return c != null && c.crossing > 0;
    }

    public bool IsSignalled(CrosswalkId id)
    {
        Crosswalk c = Get(id);
        return c != null && c.plan != null && c.carAxis >= 0 && !TrafficSignals.Flashing;
    }

    /// <summary>Seconds a pedestrian needs to clear this crosswalk (plus a safety margin).</summary>
    public float CrossTime(CrosswalkId id)
    {
        Crosswalk c = Get(id);
        return c == null ? 6f : c.width / 1.3f + 1.5f;
    }

    /// <summary>Walk signal: true when cars on the crossed road are red with enough time left to cross. Unsignalised or flashing = true.</summary>
    public bool WalkAllowed(CrosswalkId id)
    {
        if (!IsSignalled(id)) return true;
        Crosswalk c = Get(id);
        float rem;
        SignalState st = c.plan.Get(c.carAxis, out rem);
        return st == SignalState.Red && rem > CrossTime(id);
    }

    /// <summary>0 = don't walk, 1 = walk, 2 = clearance (cars about to be released: finish crossing quickly).</summary>
    public int WalkPhase(CrosswalkId id)
    {
        if (!IsSignalled(id)) return 1;
        Crosswalk c = Get(id);
        float rem;
        SignalState st = c.plan.Get(c.carAxis, out rem);
        if (st != SignalState.Red) return 0;
        return rem > CrossTime(id) ? 1 : 2;
    }

    public bool Clear(CrosswalkId id)
    {
        if (TrafficApproaching == null) return true;
        return !TrafficApproaching(id);
    }

    public void Reset()
    {
        list.Clear();
    }
}
