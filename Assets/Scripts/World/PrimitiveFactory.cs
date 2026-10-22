using UnityEngine;

public static class PrimitiveFactory
{
    public static GameObject Create(PrimitiveKind kind, Transform parent)
    {
        PrimitiveType type = PrimitiveType.Cube;
        Vector3 scale = Vector3.one;
        switch (kind)
        {
            case PrimitiveKind.Cylinder:
                type = PrimitiveType.Cylinder;
                scale = new Vector3(1f, 0.5f, 1f);
                break;
            case PrimitiveKind.Sphere:
                type = PrimitiveType.Sphere;
                break;
            case PrimitiveKind.Capsule:
                type = PrimitiveType.Capsule;
                scale = new Vector3(1f, 0.5f, 1f);
                break;
        }
        GameObject go = GameObject.CreatePrimitive(type);
        Collider col = go.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        WorldUtil.Kill(col);
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        return go;
    }
}
