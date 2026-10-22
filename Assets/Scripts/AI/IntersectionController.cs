using System.Collections.Generic;
using UnityEngine;

public class IntersectionController
{
    class Entry
    {
        public Car car;
        public RoadPath conn;
        public float arrive;
    }

    public const float RequestDistance = 14f;
    const float Timeout = 8f;
    const float OutSpace = 9f;

    readonly List<Entry> waiting = new List<Entry>(8);
    readonly List<Entry> granted = new List<Entry>(8);

    static Entry Find(List<Entry> list, Car car)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i].car == car) return list[i];
        return null;
    }

    void Purge()
    {
        for (int i = waiting.Count - 1; i >= 0; i--)
            if (waiting[i].car == null || waiting[i].car.Pooled) waiting.RemoveAt(i);
        for (int i = granted.Count - 1; i >= 0; i--)
            if (granted[i].car == null || granted[i].car.Pooled) granted.RemoveAt(i);
    }

    public bool Request(Car car, RoadPath conn)
    {
        Purge();
        Entry e = Find(granted, car);
        if (e != null) return true;
        e = Find(waiting, car);
        if (e == null)
        {
            e = new Entry();
            e.car = car;
            e.conn = conn;
            e.arrive = Time.time;
            waiting.Add(e);
        }
        e.conn = conn;
        if (!CanGrant(e)) return false;
        waiting.Remove(e);
        granted.Add(e);
        return true;
    }

    public float WaitTime(Car car)
    {
        Entry e = Find(waiting, car);
        return e == null ? 0f : Time.time - e.arrive;
    }

    public void Release(Car car)
    {
        Entry e = Find(waiting, car);
        if (e != null) waiting.Remove(e);
        e = Find(granted, car);
        if (e != null) granted.Remove(e);
    }

    bool CanGrant(Entry e)
    {
        RoadPath c = e.conn;
        CrosswalkRegistry cw = NpcDirector.Instance.Crosswalks;
        if (c.inCw.IsValid && cw.Occupied(c.inCw)) return false;
        if (c.outCw.IsValid && cw.Occupied(c.outCw)) return false;
        List<Car> outCars = c.next.cars;
        for (int i = 0; i < outCars.Count; i++)
            if (outCars[i].PathS < OutSpace) return false;
        for (int i = 0; i < granted.Count; i++)
        {
            Entry g = granted[i];
            if (g.car == e.car || g.conn == c) continue;
            if (c.conflicts.Contains(g.conn)) return false;
        }
        if (Time.time - e.arrive < Timeout)
        {
            for (int i = 0; i < waiting.Count; i++)
            {
                Entry w = waiting[i];
                if (w == e || w.arrive >= e.arrive) continue;
                if (c.conflicts.Contains(w.conn)) return false;
            }
        }
        return true;
    }
}
