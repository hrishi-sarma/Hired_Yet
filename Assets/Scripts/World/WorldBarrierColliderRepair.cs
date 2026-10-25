using System;
using UnityEngine;

/// <summary>
/// Safety net for generated railings and prefab fence meshes. Runs after world generation,
/// then repairs any named fence/rail mesh which lacks a collider.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldBarrierColliderRepair : MonoBehaviour
{
    WorldGenerator generator;
    WorldContext lastContext;
    float nextSearch;
    float nextRepair;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        GameObject go = GameObject.Find("World Barrier Collider Repair (Auto)");
        if (go == null) go = new GameObject("World Barrier Collider Repair (Auto)");
        if (go.GetComponent<WorldBarrierColliderRepair>() == null) go.AddComponent<WorldBarrierColliderRepair>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        if (Time.unscaledTime < nextSearch && generator == null) return;
        if (generator == null)
        {
            nextSearch = Time.unscaledTime + 0.5f;
            generator = FindFirstObjectByType<WorldGenerator>();
            if (generator == null) return;
        }

        if (Time.unscaledTime < nextRepair) return;
        nextRepair = Time.unscaledTime + 2f;
        WorldContext context = generator.Context;
        if (context == null || context.root == null) return;
        // Re-scan every two seconds after generation; this also catches fences instantiated
        // by a later placement stage and avoids scanning every rendered frame.
        RepairNamedMeshes(context.root);
        HideFencesNearHighway(context);
        lastContext = context;
    }

    static readonly System.Collections.Generic.HashSet<int> fenceChecked = new System.Collections.Generic.HashSet<int>();

    /// <summary>Removes house/prefab fences that sit on or beside the highway (and its end ramps) so they never cut through the highway rails.</summary>
    static void HideFencesNearHighway(WorldContext c)
    {
        if (c.roads == null || c.roads.highway == null || c.roads.highway.points.Count < 2) return;
        float half = 6f;
        if (c.generator != null)
        {
            RoadStage rs = c.generator.GetComponent<RoadStage>();
            if (rs != null) half = rs.highway.width * 0.5f + rs.highway.shoulder;
        }
        float margin = half + 6f;
        HighwayPath hw = c.roads.highway;
        Vector2 e0 = hw.points[0], e1 = hw.points[hw.points.Count - 1];
        Renderer[] rs2 = c.root.GetComponentsInChildren<Renderer>(false);
        for (int i = 0; i < rs2.Length; i++)
        {
            Renderer r = rs2[i];
            if (r == null || !fenceChecked.Add(r.GetInstanceID())) continue;
            string n = r.name.ToLowerInvariant();
            Transform up = r.transform.parent;
            if (up != null) n += "/" + up.name.ToLowerInvariant();
            if (!(n.Contains("fence") || n.Contains("picket"))) continue;
            if (n.Contains("highway") || n.Contains("barrier") || n.Contains("guardrail")) continue;
            if (n.Contains("perimeter") || n.Contains("combinedrailbatch")) continue; // generated district fence: its colliders stay, so it must stay visible
            Vector3 b = r.bounds.center;
            Vector2 p = new Vector2(b.x, b.z);
            float ext = Mathf.Max(r.bounds.extents.x, r.bounds.extents.z);
            bool near = hw.DistanceTo(p) < margin + ext
                || (p - e0).magnitude < margin + 14f + ext
                || (p - e1).magnitude < margin + 14f + ext;
            if (near) r.gameObject.SetActive(false);
        }
    }

    static void RepairNamedMeshes(Transform root)
    {
        if (root == null) return;
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        int repaired = 0;
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter == null || filter.sharedMesh == null) continue;
            if (!IsBarrierPart(filter.transform, root)) continue;
            Collider existing = filter.GetComponent<Collider>();
            if (existing != null) { existing.enabled = true; continue; }

            try
            {
                MeshCollider meshCollider = filter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = filter.sharedMesh;
                meshCollider.convex = false;
                meshCollider.enabled = true;
                repaired++;
            }
            catch (Exception)
            {
                // Fallback for a mesh which cannot be used by MeshCollider on this platform.
                Renderer renderer = filter.GetComponent<Renderer>();
                if (renderer == null) continue;
                BoxCollider box = filter.gameObject.AddComponent<BoxCollider>();
                Vector3 localSize = filter.transform.InverseTransformVector(renderer.bounds.size);
                box.size = new Vector3(Mathf.Max(0.08f, Mathf.Abs(localSize.x)),
                    Mathf.Max(0.08f, Mathf.Abs(localSize.y)), Mathf.Max(0.08f, Mathf.Abs(localSize.z)));
                box.center = filter.transform.InverseTransformPoint(renderer.bounds.center);
                repaired++;
            }
        }

        // Generated cube rail pieces usually already have BoxColliders. Re-enable them if a
        // later stage disabled them and provide one if a named rail was left collider-free.
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (!IsBarrierPart(t, root)) continue;
            Collider col = t.GetComponent<Collider>();
            if (col != null) { col.enabled = true; continue; }
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) continue; // handled in mesh pass above
            Renderer r = t.GetComponent<Renderer>();
            if (r == null) continue;
            BoxCollider box = t.gameObject.AddComponent<BoxCollider>();
            box.center = t.InverseTransformPoint(r.bounds.center);
            Vector3 size = t.InverseTransformVector(r.bounds.size);
            box.size = new Vector3(Mathf.Max(0.08f, Mathf.Abs(size.x)), Mathf.Max(0.08f, Mathf.Abs(size.y)), Mathf.Max(0.08f, Mathf.Abs(size.z)));
            repaired++;
        }
        if (repaired > 0) Debug.Log("WorldBarrierColliderRepair: added " + repaired + " missing fence/railing colliders.");
    }

    static bool IsBarrierPart(Transform t, Transform root)
    {
        Transform current = t;
        int depth = 0;
        while (current != null && depth++ < 8)
        {
            string n = current.name.ToLowerInvariant().Replace(" ", "").Replace("-", "_");
            if (n.Contains("reflector") || n.Contains("sign") || n.Contains("trafficlight") || n.Contains("water") || n.Contains("orb") || n.Contains("canopy") || n.Contains("flower") || n.Contains("lamphead") || n.Contains("lampcap")) return false;
            if (n.Contains("fence") || n.Contains("picket") || n.Contains("railing") ||
                n.Contains("guardrail") || n.Contains("guard_rail") || n.Contains("highwaybarrier_rail") ||
                n.Contains("highwaybarrier_post") || n.Contains("combinedrailbatch") ||
                n.Contains("bench") || n.Contains("trunk") || n.Contains("fountain") || n.Contains("streetlamp") ||
                n.Contains("hedge") || n.Contains("roadclosed") || n.Contains("wallcollider")) return true;
            if (current == root) break;
            current = current.parent;
        }
        return false;
    }
}
