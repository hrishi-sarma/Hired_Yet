using UnityEngine;

public class ZoneMap
{
    public readonly WorldContext context;
    public readonly Vector2 suburbCenter;
    public readonly float suburbRadius;
    public readonly Vector2 cityCenter;
    public readonly float cityRadius;
    public readonly HighwayPath highway;

    public ZoneMap(WorldContext context, Vector2 suburbCenter, float suburbRadius, Vector2 cityCenter, float cityRadius, HighwayPath highway)
    {
        this.context = context;
        this.suburbCenter = suburbCenter;
        this.suburbRadius = suburbRadius;
        this.cityCenter = cityCenter;
        this.cityRadius = cityRadius;
        this.highway = highway;
    }

    public Rect SuburbBounds
    {
        get { return new Rect(suburbCenter.x - suburbRadius, suburbCenter.y - suburbRadius, suburbRadius * 2f, suburbRadius * 2f); }
    }

    public Rect CityBounds
    {
        get { return new Rect(cityCenter.x - cityRadius, cityCenter.y - cityRadius, cityRadius * 2f, cityRadius * 2f); }
    }

    public ZoneType ZoneAt(float x, float z)
    {
        int xi = Mathf.Clamp(Mathf.RoundToInt((x + context.Size * 0.5f) / context.TexelSize), 0, context.Res - 1);
        int zi = Mathf.Clamp(Mathf.RoundToInt((z + context.Size * 0.5f) / context.TexelSize), 0, context.Res - 1);
        return context.zones[zi, xi];
    }

    public ZoneType ZoneAt(Vector2 p)
    {
        return ZoneAt(p.x, p.y);
    }

    public bool Is(float x, float z, ZoneType type)
    {
        return ZoneAt(x, z) == type;
    }

    public bool RectIsAll(Rect r, ZoneType type, float step)
    {
        for (float z = r.yMin; z <= r.yMax; z += step)
            for (float x = r.xMin; x <= r.xMax; x += step)
                if (ZoneAt(x, z) != type) return false;
        return ZoneAt(r.xMax, r.yMax) == type;
    }
}
