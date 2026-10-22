using UnityEngine;

public enum AnimalSpecies { Deer, Rabbit, Cat, Dog }

/// <summary>Transform handles used by the procedural animation controller in AnimalAgent.</summary>
public sealed class AnimalRig
{
    public Transform visual;                 // parent of every mesh part; moved/tilted by the animator
    public Transform body;
    public Transform neck;                   // deer only (null otherwise)
    public Transform head;
    public Transform tail;
    public Transform[] legs = new Transform[4];   // hip pivots: 0 FL, 1 FR, 2 BL, 3 BR
    public Transform[] feet = new Transform[4];   // marker at the very bottom of each paw
    public float[] legLength = new float[4];      // hip pivot height == leg length at scale 1
    public Transform[] ears = new Transform[2];
    public Transform[] eyes = new Transform[2];
    public Transform[] tailSegments = new Transform[0];
    public Quaternion[] tailSegmentHomeRotations = new Quaternion[0];
    public Transform[] contacts = new Transform[0]; // points on the underside of the body, used to rest it on the ground
    public Transform tongue;                  // dog tongue (scaled 0..1 to pant)
    public float sizeMul = 1f;                // per-breed size multiplier applied on top of the species scale
    public Transform tuft;                    // rabbit grass tuft (root-parented, toggled while eating)
    public Quaternion neckHome = Quaternion.identity;
    public Quaternion headHome = Quaternion.identity;
    public Quaternion tailHome = Quaternion.identity;
    public Quaternion[] earHome = new Quaternion[2];
}

/// <summary>
/// Simple, soft, toy-like animals built from a few overlapping primitives.
/// Forward is local +Z, ground is local y = 0. Every leg pivot sits INSIDE the body and the
/// leg capsule reaches exactly y = 0, so legs are always attached and paws always touch the
/// ground. AnimalAgent additionally rests the lowest contact point on the ground every frame.
/// </summary>
public static class AnimalModelFactory
{
    static readonly Color Eye = new Color(0.06f, 0.04f, 0.03f);
    static System.Collections.Generic.List<Transform> contactList;

    public static AnimalRig Build(AnimalSpecies species, Transform root, System.Random rng)
    {
        AnimalRig rig = new AnimalRig();
        contactList = new System.Collections.Generic.List<Transform>();
        rig.visual = Empty("Visual", root, Vector3.zero);
        if (species == AnimalSpecies.Deer) BuildDeer(rig, root);
        else if (species == AnimalSpecies.Rabbit) BuildRabbit(rig, root);
        else if (species == AnimalSpecies.Dog) BuildDog(rig, root, rng);
        else BuildCat(rig, root, rng);
        rig.contacts = contactList.ToArray();
        Capture(rig);
        return rig;
    }

