using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[AddComponentMenu("Procedural World/Cinematic Effects")]
public class CinematicEffects : MonoBehaviour
{
    public static CinematicEffects Instance;

    [Header("Master")]
    public bool enableAll = true;

    [Header("Depth of Field (far distance)")]
    public bool depthOfField = false;
    [Min(0f)] public float dofStart = 140f;
    [Min(1f)] public float dofEnd = 420f;
    [Range(0.5f, 5f)] public float dofMaxRadius = 1.5f;
    public bool dofHighQuality = false;

    [Header("Film Grain")]
    public bool filmGrain = false;
    public FilmGrainLookup grainType = FilmGrainLookup.Thin1;
    [Range(0f, 1f)] public float grainIntensity = 0.18f;
    [Range(0f, 1f)] public float grainResponse = 0.8f;

    [Header("Chromatic Aberration")]
    public bool chromaticAberration = false;
    [Range(0f, 1f)] public float chromaticIntensity = 0.12f;

    [Header("Outlines")]
    public bool outlines = false;
    public bool outlineBuildings = true;
    public bool outlineCharacters = true;
    public Color outlineColor = new Color(0.08f, 0.06f, 0.08f, 1f);
    [Range(0f, 1f)] public float outlineOpacity = 0.55f;
    [Range(0f, 0.15f)] public float buildingWidth = 0.03f;
    [Range(0f, 0.15f)] public float characterWidth = 0.02f;
    public float fadeStart = 60f;
    public float fadeEnd = 160f;
    public float maxActiveDistance = 180f;
    public float minPartSize = 0.8f;
    public int maxOutlinedRenderers = 2500;
    public bool excludePlayer = true;
    [Min(0f)] public float nearHideDistance = 3f;

    Volume volume;
    VolumeProfile profile;
    DepthOfField dof;
    FilmGrain grain;
    ChromaticAberration ca;
    Camera cam;
    Material buildingMat, characterMat;
    Shader outlineShader;
    float checkTimer, scanTimer, camTimer;
    bool shaderWarned;

    class Hull
    {
        public Renderer src;
        public Renderer hull;
        public bool character;
    }

    readonly List<Hull> hulls = new List<Hull>();
    readonly HashSet<Renderer> done = new HashSet<Renderer>();
    static readonly Dictionary<Mesh, Mesh> smoothCache = new Dictionary<Mesh, Mesh>();

    public static CinematicEffects Ensure()
    {
        if (Instance != null) return Instance;
        Instance = FindFirstObjectByType<CinematicEffects>();
        if (Instance == null) Instance = new GameObject("CinematicEffects").AddComponent<CinematicEffects>();
        return Instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        Ensure();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { WorldUtil.Kill(gameObject); return; }
        Instance = this;
        BuildVolume();
    }

    void BuildVolume()
    {
        GameObject go = new GameObject("CinematicVolume");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 20f;
        volume.weight = 1f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.DontSave;
        dof = profile.Add<DepthOfField>(true);
        grain = profile.Add<FilmGrain>(true);
        ca = profile.Add<ChromaticAberration>(true);
        volume.sharedProfile = profile;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (profile != null) WorldUtil.Kill(profile);
    }

