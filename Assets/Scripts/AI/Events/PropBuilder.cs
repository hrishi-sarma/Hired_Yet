using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds the "Prop" object of an event: root (ground centre, moved by WorldEvent) > Body (parts + colliders).
/// Parts added with Hold() are picked up by the player's hands during the event.</summary>
public static class PropBuilder
{
    static Color C(string h) { Color c; return ColorUtility.TryParseHtmlString(h, out c) ? c : Color.gray; }

    // centre-positioned primitive; cylinders/capsules take sy as full height
    static Transform P(Transform par, string n, PrimitiveKind k, float x, float y, float z, float sx, float sy, float sz, string hex,
                       bool col = true, float rx = 0f, float ry = 0f, float rz = 0f)
    {
        GameObject g = PrimitiveFactory.Create(k, par);
        g.name = n;
        Collider c = g.GetComponent<Collider>();
        if (!col && c != null) WorldUtil.Kill(c);
        bool tall = k == PrimitiveKind.Cylinder || k == PrimitiveKind.Capsule;
        g.transform.localPosition = new Vector3(x, y, z);
        g.transform.localRotation = Quaternion.Euler(rx, ry, rz);
        g.transform.localScale = tall ? new Vector3(sx, sy * 0.5f, sz) : new Vector3(sx, sy, sz);
        Renderer r = g.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = MaterialFactory.Get(C(hex));
        return g.transform;
    }

    static Transform Box(Transform p, string n, float x, float y, float z, float sx, float sy, float sz, string hex, bool col = true)
    { return P(p, n, PrimitiveKind.Cube, x, y, z, sx, sy, sz, hex, col); }

    static Transform Cyl(Transform p, string n, float x, float y, float z, float d, float h, string hex, bool col = true, float rx = 0f, float rz = 0f)
    { return P(p, n, PrimitiveKind.Cylinder, x, y, z, d, h, d, hex, col, rx, 0f, rz); }

    static Transform Ball(Transform p, string n, float x, float y, float z, float sx, float sy, float sz, string hex, bool col = true)
    { return P(p, n, PrimitiveKind.Sphere, x, y, z, sx, sy, sz, hex, col); }

