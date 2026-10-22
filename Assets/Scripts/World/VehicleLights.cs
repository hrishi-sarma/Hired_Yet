using System.Collections.Generic;
using UnityEngine;

public class VehicleLights : MonoBehaviour
{
    public float brakeDecel = 0.8f;
    public float honkDistance = 14f;
    public float honkLateral = 3.2f;
    public float honkVolume = 0.45f;
    // When true, a Car (traffic AI) drives the lights and VehicleLights never plays its own horn.
    [System.NonSerialized] public bool external;
    [System.NonSerialized] public bool extHead;
    [System.NonSerialized] public bool extBrake;
    // Indicator driven by the traffic AI: -1 left, +1 right, 0 off. extBlinkOn is the blink phase; extHazard blinks both sides.
    [System.NonSerialized] public int extSide;
    [System.NonSerialized] public bool extBlinkOn;
    [System.NonSerialized] public bool extHazard;

    readonly List<Renderer> heads = new List<Renderer>();
    readonly List<Renderer> tails = new List<Renderer>();
    MaterialPropertyBlock headBlock;
    MaterialPropertyBlock tailBlock;
    AudioSource audioSource;
    static AudioClip hornClip;

    Vector3 lastPos;
    float speed;
    float lastSpeed;
    bool hasMoved;
    Vector3 heading = Vector3.forward;
    float honkStart = -10f;
    bool honking;
    bool wasHonkOn;
    float brakeHold;
    Color lastHead = new Color(-1f, 0f, 0f);
    Color lastTail = new Color(-1f, 0f, 0f);
    readonly List<float> headX = new List<float>();
    readonly List<float> tailX = new List<float>();
    int lastBlinkKey = -99;
    CarLightSource src;
    bool ownerChecked;
    bool ownsSource;
    static readonly Color Amber = new Color(1f, 0.55f, 0.02f);

    public void Add(Renderer r, bool head)
    {
        if (head) { if (!heads.Contains(r)) heads.Add(r); }
        else if (!tails.Contains(r)) tails.Add(r);
    }

    void Awake()
    {
        headBlock = new MaterialPropertyBlock();
        tailBlock = new MaterialPropertyBlock();
        lastPos = transform.position;
        heading = transform.forward;
    }

    void Start()
    {
        Renderer[] rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        ContactShadows.Add(transform, new Vector3(b.center.x, b.min.y + 0.04f, b.center.z), new Vector2(b.size.x * 1.15f + 0.4f, b.size.z * 1.15f + 0.4f), transform.eulerAngles.y);
    }

