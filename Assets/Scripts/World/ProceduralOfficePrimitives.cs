using UnityEngine;

/// <summary>
/// Helper factory that generates low-poly stylized procedural furniture and office props
/// using Unity primitives and dynamic materials. Used as a high-quality fallback whenever
/// custom 3D prefabs are not assigned in the InterviewRoomCatalog.
/// </summary>
public static class ProceduralOfficePrimitives
{
    // Common Office Color Palettes
    public static readonly Color WoodDark = new Color(0.24f, 0.15f, 0.09f);
    public static readonly Color WoodWarm = new Color(0.48f, 0.31f, 0.18f);
    public static readonly Color WoodLight = new Color(0.72f, 0.58f, 0.42f);
    public static readonly Color MetalDark = new Color(0.16f, 0.16f, 0.18f);
    public static readonly Color MetalChrome = new Color(0.65f, 0.67f, 0.70f);
    public static readonly Color LeatherBlack = new Color(0.12f, 0.12f, 0.13f);
    public static readonly Color LeatherBrown = new Color(0.32f, 0.20f, 0.12f);
    public static readonly Color FabricGrey = new Color(0.35f, 0.37f, 0.40f);
    public static readonly Color FabricNavy = new Color(0.12f, 0.18f, 0.28f);
    public static readonly Color OfficeWhite = new Color(0.92f, 0.93f, 0.94f);
    public static readonly Color PlantGreen = new Color(0.18f, 0.45f, 0.22f);
    public static readonly Color PlantPotTerra = new Color(0.68f, 0.38f, 0.25f);
    public static readonly Color LampGold = new Color(0.78f, 0.65f, 0.25f);
    public static readonly Color ScreenGlow = new Color(0.4f, 0.75f, 0.95f);

    public static Material GetMaterial(Color color, float smoothness = 0.2f)
    {
        Material m = MaterialFactory.Get(color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    public static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 localScale, Color color, float smoothness = 0.2f)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = GetMaterial(color, smoothness);
        return go;
    }

