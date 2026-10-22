using UnityEngine;

/// <summary>Visible signal head for one approach of a signalised intersection. Mirrors the exact SignalPlan the cars obey.</summary>
public class TrafficLightHead : MonoBehaviour
{
    static Material unlit;
    static readonly Color RedOn = new Color(1f, 0.08f, 0.04f) * 3f;
    static readonly Color AmberOn = new Color(1f, 0.62f, 0.04f) * 3f;
    static readonly Color GreenOn = new Color(0.1f, 1f, 0.35f) * 3f;

    SignalPlan plan;
    int axis;
    Renderer red, amber, green;
    MaterialPropertyBlock mpb;
    float next;
    int lastKey = -1;

    public static Material Mat()
    {
        if (unlit != null) return unlit;
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        unlit = new Material(sh);
        unlit.name = "TrafficLamp";
        return unlit;
    }

    public static GameObject Part(Transform parent, string n, Vector3 pos, Vector3 scale, Color c, bool lamp, out Renderer rend)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = n;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        Collider col = g.GetComponent<Collider>();
        if (col != null) WorldUtil.Kill(col);
        rend = g.GetComponent<Renderer>();
        rend.sharedMaterial = lamp ? Mat() : StylizedMaterialFactory.GetWorldPart("TrafficLight_Body", c);
        if (lamp) rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    /// <summary>Builds a pole + 3-lamp head at pos, facing along facing2D (towards the approaching drivers).</summary>
    public static TrafficLightHead Create(Transform root, Vector2 pos, float y, Vector2 facing2D, SignalPlan plan, int axis)
    {
        GameObject pivot = new GameObject("TrafficLight");
        pivot.transform.SetParent(root, false);
        pivot.transform.position = new Vector3(pos.x, y, pos.y);
        pivot.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(facing2D.x, facing2D.y) * Mathf.Rad2Deg, 0f);
        Color dark = new Color(0.12f, 0.14f, 0.15f);
        Renderer r;
        Part(pivot.transform, "Pole", new Vector3(0f, 2.1f, 0f), new Vector3(0.12f, 4.2f, 0.12f), dark, false, out r);
        Part(pivot.transform, "Housing", new Vector3(0f, 3.65f, 0f), new Vector3(0.42f, 1.0f, 0.28f), dark, false, out r);
        TrafficLightHead h = pivot.AddComponent<TrafficLightHead>();
        Part(pivot.transform, "Red", new Vector3(0f, 3.95f, 0.15f), new Vector3(0.22f, 0.22f, 0.06f), Color.red, true, out h.red);
        Part(pivot.transform, "Amber", new Vector3(0f, 3.65f, 0.15f), new Vector3(0.22f, 0.22f, 0.06f), Color.yellow, true, out h.amber);
        Part(pivot.transform, "Green", new Vector3(0f, 3.35f, 0.15f), new Vector3(0.22f, 0.22f, 0.06f), Color.green, true, out h.green);
        h.plan = plan;
        h.axis = axis;
        h.mpb = new MaterialPropertyBlock();
        h.next = Random.value * 0.2f;
        return h;
    }

    void Paint(Renderer r, Color on, bool lit)
    {
        Color c = lit ? on : on * 0.04f;
        mpb.SetColor("_BaseColor", c);
        mpb.SetColor("_Color", c);
        mpb.SetColor("_EmissionColor", c);
        r.SetPropertyBlock(mpb);
    }

    void Update()
    {
        if (Time.time < next) return;
        next = Time.time + 0.12f;
        float rem;
        SignalState st = plan.Get(axis, out rem);
        bool flashOn = st == SignalState.Flash && (Time.time % 1f) < 0.5f;
        int key = (int)st * 2 + (flashOn ? 1 : 0);
        if (key == lastKey) return;
        lastKey = key;
        Paint(red, RedOn, st == SignalState.Red);
        Paint(amber, AmberOn, st == SignalState.Amber || flashOn);
        Paint(green, GreenOn, st == SignalState.Green);
    }
}