    static AudioClip BuildHorn()
    {
        if (hornClip != null) return hornClip;
        int rate = 22050;
        int n = (int)(rate * 0.38f);
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Clamp01(t / 0.015f) * Mathf.Clamp01((0.38f - t) / 0.05f);
            float a = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 440f * t)) * 0.5f + Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.5f;
            float b = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 554f * t)) * 0.5f + Mathf.Sin(2f * Mathf.PI * 554f * t) * 0.5f;
            d[i] = (a + b) * 0.25f * env;
        }
        hornClip = AudioClip.Create("Horn", n, 1, rate, false);
        hornClip.SetData(d, 0);
        return hornClip;
    }

    Transform FindPlayer()
    {
        GameObject p = null;
        try { p = GameObject.FindWithTag("Player"); } catch (UnityException) { }
        if (p != null) return p.transform;
        Camera cam = Camera.main;
        return cam != null ? cam.transform : null;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 pos = transform.position;
        Vector3 delta = pos - lastPos;
        delta.y = 0f;
        float instant = delta.magnitude / dt;
        lastPos = pos;
        speed = Mathf.Lerp(speed, instant, 1f - Mathf.Exp(-8f * dt));
        float accel = (speed - lastSpeed) / dt;
        lastSpeed = speed;
        if (speed > 1f)
        {
            hasMoved = true;
            if (delta.sqrMagnitude > 0.0001f) heading = Vector3.Lerp(heading, delta.normalized, 0.3f).normalized;
        }

        bool decelerating = speed > 0.25f && accel < -brakeDecel;
        if (decelerating) brakeHold = 0.35f;
        else brakeHold = Mathf.Max(0f, brakeHold - dt);
        bool stoppedAfterMoving = hasMoved && speed < 0.25f;
        bool brake = brakeHold > 0f || stoppedAfterMoving;
        if (external) brake = extBrake;

        if (!ownerChecked)
        {
            ownerChecked = true;
            ownsSource = GetComponentInParent<CarLights>() == null;
        }
        if (ownsSource && !external)
        {
            if (src == null)
            {
                src = GetComponent<CarLightSource>();
                if (src == null) src = gameObject.AddComponent<CarLightSource>();
            }
            src.head = StylizedVehiclePainter.CurrentNight > 0.35f && (hasMoved || speed > 0.5f);
            src.brake = brake;
        }

        bool wantHonk = false;
        if (!external && hasMoved && speed < 1.2f)
        {
            Transform player = FindPlayer();
            if (player != null)
            {
                Vector3 to = player.position - pos;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist < honkDistance && dist > 0.5f)
                {
                    float ahead = Vector3.Dot(to, heading);
                    float side = Mathf.Abs(Vector3.Dot(to, Vector3.Cross(Vector3.up, heading)));
                    wantHonk = ahead > 0.5f && side < honkLateral;
                }
            }
        }
        if (wantHonk && !honking) honkStart = Time.time;
        honking = wantHonk;

        bool honkOn = false;
        if (honking)
        {
            float t = (Time.time - honkStart) % 2f;
            honkOn = t < 0.38f || (t > 0.55f && t < 0.93f);
        }
        if (honkOn && !wasHonkOn) PlayHorn();
        wasHonkOn = honkOn;

        float night = external ? (extHead ? 1f : 0f) : StylizedVehiclePainter.CurrentNight;
        Color headColor = new Color(1f, 0.92f, 0.72f) * (external || hasMoved || speed > 0.5f ? 3.2f * night : 0f);
        if (honkOn) headColor = new Color(1f, 0.95f, 0.8f) * 4.2f;
        Color tailColor = new Color(1f, 0.08f, 0.05f) * (external || hasMoved || speed > 0.5f ? 0.9f * night : 0f);
        if (brake) tailColor = new Color(1f, 0.1f, 0.06f) * 3.8f;

        bool moving = hasMoved || speed > 0.5f;

        // Indicators reuse the existing head/tail lamps: the lamp on the signalled side flashes orange.
        int blinkKey = external && extBlinkOn ? (extHazard ? 2 : extSide) : 0;
        bool changedHead = !Approximately(headColor, lastHead);
        bool changedTail = !Approximately(tailColor, lastTail);
        if (!changedHead && !changedTail && blinkKey == lastBlinkKey) return;
        lastBlinkKey = blinkKey;
        lastHead = headColor;
        lastTail = tailColor;
        ApplyGroup(heads, headX, headBlock, headColor, blinkKey, 3.4f);
        ApplyGroup(tails, tailX, tailBlock, tailColor, blinkKey, 3.0f);
    }

    void ApplyGroup(List<Renderer> list, List<float> xs, MaterialPropertyBlock block, Color baseColor, int blinkKey, float power)
    {
        while (xs.Count < list.Count)
        {
            Renderer r = list[xs.Count];
            xs.Add(r != null ? transform.InverseTransformPoint(r.bounds.center).x : 0f);
        }
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null) continue;
            Color c = baseColor;
            bool lampOn = blinkKey == 2 || (blinkKey < 0 && xs[i] < -0.05f) || (blinkKey > 0 && xs[i] > 0.05f);
            if (blinkKey != 0 && lampOn) c = Amber * power;
            block.SetColor("_EmissionColor", c);
            list[i].SetPropertyBlock(block);
        }
    }

    static bool Approximately(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.01f;
    }

    void PlayHorn()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 6f;
            audioSource.maxDistance = 70f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.playOnAwake = false;
        }
        audioSource.pitch = 0.95f + (GetInstanceID() % 10) * 0.012f;
        audioSource.PlayOneShot(BuildHorn(), honkVolume);
    }
}