    public static GameObject CreateCylinder(string name, Transform parent, Vector3 localPos, Vector3 localScale, Color color, float smoothness = 0.2f)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = GetMaterial(color, smoothness);
        return go;
    }

    public static GameObject CreateSphere(string name, Transform parent, Vector3 localPos, Vector3 localScale, Color color, float smoothness = 0.2f)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = GetMaterial(color, smoothness);
        return go;
    }

    // -------------------------------------------------------------------------
    // 1. DESK
    // -------------------------------------------------------------------------
    public static GameObject BuildDesk(Transform parent, System.Random rng, out Vector3 deskTopCenter, out Vector3 deskSize)
    {
        GameObject root = new GameObject("ProceduralDesk");
        root.transform.SetParent(parent, false);

        float width = 2.1f + (float)rng.NextDouble() * 0.4f;   // 2.1m - 2.5m
        float depth = 0.95f + (float)rng.NextDouble() * 0.25f; // 0.95m - 1.2m
        float height = 0.75f;
        float topThickness = 0.05f;

        deskSize = new Vector3(width, height + topThickness, depth);
        deskTopCenter = new Vector3(0f, height + topThickness, 0f);

        // Pick desk style: 0 = Executive Wood, 1 = Modern Steel Legs, 2 = Minimal White
        int style = rng.Next(3);
        Color topColor = style == 0 ? (rng.Next(2) == 0 ? WoodDark : WoodWarm) : (style == 1 ? WoodLight : OfficeWhite);
        Color legColor = style == 1 ? MetalDark : (style == 0 ? topColor * 0.85f : MetalChrome);

        // Tabletop
        CreateBox("TableTop", root.transform, new Vector3(0f, height + topThickness * 0.5f, 0f), new Vector3(width, topThickness, depth), topColor, 0.45f);

        // Legs / Modesty Panel
        if (style == 0)
        {
            // Executive: Solid side panels + front modesty panel
            float sideThick = 0.06f;
            CreateBox("SideLeft", root.transform, new Vector3(-width * 0.5f + sideThick * 0.5f, height * 0.5f, 0f), new Vector3(sideThick, height, depth * 0.95f), legColor);
            CreateBox("SideRight", root.transform, new Vector3(width * 0.5f - sideThick * 0.5f, height * 0.5f, 0f), new Vector3(sideThick, height, depth * 0.95f), legColor);
            // Front modesty panel facing candidate (positive Z)
            CreateBox("ModestyPanel", root.transform, new Vector3(0f, height * 0.55f, depth * 0.25f), new Vector3(width * 0.85f, height * 0.65f, 0.03f), topColor * 0.9f);
        }
        else
        {
            // Modern / Minimal: 4 square metal legs or 2 loop frames
            float legW = 0.05f;
            float ox = width * 0.5f - 0.08f;
            float oz = depth * 0.5f - 0.08f;
            CreateBox("Leg_BL", root.transform, new Vector3(-ox, height * 0.5f, -oz), new Vector3(legW, height, legW), legColor, 0.7f);
            CreateBox("Leg_BR", root.transform, new Vector3(ox, height * 0.5f, -oz), new Vector3(legW, height, legW), legColor, 0.7f);
            CreateBox("Leg_FL", root.transform, new Vector3(-ox, height * 0.5f, oz), new Vector3(legW, height, legW), legColor, 0.7f);
            CreateBox("Leg_FR", root.transform, new Vector3(ox, height * 0.5f, oz), new Vector3(legW, height, legW), legColor, 0.7f);
        }

        // Add overall box collider for table bounds
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, (height + topThickness) * 0.5f, 0f);
        col.size = new Vector3(width, height + topThickness, depth);

        return root;
    }

    // -------------------------------------------------------------------------
    // 2. CHAIRS
    // -------------------------------------------------------------------------
    public static GameObject BuildInterviewerChair(Transform parent, System.Random rng)
    {
        GameObject root = new GameObject("ProceduralInterviewerChair");
        root.transform.SetParent(parent, false);

        Color leather = rng.Next(2) == 0 ? LeatherBlack : LeatherBrown;
        Color metal = rng.Next(2) == 0 ? MetalChrome : MetalDark;

        // Base post & wheels star
        CreateCylinder("BasePost", root.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.08f, 0.15f, 0.08f), metal, 0.7f);
        CreateCylinder("StarBase", root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.55f, 0.03f, 0.55f), metal, 0.6f);

        // Seat Cushion
        float seatY = 0.48f;
        CreateBox("Seat", root.transform, new Vector3(0f, seatY, 0f), new Vector3(0.56f, 0.09f, 0.54f), leather, 0.3f);

        // High Backrest
        CreateBox("Backrest", root.transform, new Vector3(0f, seatY + 0.42f, -0.24f), new Vector3(0.52f, 0.75f, 0.08f), leather, 0.3f);
        // Headrest extension
        CreateBox("Headrest", root.transform, new Vector3(0f, seatY + 0.84f, -0.22f), new Vector3(0.38f, 0.16f, 0.09f), leather * 0.9f, 0.3f);

        // Armrests
        CreateBox("ArmL", root.transform, new Vector3(-0.31f, seatY + 0.22f, -0.02f), new Vector3(0.06f, 0.04f, 0.36f), metal, 0.6f);
        CreateBox("ArmR", root.transform, new Vector3(0.31f, seatY + 0.22f, -0.02f), new Vector3(0.06f, 0.04f, 0.36f), metal, 0.6f);
        CreateBox("ArmPostL", root.transform, new Vector3(-0.31f, seatY + 0.11f, 0.05f), new Vector3(0.04f, 0.20f, 0.04f), metal, 0.6f);
        CreateBox("ArmPostR", root.transform, new Vector3(0.31f, seatY + 0.11f, 0.05f), new Vector3(0.04f, 0.20f, 0.04f), metal, 0.6f);

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.55f, 0f);
        col.size = new Vector3(0.68f, 1.1f, 0.68f);

        return root;
    }

    public static GameObject BuildGuestChair(Transform parent, System.Random rng)
    {
        GameObject root = new GameObject("ProceduralGuestChair");
        root.transform.SetParent(parent, false);

        Color fabric = rng.Next(3) == 0 ? FabricNavy : (rng.Next(2) == 0 ? FabricGrey : LeatherBlack);
        Color frame = MetalChrome;

        float seatY = 0.45f;
        // Seat Cushion
        CreateBox("Seat", root.transform, new Vector3(0f, seatY, 0f), new Vector3(0.50f, 0.07f, 0.48f), fabric, 0.2f);
        // Backrest
        CreateBox("Backrest", root.transform, new Vector3(0f, seatY + 0.32f, -0.21f), new Vector3(0.48f, 0.48f, 0.06f), fabric, 0.2f);

        // Sled Frame / 4 Legs
        float legW = 0.035f;
        CreateBox("LegFL", root.transform, new Vector3(-0.22f, seatY * 0.5f, 0.20f), new Vector3(legW, seatY, legW), frame, 0.8f);
        CreateBox("LegFR", root.transform, new Vector3(0.22f, seatY * 0.5f, 0.20f), new Vector3(legW, seatY, legW), frame, 0.8f);
        CreateBox("LegBL", root.transform, new Vector3(-0.22f, seatY * 0.5f, -0.20f), new Vector3(legW, seatY, legW), frame, 0.8f);
        CreateBox("LegBR", root.transform, new Vector3(0.22f, seatY * 0.5f, -0.20f), new Vector3(legW, seatY, legW), frame, 0.8f);

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.45f, 0f);
        col.size = new Vector3(0.55f, 0.90f, 0.55f);

        return root;
    }

    // -------------------------------------------------------------------------
    // 3. DESK CLUTTER & ACCESSORIES
    // -------------------------------------------------------------------------
    public static GameObject BuildDeskLamp(Transform parent, System.Random rng, bool attachLight = true)
    {
        GameObject root = new GameObject("ProceduralDeskLamp");
        root.transform.SetParent(parent, false);

        Color lampColor = rng.Next(2) == 0 ? MetalDark : LampGold;

        // Base
        CreateCylinder("Base", root.transform, new Vector3(0f, 0.012f, 0f), new Vector3(0.18f, 0.024f, 0.18f), lampColor, 0.7f);
        
        // Stem angled slightly forward
        GameObject arm = CreateCylinder("Arm", root.transform, new Vector3(0f, 0.20f, 0.04f), new Vector3(0.025f, 0.20f, 0.025f), lampColor, 0.7f);
        arm.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);

        // Lampshade head angled DOWNWARDS towards the desk
        Vector3 shadePos = new Vector3(0f, 0.38f, 0.10f);
        GameObject shade = CreateCylinder("Shade", root.transform, shadePos, new Vector3(0.14f, 0.09f, 0.14f), lampColor, 0.6f);
        shade.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);

        // Bulb / Light Source pointing directly downwards onto the desk
        if (attachLight)
        {
            GameObject lightGo = new GameObject("LampSpotlight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = shadePos;
            // Point forward and down onto the desk surface
            lightGo.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);

            Light spot = lightGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 2.8f;
            spot.spotAngle = 55f;
            spot.innerSpotAngle = 30f;
            spot.color = new Color(1.0f, 0.90f, 0.70f); // Warm incandescent lamp tone
            spot.intensity = 1.8f;
            spot.shadows = LightShadows.Soft;
        }

        return root;
    }

    public static GameObject BuildLaptop(Transform parent, System.Random rng, bool open = true)
    {
        GameObject root = new GameObject("ProceduralLaptop");
        root.transform.SetParent(parent, false);

        Color chassis = rng.Next(2) == 0 ? MetalDark : MetalChrome;

        // Base & keyboard (front is +Z towards user, back hinge is -Z)
        CreateBox("Chassis", root.transform, new Vector3(0f, 0.008f, 0f), new Vector3(0.36f, 0.016f, 0.25f), chassis, 0.5f);
        CreateBox("Keyboard", root.transform, new Vector3(0f, 0.017f, -0.02f), new Vector3(0.30f, 0.004f, 0.14f), MetalDark * 0.7f, 0.2f);
        CreateBox("Trackpad", root.transform, new Vector3(0f, 0.017f, 0.08f), new Vector3(0.10f, 0.002f, 0.06f), chassis * 0.9f, 0.3f);

        if (open)
        {
            // Upright screen tilted slightly back from vertical
            GameObject lid = new GameObject("LidPivot");
            lid.transform.SetParent(root.transform, false);
            lid.transform.localPosition = new Vector3(0f, 0.016f, -0.125f);
            lid.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f); // tilted slightly back

            CreateBox("ScreenBezel", lid.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.36f, 0.24f, 0.012f), chassis, 0.5f);
            // Display glowing face looking forward towards the keyboard / user (+Z)
            CreateBox("DisplayGlow", lid.transform, new Vector3(0f, 0.12f, 0.007f), new Vector3(0.33f, 0.21f, 0.002f), ScreenGlow, 0.8f);
        }
        else
        {
            // Closed lid
            CreateBox("LidClosed", root.transform, new Vector3(0f, 0.022f, 0f), new Vector3(0.36f, 0.012f, 0.25f), chassis, 0.5f);
        }

        return root;
    }

    public static GameObject BuildCoffeeMug(Transform parent, System.Random rng)
    {
        GameObject root = new GameObject("ProceduralCoffeeMug");
        root.transform.SetParent(parent, false);

        Color[] mugColors = { OfficeWhite, new Color(0.15f, 0.35f, 0.65f), new Color(0.75f, 0.2f, 0.15f), MetalDark };
        Color mugColor = mugColors[rng.Next(mugColors.Length)];

        // Cup Body
        CreateCylinder("Cup", root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.085f, 0.05f, 0.085f), mugColor, 0.5f);
        // Liquid
        CreateCylinder("Coffee", root.transform, new Vector3(0f, 0.095f, 0f), new Vector3(0.075f, 0.004f, 0.075f), new Color(0.18f, 0.10f, 0.06f), 0.7f);
        // Handle
        CreateBox("Handle", root.transform, new Vector3(0.055f, 0.05f, 0f), new Vector3(0.035f, 0.055f, 0.015f), mugColor, 0.5f);

        return root;
    }

    public static GameObject BuildPaperStack(Transform parent, System.Random rng)
    {
        GameObject root = new GameObject("ProceduralPaperStack");
        root.transform.SetParent(parent, false);

        Color folderColor = rng.Next(2) == 0 ? new Color(0.85f, 0.75f, 0.52f) : OfficeWhite; // Manila or white
        CreateBox("Folder", root.transform, new Vector3(0f, 0.01f, 0f), new Vector3(0.24f, 0.02f, 0.32f), folderColor, 0.1f);
        // Few scattered sheets on top
        GameObject sheet = CreateBox("Sheet", root.transform, new Vector3(0.01f, 0.022f, 0.01f), new Vector3(0.21f, 0.004f, 0.297f), OfficeWhite, 0.1f);
        sheet.transform.localRotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 8.0 - 4.0), 0f);

        return root;
    }

    public static GameObject BuildPenHolder(Transform parent, System.Random rng)
    {
        GameObject root = new GameObject("ProceduralPenHolder");
        root.transform.SetParent(parent, false);

        Color pot = MetalDark;
        CreateCylinder("Cup", root.transform, new Vector3(0f, 0.055f, 0f), new Vector3(0.07f, 0.055f, 0.07f), pot, 0.5f);
        // Pens sticking out
        Color[] penColors = { Color.black, Color.blue, Color.red };
        for (int i = 0; i < 3; i++)
        {
            GameObject pen = CreateCylinder("Pen_" + i, root.transform, new Vector3((i - 1) * 0.015f, 0.09f, 0f), new Vector3(0.01f, 0.06f, 0.01f), penColors[i]);
            pen.transform.localRotation = Quaternion.Euler(10f * (i - 1), 0f, 15f * (i - 1));
        }

        return root;
    }

    // -------------------------------------------------------------------------
    // 4. CORNER PROPS (Plants, Filing Cabinets, Water Cooler)
    // -------------------------------------------------------------------------
    public static GameObject BuildPlant(Transform parent, System.Random rng)
    {
        GameObject root = new GameObject("ProceduralPlant");
        root.transform.SetParent(parent, false);

        Color potColor = rng.Next(2) == 0 ? OfficeWhite : PlantPotTerra;
        float potH = 0.45f;
        float potR = 0.35f;

        // Pot
        CreateCylinder("Pot", root.transform, new Vector3(0f, potH * 0.5f, 0f), new Vector3(potR, potH * 0.5f, potR), potColor, 0.4f);
        // Soil
        CreateCylinder("Soil", root.transform, new Vector3(0f, potH * 0.95f, 0f), new Vector3(potR * 0.92f, 0.02f, potR * 0.92f), new Color(0.18f, 0.12f, 0.08f));

        // Snake plant / Vertical blade foliage
        int leaves = 6 + rng.Next(4);
        for (int i = 0; i < leaves; i++)
        {
            float angle = (360f / leaves) * i + (float)rng.NextDouble() * 15f;
            float leafH = 0.65f + (float)rng.NextDouble() * 0.45f;
            float leafW = 0.08f + (float)rng.NextDouble() * 0.03f;
            Vector3 leafPos = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * 0.08f, potH + leafH * 0.5f, Mathf.Cos(angle * Mathf.Deg2Rad) * 0.08f);

            GameObject leaf = CreateBox("Leaf_" + i, root.transform, leafPos, new Vector3(leafW, leafH, 0.015f), PlantGreen * (0.85f + (float)rng.NextDouble() * 0.3f), 0.3f);
            leaf.transform.localRotation = Quaternion.Euler(Mathf.Cos(angle * Mathf.Deg2Rad) * 8f, angle, Mathf.Sin(angle * Mathf.Deg2Rad) * 8f);
        }

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.7f, 0f);
        col.size = new Vector3(0.55f, 1.4f, 0.55f);

        return root;
    }

    public static GameObject BuildFilingCabinet(Transform parent, System.Random rng)
    {
        GameObject root = new GameObject("ProceduralFilingCabinet");
        root.transform.SetParent(parent, false);

        Color metal = rng.Next(2) == 0 ? MetalDark : OfficeWhite;
        float w = 0.52f;
        float h = 1.25f;
        float d = 0.62f;

        // Main body
        CreateBox("Body", root.transform, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), metal, 0.4f);

        // 3 Drawer front lines & handles
        int drawers = 3;
        float drawerH = (h - 0.1f) / drawers;
        for (int i = 0; i < drawers; i++)
        {
            float dy = 0.05f + drawerH * 0.5f + i * drawerH;
            // Handle
            CreateBox("Handle_" + i, root.transform, new Vector3(0f, dy, d * 0.5f + 0.015f), new Vector3(0.14f, 0.025f, 0.02f), MetalChrome, 0.8f);
        }

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, h * 0.5f, 0f);
        col.size = new Vector3(w, h, d);

        return root;
    }

    public static GameObject BuildWaterCooler(Transform parent, System.Random rng)
    {
        GameObject root = new GameObject("ProceduralWaterCooler");
        root.transform.SetParent(parent, false);

        // Base pedestal
        float baseH = 0.95f;
        CreateBox("Pedestal", root.transform, new Vector3(0f, baseH * 0.5f, 0f), new Vector3(0.38f, baseH, 0.38f), OfficeWhite, 0.3f);

        // Dispenser recess
        CreateBox("Alcove", root.transform, new Vector3(0f, baseH * 0.75f, 0.16f), new Vector3(0.24f, 0.22f, 0.08f), MetalDark, 0.4f);
        // Red/Blue spigots
        CreateBox("ColdTap", root.transform, new Vector3(-0.05f, baseH * 0.82f, 0.17f), new Vector3(0.02f, 0.04f, 0.04f), Color.blue);
        CreateBox("HotTap", root.transform, new Vector3(0.05f, baseH * 0.82f, 0.17f), new Vector3(0.02f, 0.04f, 0.04f), Color.red);

        // Blue water jug on top
        Color jugColor = new Color(0.2f, 0.55f, 0.85f, 0.75f);
        CreateCylinder("Jug", root.transform, new Vector3(0f, baseH + 0.22f, 0f), new Vector3(0.30f, 0.22f, 0.30f), jugColor, 0.8f);

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.7f, 0f);
        col.size = new Vector3(0.42f, 1.45f, 0.42f);

        return root;
    }

    // -------------------------------------------------------------------------
    // 5. WALL DECOR (Whiteboard, Clock, Diplomas / Art)
    // -------------------------------------------------------------------------
    public static GameObject BuildWhiteboard(Transform parent, Vector3 size)
    {
        GameObject root = new GameObject("ProceduralWhiteboard");
        root.transform.SetParent(parent, false);

        // Board
        CreateBox("Board", root.transform, Vector3.zero, size, OfficeWhite, 0.6f);
        // Aluminum frame
        float fThick = 0.04f;
        CreateBox("FrameTop", root.transform, new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x + fThick * 2f, fThick, size.z * 1.5f), MetalChrome, 0.6f);
        CreateBox("FrameBottom", root.transform, new Vector3(0f, -size.y * 0.5f, 0f), new Vector3(size.x + fThick * 2f, fThick, size.z * 1.5f), MetalChrome, 0.6f);
        CreateBox("FrameLeft", root.transform, new Vector3(-size.x * 0.5f, 0f, 0f), new Vector3(fThick, size.y, size.z * 1.5f), MetalChrome, 0.6f);
        CreateBox("FrameRight", root.transform, new Vector3(size.x * 0.5f, 0f, 0f), new Vector3(fThick, size.y, size.z * 1.5f), MetalChrome, 0.6f);

        // Marker tray
        CreateBox("Tray", root.transform, new Vector3(0f, -size.y * 0.5f - 0.03f, 0.05f), new Vector3(size.x * 0.6f, 0.02f, 0.08f), MetalDark, 0.5f);

        return root;
    }

    public static GameObject BuildWallClock(Transform parent)
    {
        GameObject root = new GameObject("ProceduralWallClock");
        root.transform.SetParent(parent, false);

        // Rim
        GameObject rim = CreateCylinder("Rim", root.transform, Vector3.zero, new Vector3(0.42f, 0.02f, 0.42f), MetalDark, 0.6f);
        rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Clock Face
        GameObject face = CreateCylinder("Face", root.transform, new Vector3(0f, 0f, 0.015f), new Vector3(0.38f, 0.005f, 0.38f), OfficeWhite, 0.2f);
        face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Hands
        CreateBox("HourHand", root.transform, new Vector3(0.04f, 0.04f, 0.022f), new Vector3(0.08f, 0.015f, 0.005f), Color.black);
        CreateBox("MinuteHand", root.transform, new Vector3(0f, 0.07f, 0.022f), new Vector3(0.012f, 0.14f, 0.005f), Color.black);

        return root;
    }

    public static GameObject BuildWallArt(Transform parent, Vector3 size, System.Random rng)
    {
        GameObject root = new GameObject("ProceduralWallArt");
        root.transform.SetParent(parent, false);

        Color frameColor = rng.Next(2) == 0 ? MetalDark : WoodDark;
        Color canvasColor = rng.Next(3) == 0 ? new Color(0.85f, 0.82f, 0.75f) : (rng.Next(2) == 0 ? FabricNavy * 1.5f : new Color(0.65f, 0.35f, 0.25f));

        CreateBox("Frame", root.transform, Vector3.zero, size, frameColor, 0.3f);
        CreateBox("Canvas", root.transform, new Vector3(0f, 0f, 0.01f), new Vector3(size.x - 0.08f, size.y - 0.08f, size.z * 1.1f), canvasColor, 0.1f);

        return root;
    }

    // -------------------------------------------------------------------------
    // 6. SEATED INTERVIEWER (Low-Poly Humanoid)
    // -------------------------------------------------------------------------
    public static GameObject BuildSeatedInterviewer(Transform parent, System.Random rng)
    {
        GameObject root = new GameObject("ProceduralInterviewerAvatar");
        root.transform.SetParent(parent, false);

        Color[] suits = { MetalDark, new Color(0.12f, 0.18f, 0.32f), new Color(0.22f, 0.22f, 0.25f), WoodDark };
        Color suit = suits[rng.Next(suits.Length)];
        Color shirt = rng.Next(2) == 0 ? OfficeWhite : new Color(0.85f, 0.90f, 0.95f);
        Color tie = rng.Next(3) == 0 ? new Color(0.75f, 0.15f, 0.15f) : (rng.Next(2) == 0 ? new Color(0.15f, 0.35f, 0.75f) : MetalDark);
        Color skin = AiPrimitives.RandomSkin(rng);
        Color hair = rng.Next(2) == 0 ? MetalDark : (rng.Next(2) == 0 ? WoodDark : new Color(0.6f, 0.6f, 0.65f));

        float seatH = 0.48f;

        // Thighs / Legs (seated forward towards +Z)
        float legLen = 0.44f;
        CreateBox("ThighL", root.transform, new Vector3(-0.12f, seatH + 0.08f, legLen * 0.5f), new Vector3(0.14f, 0.14f, legLen), suit);
        CreateBox("ThighR", root.transform, new Vector3(0.12f, seatH + 0.08f, legLen * 0.5f), new Vector3(0.14f, 0.14f, legLen), suit);

        // Calves (hanging down to floor)
        CreateBox("CalfL", root.transform, new Vector3(-0.12f, seatH * 0.5f, legLen), new Vector3(0.12f, seatH, 0.12f), suit);
        CreateBox("CalfR", root.transform, new Vector3(0.12f, seatH * 0.5f, legLen), new Vector3(0.12f, seatH, 0.12f), suit);

        // Torso / Suit Jacket
        float torsoH = 0.58f;
        Vector3 torsoPos = new Vector3(0f, seatH + 0.12f + torsoH * 0.5f, -0.05f);
        CreateBox("Torso", root.transform, torsoPos, new Vector3(0.44f, torsoH, 0.26f), suit);

        // Shirt V-neck & Tie
        CreateBox("ShirtChest", root.transform, torsoPos + new Vector3(0f, 0.12f, 0.132f), new Vector3(0.14f, 0.28f, 0.01f), shirt);
        CreateBox("Tie", root.transform, torsoPos + new Vector3(0f, 0.06f, 0.138f), new Vector3(0.05f, 0.24f, 0.008f), tie);

        // Arms resting forward on desk
        CreateBox("ArmL", root.transform, new Vector3(-0.25f, seatH + 0.35f, 0.15f), new Vector3(0.10f, 0.10f, 0.40f), suit);
        CreateBox("ArmR", root.transform, new Vector3(0.25f, seatH + 0.35f, 0.15f), new Vector3(0.10f, 0.10f, 0.40f), suit);
        CreateSphere("HandL", root.transform, new Vector3(-0.25f, seatH + 0.35f, 0.37f), new Vector3(0.09f, 0.08f, 0.10f), skin);
        CreateSphere("HandR", root.transform, new Vector3(0.25f, seatH + 0.35f, 0.37f), new Vector3(0.09f, 0.08f, 0.10f), skin);

        // Head & Neck
        Vector3 headPos = torsoPos + new Vector3(0f, torsoH * 0.5f + 0.14f, 0f);
        CreateCylinder("Neck", root.transform, torsoPos + new Vector3(0f, torsoH * 0.5f + 0.04f, 0f), new Vector3(0.12f, 0.06f, 0.12f), skin);
        CreateSphere("Head", root.transform, headPos, new Vector3(0.22f, 0.25f, 0.22f), skin);

        // Hair
        bool bald = rng.Next(4) == 0;
        if (!bald)
        {
            CreateSphere("Hair", root.transform, headPos + new Vector3(0f, 0.05f, -0.02f), new Vector3(0.23f, 0.18f, 0.23f), hair);
        }

        // Glasses (optional)
        if (rng.Next(2) == 0)
        {
            CreateBox("GlassesRim", root.transform, headPos + new Vector3(0f, 0.02f, 0.115f), new Vector3(0.18f, 0.04f, 0.015f), MetalDark, 0.8f);
        }

        return root;
    }
}
