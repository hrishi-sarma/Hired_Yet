using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static menu camera: auto-frames suburb + city + forest from the mountains behind the suburb,
/// then adds slow ambient sway and mouse parallax. Everything is tweakable in the inspector;
/// "Manual adjust" values are added on top of the automatic framing.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Start Menu/Menu Camera Rig")]
public class MenuCameraRig : MonoBehaviour
{
    public Camera cam;

    [Header("Auto framing (needs the generated world)")]
    public bool autoFrame = true;
    [Tooltip("How far behind the suburb centre (toward the mountains), in suburb radii.")]
    public float behindSuburbRadii = 1.1f;
    [Tooltip("Camera height above the terrain under it (metres).")]
    public float heightAboveGround = 70f;
    [Tooltip("0 = look at suburb, 1 = look at city centre.")]
    [Range(0f, 1f)] public float lookAtCityBlend = 0.6f;
    public float lookAtHeight = 8f;
    [Tooltip("Extra room around the fitted content (1.1 = 10%).")]
    public float fitMargin = 1.12f;
    public Vector2 fovLimits = new Vector2(35f, 80f);
    public float cityBuildingHeight = 45f;

    [Header("Manual adjust (added on top of auto framing)")]
    public Vector3 manualOffset;
    public Vector3 manualEuler;
    public float fovOffset;
    [Tooltip("Used when Auto Frame is off: the camera FOV is forced to this value (0 = leave the camera as is).")]
    public float manualFov = 55f;

    [Header("Handheld feel (slow, gentle; no mouse parallax)")]
    public float handPos = 0.35f;      // metres of drift
    public float handRot = 0.35f;      // degrees (yaw / pitch)
    public float handRoll = 0.2f;      // degrees
    public float handSpeed = 0.16f;    // overall tempo (lower = slower)

    public bool Framed { get; private set; }
    public Vector3 FocusPoint { get; private set; }

    Vector3 basePos; Quaternion baseRot;
    readonly List<Vector3> fitPoints = new List<Vector3>();
    float lastAspect; float baseFov = 50f;