    static void Ufo(Transform b, float x, float z)
    {
        Transform u = G(b, "Ufo", x, 0f, z);
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f * Mathf.Deg2Rad;
            Cyl(u, "Leg", Mathf.Sin(a) * 0.75f, 0.32f, Mathf.Cos(a) * 0.75f, 0.1f, 0.66f, "#5E6680", false);
            Ball(u, "Foot", Mathf.Sin(a) * 0.82f, 0.04f, Mathf.Cos(a) * 0.82f, 0.22f, 0.08f, 0.22f, "#434A5E", false);
        }
        Ball(u, "Hull", 0f, 0.95f, 0f, 2.3f, 0.6f, 2.3f, "#98A2BA", false);
        Cyl(u, "Rim", 0f, 1.12f, 0f, 1.7f, 0.1f, "#5E6680", false);
        Ball(u, "Dome", 0f, 1.42f, 0f, 1.0f, 0.8f, 1.0f, "#8FF0FF", false);
        Ball(u, "Pilot", 0f, 1.4f, 0.05f, 0.3f, 0.3f, 0.3f, "#58DB86", false);
        Cyl(u, "Mast", 0f, 1.95f, 0f, 0.04f, 0.3f, "#5E6680", false);
        Ball(u, "Beacon", 0f, 2.14f, 0f, 0.12f, 0.12f, 0.12f, "#FF5C8A", false);
        string[] lights = { "#FFE066", "#FF6FB5", "#6FFFE0" };
        for (int i = 0; i < 10; i++)
        {
            float a = i * 36f * Mathf.Deg2Rad;
            Ball(u, "Light", Mathf.Sin(a) * 1.08f, 0.93f, Mathf.Cos(a) * 1.08f, 0.14f, 0.14f, 0.14f, lights[i % 3], false);
        }
        GameObject g = new GameObject("Collider");
        g.transform.SetParent(u, false);
        BoxCollider bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, 0.8f, 0f);
        bc.size = new Vector3(2.2f, 1.6f, 2.2f);
    }

    static Texture2D MapTexture()
    {
        const int W = 384, Hh = 256;
        Color32[] px = new Color32[W * Hh];
        System.Random rng = new System.Random(9137);
        float ox = (float)rng.NextDouble() * 50f, oy = (float)rng.NextDouble() * 50f;
        Color paper = C("#EBDDB0"), water = C("#79BBDB"), sand = C("#E8D49A"), grass = C("#A3CF7E"), forest = C("#6FA864");
        for (int y = 0; y < Hh; y++)
        for (int x = 0; x < W; x++)
        {
            float n = Mathf.PerlinNoise(ox + x * 0.012f, oy + y * 0.014f) * 0.7f + Mathf.PerlinNoise(ox + x * 0.04f, oy + y * 0.04f) * 0.3f;
            Color c = n < 0.38f ? water : (n < 0.43f ? sand : (n < 0.68f ? grass : forest));
            float grain = (Mathf.PerlinNoise(x * 0.5f, y * 0.5f) - 0.5f) * 0.06f;
            px[y * W + x] = Color.Lerp(c, paper, 0.18f) + new Color(grain, grain, grain, 0f);
        }
        Color road = C("#F8F2DC"), edge = C("#B59A64"), block = C("#C9B48C");
        for (int gx = 0; gx < 6; gx++)
        for (int gy = 0; gy < 4; gy++)
        {
            int bx = 150 + gx * 34, by = 40 + gy * 34;
            Rect(px, W, Hh, bx + 5, by + 5, bx + 29, by + 29, block);
        }
        for (int gx = 0; gx <= 6; gx++) Line(px, W, Hh, 150 + gx * 34 + 2, 36, 150 + gx * 34 + 2, 176, road, 3);
        for (int gy = 0; gy <= 4; gy++) Line(px, W, Hh, 146, 36 + gy * 34 + 2, 356, 36 + gy * 34 + 2, road, 3);
        Line(px, W, Hh, 20, 210, 150, 120, edge, 4);
        Line(px, W, Hh, 20, 210, 150, 120, road, 2);
        Line(px, W, Hh, 150, 120, 90, 20, edge, 4);
        Line(px, W, Hh, 150, 120, 90, 20, road, 2);
        Line(px, W, Hh, 356, 100, 384, 60, road, 3);
        Vector2[] river = new Vector2[14];
        for (int i = 0; i < river.Length; i++) river[i] = new Vector2(i * 30f, 188f + Mathf.Sin(i * 0.9f) * 22f);
        for (int i = 0; i < river.Length - 1; i++) Line(px, W, Hh, (int)river[i].x, (int)river[i].y, (int)river[i + 1].x, (int)river[i + 1].y, water, 5);
        Vector2[] route = { new Vector2(40f, 200f), new Vector2(110f, 150f), new Vector2(150f, 120f), new Vector2(222f, 120f), new Vector2(222f, 86f), new Vector2(290f, 86f) };
        Color red = C("#C23A3A");
        for (int i = 0; i < route.Length - 1; i++) Dotted(px, W, Hh, route[i], route[i + 1], red);
        Disc(px, W, Hh, 40, 200, 7, C("#2E6FD0"));
        Disc(px, W, Hh, 40, 200, 3, Color.white);
        Line(px, W, Hh, 282, 78, 298, 94, red, 4);
        Line(px, W, Hh, 298, 78, 282, 94, red, 4);
        Disc(px, W, Hh, 335, 215, 16, C("#F4EBCB"));
        Line(px, W, Hh, 335, 197, 335, 233, C("#5F4F3F"), 2);
        Line(px, W, Hh, 317, 215, 353, 215, C("#5F4F3F"), 2);
        Poly(px, W, Hh, 335, 236, 329, 222, 341, 222, C("#C23A3A"));
        Color frame = C("#6B5436");
        for (int x = 0; x < W; x++) for (int t = 0; t < 7; t++) { px[t * W + x] = frame; px[(Hh - 1 - t) * W + x] = frame; }
        for (int y = 0; y < Hh; y++) for (int t = 0; t < 7; t++) { px[y * W + t] = frame; px[y * W + W - 1 - t] = frame; }
        Color fold = new Color(0f, 0f, 0f, 0.14f);
        for (int y = 7; y < Hh - 7; y++) { Blend(px, W, 128, y, fold); Blend(px, W, 256, y, fold); }
        Texture2D tex = new Texture2D(W, Hh, TextureFormat.RGBA32, true);
        tex.name = "TouristMap";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(px);
        tex.Apply(true, false);
        return tex;
    }

    static void Blend(Color32[] px, int w, int x, int y, Color c)
    {
        Color o = px[y * w + x];
        px[y * w + x] = Color.Lerp(o, new Color(c.r, c.g, c.b, 1f), c.a);
    }

    static void Rect(Color32[] px, int w, int h, int x0, int y0, int x1, int y1, Color c)
    {
        for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
        for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++) px[y * w + x] = c;
    }

    static void Disc(Color32[] px, int w, int h, int cx, int cy, int r, Color c)
    {
        for (int y = -r; y <= r; y++)
        for (int x = -r; x <= r; x++)
        {
            int X = cx + x, Y = cy + y;
            if (x * x + y * y <= r * r && X >= 0 && X < w && Y >= 0 && Y < h) px[Y * w + X] = c;
        }
    }

    static void Line(Color32[] px, int w, int h, int x0, int y0, int x1, int y1, Color c, int thick)
    {
        float len = Mathf.Max(1f, Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)));
        int steps = Mathf.CeilToInt(len);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Disc(px, w, h, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), Mathf.Max(1, thick / 2), c);
        }
    }

    static void Dotted(Color32[] px, int w, int h, Vector2 a, Vector2 b, Color c)
    {
        float len = Vector2.Distance(a, b);
        int dots = Mathf.Max(1, Mathf.RoundToInt(len / 9f));
        for (int i = 0; i <= dots; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)dots);
            Disc(px, w, h, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), 3, c);
        }
    }

    static void Poly(Color32[] px, int w, int h, int ax, int ay, int bx, int by, int cx, int cy, Color c)
    {
        int minY = Mathf.Min(ay, Mathf.Min(by, cy)), maxY = Mathf.Max(ay, Mathf.Max(by, cy));
        int minX = Mathf.Min(ax, Mathf.Min(bx, cx)), maxX = Mathf.Max(ax, Mathf.Max(bx, cx));
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
            float d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
            float d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            if (!(neg && pos) && x >= 0 && x < w && y >= 0 && y < h) px[y * w + x] = c;
        }
    }

    static Transform G(Transform p, string n, float x, float y, float z)
    {
        Transform t = new GameObject(n).transform;
        t.SetParent(p, false);
        t.localPosition = new Vector3(x, y, z);
        return t;
    }

    // both hands: the part rides the midpoint between the palms (z along the forearm, about 0.1 is the palm)
    static void HoldBoth(Transform t, float z, float pulse = 0f)
    {
        t.gameObject.AddComponent<HeldPart>().SetBoth(new Vector3(0f, 0f, z), pulse);
    }

    // hand-space pose: x right, y up, z along the forearm (about 0.1 is the palm)
    static void Hold(Transform t, bool right, float x, float y, float z, float rx, float ry, float rz, float tilt = 0f, float pulse = 0f)
    {
        t.gameObject.AddComponent<HeldPart>().Set(right, new Vector3(x, y, z), new Vector3(rx, ry, rz), tilt, pulse);
    }

    static PropMode ModeFor(string id)
    {
        switch (id)
        {
            case "grandma_groceries": case "delivery_boxes": case "lost_backpack": return PropMode.Carry;
            case "garage_sale_couch": return PropMode.CarryEnd;
            case "fallen_log": return PropMode.Drag;
            case "lawnmower": case "vending_machine": return PropMode.Shake;
            case "sprinkler_valve": return PropMode.Spin;
        }
        return PropMode.None;
    }

    public static GameObject Build(WorldEventConfig c, Transform parent, System.Random rng, NpcDirector d)
    {
        GameObject root = new GameObject("Prop");
        root.transform.SetParent(parent, false);
        Transform b = new GameObject("Body").transform;
        b.SetParent(root.transform, false);
        EventPropMotion m = root.AddComponent<EventPropMotion>();
        if (c.propPrefab != null) FromPrefab(c, b);
        else Recipe(c, b, rng, d);
        // These two carry props are interaction targets, not physical blockers. Remove every
        // collider from their visual parts so the grocery bags / box stack can never trap the
        // CharacterController, either while resting or while being carried in front of the player.
        if (c.id == "grandma_groceries" || c.id == "delivery_boxes")
        {
            Collider[] carryColliders = b.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < carryColliders.Length; i++)
                if (carryColliders[i] != null) WorldUtil.Kill(carryColliders[i]);
        }
        EventFx.Attach(c.id, b);
        Vector3 size = c.propSize;
        PropMode pm = ModeFor(c.id);
        Bounds lb;
        if ((pm == PropMode.CarryEnd || pm == PropMode.Drag) && LocalBounds(b, out lb)) size = lb.size;   // real extents, so lift/contact maths match the model
        m.Setup(b, pm, EventPlacement.TravelFor(c) > 0.01f, c.id == "boulder", size);
        // Apply collision suppression to the complete event prop after every visual/helper
        // component has been created. This also covers colliders on the root or prefab wrapper,
        // not just primitive colliders directly under Body.
        if (c.id == "grandma_groceries" || c.id == "delivery_boxes")
            m.DisablePhysicalColliders();
        return root;
    }

    // ---------- bounds helpers ----------
    static bool LocalBounds(Transform b, out Bounds r)
    {
        r = new Bounds();
        bool any = false;
        foreach (Renderer rd in b.GetComponentsInChildren<Renderer>())
        {
            Bounds w = rd.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = w.center + Vector3.Scale(w.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 l = b.InverseTransformPoint(corner);
                if (!any) { r = new Bounds(l, Vector3.zero); any = true; }
                else r.Encapsulate(l);
            }
        }
        return any;
    }

    // moves every child so the combined bounds sit centred on x/z with the bottom at y = 0
    static void Ground(Transform b)
    {
        Bounds r;
        if (!LocalBounds(b, out r)) return;
        Vector3 shift = new Vector3(-r.center.x, -r.min.y, -r.center.z);
        foreach (Transform ch in b) ch.localPosition += shift;
    }

    static void AddBox(Transform b, string name)
    {
        Bounds r;
        if (!LocalBounds(b, out r)) return;
        GameObject g = new GameObject(name);
        g.transform.SetParent(b, false);
        BoxCollider bc = g.AddComponent<BoxCollider>();
        bc.center = r.center;
        bc.size = r.size;
    }

    static void FromPrefab(WorldEventConfig c, Transform b)
    {
        GameObject g = Object.Instantiate(c.propPrefab, b);
        g.name = "Model";
        g.transform.localScale = Vector3.Scale(g.transform.localScale, c.propScale);
        g.transform.localPosition = Vector3.zero;
        Ground(b);
        g.transform.localPosition += c.propOffset;
        if (g.GetComponentInChildren<Collider>() == null) AddBox(b, "Collider");
    }

    // ---------- animals, vehicle, birds ----------
    static void Animal(Transform b, AnimalSpecies sp, bool lying, System.Random rng)
    {
        GameObject h = new GameObject(sp.ToString());
        h.transform.SetParent(b, false);
        AnimalRig rig = AnimalModelFactory.Build(sp, h.transform, rng);
        if (rig == null) return;
        if (lying) rig.visual.localRotation = Quaternion.Euler(0f, 0f, 90f);    // start pose; EventAnimalAnim takes over (pain, calm, sit, get up)
        Ground(b);
        Bounds r;
        if (!LocalBounds(b, out r)) return;
        CapsuleCollider cc = h.AddComponent<CapsuleCollider>();
        cc.direction = 2;
        cc.center = r.center;
        cc.height = Mathf.Max(r.size.z, r.size.x);
        cc.radius = Mathf.Max(0.15f, Mathf.Min(r.size.x, r.size.y) * 0.5f);
        h.AddComponent<EventAnimalAnim>().Setup(rig, sp, b.parent);
    }

    static void Car(Transform b, System.Random rng, NpcDirector d)
    {
        CustomVehicleModel cv = AgentPool.PickCustomVehicle(d.settings.customVehicles, rng);
        GameObject g;
        if (cv != null) { g = Object.Instantiate(cv.prefab, b); g.transform.localScale *= cv.scale; }
        else g = AiPrimitives.BuildCar(AiPrimitives.RandomCar(rng), b);
        g.name = "Car";
        g.transform.SetParent(b, false);
        g.transform.localPosition = Vector3.zero;
        g.transform.localRotation = Quaternion.identity;
        foreach (AgentBase a in g.GetComponentsInChildren<AgentBase>()) WorldUtil.Kill(a);
        foreach (Collider k in g.GetComponentsInChildren<Collider>()) WorldUtil.Kill(k);
        foreach (Rigidbody rb in g.GetComponentsInChildren<Rigidbody>()) WorldUtil.Kill(rb);
        GroundVehicle(b);
        AddVehicleBox(b, "Collider");
    }

    // same as Ground/AddBox but measures only the car body, so shadow planes / glow quads can't lift it off the road
    static void GroundVehicle(Transform b)
    {
        Bounds r;
        if (!VehicleBounds.TryGetLocalBodyBounds(b, out r)) return;
        Vector3 shift = new Vector3(-r.center.x, -r.min.y, -r.center.z);
        foreach (Transform ch in b) ch.localPosition += shift;
    }

    static void AddVehicleBox(Transform b, string name)
    {
        Bounds r;
        if (!VehicleBounds.TryGetLocalBodyBounds(b, out r)) return;
        GameObject g = new GameObject(name);
        g.transform.SetParent(b, false);
        BoxCollider bc = g.AddComponent<BoxCollider>();
        bc.center = r.center;
        bc.size = r.size;
    }

    static void Pigeons(Transform b, System.Random rng)
    {
        GameObject fl = new GameObject("Flock");
        fl.transform.SetParent(b, false);
        PigeonFlock pf = fl.AddComponent<PigeonFlock>();
        for (int i = 0; i < 10; i++)
        {
            GameObject h = new GameObject("Pigeon");
            h.transform.SetParent(fl.transform, false);
            float a = i * 36f * Mathf.Deg2Rad + (float)rng.NextDouble() * 0.4f;
            float rad = i == 0 ? 0f : 0.7f + (float)rng.NextDouble() * 0.8f;
            h.transform.localPosition = new Vector3(Mathf.Sin(a) * rad, 0f, Mathf.Cos(a) * rad);
            h.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            FlyerRig fr = FlyerModelFactory.Build(FlyerSpecies.Pigeon, h.transform, rng, i == 0 ? 1.35f : 1f);
            Bounds r;
            if (LocalBounds(h.transform, out r)) foreach (Transform ch in h.transform) ch.localPosition += Vector3.down * r.min.y;
            if (i == 0 && fr != null && fr.head != null) Cyl(fr.head, "Crown", 0f, 0.06f, 0f, 0.05f, 0.03f, "#E0B030", false);
            pf.Add(h.transform, fr);
        }
    }

    // ---------- recipes ----------
    static void Recipe(WorldEventConfig c, Transform b, System.Random rng, NpcDirector d)
    {
        Vector3 s = c.propSize;
        switch (c.id)
        {
            case "grandma_groceries":
                Box(b, "Bag", -0.25f, 0.2f, 0f, 0.3f, 0.4f, 0.25f, "#C8A878"); Box(b, "Bag", 0.1f, 0.22f, 0.05f, 0.3f, 0.44f, 0.25f, "#E8E0D0");
                Box(b, "Bag", 0.0f, 0.18f, -0.28f, 0.3f, 0.36f, 0.25f, "#B89868"); Ball(b, "Melon", 0.12f, 0.5f, 0.05f, 0.14f, 0.14f, 0.14f, "#80B040", false);
                break;
            case "lost_dog": Animal(b, AnimalSpecies.Dog, false, rng); break;
            case "garage_sale_couch":      // long axis = z, back on -x, so it can be lifted at one end
                Box(b, "Seat", 0f, 0.3f, 0f, 0.85f, 0.35f, 1.9f, "#B07F4A"); Box(b, "Back", -0.34f, 0.65f, 0f, 0.2f, 0.5f, 1.9f, "#A06F3A");
                Box(b, "ArmL", 0f, 0.5f, -0.88f, 0.85f, 0.3f, 0.2f, "#A06F3A"); Box(b, "ArmR", 0f, 0.5f, 0.88f, 0.85f, 0.3f, 0.2f, "#A06F3A");
                Box(b, "Cushion", 0.05f, 0.52f, -0.3f, 0.6f, 0.12f, 0.6f, "#C89060", false); Box(b, "Cushion", 0.05f, 0.52f, 0.4f, 0.6f, 0.12f, 0.6f, "#C89060", false);
                break;
            case "thirsty_plants":
                for (int i = 0; i < 3; i++) { Cyl(b, "Pot", -0.8f + i * 0.8f, 0.2f, 0f, 0.4f, 0.4f, "#B8683A"); Ball(b, "Plant", -0.8f + i * 0.8f, 0.55f, 0f, 0.4f, 0.35f, 0.4f, "#4F9F3F", false); }
                Cyl(b, "Hose", 0f, 0.03f, 0.6f, 0.05f, 2.2f, "#3F8F4F", false, 0f, 90f);
                Transform can = G(b, "WateringCan", 1.3f, 0.15f, 0.3f);
                Box(can, "Tank", 0f, 0f, 0f, 0.22f, 0.26f, 0.3f, "#5F8FBF", false); Cyl(can, "Spout", 0f, 0.05f, 0.25f, 0.05f, 0.3f, "#5F8FBF", false, 55f);
                Hold(can, true, 0f, 0.0f, 0.1f, 0f, 0f, 0f, 38f);
                break;
            case "lawnmower":
                Box(b, "Deck", 0f, 0.28f, 0f, 0.55f, 0.25f, 0.85f, "#4F8F4F"); Box(b, "Engine", 0f, 0.5f, 0.1f, 0.3f, 0.18f, 0.35f, "#C03030", false);
                Cyl(b, "Handle", -0.2f, 0.7f, -0.55f, 0.04f, 0.8f, "#303030", false, -30f); Cyl(b, "Handle", 0.2f, 0.7f, -0.55f, 0.04f, 0.8f, "#303030", false, -30f);
                Box(b, "Grip", 0f, 1.046f, -0.75f, 0.44f, 0.04f, 0.04f, "#303030", false);
                for (int i = 0; i < 4; i++) Cyl(b, "WheelRoll", (i % 2 == 0 ? -1 : 1) * 0.3f, 0.12f, (i < 2 ? 0.3f : -0.3f), 0.24f, 0.08f, "#202020", false, 0f, 90f);
                Transform wr = Box(b, "Wrench", 0.5f, 0.03f, 0.3f, 0.03f, 0.024f, 0.24f, "#9090A0", false);
                Hold(wr, true, -0.012f, 0f, 0.075f, -10f, 0f, 0f, 0f, 0.1f);   // centred in the palm, slightly toward the thumb, inside the hand's width
                break;
            case "stalled_car": Car(b, rng, d); break;
            case "lemonade_stand":
                Box(b, "Table", 0f, 0.75f, 0f, 1.3f, 0.06f, 0.7f, "#E8D040"); for (int i = 0; i < 4; i++) Box(b, "Leg", (i % 2 == 0 ? -1 : 1) * 0.6f, 0.36f, (i < 2 ? 0.3f : -0.3f), 0.06f, 0.72f, 0.06f, "#A08030", false);
                Cyl(b, "Jug", -0.4f, 0.93f, 0f, 0.2f, 0.3f, "#F0E070"); Box(b, "Sign", 0f, 0.55f, 0.37f, 0.9f, 0.3f, 0.04f, "#FFFFFF", false);
                for (int i = 0; i < 3; i++) Ball(b, "Lemon", 0.1f + i * 0.15f, 0.84f, 0.1f, 0.1f, 0.08f, 0.08f, "#F0D020", false);
                Transform lem = Ball(b, "HeldLemon", 0.55f, 0.84f, -0.1f, 0.15f, 0.12f, 0.12f, "#F0D020", false);
                HoldBoth(lem, 0.1f);
                break;
            case "sprinkler_valve":
                for (int i = 0; i < 4; i++)
                {
                    float x = -1.05f + i * 0.7f;
                    Cyl(b, "Pipe", x, 0.2f, 0f, 0.08f, 0.4f, "#707080"); Cyl(b, "Wheel" + i, x, 0.42f, 0f, 0.24f, 0.04f, "#C03030", false);
                    Box(b, "Spoke", x, 0.45f, 0f, 0.2f, 0.02f, 0.04f, "#901818", false);
                    Cyl(b, "Head", x, 0.07f, 0.9f, 0.1f, 0.14f, "#404050", false);
                }
                break;
            case "hiker_ankle":
                Box(b, "Backpack", 0.4f, 0.3f, 0f, 0.4f, 0.55f, 0.25f, "#3F7F4F"); Box(b, "Pocket", 0.4f, 0.15f, 0.15f, 0.3f, 0.2f, 0.08f, "#2F6F3F", false);
                Cyl(b, "Stick", -0.3f, 0.5f, 0f, 0.04f, 1.1f, "#7F5F3F", false, 0f, 12f);
                break;
            case "injured_deer": Animal(b, AnimalSpecies.Deer, true, rng); break;
            case "druid_dance":
                float rad = Mathf.Max(1.5f, Mathf.Min(s.x, s.z) * 0.5f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    float h = 1.4f + (i % 3) * 0.35f;
                    Box(b, "Stone", Mathf.Sin(a) * rad, h * 0.5f, Mathf.Cos(a) * rad, 0.5f, h, 0.4f, "#8F8F85").localRotation = Quaternion.Euler(0f, i * 45f, 0f);
                    if (i % 2 == 0) { Cyl(b, "Torch", Mathf.Sin(a) * (rad - 0.6f), 0.6f, Mathf.Cos(a) * (rad - 0.6f), 0.06f, 1.2f, "#6F4F2F"); Ball(b, "Flame", Mathf.Sin(a) * (rad - 0.6f), 1.3f, Mathf.Cos(a) * (rad - 0.6f), 0.18f, 0.28f, 0.18f, "#FF9020", false); }
                }
                break;
            case "witch_cauldron":
                Ball(b, "Pot", 0f, 0.55f, 0f, 0.95f, 0.75f, 0.95f, "#202025"); Cyl(b, "Rim", 0f, 0.88f, 0f, 0.8f, 0.06f, "#303038", false); Cyl(b, "Brew", 0f, 0.86f, 0f, 0.7f, 0.04f, "#50C050", false);
                for (int i = 0; i < 3; i++) Cyl(b, "Log", 0f, 0.06f, 0f, 0.1f, 0.9f, "#5F3F20", false, 90f, i * 60f);
                Ball(b, "Fire", 0f, 0.16f, 0f, 0.4f, 0.3f, 0.4f, "#FF8020", false);
                Transform ladle = G(b, "Ladle", 0.75f, 0.45f, 0f);
                Cyl(ladle, "Shaft", 0f, 0f, 0f, 0.035f, 0.9f, "#6F4F2F", false); Ball(ladle, "Bowl", 0f, -0.45f, 0f, 0.12f, 0.06f, 0.12f, "#404040", false);
                Hold(ladle, true, 0f, 0.02f, 0.14f, 20f, 0f, 0f);
                break;
            case "campfire_rain":
                for (int i = 0; i < 8; i++) { float a = i * 45f * Mathf.Deg2Rad; Ball(b, "Stone", Mathf.Sin(a) * 0.6f, 0.1f, Mathf.Cos(a) * 0.6f, 0.26f, 0.2f, 0.26f, "#6F6F68", false); }
                for (int i = 0; i < 3; i++) Cyl(b, "Log", 0f, 0.1f, 0f, 0.12f, 0.9f, "#3F2A18", false, 90f, i * 60f);
                Ball(b, "Fire", 0f, 0.2f, 0f, 0.4f, 0.3f, 0.4f, "#FF8020", false);
                break;
            case "elderly_crosswalk":
                Cyl(b, "Cane", 0.5f, 0.4f, 0f, 0.04f, 0.8f, "#6F4F2F", false);
                break;
            case "beekeeper":
                for (int k = 0; k < 2; k++) for (int i = 0; i < 3; i++) Box(b, "Hive", -0.5f + k * 1.0f, 0.15f + i * 0.3f, 0f, 0.5f, 0.28f, 0.45f, i % 2 == 0 ? "#F0E0A0" : "#E0C860");
                Box(b, "Roof", -0.5f, 0.95f, 0f, 0.56f, 0.06f, 0.5f, "#808078", false);
                Transform sm = G(b, "Smoker", 0.35f, 0.12f, 0.7f);
                Cyl(sm, "Tin", 0f, 0f, 0f, 0.14f, 0.24f, "#8F8F95", false); Cyl(sm, "Nozzle", 0f, 0.16f, 0.04f, 0.07f, 0.1f, "#6F6F75", false, 30f); Box(sm, "Bellows", 0f, -0.02f, -0.16f, 0.1f, 0.06f, 0.18f, "#7F4F2F", false);
                Hold(sm, true, 0f, 0.0f, 0.1f, 0f, 0f, 0f, 0f, 0.3f);
                break;
            case "mushroom_sorting":
                Box(b, "Blanket", 0f, 0.01f, 0.3f, 1.2f, 0.02f, 0.8f, "#B84040", false);
                Box(b, "CrateSafe", -0.55f, 0.15f, -0.4f, 0.5f, 0.3f, 0.4f, "#8F6F3F"); Box(b, "CrateBad", 0.55f, 0.15f, -0.4f, 0.5f, 0.3f, 0.4f, "#5F3F2F");
                for (int i = 0; i < 5; i++) { float x = -0.4f + i * 0.2f; Cyl(b, "Stem", x, 0.06f, 0.3f, 0.05f, 0.12f, "#F0E8D0", false); Ball(b, "Cap", x, 0.14f, 0.3f, 0.14f, 0.08f, 0.14f, i % 2 == 0 ? "#C03030" : "#C09050", false); }
                Transform mu = G(b, "HeldMushroom", 0f, 0.08f, 0.55f);
                Cyl(mu, "Stem", 0f, -0.02f, 0f, 0.05f, 0.12f, "#F0E8D0", false); Ball(mu, "Cap", 0f, 0.05f, 0f, 0.14f, 0.08f, 0.14f, "#C03030", false);
                Hold(mu, false, 0f, 0f, 0.1f, 0f, 0f, 0f, 0f, 0.2f);
                break;
            case "hermit_riddle":
                Ball(b, "Rock", 0f, 0.7f, 0f, 2f, 1.5f, 1.8f, "#6F6F5F"); Ball(b, "Rock", 0.9f, 0.35f, 0.5f, 0.9f, 0.7f, 0.8f, "#7F7F6F");
                Cyl(b, "Lantern", -0.9f, 0.15f, 0.7f, 0.18f, 0.3f, "#4F4F55"); Ball(b, "Glow", -0.9f, 0.18f, 0.7f, 0.13f, 0.13f, 0.13f, "#FFD070", false);
                break;
            case "fallen_log":
                // same look as the small fallen logs the forest stage scatters: plain dark-brown cylinder
                Cyl(b, "Log", 0f, 0.23f, 0f, 0.46f, 2.2f, "#4D301B", true, 90f);
                break;
            case "boulder":
                Ball(b, "Boulder", 0f, s.y * 0.5f, 0f, s.x, s.y, s.z, "#6F5F4F"); Ball(b, "Bump", s.x * 0.3f, s.y * 0.75f, s.z * 0.2f, s.x * 0.4f, s.y * 0.35f, s.z * 0.4f, "#7F6F5F", false);
                break;
            case "lost_backpack":
                Box(b, "Pack", 0f, 0.28f, 0f, 0.38f, 0.52f, 0.24f, "#CF6F2F"); Box(b, "Flap", 0f, 0.5f, 0.02f, 0.4f, 0.1f, 0.27f, "#AF4F1F", false);
                Box(b, "Pocket", 0f, 0.15f, 0.14f, 0.3f, 0.2f, 0.07f, "#BF5F1F", false);
                break;
            case "delivery_boxes":
                Box(b, "Box", 0f, 0.15f, 0f, 0.45f, 0.3f, 0.4f, "#B89058"); Box(b, "Box", 0.02f, 0.45f, 0f, 0.4f, 0.3f, 0.36f, "#C8A068");
                Box(b, "Box", -0.02f, 0.73f, 0f, 0.36f, 0.26f, 0.32f, "#B89058"); Box(b, "Tape", 0f, 0.87f, 0f, 0.08f, 0.01f, 0.33f, "#E8D8B0", false);
                break;
            case "pigeon_king": Pigeons(b, rng); break;
            case "runaway_cart":
                Box(b, "Frame", 0f, 0.3f, 0f, 0.5f, 0.04f, 0.9f, "#A0A0B0"); Box(b, "Basket", 0f, 0.55f, 0.05f, 0.55f, 0.4f, 0.8f, "#B0B0C0", false);
                Box(b, "Handle", 0f, 0.95f, -0.45f, 0.55f, 0.04f, 0.04f, "#303038", false);
                for (int i = 0; i < 4; i++) Cyl(b, "Wheel", (i % 2 == 0 ? -1 : 1) * 0.22f, 0.07f, (i < 2 ? 0.38f : -0.38f), 0.14f, 0.05f, "#202020", false, 0f, 90f);
                Box(b, "Goods", -0.1f, 0.75f, 0.1f, 0.25f, 0.25f, 0.25f, "#E0A040", false); Box(b, "Goods", 0.15f, 0.72f, 0.2f, 0.2f, 0.2f, 0.2f, "#60A060", false);
                Box(b, "CartCollider", 0f, 0.55f, 0f, 0.55f, 0.9f, 1f, "#FFFFFF").GetComponent<Renderer>().enabled = false;
                break;
            case "vending_machine":
                Box(b, "Body", 0f, 0.95f, 0f, 0.95f, 1.9f, 0.8f, "#C03038"); Box(b, "Window", -0.12f, 1.25f, 0.405f, 0.6f, 1.0f, 0.02f, "#A0D0E8", false);
                Box(b, "Panel", 0.34f, 1.2f, 0.405f, 0.18f, 0.7f, 0.02f, "#303038", false); Box(b, "Slot", 0f, 0.3f, 0.405f, 0.7f, 0.18f, 0.02f, "#101015", false);
                for (int i = 0; i < 4; i++) Box(b, "Snack", -0.12f, 0.92f + i * 0.24f, 0.38f, 0.5f, 0.1f, 0.02f, i % 2 == 0 ? "#E0A030" : "#4090D0", false);
                break;
            case "food_truck":
                Box(b, "Body", 0f, 1.2f, -0.4f, 2.2f, 2.0f, 3.2f, "#E0E0E0"); Box(b, "Cab", 0f, 0.9f, 1.8f, 2.0f, 1.5f, 1.2f, "#D0D0D8");
                Box(b, "Window", 0f, 1.5f, 0.7f, 0.04f, 0.6f, 1.0f, "#303038", false); Box(b, "Awning", 1.3f, 2.0f, -0.3f, 0.6f, 0.05f, 1.8f, "#D05050", false);
                for (int i = 0; i < 4; i++) Cyl(b, "Tyre", (i % 2 == 0 ? -1 : 1) * 1.0f, 0.3f, (i < 2 ? 1.6f : -1.4f), 0.6f, 0.25f, "#202020", false, 0f, 90f);
                Box(b, "Counter", 1.45f, 0.95f, -0.3f, 0.5f, 0.08f, 1.8f, "#B0B0B8"); Box(b, "Board", 1.45f, 1.01f, 0.05f, 0.35f, 0.03f, 0.5f, "#C89060", false);
                for (int i = 0; i < 3; i++) Ball(b, "Onion", 1.45f, 1.06f, -0.15f + i * 0.12f, 0.08f, 0.08f, 0.08f, "#E8D8A0", false);
                Transform kn = Box(b, "Knife", 1.4f, 1.04f, 0.3f, 0.02f, 0.03f, 0.28f, "#C0C0C8", false);
                Hold(kn, true, 0f, 0f, 0.12f, 0f, 0f, 0f, 0f, 0.0f);
                break;
            case "street_magician":
                Box(b, "Top", 0f, 0.85f, 0f, 0.9f, 0.05f, 0.6f, "#2F6F4F"); for (int i = 0; i < 4; i++) Box(b, "Leg", (i % 2 == 0 ? -1 : 1) * 0.4f, 0.4f, (i < 2 ? 0.25f : -0.25f), 0.05f, 0.8f, 0.05f, "#303038", false);
                for (int i = 0; i < 4; i++) Box(b, "Card", -0.2f + i * 0.1f, 0.89f, 0.05f, 0.07f, 0.005f, 0.1f, "#FFFFFF", false);
                Cyl(b, "Hat", 0.3f, 0.99f, 0f, 0.18f, 0.2f, "#101015", false);
                break;
            case "tourist_map":
                Box(b, "PostL", -0.55f, 0.8f, 0f, 0.07f, 1.6f, 0.07f, "#5F4F3F"); Box(b, "PostR", 0.55f, 0.8f, 0f, 0.07f, 1.6f, 0.07f, "#5F4F3F");
                Box(b, "Frame", 0f, 1.2f, 0f, 1.28f, 0.88f, 0.03f, "#5F4F3F", false);
                Transform board = Box(b, "Board", 0f, 1.2f, 0f, 1.2f, 0.8f, 0.044f, "#FFFFFF", false);
                Material mm = new Material(MaterialFactory.Get(Color.white));
                Texture2D mt = MapTexture();
                mm.mainTexture = mt;
                if (mm.HasProperty("_BaseMap")) mm.SetTexture("_BaseMap", mt);
                if (mm.HasProperty("_BaseColor")) mm.SetColor("_BaseColor", Color.white);
                mm.color = Color.white;
                board.GetComponent<Renderer>().sharedMaterial = mm;
                break;
            case "alien_tourist":
                Ufo(b, -3.3f, -0.3f);   // further from the alien so the two never clip
                break;
            case "flying_documents":
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f * Mathf.Deg2Rad, r = 0.6f + (i % 3) * 0.5f;
                    P(b, "Paper", PrimitiveKind.Cube, Mathf.Sin(a) * r, 0.01f + i * 0.002f, Mathf.Cos(a) * r, 0.21f, 0.004f, 0.3f, "#F4F4EE", false, 0f, i * 47f, 0f);
                }
                Transform pp = Box(b, "HeldPaper", 0f, 0.016f, 0f, 0.21f, 0.004f, 0.3f, "#F4F4EE", false);
                Hold(pp, true, 0f, 0.0f, 0.12f, 0f, 0f, 0f, 0f, 0.0f);
                break;
            case "lost_wallet":
                Transform w = G(b, "Wallet", 0f, 0.02f, 0f);
                Box(w, "Body", 0f, 0f, 0f, 0.2f, 0.035f, 0.12f, "#5F3F2F", false); Box(w, "Card", 0f, 0.02f, 0f, 0.14f, 0.005f, 0.08f, "#F0E0A0", false);
                HoldBoth(w, 0.1f);
                break;
            default:
                P(b, "Prop", c.propKind, 0f, s.y * 0.5f, 0f, s.x, s.y, s.z, "#" + ColorUtility.ToHtmlStringRGB(c.propColor));
                break;
        }
    }
}