    // ------------------------------------------------------------------ deer
    static void BuildDeer(AnimalRig rig, Transform root)
    {
        Transform v = rig.visual;
        Color coat = new Color(0.60f, 0.36f, 0.19f), light = new Color(0.93f, 0.80f, 0.60f);
        Color dark = new Color(0.28f, 0.17f, 0.11f), legC = new Color(0.50f, 0.31f, 0.17f);
        Color spot = new Color(0.97f, 0.90f, 0.75f), horn = new Color(0.42f, 0.28f, 0.17f);

        Vector3 bc = new Vector3(0f, 0.54f, -0.03f), br = new Vector3(0.44f, 0.38f, 0.66f);
        rig.body = Part(PrimitiveKind.Sphere, "Body", v, bc, br, coat, 0);
        Part(PrimitiveKind.Sphere, "Belly", v, new Vector3(0f, 0.47f, 0.02f), new Vector3(0.34f, 0.24f, 0.50f), light, 0);
        Contacts(v, bc, br);

        // fawn spots lying on the back
        float[] spotAng = { 35f, -30f, 40f, -35f, 10f };
        float[] spotZ = { 0.12f, -0.02f, -0.18f, 0.20f, -0.30f };
        for (int i = 0; i < spotAng.Length; i++)
        {
            float a = spotAng[i] * Mathf.Deg2Rad;
            Vector3 n = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f);
            float ratio = Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Pow(spotZ[i] / (br.z * 0.5f), 2f)));
            Vector3 p = new Vector3(bc.x + n.x * br.x * 0.5f * ratio * 0.98f, bc.y + n.y * br.y * 0.5f * ratio * 0.98f, bc.z + spotZ[i]);
            Transform s = Part(PrimitiveKind.Sphere, "Spot", v, p, new Vector3(0.07f, 0.025f, 0.07f), spot, -1);
            s.localRotation = Quaternion.FromToRotation(Vector3.up, n);
        }

        rig.neck = Empty("NeckPivot", v, new Vector3(0f, 0.60f, 0.27f));
        rig.neck.localRotation = Quaternion.Euler(28f, 0f, 0f);
        Part(PrimitiveKind.Capsule, "Neck", rig.neck, new Vector3(0f, 0.14f, 0f), new Vector3(0.17f, 0.21f, 0.17f), coat, 0);

        rig.head = Empty("HeadPivot", rig.neck, new Vector3(0f, 0.30f, 0f));
        rig.head.localRotation = Quaternion.Euler(-28f, 0f, 0f);
        Transform h = rig.head;
        Part(PrimitiveKind.Sphere, "Head", h, new Vector3(0f, 0f, 0.03f), new Vector3(0.30f, 0.27f, 0.30f), coat, 0);
        Part(PrimitiveKind.Sphere, "Muzzle", h, new Vector3(0f, -0.065f, 0.165f), new Vector3(0.16f, 0.12f, 0.15f), light, 0);
        Part(PrimitiveKind.Sphere, "Nose", h, new Vector3(0f, -0.045f, 0.235f), new Vector3(0.06f, 0.045f, 0.04f), dark, -1);
        rig.eyes[0] = MakeEye(h, "EyeL", new Vector3(-0.105f, 0.045f, 0.122f), new Vector3(0.058f, 0.068f, 0.032f), Eye);
        rig.eyes[1] = MakeEye(h, "EyeR", new Vector3(0.105f, 0.045f, 0.122f), new Vector3(0.058f, 0.068f, 0.032f), Eye);
        for (int k = 0; k < 2; k++)
        {
            float s = k == 0 ? -1f : 1f;
            Transform ep = Empty(k == 0 ? "EarPivotL" : "EarPivotR", h, new Vector3(s * 0.10f, 0.10f, -0.03f));
            ep.localRotation = Quaternion.Euler(0f, 0f, -s * 55f);
            rig.ears[k] = ep;
            Part(PrimitiveKind.Capsule, "Ear", ep, new Vector3(0f, 0.075f, 0f), new Vector3(0.08f, 0.09f, 0.04f), coat, 0);
            Part(PrimitiveKind.Capsule, "EarInner", ep, new Vector3(0f, 0.08f, 0.012f), new Vector3(0.045f, 0.065f, 0.025f), light, -1);
            Transform ap = Empty("Antler", h, new Vector3(s * 0.06f, 0.12f, -0.06f));
            ap.localRotation = Quaternion.Euler(-10f, 0f, -s * 18f);
            Part(PrimitiveKind.Capsule, "AntlerMain", ap, new Vector3(0f, 0.10f, 0f), new Vector3(0.032f, 0.10f, 0.032f), horn, -1);
            Part(PrimitiveKind.Capsule, "AntlerTine", ap, new Vector3(0f, 0.13f, 0.03f), new Vector3(0.024f, 0.045f, 0.024f), horn, -1)
                .localRotation = Quaternion.Euler(45f, 0f, 0f);
        }

        rig.tail = Empty("TailPivot", v, new Vector3(0f, 0.62f, -0.33f));
        Part(PrimitiveKind.Sphere, "Tail", rig.tail, new Vector3(0f, 0f, -0.06f), new Vector3(0.12f, 0.15f, 0.11f), light, 0);

        Vector3[] hips = { new Vector3(-0.15f, 0.47f, 0.20f), new Vector3(0.15f, 0.47f, 0.20f), new Vector3(-0.15f, 0.47f, -0.20f), new Vector3(0.15f, 0.47f, -0.20f) };
        for (int i = 0; i < 4; i++) Leg(rig, v, i, hips[i], 0.47f, 0.10f, 0.12f, legC, dark, 1.35f, 0.7f, 1.5f);
    }

    // ---------------------------------------------------------------- rabbit
    static void BuildRabbit(AnimalRig rig, Transform root)
    {
        Transform v = rig.visual;
        Color coat = new Color(0.84f, 0.76f, 0.64f), white = new Color(0.97f, 0.94f, 0.88f);
        Color pink = new Color(0.93f, 0.52f, 0.58f);

        Vector3 bc = new Vector3(0f, 0.27f, -0.02f), br = new Vector3(0.36f, 0.32f, 0.46f);
        rig.body = Part(PrimitiveKind.Sphere, "Body", v, bc, br, coat, 1);
        Part(PrimitiveKind.Sphere, "Chest", v, new Vector3(0f, 0.27f, 0.13f), new Vector3(0.28f, 0.26f, 0.26f), white, 1);
        Contacts(v, bc, br);

        rig.head = Empty("HeadPivot", v, new Vector3(0f, 0.38f, 0.19f));
        Transform h = rig.head;
        Part(PrimitiveKind.Sphere, "Head", h, new Vector3(0f, 0.03f, 0.04f), new Vector3(0.28f, 0.25f, 0.26f), coat, 1);
        Part(PrimitiveKind.Sphere, "CheekL", h, new Vector3(-0.05f, -0.04f, 0.11f), new Vector3(0.10f, 0.085f, 0.10f), white, 1);
        Part(PrimitiveKind.Sphere, "CheekR", h, new Vector3(0.05f, -0.04f, 0.11f), new Vector3(0.10f, 0.085f, 0.10f), white, 1);
        Part(PrimitiveKind.Sphere, "Nose", h, new Vector3(0f, -0.005f, 0.172f), new Vector3(0.045f, 0.032f, 0.03f), pink, -1);
        rig.eyes[0] = MakeEye(h, "EyeL", new Vector3(-0.095f, 0.07f, 0.125f), new Vector3(0.048f, 0.058f, 0.03f), Eye);
        rig.eyes[1] = MakeEye(h, "EyeR", new Vector3(0.095f, 0.07f, 0.125f), new Vector3(0.048f, 0.058f, 0.03f), Eye);
        for (int k = 0; k < 2; k++)
        {
            float s = k == 0 ? -1f : 1f;
            Transform ep = Empty(k == 0 ? "EarPivotL" : "EarPivotR", h, new Vector3(s * 0.055f, 0.12f, 0f));
            ep.localRotation = Quaternion.Euler(-6f, 0f, -s * 9f);
            rig.ears[k] = ep;
            Part(PrimitiveKind.Capsule, "Ear", ep, new Vector3(0f, 0.12f, 0f), new Vector3(0.07f, 0.13f, 0.035f), coat, 1);
            Part(PrimitiveKind.Capsule, "EarInner", ep, new Vector3(0f, 0.125f, 0.014f), new Vector3(0.036f, 0.09f, 0.02f), pink, -1);
        }

        rig.tail = Empty("TailPivot", v, new Vector3(0f, 0.27f, -0.22f));
        Part(PrimitiveKind.Sphere, "Tail", rig.tail, new Vector3(0f, 0f, -0.03f), new Vector3(0.12f, 0.12f, 0.12f), white, 1);

        Vector3[] hips = { new Vector3(-0.11f, 0.21f, 0.14f), new Vector3(0.11f, 0.21f, 0.14f), new Vector3(-0.15f, 0.22f, -0.14f), new Vector3(0.15f, 0.22f, -0.14f) };
        for (int i = 0; i < 4; i++)
        {
            if (i < 2) Leg(rig, v, i, hips[i], 0.21f, 0.07f, 0.12f, coat, white, 1.35f, 0.7f, 1.8f);
            else
            {
                Leg(rig, v, i, hips[i], 0.22f, 0.075f, 0.12f, coat, white, 1.5f, 0.7f, 2.6f);
                Part(PrimitiveKind.Sphere, "Haunch", rig.legs[i], Vector3.zero, new Vector3(0.17f, 0.20f, 0.26f), coat, 1);
            }
        }

        // grass tuft shown at ground level in front of the rabbit while it eats
        rig.tuft = Empty("GrassTuft", root, new Vector3(0f, 0f, 0.31f));
        Color g1 = new Color(0.30f, 0.68f, 0.24f), g2 = new Color(0.40f, 0.78f, 0.28f);
        float[] gx = { -0.05f, 0f, 0.05f };
        float[] gz = { 0.0f, 0.03f, -0.01f };
        float[] gr = { -14f, 0f, 14f };
        for (int i = 0; i < 3; i++)
        {
            Transform b = Part(PrimitiveKind.Capsule, "Blade", rig.tuft, new Vector3(gx[i], 0.11f, gz[i]), new Vector3(0.035f, 0.12f, 0.035f), i == 1 ? g2 : g1, -1);
            b.localRotation = Quaternion.Euler(0f, 0f, gr[i]);
        }
        rig.tuft.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------- dog
    // Eight breeds picked at random: Labrador, Golden retriever, Husky, Beagle, Corgi,
    // German shepherd, Dalmatian, Pug.
    static void BuildDog(AnimalRig rig, Transform root, System.Random rng)
    {
        Transform v = rig.visual;
        int breed = rng.Next(8);
        Color coat, light, accent, legC, pawC, eyeC = new Color(0.22f, 0.13f, 0.07f), mask = Color.clear;
        float L = 0.34f, bodyL = 1f, bodyW = 1f, snout = 1f, earS = 1f;
        int ears = 0;      // 0 floppy, 1 pointy
        int tailType = 0;  // 0 straight, 1 curled over back, 2 stub, 3 feathered
        bool ruff = false, dalmatian = false, saddle = false, beagle = false, whiteTip = false;
        switch (breed)
        {
            case 0: // Labrador
                coat = new Color(0.88f, 0.72f, 0.42f); light = new Color(0.94f, 0.82f, 0.60f); accent = new Color(0.78f, 0.60f, 0.32f); tailType = 0; break;
            case 1: // Golden retriever
                coat = new Color(0.86f, 0.56f, 0.22f); light = new Color(0.95f, 0.76f, 0.46f); accent = new Color(0.76f, 0.46f, 0.16f); tailType = 3; ruff = true; rig.sizeMul = 1.05f; break;
            case 2: // Husky
                coat = new Color(0.50f, 0.52f, 0.57f); light = new Color(0.96f, 0.96f, 0.97f); accent = new Color(0.34f, 0.36f, 0.40f);
                ears = 1; tailType = 1; eyeC = new Color(0.40f, 0.68f, 0.95f); L = 0.36f; mask = new Color(0.96f, 0.96f, 0.97f); ruff = true; rig.sizeMul = 1.05f; break;
            case 3: // Beagle
                coat = new Color(0.80f, 0.54f, 0.26f); light = new Color(0.97f, 0.95f, 0.90f); accent = new Color(0.34f, 0.21f, 0.12f);
                tailType = 0; L = 0.28f; beagle = true; whiteTip = true; earS = 1.25f; rig.sizeMul = 0.88f; break;
            case 4: // Corgi
                coat = new Color(0.92f, 0.57f, 0.24f); light = new Color(0.98f, 0.95f, 0.90f); accent = coat;
                ears = 1; earS = 1.35f; tailType = 2; L = 0.22f; bodyL = 1.15f; rig.sizeMul = 0.92f; break;
            case 5: // German shepherd
                coat = new Color(0.80f, 0.56f, 0.28f); light = new Color(0.88f, 0.72f, 0.48f); accent = new Color(0.16f, 0.12f, 0.10f);
                ears = 1; earS = 1.3f; tailType = 3; L = 0.38f; snout = 1.25f; saddle = true; rig.sizeMul = 1.2f; break;
            case 6: // Dalmatian
                coat = new Color(0.97f, 0.97f, 0.95f); light = coat; accent = new Color(0.12f, 0.11f, 0.11f);
                tailType = 0; L = 0.38f; dalmatian = true; bodyW = 0.92f; rig.sizeMul = 1.1f; break;
            default: // Pug
                coat = new Color(0.86f, 0.72f, 0.50f); light = coat; accent = new Color(0.20f, 0.15f, 0.13f);
                tailType = 1; L = 0.22f; snout = 0.45f; bodyL = 0.85f; bodyW = 1.12f; earS = 0.75f; mask = new Color(0.20f, 0.15f, 0.13f); rig.sizeMul = 0.8f; break;
        }
        legC = (breed == 4 || breed == 3) ? light : coat;
        pawC = (breed == 4 || breed == 3 || breed == 2) ? light : coat;
        Color nose = new Color(0.08f, 0.06f, 0.06f), pink = new Color(0.93f, 0.50f, 0.56f);

        Vector3 bc = new Vector3(0f, L, -0.02f), br = new Vector3(0.30f * bodyW, 0.30f, 0.56f * bodyL);
        rig.body = Part(PrimitiveKind.Sphere, "Body", v, bc, br, coat, 1);
        Part(PrimitiveKind.Sphere, "Chest", v, new Vector3(0f, L + 0.01f, 0.17f * bodyL), new Vector3(0.27f * bodyW, 0.27f, 0.26f), light, 1);
        Contacts(v, bc, br);

        if (dalmatian)
        {
            float[] z = { -0.20f, -0.12f, -0.04f, 0.04f, 0.12f, -0.16f, -0.08f, 0f, 0.08f, 0.16f, -0.22f, 0.02f };
            float[] a = { -40f, 15f, -20f, 35f, -12f, 30f, -48f, 48f, -35f, 20f, 10f, -60f };
            for (int i = 0; i < z.Length; i++)
                SurfaceMark(v, bc, br, z[i], a[i], new Vector3(0.06f + 0.015f * (i % 3), 0.02f, 0.06f), accent);
        }
        if (saddle || beagle)
        {
            SurfaceMark(v, bc, br, -0.04f, 0f, new Vector3(0.20f * bodyW, 0.025f, 0.32f), accent);
            SurfaceMark(v, bc, br, -0.04f, 42f, new Vector3(0.12f, 0.025f, 0.26f), accent);
            SurfaceMark(v, bc, br, -0.04f, -42f, new Vector3(0.12f, 0.025f, 0.26f), accent);
        }

        float sz = 0.14f * snout + 0.04f;           // muzzle length
        float mz = 0.115f + 0.045f * snout;         // muzzle centre z
        Color muzzleC = (saddle || mask.a > 0f) ? (mask.a > 0f ? mask : accent) : light;
        rig.head = Empty("HeadPivot", v, new Vector3(0f, L + 0.14f, 0.30f * bodyL));
        Transform h = rig.head;
        Part(PrimitiveKind.Sphere, "Head", h, new Vector3(0f, 0.02f, 0.04f), new Vector3(0.26f, 0.235f, 0.25f), coat, 1);
        if (ruff) Part(PrimitiveKind.Sphere, "Ruff", h, new Vector3(0f, -0.06f, -0.03f), new Vector3(0.30f, 0.22f, 0.24f), light, 1);
        if (mask.a > 0f && breed == 2)
        {   // husky face: white cheek patches + dark brow cap
            Part(PrimitiveKind.Sphere, "CheekL", h, new Vector3(-0.065f, -0.035f, 0.09f), new Vector3(0.11f, 0.10f, 0.12f), mask, 1);
            Part(PrimitiveKind.Sphere, "CheekR", h, new Vector3(0.065f, -0.035f, 0.09f), new Vector3(0.11f, 0.10f, 0.12f), mask, 1);
        }
        Part(PrimitiveKind.Sphere, "Muzzle", h, new Vector3(0f, -0.045f, mz), new Vector3(0.125f, 0.105f, sz), muzzleC, 1);
        Part(PrimitiveKind.Sphere, "Nose", h, new Vector3(0f, -0.018f, mz + sz * 0.5f - 0.008f), new Vector3(0.055f, 0.042f, 0.036f), nose, -1);
        rig.eyes[0] = MakeDogEye(h, "EyeL", new Vector3(-0.088f, 0.075f, 0.118f), new Vector3(0.048f, 0.054f, 0.03f), eyeC);
        rig.eyes[1] = MakeDogEye(h, "EyeR", new Vector3(0.088f, 0.075f, 0.118f), new Vector3(0.048f, 0.054f, 0.03f), eyeC);
        // tongue (hidden at scale ~0; scaled up to pant)
        rig.tongue = Empty("Tongue", h, new Vector3(0f, -0.088f, mz + 0.01f));
        Part(PrimitiveKind.Sphere, "TongueMesh", rig.tongue, new Vector3(0f, -0.012f, 0.03f), new Vector3(0.05f, 0.016f, 0.07f), pink, -1);
        rig.tongue.localScale = Vector3.one * 0.001f;

        Color earC = (saddle || beagle || breed == 6 || breed == 7) ? accent : (breed == 0 || breed == 1 ? accent : coat);
        for (int k = 0; k < 2; k++)
        {
            float s = k == 0 ? -1f : 1f;
            if (ears == 0)
            {   // floppy ear hanging from the side of the head
                Transform ep = Empty(k == 0 ? "EarPivotL" : "EarPivotR", h, new Vector3(s * 0.10f, 0.085f, -0.01f));
                ep.localRotation = Quaternion.Euler(0f, 0f, s * 25f);
                rig.ears[k] = ep;
                Part(PrimitiveKind.Capsule, "Ear", ep, new Vector3(0f, -0.07f * earS, 0f), new Vector3(0.08f * earS, 0.095f * earS, 0.04f), earC, 1);
            }
            else
            {   // pointy upright ear
                Transform ep = Empty(k == 0 ? "EarPivotL" : "EarPivotR", h, new Vector3(s * 0.075f, 0.085f, -0.02f));
                ep.localRotation = Quaternion.Euler(0f, 0f, -s * 10f);
                rig.ears[k] = ep;
                PartMesh("Ear", ep, Vector3.zero, new Vector3(0.115f * earS, 0.16f * earS, 0.05f), earC, 1);
                PartMesh("EarInner", ep, new Vector3(0f, -0.005f, 0.011f), new Vector3(0.07f * earS, 0.115f * earS, 0.035f), pink, -1);
            }
        }

        // tail
        rig.tail = Empty("TailPivot", v, new Vector3(0f, L + 0.09f, -0.28f * bodyL));
        Color tailC = coat;
        if (tailType == 0)
        {
            rig.tail.localRotation = Quaternion.Euler(-55f, 0f, 0f);
            Part(PrimitiveKind.Capsule, "Tail", rig.tail, new Vector3(0f, 0.11f, 0f), new Vector3(0.07f, 0.12f, 0.07f), tailC, 1);
            if (whiteTip) Part(PrimitiveKind.Sphere, "TailTip", rig.tail, new Vector3(0f, 0.225f, 0f), new Vector3(0.075f, 0.075f, 0.075f), light, 1);
        }
        else if (tailType == 1)
        {
            rig.tail.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            Part(PrimitiveKind.Capsule, "Tail", rig.tail, new Vector3(0f, 0.07f, 0f), new Vector3(0.09f, 0.075f, 0.09f), tailC, 1);
            Transform curl = Empty("TailCurl", rig.tail, new Vector3(0f, 0.12f, 0f));
            curl.localRotation = Quaternion.Euler(75f, 0f, 0f);
            Part(PrimitiveKind.Capsule, "TailCurlMesh", curl, new Vector3(0f, 0.05f, 0f), new Vector3(0.085f, 0.06f, 0.085f), breed == 2 ? light : tailC, 1);
        }
        else if (tailType == 2)
        {
            rig.tail.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            Part(PrimitiveKind.Sphere, "Tail", rig.tail, new Vector3(0f, 0.02f, -0.02f), new Vector3(0.10f, 0.10f, 0.10f), tailC, 1);
        }
        else
        {
            rig.tail.localRotation = Quaternion.Euler(-100f, 0f, 0f);
            Part(PrimitiveKind.Capsule, "Tail", rig.tail, new Vector3(0f, 0.13f, 0f), new Vector3(0.105f, 0.14f, 0.105f), saddle ? accent : tailC, 1);
        }

        float hx = 0.10f * bodyW, hz = 0.20f * bodyL;
        Vector3[] hips = { new Vector3(-hx, L, 0.17f * bodyL), new Vector3(hx, L, 0.17f * bodyL), new Vector3(-hx - 0.01f, L, -hz), new Vector3(hx + 0.01f, L, -hz) };
        for (int i = 0; i < 4; i++) Leg(rig, v, i, hips[i], L, 0.09f, 0.10f, legC, pawC, 1.3f, 0.7f, 1.6f);
    }

    static Transform MakeDogEye(Transform parent, string name, Vector3 pos, Vector3 sc, Color iris)
    {
        Transform e = Empty(name, parent, pos);
        Part(PrimitiveKind.Sphere, "Iris", e, Vector3.zero, sc, iris, -1);
        Part(PrimitiveKind.Sphere, "Pupil", e, new Vector3(0f, 0f, sc.z * 0.38f), new Vector3(sc.x * 0.6f, sc.y * 0.6f, sc.z * 0.4f), new Color(0.03f, 0.02f, 0.02f), -1);
        Part(PrimitiveKind.Sphere, "Glint", e, new Vector3(-sc.x * 0.18f, sc.y * 0.22f, sc.z * 0.5f), new Vector3(sc.x * 0.26f, sc.y * 0.26f, sc.z * 0.25f), Color.white, -1);
        return e;
    }

    // ------------------------------------------------------------------- cat
    // Eight coat variants chosen at random per cat: orange tabby, grey tabby, brown tabby, black,
    // tuxedo, siamese, calico, cream.
    static void BuildCat(AnimalRig rig, Transform root, System.Random rng)
    {
        Transform v = rig.visual;
        int variant = rng.Next(10);
        Color coat, light, point, stripe = Color.clear, iris = new Color(0.20f, 0.52f, 0.22f);
        Color patchA = Color.clear, patchB = Color.clear;
        bool tabby = false, points = false, ruff = false, tufts = false, flat = false, spots = false;
        float bodyW = 1f, bodyL = 1f, tailW = 1f, earS = 1f;
        Color pink = new Color(0.92f, 0.52f, 0.56f);
        switch (variant)
        {
            case 0: // orange tabby
                coat = new Color(0.90f, 0.54f, 0.25f); light = new Color(0.97f, 0.88f, 0.74f); stripe = new Color(0.66f, 0.32f, 0.12f); tabby = true; break;
            case 1: // British shorthair: chunky, round, blue-grey, small ears
                coat = new Color(0.55f, 0.60f, 0.68f); light = new Color(0.63f, 0.68f, 0.75f); iris = new Color(0.90f, 0.56f, 0.14f);
                bodyW = 1.14f; earS = 0.8f; tailW = 1.1f; flat = true; break;
            case 2: // Siamese: slim, big ears, dark points, blue eyes
                coat = new Color(0.94f, 0.89f, 0.79f); light = coat; points = true; iris = new Color(0.30f, 0.58f, 0.92f);
                bodyW = 0.88f; bodyL = 1.08f; earS = 1.25f; tailW = 0.85f; break;
            case 3: // Persian: fluffy, flat face, tiny ears, bushy tail
                coat = new Color(0.96f, 0.92f, 0.85f); light = new Color(1f, 0.97f, 0.92f); iris = new Color(0.86f, 0.50f, 0.14f);
                bodyW = 1.15f; earS = 0.72f; tailW = 1.55f; ruff = true; flat = true; break;
            case 4: // Maine coon: large, brown tabby, ruff, ear tufts, huge tail
                coat = new Color(0.56f, 0.39f, 0.27f); light = new Color(0.88f, 0.76f, 0.62f); stripe = new Color(0.33f, 0.20f, 0.12f);
                tabby = true; iris = new Color(0.78f, 0.60f, 0.16f); ruff = true; tufts = true; bodyL = 1.12f; earS = 1.12f; tailW = 1.5f; rig.sizeMul = 1.2f; break;
            case 5: // tuxedo
                coat = new Color(0.17f, 0.16f, 0.18f); light = new Color(0.97f, 0.96f, 0.94f); break;
            case 6: // calico
                coat = new Color(0.97f, 0.95f, 0.91f); light = coat; patchA = new Color(0.88f, 0.50f, 0.20f); patchB = new Color(0.20f, 0.18f, 0.20f); break;
            case 7: // black (Bombay)
                coat = new Color(0.17f, 0.16f, 0.18f); light = coat; iris = new Color(0.90f, 0.68f, 0.14f); pink = new Color(0.45f, 0.30f, 0.32f); break;
            case 8: // Bengal: golden with dark rosette spots
                coat = new Color(0.86f, 0.62f, 0.30f); light = new Color(0.97f, 0.90f, 0.75f); stripe = new Color(0.30f, 0.18f, 0.08f); spots = true; bodyW = 0.95f; bodyL = 1.05f; break;
            default: // grey tabby
                coat = new Color(0.58f, 0.60f, 0.64f); light = new Color(0.90f, 0.90f, 0.92f); stripe = new Color(0.34f, 0.36f, 0.40f); tabby = true; iris = new Color(0.80f, 0.62f, 0.18f); break;
        }
        point = points ? new Color(0.33f, 0.25f, 0.22f) : coat;   // ears / tail / paws colour
        Color paw = points ? point : light;
        Color inner = points ? new Color(0.60f, 0.42f, 0.40f) : pink;

        Vector3 bc = new Vector3(0f, 0.25f, -0.04f), br = new Vector3(0.31f * bodyW, 0.27f * (0.6f + 0.4f * bodyW), 0.54f * bodyL);
        rig.body = Part(PrimitiveKind.Sphere, "Body", v, bc, br, coat, 1);
        Part(PrimitiveKind.Sphere, "Chest", v, new Vector3(0f, 0.255f, 0.16f), new Vector3(0.25f, 0.24f, 0.24f), light, 1);
        Contacts(v, bc, br);

        // tabby stripes / calico patches lie on the back like the deer's spots
        if (tabby)
        {
            float[] sz = { -0.17f, -0.05f, 0.07f };
            float[] sa = { -42f, 0f, 42f };
            for (int i = 0; i < sz.Length; i++)
                for (int j = 0; j < sa.Length; j++)
                    SurfaceMark(v, bc, br, sz[i], sa[j], new Vector3(0.075f, 0.02f, 0.035f), stripe);
        }
        else if (spots)
        {
            float[] spz = { -0.2f, -0.12f, -0.04f, 0.04f, 0.12f, -0.16f, -0.08f, 0f, 0.08f, 0.16f };
            float[] spa = { -35f, 10f, -20f, 30f, -10f, 28f, -45f, 45f, -35f, 18f };
            for (int i = 0; i < spz.Length; i++)
                SurfaceMark(v, bc, br, spz[i], spa[i], new Vector3(0.055f, 0.02f, 0.045f), stripe);
        }
        else if (patchA.a > 0f)
        {
            SurfaceMark(v, bc, br, -0.12f, 28f, new Vector3(0.15f, 0.025f, 0.15f), patchA);
            SurfaceMark(v, bc, br, 0.08f, -35f, new Vector3(0.14f, 0.025f, 0.13f), patchB);
            SurfaceMark(v, bc, br, -0.2f, -20f, new Vector3(0.11f, 0.025f, 0.10f), patchA);
        }

        rig.head = Empty("HeadPivot", v, new Vector3(0f, 0.34f, 0.27f));
        Transform h = rig.head;
        Part(PrimitiveKind.Sphere, "Head", h, new Vector3(0f, 0.04f, 0.04f), new Vector3(0.33f, 0.26f, 0.27f), coat, 1);
        if (points) Part(PrimitiveKind.Sphere, "Mask", h, new Vector3(0f, -0.02f, 0.09f), new Vector3(0.17f, 0.13f, 0.13f), point, 1);
        // broad muzzle pads + chin give a cat face rather than a pointed rodent snout
        Part(PrimitiveKind.Sphere, "CheekL", h, new Vector3(-0.05f, -0.025f, 0.11f), new Vector3(0.10f, 0.08f, 0.09f), light, 1);
        Part(PrimitiveKind.Sphere, "CheekR", h, new Vector3(0.05f, -0.025f, 0.11f), new Vector3(0.10f, 0.08f, 0.09f), light, 1);
        Part(PrimitiveKind.Sphere, "Chin", h, new Vector3(0f, -0.075f, 0.105f), new Vector3(0.07f, 0.045f, 0.06f), light, 1);
        Part(PrimitiveKind.Sphere, "Nose", h, new Vector3(0f, 0.012f, flat ? 0.158f : 0.168f), new Vector3(0.036f, 0.026f, 0.022f), inner, -1);
        // eyes sit ON the head surface (computed from the head ellipsoid) and face outward, so no breed can bury them
        Vector3 headCenter = new Vector3(0f, 0.04f, 0.04f), headRadii = new Vector3(0.165f, 0.13f, 0.135f);
        rig.eyes[0] = MakeCatEye(h, "EyeL", Vector3.zero, new Vector3(0.058f, 0.062f, 0.03f), iris);
        rig.eyes[1] = MakeCatEye(h, "EyeR", Vector3.zero, new Vector3(0.058f, 0.062f, 0.03f), iris);
        PlaceEyeOnSurface(rig.eyes[0], headCenter, headRadii, -36f, 16f);
        PlaceEyeOnSurface(rig.eyes[1], headCenter, headRadii, 36f, 16f);
        if (ruff) Part(PrimitiveKind.Sphere, "Ruff", h, new Vector3(0f, -0.07f, -0.01f), new Vector3(0.36f, 0.20f, 0.26f), light, 1);
        if (tabby) Part(PrimitiveKind.Sphere, "Brow", h, new Vector3(0f, 0.125f, 0.095f), new Vector3(0.04f, 0.02f, 0.03f), stripe, -1);

        for (int k = 0; k < 2; k++)
        {
            float s = k == 0 ? -1f : 1f;
            // Pointed triangular ears: pyramid mesh, base buried in the skull, tip up and slightly outward.
            Transform ep = Empty(k == 0 ? "EarPivotL" : "EarPivotR", h, new Vector3(s * 0.085f, 0.085f, -0.005f));
            ep.localRotation = Quaternion.Euler(0f, 0f, -s * 14f);
            rig.ears[k] = ep;
            PartMesh("Ear", ep, new Vector3(0f, 0f, 0f), new Vector3(0.125f * earS, 0.17f * earS, 0.06f), point, 1);
            PartMesh("EarInner", ep, new Vector3(0f, -0.005f, 0.012f), new Vector3(0.075f * earS, 0.125f * earS, 0.04f), inner, -1);
            if (tufts) Part(PrimitiveKind.Capsule, "EarTuft", ep, new Vector3(0f, 0.165f * earS, 0f), new Vector3(0.016f, 0.04f, 0.016f), stripe, -1);
            for (int w = 0; w < 3; w++)
            {
                Transform wh = Part(PrimitiveKind.Capsule, "Whisker", h, new Vector3(s * 0.15f, -0.03f + 0.013f * w, 0.105f), new Vector3(0.005f, 0.075f, 0.005f), new Color(0.96f, 0.96f, 0.96f), -1);
                wh.localRotation = Quaternion.Euler(0f, 0f, -s * (84f - w * 12f));
            }
        }

        // 3-segment fluffy tail: base points back/up, outer segments curl upward
        rig.tail = Empty("TailPivot", v, new Vector3(0f, 0.27f, -0.29f * bodyL));
        rig.tail.localRotation = Quaternion.Euler(-60f, 0f, 0f);
        Part(PrimitiveKind.Capsule, "Tail0", rig.tail, new Vector3(0f, 0.06f, 0f), new Vector3(0.08f * tailW, 0.075f, 0.08f * tailW), coat, 1);
        Transform seg1 = Empty("TailSeg1", rig.tail, new Vector3(0f, 0.12f, 0f));
        seg1.localRotation = Quaternion.Euler(30f, 0f, 0f);
        Part(PrimitiveKind.Capsule, "Tail1", seg1, new Vector3(0f, 0.06f, 0f), new Vector3(0.078f * tailW, 0.075f, 0.078f * tailW), tabby ? stripe : point, 1);
        Transform seg2 = Empty("TailSeg2", seg1, new Vector3(0f, 0.12f, 0f));
        seg2.localRotation = Quaternion.Euler(30f, 0f, 0f);
        Part(PrimitiveKind.Capsule, "Tail2", seg2, new Vector3(0f, 0.06f, 0f), new Vector3(0.074f * tailW, 0.075f, 0.074f * tailW), tabby ? coat : point, 1);
        rig.tailSegments = new[] { seg1, seg2 };
        rig.tailSegmentHomeRotations = new[] { seg1.localRotation, seg2.localRotation };

        float hx = 0.09f * bodyW, hz = 0.20f * bodyL;
        Vector3[] hips = { new Vector3(-hx, 0.25f, 0.16f), new Vector3(hx, 0.25f, 0.16f), new Vector3(-hx - 0.01f, 0.25f, -hz), new Vector3(hx + 0.01f, 0.25f, -hz) };
        for (int i = 0; i < 4; i++) Leg(rig, v, i, hips[i], 0.25f, 0.085f, 0.06f, points ? point : coat, paw, 1.35f, 0.7f, 1.6f);
    }

    // Flat disc lying on the surface of an ellipsoid body (angle = rotation around the body's long axis).
    static void SurfaceMark(Transform parent, Vector3 c, Vector3 r, float z, float angleDeg, Vector3 size, Color col)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        Vector3 n = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f);
        float ratio = Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Pow(z / (r.z * 0.5f), 2f)));
        Vector3 p = new Vector3(c.x + n.x * r.x * 0.5f * ratio * 0.98f, c.y + n.y * r.y * 0.5f * ratio * 0.98f, c.z + z);
        Part(PrimitiveKind.Sphere, "Mark", parent, p, size, col, -1).localRotation = Quaternion.FromToRotation(Vector3.up, n);
    }

    // Puts an eye on the surface of the head ellipsoid at the given azimuth/elevation, facing outward.
    static void PlaceEyeOnSurface(Transform eye, Vector3 center, Vector3 radii, float azimuthDeg, float elevationDeg)
    {
        float az = azimuthDeg * Mathf.Deg2Rad, el = elevationDeg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
        Vector3 local = new Vector3(dir.x * radii.x, dir.y * radii.y, dir.z * radii.z);
        eye.localPosition = center + local;
        Vector3 normal = new Vector3(local.x / (radii.x * radii.x), local.y / (radii.y * radii.y), local.z / (radii.z * radii.z));
        if (normal.sqrMagnitude > 1e-6f) eye.localRotation = Quaternion.LookRotation(normal.normalized, Vector3.up);
    }

    // Cat eye: coloured iris, vertical slit pupil, small highlight. Parent squashes in Y to blink.
    static Transform MakeCatEye(Transform parent, string name, Vector3 pos, Vector3 sc, Color iris)
    {
        Transform e = Empty(name, parent, pos);
        Part(PrimitiveKind.Sphere, "Iris", e, Vector3.zero, sc, iris, -1);
        Part(PrimitiveKind.Sphere, "Pupil", e, new Vector3(0f, 0f, sc.z * 0.40f), new Vector3(sc.x * 0.26f, sc.y * 0.85f, sc.z * 0.35f), new Color(0.03f, 0.02f, 0.02f), -1);
        Part(PrimitiveKind.Sphere, "Glint", e, new Vector3(-sc.x * 0.20f, sc.y * 0.24f, sc.z * 0.45f),
            new Vector3(sc.x * 0.24f, sc.y * 0.24f, sc.z * 0.25f), Color.white, -1);
        return e;
    }

    // Unit pyramid (base 1x1 at y=0, apex at (0,1,0)) with flat-shaded faces, used for pointy ears.
    static Mesh pyramidMesh;
    static Mesh GetPyramid()
    {
        if (pyramidMesh != null) return pyramidMesh;
        Vector3 b0 = new Vector3(-0.5f, 0f, -0.5f), b1 = new Vector3(0.5f, 0f, -0.5f);
        Vector3 b2 = new Vector3(0.5f, 0f, 0.5f), b3 = new Vector3(-0.5f, 0f, 0.5f);
        Vector3 a = new Vector3(0f, 1f, 0f);
        Vector3[][] faces = { new[] { b3, b2, a }, new[] { b2, b1, a }, new[] { b1, b0, a }, new[] { b0, b3, a }, new[] { b0, b1, b2 }, new[] { b0, b2, b3 } };
        System.Collections.Generic.List<Vector3> verts = new System.Collections.Generic.List<Vector3>();
        System.Collections.Generic.List<Vector2> uvs = new System.Collections.Generic.List<Vector2>();
        System.Collections.Generic.List<int> tris = new System.Collections.Generic.List<int>();
        for (int f = 0; f < faces.Length; f++)
        {
            Vector3 p0 = faces[f][0], p1 = faces[f][1], p2 = faces[f][2];
            Vector3 centroid = (p0 + p1 + p2) / 3f;
            // make the winding face outward (away from the pyramid's middle)
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), centroid - new Vector3(0f, 0.3f, 0f)) < 0f)
            { Vector3 tmp = p1; p1 = p2; p2 = tmp; }
            int i0 = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(0.5f, 1f));
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
        }
        Mesh m = new Mesh();
        m.name = "AnimalPyramid";
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        pyramidMesh = m;
        return m;
    }

    internal static Transform PartMesh(string name, Transform parent, Vector3 pos, Vector3 scale, Color color, int pattern)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = GetPyramid();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = pattern >= 0 ? AnimalMaterialFactory.Fur(color, pattern) : AnimalMaterialFactory.Flat(color, 0.32f);
        return go.transform;
    }

    // ---------------------------------------------------------------- helpers
    // Hip pivot sits inside the body. Capsule runs from `top` above the pivot down to y = -length,
    // so with the pivot at height `length` the paw rests exactly on the ground.
    static void Leg(AnimalRig rig, Transform parent, int i, Vector3 hip, float length, float d, float top,
        Color legColor, Color pawColor, float pawX, float pawY, float pawZ)
    {
        Transform pivot = Empty("LegPivot" + i, parent, hip);
        rig.legs[i] = pivot;
        rig.legLength[i] = length;
        Part(PrimitiveKind.Capsule, "Leg", pivot, new Vector3(0f, (top - length) * 0.5f, 0f), new Vector3(d, (length + top) * 0.5f, d), legColor, 0);
        Part(PrimitiveKind.Sphere, "Paw", pivot, new Vector3(0f, -length + d * pawY * 0.5f, d * 0.2f * pawZ / 1.6f),
            new Vector3(d * pawX, d * pawY, d * pawZ), pawColor, 0);
        rig.feet[i] = Empty("Foot", pivot, new Vector3(0f, -length, 0f));
    }

    // Three support points on the underside of an ellipsoid body (centre, front, rear).
    static void Contacts(Transform parent, Vector3 c, Vector3 r)
    {
        float[] dz = { 0f, 0.6f, -0.6f };
        float[] dy = { -1f, -0.8f, -0.8f };
        for (int i = 0; i < 3; i++)
            contactList.Add(Empty("Contact", parent, new Vector3(c.x, c.y + dy[i] * r.y * 0.5f, c.z + dz[i] * r.z * 0.5f)));
        // flank points so a lying-on-side pose rests on the body instead of sinking into the ground
        contactList.Add(Empty("ContactL", parent, new Vector3(c.x - r.x * 0.49f, c.y, c.z)));
        contactList.Add(Empty("ContactR", parent, new Vector3(c.x + r.x * 0.49f, c.y, c.z)));
    }

    // Big glossy bead eye with a small highlight; the parent can be squashed in Y to blink/close.
    static Transform MakeEye(Transform parent, string name, Vector3 pos, Vector3 sc, Color col)
    {
        Transform e = Empty(name, parent, pos);
        Part(PrimitiveKind.Sphere, "Eyeball", e, Vector3.zero, sc, col, -1);
        Part(PrimitiveKind.Sphere, "Glint", e, new Vector3(-sc.x * 0.12f, sc.y * 0.22f, sc.z * 0.38f),
            new Vector3(sc.x * 0.36f, sc.y * 0.36f, sc.z * 0.3f), Color.white, -1);
        return e;
    }

    internal static Transform Empty(string name, Transform parent, Vector3 pos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        go.transform.localPosition = pos;
        return go.transform;
    }

    internal static Transform Part(PrimitiveKind kind, string name, Transform parent, Vector3 pos, Vector3 scale, Color color, int pattern)
    {
        GameObject go = PrimitiveFactory.Create(kind, parent);
        go.name = name;
        go.layer = parent.gameObject.layer;
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = pattern >= 0 ? AnimalMaterialFactory.Fur(color, pattern) : AnimalMaterialFactory.Flat(color, 0.32f);
        Collider col = go.GetComponent<Collider>();
        if (col != null) WorldUtil.Kill(col);
        return go.transform;
    }

    static void Capture(AnimalRig rig)
    {
        if (rig.neck != null) rig.neckHome = rig.neck.localRotation;
        rig.headHome = rig.head.localRotation;
        rig.tailHome = rig.tail.localRotation;
        for (int i = 0; i < 2; i++) rig.earHome[i] = rig.ears[i].localRotation;
    }
}
