using UnityEngine;

public static class ModelFitter
{
    static readonly System.Collections.Generic.Dictionary<string, Vector3> footprintCache = new System.Collections.Generic.Dictionary<string, Vector3>();

    public static void ClearCache()
    {
        footprintCache.Clear();
    }

    public static Vector3 MeasureFootprint(ModelSlot slot, GameObject variant)
    {
        if (variant == null) return Vector3.zero;
        string key = variant.GetInstanceID() + "_" + slot.rotationOffset.ToString();
        Vector3 cached;
        if (footprintCache.TryGetValue(key, out cached)) return cached;
        GameObject temp = new GameObject("FootprintProbe");
        temp.transform.position = new Vector3(0f, -5000f, 0f);
        GameObject model = (GameObject)Object.Instantiate(variant);
        model.transform.SetParent(temp.transform, false);
        OrientModel(slot, model.transform, null, false);
        Renderer[] rs = model.GetComponentsInChildren<Renderer>(true);
        Vector3 size = Vector3.zero;
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            size = b.size;
        }
        Object.DestroyImmediate(temp);
        footprintCache[key] = size;
        return size;
    }

    public static GameObject Spawn(ModelSlot slot, Vector3 baseCenter, float yawDeg, Vector3 boxSize, Transform parent, System.Random rng, string semanticName = null, GameObject forcedVariant = null, bool nativeScale = false)
    {
        if (slot == null) slot = new ModelSlot();
        GameObject variant = forcedVariant != null ? forcedVariant : slot.PickVariant(rng);
        GameObject wrapper = new GameObject(variant != null ? variant.name : slot.primitive.ToString());
        wrapper.transform.SetParent(parent, false);
        wrapper.transform.SetPositionAndRotation(baseCenter, Quaternion.identity);
        GameObject fit = new GameObject("Fit");
        fit.transform.SetParent(wrapper.transform, false);

        Color tint = Color.white;
        if (slot.tints != null && slot.tints.Length > 0) tint = slot.tints[rng.Next(slot.tints.Length)];

        if (variant == null) BuildPrimitive(slot, fit.transform, boxSize, tint);
        else
        {
            Vector3 actual = BuildModel(slot, variant, fit.transform, baseCenter, boxSize, tint, rng, semanticName, nativeScale);
            if (nativeScale) boxSize = actual;
        }

        ApplyColliders(slot, wrapper, boxSize);
        // Some imported suburb-house slots are configured with ColliderMode.None. Give
        // those buildings a fallback volume so the player cannot walk straight through them.
        // Preserve detailed/model colliders when the slot already supplied any.
        if (IsBuildingSemantic(semanticName) && wrapper.GetComponentInChildren<Collider>(true) == null)
        {
            BoxCollider houseCollider = wrapper.AddComponent<BoxCollider>();
            houseCollider.center = new Vector3(0f, Mathf.Max(0.1f, boxSize.y * 0.5f), 0f);
            houseCollider.size = new Vector3(Mathf.Max(0.5f, boxSize.x), Mathf.Max(0.5f, boxSize.y), Mathf.Max(0.5f, boxSize.z));
        }
        // Building prefabs often contain a fence as separate named mesh parts. Their
        // broad house collider is not enough to stop the player passing through rails.
        if (variant != null) ApplyNamedFenceColliders(fit.transform);
        if (slot.markStatic) SetStatic(wrapper.transform);
        wrapper.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
        return wrapper;
    }

    static bool IsBuildingSemantic(string semanticName)
    {
        if (string.IsNullOrEmpty(semanticName)) return false;
        string n = semanticName.ToLowerInvariant();
        return n.Contains("house") || n.Contains("cottage") || n.Contains("building") ||
            n.Contains("office") || n.Contains("mall") || n.Contains("hotel") ||
            n.Contains("hospital") || n.Contains("apartment") || n.Contains("school") ||
            n.Contains("cinema") || n.Contains("station") || n.Contains("warehouse") ||
            n.Contains("clinic") || n.Contains("shop") || n.Contains("cafe") ||
            n.Contains("motel") || n.Contains("library") || n.Contains("workshop") ||
            n.Contains("plaza") || n.Contains("tower") || n.Contains("hall") ||
            n.Contains("fire station") || n.Contains("sports club") || n.Contains("garden center");
    }

    static void BuildPrimitive(ModelSlot slot, Transform fit, Vector3 box, Color tint)
    {
        GameObject prim = PrimitiveFactory.Create(slot.primitive, fit);
        prim.transform.localRotation = Quaternion.Euler(slot.rotationOffset);
        fit.localScale = box;
        fit.localPosition = new Vector3(0f, box.y * 0.5f, 0f) + slot.positionOffset;
        Renderer r = prim.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = slot.material != null ? slot.material : MaterialFactory.Get(tint);
    }

    static Vector3 BuildModel(ModelSlot slot, GameObject variant, Transform fit, Vector3 baseCenter, Vector3 box, Color tint, System.Random rng, string semanticName, bool nativeScale)
    {
        GameObject model = (GameObject)Object.Instantiate(variant);
        model.transform.SetParent(fit, false);
        model.transform.localPosition = Vector3.zero;

        OrientModel(slot, model.transform, rng, !nativeScale && slot.random90);

        Renderer[] rs = model.GetComponentsInChildren<Renderer>(true);
        string semantic = (semanticName ?? string.Empty).ToLowerInvariant();
        bool skyscraper = semantic.Contains("skyscraper") || semantic.Contains("tower") || variant.name.ToLowerInvariant().Contains("skyscraper");
        bool namedBuilding = semantic.Contains("house") || semantic.Contains("cottage") || semantic.Contains("building") ||
            semantic.Contains("office") || semantic.Contains("mall") || semantic.Contains("hotel") ||
            semantic.Contains("hospital") || semantic.Contains("apartment") || semantic.Contains("school") ||
            semantic.Contains("cinema") || semantic.Contains("station") || semantic.Contains("warehouse") ||
            semantic.Contains("clinic") || semantic.Contains("shop") || semantic.Contains("cafe") ||
            semantic.Contains("motel") || semantic.Contains("library") || semantic.Contains("workshop") ||
            semantic.Contains("plaza") || semantic.Contains("tower") || semantic.Contains("hall") ||
            semantic.Contains("fire station") || semantic.Contains("sports club") || semantic.Contains("garden center");
        bool isBuilding = !skyscraper && (namedBuilding || StylizedMaterialFactory.LooksLikeBuilding(model.transform));
        if (isBuilding)
        {
            string paletteSeed = (semanticName ?? variant.name) + "_" + Mathf.RoundToInt(baseCenter.x * 10f) + "_" + Mathf.RoundToInt(baseCenter.z * 10f);
            StylizedMaterialFactory.ApplyHouseMaterials(model.transform, box, paletteSeed);
        }
        else
        {
            if (slot.applyMaterialToModels)
            {
                Material m = slot.material != null ? slot.material : MaterialFactory.Get(tint);
                for (int i = 0; i < rs.Length; i++) rs[i].sharedMaterial = m;
            }
            if (!skyscraper) StylizedMaterialFactory.ApplyWorldMaterials(model.transform);
        }

        Bounds b = new Bounds(baseCenter + Vector3.up * 0.5f, Vector3.one);
        if (rs.Length > 0)
        {
            b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        }

        float bx = Mathf.Max(b.size.x, 0.0001f);
        float by = Mathf.Max(b.size.y, 0.0001f);
        float bz = Mathf.Max(b.size.z, 0.0001f);
        float rx = box.x / bx;
        float ry = box.y / by;
        float rz = box.z / bz;
        Vector3 s;
        if (nativeScale) s = Vector3.one;
        else switch (slot.fit)
        {
            case FitMode.Stretch:
                s = new Vector3(rx, ry, rz);
                break;
            case FitMode.UniformByVolume:
                float u = Mathf.Pow(Mathf.Max(box.x * box.y * box.z, 0.000001f) / (bx * by * bz), 1f / 3f);
                if (slot.clampToBox) u = Mathf.Min(u, Mathf.Min(rx, Mathf.Min(ry, rz)));
                s = new Vector3(u, u, u);
                break;
            default:
                float m2 = Mathf.Min(rx, Mathf.Min(ry, rz));
                s = new Vector3(m2, m2, m2);
                break;
        }

        Vector3 rel = new Vector3(b.center.x - baseCenter.x, b.min.y - baseCenter.y, b.center.z - baseCenter.z);
        fit.localScale = s;
        fit.localPosition = -Vector3.Scale(rel, s) + slot.positionOffset;
        return Vector3.Scale(b.size, s);
    }

    static void OrientModel(ModelSlot slot, Transform model, System.Random rng, bool random90)
    {
        Quaternion baseRotation = Quaternion.Euler(slot.rotationOffset);
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;

        Transform doorPart = FindDoorPartByName(model);
        if (doorPart == null)
        {
            int k = random90 && rng != null ? rng.Next(4) : 0;
            model.localRotation = Quaternion.Euler(0f, 90f * k, 0f) * baseRotation;
            return;
        }

        Renderer[] all = model.GetComponentsInChildren<Renderer>(true);
        Renderer[] doorRenderers = doorPart.GetComponentsInChildren<Renderer>(true);
        System.Collections.Generic.List<Renderer> body = new System.Collections.Generic.List<Renderer>();
        for (int i = 0; i < all.Length; i++)
        {
            string n = all[i].transform.name.ToLowerInvariant();
            if (n.Contains("door") || n.Contains("window") || n.Contains("platform") || n.Contains("fence") || n.Contains("foundation") || n.Contains("step") || n.Contains("path") || n.Contains("garden")) continue;
            body.Add(all[i]);
        }
        Bounds bodyBounds, doorBounds;
        bool haveBody = TryGetLocalBounds(model, body.Count > 0 ? body.ToArray() : all, out bodyBounds);
        if (!haveBody) { model.localRotation = baseRotation; return; }
        bool haveDoor = TryGetLocalBounds(model, doorRenderers, out doorBounds);
        if (!haveDoor) doorBounds = new Bounds(model.InverseTransformPoint(doorPart.position), Vector3.zero);

        float dx = (doorBounds.center.x - bodyBounds.center.x) / Mathf.Max(bodyBounds.extents.x, 0.001f);
        float dz = (doorBounds.center.z - bodyBounds.center.z) / Mathf.Max(bodyBounds.extents.z, 0.001f);
        Vector3 doorSide;
        float thinX = doorBounds.size.x, thinZ = doorBounds.size.z;
        bool decisive = haveDoor && Mathf.Min(thinX, thinZ) < 0.6f * Mathf.Max(thinX, thinZ);
        if (decisive)
        {
            if (thinX < thinZ) doorSide = new Vector3(dx >= 0f ? 1f : -1f, 0f, 0f);
            else doorSide = new Vector3(0f, 0f, dz >= 0f ? 1f : -1f);
        }
        else if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) < 0.2f)
        {
            float xMinGap = Mathf.Abs(doorBounds.min.x - bodyBounds.min.x);
            float xMaxGap = Mathf.Abs(bodyBounds.max.x - doorBounds.max.x);
            float zMinGap = Mathf.Abs(doorBounds.min.z - bodyBounds.min.z);
            float zMaxGap = Mathf.Abs(bodyBounds.max.z - doorBounds.max.z);
            float xGap = Mathf.Min(xMinGap, xMaxGap) / Mathf.Max(bodyBounds.size.x, 0.001f);
            float zGap = Mathf.Min(zMinGap, zMaxGap) / Mathf.Max(bodyBounds.size.z, 0.001f);
            if (xGap < zGap) doorSide = new Vector3(xMinGap <= xMaxGap ? -1f : 1f, 0f, 0f);
            else doorSide = new Vector3(0f, 0f, zMinGap <= zMaxGap ? -1f : 1f);
        }
        else if (Mathf.Abs(dx) > Mathf.Abs(dz)) doorSide = new Vector3(Mathf.Sign(dx), 0f, 0f);
        else doorSide = new Vector3(0f, 0f, Mathf.Sign(dz));

        Vector3 currentSide = baseRotation * doorSide;
        currentSide.y = 0f;
        if (currentSide.sqrMagnitude < 0.0001f) currentSide = Vector3.forward;
        float correctionYaw = Vector3.SignedAngle(currentSide.normalized, Vector3.forward, Vector3.up);
        model.localRotation = Quaternion.Euler(0f, correctionYaw, 0f) * baseRotation;
    }


    static bool TryGetLocalBounds(Transform root, Renderer[] renderers, out Bounds result)
    {
        result = new Bounds(Vector3.zero, Vector3.zero);
        bool found = false;
        if (root == null || renderers == null) return false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled) continue;
            Bounds b = r.bounds;
            for (int c = 0; c < 8; c++)
            {
                Vector3 wp = b.center + new Vector3(
                    (c & 1) == 0 ? -b.extents.x : b.extents.x,
                    (c & 2) == 0 ? -b.extents.y : b.extents.y,
                    (c & 4) == 0 ? -b.extents.z : b.extents.z);
                Vector3 lp = root.InverseTransformPoint(wp);
                if (!found) { result = new Bounds(lp, Vector3.zero); found = true; }
                else result.Encapsulate(lp);
            }
        }
        return found;
    }

    static Transform FindDoorPartByName(Transform root)
    {
        if (root == null) return null;

        Transform door = FindNamedPartRecursive(root, 3);
        if (door != null) return door;
        door = FindNamedPartRecursive(root, 2);
        if (door != null) return door;
        return FindNamedPartRecursive(root, 1);
    }

    static Transform FindNamedPartRecursive(Transform root, int matchKind)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            string n = child.name.ToLowerInvariant().Replace(" ", "").Replace("-", "_").Replace(".", "_");
            bool isDoor = n.Contains("door") && !n.Contains("garage") && !n.Contains("backdoor") && !n.Contains("back_door") && !n.Contains("indoor") && !n.Contains("outdoor");
            bool isFrame = n.Contains("frame") || n.Contains("fram");
            bool isEntrance = n == "entrance" || n == "entry" || n.Contains("entrance_") || n.Contains("entry_door");
            bool match = matchKind == 3 ? isDoor && !isFrame : matchKind == 2 ? isFrame : isEntrance;
            if (match) return child;
            Transform found = FindNamedPartRecursive(child, matchKind);
            if (found != null) return found;
        }
        return null;
    }

    static void ApplyColliders(ModelSlot slot, GameObject wrapper, Vector3 box)
    {
        if (slot.colliders != ColliderMode.KeepModel)
        {
            Collider[] cols = wrapper.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                cols[i].enabled = false;
                WorldUtil.Kill(cols[i]);
            }
        }
        if (slot.colliders == ColliderMode.Box || slot.colliders == ColliderMode.KeepModel)
        {
            // Exact MeshColliders on every mesh of the model (no coarse single box).
            MeshFilter[] mfs = wrapper.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < mfs.Length; i++)
            {
                if (mfs[i].sharedMesh == null || mfs[i].GetComponent<Renderer>() == null) continue;
                MeshCollider ex = mfs[i].GetComponent<MeshCollider>();
                if (ex == null) ex = mfs[i].gameObject.AddComponent<MeshCollider>();
                ex.sharedMesh = mfs[i].sharedMesh;
                ex.convex = false;
                ex.enabled = true;
            }
        }
    }


    static void ApplyNamedFenceColliders(Transform modelRoot)
    {
        if (modelRoot == null) return;
        MeshFilter[] filters = modelRoot.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter == null || filter.sharedMesh == null) continue;
            Transform t = filter.transform;
            bool fencePart = false;
            Transform cursor = t;
            while (cursor != null && cursor != modelRoot.parent)
            {
                string n = cursor.name.ToLowerInvariant().Replace(" ", "").Replace("-", "_");
                if (n.Contains("fence") || n.Contains("picket") || n.Contains("railing") ||
                    n.Contains("guardrail") || n.Contains("guard_rail"))
                {
                    fencePart = true;
                    break;
                }
                if (cursor == modelRoot) break;
                cursor = cursor.parent;
            }
            if (!fencePart) continue;

            // Avoid duplicate colliders if the prefab already had one (it may have been
            // removed by ApplyColliders above, but checking makes this safe for KeepModel).
            if (t.GetComponent<Collider>() != null) continue;
            MeshCollider mc = t.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = filter.sharedMesh;
            mc.convex = false;
        }
    }

    static void SetStatic(Transform t)
    {
        t.gameObject.isStatic = true;
        for (int i = 0; i < t.childCount; i++) SetStatic(t.GetChild(i));
    }
}
