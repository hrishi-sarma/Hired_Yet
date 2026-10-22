using UnityEngine;

[System.Serializable]
public class ModelSlot
{
    public GameObject[] variants = new GameObject[0];
    public PrimitiveKind primitive = PrimitiveKind.Cube;
    public Material material;
    public Color[] tints = new Color[] { Color.white };
    public bool applyMaterialToModels;
    public FitMode fit = FitMode.UniformInside;
    public bool clampToBox = true;
    public Vector3 rotationOffset;
    public Vector3 positionOffset;
    public bool random90;
    public ColliderMode colliders = ColliderMode.Box;
    public bool markStatic = true;

    public static ModelSlot Primitive(PrimitiveKind kind, Color tint)
    {
        ModelSlot s = new ModelSlot();
        s.primitive = kind;
        s.tints = new Color[] { tint };
        return s;
    }

    public GameObject PickVariant(System.Random rng)
    {
        if (variants == null || variants.Length == 0) return null;
        int count = 0;
        for (int i = 0; i < variants.Length; i++)
            if (variants[i] != null) count++;
        if (count == 0) return null;
        int pick = rng.Next(count);
        for (int i = 0; i < variants.Length; i++)
        {
            if (variants[i] == null) continue;
            if (pick == 0) return variants[i];
            pick--;
        }
        return null;
    }

    public ModelSlot WithFit(FitMode mode)
    {
        ModelSlot s = (ModelSlot)MemberwiseClone();
        s.fit = mode;
        return s;
    }
}
