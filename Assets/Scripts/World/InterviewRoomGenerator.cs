using System;
using UnityEngine;
using EasyPeasyFirstPersonController;

/// <summary>
/// Master controller for the procedural interview room.
/// Generates the room layout, furniture, interviewer, and decor dynamically.
/// Works with custom Asset Store prefabs via InterviewRoomCatalog,
/// falling back to procedural low-poly geometry if slots are unassigned.
/// </summary>
[DisallowMultipleComponent]
public class InterviewRoomGenerator : MonoBehaviour
{
    [Header("Catalog & Presets")]
    [Tooltip("Optional catalog containing your 3D Asset Store prefabs. If empty or null, procedural fallbacks will be used.")]
    public InterviewRoomCatalog catalog;

    [Header("Generation Settings")]
    [Tooltip("Generate automatically when entering the scene.")]
    public bool generateOnStart = true;

    [Tooltip("Randomize a fresh seed every time the room is generated.")]
    public bool randomizeEachPlay = true;

    [Tooltip("Seed used for deterministic generation if randomizeEachPlay is disabled.")]
    public int seed = 12345;

    [Header("Room Shell Dimensions")]
    [Range(5f, 12f)] public float roomWidth = 7.5f;
    [Range(6f, 14f)] public float roomDepth = 8.5f;
    [Range(2.8f, 5f)] public float roomHeight = 3.2f;

    [Header("Decor & Lighting Toggles")]
    public bool spawnCarpet = true;
    public bool spawnDeskLamp = true;
    public bool spawnCeilingLights = true;
    public bool spawnCornerProps = true;
    public bool spawnWallDecor = true;
    public bool spawnWindow = true;

    [Header("Existing Scene Integration")]
    [Tooltip("Reference to the placeholder table Cube (will be hidden automatically).")]
    [SerializeField] GameObject placeholderCube;

    [Tooltip("Reference to the placeholder floor Plane (will be hidden automatically).")]
    [SerializeField] GameObject placeholderPlane;

    [Tooltip("Reference to the interviewer Sphere with InterviewUI (will be positioned and hidden automatically).")]
    [SerializeField] GameObject interviewerSphere;

    const string ContainerName = "[GeneratedInterviewRoom]";
    GameObject roomContainer;

    void Awake()
    {
        if (generateOnStart && Application.isPlaying)
        {
            if (randomizeEachPlay)
            {
                seed = SaveSystem.RoomSeed(UnityEngine.Random.Range(1, int.MaxValue));
            }
            Generate();
        }
    }

    void Start()
    {
        // Fallback if not already generated in Awake
        if (roomContainer == null && generateOnStart)
        {
            if (randomizeEachPlay && Application.isPlaying)
            {
                seed = SaveSystem.RoomSeed(UnityEngine.Random.Range(1, int.MaxValue));
            }
            Generate();
        }
    }

    [ContextMenu("Regenerate Room")]
    public void Regenerate()
    {
        if (randomizeEachPlay)
        {
            seed = UnityEngine.Random.Range(1, int.MaxValue);
        }
        Generate();
    }

