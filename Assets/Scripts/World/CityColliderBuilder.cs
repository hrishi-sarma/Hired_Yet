using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds ACCURATE colliders for the city: walls, benches, stairs, fountains, fences, trees and every part
/// of the city park / plaza. Self-installing, works on generated or baked worlds, no setup needed.
///
/// Why this exists: ModelFitter's "Box" collider mode deletes every collider of an imported model and puts ONE
/// box around the whole thing, so stairs become a ramp-block, benches become solid cubes, walls ignore gaps, and
/// trees block with their whole canopy. This script replaces those coarse colliders with real ones:
///   - stairs, benches, walls, fences, fountains, arches ... -> non-convex MeshCollider (exact shape)
///   - trees                                                  -> thin trunk capsule only (canopy never blocks)
///   - every other solid park part                            -> convex hull (or exact mesh if large)
///   - decor (grass, flowers, leaves, water, glass, lamp glow, flat decals) never gets a collider.
/// </summary>
[DisallowMultipleComponent]
public sealed class CityColliderBuilder : MonoBehaviour
{
    enum Kind { None, Concave, Solid, Trunk }

    // Hierarchy names that mark the city park / walls. Everything under them is treated as a park part.
    static readonly string[] Roots = { "cityplaza", "plazamodel", "plazadecor", "citypark", "cityborder", "citywall", "citywalls" };
    // Always handled, anywhere in the scene.
    static readonly string[] GlobalConcave = { "stair", "step", "bench", "fountain" };
    // Handled when inside a root above.
    static readonly string[] ScopedConcave = { "wall", "fence", "rail", "gazebo", "pavilion", "arch", "bridge", "pergola", "planter", "basin", "gate", "fountain" };
    static readonly string[] TreeWords = { "tree", "palm", "pine", "oak", "birch", "willow", "cypress" };
    static readonly string[] TrunkWords = { "trunk", "stem", "bark", "log" };
    static readonly string[] Skip =
    {
        "grass", "flower", "petal", "water", "glow", "light", "lamphead", "lampcap", "shadow", "decal", "mist", "fog",
        "canopy", "leaf", "leaves", "foliage", "crown", "glass", "window", "sign", "cloud", "collider", "probe"
    };

