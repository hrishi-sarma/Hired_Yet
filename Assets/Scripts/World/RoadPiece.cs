using System.Collections.Generic;
using UnityEngine;

public struct RoadLine
{
    public Vector2 a;
    public Vector2 b;
    public float width;
    public float roadTop;
}

public class BlockInfo
{
    public ZoneType zone;
    public Vector2 center;
    public Rect lotRect;
    public float roadTop;
    public float sidewalkTop;
    public float sidewalkWidth;
    public int ix;
    public int iz;
}

public class RoadNetwork
{
    public readonly List<BlockInfo> blocks = new List<BlockInfo>();
    public readonly List<RoadLine> lines = new List<RoadLine>();
    public readonly List<Rect> footprints = new List<Rect>();
    public HighwayPath highway;
    public float[] highwayHeights;
    public int roadPieces;
    public int sidewalkPieces;
    public int rejected;

    public bool InFootprint(Vector2 p, float margin)
    {
        for (int i = 0; i < footprints.Count; i++)
        {
            Rect r = footprints[i];
            if (p.x > r.xMin - margin && p.x < r.xMax + margin && p.y > r.yMin - margin && p.y < r.yMax + margin) return true;
        }
        return false;
    }
}

public static class RoadPiece
{
    public static GameObject Spawn(WorldContext c, ModelSlot slot, PlacementLayer layer, Vector2 center, float yawDeg, Vector3 size, float baseY, WorldRng rng)
    {
        Vector3 baseCenter = new Vector3(center.x, baseY, center.y);
        OrientedBox box = OrientedBox.FromBase(baseCenter, size, yawDeg);
        if (!c.occupancy.IsFree(box, layer, 0f)) return null;
        GameObject go = ModelFitter.Spawn(slot, baseCenter, yawDeg, size, c.LayerRoot(layer), rng.Raw);
        if (layer == PlacementLayer.Road) StylizedMaterialFactory.ApplyUniformRole(go.transform, "asphalt");
        else if (layer == PlacementLayer.Sidewalk) StylizedMaterialFactory.ApplyUniformRole(go.transform, "concrete");
        // Lot pads are large, adjacent grass-base planes. Give every pad the same
        // canonical ground material so neighbouring lots cannot change hue at seams.
        // World-space UV projection then keeps the texture phase continuous across them.
        else if (layer == PlacementLayer.Lot) StylizedMaterialFactory.ApplyUniformRole(go.transform, "ground");
        c.occupancy.Add(box, layer, go);
        return go;
    }
}