/// <summary>Pigeons for pigeon_king. They mill about pecking at the ground. When the player feeds them, a bread piece is taken out, tossed
/// and breaks into crumbs that arc to the ground, bounce and settle. The nearest birds notice, trot over with a head-bob, claim one crumb
/// each and peck it away bite by bite; late birds flutter in. The flock stays afterwards and can be fed again at any time.</summary>
public class PigeonFlock : MonoBehaviour, IInteractable
{
    sealed class Crumb
    {
        public Transform t; public Vector3 from, to; public float flightT, flightLen, arc, amount = 1f, size, age, bounce;
        public B claim;
        public bool Landed { get { return flightT >= 1f; } }
    }

    sealed class B
    {
        public Transform t; public FlyerRig r;
        public int state, sub;                 // state: 0 idle, 1 notice, 2 trot, 3 eat; sub (idle): 0 look, 1 peck, 2 wander
        public float ph, subT, noticeT, pk, prevU, step, h, vy, flutter, eatGap, speed;
        public Vector3 home, goal;
        public Crumb target;
        public Quaternion w0, w1, body0;
        public Vector3 head0, body0p;
        public float scale = 1f;
    }

    readonly List<B> bs = new List<B>();
    readonly List<Crumb> crumbs = new List<Crumb>();
    readonly System.Random rng = new System.Random(917);
    WorldEvent ev;
    bool init, registered, holding, released;
    float holdShow, nextFeed, spawned, eaten, settled, eatenTotal, agit = 1f, twitch;
    int throws;
    const float CalmNeed = 13f;   // crumbs that must be eaten to calm the flock
    Transform held;
    Vector3 center;
    float groundY;

