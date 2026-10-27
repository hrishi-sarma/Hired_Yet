using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class WeatherPuddles : MonoBehaviour
{
    struct Spot
    {
        public Vector3 pos;
        public Vector3 dir;
        public float rx, rz, hintY;
        public int mesh;
        public bool resolved, valid, active;
    }

    class Item
    {
        public Transform t;
        public MeshFilter mf;
        public int spot = -1;
        public float lastScale = -1f;
        public MeshRenderer mr;
    }

    public int maxPuddles = 40;
    public int maxSpots = 500;
    public float activeRadius = 55f;
    public float riseSeconds = 70f;
    public float drySeconds = 120f;
    public bool reflections = true;
    public bool planarReflections = true;        // true mirror reflection (needs Resources/WeatherPuddlePlanar.shader); falls back to the cube probe
    public float dampDelaySeconds = 25f;         // after the rain stops the ground stays damp this long, then slowly dries back
    public float dampFadeSeconds = 45f;          // time for the ground textures to go fully damp / fully dry
    public bool boxProjection = false;           // per-probe box projection (parallax-corrected, but shifted reflections in testing); URP probe blending stays enabled either way
    public int probeResolution = 256;
    public float probeInterval = 0.45f;          // seconds between cube faces (one face per tick, so a full refresh takes ~6 ticks)
    public bool globalReflection = true;         // also feed the capture to RenderSettings' default reflection (weights down to the original when dry)

    public float Wetness { get { return wet; } }
    public bool Busy { get { return wet > 0.001f || dampK > 0.001f; } }

    bool planarMode;
    Camera planarCam;
    RenderTexture planarRT;
    float dampK, dampHold, lastDamp = -1f;
    Terrain dampTerrain;
    TerrainLayer[] dampOrig, dampClone;
    readonly List<Material> groundMats = new List<Material>();
    readonly List<Color> groundCols = new List<Color>();
    readonly List<float> groundSmooth = new List<float>();

    readonly List<Spot> spots = new List<Spot>();
    readonly List<Transform> spotOwners = new List<Transform>();
    readonly List<Item> items = new List<Item>();
    readonly List<Material> roadMats = new List<Material>();
    readonly List<Color> roadColors = new List<Color>();
    readonly List<float> roadSmooth = new List<float>();
    readonly Mesh[] meshes = new Mesh[3];

    Material puddleMat, rippleMat;
    Texture2D ringTex;
    ParticleSystem ripples;
    ReflectionProbe probe;
    // URP asset flags we switched on at runtime (restored on destroy). null = untouched.
    UnityEngine.Object urpAsset; bool? prevBoxProj, prevBlend;
    RenderTexture probeRT;
    Camera capCam;
    int nextFace; bool filled;
    bool globalOn; UnityEngine.Rendering.DefaultReflectionMode prevMode; Texture prevTex; float prevIntensity;
    readonly List<float> roadEnv = new List<float>(), roadSpec = new List<float>();
    readonly List<bool> roadEnvKw = new List<bool>(), roadSpecKw = new List<bool>();
    NpcDirector director;
    bool built, matsApplied;
    float wet, nextBuild, nextRefresh, nextProbe, lastWetK = -1f, rippleAcc, nextTint;
    RaycastHit hit;

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    void OnDisable() { RestoreRoads(); ReleaseGlobalReflection(); RestoreDamp(); }

    void OnDestroy()
    {
        if (puddleMat != null) Destroy(puddleMat);
        ReleaseGlobalReflection();
        RestoreBoxProjection();
        if (capCam != null) Destroy(capCam.gameObject);
        if (probeRT != null) { probeRT.Release(); Destroy(probeRT); }
        if (planarCam != null) Destroy(planarCam.gameObject);
        if (planarRT != null) { planarRT.Release(); Destroy(planarRT); }
        RestoreDamp();
        if (rippleMat != null) Destroy(rippleMat);
        if (ringTex != null) Destroy(ringTex);
        for (int i = 0; i < meshes.Length; i++) if (meshes[i] != null) Destroy(meshes[i]);
    }

    public void Tick(float rain, float dt, Camera cam)
    {
        if (rain > 0.05f) wet = Mathf.Min(1f, wet + dt / riseSeconds * rain);
        else wet = Mathf.Max(0f, wet - dt / drySeconds);
        if (cam == null) return;

        if (!built && wet > 0.001f && Time.unscaledTime >= nextBuild)
        {
            nextBuild = Time.unscaledTime + 5f;
            BuildSpots();
        }

        ApplyWetRoads(Mathf.Clamp01(wet * 2.2f));
        UpdateDamp(rain, dt);
        UpdateProbe(cam);
        UpdatePlanar(cam);

        float scale = Smooth(0.12f, 0.9f, wet);
        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.6f;
            RefreshActive(cam.transform.position, scale);
        }
        ApplyScale(scale);

        if (rain > 0.05f) EmitRipples(rain, dt, cam);
    }

    void BuildSpots()
    {
        if (director == null) director = FindFirstObjectByType<NpcDirector>();
        if (director == null || director.Ctx == null || director.Ctx.occupancy == null) return;
        built = true;
        BuildMeshes();
        List<SpawnRecord> recs = director.Ctx.occupancy.Records;
        for (int i = 0; i < recs.Count && spots.Count < maxSpots; i++)
        {
            SpawnRecord r = recs[i];
            if (r.layer != PlacementLayer.Road || r.owner == null || r.owner.name == "Highway") continue;
            OrientedBox b = r.box;
            bool longX = b.half.x >= b.half.y;
            float longH = longX ? b.half.x : b.half.y;
            float shortH = longX ? b.half.y : b.half.x;
            if (shortH < 2f || longH < 4f) continue;
            CollectRoadMaterial(r.owner);
            System.Random rng = new System.Random(unchecked((int)(b.center.x * 131.7f) * 73856093 ^ (int)(b.center.y * 91.3f) * 19349663));
            int count = Mathf.Min(3, Mathf.FloorToInt(longH * 2f / 22f + (float)rng.NextDouble()));
            Vector2 axL = longX ? b.axisX : b.axisZ;
            Vector2 axS = longX ? b.axisZ : b.axisX;
            for (int k = 0; k < count && spots.Count < maxSpots; k++)
            {
                float along = ((float)rng.NextDouble() * 2f - 1f) * longH * 0.9f;
                float side;
                if (rng.NextDouble() < 0.6) side = (rng.Next(2) == 0 ? -1f : 1f) * Mathf.Lerp(0.55f, 0.9f, (float)rng.NextDouble()) * shortH;
                else side = ((float)rng.NextDouble() * 2f - 1f) * shortH * 0.8f;
                Vector2 p = b.center + axL * along + axS * side;
                float r0 = Mathf.Lerp(0.6f, 1.35f, (float)rng.NextDouble());
                Spot s = new Spot();
                s.pos = new Vector3(p.x, b.maxY, p.y);
                s.dir = new Vector3(axL.x, 0f, axL.y).normalized;
                s.rx = r0 * Mathf.Lerp(0.8f, 1.2f, (float)rng.NextDouble());
                s.rz = r0 * Mathf.Lerp(1.1f, 1.7f, (float)rng.NextDouble());
                s.hintY = b.maxY;
                s.mesh = rng.Next(meshes.Length);
                spots.Add(s);
                spotOwners.Add(r.owner.transform);
            }
        }
    }

    void CollectRoadMaterial(GameObject owner)
    {
        if (roadMats.Count >= 8) return;
        Renderer[] rs = owner.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < rs.Length && roadMats.Count < 8; i++)
        {
            Material m = rs[i].sharedMaterial;
            if (m == null || roadMats.Contains(m) || !m.HasProperty("_Smoothness") || !m.HasProperty("_BaseColor")) continue;
            if (!m.name.ToLowerInvariant().Contains("asphalt")) continue;
            roadMats.Add(m);
            roadColors.Add(m.GetColor("_BaseColor"));
            roadSmooth.Add(m.GetFloat("_Smoothness"));
            roadEnv.Add(m.HasProperty("_EnvironmentReflections") ? m.GetFloat("_EnvironmentReflections") : 1f);
            roadSpec.Add(m.HasProperty("_SpecularHighlights") ? m.GetFloat("_SpecularHighlights") : 1f);
            roadEnvKw.Add(m.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"));
            roadSpecKw.Add(m.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"));
        }
    }

    void ApplyWetRoads(float k)
    {
        if (roadMats.Count == 0) return;
        if (k <= 0.001f) { RestoreRoads(); return; }
        if (Mathf.Abs(k - lastWetK) < 0.01f) return;
        lastWetK = k;
        matsApplied = true;
        for (int i = 0; i < roadMats.Count; i++)
        {
            if (roadMats[i] == null) continue;
            Color c = roadColors[i];
            float d = Mathf.Lerp(1f, 0.55f, k);
            roadMats[i].SetColor("_BaseColor", new Color(c.r * d, c.g * d, c.b * d, c.a));
            roadMats[i].SetFloat("_Smoothness", Mathf.Lerp(roadSmooth[i], 0.55f, k));
            // asphalt ships with environment reflections + specular highlights off (matte look); a wet road needs both
            if (roadMats[i].HasProperty("_EnvironmentReflections")) roadMats[i].SetFloat("_EnvironmentReflections", 1f);
            if (roadMats[i].HasProperty("_SpecularHighlights")) roadMats[i].SetFloat("_SpecularHighlights", 1f);
            roadMats[i].DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            roadMats[i].DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }
    }

    void RestoreRoads()
    {
        if (!matsApplied) return;
        matsApplied = false;
        lastWetK = -1f;
        for (int i = 0; i < roadMats.Count; i++)
        {
            if (roadMats[i] == null) continue;
            roadMats[i].SetColor("_BaseColor", roadColors[i]);
            roadMats[i].SetFloat("_Smoothness", roadSmooth[i]);
            if (roadMats[i].HasProperty("_EnvironmentReflections")) roadMats[i].SetFloat("_EnvironmentReflections", roadEnv[i]);
            if (roadMats[i].HasProperty("_SpecularHighlights")) roadMats[i].SetFloat("_SpecularHighlights", roadSpec[i]);
            if (roadEnvKw[i]) roadMats[i].EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (roadSpecKw[i]) roadMats[i].EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }
    }

    // The probe is created at runtime, so the URP asset's "Reflection Probes > Box Projection / Probe Blending" cannot be ticked by hand
    // for it. Both setters are internal in URP, so the private fields are written (same approach as WorldShadowFix). Failure is harmless:
    // the puddles then just reflect without box projection. Previous values are restored in OnDestroy.
    // ---- planar (true mirror) reflection: one extra low-res render from the camera mirrored in the puddle plane ----
    static Matrix4x4 ReflectMatrix(float py)
    {
        // plane n = (0,1,0), d = -py
        Matrix4x4 m = Matrix4x4.identity;
        m.m11 = -1f;
        m.m13 = 2f * py;
        return m;
    }

    void UpdatePlanar(Camera cam)
    {
        if (!planarMode || cam == null || wet < 0.08f) return;
        Vector3 cp = cam.transform.position;
        float best = 45f * 45f, py = 0f;
        bool any = false;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].spot < 0 || !items[i].t.gameObject.activeSelf) continue;
            float d = (items[i].t.position - cp).sqrMagnitude;
            if (d < best) { best = d; py = items[i].t.position.y; any = true; }
        }
        if (!any) return;
        py -= 0.02f;

        int w = Mathf.Clamp(Screen.width / 2, 256, 1024), h = Mathf.Clamp(Screen.height / 2, 144, 576);
        if (planarRT == null || planarRT.width != w || planarRT.height != h)
        {
            if (planarRT != null) { planarRT.Release(); Destroy(planarRT); }
            RenderTextureFormat fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR) ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32;
            planarRT = new RenderTexture(w, h, 16, fmt);
            planarRT.useMipMap = true;
            planarRT.autoGenerateMips = true;
            planarRT.filterMode = FilterMode.Trilinear;
            planarRT.wrapMode = TextureWrapMode.Clamp;
            planarRT.Create();
        }
        if (planarCam == null)
        {
            GameObject go = new GameObject("WeatherPlanarCamera");
            go.transform.SetParent(transform, false);
            go.hideFlags = HideFlags.HideAndDontSave;
            planarCam = go.AddComponent<Camera>();
            planarCam.enabled = false;
        }
        planarCam.CopyFrom(cam);
        planarCam.enabled = false;
        planarCam.targetTexture = planarRT;
        planarCam.cullingMask = cam.cullingMask;
        planarCam.farClipPlane = Mathf.Min(cam.farClipPlane, 220f);
        planarCam.allowMSAA = false;
        var data = planarCam.GetUniversalAdditionalCameraData();
        if (data != null) { data.renderShadows = false; data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; data.requiresDepthTexture = false; data.requiresColorTexture = false; }

        Matrix4x4 r = ReflectMatrix(py);
        planarCam.transform.position = r.MultiplyPoint(cp);
        Vector3 e = cam.transform.eulerAngles;
        planarCam.transform.eulerAngles = new Vector3(-e.x, e.y, e.z);
        planarCam.worldToCameraMatrix = cam.worldToCameraMatrix * r;
        Matrix4x4 wv = planarCam.worldToCameraMatrix;
        Vector3 cpos = wv.MultiplyPoint(new Vector3(0f, py, 0f));
        Vector3 cn = wv.MultiplyVector(Vector3.up).normalized;
        planarCam.projectionMatrix = cam.CalculateObliqueMatrix(new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cpos, cn)));

        for (int i = 0; i < items.Count; i++) if (items[i].mr != null && items[i].t.gameObject.activeSelf) items[i].mr.enabled = false;
        GL.invertCulling = true;
        try { planarCam.Render(); }
        catch (System.Exception) { }
        GL.invertCulling = false;
        for (int i = 0; i < items.Count; i++) if (items[i].mr != null) items[i].mr.enabled = true;

        puddleMat.SetTexture("_ReflTex", planarRT);
        puddleMat.SetFloat("_Wet", Mathf.Clamp01(wet * 2f));
    }

    // ---- damp ground: terrain layers + ground materials darken gradually while it rains, and dry back after a delay ----
    void UpdateDamp(float rain, float dt)
    {
        float k = Smooth(0.1f, 0.95f, wet);
        if (rain > 0.05f) dampHold = dampDelaySeconds; else dampHold -= dt;
        float target = (rain > 0.05f) ? k : (dampHold > 0f ? dampK : k);
        dampK = Mathf.MoveTowards(dampK, target, dt / Mathf.Max(1f, dampFadeSeconds));
        if (dampK <= 0.001f) { if (lastDamp >= 0f) RestoreDamp(); return; }
        if (lastDamp >= 0f && Mathf.Abs(dampK - lastDamp) < 0.04f) return;     // quantised: terrain layer updates are not free
        lastDamp = dampK;

        if (dampTerrain == null) dampTerrain = Terrain.activeTerrain;
        if (dampTerrain != null && dampTerrain.terrainData != null && dampClone == null)
        {
            dampOrig = dampTerrain.terrainData.terrainLayers;
            dampClone = new TerrainLayer[dampOrig.Length];
            for (int i = 0; i < dampOrig.Length; i++) dampClone[i] = dampOrig[i] != null ? Instantiate(dampOrig[i]) : null;
        }
        if (dampClone != null)
        {
            float dk = Mathf.Lerp(1f, 0.6f, dampK);
            for (int i = 0; i < dampClone.Length; i++)
            {
                if (dampClone[i] == null || dampOrig[i] == null) continue;
                Vector4 m = dampOrig[i].diffuseRemapMax;
                dampClone[i].diffuseRemapMax = new Vector4(m.x * dk, m.y * dk, m.z * dk * 1.03f, m.w);
                dampClone[i].smoothness = Mathf.Lerp(dampOrig[i].smoothness, 0.32f, dampK);
            }
            dampTerrain.terrainData.terrainLayers = dampClone;
        }
        if (groundMats.Count == 0)
        {
            List<Material> found = new List<Material>();
            StylizedMaterialFactory.CollectByRole("ground", found);
            for (int i = 0; i < found.Count; i++)
            {
                Material m = found[i];
                if (m == null || !m.HasProperty("_BaseColor")) continue;
                groundMats.Add(m);
                groundCols.Add(m.GetColor("_BaseColor"));
                groundSmooth.Add(m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0f);
            }
        }
        float d2 = Mathf.Lerp(1f, 0.62f, dampK);
        for (int i = 0; i < groundMats.Count; i++)
        {
            if (groundMats[i] == null) continue;
            Color c = groundCols[i];
            groundMats[i].SetColor("_BaseColor", new Color(c.r * d2, c.g * d2, c.b * d2 * 1.03f, c.a));
            if (groundMats[i].HasProperty("_Smoothness")) groundMats[i].SetFloat("_Smoothness", Mathf.Lerp(groundSmooth[i], 0.3f, dampK));
        }
    }

    void RestoreDamp()
    {
        if (dampTerrain != null && dampTerrain.terrainData != null && dampOrig != null) dampTerrain.terrainData.terrainLayers = dampOrig;
        if (dampClone != null) { for (int i = 0; i < dampClone.Length; i++) if (dampClone[i] != null) Destroy(dampClone[i]); dampClone = null; }
        dampOrig = null;
        for (int i = 0; i < groundMats.Count; i++)
        {
            if (groundMats[i] == null) continue;
            groundMats[i].SetColor("_BaseColor", groundCols[i]);
            if (groundMats[i].HasProperty("_Smoothness")) groundMats[i].SetFloat("_Smoothness", groundSmooth[i]);
        }
        lastDamp = -1f;
    }

    void EnableBoxProjection()
    {
        var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (urp == null) return;
        try
        {
            System.Type t = urp.GetType();
            const BindingFlags bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            FieldInfo box = t.GetField("m_ReflectionProbeBoxProjection", bf);
            FieldInfo blend = t.GetField("m_ReflectionProbeBlending", bf);
            if (box != null && !(bool)box.GetValue(urp)) { prevBoxProj = false; box.SetValue(urp, true); urpAsset = urp; }
            if (blend != null && !(bool)blend.GetValue(urp)) { prevBlend = false; blend.SetValue(urp, true); urpAsset = urp; }
            if (box == null) Debug.LogWarning("WeatherPuddles: URP field m_ReflectionProbeBoxProjection not found; enable Box Projection in the URP asset manually.");
        }
        catch (System.Exception e) { Debug.LogWarning("WeatherPuddles: could not enable box projection (" + e.Message + ")."); }
    }

    void RestoreBoxProjection()
    {
        if (urpAsset == null) return;
        try
        {
            System.Type t = urpAsset.GetType();
            const BindingFlags bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            if (prevBoxProj.HasValue) { FieldInfo f = t.GetField("m_ReflectionProbeBoxProjection", bf); if (f != null) f.SetValue(urpAsset, prevBoxProj.Value); }
            if (prevBlend.HasValue) { FieldInfo f = t.GetField("m_ReflectionProbeBlending", bf); if (f != null) f.SetValue(urpAsset, prevBlend.Value); }
        }
        catch (System.Exception) { }
        urpAsset = null; prevBoxProj = null; prevBlend = null;
    }

    void UpdateProbe(Camera cam)
    {
        if (!reflections) return;
        if (wet < 0.08f)
        {
            if (probe != null && probe.gameObject.activeSelf) probe.gameObject.SetActive(false);
            ReleaseGlobalReflection();
            return;
        }
        if (probe == null)
        {
            RenderTextureFormat fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR) ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32;
            probeRT = new RenderTexture(probeResolution, probeResolution, 16, fmt);
            probeRT.dimension = UnityEngine.Rendering.TextureDimension.Cube;
            probeRT.useMipMap = true;
            probeRT.autoGenerateMips = false;        // mips are generated by hand after each face (GenerateMips below)
            probeRT.Create();
            GameObject go = new GameObject("WeatherReflectionProbe");
            go.transform.SetParent(transform, false);
            probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
            probe.customBakedTexture = probeRT;
            probe.boxProjection = boxProjection;   // box projection warped the capture (house reflected at the far side of the puddle); plain direction lookup is correct for distant scenery
            probe.size = new Vector3(160f, 80f, 160f);
            probe.hdr = true;
            probe.nearClipPlane = 0.3f;
            probe.farClipPlane = 250f;
            probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
            probe.importance = 10;
            EnableBoxProjection();                    // also turns on URP "Probe Blending", which the puddles need to sample this probe at all
            probe.intensity = 1.15f;
            nextProbe = 0f; filled = false; nextFace = 0;
        }
        if (!probe.gameObject.activeSelf) { probe.gameObject.SetActive(true); nextProbe = 0f; }
        probe.transform.position = cam.transform.position + Vector3.up * 0.5f;
        if (Time.unscaledTime >= nextProbe)
        {
            nextProbe = Time.unscaledTime + probeInterval;
            CaptureFace(cam);
        }
        ApplyGlobalReflection();
    }

    // The cube is rendered by our own camera (Camera.RenderToCubemap) instead of ReflectionProbe.RenderProbe, which in URP could leave the
    // texture empty so that puddles only showed light highlights. The first capture renders all six faces, afterwards one face per tick.
    // Puddles are hidden while capturing so they don't reflect themselves. The same texture feeds the probe (box projection) and,
    // as a fallback for anything the probe does not cover, the scene's default reflection.
    void CaptureFace(Camera cam)
    {
        if (probeRT == null) return;
        if (capCam == null)
        {
            GameObject go = new GameObject("WeatherReflectionCamera");
            go.transform.SetParent(transform, false);
            go.hideFlags = HideFlags.HideAndDontSave;
            capCam = go.AddComponent<Camera>();
            capCam.enabled = false;
            capCam.clearFlags = CameraClearFlags.Skybox;
            capCam.nearClipPlane = 0.3f;
            capCam.farClipPlane = 400f;
            capCam.allowHDR = true;
            capCam.allowMSAA = false;
            var data = capCam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.renderShadows = true; }
        }
        capCam.transform.position = cam.transform.position + Vector3.up * 0.5f;
        capCam.cullingMask = cam.cullingMask;

        for (int i = 0; i < items.Count; i++) if (items[i].mr != null && items[i].t.gameObject.activeSelf) items[i].mr.enabled = false;
        int mask = filled ? (1 << nextFace) : 63;
        bool ok = false;
        try { ok = capCam.RenderToCubemap(probeRT, mask); }
        catch (System.Exception) { ok = false; }
        for (int i = 0; i < items.Count; i++) if (items[i].mr != null) items[i].mr.enabled = true;

        if (ok) { probeRT.GenerateMips(); filled = true; nextFace = (nextFace + 1) % 6; }
        else probe.RenderProbe(probeRT);             // fallback to the built-in path
    }

    void ApplyGlobalReflection()
    {
        if (!globalReflection || probeRT == null || !filled) return;
        if (!globalOn)
        {
            prevMode = RenderSettings.defaultReflectionMode;
#if UNITY_2022_1_OR_NEWER
            prevTex = RenderSettings.customReflectionTexture;
            RenderSettings.customReflectionTexture = probeRT;
#else
            prevTex = RenderSettings.customReflection;
            RenderSettings.customReflection = null;
#endif
            prevIntensity = RenderSettings.reflectionIntensity;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            globalOn = true;
        }
        float target = Mathf.Lerp(prevIntensity, Mathf.Max(prevIntensity, 1f), Smooth(0.08f, 0.6f, wet));
        if (Mathf.Abs(RenderSettings.reflectionIntensity - target) > 0.01f) RenderSettings.reflectionIntensity = target;
    }

    void ReleaseGlobalReflection()
    {
        if (!globalOn) return;
        globalOn = false;
        RenderSettings.defaultReflectionMode = prevMode;
#if UNITY_2022_1_OR_NEWER
        RenderSettings.customReflectionTexture = prevTex;
#endif
        RenderSettings.reflectionIntensity = prevIntensity;
    }

    void RefreshActive(Vector3 camPos, float scale)
    {
        if (!built || spots.Count == 0) return;
        EnsureItems();
        float keep = (activeRadius + 12f) * (activeRadius + 12f);
        float take = activeRadius * activeRadius;
        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];
            if (it.spot < 0) continue;
            Spot s = spots[it.spot];
            Vector3 d = s.pos - camPos; d.y = 0f;
            if (scale <= 0.02f || d.sqrMagnitude > keep || !s.valid)
            {
                s.active = false; spots[it.spot] = s;
                it.spot = -1;
                it.t.gameObject.SetActive(false);
            }
        }
        if (scale <= 0.02f) return;

        int resolveBudget = 8;
        for (int n = 0; n < spots.Count; n++)
        {
            Spot s = spots[n];
            if (s.active || (s.resolved && !s.valid)) continue;
            Vector3 d = s.pos - camPos; d.y = 0f;
            if (d.sqrMagnitude > take) continue;
            Item free = FreeItem();
            if (free == null) break;
            if (!s.resolved)
            {
                if (resolveBudget-- <= 0) continue;
                Vector3 o = new Vector3(s.pos.x, s.hintY + 2f, s.pos.z);
                Transform owner = spotOwners[n];
                if (Physics.Raycast(o, Vector3.down, out hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (owner != null && hit.collider.transform.IsChildOf(owner)) { s.pos.y = hit.point.y; s.resolved = true; s.valid = true; }
                    else { spots[n] = s; continue; }
                }
                else { s.pos.y = s.hintY; s.resolved = true; s.valid = true; }
            }
            s.active = true;
            spots[n] = s;
            free.spot = n;
            free.lastScale = -1f;
            free.mf.sharedMesh = meshes[s.mesh];
            free.t.position = s.pos + Vector3.up * 0.015f;
            free.t.rotation = Quaternion.LookRotation(s.dir, Vector3.up);
            free.t.localScale = Vector3.zero;
            free.t.gameObject.SetActive(true);
        }
    }

    void ApplyScale(float scale)
    {
        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];
            if (it.spot < 0 || Mathf.Abs(scale - it.lastScale) < 0.005f) continue;
            it.lastScale = scale;
            Spot s = spots[it.spot];
            it.t.localScale = new Vector3(s.rx * scale, 1f, s.rz * scale);
        }
    }

    Item FreeItem()
    {
        for (int i = 0; i < items.Count; i++) if (items[i].spot < 0) return items[i];
        return null;
    }

    void EnsureItems()
    {
        if (items.Count > 0) return;
        Shader planarSh = planarReflections ? Resources.Load<Shader>("WeatherPuddlePlanar") : null;
        if (puddleMat == null && planarSh != null && planarSh.isSupported)
        {
            puddleMat = new Material(planarSh);
            puddleMat.name = "WeatherPuddle (planar)";
            planarMode = true;
        }
        if (puddleMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (sh == null) sh = Shader.Find("Standard");
            puddleMat = new Material(sh);
            puddleMat.name = "WeatherPuddle";
            Color c = new Color(0.35f, 0.4f, 0.45f, 1f);
            if (puddleMat.HasProperty("_BaseColor")) puddleMat.SetColor("_BaseColor", c);
            if (puddleMat.HasProperty("_Color")) puddleMat.SetColor("_Color", c);
            if (puddleMat.HasProperty("_Metallic")) puddleMat.SetFloat("_Metallic", 0.35f);
            if (puddleMat.HasProperty("_Smoothness")) puddleMat.SetFloat("_Smoothness", 0.8f);
            if (puddleMat.HasProperty("_Glossiness")) puddleMat.SetFloat("_Glossiness", 0.8f);
            if (puddleMat.HasProperty("_EnvironmentReflections")) puddleMat.SetFloat("_EnvironmentReflections", 1f);
            if (puddleMat.HasProperty("_SpecularHighlights")) puddleMat.SetFloat("_SpecularHighlights", 1f);
            puddleMat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            puddleMat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            puddleMat.enableInstancing = true;
        }
        for (int i = 0; i < maxPuddles; i++)
        {
            GameObject go = new GameObject("Puddle");
            go.transform.SetParent(transform, false);
            Item it = new Item();
            it.t = go.transform;
            it.mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            it.mr = mr;
            mr.sharedMaterial = puddleMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbesAndSkybox;
            go.SetActive(false);
            items.Add(it);
        }
    }

    void BuildMeshes()
    {
        const int seg = 20;
        for (int m = 0; m < meshes.Length; m++)
        {
            System.Random rng = new System.Random(1000 + m * 77);
            float p1 = (float)rng.NextDouble() * 6.28f, p2 = (float)rng.NextDouble() * 6.28f, p3 = (float)rng.NextDouble() * 6.28f;
            Vector3[] v = new Vector3[seg + 1];
            Vector3[] n = new Vector3[seg + 1];
            Vector2[] uv = new Vector2[seg + 1];
            int[] tri = new int[seg * 3];
            v[0] = Vector3.zero; n[0] = Vector3.up; uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float r = 1f + 0.16f * Mathf.Sin(2f * a + p1) + 0.11f * Mathf.Sin(3f * a + p2) + 0.07f * Mathf.Sin(5f * a + p3);
                v[i + 1] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                n[i + 1] = Vector3.up;
                uv[i + 1] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f);
                tri[i * 3] = 0;
                tri[i * 3 + 1] = (i + 1) % seg + 1;
                tri[i * 3 + 2] = i + 1;
            }
            Mesh mesh = new Mesh();
            mesh.name = "PuddleBlob" + m;
            mesh.vertices = v; mesh.normals = n; mesh.uv = uv; mesh.triangles = tri;
            mesh.RecalculateBounds();
            meshes[m] = mesh;
        }
    }

    void EnsureRipples()
    {
        if (ripples != null) return;
        const int size = 64;
        ringTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        ringTex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Exp(-Mathf.Pow((d - 0.78f) / 0.09f, 2f)) * Mathf.Clamp01((1f - d) * 8f);
                ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        ringTex.Apply(false, true);

        GameObject go = new GameObject("WeatherRipples");
        go.transform.SetParent(transform, false);
        ripples = go.AddComponent<ParticleSystem>();
        ripples.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ripples.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.8f;
        main.startSpeed = 0f;
        main.maxParticles = 120;
        ParticleSystem.EmissionModule em = ripples.emission;
        em.enabled = false;
        ParticleSystem.ShapeModule sp = ripples.shape;
        sp.enabled = false;

        ParticleSystem.SizeOverLifetimeModule so = ripples.sizeOverLifetime;
        so.enabled = true;
        so.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(1f, 1f)));

        ParticleSystem.ColorOverLifetimeModule co = ripples.colorOverLifetime;
        co.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
        co.color = new ParticleSystem.MinMaxGradient(g);

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        rippleMat = new Material(sh);
        rippleMat.mainTexture = ringTex;
        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        pr.sharedMaterial = rippleMat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        ripples.Play();
    }

    void EmitRipples(float rain, float dt, Camera cam)
    {
        EnsureRipples();
        if (Time.unscaledTime >= nextTint)
        {
            nextTint = Time.unscaledTime + 0.25f;
            float k = Mathf.Lerp(1f, 0.5f, StylizedVehiclePainter.CurrentNight);
            rippleMat.color = new Color(0.85f * k, 0.9f * k, k, 0.5f);
        }
        rippleAcc += dt * (22f * rain);
        int guard = 0;
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        while (rippleAcc >= 1f && guard++ < 6)
        {
            rippleAcc -= 1f;
            Vector3 pos;
            float size;
            if (Random.value < 0.65f && TryPuddlePoint(out pos, out size)) { }
            else if (!TryGroundPoint(cam, out pos)) continue;
            else size = Random.Range(0.35f, 0.6f);
            ep.position = pos;
            ep.startSize = size;
            ep.startLifetime = 0.8f;
            ep.velocity = Vector3.zero;
            ep.startColor = Color.white;
            ripples.Emit(ep, 1);
        }
        if (rippleAcc > 3f) rippleAcc = 0f;
    }

    bool TryPuddlePoint(out Vector3 pos, out float size)
    {
        pos = Vector3.zero; size = 0f;
        if (items.Count == 0) return false;
        for (int tries = 0; tries < 4; tries++)
        {
            Item it = items[Random.Range(0, items.Count)];
            if (it.spot < 0 || it.lastScale < 0.3f) continue;
            Spot s = spots[it.spot];
            Vector2 o = Random.insideUnitCircle * 0.7f;
            Vector3 right = Vector3.Cross(Vector3.up, s.dir);
            pos = s.pos + Vector3.up * 0.03f + right * (o.x * s.rx * it.lastScale) + s.dir * (o.y * s.rz * it.lastScale);
            size = Random.Range(0.3f, 0.55f);
            return true;
        }
        return false;
    }

    bool TryGroundPoint(Camera cam, out Vector3 pos)
    {
        pos = Vector3.zero;
        Vector2 o = Random.insideUnitCircle * 14f;
        if (o.sqrMagnitude < 9f) o = o.normalized * 3f;
        Vector3 c = cam.transform.position;
        Vector3 from = new Vector3(c.x + o.x, c.y + 2f, c.z + o.y);
        if (!Physics.Raycast(from, Vector3.down, out hit, 10f, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (hit.normal.y < 0.85f) return false;
        pos = hit.point + Vector3.up * 0.03f;
        return true;
    }
}
