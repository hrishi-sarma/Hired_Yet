using UnityEngine;

/// <summary>Head, tail, brake, indicator and hazard lights for traffic cars. Uses VehicleLights' renderers when present.</summary>
public class CarLights : MonoBehaviour
{
    static Material unlit;
    static bool headsNeeded;
    static float nextClock;

    Renderer[] heads;
    Renderer[] tails;
    // Car-model indicator parts named "indicator_Left" / "indicator_Right" (headlights and taillights are never touched for blinking).
    readonly System.Collections.Generic.List<Material> indL = new System.Collections.Generic.List<Material>();
    readonly System.Collections.Generic.List<Material> indR = new System.Collections.Generic.List<Material>();
    readonly System.Collections.Generic.List<Color> indLBase = new System.Collections.Generic.List<Color>();
    readonly System.Collections.Generic.List<Color> indRBase = new System.Collections.Generic.List<Color>();
    float nextInd;
    bool indFound;
    MaterialPropertyBlock mpb;
    VehicleLights vl;
    float nextVl;
    float length = 4.2f;
    bool built;
    bool head;
    bool brake;
    int indicator;           // -1 left, +1 right
    bool hazard;
    float lastKey = -1f;
    CarLightSource src;

    float flashStart = -10f, flashUntil = -10f;

    /// <summary>Flashes the headlights (double/triple blink) at whoever is blocking the road. Works day and night.</summary>
    public void Flash(float seconds)
    {
        if (Time.time < flashUntil) return;   // one flash sequence at a time
        flashStart = Time.time;
        flashUntil = Time.time + Mathf.Max(0.6f, seconds);
    }

    bool FlashOn { get { return Time.time < flashUntil && ((Time.time - flashStart) % 0.32f) < 0.17f; } }

    public void Configure(float bodyLength)
    {
        length = bodyLength;
    }

    public void Set(bool headlights, bool brakeOn, int indicatorDir, bool hazardOn)
    {
        head = headlights;
        brake = brakeOn;
        indicator = indicatorDir;
        hazard = hazardOn;
    }

    /// <summary>Whether the lights should be on at the current hour (cached, refreshed once a second).</summary>
    public static bool NightHeadlights
    {
        get
        {
            if (Time.time >= nextClock)
            {
                nextClock = Time.time + 1f;
                headsNeeded = TrafficClock.HeadlightsOn;
            }
            return headsNeeded;
        }
    }

    static Material Mat()
    {
        if (unlit != null) return unlit;
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        unlit = new Material(sh);
        unlit.name = "CarLight";
        return unlit;
    }

    Renderer Make(string n, Vector3 pos, Vector3 size)
    {
        GameObject g = PrimitiveFactory.Create(PrimitiveKind.Cube, transform);
        g.name = n;
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        Collider c = g.GetComponent<Collider>();
        if (c != null) WorldUtil.Kill(c);
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = Mat();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return r;
    }

    void Build()
    {
        built = true;
        mpb = new MaterialPropertyBlock();
        float k = length / 4.2f;
        float hz = length * 0.5f;
        float w = 1.8f * k;
        Vector3 s = new Vector3(0.3f, 0.16f, 0.05f) * Mathf.Max(0.7f, k);
        heads = new Renderer[] { Make("HeadL", new Vector3(-w * 0.32f, 0.62f, hz), s), Make("HeadR", new Vector3(w * 0.32f, 0.62f, hz), s) };
        tails = new Renderer[] { Make("TailL", new Vector3(-w * 0.32f, 0.66f, -hz), s), Make("TailR", new Vector3(w * 0.32f, 0.66f, -hz), s) };
        for (int i = 0; i < 2; i++)
        {
            Paint(heads[i], Color.black);
            Paint(tails[i], Color.black);
        }
    }

    void Paint(Renderer r, Color c)
    {
        if (r == null) return;
        mpb.SetColor("_BaseColor", c);
        mpb.SetColor("_Color", c);
        mpb.SetColor("_EmissionColor", c);
        r.SetPropertyBlock(mpb);
    }

    void FindIndicators()
    {
        indFound = true;
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string n = all[i].name.ToLowerInvariant();
            bool left = n.Contains("indicator_left");
            bool right = n.Contains("indicator_right");
            if (!left && !right) continue;
            Renderer[] rs = all[i].GetComponentsInChildren<Renderer>(true);
            for (int k = 0; k < rs.Length; k++)
            {
                Material m = rs[k].material; // per-car instance so blinking never leaks to other cars
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.black);
                m.EnableKeyword("_EMISSION");
                if (left) { indL.Add(m); indLBase.Add(c); }
                else { indR.Add(m); indRBase.Add(c); }
            }
        }
    }

    static void SetInd(System.Collections.Generic.List<Material> ms, System.Collections.Generic.List<Color> baseC, bool on)
    {
        Color amber = new Color(1f, 0.55f, 0.02f);
        for (int i = 0; i < ms.Count; i++)
        {
            Material m = ms[i];
            if (m == null) continue;
            Color c = on ? amber : baseC[i];
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", on ? amber * 3f : Color.black);
        }
    }

    void Update()
    {
        if (!built) Build();
        if (!indFound && Time.time >= nextInd)
        {
            nextInd = Time.time + 2f;
            FindIndicators();
            if (indL.Count + indR.Count == 0) indFound = false; // model may not be attached yet; retry
        }
        if (vl == null && Time.time >= nextVl)
        {
            nextVl = Time.time + 2f;
            vl = GetComponentInChildren<VehicleLights>(true);
            if (vl != null)
            {
                vl.external = true;
                for (int i = 0; i < 2; i++)
                {
                    heads[i].gameObject.SetActive(false);
                    tails[i].gameObject.SetActive(false);
                }
            }
        }
        float blink = (Time.time * 1.7f) % 1f < 0.5f ? 1f : 0f;
        bool lOn = (hazard || indicator < 0) && blink > 0f;
        bool rOn = (hazard || indicator > 0) && blink > 0f;
        if (src == null)
        {
            src = GetComponent<CarLightSource>();
            if (src == null) src = gameObject.AddComponent<CarLightSource>();
        }
        src.length = length;
        bool flashOn = FlashOn;
        bool headEff = head || flashOn;
        src.head = headEff;
        src.brake = brake;
        src.indicator = indicator;
        src.hazard = hazard;
        src.blinkOn = blink > 0f;
        float key = (headEff ? 1f : 0f) + (brake ? 2f : 0f) + (lOn ? 4f : 0f) + (rOn ? 8f : 0f);
        if (vl != null)
        {
            vl.extHead = headEff;
            vl.extFlash = flashOn;
            vl.extBrake = brake;
        }
        if (Mathf.Approximately(key, lastKey)) return;
        lastKey = key;
        SetInd(indL, indLBase, lOn);
        SetInd(indR, indRBase, rOn);
        if (vl == null)
        {
            Color h = headEff ? new Color(1f, 0.95f, 0.78f) * (flashOn ? 4f : 2.5f) : new Color(0.25f, 0.25f, 0.22f);
            Color t = brake ? new Color(1f, 0.08f, 0.04f) * 3f : (head ? new Color(0.7f, 0.04f, 0.03f) : new Color(0.25f, 0.04f, 0.03f));
            for (int i = 0; i < 2; i++)
            {
                Paint(heads[i], h);
                Paint(tails[i], t);
            }
        }
    }

    void OnDisable()
    {
        lastKey = -1f;
        flashUntil = -10f;
    }
}