    static readonly string[] Tints = { "#D9B77A", "#E8CC93", "#C79B5A", "#F0DCAB" };

    public bool Released { get { return released; } }
    public bool AllEaten { get { return released && crumbs.Count == 0; } }
    public float EatenFraction { get { return spawned > 0f ? Mathf.Clamp01(eaten / spawned) : 0f; } }
    public bool SettledFor(float s) { return settled >= s; }
    public int Throws { get { return throws; } }
    public bool Holding { get { return holding; } }
    public bool CanThrow { get { return init && !holding && Time.time >= nextFeed; } }
    public bool Calmed { get { return eatenTotal >= CalmNeed; } }
    /// <summary>0 = calm, 1 = agitated. Agitated until the flock has eaten enough crumbs; always calm once the event is over.</summary>
    public float Agitation { get { return agit; } }

    public void Add(Transform t, FlyerRig r)
    {
        B b = new B { t = t, r = r, ph = Random.value * 6.28f, subT = Random.value * 2f, sub = Random.Range(0, 3), speed = Random.Range(0.95f, 1.35f) };
        bs.Add(b);
    }

    // ---------- open-world interaction (after the event is over) ----------
    public Vector3 Position { get { return center; } }
    public float Range { get { return 3.2f; } }
    public string Prompt { get { return "[ PRESS E ] Feed the pigeons"; } }

