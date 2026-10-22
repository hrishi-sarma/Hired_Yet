using UnityEngine;

[System.Serializable]
public class WorldSettings
{
    public int seed = 12345;

    [Tooltip("Square world size in meters.")]
    public float worldSize = 1024f;

    [Tooltip("129, 257, 513 or 1025. 513 is recommended for WebGL.")]
    public int heightmapResolution = 513;

    public float maxTerrainHeight = 160f;

    [Tooltip("Bucket size of the overlap grid in meters.")]
    public float spatialCellSize = 8f;

    [Tooltip("URP Lit material used for every primitive without its own material. Assign it so the shader is included in WebGL builds.")]
    public Material defaultMaterial;

    public float Half
    {
        get { return worldSize * 0.5f; }
    }
}
