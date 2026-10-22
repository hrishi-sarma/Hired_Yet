using UnityEngine;

public static class SidewalkBuilder
{
    public static int Build(WorldContext c, DistrictSpec s, Vector2 blockCenter, float roadTop, WorldRng rng)
    {
        float b = s.blockSize;
        float w = s.sidewalkWidth;
        float baseY = roadTop - s.carveDepth;
        float h = s.carveDepth + s.curbHeight;
        float o = b * 0.5f + w * 0.5f;
        int placed = 0;
        Vector3 horizontal = new Vector3(w, h, b + 2f * w);
        Vector3 vertical = new Vector3(w, h, b);
        if (RoadPiece.Spawn(c, s.sidewalk, PlacementLayer.Sidewalk, new Vector2(blockCenter.x, blockCenter.y - o), 90f, horizontal, baseY, rng) != null) placed++;
        if (RoadPiece.Spawn(c, s.sidewalk, PlacementLayer.Sidewalk, new Vector2(blockCenter.x, blockCenter.y + o), 90f, horizontal, baseY, rng) != null) placed++;
        if (RoadPiece.Spawn(c, s.sidewalk, PlacementLayer.Sidewalk, new Vector2(blockCenter.x - o, blockCenter.y), 0f, vertical, baseY, rng) != null) placed++;
        if (RoadPiece.Spawn(c, s.sidewalk, PlacementLayer.Sidewalk, new Vector2(blockCenter.x + o, blockCenter.y), 0f, vertical, baseY, rng) != null) placed++;
        return placed;
    }
}
