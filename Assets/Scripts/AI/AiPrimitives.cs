using UnityEngine;

public static class AiPrimitives
{
    public const float PedestrianRadius = 0.25f;
    public static readonly Vector3 CarSize = new Vector3(1.8f, 1.4f, 4.2f);

    static readonly Color[] Shirts =
    {
        new Color(0.78f, 0.25f, 0.22f), new Color(0.2f, 0.4f, 0.75f), new Color(0.25f, 0.6f, 0.35f),
        new Color(0.9f, 0.75f, 0.25f), new Color(0.55f, 0.3f, 0.65f), new Color(0.9f, 0.9f, 0.88f),
        new Color(0.2f, 0.2f, 0.24f), new Color(0.85f, 0.5f, 0.2f), new Color(0.35f, 0.65f, 0.7f),
        new Color(0.6f, 0.45f, 0.35f)
    };

    static readonly Color[] Skins =
    {
        new Color(0.96f, 0.80f, 0.69f), new Color(0.87f, 0.68f, 0.52f), new Color(0.72f, 0.52f, 0.37f),
        new Color(0.55f, 0.38f, 0.26f), new Color(0.38f, 0.26f, 0.18f), new Color(0.92f, 0.74f, 0.6f)
    };

    static readonly Color[] CarColors =
    {
        new Color(0.75f, 0.12f, 0.12f), new Color(0.15f, 0.3f, 0.65f), new Color(0.85f, 0.85f, 0.85f),
        new Color(0.12f, 0.12f, 0.14f), new Color(0.55f, 0.57f, 0.6f), new Color(0.9f, 0.75f, 0.15f),
        new Color(0.2f, 0.5f, 0.3f), new Color(0.9f, 0.45f, 0.1f)
    };

    static readonly Color Glass = new Color(0.12f, 0.16f, 0.2f);
    static readonly Color Tire = new Color(0.07f, 0.07f, 0.08f);

    public static Color RandomShirt(System.Random rng)
    {
        return Shirts[rng.Next(Shirts.Length)];
    }

    public static Color RandomSkin(System.Random rng)
    {
        return Skins[rng.Next(Skins.Length)];
    }

    public static Color RandomCar(System.Random rng)
    {
        return CarColors[rng.Next(CarColors.Length)];
    }

    static void Paint(GameObject go, Color color)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = MaterialFactory.Get(color);
    }

    static void Paint(Transform t, Color color)
    {
        if (t != null) Paint(t.gameObject, color);
    }

    public static GameObject BuildPedestrian(Color shirt, Color skin, float height, Transform parent)
    {
        GameObject root = new GameObject("Pedestrian");
        root.transform.SetParent(parent, false);
        float bodyH = Mathf.Max(0.6f, height - 0.24f);

        GameObject body = PrimitiveFactory.Create(PrimitiveKind.Capsule, root.transform);
        body.name = "Body";
        body.transform.localScale = new Vector3(PedestrianRadius * 2f, bodyH * 0.5f, PedestrianRadius * 2f);
        body.transform.localPosition = new Vector3(0f, bodyH * 0.5f, 0f);
        Paint(body, shirt);

        GameObject head = PrimitiveFactory.Create(PrimitiveKind.Sphere, root.transform);
        head.name = "Head";
        head.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
        head.transform.localPosition = new Vector3(0f, bodyH + 0.1f, 0f);
        Paint(head, skin);

        GameObject nose = PrimitiveFactory.Create(PrimitiveKind.Cube, root.transform);
        nose.name = "Nose";
        nose.transform.localScale = new Vector3(0.06f, 0.06f, 0.1f);
        nose.transform.localPosition = new Vector3(0f, bodyH + 0.1f, 0.12f);
        Paint(nose, skin * 0.8f);

        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.direction = 1;
        col.radius = PedestrianRadius;
        col.height = height;
        col.center = new Vector3(0f, height * 0.5f, 0f);
        return root;
    }

    public static void RecolorPedestrian(Transform root, Color shirt, Color skin)
    {
        Paint(root.Find("Body"), shirt);
        Paint(root.Find("Head"), skin);
        Paint(root.Find("Nose"), skin * 0.8f);
    }

    public static GameObject BuildCar(Color bodyColor, Transform parent)
    {
        GameObject root = new GameObject("Car");
        root.transform.SetParent(parent, false);

        GameObject chassis = PrimitiveFactory.Create(PrimitiveKind.Cube, root.transform);
        chassis.name = "Body";
        chassis.transform.localScale = new Vector3(1.8f, 0.6f, 4.2f);
        chassis.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        Paint(chassis, bodyColor);

        GameObject cabin = PrimitiveFactory.Create(PrimitiveKind.Cube, root.transform);
        cabin.name = "Cabin";
        cabin.transform.localScale = new Vector3(1.6f, 0.6f, 2.1f);
        cabin.transform.localPosition = new Vector3(0f, 1.1f, -0.3f);
        Paint(cabin, Glass);

        for (int i = 0; i < 4; i++)
        {
            GameObject wheel = PrimitiveFactory.Create(PrimitiveKind.Cylinder, root.transform);
            wheel.name = "Wheel";
            wheel.transform.localScale = new Vector3(0.6f, 0.125f, 0.6f);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            float x = (i & 1) == 0 ? -0.8f : 0.8f;
            float z = (i & 2) == 0 ? -1.35f : 1.35f;
            wheel.transform.localPosition = new Vector3(x, 0.3f, z);
            Paint(wheel, Tire);
        }

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.size = CarSize;
        col.center = new Vector3(0f, CarSize.y * 0.5f, 0f);
        return root;
    }

    public static void RecolorCar(Transform root, Color bodyColor)
    {
        Paint(root.Find("Body"), bodyColor);
    }

    public static GameObject BuildItem(ItemDef def, Transform parent)
    {
        GameObject root = new GameObject("Item_" + def.id);
        root.transform.SetParent(parent, false);
        GameObject body = PrimitiveFactory.Create(def.primitive, root.transform);
        body.name = "Body";
        body.transform.localScale = def.size;
        body.transform.localPosition = new Vector3(0f, def.size.y * 0.5f, 0f);
        Paint(body, def.DisplayColor);
        return root;
    }
}
