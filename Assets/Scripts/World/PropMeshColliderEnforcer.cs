using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Guarantees an exact (non-convex) MeshCollider on every solid city prop: walls, road blocks, stairs, chairs,
/// benches, bus stands/shelters, fences, bollards, planters, tables, fountains, arches...
/// Runs automatically at play, rescans while the world generates, and replaces coarse Box/Capsule colliders.
/// Selected MeshColliders are drawn in the Scene view (cyan wire) so you can SEE them.
/// Editor menu: Tools/World/Bake Prop Mesh Colliders (saves them into the scene).
/// </summary>
[DisallowMultipleComponent]
public sealed class PropMeshColliderEnforcer : MonoBehaviour
{
    static readonly string[] Solid =
    {
        "chair", "stair", "step", "bench", "busstand", "busstop", "busshelter", "shelter", "bus", "stand", "kiosk",
        "wall", "citywall", "cityborder", "barrier", "roadblock", "roadclosed", "block", "barricade", "bollard",
        "fence", "rail", "picket", "hedge", "planter", "table", "trash", "bin", "hydrant", "mailbox", "fountain",
        "arch", "gate", "pergola", "gazebo", "pavilion", "bridge", "basin", "post", "pole", "lamppost", "streetlamp",
        "plaza", "citypark", "prop", "curb", "ramp", "tunnel", "stub", "rock", "boulder", "statue", "sofa", "crate", "box"
    };
    static readonly string[] Skip =
    {
        "grass", "flower", "petal", "water", "glow", "lamphead", "lampcap", "shadow", "decal", "mist", "fog",
        "canopy", "leaf", "leaves", "foliage", "crown", "glass", "window", "cloud", "probe", "reflector", "orb",
        "tree", "palm", "pine", "birch", "willow", "cypress", "vehicle", "npc", "pedestrian", "animal", "stripe", "marking", "crosswalk", "zebra", "lawncurb", "trafficlight", "sky"
    };

