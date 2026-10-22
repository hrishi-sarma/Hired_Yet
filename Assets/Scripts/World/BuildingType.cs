using UnityEngine;

[System.Serializable]
public class BuildingType
{
    public string name = "Building";
    public ZoneType zone = ZoneType.Suburb;
    public float weight = 1f;
    [Range(0f, 1f)] public float spawnChance = 0.95f;
    public float minWidth = 8f;
    public float maxWidth = 11f;
    public float minDepth = 8f;
    public float maxDepth = 11f;
    public float minHeight = 4f;
    public float maxHeight = 6f;
    public float frontSetback = 3f;
    public float sideSetback = 1f;
    public float backSetback = 1f;
    public ModelSlot slot = new ModelSlot();

    public static BuildingType Create(string name, ZoneType zone, float weight, Vector2 width, Vector2 depth, Vector2 height, Vector3 setbacks, Color[] tints)
    {
        BuildingType t = new BuildingType();
        t.name = name;
        t.zone = zone;
        t.weight = weight;
        t.minWidth = width.x;
        t.maxWidth = width.y;
        t.minDepth = depth.x;
        t.maxDepth = depth.y;
        t.minHeight = height.x;
        t.maxHeight = height.y;
        t.frontSetback = setbacks.x;
        t.sideSetback = setbacks.y;
        t.backSetback = setbacks.z;
        t.slot = new ModelSlot();
        t.slot.tints = tints;
        return t;
    }
}
