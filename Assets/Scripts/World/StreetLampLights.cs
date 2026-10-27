using System.Collections.Generic;
using UnityEngine;

public sealed class StreetLampLights : MonoBehaviour
{
    public int poolSize = 10;
    public float fullRadius = 30f;
    public float fadeRadius = 55f;
    public float lightRange = 12f;
    public float lightIntensity = 3f;
    public Color lightColor = new Color(1f, 0.82f, 0.5f);
    public float fadeSpeed = 3f;
    public float tickInterval = 0.2f;
    public float nightThreshold = 0.02f;

    [Header("Soft glow halos (night only; cheap billboard quads, no lights)")]
    public bool haloEnabled = true;
    public int haloCount = 24;
    public float haloRadius = 110f;
    public float haloSize = 3.4f;
    public float haloAlpha = 0.5f;

    const float CellSize = 32f;

    static readonly List<Vector3> lampPos = new List<Vector3>();
    static readonly List<float> lampRange = new List<float>();
    static readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
    static readonly HashSet<long> known = new HashSet<long>();
    static StreetLampLights instance;

    sealed class Slot
    {
        public Light light;
        public int lamp = -1;
        public float factor;
        public float target;
    }

    sealed class Halo
    {
        public Transform t;
        public MeshRenderer mr;
        public int lamp = -1;
        public float factor, target, dist;
    }

    Halo[] halos;
    int[] hIdx; float[] hDist; bool[] hUsed;
    Mesh haloMesh; Material haloMat; Texture2D haloTex;
    MaterialPropertyBlock haloMpb;
    Camera haloCam;
    float nextHaloTick;
    bool anyHalo;
    static readonly int IdColor = Shader.PropertyToID("_Color");

