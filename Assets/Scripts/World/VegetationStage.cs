using UnityEngine;

[System.Serializable]
public class TreeType
{
    public string name = "Tree";
    public float weight = 1f;
    public float minHeight = 7f;
    public float maxHeight = 12f;
    public float crownRatio = 0.6f;
    [Range(0.1f, 0.8f)] public float trunkFraction = 0.4f;
    public ModelSlot trunk = ModelSlot.Primitive(PrimitiveKind.Cylinder, new Color(0.35f, 0.24f, 0.15f));
    public ModelSlot crown = ModelSlot.Primitive(PrimitiveKind.Sphere, new Color(0.18f, 0.42f, 0.16f));
    public ModelSlot whole = new ModelSlot();

    public static TreeType Create(string name, float weight, Vector2 height, float crownRatio, float trunkFraction, PrimitiveKind crownShape, Color[] crownTints)
    {
        TreeType t = new TreeType();
        t.name = name;
        t.weight = weight;
        t.minHeight = height.x;
        t.maxHeight = height.y;
        t.crownRatio = crownRatio;
        t.trunkFraction = trunkFraction;
        t.crown = ModelSlot.Primitive(crownShape, Color.green);
        t.crown.tints = crownTints;
        t.crown.colliders = ColliderMode.None;
        return t;
    }

    public bool HasWholeModel
    {
        get
        {
            if (whole == null || whole.variants == null) return false;
            for (int i = 0; i < whole.variants.Length; i++)
                if (whole.variants[i] != null) return true;
            return false;
        }
    }
}

[AddComponentMenu("Procedural World/Stages/6 Vegetation")]
public class VegetationStage : WorldStage
{
    [Header("Placement")]
    public float spacing = 9f;
    public int maxTrees = 1600;
    public float clearance = 1.5f;
    [Range(0.2f, 1f)] public float footprintScale = 0.5f;
    public float highwayClearance = 14f;
    [Range(5f, 60f)] public float maxSlopeDeg = 32f;
    public float sink = 0.2f;

    [Header("Density")]
    public float noiseFrequency = 0.012f;
    [Range(0f, 1f)] public float forestMinDensity = 0.45f;
    [Range(0f, 1f)] public float openDensity = 0.08f;

    [Header("Tree Types")]
    public TreeType[] types = new TreeType[]
    {
        TreeType.Create("Broadleaf", 3f, new Vector2(7f, 12f), 0.65f, 0.4f, PrimitiveKind.Sphere, new Color[] { new Color(0.18f, 0.42f, 0.16f), new Color(0.25f, 0.5f, 0.2f), new Color(0.3f, 0.46f, 0.15f) }),
        TreeType.Create("Pine", 2f, new Vector2(10f, 18f), 0.3f, 0.3f, PrimitiveKind.Capsule, new Color[] { new Color(0.1f, 0.32f, 0.17f), new Color(0.13f, 0.38f, 0.2f) })
    };

    [Header("Forest Floor Dressing (grass-free, browser-friendly)")]
    [Tooltip("Adds lightweight shrubs, ferns, rocks, timber and occasional landmarks. No ponds or terrain carving.")]
    public bool environmentDressing = true;
    [Range(0, 2000)] public int maxBushes = 520;
    [Range(0, 500)] public int maxStumps = 95;
    [Range(0, 500)] public int maxLogs = 120;
    [Range(0, 1200)] public int maxRocks = 520;
    [Tooltip("Additional rocks placed near the highway edge, while keeping a safe offset from the road surface.")]
    [Range(0, 500)] public int maxRoadsideRocks = 160;
    [Range(0, 1200)] public int maxFerns = 360;
    [Range(0, 300)] public int maxSmallDetails = 100;
    [Range(0, 30)] public int maxLandmarks = 8;
    [Range(2f, 18f)] public float dressingSpacing = 5.5f;
    [Range(0f, 1f)] public float dressingForestBias = 0.78f;
    public float dressingRoadClearance = 18f;
    [Tooltip("Extra safety margin around reserved structures and roads.")]
    [Range(0f, 12f)] public float layoutSafetyMargin = 2.5f;