    [ContextMenu("Clear Room")]
    public void Clear()
    {
        Transform existing = transform.Find(ContainerName);
        if (existing != null)
        {
            if (Application.isPlaying) WorldUtil.Kill(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
    }

    public void Generate()
    {
        Clear();

        System.Random rng = new System.Random(seed);

        roomContainer = new GameObject(ContainerName);
        roomContainer.transform.SetParent(transform, false);

        // 1. Hide legacy placeholder objects and fix player height
        CleanLegacyPlaceholders();

        // 2. Build Room Shell (Walls, Floor, Ceiling)
        BuildRoomShell(rng);

        // 3. Build Center Desk & Seating
        Vector3 deskTopPos;
        Vector3 deskSize;
        Vector3 interviewerChairPos;
        BuildCenterpiece(rng, out deskTopPos, out deskSize, out interviewerChairPos);

        // 4. Build Desk Clutter & Working Lamp
        BuildDeskClutter(rng, deskTopPos, deskSize);

        // 5. Build Interviewer Avatar & Hook up InterviewUI trigger
        BuildInterviewer(rng, interviewerChairPos);

        // 6. Build Corner Props (Plants, Water Cooler, Filing Cabinets)
        if (spawnCornerProps)
        {
            BuildCornerProps(rng);
        }

        // 7. Build Wall Decor (Whiteboards, Clocks, Art, Window)
        if (spawnWallDecor)
        {
            BuildWallDecor(rng);
        }

        // 8. Ceiling Lighting Fixtures
        if (spawnCeilingLights)
        {
            BuildCeilingLighting(rng);
        }

        Debug.Log($"InterviewRoomGenerator: Generated room with seed {seed}");
    }

    void CleanLegacyPlaceholders()
    {
        // 1. Fix player scale (ensure player is normal adult height, not shrunk down to 0.56)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            FirstPersonController fpc = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            if (fpc != null) player = fpc.gameObject;
        }
        if (player != null && player.transform.localScale.y < 0.95f)
        {
            player.transform.localScale = Vector3.one;
            Debug.Log("InterviewRoomGenerator: Restored player scale to normal adult height (1.0).");
        }

        // 2. Find existing Cube and Plane in scene if not directly assigned
        if (placeholderCube == null) placeholderCube = GameObject.Find("Cube");
        if (placeholderPlane == null) placeholderPlane = GameObject.Find("Plane");
        if (interviewerSphere == null) interviewerSphere = GameObject.Find("Sphere");

        if (placeholderCube != null) placeholderCube.SetActive(false);
        if (placeholderPlane != null) placeholderPlane.SetActive(false);

        // 3. For the Sphere containing InterviewUI:
        if (interviewerSphere != null)
        {
            // Position trigger in front of the desk so player walks into it naturally
            interviewerSphere.transform.position = new Vector3(0f, 0.5f, -0.6f);

            // Hide the visible sphere mesh
            MeshRenderer mr = interviewerSphere.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;

            // Ensure collider is a trigger
            SphereCollider sc = interviewerSphere.GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = true;
                sc.radius = Mathf.Max(sc.radius, 2.8f);
            }
        }
    }

