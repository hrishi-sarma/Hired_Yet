using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared "is a moving car heading for me?" query used by animals and birds.</summary>
public static class AnimalCarSense
{
    public struct Threat
    {
        public Car car;
        public float ahead;     // metres from the car's front, along its heading, to the animal
        public float lateral;   // signed sideways offset from the car's line (+ = right of travel)
        public float ttc;       // seconds until the car reaches the animal at its current speed
    }

    static readonly List<IAgent> buf = new List<IAgent>(8);

    /// <summary>Nearest-in-time moving car whose path (corridor of +-halfWidth) passes through pos.</summary>
    public static bool Find(NpcDirector d, Vector3 pos, float halfWidth, out Threat best)
    {
        best = new Threat();
        best.ttc = float.MaxValue;
        if (d == null || d.Registry == null) return false;
        Vector2 p = new Vector2(pos.x, pos.z);
        d.Registry.Query(p, 48f, buf);
        bool found = false;
        for (int i = 0; i < buf.Count; i++)
        {
            Car c = buf[i] as Car;
            if (c == null || c.Pooled || c.Speed < 1.2f) continue;
            Vector2 h = c.Heading;
            Vector2 rel = p - c.Pos2D;
            float ahead = Vector2.Dot(rel, h);
            float range = 8f + c.Speed * 3.2f;
            if (ahead < -1f || ahead > range) continue;
            float lat = rel.x * h.y - rel.y * h.x;
            if (Mathf.Abs(lat) > halfWidth) continue;
            float ttc = Mathf.Max(0f, ahead) / c.Speed;
            if (ttc < best.ttc)
            {
                best.car = c;
                best.ahead = ahead;
                best.lateral = lat;
                best.ttc = ttc;
                found = true;
            }
        }
        return found;
    }
}
