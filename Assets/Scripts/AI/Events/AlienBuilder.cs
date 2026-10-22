using UnityEngine;

public static class AlienBuilder
{
    static Color H(string hex)
    {
        Color c;
        return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.gray;
    }

    static Transform S(Transform p, string n, PrimitiveKind k, float x, float y, float z, float sx, float sy, float sz, string hex, float rx = 0f, float rz = 0f)
    {
        GameObject g = PrimitiveFactory.Create(k, p);
        g.name = n;
        Collider c = g.GetComponent<Collider>();
        if (c != null) WorldUtil.Kill(c);
        bool tall = k == PrimitiveKind.Cylinder || k == PrimitiveKind.Capsule;
        g.transform.localPosition = new Vector3(x, y, z);
        g.transform.localRotation = Quaternion.Euler(rx, 0f, rz);
        g.transform.localScale = tall ? new Vector3(sx, sy * 0.5f, sz) : new Vector3(sx, sy, sz);
        Renderer r = g.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = MaterialFactory.Get(H(hex));
        return g.transform;
    }

    static void Eye(Transform p, string n, float x, float y, float z, float size, string iris)
    {
        Transform g = new GameObject(n).transform;
        g.SetParent(p, false);
        g.localPosition = new Vector3(x, y, z);
        S(g, "Ball", PrimitiveKind.Sphere, 0f, 0f, 0f, size, size, size, "#F4FFF0");
        S(g, "Iris", PrimitiveKind.Sphere, 0f, 0f, size * 0.38f, size * 0.62f, size * 0.62f, size * 0.3f, iris);
        S(g, "Pupil", PrimitiveKind.Sphere, 0f, 0f, size * 0.5f, size * 0.3f, size * 0.42f, size * 0.12f, "#101018");
    }

    public static GameObject Build(Transform parent)
    {
        GameObject root = new GameObject("Alien");
        root.transform.SetParent(parent, false);
        Transform v = new GameObject("Goo").transform;
        v.SetParent(root.transform, false);

        S(v, "Puddle", PrimitiveKind.Sphere, 0f, 0.04f, 0f, 1.35f, 0.1f, 1.35f, "#2FA85F");
        S(v, "BlobLow", PrimitiveKind.Sphere, 0f, 0.32f, 0f, 0.98f, 0.6f, 0.98f, "#58DB86");
        S(v, "BlobMid", PrimitiveKind.Sphere, 0f, 0.78f, 0f, 0.74f, 0.72f, 0.74f, "#68EA96");
        S(v, "BlobHead", PrimitiveKind.Sphere, 0f, 1.24f, 0f, 0.68f, 0.58f, 0.68f, "#7AF4A6");
        S(v, "BlobDrip", PrimitiveKind.Sphere, 0.42f, 0.14f, 0.22f, 0.2f, 0.26f, 0.2f, "#58DB86");
        S(v, "BlobDrip", PrimitiveKind.Sphere, -0.36f, 0.12f, 0.3f, 0.16f, 0.22f, 0.16f, "#58DB86");
        S(v, "BlobDrip", PrimitiveKind.Sphere, 0.1f, 0.1f, -0.46f, 0.18f, 0.2f, 0.18f, "#4FCB7A");
        S(v, "Shine", PrimitiveKind.Sphere, -0.2f, 0.95f, 0.28f, 0.14f, 0.09f, 0.08f, "#E4FFEE");
        S(v, "Shine", PrimitiveKind.Sphere, 0.24f, 0.5f, 0.4f, 0.12f, 0.07f, 0.07f, "#E4FFEE");
        S(v, "Shine", PrimitiveKind.Sphere, -0.12f, 1.42f, 0.18f, 0.1f, 0.06f, 0.06f, "#F4FFF6");

        Eye(v, "Eye", 0f, 1.3f, 0.27f, 0.3f, "#B45CFF");
        Eye(v, "Eye", 0.27f, 1.12f, 0.24f, 0.12f, "#FFB43C");
        Eye(v, "Eye", -0.3f, 1.06f, 0.2f, 0.09f, "#FF5C8A");
        Eye(v, "Eye", 0.3f, 0.74f, 0.3f, 0.07f, "#3CD0FF");

        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? 1f : -1f;
            Transform stalk = new GameObject("Stalk").transform;
            stalk.SetParent(v, false);
            stalk.localPosition = new Vector3(0.18f * s, 1.46f, 0.02f);
            stalk.localRotation = Quaternion.Euler(0f, 0f, -22f * s);
            S(stalk, "Stem", PrimitiveKind.Cylinder, 0f, 0.2f, 0f, 0.05f, 0.4f, 0.05f, "#5FE890");
            Eye(stalk, "Eye", 0f, 0.44f, 0.02f, i == 0 ? 0.2f : 0.14f, i == 0 ? "#FFE03C" : "#3CFFC8");
        }