    public bool CanInteract
    {
        get
        {
            InteractionSystem sys = InteractionSystem.Instance;
            return ev != null && ev.Done && init && Time.time >= nextFeed && !holding && crumbs.Count == 0 && sys != null && !sys.Suppressed;
        }
    }

    public void Interact() { if (CanInteract) StartThrow(); }

    void OnDestroy()
    {
        if (registered && InteractionSystem.Instance != null) InteractionSystem.Instance.Unregister(this);
        if (held != null) WorldUtil.Kill(held.gameObject);
    }

    // ---------- throwing ----------
    public void StartThrow()
    {
        NpcDirector d = NpcDirector.Instance;
        if (d == null || d.Player == null) return;
        Vector3 pp = d.Player.position;
        Vector3 to = center - pp; to.y = 0f;
        float dist = to.magnitude;
        Vector3 dir = dist > 0.1f ? to / dist : d.Player.forward;
        // land the crumbs a little short of the flock so the nearest birds reach them first
        Vector3 target = pp + dir * Mathf.Clamp(dist - 0.7f, 1.8f, 3.4f);
        target.y = GroundAt(target);
        holding = true; released = false; settled = 0f;
        holdShow = Time.time + 0.5f;                      // bread appears once the hand is out of the pocket
        nextFeed = Time.time + 4f;
        throws++;
        if (crumbs.Count == 0) { spawned = 0f; eaten = 0f; }
        FPAnimation.FPEvents.RaiseFeedStarted(target, delegate { Release(target); });
    }