    Slot[] slots;
    int[] candIdx;
    float[] candDist;
    bool[] candUsed;
    float nextTick;
    float nextFind;
    bool anyLit;
    Transform tagged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        StreetLampLights existing = FindFirstObjectByType<StreetLampLights>();
        if (existing != null) { instance = existing; return; }
        GameObject go = new GameObject("StreetLampLights");
        go.AddComponent<StreetLampLights>();
        DontDestroyOnLoad(go);
    }

    public static void Register(Vector3 position, float range = 0f)
    {
        long key = Key((int)Mathf.Round(position.x * 10f), (int)Mathf.Round(position.z * 10f));
        key ^= (long)(int)Mathf.Round(position.y * 10f) * 7919L;
        if (!known.Add(key)) return;
        int index = lampPos.Count;
        lampPos.Add(position);
        lampRange.Add(range);
        long cell = Key(Mathf.FloorToInt(position.x / CellSize), Mathf.FloorToInt(position.z / CellSize));
        List<int> list;
        if (!grid.TryGetValue(cell, out list))
        {
            list = new List<int>();
            grid[cell] = list;
        }
        list.Add(index);
    }

    public static void Export(List<Vector3> positions, List<float> ranges)
    {
        positions.AddRange(lampPos);
        ranges.AddRange(lampRange);
    }

    public static void Clear()
    {
        lampPos.Clear();
        lampRange.Clear();
        grid.Clear();
        known.Clear();
        if (instance != null) instance.ReleaseAll();
    }

    static long Key(int a, int b)
    {
        return ((long)a << 32) ^ (uint)b;
    }

    void Awake()
    {
        if (instance != null && instance != this) { WorldUtil.Kill(gameObject); return; }
        instance = this;
        BuildPool();
        BuildHalos();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (haloMesh != null) Destroy(haloMesh);
        if (haloMat != null) Destroy(haloMat);
        if (haloTex != null) Destroy(haloTex);
    }

    void BuildPool()
    {
        int n = Mathf.Max(1, poolSize);
        slots = new Slot[n];
        candIdx = new int[n];
        candDist = new float[n];
        candUsed = new bool[n];
        for (int i = 0; i < n; i++)
        {
            GameObject go = new GameObject("StreetLampLight");
            go.transform.SetParent(transform, false);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.shadows = LightShadows.None;
            l.color = lightColor;
            l.range = lightRange;
            l.intensity = 0f;
            l.enabled = false;
            slots[i] = new Slot { light = l };
        }
    }

    void ReleaseAll()
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].lamp = -1;
            slots[i].factor = 0f;
            slots[i].target = 0f;
            if (slots[i].light != null) slots[i].light.enabled = false;
        }
        anyLit = false;
        if (halos != null)
        {
            for (int i = 0; i < halos.Length; i++)
            {
                halos[i].lamp = -1; halos[i].factor = 0f; halos[i].target = 0f;
                if (halos[i].mr != null) halos[i].mr.enabled = false;
            }
            anyHalo = false;
        }
    }

    Transform FindTarget()
    {
        if (WorldLauncher.Player != null) return WorldLauncher.Player;
        if (tagged != null) return tagged;
        if (Time.unscaledTime >= nextFind)
        {
            nextFind = Time.unscaledTime + 2f;
            try
            {
                GameObject g = GameObject.FindGameObjectWithTag("Player");
                if (g != null) tagged = g.transform;
            }
            catch (System.Exception) { }
        }
        if (tagged != null) return tagged;
        Camera cam = Camera.main;
        return cam != null ? cam.transform : null;
    }

    void Update()
    {
        if (slots == null) return;
        float night = StylizedMaterialFactory.CurrentNight;
        bool active = night > nightThreshold && lampPos.Count > 0;
        UpdateHalos(night, active);
        if (!active && !anyLit) return;

        if (Time.unscaledTime >= nextTick)
        {
            nextTick = Time.unscaledTime + tickInterval;
            Transform t = active ? FindTarget() : null;
            Retarget(t != null ? t.position : Vector3.zero, t != null);
        }

        float step = fadeSpeed * Time.unscaledDeltaTime;
        bool lit = false;
        for (int i = 0; i < slots.Length; i++)
        {
            Slot s = slots[i];
            if (s.lamp < 0) continue;
            s.factor = Mathf.MoveTowards(s.factor, s.target, step);
            float intensity = lightIntensity * s.factor * night;
            if (s.factor <= 0f && s.target <= 0f)
            {
                s.lamp = -1;
                s.light.enabled = false;
                continue;
            }
            s.light.intensity = intensity;
            s.light.enabled = intensity > 0.01f;
            lit = true;
        }
        anyLit = lit;
    }

    // ------------------------------------------------------------------ halos
    // One shared material + mesh, <= haloCount camera-facing quads on the nearest lamps (reaches further than the 10 real lights so far
    // lamps read as warm glows instead of flat discs). Same stable-assignment idea as the light pool; nothing is allocated per frame.
    void BuildHalos()
    {
        if (!haloEnabled) return;
        int n = Mathf.Max(1, haloCount);
        halos = new Halo[n];
        hIdx = new int[n]; hDist = new float[n]; hUsed = new bool[n];
        haloMpb = new MaterialPropertyBlock();

        haloMesh = new Mesh();
        haloMesh.name = "LampHaloQuad";
        haloMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        haloMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        haloMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        haloMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        haloMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);

        const int size = 64;
        haloTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        haloTex.name = "LampHaloTex";
        haloTex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a = a * a * a;                                   // soft falloff, hot centre
                px[y * size + x] = new Color(1f, 0.78f, 0.42f, a);
            }
        haloTex.SetPixels(px);
        haloTex.Apply(false, true);

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        if (sh != null)
        {
            haloMat = new Material(sh);
            haloMat.name = "LampHalo";
            haloMat.mainTexture = haloTex;
            haloMat.renderQueue = 3100;
        }

        for (int i = 0; i < n; i++)
        {
            GameObject go = new GameObject("LampHalo");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = haloMesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = haloMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.enabled = false;
            halos[i] = new Halo { t = go.transform, mr = mr };
        }
    }

    void UpdateHalos(float night, bool active)
    {
        if (halos == null || haloMat == null) return;
        if (!active && !anyHalo) return;

        if (Time.unscaledTime >= nextHaloTick)
        {
            nextHaloTick = Time.unscaledTime + 0.25f;
            if (haloCam == null || !haloCam.isActiveAndEnabled) haloCam = Camera.main;
            Transform t = active ? FindTarget() : null;
            RetargetHalos(t != null ? t.position : Vector3.zero, t != null);
        }

        Quaternion face = haloCam != null ? haloCam.transform.rotation : Quaternion.identity;
        float step = fadeSpeed * Time.unscaledDeltaTime;
        bool any = false;
        for (int i = 0; i < halos.Length; i++)
        {
            Halo h = halos[i];
            if (h.lamp < 0) continue;
            h.factor = Mathf.MoveTowards(h.factor, h.target, step);
            if (h.factor <= 0f && h.target <= 0f) { h.lamp = -1; h.mr.enabled = false; continue; }
            float a = haloAlpha * h.factor * night;
            if (a < 0.004f) { h.mr.enabled = false; any = true; continue; }
            h.mr.enabled = true;
            h.t.rotation = face;
            h.t.localScale = Vector3.one * (haloSize * (1f + h.dist / 70f));   // grows with distance so far lamps stay visible
            haloMpb.SetColor(IdColor, new Color(1f, 1f, 1f, a));
            h.mr.SetPropertyBlock(haloMpb);
            any = true;
        }
        anyHalo = any;
    }

    float HaloWeight(float d)
    {
        float far = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - haloRadius * 0.7f) / Mathf.Max(0.01f, haloRadius * 0.3f)));
        float near = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 3f) / 8f));      // no big blob right on top of the player
        return far * near;
    }

    void RetargetHalos(Vector3 p, bool hasTarget)
    {
        int count = 0, cap = hIdx.Length;
        if (hasTarget)
        {
            int cx0 = Mathf.FloorToInt((p.x - haloRadius) / CellSize), cx1 = Mathf.FloorToInt((p.x + haloRadius) / CellSize);
            int cz0 = Mathf.FloorToInt((p.z - haloRadius) / CellSize), cz1 = Mathf.FloorToInt((p.z + haloRadius) / CellSize);
            float maxSqr = haloRadius * haloRadius;
            for (int cx = cx0; cx <= cx1; cx++)
                for (int cz = cz0; cz <= cz1; cz++)
                {
                    List<int> list;
                    if (!grid.TryGetValue(Key(cx, cz), out list)) continue;
                    for (int k = 0; k < list.Count; k++)
                    {
                        int li = list[k];
                        float sqr = (lampPos[li] - p).sqrMagnitude;
                        if (sqr >= maxSqr) continue;
                        float d = Mathf.Sqrt(sqr);
                        if (count == cap && d >= hDist[cap - 1]) continue;
                        int pos = count < cap ? count : cap - 1;
                        while (pos > 0 && hDist[pos - 1] > d) { hIdx[pos] = hIdx[pos - 1]; hDist[pos] = hDist[pos - 1]; pos--; }
                        hIdx[pos] = li; hDist[pos] = d;
                        if (count < cap) count++;
                    }
                }
        }
        for (int i = 0; i < count; i++) hUsed[i] = false;

        for (int s = 0; s < halos.Length; s++)
        {
            Halo h = halos[s];
            if (h.lamp < 0) continue;
            int found = -1;
            for (int i = 0; i < count; i++) if (hIdx[i] == h.lamp) { found = i; break; }
            if (found >= 0) { hUsed[found] = true; h.dist = hDist[found]; h.target = HaloWeight(hDist[found]); }
            else h.target = 0f;
        }
        for (int i = 0; i < count; i++)
        {
            if (hUsed[i]) continue;
            float w = HaloWeight(hDist[i]);
            if (w < 0.01f) continue;
            Halo free = null;
            for (int s = 0; s < halos.Length; s++) if (halos[s].lamp < 0) { free = halos[s]; break; }
            if (free == null) break;
            free.lamp = hIdx[i]; free.factor = 0f; free.target = w; free.dist = hDist[i];
            free.t.position = lampPos[hIdx[i]];
        }
    }

    float Weight(float dist)
    {
        if (dist <= fullRadius) return 1f;
        float t = Mathf.Clamp01((dist - fullRadius) / Mathf.Max(0.01f, fadeRadius - fullRadius));
        t = t * t * (3f - 2f * t);
        return 1f - t;
    }

    void Retarget(Vector3 p, bool hasTarget)
    {
        int count = 0;
        if (hasTarget)
        {
            int cx0 = Mathf.FloorToInt((p.x - fadeRadius) / CellSize);
            int cx1 = Mathf.FloorToInt((p.x + fadeRadius) / CellSize);
            int cz0 = Mathf.FloorToInt((p.z - fadeRadius) / CellSize);
            int cz1 = Mathf.FloorToInt((p.z + fadeRadius) / CellSize);
            float maxSqr = fadeRadius * fadeRadius;
            int cap = candIdx.Length;
            for (int cx = cx0; cx <= cx1; cx++)
            {
                for (int cz = cz0; cz <= cz1; cz++)
                {
                    List<int> list;
                    if (!grid.TryGetValue(Key(cx, cz), out list)) continue;
                    for (int k = 0; k < list.Count; k++)
                    {
                        int li = list[k];
                        float sqr = (lampPos[li] - p).sqrMagnitude;
                        if (sqr >= maxSqr) continue;
                        float d = Mathf.Sqrt(sqr);
                        if (count == cap && d >= candDist[cap - 1]) continue;
                        int pos = count < cap ? count : cap - 1;
                        while (pos > 0 && candDist[pos - 1] > d)
                        {
                            if (pos < cap) { candIdx[pos] = candIdx[pos - 1]; candDist[pos] = candDist[pos - 1]; }
                            pos--;
                        }
                        candIdx[pos] = li;
                        candDist[pos] = d;
                        if (count < cap) count++;
                    }
                }
            }
        }

        for (int i = 0; i < count; i++) candUsed[i] = false;

        for (int s = 0; s < slots.Length; s++)
        {
            Slot slot = slots[s];
            if (slot.lamp < 0) continue;
            int found = -1;
            for (int i = 0; i < count; i++)
            {
                if (candIdx[i] == slot.lamp) { found = i; break; }
            }
            if (found >= 0)
            {
                candUsed[found] = true;
                slot.target = Weight(candDist[found]);
            }
            else
            {
                slot.target = 0f;
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (candUsed[i]) continue;
            float w = Weight(candDist[i]);
            if (w < 0.01f) continue;
            Slot free = null;
            for (int s = 0; s < slots.Length; s++)
            {
                if (slots[s].lamp < 0) { free = slots[s]; break; }
            }
            if (free == null) break;
            int li = candIdx[i];
            free.lamp = li;
            free.factor = 0f;
            free.target = w;
            float r = lampRange[li];
            free.light.range = r > 0f ? r : lightRange;
            free.light.transform.position = lampPos[li];
            free.light.intensity = 0f;
        }
    }
}
