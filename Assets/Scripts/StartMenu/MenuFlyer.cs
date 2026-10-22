using UnityEngine;

/// <summary>
/// Stage 4C: baked butterfly / bird. No AI: butterflies wander on a looping Lissajous path, birds soar in circles
/// (flap bursts + glides, banking into the turn). Wings flap procedurally about the local Z axis.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Start Menu/Menu Flyer")]
public class MenuFlyer : MonoBehaviour
{
    public bool butterfly;
    public Vector3 center;
    public Vector3 extent = new Vector3(5f, 1f, 5f);   // butterfly: x/z wander radius + y bob. bird: x = orbit radius, y = altitude wobble
    public float speed = 8f;                           // bird ground speed (m/s)
    public float phase;
    public int direction = 1;
    public Transform[] wings = new Transform[0];

    Quaternion[] home;
    float t, angle;
    Vector3 lastPos;

    void Awake()
    {
        home = new Quaternion[wings.Length];
        for (int i = 0; i < wings.Length; i++) if (wings[i] != null) home[i] = wings[i].localRotation;
        t = phase;
        angle = phase;
        Vector3 p = Pos(t, out Vector3 fwd);
        transform.SetPositionAndRotation(p, Look(fwd, 0f));
        lastPos = p;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        t += dt;
        Vector3 fwd;
        float bank = 0f, flap, amp;
        if (butterfly)
        {
            Vector3 p = Pos(t, out fwd);
            transform.position = p;
            flap = Mathf.Sin(t * 44f + phase); amp = 55f * (0.6f + 0.4f * Mathf.Sin(t * 1.7f));
        }
        else
        {
            angle += direction * speed / Mathf.Max(5f, extent.x) * dt;
            Vector3 p = Pos(t, out fwd);
            transform.position = p;
            bank = -direction * 18f;
            float cyc = Mathf.Repeat(t + phase * 3f, 4.5f);
            float flapW = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((2.5f - cyc) * 3f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(cyc * 3f));
            flap = Mathf.Sin(t * 30f) * flapW; amp = 42f * flapW + 8f;
            amp = Mathf.Max(amp, 8f);
            if (flapW < 0.05f) flap = 0.4f;   // glide: wings held slightly up
        }
        transform.rotation = Quaternion.Slerp(transform.rotation, Look(fwd, bank), 1f - Mathf.Exp(-6f * dt));
        for (int i = 0; i < wings.Length; i++)
            if (wings[i] != null) wings[i].localRotation = home[i] * Quaternion.Euler(0f, 0f, (i == 0 ? 1f : -1f) * flap * amp);
    }

    Vector3 Pos(float time, out Vector3 fwd)
    {
        Vector3 p, v;
        if (butterfly)
        {
            float a1 = 0.45f, a2 = 1.1f, a3 = 0.61f;
            p = center + new Vector3(extent.x * Mathf.Sin(a1 * time + phase), extent.y * Mathf.Sin(a2 * time + phase * 1.7f), extent.z * Mathf.Sin(a3 * time + phase * 0.6f));
            v = new Vector3(extent.x * a1 * Mathf.Cos(a1 * time + phase), extent.y * a2 * Mathf.Cos(a2 * time + phase * 1.7f), extent.z * a3 * Mathf.Cos(a3 * time + phase * 0.6f));
        }
        else
        {
            float r = extent.x * (1f + 0.12f * Mathf.Sin(0.3f * time + phase));
            p = center + new Vector3(Mathf.Cos(angle) * r, extent.y * Mathf.Sin(0.4f * time + phase), Mathf.Sin(angle) * r);
            v = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * direction;
        }
        fwd = v.sqrMagnitude > 0.0001f ? v.normalized : transform.forward;
        return p;
    }

    static Quaternion Look(Vector3 fwd, float bank)
    {
        return Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0f, 0f, bank);
    }
}