    void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>();
        basePos = transform.position; baseRot = transform.rotation;
        if (cam != null) { if (!autoFrame && manualFov > 0f) cam.fieldOfView = manualFov; baseFov = cam.fieldOfView; }
    }

    /// <summary>Computes position/rotation/FOV from the generated world.</summary>
    public void Frame(WorldContext c)
    {
        if (c == null || c.zoneMap == null) return;
        if (!autoFrame)
        {
            // keep the pose exactly as set in the scene; focus = where the view axis meets the terrain (used to place the roaming cast)
            Vector3 o = transform.position, d = transform.forward; Vector3 hit = o + d * 200f;
            for (float t = 5f; t < 900f; t += 5f) { Vector3 q = o + d * t; if (q.y <= c.HeightAt(q.x, q.z)) { hit = q; break; } }
            hit.y = c.HeightAt(hit.x, hit.z);
            FocusPoint = hit; Framed = true;
            if (cam != null && manualFov > 0f) cam.fieldOfView = manualFov;
            return;
        }

        ZoneMap z = c.zoneMap;
        Vector2 sub = z.suburbCenter, city = z.cityCenter;
        Vector2 toCity = city - sub;
        if (toCity.sqrMagnitude < 1f) toCity = Vector2.up;
        Vector2 awayDir = -toCity.normalized;

        // sample zones once: mountains near the suburb, forest around the middle of the shot
        Vector2 mtn = Vector2.zero, forest = Vector2.zero; int nm = 0, nf = 0;
        Vector2 mid0 = (sub + city) * 0.5f;
        float reachF = toCity.magnitude + z.suburbRadius + z.cityRadius;
        int n = 48; float step = c.Size / n;
        for (int i = 0; i <= n; i++)
            for (int j = 0; j <= n; j++)
            {
                Vector2 p = new Vector2(-c.Size * 0.5f + i * step, -c.Size * 0.5f + j * step);
                ZoneType zt = z.ZoneAt(p);
                if (zt == ZoneType.Mountain && (p - sub).magnitude < z.suburbRadius * 3.5f) { mtn += p; nm++; }
                else if (zt == ZoneType.Forest && (p - mid0).magnitude < reachF) { forest += p; nf++; }
            }

        Vector2 camDir = awayDir;
        if (nm > 0) { Vector2 d = mtn / nm - sub; if (d.sqrMagnitude > 1f) camDir = d.normalized; }
        float half = c.Size * 0.48f;
        Vector2 cxz = sub + camDir * z.suburbRadius * behindSuburbRadii;
        cxz = new Vector2(Mathf.Clamp(cxz.x, -half, half), Mathf.Clamp(cxz.y, -half, half));

        float gy = c.HeightAt(cxz.x, cxz.y);
        for (int k = 1; k <= 8; k++)   // never end up inside a ridge in front of us
        {
            Vector2 q = Vector2.Lerp(cxz, sub, k / 8f);
            gy = Mathf.Max(gy, c.HeightAt(q.x, q.y) - k * 2f);
        }
        Vector3 pos = new Vector3(cxz.x, gy + heightAboveGround, cxz.y);

        Vector2 look = Vector2.Lerp(sub, city, lookAtCityBlend);
        Vector3 target = new Vector3(look.x, c.HeightAt(look.x, look.y) + lookAtHeight, look.y);
        FocusPoint = new Vector3(look.x, c.HeightAt(look.x, look.y), look.y);

        fitPoints.Clear();
        Vector2 side = new Vector2(-toCity.normalized.y, toCity.normalized.x);
        AddFit(c, sub - toCity.normalized * z.suburbRadius, 6f);
        AddFit(c, sub + side * z.suburbRadius, 6f);
        AddFit(c, sub - side * z.suburbRadius, 6f);
        AddFit(c, city + toCity.normalized * z.cityRadius, cityBuildingHeight);
        AddFit(c, city + side * z.cityRadius, cityBuildingHeight);
        AddFit(c, city - side * z.cityRadius, cityBuildingHeight);
        if (nf > 0) AddFit(c, forest / nf, 25f);

        basePos = pos + manualOffset;
        baseRot = Quaternion.LookRotation(target - pos, Vector3.up) * Quaternion.Euler(manualEuler);
        transform.SetPositionAndRotation(basePos, baseRot);
        Refit();
        Framed = true;
    }

    void AddFit(WorldContext c, Vector2 p, float up)
    {
        fitPoints.Add(new Vector3(p.x, c.HeightAt(p.x, p.y) + up, p.y));
    }

    void Refit()
    {
        if (cam == null || fitPoints.Count == 0) return;
        float aspect = Mathf.Max(0.5f, (float)Screen.width / Mathf.Max(1, Screen.height));
        lastAspect = aspect;
        Quaternion inv = Quaternion.Inverse(baseRot);
        float worst = 0.1f;
        for (int i = 0; i < fitPoints.Count; i++)
        {
            Vector3 l = inv * (fitPoints[i] - basePos);
            if (l.z < 1f) continue;
            float v = Mathf.Atan2(Mathf.Abs(l.y), l.z);
            float h = Mathf.Atan(Mathf.Tan(Mathf.Atan2(Mathf.Abs(l.x), l.z)) / aspect);
            worst = Mathf.Max(worst, v, h);
        }
        float fov = 2f * worst * Mathf.Rad2Deg * fitMargin + fovOffset;
        baseFov = Mathf.Clamp(fov, fovLimits.x, fovLimits.y);
        cam.fieldOfView = baseFov;
    }

    void LateUpdate()
    {
        if (!Framed && autoFrame) return;
        if (autoFrame && cam != null && Mathf.Abs((float)Screen.width / Mathf.Max(1, Screen.height) - lastAspect) > 0.01f) Refit();

        // two layers of smooth noise (slow drift + a softer, quicker wobble) = relaxed handheld camera
        float t = Time.unscaledTime * handSpeed;
        Vector3 d = Vector3.zero, r = Vector3.zero;
        for (int i = 0; i < 2; i++)
        {
            float f = i == 0 ? 1f : 2.7f, w = i == 0 ? 1f : 0.35f, o = i * 31.7f;
            d.x += (Mathf.PerlinNoise(t * f + o, 11.3f) - 0.5f) * w; d.y += (Mathf.PerlinNoise(23.7f, t * f + o) - 0.5f) * w; d.z += (Mathf.PerlinNoise(t * f + o, 47.1f) - 0.5f) * w;
            r.x += (Mathf.PerlinNoise(t * f + o, 5.5f) - 0.5f) * w; r.y += (Mathf.PerlinNoise(8.8f, t * f + o) - 0.5f) * w; r.z += (Mathf.PerlinNoise(t * f + o, 61.9f) - 0.5f) * w;
        }
        transform.position = basePos + baseRot * (d * (2f * handPos));
        transform.rotation = baseRot * Quaternion.Euler(r.x * 2f * handRot, r.y * 2f * handRot, r.z * 2f * handRoll);
    }
}