    void BuildRoomShell(System.Random rng)
    {
        GameObject shellRoot = new GameObject("RoomShell");
        shellRoot.transform.SetParent(roomContainer.transform, false);

        // Pick materials or fallbacks
        Material floorMat = catalog != null ? catalog.PickRandomMaterial(catalog.floorMaterials, rng) : null;
        Material wallMat = catalog != null ? catalog.PickRandomMaterial(catalog.wallMaterials, rng) : null;
        Material ceilingMat = catalog != null ? catalog.PickRandomMaterial(catalog.ceilingMaterials, rng) : null;

        Color wallCol = wallMat != null ? Color.white : new Color(0.88f, 0.89f, 0.90f);
        Color floorCol = floorMat != null ? Color.white : (rng.Next(2) == 0 ? new Color(0.25f, 0.26f, 0.28f) : new Color(0.42f, 0.32f, 0.22f));
        Color ceilingCol = ceilingMat != null ? Color.white : new Color(0.95f, 0.95f, 0.95f);

        // Floor
        GameObject floor = ProceduralOfficePrimitives.CreateBox("Floor", shellRoot.transform, new Vector3(0f, -0.05f, 0f), new Vector3(roomWidth, 0.1f, roomDepth), floorCol, 0.25f);
        if (floorMat != null) floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        // Ceiling
        GameObject ceiling = ProceduralOfficePrimitives.CreateBox("Ceiling", shellRoot.transform, new Vector3(0f, roomHeight + 0.05f, 0f), new Vector3(roomWidth, 0.1f, roomDepth), ceilingCol, 0.1f);
        if (ceilingMat != null) ceiling.GetComponent<Renderer>().sharedMaterial = ceilingMat;

        // Back Wall (behind interviewer, -Z)
        float wallThick = 0.15f;
        float halfD = roomDepth * 0.5f;
        float halfW = roomWidth * 0.5f;

        GameObject backWall = ProceduralOfficePrimitives.CreateBox("BackWall", shellRoot.transform, new Vector3(0f, roomHeight * 0.5f, -halfD - wallThick * 0.5f), new Vector3(roomWidth, roomHeight, wallThick), wallCol);
        if (wallMat != null) backWall.GetComponent<Renderer>().sharedMaterial = wallMat;

        // Front Wall (behind player, +Z)
        GameObject frontWall = ProceduralOfficePrimitives.CreateBox("FrontWall", shellRoot.transform, new Vector3(0f, roomHeight * 0.5f, halfD + wallThick * 0.5f), new Vector3(roomWidth, roomHeight, wallThick), wallCol);
        if (wallMat != null) frontWall.GetComponent<Renderer>().sharedMaterial = wallMat;

        // Left Wall (-X)
        GameObject leftWall = ProceduralOfficePrimitives.CreateBox("LeftWall", shellRoot.transform, new Vector3(-halfW - wallThick * 0.5f, roomHeight * 0.5f, 0f), new Vector3(wallThick, roomHeight, roomDepth), wallCol);
        if (wallMat != null) leftWall.GetComponent<Renderer>().sharedMaterial = wallMat;

        // Right Wall (+X)
        GameObject rightWall = ProceduralOfficePrimitives.CreateBox("RightWall", shellRoot.transform, new Vector3(halfW + wallThick * 0.5f, roomHeight * 0.5f, 0f), new Vector3(wallThick, roomHeight, roomDepth), wallCol);
        if (wallMat != null) rightWall.GetComponent<Renderer>().sharedMaterial = wallMat;

        // Entrance Door frame on front wall (behind player, around X=0, Z=halfD)
        BuildEntranceDoor(shellRoot.transform, new Vector3(0f, 0f, halfD - 0.02f), rng);

        // Optional Carpet under desk
        if (spawnCarpet)
        {
            GameObject carpetPrefab = catalog != null ? catalog.PickRandom(catalog.carpetPrefabs, rng) : null;
            if (carpetPrefab != null)
            {
                Instantiate(carpetPrefab, new Vector3(0f, 0.005f, 0f), Quaternion.identity, shellRoot.transform);
            }
            else
            {
                Color[] rugColors = { new Color(0.18f, 0.22f, 0.28f), new Color(0.28f, 0.18f, 0.18f), new Color(0.20f, 0.25f, 0.22f), new Color(0.32f, 0.30f, 0.28f) };
                Color rugCol = rugColors[rng.Next(rugColors.Length)];
                ProceduralOfficePrimitives.CreateBox("OfficeRug", shellRoot.transform, new Vector3(0f, 0.003f, 0.1f), new Vector3(roomWidth * 0.48f, 0.006f, roomDepth * 0.45f), rugCol, 0.05f);
            }
        }
    }

    void BuildEntranceDoor(Transform parent, Vector3 pos, System.Random rng)
    {
        GameObject doorPrefab = catalog != null ? catalog.PickRandom(catalog.doorPrefabs, rng) : null;
        if (doorPrefab != null)
        {
            Instantiate(doorPrefab, pos, Quaternion.Euler(0f, 180f, 0f), parent);
            return;
        }

        GameObject doorRoot = new GameObject("EntranceDoor");
        doorRoot.transform.SetParent(parent, false);
        doorRoot.transform.localPosition = pos;

        float doorW = 1.0f;
        float doorH = 2.2f;
        // Frame
        ProceduralOfficePrimitives.CreateBox("Frame", doorRoot.transform, new Vector3(0f, doorH * 0.5f, 0f), new Vector3(doorW + 0.12f, doorH + 0.06f, 0.04f), ProceduralOfficePrimitives.MetalDark);
        // Panel
        Color doorColor = rng.Next(2) == 0 ? ProceduralOfficePrimitives.WoodDark : ProceduralOfficePrimitives.WoodWarm;
        ProceduralOfficePrimitives.CreateBox("Panel", doorRoot.transform, new Vector3(0f, doorH * 0.5f, -0.01f), new Vector3(doorW, doorH, 0.03f), doorColor, 0.4f);
        // Handle
        ProceduralOfficePrimitives.CreateBox("Handle", doorRoot.transform, new Vector3(-doorW * 0.38f, 1.05f, -0.035f), new Vector3(0.04f, 0.14f, 0.05f), ProceduralOfficePrimitives.MetalChrome, 0.8f);
    }

