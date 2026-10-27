using UnityEngine;

[AddComponentMenu("Procedural World/Stages/4 Buildings")]
public class BuildingStage : WorldStage
{
    public BuildingType[] types = new BuildingType[]
    {
        BuildingType.Create("Small House", ZoneType.Suburb, 3f, new Vector2(8f, 10f), new Vector2(8f, 10f), new Vector2(4f, 5f), new Vector3(3f, 1f, 1f), new Color[] { new Color(0.85f, 0.8f, 0.7f), new Color(0.9f, 0.85f, 0.6f) }),
        BuildingType.Create("Family House", ZoneType.Suburb, 2f, new Vector2(9f, 11f), new Vector2(9f, 12f), new Vector2(5.5f, 7f), new Vector3(3f, 0.5f, 1f), new Color[] { new Color(0.75f, 0.55f, 0.45f), new Color(0.7f, 0.75f, 0.8f) }),
        BuildingType.Create("Wide Cottage", ZoneType.Suburb, 1f, new Vector2(10f, 11f), new Vector2(6f, 8f), new Vector2(3.5f, 4.5f), new Vector3(4f, 0.5f, 1f), new Color[] { new Color(0.8f, 0.7f, 0.65f) }),
        BuildingType.Create("Office Block", ZoneType.City, 3f, new Vector2(20f, 25f), new Vector2(20f, 25f), new Vector2(25f, 60f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.6f, 0.62f, 0.66f), new Color(0.45f, 0.55f, 0.65f) }),
        BuildingType.Create("Tower", ZoneType.City, 1.5f, new Vector2(14f, 18f), new Vector2(14f, 18f), new Vector2(60f, 110f), new Vector3(3f, 3f, 3f), new Color[] { new Color(0.35f, 0.4f, 0.48f), new Color(0.4f, 0.5f, 0.6f) }),
        BuildingType.Create("Mid Rise", ZoneType.City, 2f, new Vector2(22f, 26f), new Vector2(18f, 24f), new Vector2(12f, 24f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.7f, 0.65f, 0.6f), new Color(0.55f, 0.55f, 0.58f), new Color(0.78f, 0.66f, 0.52f), new Color(0.6f, 0.68f, 0.62f) }),
        BuildingType.Create("Shopping Mall", ZoneType.City, 0.8f, new Vector2(34f, 44f), new Vector2(28f, 38f), new Vector2(8f, 14f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.72f, 0.74f, 0.76f), new Color(0.55f, 0.62f, 0.68f) }),
        BuildingType.Create("Parking Garage", ZoneType.City, 0.8f, new Vector2(26f, 34f), new Vector2(22f, 30f), new Vector2(5f, 10f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.48f, 0.5f, 0.53f) }),
        BuildingType.Create("Hotel", ZoneType.City, 0.8f, new Vector2(20f, 28f), new Vector2(18f, 24f), new Vector2(24f, 42f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.68f, 0.58f, 0.48f), new Color(0.56f, 0.62f, 0.68f), new Color(0.76f, 0.66f, 0.56f) }),
        BuildingType.Create("Hospital", ZoneType.City, 0.5f, new Vector2(28f, 36f), new Vector2(24f, 32f), new Vector2(12f, 22f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.82f, 0.84f, 0.82f) }),
        BuildingType.Create("Office Plaza", ZoneType.City, 1.2f, new Vector2(24f, 32f), new Vector2(20f, 28f), new Vector2(30f, 70f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.35f, 0.48f, 0.58f), new Color(0.62f, 0.68f, 0.72f) }),
        BuildingType.Create("Cinema", ZoneType.City, 0.7f, new Vector2(24f, 30f), new Vector2(20f, 26f), new Vector2(8f, 14f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.32f, 0.3f, 0.38f) }),
        BuildingType.Create("Transit Station", ZoneType.City, 0.7f, new Vector2(24f, 34f), new Vector2(18f, 26f), new Vector2(7f, 12f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.66f, 0.68f, 0.69f) }),
        BuildingType.Create("Warehouse", ZoneType.City, 1.1f, new Vector2(26f, 36f), new Vector2(22f, 30f), new Vector2(7f, 13f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.56f, 0.58f, 0.58f) }),
        BuildingType.Create("Apartment Complex", ZoneType.City, 1.8f, new Vector2(20f, 28f), new Vector2(20f, 28f), new Vector2(22f, 48f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.72f, 0.68f, 0.62f), new Color(0.56f, 0.62f, 0.67f), new Color(0.74f, 0.58f, 0.48f), new Color(0.8f, 0.76f, 0.6f) }),
        BuildingType.Create("Fire Station", ZoneType.Suburb, 0.8f, new Vector2(12f, 18f), new Vector2(10f, 16f), new Vector2(5f, 9f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.72f, 0.24f, 0.18f) }),
        BuildingType.Create("Corner Shop", ZoneType.Suburb, 1.1f, new Vector2(8f, 12f), new Vector2(8f, 12f), new Vector2(4f, 7f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.8f, 0.72f, 0.48f), new Color(0.62f, 0.7f, 0.58f) }),
        BuildingType.Create("School", ZoneType.Suburb, 0.8f, new Vector2(16f, 22f), new Vector2(14f, 20f), new Vector2(6f, 12f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.78f, 0.72f, 0.58f) }),
        BuildingType.Create("Community Hall", ZoneType.Suburb, 0.7f, new Vector2(14f, 18f), new Vector2(12f, 16f), new Vector2(5f, 9f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.74f, 0.68f, 0.6f) }),
        BuildingType.Create("Cafe", ZoneType.Suburb, 1.1f, new Vector2(8f, 12f), new Vector2(8f, 12f), new Vector2(4f, 7f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.62f, 0.42f, 0.3f) }),
        BuildingType.Create("Clinic", ZoneType.Suburb, 0.6f, new Vector2(10f, 14f), new Vector2(9f, 13f), new Vector2(5f, 8f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.8f, 0.82f, 0.78f) }),
        BuildingType.Create("Sports Club", ZoneType.Suburb, 0.6f, new Vector2(14f, 20f), new Vector2(12f, 18f), new Vector2(5f, 10f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.42f, 0.56f, 0.42f) }),
        BuildingType.Create("Garden Center", ZoneType.Suburb, 0.6f, new Vector2(12f, 16f), new Vector2(10f, 14f), new Vector2(4f, 7f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.46f, 0.58f, 0.4f) }),
        BuildingType.Create("Library", ZoneType.Suburb, 0.5f, new Vector2(12f, 16f), new Vector2(10f, 14f), new Vector2(6f, 10f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.72f, 0.68f, 0.58f) }),
        BuildingType.Create("Workshop", ZoneType.Suburb, 0.8f, new Vector2(10f, 16f), new Vector2(10f, 14f), new Vector2(4f, 8f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.55f, 0.56f, 0.55f) }),
        BuildingType.Create("Motel", ZoneType.Suburb, 0.5f, new Vector2(14f, 20f), new Vector2(10f, 16f), new Vector2(5f, 9f), new Vector3(1f, 1f, 1f), new Color[] { new Color(0.68f, 0.58f, 0.48f) })
    };

    public float buildingSink = 0.3f;

    public void EnsureCityInfill()
    {
        if (types == null) return;
        float sum = 0f;
        for (int i = 0; i < types.Length; i++)
        {
            BuildingType t = types[i];
            if (t == null) continue;
            if (t.name.StartsWith("City Infill")) return;
            if (t.zone == ZoneType.City) sum += Mathf.Max(0f, t.weight);
        }
        if (sum <= 0f) sum = 10f;
        BuildingType[] add =
        {
            BuildingType.Create("City Infill Shop", ZoneType.City, sum * 0.5f, new Vector2(9f, 13f), new Vector2(9f, 12f), new Vector2(7f, 14f), new Vector3(2f, 0.5f, 0.5f), new Color[] { new Color(0.78f, 0.7f, 0.6f), new Color(0.6f, 0.68f, 0.72f), new Color(0.8f, 0.62f, 0.5f) }),
            BuildingType.Create("City Infill Row", ZoneType.City, sum * 0.45f, new Vector2(7f, 9f), new Vector2(9f, 11f), new Vector2(9f, 18f), new Vector3(2f, 0.3f, 0.5f), new Color[] { new Color(0.7f, 0.58f, 0.5f), new Color(0.56f, 0.6f, 0.7f), new Color(0.82f, 0.76f, 0.6f) }),
            BuildingType.Create("City Infill Flats", ZoneType.City, sum * 0.35f, new Vector2(12f, 16f), new Vector2(10f, 14f), new Vector2(12f, 26f), new Vector3(2f, 0.5f, 0.5f), new Color[] { new Color(0.66f, 0.64f, 0.6f), new Color(0.52f, 0.6f, 0.66f) })
        };
        BuildingType[] all = new BuildingType[types.Length + add.Length];
        System.Array.Copy(types, all, types.Length);
        System.Array.Copy(add, 0, all, types.Length, add.Length);
        types = all;
    }
    public float tallBuildingHeight = 45f;
    [Range(0f, 1f)] public float tallBuildingWeightScale = 0.3f;

    public override int Order
    {
        get { return StageOrder.Buildings; }
    }

    public override string StageName
    {
        get { return "Buildings"; }
    }

    BuildingType Pick(ZoneType zone, WorldRng rng)
    {
        float total = 0f;
        for (int i = 0; i < types.Length; i++)
            if (types[i] != null && types[i].zone == zone) total += Mathf.Max(0f, types[i].weight);
        if (total <= 0f) return null;
        float v = rng.Value() * total;
        BuildingType last = null;
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] == null || types[i].zone != zone) continue;
            last = types[i];
            v -= Mathf.Max(0f, types[i].weight);
            if (v <= 0f) return types[i];
        }
        return last;
    }

    public Vector3 FootprintOf(BuildingType t, GameObject variant)
    {
        if (variant != null)
        {
            Vector3 m = ModelFitter.MeasureFootprint(t.slot, variant);
            if (m.x > 0.01f && m.z > 0.01f) return m;
        }
        return new Vector3(t.maxWidth, t.maxHeight, t.maxDepth);
    }

    public Vector2 MaxFootprint(BuildingType t)
    {
        float mw = 0f, md = 0f;
        bool any = false;
        if (t.slot != null && t.slot.variants != null)
        {
            for (int i = 0; i < t.slot.variants.Length; i++)
            {
                if (t.slot.variants[i] == null) continue;
                Vector3 f = FootprintOf(t, t.slot.variants[i]);
                mw = Mathf.Max(mw, f.x);
                md = Mathf.Max(md, f.z);
                any = true;
            }
        }
        return any ? new Vector2(mw, md) : new Vector2(t.maxWidth, t.maxDepth);
    }

    public bool PickForLot(ZoneType zone, WorldRng rng, float availableWidth, float rowDepth, out BuildingType type, out GameObject variant, out Vector3 footprint)
    {
        type = null;
        variant = null;
        footprint = Vector3.zero;
        if (types == null || rng == null) return false;
        System.Collections.Generic.List<BuildingType> ct = new System.Collections.Generic.List<BuildingType>();
        System.Collections.Generic.List<GameObject> cv = new System.Collections.Generic.List<GameObject>();
        System.Collections.Generic.List<Vector3> cf = new System.Collections.Generic.List<Vector3>();
        System.Collections.Generic.List<float> cw = new System.Collections.Generic.List<float>();
        float total = 0f;
        for (int i = 0; i < types.Length; i++)
        {
            BuildingType t = types[i];
            if (t == null || t.zone != zone) continue;
            int count = 0;
            if (t.slot != null && t.slot.variants != null)
                for (int v = 0; v < t.slot.variants.Length; v++) if (t.slot.variants[v] != null) count++;
            int entries = Mathf.Max(1, count);
            for (int v = 0; v < entries; v++)
            {
                GameObject variantObj = null;
                if (count > 0)
                {
                    int skip = v;
                    for (int k = 0; k < t.slot.variants.Length; k++)
                    {
                        if (t.slot.variants[k] == null) continue;
                        if (skip == 0) { variantObj = t.slot.variants[k]; break; }
                        skip--;
                    }
                }
                Vector3 f = FootprintOf(t, variantObj);
                if (f.x + 2f * t.sideSetback > availableWidth + 0.01f) continue;
                if (f.z + t.frontSetback + t.backSetback > rowDepth + 0.01f) continue;
                float w = Mathf.Max(0f, t.weight) / entries;
                if (zone == ZoneType.City && f.y >= tallBuildingHeight) w *= tallBuildingWeightScale;
                ct.Add(t); cv.Add(variantObj); cf.Add(f); cw.Add(w);
                total += w;
            }
        }
        if (ct.Count == 0 || total <= 0f) return false;
        float value = rng.Value() * total;
        int chosen = ct.Count - 1;
        for (int i = 0; i < ct.Count; i++)
        {
            value -= cw[i];
            if (value <= 0f) { chosen = i; break; }
        }
        type = ct[chosen];
        variant = cv[chosen];
        footprint = cf[chosen];
        return true;
    }

    public override void Run(WorldContext c)
    {
        WorldRng rng = c.Rng("buildings");
        int placed = 0, cityPlaced = 0, suburbPlaced = 0;
        int empty = 0;
        int rejected = 0;
        GameObject gardenRootObject = new GameObject("HouseGardenDetails");
        gardenRootObject.transform.SetParent(c.LayerRoot(PlacementLayer.Prop), false);
        Transform gardenRoot = gardenRootObject.transform;
        int gardenStones = 0, gardenBushes = 0;
        for (int k = 0; k < c.lots.Count; k++)
        {
            LotInfo lot = c.lots[k];
            if (lot.empty) continue;
            BuildingType t = lot.buildingType != null ? lot.buildingType : Pick(lot.zone, rng);
            if (t == null || !rng.Chance(t.spawnChance))
            {
                empty++;
                continue;
            }
            float availW = lot.rect.width - 2f * t.sideSetback;
            float availD = lot.rect.height - t.frontSetback - t.backSetback;
            if (availW < 2f || availD < 2f)
            {
                rejected++;
                continue;
            }
            GameObject variant = lot.variant;
            if (variant == null && lot.buildingType == null) variant = t.slot != null ? t.slot.PickVariant(rng.Raw) : null;
            bool native = variant != null;
            float w, d, h;
            if (native)
            {
                Vector3 fp = FootprintOf(t, variant);
                w = fp.x; d = fp.z; h = fp.y;
                if (w > availW + 0.01f || d > availD + 0.01f)
                {
                    rejected++;
                    continue;
                }
            }
            else
            {
                float requestedW = rng.Range(t.minWidth, t.maxWidth);
                float requestedD = rng.Range(t.minDepth, t.maxDepth);
                w = requestedW;
                d = requestedD;
                if (w > availW)
                {
                    if (availW < requestedW * 0.90f) { rejected++; continue; }
                    w = availW;
                }
                if (d > availD)
                {
                    if (availD < requestedD * 0.90f) { rejected++; continue; }
                    d = availD;
                }
                h = rng.Range(t.minHeight, t.maxHeight);
            }
            float zc = lot.center.y + lot.facingZ * (lot.rect.height * 0.5f - t.frontSetback - d * 0.5f);
            float xc = lot.center.x + rng.Range(-1f, 1f) * (availW - w) * 0.5f;
            Vector3 size = new Vector3(w, h, d);
            float cityDrop = lot.zone == ZoneType.City ? 0.25f : 0f;           // city models were spawning ~0.25 m too high
            Vector3 baseCenter = new Vector3(xc, lot.baseY - buildingSink - cityDrop, zc);
            OrientedBox box = OrientedBox.FromBase(baseCenter, size, lot.yaw);
            if (!c.occupancy.IsFree(box, PlacementLayer.Building, 0f))
            {
                rejected++;
                continue;
            }
            ContactShadows.Add(c.LayerRoot(PlacementLayer.Building), new Vector3(xc, lot.baseY + 0.03f, zc), new Vector2(w + 2.2f, d + 2.2f), lot.yaw);
            GameObject go = ModelFitter.Spawn(t.slot, baseCenter, lot.yaw, size, c.LayerRoot(PlacementLayer.Building), rng.Raw, t.name, variant, native);
            go.name = t.name;
            if (lot.zone == ZoneType.Suburb) EnsureHouseBounds(go, size);
            if (lot.zone == ZoneType.City) CityBuildingIds.Add(go.GetInstanceID());
            c.occupancy.Add(box, PlacementLayer.Building, go);
            if (lot.zone == ZoneType.Suburb && IsResidential(t.name))
                ScatterHouseGarden(c, lot, baseCenter, size, lot.yaw, rng, gardenRoot, ref gardenStones, ref gardenBushes);
            placed++;
            if (lot.zone == ZoneType.City) cityPlaced++;
            else if (lot.zone == ZoneType.Suburb) suburbPlaced++;
        }
        CombineGardenMeshes(gardenRoot);
        Debug.Log("Buildings: " + placed + " placed (city " + cityPlaced + ", suburb " + suburbPlaced + "), " + empty + " lots left empty, " + rejected + " rejected; garden dressing " + gardenStones + " stones/gravel and " + gardenBushes + " shrubs.");
    }

    /// <summary>Buildings spawned on City lots, whatever zone the zone map reports at their centre.</summary>
    public static readonly System.Collections.Generic.HashSet<int> CityBuildingIds = new System.Collections.Generic.HashSet<int>();

    /// <summary>Guarantees every suburb building keeps a solid bounding-box collider (mesh colliders can be missing/unreadable).</summary>
    static void EnsureHouseBounds(GameObject go, Vector3 size)
    {
        BoxCollider bc = go.GetComponent<BoxCollider>();
        if (bc == null) bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = false;
        bc.enabled = true;
        bc.center = new Vector3(0f, size.y * 0.5f, 0f);
        bc.size = size;
    }

    static bool IsResidential(string name)
    {
        string n = (name ?? string.Empty).ToLowerInvariant();
        return n.Contains("house") || n.Contains("cottage") || n.Contains("motel");
    }

    static void ScatterHouseGarden(WorldContext c, LotInfo lot, Vector3 houseBase, Vector3 size, float yaw, WorldRng rng,
        Transform root, ref int stoneCount, ref int bushCount)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        int rockTarget = rng.Range(3, 7);
        int gravelTarget = rng.Range(3, 7);
        int bushTarget = rng.Range(1, 4);
        for (int i = 0; i < rockTarget + gravelTarget + bushTarget; i++)
        {
            bool bush = i >= rockTarget + gravelTarget;
            bool gravel = i >= rockTarget && i < rockTarget + gravelTarget;
            float localX, localZ;
            int layout = rng.Range(0, 4);
            if (layout == 0 || layout == 1)
            {
                localX = (layout == 0 ? -1f : 1f) * (size.x * 0.5f + rng.Range(0.7f, 2.0f));
                localZ = rng.Range(-size.z * 0.42f, size.z * 0.20f);
            }
            else if (layout == 2)
            {
                localX = rng.Range(-size.x * 0.42f, size.x * 0.42f);
                localZ = -(size.z * 0.5f + rng.Range(0.7f, 1.9f));
            }
            else
            {
                localX = (rng.Chance(0.5f) ? -1f : 1f) * rng.Range(size.x * 0.34f, size.x * 0.48f);
                localZ = size.z * 0.5f + rng.Range(0.55f, 1.15f);
            }
            Vector3 offset = rotation * new Vector3(localX, 0f, localZ);
            Vector2 p = new Vector2(houseBase.x + offset.x, houseBase.z + offset.z);
            const float lotMargin = 1.6f;
            if (p.x < lot.rect.xMin + lotMargin || p.x > lot.rect.xMax - lotMargin ||
                p.y < lot.rect.yMin + lotMargin || p.y > lot.rect.yMax - lotMargin) continue;
            float groundY = lot.baseY;
            float yawRandom = rng.Range(0f, 360f);
            if (bush)
            {
                float radius = rng.Range(0.38f, 0.68f);
                OrientedBox footprint = OrientedBox.FromBase(new Vector3(p.x, groundY, p.y), new Vector3(radius * 2.1f, radius * 1.4f, radius * 2.1f), yawRandom);
                if (!c.occupancy.TryReserve(footprint, PlacementLayer.Prop, 0.12f, null)) continue;
                GameObject shrub = new GameObject("HouseGarden_Bush");
                shrub.transform.SetParent(root, true);
                shrub.transform.position = new Vector3(p.x, groundY + radius * 0.42f, p.y);
                shrub.transform.rotation = Quaternion.Euler(0f, yawRandom, 0f);
                Color[] greens = { new Color(0.20f, 0.39f, 0.16f), new Color(0.29f, 0.48f, 0.20f), new Color(0.38f, 0.52f, 0.24f) };
                Material leafMat = StylizedMaterialFactory.Get("foliage", greens[rng.Range(0, greens.Length)], rng.Range(0, 20));
                int clumps = rng.Range(3, 6);
                for (int j = 0; j < clumps; j++)
                {
                    GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    leaf.name = "BushCanopy"; leaf.transform.SetParent(shrub.transform, false);
                    leaf.transform.localPosition = new Vector3(rng.Range(-radius * 0.65f, radius * 0.65f), rng.Range(-radius * 0.12f, radius * 0.38f), rng.Range(-radius * 0.60f, radius * 0.60f));
                    float sx = rng.Range(radius * 0.58f, radius * 1.05f);
                    leaf.transform.localScale = new Vector3(sx, sx * rng.Range(0.60f, 0.82f), sx);
                    leaf.GetComponent<Renderer>().sharedMaterial = leafMat;
                    RemoveCollider(leaf);
                }
                bushCount++;
            }
            else
            {
                float s = gravel ? rng.Range(0.07f, 0.16f) : rng.Range(0.16f, 0.34f);
                OrientedBox footprint = OrientedBox.FromBase(new Vector3(p.x, groundY, p.y), new Vector3(s * 2.2f, s, s * 1.8f), yawRandom);
                if (!c.occupancy.TryReserve(footprint, PlacementLayer.Prop, 0.04f, null)) continue;
                GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stone.name = gravel ? "HouseGarden_Gravel" : "HouseGarden_Stone";
                stone.transform.SetParent(root, true);
                stone.transform.position = new Vector3(p.x, groundY + s * 0.18f, p.y);
                stone.transform.rotation = Quaternion.Euler(rng.Range(-18f, 18f), yawRandom, rng.Range(-18f, 18f));
                stone.transform.localScale = new Vector3(s * rng.Range(1.0f, 1.8f), s * rng.Range(0.42f, 0.82f), s * rng.Range(0.8f, 1.5f));
                Color stoneColor = rng.Chance(0.5f) ? new Color(0.39f, 0.40f, 0.36f) : new Color(0.53f, 0.50f, 0.43f);
                stone.GetComponent<Renderer>().sharedMaterial = StylizedMaterialFactory.Get("rock", stoneColor, rng.Range(0, 12));
                RemoveCollider(stone);
                stoneCount++;
            }
        }
    }

    static void RemoveCollider(GameObject go)
    {
        Collider[] cols = go.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (Application.isPlaying) WorldUtil.Kill(cols[i]);
            else Object.DestroyImmediate(cols[i]);
        }
    }

    static void CombineGardenMeshes(Transform root)
    {
        if (root == null) return;
        System.Collections.Generic.Dictionary<Material, System.Collections.Generic.List<CombineInstance>> groups =
            new System.Collections.Generic.Dictionary<Material, System.Collections.Generic.List<CombineInstance>>();
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter f = filters[i];
            if (f == null || f.sharedMesh == null) continue;
            Renderer r = f.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null) continue;
            System.Collections.Generic.List<CombineInstance> list;
            if (!groups.TryGetValue(r.sharedMaterial, out list)) { list = new System.Collections.Generic.List<CombineInstance>(); groups.Add(r.sharedMaterial, list); }
            CombineInstance ci = new CombineInstance(); ci.mesh = f.sharedMesh;
            ci.transform = root.worldToLocalMatrix * f.transform.localToWorldMatrix; list.Add(ci);
        }
        int batchIndex = 0;
        foreach (System.Collections.Generic.KeyValuePair<Material, System.Collections.Generic.List<CombineInstance>> pair in groups)
        {
            Mesh mesh = new Mesh(); mesh.name = "CombinedHouseGarden_" + batchIndex;
            int vertices = 0; for (int i = 0; i < pair.Value.Count; i++) vertices += pair.Value[i].mesh.vertexCount;
            if (vertices > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.CombineMeshes(pair.Value.ToArray(), true, true, false);
            GameObject batch = new GameObject("CombinedHouseGardenBatch_" + batchIndex++);
            batch.transform.SetParent(root, false);
            batch.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = batch.AddComponent<MeshRenderer>(); mr.sharedMaterial = pair.Key;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        }
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child.name.StartsWith("CombinedHouseGardenBatch_")) continue;
            if (Application.isPlaying) WorldUtil.Kill(child.gameObject); else Object.DestroyImmediate(child.gameObject);
        }
    }
}
