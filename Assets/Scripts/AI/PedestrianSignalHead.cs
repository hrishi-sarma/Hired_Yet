using UnityEngine;

/// <summary>Walk / don't-walk head for a crosswalk. Reads the same SignalPlan the cars use via CrosswalkRegistry.WalkPhase.</summary>
public class PedestrianSignalHead : MonoBehaviour
{
    static readonly Color RedOn = new Color(1f, 0.08f, 0.04f) * 3f;
    static readonly Color GreenOn = new Color(0.2f, 1f, 0.45f) * 3f;

    CrosswalkRegistry reg;
    CrosswalkId id;
    Renderer red, green;
    MaterialPropertyBlock mpb;
    float next;
    int lastKey = -1;

    public static PedestrianSignalHead Create(Transform root, Vector2 pos, float y, Vector2 facing2D, CrosswalkRegistry reg, CrosswalkId id)
    {
        GameObject pivot = new GameObject("PedestrianSignal");
        pivot.transform.SetParent(root, false);
        pivot.transform.position = new Vector3(pos.x, y, pos.y);
        pivot.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(facing2D.x, facing2D.y) * Mathf.Rad2Deg, 0f);
        Color dark = new Color(0.12f, 0.14f, 0.15f);
        Renderer r;
        TrafficLightHead.Part(pivot.transform, "Pole", new Vector3(0f, 1.4f, 0f), new Vector3(0.1f, 2.8f, 0.1f), dark, false, out r);
        TrafficLightHead.Part(pivot.transform, "Housing", new Vector3(0f, 2.55f, 0f), new Vector3(0.34f, 0.72f, 0.2f), dark, false, out r);
        PedestrianSignalHead h = pivot.AddComponent<PedestrianSignalHead>();
        TrafficLightHead.Part(pivot.transform, "DontWalk", new Vector3(0f, 2.72f, 0.11f), new Vector3(0.22f, 0.22f, 0.05f), Color.red, true, out h.red);
        TrafficLightHead.Part(pivot.transform, "Walk", new Vector3(0f, 2.40f, 0.11f), new Vector3(0.22f, 0.22f, 0.05f), Color.green, true, out h.green);
        h.reg = reg;
        h.id = id;
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
        int phase = reg.WalkPhase(id);                       // 0 stop, 1 walk, 2 clearance
        bool flashing = TrafficSignals.Flashing;             // late night: unsignalised behaviour, show steady don't-walk dimmed
        bool blink = (Time.time % 0.8f) < 0.4f;
        int key = (flashing ? 10 : phase) * 2 + (phase == 2 && blink ? 1 : 0);
        if (key == lastKey) return;
        lastKey = key;
        Paint(green, GreenOn, !flashing && phase == 1);
        Paint(red, RedOn, !flashing && (phase == 0 || (phase == 2 && blink)));
    }
}