    void BuildCenterpiece(System.Random rng, out Vector3 deskTopPos, out Vector3 deskSize, out Vector3 interviewerChairPos)
    {
        GameObject centerRoot = new GameObject("Centerpiece");
        centerRoot.transform.SetParent(roomContainer.transform, false);

        // 1. Desk
        GameObject deskPrefab = catalog != null ? catalog.PickRandom(catalog.deskPrefabs, rng) : null;
        if (deskPrefab != null)
        {
            // Rotate 180 degrees so drawers and knee-hole face the interviewer (-Z), and front panel faces candidate (+Z)
            GameObject deskInstance = Instantiate(deskPrefab, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), centerRoot.transform);
            Bounds b = GetObjectBounds(deskInstance);
            if (Mathf.Abs(b.min.y) > 0.01f)
            {
                deskInstance.transform.position -= new Vector3(0f, b.min.y, 0f);
                b = GetObjectBounds(deskInstance);
            }
            deskSize = b.size;
            deskTopPos = new Vector3(0f, b.max.y, 0f);
        }
        else
        {
            ProceduralOfficePrimitives.BuildDesk(centerRoot.transform, rng, out deskTopPos, out deskSize);
        }

        // 2. Interviewer Chair (Behind desk, facing +Z towards candidate)
        float chairBackZ = -Mathf.Max(deskSize.z * 0.5f, 0.45f) - 0.55f;
        interviewerChairPos = new Vector3(0f, 0f, chairBackZ);
        float chairSeatHeight = 0.46f; // Default seat height used by HumanoidSittingPose
        GameObject intChairPrefab = catalog != null ? catalog.PickRandom(catalog.interviewerChairPrefabs, rng) : null;
        if (intChairPrefab != null)
        {
            GameObject chairInstance = Instantiate(intChairPrefab, interviewerChairPos, Quaternion.identity, centerRoot.transform);
            Bounds b = GetObjectBounds(chairInstance);
            if (Mathf.Abs(b.min.y) > 0.01f)
            {
                chairInstance.transform.position -= new Vector3(0f, b.min.y, 0f);
                b = GetObjectBounds(chairInstance);
            }
            // Approximate seat height as ~55% of the chair bounding box height
            // (the seat cushion is typically in the lower half of the total chair height)
            chairSeatHeight = Mathf.Clamp(b.size.y * 0.45f, 0.38f, 0.58f);
        }
        else
        {
            GameObject chair = ProceduralOfficePrimitives.BuildInterviewerChair(centerRoot.transform, rng);
            chair.transform.localPosition = interviewerChairPos;
            chairSeatHeight = 0.46f; // procedural chair seat height
        }
        interviewerSeatHeight = chairSeatHeight;

        // 3. Guest Chair (In front of desk, facing -Z towards interviewer)
        float chairFrontZ = Mathf.Max(deskSize.z * 0.5f, 0.45f) + 0.55f;
        Vector3 guestChairPos = new Vector3(0f, 0f, chairFrontZ);
        GameObject guestChairPrefab = catalog != null ? catalog.PickRandom(catalog.guestChairPrefabs, rng) : null;
        if (guestChairPrefab != null)
        {
            GameObject chairInstance = Instantiate(guestChairPrefab, guestChairPos, Quaternion.Euler(0f, 180f, 0f), centerRoot.transform);
            Bounds b = GetObjectBounds(chairInstance);
            if (Mathf.Abs(b.min.y) > 0.01f)
            {
                chairInstance.transform.position -= new Vector3(0f, b.min.y, 0f);
            }
        }
        else
        {
            GameObject chair = ProceduralOfficePrimitives.BuildGuestChair(centerRoot.transform, rng);
            chair.transform.localPosition = guestChairPos;
            chair.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
    }