    float GroundAt(Vector3 p)
    {
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out hit, 5f, ~0, QueryTriggerInteraction.Ignore)) return hit.point.y;
        return groundY;
    }

    void Release(Vector3 target)
    {
        Vector3 hand = held != null && held.gameObject.activeSelf ? held.position :
            (FPAnimation.FPEvents.HandR != null ? FPAnimation.FPEvents.HandR.position : target + Vector3.up * 1.2f);
        holding = false;
        if (held != null) held.gameObject.SetActive(false);
        int n = 13;
        for (int i = 0; i < n; i++)
        {
            // a flick of the fingers: most pieces land in a tight patch, a few roll or fly further
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Pow((float)rng.NextDouble(), 0.7f) * 0.6f;
            Vector3 p = target + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r * 1.2f);
            p.y = GroundAt(p);
            AddCrumb(hand, p, i * 0.02f);
        }
        released = true;
    }

    void AddCrumb(Vector3 from, Vector3 to, float delay)
    {
        GameObject g = PrimitiveFactory.Create(rng.Next(3) == 0 ? PrimitiveKind.Sphere : PrimitiveKind.Cube, transform);
        g.name = "Crumb";
        Collider c = g.GetComponent<Collider>();
        if (c != null) WorldUtil.Kill(c);
        g.layer = 2;
        Color col;
        ColorUtility.TryParseHtmlString(Tints[rng.Next(Tints.Length)], out col);
        Renderer rd = g.GetComponent<Renderer>();
        if (rd != null) rd.sharedMaterial = MaterialFactory.Get(col);
        float sz = 0.014f + (float)rng.NextDouble() * 0.018f;
        Crumb cr = new Crumb { t = g.transform, from = from, to = to, size = sz };
        cr.flightLen = 0.38f + (to - from).magnitude * 0.07f;
        cr.arc = 0.18f + (to - from).magnitude * 0.09f;
        cr.flightT = -delay / cr.flightLen;
        g.transform.position = from;
        g.transform.localScale = new Vector3(sz, sz * 0.6f, sz * (0.7f + (float)rng.NextDouble() * 0.6f));
        g.transform.rotation = Random.rotation;
        g.SetActive(delay <= 0f);
        crumbs.Add(cr);
        spawned += 1f;
    }

    // ---------- update ----------
    void Init()
    {
        init = true;
        groundY = transform.position.y;
        Vector3 sum = Vector3.zero;
        foreach (B b in bs)
        {
            b.home = b.t.position; b.goal = b.home;
            sum += b.home;
            FlyerRig r = b.r;
            if (r == null) continue;
            if (r.wings != null && r.wings.Length > 1 && r.wings[0] != null && r.wings[1] != null) { b.w0 = r.wings[0].localRotation; b.w1 = r.wings[1].localRotation; }
            if (r.head != null) b.head0 = r.head.localPosition;
            if (r.body != null) { b.body0 = r.body.localRotation; b.body0p = r.body.localPosition; }
        }
        center = bs.Count > 0 ? sum / bs.Count : transform.position;
        held = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        held.name = "BreadPiece";
        WorldUtil.Kill(held.GetComponent<Collider>());
        held.gameObject.layer = 2;
        held.localScale = new Vector3(0.05f, 0.03f, 0.04f);
        Color c; ColorUtility.TryParseHtmlString("#E2C98F", out c);
        Renderer hr = held.GetComponent<Renderer>();
        if (hr != null) hr.sharedMaterial = MaterialFactory.Get(c);
        held.gameObject.SetActive(false);
    }

    void Update()
    {
        if (ev == null) ev = GetComponentInParent<WorldEvent>();
        if (!init) { if (bs.Count == 0) return; Init(); }
        if (!registered && ev != null && ev.Done && InteractionSystem.Instance != null) { InteractionSystem.Instance.Register(this); registered = true; }
        float dt = Time.deltaTime;
        float wantAgit = (ev != null && ev.Done) ? 0f : Mathf.Clamp01(1f - eatenTotal / CalmNeed);
        agit = Mathf.MoveTowards(agit, wantAgit, dt * 0.35f);
        UpdateCrumbs(dt);
        for (int i = 0; i < bs.Count; i++) UpdateBird(bs[i], dt, Time.time);
        settled = crumbs.Count == 0 && released ? settled + dt : 0f;
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < bs.Count; i++) sum += bs[i].t.position;
        if (bs.Count > 0) center = sum / bs.Count;
    }

    void LateUpdate()
    {
        if (held == null) return;
        bool show = holding && !released && Time.time >= holdShow && FPAnimation.FPEvents.HandR != null;
        if (show != held.gameObject.activeSelf) held.gameObject.SetActive(show);
        if (!show) return;
        Camera cam = Camera.main;
        Vector3 f = cam != null ? cam.transform.forward : Vector3.forward, u = cam != null ? cam.transform.up : Vector3.up;
        held.position = FPAnimation.FPEvents.HandR.position + f * 0.07f - u * 0.02f;
        held.rotation = Quaternion.LookRotation(f, u);
    }

    void UpdateCrumbs(float dt)
    {
        for (int i = crumbs.Count - 1; i >= 0; i--)
        {
            Crumb c = crumbs[i];
            if (c.t == null) { Drop(i); continue; }
            if (!c.Landed)
            {
                c.flightT += dt / c.flightLen;
                if (c.flightT < 0f) continue;
                if (!c.t.gameObject.activeSelf) { c.t.gameObject.SetActive(true); c.t.position = c.from; }
                float k = Mathf.Clamp01(c.flightT);
                Vector3 pos = Vector3.Lerp(c.from, c.to, k);
                pos.y += Mathf.Sin(k * Mathf.PI) * c.arc;
                c.t.position = pos;
                c.t.Rotate(420f * dt, 260f * dt, 0f);
                continue;
            }
            // a small bounce on landing, then it lies still
            c.age += dt;
            float bt = Mathf.Clamp01(c.age / 0.16f);
            c.t.position = c.to + Vector3.up * (c.size * 0.3f + Mathf.Sin(bt * Mathf.PI) * 0.035f * (1f - bt * 0.3f));
            if (bt < 1f) c.t.Rotate(180f * dt, 90f * dt, 0f);
            if (c.amount <= 0.02f || c.age > 90f) { WorldUtil.Kill(c.t.gameObject); Drop(i); }
        }
    }

    void Drop(int i)
    {
        if (crumbs[i].claim != null && crumbs[i].claim.target == crumbs[i]) crumbs[i].claim.target = null;
        crumbs.RemoveAt(i);
    }

    Crumb FreeCrumb(Vector3 from, float maxD)
    {
        Crumb best = null; float bd = maxD * maxD;
        for (int i = 0; i < crumbs.Count; i++)
        {
            Crumb c = crumbs[i];
            if (!c.Landed || c.claim != null || c.amount <= 0.02f) continue;
            Vector3 d = c.to - from; d.y = 0f;
            if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = c; }
        }
        return best;
    }

    static float Peck(float u) { return u < 0.22f ? Mathf.SmoothStep(0f, 1f, u / 0.22f) : (u < 0.42f ? 1f - Mathf.SmoothStep(0f, 1f, (u - 0.22f) / 0.2f) : 0f); }

    void UpdateBird(B b, float dt, float tm)
    {
        if (b.r == null) return;
        Vector3 pos = b.t.position;
        float headPitch = 0f, headYaw = 0f, bodyPitch = 0f, headFwd = 0f, legSwing = 0f;
        Vector3 lookDir = Vector3.zero;
        bool moving = false, flap = false;

        switch (b.state)
        {
            case 0:
            {
                if (FreeCrumb(pos, 6f) != null && released) { b.state = 1; b.noticeT = 0.15f + (float)rng.NextDouble() * 0.7f; break; }
                b.subT -= dt * (1f + agit * 2.2f);
                if (agit > 0.05f)
                {   // agitated: sudden twitches, hops and flutters
                    b.eatGap += dt;
                    if (b.eatGap > 1.2f + (float)rng.NextDouble() * 2.5f / (0.3f + agit))
                    {
                        b.eatGap = 0f;
                        if (rng.NextDouble() < 0.5 * agit) b.flutter = 0.3f;
                        b.t.rotation *= Quaternion.Euler(0f, (float)(rng.NextDouble() - 0.5) * 140f * agit, 0f);
                    }
                }
                if (b.subT <= 0f)
                {
                    b.sub = rng.Next(3);
                    b.subT = b.sub == 1 ? 1.2f + (float)rng.NextDouble() * 1.8f : 0.8f + (float)rng.NextDouble() * 1.6f;
                    if (b.sub == 2) { Vector2 o = Random.insideUnitCircle * 0.45f; b.goal = b.home + new Vector3(o.x, 0f, o.y); }
                }
                if (b.sub == 1) { b.pk += dt * (1.6f + agit * 3.4f); float u = b.pk - Mathf.Floor(b.pk); headPitch = Peck(u) * 50f; bodyPitch = Peck(u) * 14f; }
                else if (b.sub == 0) { headYaw = Mathf.Sin(tm * (1.4f + agit * 9f) + b.ph) * (38f + agit * 25f); headPitch = -3f - agit * 6f; }
                else
                {
                    Vector3 d = b.goal - pos; d.y = 0f;
                    if (d.magnitude < 0.05f) b.subT = 0f;
                    else { lookDir = d.normalized; Walk(b, lookDir, 0.45f + agit * 0.7f, dt); moving = true; headPitch = 6f; }
                }
                break;
            }
            case 1:
            {
                // head comes up and snaps toward the food before the bird commits
                Crumb c0 = FreeCrumb(pos, 6f);
                b.noticeT -= dt;
                headPitch = -10f; headYaw = Mathf.Sin(tm * 9f + b.ph) * 8f;
                if (c0 != null) { Vector3 d = c0.to - pos; d.y = 0f; if (d.sqrMagnitude > 0.001f) lookDir = d.normalized; }
                if (b.noticeT <= 0f)
                {
                    if (c0 == null) { b.state = 0; break; }
                    b.target = c0; c0.claim = b; b.state = 2;
                    for (int i = 0; i < bs.Count; i++) if (bs[i] != b && bs[i].state == 3 && (bs[i].t.position - c0.to).sqrMagnitude < 0.04f) b.flutter = 0.35f;
                }
                break;
            }
            case 2:
            {
                if (b.target == null || b.target.amount <= 0.02f) { b.target = null; b.state = 1; b.noticeT = 0.1f; break; }
                Vector3 beak = b.target.to; Vector3 d = beak - pos; d.y = 0f;
                float dist = d.magnitude;
                if (dist < 0.001f) { b.state = 3; break; }
                lookDir = d / dist;
                Vector3 stopAt = beak - lookDir * 0.085f * b.scale;
                float rest = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(stopAt.x, 0f, stopAt.z));
                if (Vector3.Dot(stopAt - pos, lookDir) <= 0.01f || rest < 0.03f) { b.state = 3; b.pk = 0f; b.prevU = 0f; break; }
                float spd = b.speed * Mathf.Clamp(dist * 1.2f, 0.45f, 1.3f);
                Walk(b, lookDir, spd, dt); moving = true; legSwing = 1f;
                headPitch = 8f;
                break;
            }
            default:
            {
                Crumb c = b.target;
                if (c == null || c.amount <= 0.02f)
                {
                    b.eatGap += dt;
                    headPitch = -6f;
                    if (b.eatGap > 0.18f)
                    {
                        b.eatGap = 0f; b.target = null;
                        Crumb nx = FreeCrumb(pos, 1.3f);
                        if (nx != null) { b.target = nx; nx.claim = b; b.state = 2; }
                        else { b.state = 0; b.subT = 0.5f; b.home = pos; }
                    }
                    break;
                }
                Vector3 d = c.to - pos; d.y = 0f;
                if (d.sqrMagnitude > 0.0001f) lookDir = d.normalized;
                b.pk += dt * (2.4f + 0.6f * (b.ph % 1f));
                float u = b.pk - Mathf.Floor(b.pk);
                float pkv = Peck(u);
                headPitch = pkv * 62f; bodyPitch = pkv * 20f; headFwd = pkv * 0.012f;
                if (b.prevU < 0.22f && u >= 0.22f) { c.amount -= 0.36f; { float bite = Mathf.Min(0.36f, c.amount + 0.36f); eaten += bite; eatenTotal += bite; } c.t.localScale *= 0.8f; }
                b.prevU = u;
                break;
            }
        }

        if (lookDir.sqrMagnitude > 0.001f)
        {
            float rate = b.state == 2 ? 520f : 360f;
            b.t.rotation = Quaternion.RotateTowards(b.t.rotation, Quaternion.LookRotation(lookDir), rate * dt);
        }

        // short flutter-hop (squabbling for a crumb)
        if (b.flutter > 0f)
        {
            b.flutter -= dt; flap = true;
            if (b.h <= 0.001f && b.vy == 0f) b.vy = 1.5f;
        }
        if (b.vy != 0f || b.h > 0f) { b.vy -= 9f * dt; b.h += b.vy * dt; if (b.h <= 0f) { b.h = 0f; b.vy = 0f; } }

        // pose
        float bob = moving ? Mathf.Abs(Mathf.Sin(b.step)) * 0.004f : 0f;
        b.t.position = new Vector3(b.t.position.x, groundY + bob + b.h, b.t.position.z);
        if (b.r.head != null)
        {
            if (moving) { headFwd = Mathf.Sin(b.step) * 0.012f; }
            b.r.head.localRotation = Quaternion.Euler(headPitch, headYaw, 0f);
            b.r.head.localPosition = b.head0 + new Vector3(0f, 0f, headFwd);
        }
        if (b.r.body != null) b.r.body.localRotation = Quaternion.Slerp(b.r.body.localRotation, b.body0 * Quaternion.Euler(bodyPitch, 0f, 0f), 1f - Mathf.Exp(-14f * dt));
        if (b.r.legs != null && b.r.legs.Length > 1 && b.r.legs[0] != null && b.r.legs[1] != null)
        {
            float sw = moving ? Mathf.Sin(b.step) * 35f : 0f;
            if (b.h > 0.001f) sw = -25f;
            b.r.legs[0].localRotation = Quaternion.Euler(sw, 0f, 0f);
            b.r.legs[1].localRotation = Quaternion.Euler(b.h > 0.001f ? sw : -sw, 0f, 0f);
        }
        if (b.r.wings != null && b.r.wings.Length > 1 && b.r.wings[0] != null && b.r.wings[1] != null)
        {
            float f = flap || b.h > 0.001f ? Mathf.Sin(tm * 38f + b.ph) * 50f : 0f;
            b.r.wings[0].localRotation = b.w0 * Quaternion.Euler(0f, 0f, f);
            b.r.wings[1].localRotation = b.w1 * Quaternion.Euler(0f, 0f, -f);
        }
    }

    void Walk(B b, Vector3 dir, float speed, float dt)
    {
        b.t.position += dir * speed * dt;
        b.step += speed * dt * 9f;
    }
}