    public override int Order
    {
        get { return StageOrder.Vegetation; }
    }

    public override string StageName
    {
        get { return "Vegetation"; }
    }

    TreeType Pick(WorldRng rng)
    {
        float total = 0f;
        for (int i = 0; i < types.Length; i++)
            if (types[i] != null) total += Mathf.Max(0f, types[i].weight);
        if (total <= 0f) return null;
        float v = rng.Value() * total;
        TreeType last = null;
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] == null) continue;
            last = types[i];
            v -= Mathf.Max(0f, types[i].weight);
            if (v <= 0f) return types[i];
        }
        return last;
    }

    public override void Run(WorldContext c)
    {
        if (c.zoneMap == null)
        {
            Debug.LogError("Vegetation needs the Zones stage enabled.");
            return;
        }
        WorldRng rng = c.Rng("vegetation");
        Vector2 off = new Vector2(rng.Range(2000f, 9000f), rng.Range(2000f, 9000f));
        float half = c.Size * 0.5f;
        System.Collections.Generic.List<Vector2> cands = PoissonDisc.Generate(new Rect(-half, -half, c.Size, c.Size), Mathf.Max(2f, spacing), rng.Raw, 20);
        for (int i = cands.Count - 1; i > 0; i--)
        {
            int j = rng.Range(0, i + 1);
            Vector2 tmp = cands[i];
            cands[i] = cands[j];
            cands[j] = tmp;
        }

        float minNormalY = Mathf.Cos(maxSlopeDeg * Mathf.Deg2Rad);
        Transform parent = c.LayerRoot(PlacementLayer.Tree);
        int placed = 0;
        int blocked = 0;
        for (int i = 0; i < cands.Count && placed < maxTrees; i++)
        {
            Vector2 p = cands[i];
            ZoneType zone = c.zoneMap.ZoneAt(p);
            if (zone != ZoneType.Forest && zone != ZoneType.Open) continue;
            float n = NoiseLib.Fbm(p.x * noiseFrequency + off.x, p.y * noiseFrequency + off.y, 3, 2f, 0.5f);
            float density = zone == ZoneType.Forest ? Mathf.Lerp(forestMinDensity, 1f, Mathf.SmoothStep(0.3f, 0.7f, n)) : openDensity * Mathf.SmoothStep(0.55f, 0.75f, n);
            if (!rng.Chance(density)) continue;
            if (c.zoneMap.highway.DistanceTo(p) < highwayClearance) continue;
            if (c.NormalAt(p.x, p.y).y < minNormalY) continue;

            TreeType t = Pick(rng);
            if (t == null) break;
            float h = rng.Range(t.minHeight, t.maxHeight);
            float cw = h * t.crownRatio;
            float fp = Mathf.Max(0.5f, cw * footprintScale);
            float baseY = c.HeightAt(p.x, p.y) - sink;
            Vector3 baseCenter = new Vector3(p.x, baseY, p.y);
            float yaw = rng.Range(0f, 360f);
            OrientedBox box = OrientedBox.FromBase(baseCenter, new Vector3(fp, h, fp), yaw);
            if (!c.occupancy.IsFree(box, PlacementLayer.Tree, clearance))
            {
                blocked++;
                continue;
            }

            GameObject go;
            if (t.HasWholeModel)
            {
                go = ModelFitter.Spawn(t.whole, baseCenter, yaw, new Vector3(cw, h + sink, cw), parent, rng.Raw);
            }
            else
            {
                float trunkH = (h + sink) * t.trunkFraction;
                float trunkW = Mathf.Max(0.4f, h * 0.06f);
                go = ModelFitter.Spawn(t.trunk, baseCenter, yaw, new Vector3(trunkW, trunkH, trunkW), parent, rng.Raw);
                float crownBase = baseY + trunkH * 0.5f;
                ModelFitter.Spawn(t.crown, new Vector3(p.x, crownBase, p.y), yaw, new Vector3(cw, baseY + h + sink - crownBase, cw), go.transform, rng.Raw);
            }
            go.name = t.name;
            c.occupancy.Add(box, PlacementLayer.Tree, go);
            placed++;
        }
        if (environmentDressing) PlaceForestFloor(c, rng, off);
        Debug.Log("Vegetation: " + placed + " trees from " + cands.Count + " candidates, " + blocked + " blocked by other objects" + (placed >= maxTrees ? " (hit maxTrees)" : ""));
    }

    void PlaceForestFloor(WorldContext c, WorldRng rng, Vector2 off)
    {
        Transform parent = c.LayerRoot(PlacementLayer.Prop);
        int bushes = 0, stumps = 0, logs = 0, rocks = 0, ferns = 0, details = 0, landmarks = 0;
        float half = c.Size * 0.5f;
        int target = maxBushes + maxStumps + maxLogs + maxRocks + maxFerns + maxSmallDetails + maxLandmarks;
        int attempts = Mathf.Clamp(target * 18, 1800, 30000);
        float minNormalY = Mathf.Cos(32f * Mathf.Deg2Rad);
        for (int i = 0; i < attempts && (bushes < maxBushes || stumps < maxStumps || logs < maxLogs || rocks < maxRocks || ferns < maxFerns || details < maxSmallDetails || landmarks < maxLandmarks); i++)
        {
            Vector2 p = new Vector2(rng.Range(-half + 12f, half - 12f), rng.Range(-half + 12f, half - 12f));
            ZoneType zone = c.zoneMap.ZoneAt(p);
            if (zone != ZoneType.Forest && zone != ZoneType.Open) continue;
            bool forest = zone == ZoneType.Forest;
            if (!forest && !rng.Chance(0.32f)) continue;
            if (c.zoneMap.highway != null && c.zoneMap.highway.DistanceTo(p) < dressingRoadClearance) continue;
            if (c.NormalAt(p.x, p.y).y < minNormalY) continue;
            float n = NoiseLib.Fbm(p.x * 0.021f + off.x, p.y * 0.021f + off.y, 3, 2f, 0.5f);
            if (forest && n < 0.12f) continue;
            float y = c.HeightAt(p.x, p.y);
            float roll = rng.Value();
            float yaw = rng.Range(0f, 360f);

            // All dressing uses the existing occupancy grid and a conservative extra margin.
            // Decorative primitives have their colliders removed, so they cannot obstruct movement.
            if (landmarks < maxLandmarks && roll < 0.006f && forest)
            {
                float size = rng.Range(1.8f, 3.0f);
                OrientedBox box = OrientedBox.FromBase(new Vector3(p.x, y, p.y), new Vector3(size * 2.2f, size * 1.5f, size * 2.2f), yaw);
                if (!c.occupancy.TryReserve(box, PlacementLayer.Prop, layoutSafetyMargin + 1f, null)) continue;
                // Small, low-poly standing-stone cluster as a discoverable landmark.
                GameObject group = new GameObject("ForestLandmark_StandingStones");
                group.transform.SetParent(parent, true);
                group.transform.position = new Vector3(p.x, y, p.y);
                int stoneCount = rng.Range(3, 6);
                for (int k = 0; k < stoneCount; k++)
                {
                    GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stone.name = "StandingStone";
                    stone.transform.SetParent(group.transform, false);
                    float h = rng.Range(1.1f, 2.3f);
                    float w = rng.Range(0.35f, 0.65f);
                    float a = k * Mathf.PI * 2f / stoneCount;
                    stone.transform.localPosition = new Vector3(Mathf.Cos(a) * size * 0.65f, h * 0.5f, Mathf.Sin(a) * size * 0.65f);
                    stone.transform.localScale = new Vector3(w, h, w * rng.Range(0.7f, 1.3f));
                    stone.transform.localRotation = Quaternion.Euler(rng.Range(-7f, 7f), rng.Range(0f, 360f), rng.Range(-7f, 7f));
                    SetVisualOnly(stone, new Color(0.34f, 0.36f, 0.33f));
                }
                landmarks++;
                continue;
            }
            if (bushes < maxBushes && roll < 0.31f)
            {
                float size = rng.Range(0.8f, 1.7f);
                OrientedBox box = OrientedBox.FromBase(new Vector3(p.x, y, p.y), new Vector3(size * 1.7f, size * 1.25f, size * 1.7f), yaw);
                if (!c.occupancy.TryReserve(box, PlacementLayer.Prop, layoutSafetyMargin * 0.35f, null)) continue;
                GameObject bush = new GameObject("ForestShrub");
                bush.transform.SetParent(parent, true);
                bush.transform.position = new Vector3(p.x, y, p.y);
                bush.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                Color[] greens = { new Color(0.12f, 0.28f, 0.11f), new Color(0.20f, 0.36f, 0.13f), new Color(0.29f, 0.40f, 0.16f), new Color(0.16f, 0.31f, 0.19f) };
                Color tint = greens[rng.Range(0, greens.Length)];
                for (int k = 0; k < 3; k++)
                {
                    GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    leaf.name = "BushCanopy"; leaf.transform.SetParent(bush.transform, false);
                    float s = size * rng.Range(0.58f, 0.88f);
                    leaf.transform.localPosition = new Vector3(rng.Range(-0.34f, 0.34f) * size, s * 0.42f, rng.Range(-0.34f, 0.34f) * size);
                    leaf.transform.localScale = new Vector3(s * 1.1f, s * 0.82f, s);
                    SetVisualOnly(leaf, Color.Lerp(tint, Color.white, rng.Range(0f, 0.12f)));
                }
                bushes++; continue;
            }
            if (ferns < maxFerns && roll < 0.51f)
            {
                float size = rng.Range(0.65f, 1.35f);
                OrientedBox box = OrientedBox.FromBase(new Vector3(p.x, y, p.y), new Vector3(size * 1.4f, size, size * 1.4f), yaw);
                if (!c.occupancy.TryReserve(box, PlacementLayer.Prop, layoutSafetyMargin * 0.2f, null)) continue;
                GameObject fern = new GameObject("ForestFernCluster"); fern.transform.SetParent(parent, true);
                fern.transform.position = new Vector3(p.x, y, p.y); fern.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                Color[] leafColors = { new Color(0.12f, 0.30f, 0.12f), new Color(0.20f, 0.40f, 0.16f), new Color(0.30f, 0.45f, 0.18f) };
                for (int k = 0; k < 5; k++)
                {
                    GameObject frond = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    frond.name = "FernFrond"; frond.transform.SetParent(fern.transform, false);
                    frond.transform.localPosition = new Vector3(rng.Range(-0.25f, 0.25f) * size, size * 0.36f, rng.Range(-0.25f, 0.25f) * size);
                    frond.transform.localRotation = Quaternion.Euler(rng.Range(-35f, 35f), k * 72f, rng.Range(-30f, 30f));
                    frond.transform.localScale = new Vector3(size * 0.12f, size * rng.Range(0.35f, 0.65f), size * 0.22f);
                    SetVisualOnly(frond, leafColors[rng.Range(0, leafColors.Length)]);
                }
                ferns++; continue;
            }
            if (rocks < maxRocks && roll < 0.73f)
            {
                float size = rng.Range(0.35f, 1.25f);
                OrientedBox box = OrientedBox.FromBase(new Vector3(p.x, y, p.y), new Vector3(size * 1.8f, size, size * 1.6f), yaw);
                if (!c.occupancy.TryReserve(box, PlacementLayer.Prop, layoutSafetyMargin * 0.2f, null)) continue;
                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = "MossyRock"; rock.transform.SetParent(parent, true);
                rock.transform.position = new Vector3(p.x, y + size * 0.22f, p.y);
                rock.transform.rotation = Quaternion.Euler(rng.Range(-15f, 15f), yaw, rng.Range(-15f, 15f));
                rock.transform.localScale = new Vector3(size * rng.Range(0.9f, 1.8f), size * rng.Range(0.45f, 0.85f), size * rng.Range(0.8f, 1.5f));
                Color rc = rng.Chance(0.38f) ? new Color(0.20f, 0.29f, 0.16f) : new Color(0.32f, 0.34f, 0.32f);
                RoughenRockMesh(rock, rng.Range(1, 1000000));
                SetVisualOnly(rock, rc); rocks++; continue;
            }
            if (stumps < maxStumps && roll < 0.82f)
            {
                float radius = rng.Range(0.32f, 0.58f);
                OrientedBox box = OrientedBox.FromBase(new Vector3(p.x, y, p.y), new Vector3(radius * 2f, 0.9f, radius * 2f), yaw);
                if (!c.occupancy.TryReserve(box, PlacementLayer.Prop, layoutSafetyMargin * 0.25f, null)) continue;
                GameObject stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder); stump.name = "WeatheredTreeStump";
                stump.transform.SetParent(parent, true); stump.transform.position = new Vector3(p.x, y + 0.32f, p.y);
                stump.transform.localScale = new Vector3(radius * 1.6f, rng.Range(0.25f, 0.48f), radius * 1.6f);
                stump.transform.rotation = Quaternion.Euler(rng.Range(-8f, 8f), yaw, rng.Range(-8f, 8f));
                SetVisualOnly(stump, new Color(0.27f, 0.16f, 0.085f)); stumps++; continue;
            }
            if (logs < maxLogs && roll < 0.91f)
            {
                float length = rng.Range(2f, 4.8f); float radius = rng.Range(0.22f, 0.42f);
                OrientedBox box = OrientedBox.FromBase(new Vector3(p.x, y, p.y), new Vector3(length, radius * 2f, radius * 2f), yaw);
                if (!c.occupancy.TryReserve(box, PlacementLayer.Prop, layoutSafetyMargin * 0.25f, null)) continue;
                GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder); log.name = "FallenLog";
                log.transform.SetParent(parent, true); log.transform.position = new Vector3(p.x, y + radius * 0.72f, p.y);
                log.transform.rotation = Quaternion.Euler(0f, yaw, 90f); log.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
                SetVisualOnly(log, new Color(0.30f, 0.19f, 0.105f)); SitSpot.AddLog(log); logs++; continue;
            }
            if (details < maxSmallDetails)
            {
                float size = rng.Range(0.16f, 0.35f);
                OrientedBox box = OrientedBox.FromBase(new Vector3(p.x, y, p.y), new Vector3(size * 2f, size, size * 2f), yaw);
                if (!c.occupancy.TryReserve(box, PlacementLayer.Prop, 0.15f, null)) continue;
                GameObject detail = GameObject.CreatePrimitive(rng.Chance(0.5f) ? PrimitiveType.Sphere : PrimitiveType.Cube);
                detail.name = "ForestGroundDetail"; detail.transform.SetParent(parent, true);
                detail.transform.position = new Vector3(p.x, y + size * 0.15f, p.y);
                detail.transform.rotation = Quaternion.Euler(rng.Range(-10f, 10f), yaw, rng.Range(-10f, 10f));
                detail.transform.localScale = new Vector3(size * rng.Range(1f, 2f), size * 0.35f, size);
                SetVisualOnly(detail, rng.Chance(0.5f) ? new Color(0.43f, 0.35f, 0.20f) : new Color(0.35f, 0.37f, 0.30f)); details++;
            }
        }
        int roadsideRocks = PlaceRoadsideRocks(c, rng, parent);
        CombineDecorativeMeshes(parent);
        Debug.Log("Forest dressing (pond-free): " + bushes + " shrubs, " + ferns + " fern clusters, " + (rocks + roadsideRocks) + " rocks (" + roadsideRocks + " roadside), " + stumps + " stumps, " + logs + " fallen logs, " + details + " small details, " + landmarks + " landmarks. Decorative meshes combined into material batches; terrain heightmap unchanged.");
    }


    int PlaceRoadsideRocks(WorldContext c, WorldRng rng, Transform parent)
    {
        if (maxRoadsideRocks <= 0 || c.zoneMap == null || c.zoneMap.highway == null) return 0;
        int placed = 0;
        int attempts = Mathf.Clamp(maxRoadsideRocks * 28, 1200, 12000);
        float half = c.Size * 0.5f;
        float minNormalY = Mathf.Cos(32f * Mathf.Deg2Rad);
        for (int i = 0; i < attempts && placed < maxRoadsideRocks; i++)
        {
            Vector2 p = new Vector2(rng.Range(-half + 8f, half - 8f), rng.Range(-half + 8f, half - 8f));
            ZoneType zone = c.zoneMap.ZoneAt(p);
            if (zone != ZoneType.Forest && zone != ZoneType.Open) continue;
            float roadDistance = c.zoneMap.highway.DistanceTo(p);
            // Stay beyond the normal dressing/tree clearance so rocks sit beside the
            // highway shoulder rather than on the paved surface, while still framing it.
            if (roadDistance < 18.5f || roadDistance > 29.0f) continue;
            if (c.NormalAt(p.x, p.y).y < minNormalY) continue;
            float y = c.HeightAt(p.x, p.y);
            float size = rng.Range(0.38f, 1.45f);
            float yaw = rng.Range(0f, 360f);
            OrientedBox box = OrientedBox.FromBase(new Vector3(p.x, y, p.y), new Vector3(size * 1.9f, size * 0.9f, size * 1.7f), yaw);
            if (!c.occupancy.TryReserve(box, PlacementLayer.Prop, layoutSafetyMargin * 0.25f, null)) continue;

            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "MossyRoadsideRock";
            rock.transform.SetParent(parent, true);
            rock.transform.position = new Vector3(p.x, y + size * 0.18f, p.y);
            rock.transform.rotation = Quaternion.Euler(rng.Range(-18f, 18f), yaw, rng.Range(-18f, 18f));
            rock.transform.localScale = new Vector3(size * rng.Range(0.9f, 1.75f), size * rng.Range(0.42f, 0.82f), size * rng.Range(0.8f, 1.55f));
            Color rc = rng.Chance(0.42f) ? new Color(0.23f, 0.31f, 0.18f) : new Color(0.38f, 0.39f, 0.36f);
            RoughenRockMesh(rock, rng.Range(1, 1000000));
            SetVisualOnly(rock, rc);
            placed++;
        }
        return placed;
    }

    static void RoughenRockMesh(GameObject rock, int seed)
    {
        if (rock == null) return;
        MeshFilter filter = rock.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;
        Mesh mesh = Object.Instantiate(filter.sharedMesh);
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            float noise = Mathf.PerlinNoise(v.x * 3.7f + seed * 0.001f, v.z * 3.7f + seed * 0.002f);
            float ridge = Mathf.PerlinNoise(v.y * 5.1f + seed * 0.003f, v.x * 4.4f + seed * 0.004f);
            float displacement = (noise - 0.5f) * 0.18f + (ridge - 0.5f) * 0.08f;
            vertices[i] = v + v.normalized * displacement;
        }
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.name = "StylizedFacetedRockMesh";
        filter.sharedMesh = mesh;
    }

    // Merge static decorative meshes by shared material into <= 60k-vertex batches.
    // This greatly reduces renderer/draw-call overhead in WebGL builds compared with
    // leaving every primitive as an independent renderer. Original props have no gameplay colliders.
    static void CombineDecorativeMeshes(Transform parent)
    {
        if (parent == null) return;
        System.Collections.Generic.Dictionary<Material, System.Collections.Generic.List<CombineInstance>> groups =
            new System.Collections.Generic.Dictionary<Material, System.Collections.Generic.List<CombineInstance>>();
        MeshFilter[] filters = parent.GetComponentsInChildren<MeshFilter>();
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter == null || filter.sharedMesh == null || filter.transform == parent) continue;
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) continue;
            Material material = renderer.sharedMaterial;
            System.Collections.Generic.List<CombineInstance> list;
            if (!groups.TryGetValue(material, out list)) { list = new System.Collections.Generic.List<CombineInstance>(); groups.Add(material, list); }
            CombineInstance ci = new CombineInstance();
            ci.mesh = filter.sharedMesh;
            ci.transform = parent.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            list.Add(ci);
        }
        int batchIndex = 0;
        foreach (System.Collections.Generic.KeyValuePair<Material, System.Collections.Generic.List<CombineInstance>> pair in groups)
        {
            System.Collections.Generic.List<CombineInstance> batch = new System.Collections.Generic.List<CombineInstance>();
            int vertexCount = 0;
            for (int i = 0; i < pair.Value.Count; i++)
            {
                CombineInstance ci = pair.Value[i];
                int count = ci.mesh != null ? ci.mesh.vertexCount : 0;
                if (batch.Count > 0 && vertexCount + count > 60000)
                {
                    CreateCombinedBatch(parent, pair.Key, batch, batchIndex++);
                    batch.Clear(); vertexCount = 0;
                }
                batch.Add(ci); vertexCount += count;
            }
            if (batch.Count > 0) CreateCombinedBatch(parent, pair.Key, batch, batchIndex++);
        }
        // Remove original primitive hierarchies after combined meshes have been created.
        // Only children containing generated decoration meshes are removed; other world layers are untouched.
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.name.StartsWith("CombinedDecorationBatch_")) continue;
            if (child.GetComponentInChildren<MeshFilter>() != null)
            {
                if (Application.isPlaying) WorldUtil.Kill(child.gameObject);
                else Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    static void CreateCombinedBatch(Transform parent, Material material, System.Collections.Generic.List<CombineInstance> items, int index)
    {
        Mesh mesh = new Mesh();
        mesh.name = "CombinedForestDecorationMesh";
        mesh.indexFormat = items.Count > 2000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.CombineMeshes(items.ToArray(), true, true, false);
        GameObject batch = new GameObject("CombinedDecorationBatch_" + index);
        batch.transform.SetParent(parent, false);
        MeshFilter mf = batch.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
        MeshRenderer mr = batch.AddComponent<MeshRenderer>(); mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    static void SetVisualOnly(GameObject go, Color color)
    {
        Collider[] colliders = go.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (Application.isPlaying) WorldUtil.Kill(colliders[i]);
            else Object.DestroyImmediate(colliders[i]);
        }
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        string n = (go.name ?? string.Empty).ToLowerInvariant();
        bool foliage = n.Contains("leaf") || n.Contains("canopy") || n.Contains("fern") || n.Contains("frond");
        bool rock = n.Contains("rock") || n.Contains("stone") || n.Contains("boulder");
        Material mat = rock
            ? StylizedMaterialFactory.Get("rock", color, Mathf.RoundToInt(color.grayscale * 100f))
            : foliage
                ? StylizedMaterialFactory.Get("foliage", color, Mathf.RoundToInt(color.grayscale * 100f))
                : MaterialFactory.Get(color);
        for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = mat;
    }
}