        Transform ant = new GameObject("Stalk").transform;
        ant.SetParent(v, false);
        ant.localPosition = new Vector3(0f, 1.5f, -0.1f);
        ant.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        S(ant, "Stem", PrimitiveKind.Cylinder, 0f, 0.28f, 0f, 0.035f, 0.56f, 0.035f, "#5FE890");
        S(ant, "Orb", PrimitiveKind.Sphere, 0f, 0.6f, 0f, 0.14f, 0.14f, 0.14f, "#FFF27A");

        S(v, "Mouth", PrimitiveKind.Sphere, 0f, 1.08f, 0.31f, 0.24f, 0.05f, 0.05f, "#1B5E39");
        S(v, "Tooth", PrimitiveKind.Cube, -0.04f, 1.1f, 0.33f, 0.025f, 0.035f, 0.02f, "#F4FFF0");
        S(v, "Tooth", PrimitiveKind.Cube, 0.06f, 1.1f, 0.33f, 0.025f, 0.03f, 0.02f, "#F4FFF0");

        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? 1f : -1f;
            Transform arm = new GameObject("Arm").transform;
            arm.SetParent(v, false);
            arm.localPosition = new Vector3(0.36f * s, 0.9f, 0.05f);
            S(arm, "BlobArm", PrimitiveKind.Sphere, 0.08f * s, -0.02f, 0f, 0.22f, 0.22f, 0.22f, "#68EA96");
            S(arm, "BlobArm", PrimitiveKind.Sphere, 0.2f * s, -0.18f, 0.06f, 0.17f, 0.17f, 0.17f, "#68EA96");
            S(arm, "BlobArm", PrimitiveKind.Sphere, 0.26f * s, -0.36f, 0.12f, 0.13f, 0.15f, 0.13f, "#7AF4A6");
        }

        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.direction = 1;
        col.radius = 0.4f;
        col.height = 1.5f;
        col.center = new Vector3(0f, 0.75f, 0f);
        root.AddComponent<AlienWobble>();
        return root;
    }
}

public class AlienWobble : MonoBehaviour
{
    Transform[] blobs, stalks;
    Vector3[] blobBase;
    Quaternion[] stalkBase;
    float[] phase;

    void Awake()
    {
        System.Collections.Generic.List<Transform> b = new System.Collections.Generic.List<Transform>();
        System.Collections.Generic.List<Transform> s = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Blob")) b.Add(t);
            else if (t.name == "Stalk") s.Add(t);
        }
        blobs = b.ToArray();
        stalks = s.ToArray();
        blobBase = new Vector3[blobs.Length];
        phase = new float[blobs.Length];
        for (int i = 0; i < blobs.Length; i++) { blobBase[i] = blobs[i].localScale; phase[i] = i * 0.9f; }
        stalkBase = new Quaternion[stalks.Length];
        for (int i = 0; i < stalks.Length; i++) stalkBase[i] = stalks[i].localRotation;
    }

    void Update()
    {
        float t = Time.time;
        for (int i = 0; i < blobs.Length; i++)
        {
            float w = Mathf.Sin(t * 2.2f + phase[i]) * 0.07f;
            blobs[i].localScale = new Vector3(blobBase[i].x * (1f - w * 0.6f), blobBase[i].y * (1f + w), blobBase[i].z * (1f - w * 0.6f));
        }
        for (int i = 0; i < stalks.Length; i++)
            stalks[i].localRotation = stalkBase[i] * Quaternion.Euler(Mathf.Sin(t * 2.6f + i * 1.7f) * 9f, 0f, Mathf.Sin(t * 1.9f + i) * 11f);
    }
}