    readonly HashSet<int> done = new HashSet<int>();
    float next, stamp;
    bool scanning;
    int stairs, benches, walls, trunks, parts, wrappers, fallbacks;
    string toast;
    float toastUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<CityColliderBuilder>() != null) return;
        GameObject go = new GameObject("City Collider Builder (Auto)");
        go.AddComponent<CityColliderBuilder>();
        DontDestroyOnLoad(go);
    }

    void OnEnable() { SceneManager.sceneLoaded += OnScene; stamp = Time.unscaledTime; }
    void OnDisable() { SceneManager.sceneLoaded -= OnScene; }
    void OnScene(Scene s, LoadSceneMode m) { done.Clear(); stamp = Time.unscaledTime; next = 0f; }

    void Update()
    {
        if (scanning || Time.unscaledTime < next) return;
        next = Time.unscaledTime + (Time.unscaledTime - stamp < 90f || WorldLauncher.Launching ? 3f : 20f);
        StartCoroutine(Scan());
    }

    // ---------------------------------------------------------------- classification
    static string Norm(string n) { return n.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", ""); }

    static bool Has(string s, string[] list)
    {
        for (int i = 0; i < list.Length; i++) if (s.Contains(list[i])) return true;
        return false;
    }

    static bool IsRootName(string n)
    {
        if (Has(n, Roots)) return true;
        return n.Contains("park") && !n.Contains("parking");
    }

    static Kind Classify(Transform t, out bool scoped)
    {
        scoped = false;
        string own = Norm(t.name);
        if (Has(own, Skip)) return Kind.None;
        if (t.parent != null && Norm(t.parent.name).Contains("water")) return Kind.None;

        string near = own;
        Transform c = t.parent;
        for (int d = 0; d < 12 && c != null; d++, c = c.parent)
        {
            string n = Norm(c.name);
            if (d < 3) near += "/" + n;
            if (IsRootName(n)) scoped = true;
        }

        if (Has(near, TreeWords) && (scoped || own.Contains("tree") || near.Contains("tree")))
            return Has(own, TrunkWords) ? Kind.Solid : Kind.Trunk;
        if (Has(near, GlobalConcave)) return Kind.Concave;
        if (scoped && Has(near, ScopedConcave)) return Kind.Concave;
        if (scoped) return Kind.Solid;
        return Kind.None;
    }

    // ---------------------------------------------------------------- scan
    IEnumerator Scan()
    {
        scanning = true;
        MeshFilter[] fs = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        int slice = 0, cooked = 0, before = parts;
        for (int i = 0; i < fs.Length; i++)
        {
            if (++slice > 400) { slice = 0; yield return null; }
            MeshFilter f = fs[i];
            if (f == null || f.sharedMesh == null) continue;
            int id = f.GetInstanceID();
            if (done.Contains(id)) continue;
            Renderer r = f.GetComponent<Renderer>();
            if (r == null) continue;
            done.Add(id);

            bool scoped;
            Kind k = Classify(f.transform, out scoped);
            if (k == Kind.None) continue;
            Bounds b = r.bounds;
            if (b.size.y < 0.12f && k != Kind.Concave) continue;       // flat decals / paving: ground handles it
            if (b.size.magnitude < 0.08f) continue;

            Apply(f, r, k, scoped);
            if (++cooked % 20 == 0) yield return null;
        }
        Physics.SyncTransforms();
        if (parts != before)
        {
            toast = "City colliders rebuilt: " + parts + " parts (stairs " + stairs + ", benches " + benches + ", walls " + walls +
                    ", tree trunks " + trunks + ", coarse model boxes removed " + wrappers + ")";
            Debug.Log(toast);
            toastUntil = Time.unscaledTime + 10f;
        }
        scanning = false;
    }

    // ---------------------------------------------------------------- build
    static bool Exact(Collider c, Mesh m, bool needNonConvex)
    {
        MeshCollider mc = c as MeshCollider;
        if (mc != null) return mc.sharedMesh == m && (!needNonConvex || !mc.convex);
        if (c is BoxCollider) return m.name.StartsWith("Cube") && m.vertexCount <= 24;
        if (c is SphereCollider) return !needNonConvex && m.name.StartsWith("Sphere");
        return false;
    }

    void Apply(MeshFilter f, Renderer r, Kind k, bool scoped)
    {
        Mesh m = f.sharedMesh;
        GameObject go = f.gameObject;
        string near = Norm(go.name) + "/" + (go.transform.parent != null ? Norm(go.transform.parent.name) : "");

        if (scoped) RemoveCoarseWrappers(go.transform);

        Collider[] cols = go.GetComponents<Collider>();
        bool needNonConvex = k == Kind.Concave;

        if (k == Kind.Trunk)
        {
            for (int i = 0; i < cols.Length; i++) if (cols[i] != null && !cols[i].isTrigger) WorldUtil.Kill(cols[i]);
            AddTrunk(f, r);
            trunks++; parts++;
            return;
        }

        bool ok = false;
        bool anyOther = false;
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null || cols[i].isTrigger) continue;
            if (Exact(cols[i], m, needNonConvex)) { cols[i].enabled = true; ok = true; }
            else anyOther = true;
        }
        if (ok && !anyOther) { Count(near, k); return; }

        for (int i = 0; i < cols.Length; i++) if (cols[i] != null && !cols[i].isTrigger && !Exact(cols[i], m, needNonConvex)) WorldUtil.Kill(cols[i]);
        if (ok) { Count(near, k); return; }

        if (!m.isReadable && !Application.isEditor)
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.center = m.bounds.center;
            bc.size = m.bounds.size;
            fallbacks++;
        }
        else
        {
            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = m;
            mc.convex = !needNonConvex && m.vertexCount <= 3000 && r.bounds.size.magnitude < 14f;
        }
        Count(near, k);
    }

    void Count(string near, Kind k)
    {
        parts++;
        if (near.Contains("stair") || near.Contains("step")) stairs++;
        else if (near.Contains("bench")) benches++;
        else if (near.Contains("wall") || near.Contains("fence") || near.Contains("rail")) walls++;
    }

    /// <summary>Delete the single big Box/Capsule/Sphere ModelFitter leaves on a model wrapper (no mesh of its own).</summary>
    void RemoveCoarseWrappers(Transform part)
    {
        Transform a = part.parent;
        for (int d = 0; d < 12 && a != null; d++, a = a.parent)
        {
            string n = Norm(a.name);
            bool isRoot = IsRootName(n);
            if (a.GetComponent<MeshFilter>() == null && a.GetComponent<MeshRenderer>() == null && !n.Contains("collider") && !n.Contains("tunnel") && !n.Contains("roadclosed"))
            {
                Collider[] cs = a.GetComponents<Collider>();
                for (int i = 0; i < cs.Length; i++)
                {
                    Collider c = cs[i];
                    if (c == null || c.isTrigger || c is MeshCollider || c is TerrainCollider) continue;
                    WorldUtil.Kill(c);
                    wrappers++;
                }
            }
            if (isRoot) break;
        }
    }

    /// <summary>Thin vertical capsule around the real trunk, measured from the lowest vertices of the tree mesh.</summary>
    void AddTrunk(MeshFilter f, Renderer r)
    {
        Bounds wb = r.bounds;
        float height = Mathf.Min(wb.size.y, 3.2f);
        Vector3 centre = new Vector3(wb.center.x, wb.min.y, wb.center.z);
        float radius = Mathf.Clamp(Mathf.Min(wb.extents.x, wb.extents.z) * 0.12f, 0.12f, 1f);

        if (f.sharedMesh.isReadable)
        {
            Vector3[] v = f.sharedMesh.vertices;
            Matrix4x4 M = f.transform.localToWorldMatrix;
            float lim = wb.min.y + Mathf.Max(0.4f, wb.size.y * 0.12f);
            Vector3 sum = Vector3.zero;
            int n = 0;
            List<Vector3> low = new List<Vector3>();
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 w = M.MultiplyPoint3x4(v[i]);
                if (w.y > lim) continue;
                low.Add(w);
                sum += w;
                n++;
            }
            if (n >= 3)
            {
                Vector3 avg = sum / n;
                float rr = 0f;
                for (int i = 0; i < low.Count; i++)
                {
                    float dx = low[i].x - avg.x, dz = low[i].z - avg.z;
                    rr = Mathf.Max(rr, Mathf.Sqrt(dx * dx + dz * dz));
                }
                centre = new Vector3(avg.x, wb.min.y, avg.z);
                radius = Mathf.Clamp(rr * 0.95f, 0.1f, 1.5f);
            }
        }

        GameObject g = new GameObject("TrunkCollider");
        g.transform.SetParent(f.transform, true);
        g.transform.position = centre + Vector3.up * (height * 0.5f);
        g.transform.rotation = Quaternion.identity;
        Vector3 ls = f.transform.lossyScale;
        g.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, ls.x), 1f / Mathf.Max(0.0001f, ls.y), 1f / Mathf.Max(0.0001f, ls.z));
        CapsuleCollider cc = g.AddComponent<CapsuleCollider>();
        cc.direction = 1;
        cc.radius = radius;
        cc.height = Mathf.Max(height, radius * 2f);
    }

}
