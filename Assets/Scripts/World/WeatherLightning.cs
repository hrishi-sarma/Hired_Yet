using UnityEngine;

// Thunderstorm lightning, driven by WeatherController (no Update of its own).
// Every strike rolls a strength 0.15..1 (weighted towards weak ones, so a really bright one is an occasional event). Strength drives:
//   - how close the strike lands (strong = nearby),
//   - the bolt: jagged main channel + glow + branches, with 2-4 quick return-stroke flickers,
//   - Flash (0..1): WeatherController adds it to ambient / fog / sky / sun, so a strong strike lights up the whole street,
//   - the thunder: thunder clip played after a delay (speed of sound, a little exaggerated), louder, lower and heavier for strong strikes.
public class WeatherLightning : MonoBehaviour
{
    public float Flash { get; private set; }
    public bool Busy { get { return strikeActive || thCount > 0; } }
    public AudioClip thunderClip;

    public Vector2 strikeGap = new Vector2(5f, 20f);   // seconds between strikes
    [Range(0f, 1f)] public float clusterChance = 0.3f; // chance that a strike is followed quickly by another
    public float minDistance = 80f, maxDistance = 420f;
    public float speedOfSound = 343f;
    [Range(1f, 3f)] public float delayScale = 1.6f;    // >1 exaggerates the lag between flash and thunder
    [Range(0f, 1f)] public float thunderVolume = 0.9f;

    const int PtsMax = 33;
    const int Branches = 2;
    const int BranchPts = 7;
    const int MaxPulses = 5;
    const int MaxThunder = 6;

    sealed class Bolt
    {
        public GameObject go;
        public LineRenderer main, glow;
        public LineRenderer[] branch = new LineRenderer[Branches];
    }

    Bolt bolt;
    Material mat;
    readonly Vector3[] pts = new Vector3[PtsMax];
    readonly Vector3[] bpts = new Vector3[BranchPts];
    readonly float[] pulseT = new float[MaxPulses], pulseA = new float[MaxPulses];
    int pulseCount;
    bool strikeActive;
    float strikeStart, strikeStrength, strikeEnd, widthMain;
    float nextStrike = -1f;