    void BuildDeskClutter(System.Random rng, Vector3 deskTopPos, Vector3 deskSize)
    {
        GameObject clutterRoot = new GameObject("DeskClutter");
        clutterRoot.transform.SetParent(roomContainer.transform, false);

        float halfW = deskSize.x * 0.5f;
        float halfD = deskSize.z * 0.5f;

        // 1. Desk Lamp (placed on left or right rear corner, angled inward toward the papers)
        if (spawnDeskLamp)
        {
            float lampSide = rng.Next(2) == 0 ? -1f : 1f;
            // X/Z position on desk surface; Y will be corrected to deskTop after bounds-grounding
            Vector3 lampXZ = new Vector3(lampSide * (halfW * 0.68f), 0f, -halfD * 0.45f);
            Quaternion lampRot = Quaternion.Euler(0f, lampSide > 0 ? -60f : 60f, 0f);

            GameObject lampPrefab = catalog != null ? catalog.PickRandom(catalog.deskLampPrefabs, rng) : null;
            if (lampPrefab != null)
            {
                // Spawn at origin first, measure bounds, then move so bottom sits on deskTop
                GameObject lampInst = Instantiate(lampPrefab, Vector3.zero, lampRot, clutterRoot.transform);
                Bounds lb = GetObjectBounds(lampInst);
                float yOffset = lb.min.y; // negative if pivot is above bottom
                lampInst.transform.position = new Vector3(lampXZ.x, deskTopPos.y - yOffset, lampXZ.z);
            }
            else
            {
                GameObject lamp = ProceduralOfficePrimitives.BuildDeskLamp(clutterRoot.transform, rng, true);
                lamp.transform.localPosition = new Vector3(lampXZ.x, deskTopPos.y, lampXZ.z);
                lamp.transform.localRotation = lampRot;
            }
        }

        // 2. Laptop (in front of interviewer, screen facing the interviewer at 180 deg)
        Vector3 laptopPos = new Vector3(0.05f * (float)(rng.NextDouble() * 2.0 - 1.0), deskTopPos.y, -0.05f);
        GameObject laptopPrefab = catalog != null ? catalog.PickRandom(catalog.laptopPrefabs, rng) : null;
        if (laptopPrefab != null)
        {
            Instantiate(laptopPrefab, laptopPos, Quaternion.Euler(0f, 180f, 0f), clutterRoot.transform);
        }
        else
        {
            GameObject laptop = ProceduralOfficePrimitives.BuildLaptop(clutterRoot.transform, rng, true);
            laptop.transform.localPosition = laptopPos;
            laptop.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        // 3. Coffee Mug / Water Glass
        Vector3 mugPos = new Vector3(halfW * 0.42f, deskTopPos.y, -halfD * 0.25f);
        GameObject mug = ProceduralOfficePrimitives.BuildCoffeeMug(clutterRoot.transform, rng);
        mug.transform.localPosition = mugPos;

        // 4. Paper Stack / Resume Folders
        Vector3 paperPos = new Vector3(-halfW * 0.38f, deskTopPos.y, 0.08f);
        GameObject papers = ProceduralOfficePrimitives.BuildPaperStack(clutterRoot.transform, rng);
        papers.transform.localPosition = paperPos;

        // 5. Pen Holder
        Vector3 penPos = new Vector3(halfW * 0.55f, deskTopPos.y, 0.12f);
        GameObject pens = ProceduralOfficePrimitives.BuildPenHolder(clutterRoot.transform, rng);
        pens.transform.localPosition = penPos;
    }

    // Scale factor for OfficeCharactersPack characters relative to the VNB desk/props.
    [Tooltip("Scale applied to the interviewer NPC prefab. Decrease if the character looks too large next to the desk.")]
    public float interviewerScale = 0.78f;

    // Seat height measured from the chair bounds — set by BuildCenterpiece before BuildInterviewer runs.
    float interviewerSeatHeight = 0.46f;

    void BuildInterviewer(System.Random rng, Vector3 chairPos)
    {
        GameObject interviewerRoot = new GameObject("InterviewerNPC");
        interviewerRoot.transform.SetParent(roomContainer.transform, false);

        // Place root at floor level directly above the chair centre.
        // The NPC prefab pivot is at ground (Y=0) between the feet, so we keep Y=0
        // and let HumanoidSittingPose lower the hips onto the seat cushion.
        Vector3 spawnPos = new Vector3(chairPos.x, 0f, chairPos.z);
        interviewerRoot.transform.localPosition = spawnPos;
        interviewerRoot.transform.localRotation = Quaternion.identity; // face +Z (toward player)

        GameObject customAvatar = catalog != null ? catalog.PickRandom(catalog.interviewerPrefabs, rng) : null;
        if (customAvatar != null)
        {
            // Spawn NPC at same world position as root (floor level)
            GameObject npc = Instantiate(customAvatar, spawnPos, Quaternion.identity, interviewerRoot.transform);
            npc.transform.localScale = Vector3.one * interviewerScale;

            // Tell the sitting-pose script where the seat cushion is so it drops the hips correctly
            HumanoidSittingPose pose = npc.AddComponent<HumanoidSittingPose>();
            pose.seatHeight = interviewerSeatHeight;
        }
        else
        {
            ProceduralOfficePrimitives.BuildSeatedInterviewer(interviewerRoot.transform, rng);
        }
    }

    void BuildCornerProps(System.Random rng)
    {
        GameObject cornerRoot = new GameObject("CornerProps");
        cornerRoot.transform.SetParent(roomContainer.transform, false);

        float halfW = roomWidth * 0.5f - 0.75f;
        float halfD = roomDepth * 0.5f - 0.75f;

        Vector3[] corners = {
            new Vector3(-halfW, 0f, -halfD), // Back-Left
            new Vector3(halfW, 0f, -halfD),  // Back-Right
            new Vector3(-halfW, 0f, halfD),  // Front-Left
            new Vector3(halfW, 0f, halfD)    // Front-Right
        };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 pos = corners[i];
            int roll = (i + rng.Next(3)) % 3;

            if (roll == 0)
            {
                // Plant
                GameObject plantPrefab = catalog != null ? catalog.PickRandom(catalog.plantPrefabs, rng) : null;
                if (plantPrefab != null)
                {
                    SpawnGrounded(plantPrefab, pos, Quaternion.identity, cornerRoot.transform);
                }
                else
                {
                    GameObject plant = ProceduralOfficePrimitives.BuildPlant(cornerRoot.transform, rng);
                    plant.transform.localPosition = pos;
                }
            }
            else if (roll == 1)
            {
                // Filing Cabinet
                GameObject cornerPrefab = catalog != null ? catalog.PickRandom(catalog.cornerPropPrefabs, rng) : null;
                if (cornerPrefab != null)
                {
                    float yaw = pos.x < 0 ? 90f : -90f;
                    SpawnGrounded(cornerPrefab, pos, Quaternion.Euler(0f, yaw, 0f), cornerRoot.transform);
                }
                else
                {
                    GameObject cab = ProceduralOfficePrimitives.BuildFilingCabinet(cornerRoot.transform, rng);
                    cab.transform.localPosition = pos;
                    float yaw = pos.x < 0 ? 90f : -90f;
                    cab.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                }
            }
            else
            {
                // Water Cooler
                GameObject cooler = ProceduralOfficePrimitives.BuildWaterCooler(cornerRoot.transform, rng);
                cooler.transform.localPosition = pos;
                float yaw = pos.z < 0 ? 45f : 135f;
                cooler.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }
    }

    GameObject SpawnGrounded(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent)
    {
        GameObject go = Instantiate(prefab, pos, rot, parent);
        Bounds b = GetObjectBounds(go);
        if (Mathf.Abs(b.min.y) > 0.01f)
        {
            go.transform.position -= new Vector3(0f, b.min.y, 0f);
        }
        return go;
    }

    GameObject SpawnOnSurface(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent, float surfaceY)
    {
        GameObject go = Instantiate(prefab, pos, rot, parent);
        Bounds b = GetObjectBounds(go);
        float diff = surfaceY - b.min.y;
        go.transform.position += new Vector3(0f, diff, 0f);
        return go;
    }

    void BuildWallDecor(System.Random rng)
    {
        GameObject wallDecorRoot = new GameObject("WallDecor");
        wallDecorRoot.transform.SetParent(roomContainer.transform, false);

        float halfD = roomDepth * 0.5f - 0.05f;
        float halfW = roomWidth * 0.5f - 0.05f;

        // 1. Back Wall: Whiteboard or Large Framed Art or Window
        bool hasWindow = spawnWindow && rng.Next(2) == 0;
        if (hasWindow)
        {
            // Build Window on back wall
            BuildOfficeWindow(wallDecorRoot.transform, new Vector3(0f, 1.8f, -halfD), new Vector3(3.2f, 1.6f, 0.08f), rng);
        }
        else
        {
            // Build Whiteboard
            GameObject wb = ProceduralOfficePrimitives.BuildWhiteboard(wallDecorRoot.transform, new Vector3(2.6f, 1.3f, 0.04f));
            wb.transform.localPosition = new Vector3(0f, 1.85f, -halfD);
        }

        // 2. Left Wall: Wall Clock
        GameObject clock = ProceduralOfficePrimitives.BuildWallClock(wallDecorRoot.transform);
        clock.transform.localPosition = new Vector3(-halfW, 2.2f, 0.3f);
        clock.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

        // 3. Right Wall: Diplomas / Framed Art
        GameObject art = ProceduralOfficePrimitives.BuildWallArt(wallDecorRoot.transform, new Vector3(1.4f, 1.0f, 0.04f), rng);
        art.transform.localPosition = new Vector3(halfW, 1.85f, -0.4f);
        art.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
    }

    void BuildOfficeWindow(Transform parent, Vector3 pos, Vector3 size, System.Random rng)
    {
        GameObject root = new GameObject("OfficeWindow");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = pos;

        // Outer Frame
        ProceduralOfficePrimitives.CreateBox("Frame", root.transform, Vector3.zero, size, ProceduralOfficePrimitives.MetalDark);

        // Glass Pane with subtle exterior sky glow
        Color skyCol = new Color(0.65f, 0.80f, 0.95f, 0.6f);
        ProceduralOfficePrimitives.CreateBox("Glass", root.transform, new Vector3(0f, 0f, 0.02f), new Vector3(size.x - 0.12f, size.y - 0.12f, 0.02f), skyCol, 0.95f);

        // Horizontal Venetian Blinds slats
        int slats = 14;
        float slatH = (size.y - 0.16f) / slats;
        Color blindColor = ProceduralOfficePrimitives.OfficeWhite * 0.9f;
        for (int i = 0; i < slats; i++)
        {
            float sy = -size.y * 0.5f + 0.1f + i * slatH;
            GameObject slat = ProceduralOfficePrimitives.CreateBox("Slat_" + i, root.transform, new Vector3(0f, sy, 0.035f), new Vector3(size.x - 0.16f, 0.035f, 0.08f), blindColor);
            slat.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
        }
    }

    void BuildCeilingLighting(System.Random rng)
    {
        GameObject lightRoot = new GameObject("CeilingLighting");
        lightRoot.transform.SetParent(roomContainer.transform, false);

        // 2 Recessed ceiling light panels
        float panelW = 0.6f;
        float panelL = 1.2f;
        float ceilY = roomHeight - 0.01f;

        Vector3[] panelPositions = {
            new Vector3(0f, ceilY, -1.2f), // Over desk & interviewer
            new Vector3(0f, ceilY, 1.4f)   // Over candidate area
        };

        for (int i = 0; i < panelPositions.Length; i++)
        {
            Vector3 pos = panelPositions[i];
            GameObject panel = ProceduralOfficePrimitives.CreateBox("LightPanel_" + i, lightRoot.transform, pos, new Vector3(panelW, 0.02f, panelL), Color.white, 0.1f);

            // Add gentle downlight
            GameObject lGo = new GameObject("DownLight_" + i);
            lGo.transform.SetParent(panel.transform, false);
            lGo.transform.localPosition = new Vector3(0f, -0.1f, 0f);

            Light l = lGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 5.5f;
            l.intensity = 1.0f;
            l.color = new Color(0.98f, 0.96f, 0.92f);
            l.shadows = LightShadows.None;
        }
    }

    Bounds GetObjectBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }
}
