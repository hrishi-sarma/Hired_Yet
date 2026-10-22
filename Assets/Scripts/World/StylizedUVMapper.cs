using UnityEngine;

/// <summary>
/// Remaps generated stylized textures using local planar projections measured in world units.
/// This prevents cube/primitive roads, sidewalks, and scaled props from stretching textures
/// across their full dimensions. Each renderer gets a private mesh copy; source prefab meshes
/// are never modified.
/// </summary>
[DisallowMultipleComponent]
public sealed class StylizedUVMapper : MonoBehaviour
{
    Mesh sourceMesh;
    Mesh mappedMesh;
    bool mappedAsHouse;
    int mappedHouseSeed;
    MeshFilter meshFilter;
    SkinnedMeshRenderer skinnedRenderer;

    public static void Apply(Renderer renderer)
    {
        ApplyInternal(renderer, false, 0);
    }

    public static void ApplyHouse(Renderer renderer, int seed)
    {
        ApplyInternal(renderer, true, seed);
    }

    static void ApplyInternal(Renderer renderer, bool houseMode, int seed)
    {
        if (renderer == null) return;
        // Asphalt must use the same world-metre projection as other generated surfaces.
        // Leaving primitive road meshes on their default 0-1 UVs stretched the entire
        // asphalt texture across each long road segment. GetTileWorldSize controls scale.

        // Imported model meshes often have Read/Write disabled. Do not call vertices,
        // normals, or triangle APIs on those meshes: Unity throws errors for them.
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        Mesh candidate = filter != null ? filter.sharedMesh :
            (skinned != null ? skinned.sharedMesh : null);
        if (candidate == null || !candidate.isReadable)
        {
            if (candidate != null && !candidate.isReadable)
                WarnUnreadableOnce(candidate);
            return;
        }

        StylizedUVMapper mapper = renderer.GetComponent<StylizedUVMapper>();
        if (mapper == null) mapper = renderer.gameObject.AddComponent<StylizedUVMapper>();
        mapper.RemapIfNeeded(renderer, houseMode, seed);
    }

    static readonly System.Collections.Generic.HashSet<int> warnedMeshes =
        new System.Collections.Generic.HashSet<int>();

    static void WarnUnreadableOnce(Mesh mesh)
    {
        if (mesh == null || !warnedMeshes.Add(mesh.GetInstanceID())) return;
        Debug.LogWarning("[StylizedUVMapper] Skipping UV remapping for mesh '" + mesh.name +
            "' because Read/Write is disabled in its import settings. Materials remain assigned; " +
            "enable Model Import Settings > Read/Write if this mesh needs custom UV remapping.");
    }

    void RemapIfNeeded(Renderer renderer, bool houseMode, int seed)
    {
        meshFilter = renderer.GetComponent<MeshFilter>();
        skinnedRenderer = renderer as SkinnedMeshRenderer;
        Mesh current = meshFilter != null ? meshFilter.sharedMesh :
            (skinnedRenderer != null ? skinnedRenderer.sharedMesh : null);
        if (current == null || !current.isReadable) return;
        if (current == mappedMesh)
        {
            if (mappedAsHouse == houseMode && (!houseMode || mappedHouseSeed == seed)) return;
            current = sourceMesh != null ? sourceMesh : current;
        }

        DestroyMappedMesh();
        sourceMesh = current;
        mappedMesh = Instantiate(sourceMesh);
        mappedMesh.name = sourceMesh.name + "_WorldScaledUV";

        Vector3[] vertices = mappedMesh.vertices;
        Vector3[] normals = mappedMesh.normals;
        Vector2[] uv = new Vector2[vertices.Length];

        // Use world-space coordinates, not each object's local origin. Separate sidewalk
        // segments, curb pieces, and grass-base pieces therefore share the same texture phase
        // at their boundaries instead of restarting the pattern on every primitive.
        // The asphalt texture itself is unchanged; only the UV coordinate phase is continuous.
        if (normals == null || normals.Length != vertices.Length)
        {
            mappedMesh.RecalculateNormals();
            normals = mappedMesh.normals;
        }

        Transform tr = renderer.transform;
        bool plasterMaterial = false;
        if (houseMode)
        {
            Material[] mats = renderer.sharedMaterials;
            for (int m = 0; mats != null && m < mats.Length; m++)
                if (mats[m] != null && mats[m].name.ToLowerInvariant().Contains("house_plaster")) { plasterMaterial = true; break; }
        }
        int quarterTurnsForHouse = houseMode ? Mathf.Abs(seed % 4) : 0;
        // House UVs intentionally use a renderer-local origin. Rotating absolute world
        // coordinates can look almost identical on small houses and can shift patches by
        // whole texture periods. Centering first makes the per-house orientation visibly
        // deterministic while leaving non-house/world UVs continuous across adjacent tiles.
        Vector3 houseOrigin = renderer.bounds.center;
        float houseOffsetU = houseMode ? (Mathf.Abs(seed % 997) / 997f) : 0f;
        float houseOffsetV = houseMode ? (Mathf.Abs(seed % 991) / 991f) : 0f;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = tr.TransformPoint(vertices[i]);
            Vector3 n = i < normals.Length ? tr.TransformDirection(normals[i]).normalized : Vector3.up;
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            Vector3 projected = houseMode ? world - houseOrigin : world;
            if (ay >= ax && ay >= az)
                uv[i] = new Vector2(projected.x, projected.z); // top/bottom
            else if (ax >= az)
                uv[i] = new Vector2(projected.z, projected.y); // left/right
            else
                uv[i] = new Vector2(projected.x, projected.y); // front/back

            if (houseMode)
            {
                // Per-house quarter-turn rotation plus a stable fractional offset ensures
                // the plaster brush patches visibly differ from one small house to another.
                Vector2 q = uv[i];
                if (quarterTurnsForHouse == 1) uv[i] = new Vector2(-q.y, q.x);
                else if (quarterTurnsForHouse == 2) uv[i] = new Vector2(-q.x, -q.y);
                else if (quarterTurnsForHouse == 3) uv[i] = new Vector2(q.y, -q.x);
                if (plasterMaterial) uv[i] += new Vector2(houseOffsetU, houseOffsetV);
            }
        }

        mappedMesh.uv = uv;
        mappedMesh.RecalculateBounds();
        if (meshFilter != null) meshFilter.sharedMesh = mappedMesh;
        else if (skinnedRenderer != null) skinnedRenderer.sharedMesh = mappedMesh;
        mappedAsHouse = houseMode;
        mappedHouseSeed = seed;
    }

    void DestroyMappedMesh()
    {
        if (mappedMesh == null) return;
        if (Application.isPlaying) WorldUtil.Kill(mappedMesh);
        else DestroyImmediate(mappedMesh);
        mappedMesh = null;
    }

    void OnDestroy()
    {
        DestroyMappedMesh();
    }
}