    readonly HashSet<int> done = new HashSet<int>();
    static Mesh cube;
    float next, stamp;
    bool scanning;
    public int meshColliders, boxesReplaced;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<PropMeshColliderEnforcer>() != null) return;
        GameObject go = new GameObject("Prop Mesh Collider Enforcer (Auto)");
        go.AddComponent<PropMeshColliderEnforcer>();
        DontDestroyOnLoad(go);
    }

    void OnEnable() { SceneManager.sceneLoaded += OnScene; stamp = Time.unscaledTime; }
    void OnDisable() { SceneManager.sceneLoaded -= OnScene; }
    void OnScene(Scene s, LoadSceneMode m) { done.Clear(); stamp = Time.unscaledTime; next = 0f; }

    void Update()
    {
        if (scanning || Time.unscaledTime < next) return;
        next = Time.unscaledTime + (Time.unscaledTime - stamp < 120f || WorldLauncher.Launching ? 1.5f : 10f);
        StartCoroutine(Scan());
    }

    static string Norm(string n) { return n.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", ""); }
    static bool Has(string s, string[] l) { for (int i = 0; i < l.Length; i++) if (s.Contains(l[i])) return true; return false; }

    /// <summary>True if this object (or an ancestor up to 10 levels) is a solid prop and nothing decorative.</summary>
    public static bool IsSolidProp(Transform t)
    {
        string own = Norm(t.name);
        if (Has(own, Skip)) return false;
        Transform c = t;
        for (int d = 0; d < 10 && c != null; d++, c = c.parent)
        {
            string n = Norm(c.name);
            if (d > 0 && Has(n, new[] { "water", "canopy", "foliage", "tree", "vehicle", "npc", "pedestrian" })) return false;
            if (Has(n, Solid)) return true;
        }
        return false;
    }

    static bool Flat(MeshFilter f)
    {
        Renderer r = f.GetComponent<Renderer>();
        if (r == null) return true;
        string n = Norm(f.name);
        bool must = n.Contains("stair") || n.Contains("step") || n.Contains("wall") || n.Contains("curb");
        return !must && r.bounds.size.y < 0.12f;
    }

    static Mesh CubeMesh()
    {
        if (cube != null) return cube;
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube = g.GetComponent<MeshFilter>().sharedMesh;
        WorldUtil.Kill(g);
        return cube;
    }

    IEnumerator Scan()
    {
        scanning = true;
        MeshFilter[] fs = FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int slice = 0, cooked = 0;
        for (int i = 0; i < fs.Length; i++)
        {
            if (++slice > 300) { slice = 0; yield return null; }
            MeshFilter f = fs[i];
            if (f == null || f.sharedMesh == null) continue;
            int id = f.GetInstanceID();
            if (done.Contains(id)) continue;
            if (f.GetComponent<Renderer>() == null) continue;
            done.Add(id);
            if (!IsSolidProp(f.transform) || Flat(f)) continue;
            if (EnsureMeshCollider(f)) { if (++cooked % 15 == 0) yield return null; }
        }
        ConvertBoxHelpers();
        Physics.SyncTransforms();
        scanning = false;
    }

    /// <summary>Remove any non-mesh collider on this object and add an exact MeshCollider. Returns true if added.</summary>
    public bool EnsureMeshCollider(MeshFilter f)
    {
        GameObject go = f.gameObject;
        Mesh m = f.sharedMesh;
        MeshCollider[] mcs = go.GetComponents<MeshCollider>();
        for (int i = 0; i < mcs.Length; i++)
            if (mcs[i].sharedMesh == m && !mcs[i].convex) { mcs[i].enabled = true; mcs[i].isTrigger = false; return false; }

        Collider[] cols = go.GetComponents<Collider>();
        for (int i = 0; i < cols.Length; i++)
            if (cols[i] != null && !cols[i].isTrigger) { WorldUtil.Kill(cols[i]); boxesReplaced++; }

        if (!m.isReadable && !Application.isEditor)
        {
            // Runtime-added MeshColliders need readable meshes in a build; tight local box is the only safe fallback.
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.center = m.bounds.center; bc.size = m.bounds.size;
            return true;
        }
        MeshCollider mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = m;
        mc.convex = false;
        meshColliders++;
        return true;
    }

    /// <summary>Generated wall / road-block collider helpers (cube-sized BoxColliders) become real MeshColliders.</summary>
    void ConvertBoxHelpers()
    {
        BoxCollider[] bs = FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < bs.Length; i++)
        {
            BoxCollider b = bs[i];
            if (b == null || b.isTrigger || b.GetComponent<MeshFilter>() != null) continue;
            string n = Norm(b.name);
            if (!(n.Contains("wallcollider") || n.Contains("wallpostcollider") || n.Contains("barriercollider") || n.Contains("tunnelcollider"))) continue;
            GameObject g = b.gameObject;
            Vector3 size = b.size, center = b.center;
            WorldUtil.Kill(b);
            GameObject child = new GameObject("MeshCol");
            child.transform.SetParent(g.transform, false);
            child.transform.localPosition = center;
            child.transform.localScale = size;
            child.isStatic = true;
            MeshCollider mc = child.AddComponent<MeshCollider>();
            mc.sharedMesh = CubeMesh();
            mc.convex = false;
            meshColliders++;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
        MeshCollider[] all = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            MeshCollider mc = all[i];
            if (mc == null || mc.sharedMesh == null || !mc.enabled) continue;
            Gizmos.DrawWireMesh(mc.sharedMesh, mc.transform.position, mc.transform.rotation, mc.transform.lossyScale);
        }
    }

#if UNITY_EDITOR
    [MenuItem("Tools/World/Bake Prop Mesh Colliders")]
    static void Bake()
    {
        GameObject tmp = new GameObject("tmp");
        PropMeshColliderEnforcer e = tmp.AddComponent<PropMeshColliderEnforcer>();
        MeshFilter[] fs = FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int n = 0;
        for (int i = 0; i < fs.Length; i++)
        {
            if (fs[i].sharedMesh == null || fs[i].GetComponent<Renderer>() == null) continue;
            if (!IsSolidProp(fs[i].transform) || Flat(fs[i])) continue;
            if (e.EnsureMeshColliderEditor(fs[i])) n++;
        }
        DestroyImmediate(tmp);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Baked " + n + " prop MeshColliders into the scene.");
    }

    bool EnsureMeshColliderEditor(MeshFilter f)
    {
        GameObject go = f.gameObject;
        foreach (MeshCollider x in go.GetComponents<MeshCollider>()) if (x.sharedMesh == f.sharedMesh && !x.convex) return false;
        foreach (Collider c in go.GetComponents<Collider>()) if (!c.isTrigger) Undo.DestroyObjectImmediate(c);
        MeshCollider mc = Undo.AddComponent<MeshCollider>(go);
        mc.sharedMesh = f.sharedMesh; mc.convex = false;
        return true;
    }
#endif
}
