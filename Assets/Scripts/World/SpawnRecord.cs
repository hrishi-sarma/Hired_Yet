using UnityEngine;

public struct OrientedBox
{
    public readonly Vector2 center;
    public readonly Vector2 half;
    public readonly float yawDeg;
    public readonly float minY;
    public readonly float maxY;
    public readonly Vector2 axisX;
    public readonly Vector2 axisZ;

    public OrientedBox(Vector2 center, Vector2 half, float yawDeg, float minY, float maxY)
    {
        this.center = center;
        this.half = half;
        this.yawDeg = yawDeg;
        this.minY = minY;
        this.maxY = maxY;
        float r = yawDeg * Mathf.Deg2Rad;
        float cs = Mathf.Cos(r);
        float sn = Mathf.Sin(r);
        axisX = new Vector2(cs, -sn);
        axisZ = new Vector2(sn, cs);
    }

    public static OrientedBox FromBase(Vector3 baseCenter, Vector3 size, float yawDeg)
    {
        return new OrientedBox(new Vector2(baseCenter.x, baseCenter.z), new Vector2(size.x * 0.5f, size.z * 0.5f), yawDeg, baseCenter.y, baseCenter.y + size.y);
    }

    public OrientedBox Expanded(float amount)
    {
        return new OrientedBox(center, half + new Vector2(amount, amount), yawDeg, minY, maxY);
    }

    public Rect Aabb
    {
        get
        {
            float ex = Mathf.Abs(axisX.x) * half.x + Mathf.Abs(axisZ.x) * half.y;
            float ez = Mathf.Abs(axisX.y) * half.x + Mathf.Abs(axisZ.y) * half.y;
            return new Rect(center.x - ex, center.y - ez, ex * 2f, ez * 2f);
        }
    }

    public void Corners(Vector2[] c)
    {
        Vector2 ax = axisX * half.x;
        Vector2 az = axisZ * half.y;
        c[0] = center - ax - az;
        c[1] = center + ax - az;
        c[2] = center + ax + az;
        c[3] = center - ax + az;
    }

    public static bool Overlap(OrientedBox a, OrientedBox b, float eps)
    {
        Vector2 d = b.center - a.center;
        if (Separated(a.axisX, a, b, d, eps)) return false;
        if (Separated(a.axisZ, a, b, d, eps)) return false;
        if (Separated(b.axisX, a, b, d, eps)) return false;
        if (Separated(b.axisZ, a, b, d, eps)) return false;
        return true;
    }

    static bool Separated(Vector2 u, OrientedBox a, OrientedBox b, Vector2 d, float eps)
    {
        float ra = Mathf.Abs(Vector2.Dot(u, a.axisX)) * a.half.x + Mathf.Abs(Vector2.Dot(u, a.axisZ)) * a.half.y;
        float rb = Mathf.Abs(Vector2.Dot(u, b.axisX)) * b.half.x + Mathf.Abs(Vector2.Dot(u, b.axisZ)) * b.half.y;
        return Mathf.Abs(Vector2.Dot(u, d)) >= ra + rb - eps;
    }
}

public class SpawnRecord
{
    public OrientedBox box;
    public PlacementLayer layer;
    public GameObject owner;
    public int stamp;

    public SpawnRecord(OrientedBox box, PlacementLayer layer, GameObject owner)
    {
        this.box = box;
        this.layer = layer;
        this.owner = owner;
    }
}