    public void AddBuilding(Renderer r)
    {
        if (r == null || done.Contains(r) || hulls.Count >= maxOutlinedRenderers) return;
        Bounds b = r.bounds;
        if (Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) < minPartSize) return;
        AddHull(r, false);
    }

    void AddHull(Renderer src, bool character)
    {
        done.Add(src);
        if (!EnsureShader()) return;
        Material m = character ? characterMat : buildingMat;
        GameObject go = new GameObject("Outline");
        go.transform.SetParent(src.transform, false);
        Renderer hr;
        SkinnedMeshRenderer smr = src as SkinnedMeshRenderer;
        if (smr != null)
        {
            if (smr.sharedMesh == null) { WorldUtil.Kill(go); return; }
            SkinnedMeshRenderer h = go.AddComponent<SkinnedMeshRenderer>();
            h.sharedMesh = smr.sharedMesh;
            h.bones = smr.bones;
            h.rootBone = smr.rootBone;
            h.updateWhenOffscreen = smr.updateWhenOffscreen;
            hr = h;
        }
        else
        {
            MeshFilter mf = src.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { WorldUtil.Kill(go); return; }
            go.AddComponent<MeshFilter>().sharedMesh = Smooth(mf.sharedMesh);
            hr = go.AddComponent<MeshRenderer>();
        }
        int subs = 1;
        if (smr != null) subs = smr.sharedMesh.subMeshCount;
        else subs = src.GetComponent<MeshFilter>().sharedMesh.subMeshCount;
        Material[] arr = new Material[Mathf.Max(1, subs)];
        for (int i = 0; i < arr.Length; i++) arr[i] = m;
        hr.sharedMaterials = arr;
        hr.shadowCastingMode = ShadowCastingMode.Off;
        hr.receiveShadows = false;
        hr.enabled = false;
        Hull e = new Hull();
        e.src = src;
        e.hull = hr;
        e.character = character;
        hulls.Add(e);
    }

    bool EnsureShader()
    {
        if (buildingMat != null && characterMat != null) return true;
        outlineShader = Shader.Find("Stylized/InvertedHullOutline");
        if (outlineShader == null)
        {
            if (!shaderWarned)
            {
                Debug.LogWarning("CinematicEffects: shader 'Stylized/InvertedHullOutline' not found. Put StylizedOutline.shader inside a folder named Resources (or add it to Always Included Shaders).");
                shaderWarned = true;
            }
            return false;
        }
        buildingMat = new Material(outlineShader);
        buildingMat.name = "OutlineBuildings";
        characterMat = new Material(outlineShader);
        characterMat.name = "OutlineCharacters";
        return true;
    }

    static Mesh Smooth(Mesh m)
    {
        Mesh cached;
        if (smoothCache.TryGetValue(m, out cached) && cached != null) return cached;
        if (!m.isReadable || m.vertexCount > 65000) return m;
        Vector3[] v = m.vertices;
        Vector3[] n = m.normals;
        if (n.Length != v.Length) return m;
        Dictionary<Vector3Int, Vector3> sum = new Dictionary<Vector3Int, Vector3>();
        Vector3Int[] keys = new Vector3Int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            keys[i] = new Vector3Int(Mathf.RoundToInt(v[i].x * 500f), Mathf.RoundToInt(v[i].y * 500f), Mathf.RoundToInt(v[i].z * 500f));
            Vector3 s;
            sum.TryGetValue(keys[i], out s);
            sum[keys[i]] = s + n[i];
        }
        Vector3[] nn = new Vector3[n.Length];
        for (int i = 0; i < n.Length; i++) nn[i] = sum[keys[i]].normalized;
        Mesh c = UnityEngine.Object.Instantiate(m);
        c.name = m.name + "_Outline";
        c.normals = nn;
        smoothCache[m] = c;
        return c;
    }

    Transform PlayerRoot()
    {
        GameObject player = null;
        try { player = GameObject.FindWithTag("Player"); } catch (UnityException) { }
        if (player != null) return player.transform;
        return cam != null ? cam.transform.root : null;
    }

    bool IsPlayerPart(Transform t)
    {
        if (!excludePlayer) return false;
        Transform root = PlayerRoot();
        return root != null && t.IsChildOf(root);
    }

    void ScanCharacters()
    {
        if (!EnsureShader()) return;
        SkinnedMeshRenderer[] all = FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (done.Contains(all[i]) || all[i].gameObject.name == "Outline") continue;
            if (IsPlayerPart(all[i].transform)) continue;
            AddHull(all[i], true);
        }
    }

    void ApplyVolume()
    {
        bool on = enableAll;
        dof.active = on && depthOfField;
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(dofStart);
        dof.gaussianEnd.Override(Mathf.Max(dofStart + 1f, dofEnd));
        dof.gaussianMaxRadius.Override(dofMaxRadius);
        dof.highQualitySampling.Override(dofHighQuality);
        grain.active = on && filmGrain;
        grain.type.Override(grainType);
        grain.intensity.Override(grainIntensity);
        grain.response.Override(grainResponse);
        ca.active = on && chromaticAberration;
        ca.intensity.Override(chromaticIntensity);
    }

    void ApplyOutlineMaterials()
    {
        if (buildingMat == null || characterMat == null) return;
        Color c = new Color(outlineColor.r, outlineColor.g, outlineColor.b, outlineOpacity);
        Vector4 fade = new Vector4(fadeStart, Mathf.Max(fadeStart + 1f, fadeEnd), 0f, 0f);
        buildingMat.SetColor("_Color", c);
        buildingMat.SetFloat("_Width", buildingWidth);
        buildingMat.SetVector("_Fade", fade);
        characterMat.SetColor("_Color", c);
        characterMat.SetFloat("_Width", characterWidth);
        characterMat.SetVector("_Fade", fade);
    }

    void Update()
    {
        if (profile == null) return;
        ApplyVolume();
        ApplyOutlineMaterials();
        if (cam == null || Time.unscaledTime > camTimer)
        {
            camTimer = Time.unscaledTime + 2f;
            cam = Camera.main;
            if (cam != null)
            {
                UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
                if (data != null && enableAll && !data.renderPostProcessing) data.renderPostProcessing = true;
            }
        }
        if (cam == null) return;
        if (outlines && enableAll && outlineCharacters && Time.unscaledTime > scanTimer)
        {
            scanTimer = Time.unscaledTime + 4f;
            ScanCharacters();
        }
        if (Time.unscaledTime < checkTimer) return;
        checkTimer = Time.unscaledTime + 0.3f;
        Vector3 cp = cam.transform.position;
        float max2 = maxActiveDistance * maxActiveDistance;
        bool master = enableAll && outlines;
        float near2 = nearHideDistance * nearHideDistance;
        Transform playerRoot = excludePlayer ? PlayerRoot() : null;
        for (int i = hulls.Count - 1; i >= 0; i--)
        {
            Hull h = hulls[i];
            if (h.src == null || h.hull == null) { hulls.RemoveAt(i); continue; }
            bool allowed = master && (h.character ? outlineCharacters : outlineBuildings);
            bool vis = false;
            if (allowed && h.src.enabled && h.src.gameObject.activeInHierarchy)
            {
                Vector3 d = h.src.bounds.center - cp;
                vis = d.sqrMagnitude < max2 && h.src.bounds.SqrDistance(cp) > near2;
                if (vis && playerRoot != null && h.src.transform.IsChildOf(playerRoot)) vis = false;
            }
            if (h.hull.enabled != vis) h.hull.enabled = vis;
        }
    }
}

[AddComponentMenu("Procedural World/Stages/Cinematic Outlines")]
public class CinematicOutlineStage : WorldStage
{
    public override int Order { get { return StageOrder.Props + 30; } }
    public override string StageName { get { return "Outlines"; } }

    static readonly string[] skip = { "glass", "window", "overlay", "weather", "shadow", "detail", "outline" };

    public override void Run(WorldContext c)
    {
        CinematicEffects fx = CinematicEffects.Ensure();
        Transform root = c.LayerRoot(PlacementLayer.Building);
        MeshRenderer[] rs = root.GetComponentsInChildren<MeshRenderer>(false);
        int added = 0;
        for (int i = 0; i < rs.Length; i++)
        {
            string n = rs[i].gameObject.name.ToLowerInvariant();
            bool bad = false;
            for (int k = 0; k < skip.Length && !bad; k++) if (n.Contains(skip[k])) bad = true;
            if (bad) continue;
            fx.AddBuilding(rs[i]);
            added++;
        }
        Debug.Log("Outlines: queued " + added + " building parts.");
    }
}