    readonly float[] thAt = new float[MaxThunder], thVol = new float[MaxThunder], thPitch = new float[MaxThunder];
    int thCount;

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }

    public void Tick(bool storm, Camera cam)
    {
        float now = Time.time;
        if (storm && cam != null)
        {
            if (nextStrike < 0f) nextStrike = now + Random.Range(3f, 9f);
            if (now >= nextStrike)
            {
                if (strikeActive) nextStrike = now + 0.25f;
                else
                {
                    Strike(cam, now);
                    nextStrike = now + (Random.value < clusterChance ? Random.Range(0.9f, 2.5f) : Random.Range(strikeGap.x, strikeGap.y));
                }
            }
        }
        else nextStrike = -1f;

        UpdateStrike(now);
        UpdateThunder(now);
    }

    public void Stop()
    {
        nextStrike = -1f;
        strikeActive = false;
        Flash = 0f;
        SetBoltActive(false);
    }

    // ------------------------------------------------------------------ strike
    void Strike(Camera cam, float now)
    {
        float s = 0.15f + 0.85f * Mathf.Pow(Random.value, 1.8f);
        float dist = Mathf.Lerp(maxDistance, minDistance, Mathf.Pow(s, 0.7f)) * Random.Range(0.85f, 1.15f);

        Vector3 cp = cam.transform.position;
        float yaw = cam.transform.eulerAngles.y + (Random.value < 0.8f ? Random.Range(-55f, 55f) : Random.Range(-180f, 180f));
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 ground = cp + dir * dist;
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(ground.x, cp.y + 300f, ground.z), Vector3.down, out hit, 700f, ~0, QueryTriggerInteraction.Ignore)) ground.y = hit.point.y;
        else ground.y = cp.y - 1.5f;
        Vector3 top = new Vector3(ground.x + Random.Range(-50f, 50f), cp.y + Random.Range(150f, 230f), ground.z + Random.Range(-50f, 50f));

        EnsureBolt();
        BuildMain(top, ground);
        BuildBranches(ground, dist);

        widthMain = (0.5f + 1.1f * s) * (1f + dist / 90f) * 0.5f;
        bolt.main.startWidth = widthMain; bolt.main.endWidth = widthMain * 0.45f;
        bolt.glow.startWidth = widthMain * 6f; bolt.glow.endWidth = widthMain * 3f;
        for (int i = 0; i < Branches; i++) { bolt.branch[i].startWidth = widthMain * 0.45f; bolt.branch[i].endWidth = widthMain * 0.1f; }

        // return strokes: 2-4 quick flickers, stronger strikes flicker more
        pulseCount = Mathf.Clamp(2 + Mathf.RoundToInt(s * 2f + Random.value), 2, MaxPulses);
        float t = 0f;
        for (int i = 0; i < pulseCount; i++)
        {
            pulseT[i] = t;
            pulseA[i] = i == 0 ? 1f : Random.Range(0.35f, 0.95f);
            t += Random.Range(0.06f, 0.16f);
        }
        strikeStart = now; strikeStrength = s; strikeEnd = now + t + 0.5f;
        strikeActive = true;
        SetBoltActive(true);

        // thunder: later, and bigger when the strike is stronger
        if (thCount < MaxThunder)
        {
            thAt[thCount] = now + 0.35f + dist / Mathf.Max(100f, speedOfSound) * delayScale;
            thVol[thCount] = thunderVolume * 1.8f * Mathf.Lerp(0.45f, 1f, Mathf.Pow(s, 0.8f)) * Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(dist / maxDistance));
            thPitch[thCount] = Mathf.Lerp(1.1f, 0.82f, s) * Random.Range(0.97f, 1.03f);
            thCount++;
        }
    }

    void UpdateStrike(float now)
    {
        if (!strikeActive) { Flash = 0f; return; }
        if (now >= strikeEnd) { strikeActive = false; Flash = 0f; SetBoltActive(false); return; }

        float t = now - strikeStart;
        float I = 0.25f * Mathf.Exp(-t / 0.5f);                    // cloud afterglow
        for (int i = 0; i < pulseCount; i++)
        {
            float x = t - pulseT[i];
            if (x < 0f) continue;
            float p = x < 0.02f ? x / 0.02f : Mathf.Exp(-(x - 0.02f) / 0.07f);
            I += pulseA[i] * p;
        }
        Flash = strikeStrength * Mathf.Min(I, 1.2f);

        float a = Mathf.Clamp01(I) * (0.45f + 0.55f * strikeStrength);
        bolt.main.startColor = bolt.main.endColor = new Color(0.93f, 0.96f, 1f, a);
        bolt.glow.startColor = bolt.glow.endColor = new Color(0.55f, 0.66f, 1f, a * 0.3f);
        for (int i = 0; i < Branches; i++)
            bolt.branch[i].startColor = bolt.branch[i].endColor = new Color(0.85f, 0.9f, 1f, a * 0.8f);
    }

    void UpdateThunder(float now)
    {
        for (int i = thCount - 1; i >= 0; i--)
        {
            if (now < thAt[i]) continue;
            if (thunderClip != null)
            {
                AudioDirector.Play2D(thunderClip, "ambient", Mathf.Clamp01(thVol[i]), thPitch[i]);
                if (thVol[i] > 1f) AudioDirector.Play2D(thunderClip, "ambient", Mathf.Clamp01(thVol[i] - 1f + 0.4f), thPitch[i] * 0.97f);   // layered copy: a single source cannot exceed volume 1
            }
            thCount--;
            thAt[i] = thAt[thCount]; thVol[i] = thVol[thCount]; thPitch[i] = thPitch[thCount];
        }
    }

    // ------------------------------------------------------------------ geometry
    void BuildMain(Vector3 a, Vector3 b)
    {
        int n = 2;
        pts[0] = a; pts[1] = b;
        float amp = (a - b).magnitude * 0.12f;
        for (int iter = 0; iter < 5; iter++)
        {
            for (int i = n - 1; i >= 1; i--) pts[2 * i] = pts[i];
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 m = (pts[2 * i] + pts[2 * i + 2]) * 0.5f;
                Vector3 off = Random.insideUnitSphere; off.y *= 0.3f;
                pts[2 * i + 1] = m + off * amp;
            }
            n = n * 2 - 1;
            amp *= 0.52f;
        }
        bolt.main.positionCount = n; bolt.main.SetPositions(pts);
        bolt.glow.positionCount = n; bolt.glow.SetPositions(pts);
    }

    void BuildBranches(Vector3 ground, float dist)
    {
        float len = Mathf.Clamp(dist * 0.12f, 25f, 70f);
        for (int b = 0; b < Branches; b++)
        {
            LineRenderer lr = bolt.branch[b];
            if (Random.value < 0.25f) { lr.positionCount = 0; continue; }
            Vector3 p = pts[Random.Range(8, 24)];
            Vector3 d = new Vector3(Random.Range(-1f, 1f), -Random.Range(0.4f, 0.9f), Random.Range(-1f, 1f)).normalized;
            for (int i = 0; i < BranchPts; i++)
            {
                bpts[i] = p;
                Vector3 jitter = Random.insideUnitSphere * (len * 0.06f);
                p += d * (len / (BranchPts - 1)) + jitter;
            }
            lr.positionCount = BranchPts; lr.SetPositions(bpts);
        }
    }

    // ------------------------------------------------------------------ objects
    void EnsureBolt()
    {
        if (bolt != null) return;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        mat = new Material(sh);
        mat.name = "LightningBolt";
        mat.renderQueue = 3200;

        bolt = new Bolt();
        bolt.go = new GameObject("LightningBolt");
        bolt.go.transform.SetParent(transform, false);
        bolt.glow = MakeLine("Glow");
        bolt.main = MakeLine("Main");
        for (int i = 0; i < Branches; i++) bolt.branch[i] = MakeLine("Branch");
        SetBoltActive(false);
    }

    LineRenderer MakeLine(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(bolt.go.transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = mat;
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.View;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        lr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        lr.positionCount = 0;
        return lr;
    }

    void SetBoltActive(bool on)
    {
        if (bolt != null && bolt.go != null) bolt.go.SetActive(on);
    }
}
