using UnityEngine;

[System.Serializable]
public class DistrictSpec
{
    public float roadWidth = 8f;
    public float sidewalkWidth = 2f;
    public float blockSize = 36f;
    public float roadThickness = 0.3f;
    public float curbHeight = 0.15f;
    public float carveDepth = 0.15f;
    public float flattenBlend = 30f;
    public ModelSlot intersection = ModelSlot.Primitive(PrimitiveKind.Cube, new Color(0.2f, 0.2f, 0.22f));
    public ModelSlot straight = ModelSlot.Primitive(PrimitiveKind.Cube, new Color(0.2f, 0.2f, 0.22f));
    public ModelSlot sidewalk = ModelSlot.Primitive(PrimitiveKind.Cube, new Color(0.65f, 0.65f, 0.62f));

    public float Pitch
    {
        get { return roadWidth + 2f * sidewalkWidth + blockSize; }
    }

    public static DistrictSpec Create(float road, float walk, float block)
    {
        DistrictSpec s = new DistrictSpec();
        s.roadWidth = road;
        s.sidewalkWidth = walk;
        s.blockSize = block;
        return s;
    }
}

[System.Serializable]
public class HighwaySpec
{
    public float width = 12f;
    public float shoulder = 2f;
    public float blend = 20f;
    public float carveDepth = 0.2f;
    public float gateLength = 16f;
    [Range(0, 300)] public int smoothPasses = 80;
    [Range(0, 4)] public int chaikinIterations = 2;
    public float sampleSpacing = 8f;
    public Color tint = new Color(0.17f, 0.17f, 0.19f);
    public Material material;
}
