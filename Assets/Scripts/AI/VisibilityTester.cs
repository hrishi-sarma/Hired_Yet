using UnityEngine;

public static class VisibilityTester
{
    static readonly Plane[] planes = new Plane[6];
    static int planeFrame = -1;
    static Camera planeCam;
    static int budget = 12;
    static int budgetMax = 12;
    static readonly RaycastHit[] hits = new RaycastHit[24];
    const float MinOccluderSize = 1.2f;

    static bool Occluded(Vector3 from, Vector3 to, Transform owner)
    {
        Vector3 d = to - from;
        float len = d.magnitude;
        if (len < 0.01f) return false;
        int n = Physics.RaycastNonAlloc(from, d / len, hits, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (c == null) continue;
            Transform t = c.transform;
            if (owner != null && t.IsChildOf(owner)) continue;
            if (c.GetComponentInParent<AgentBase>() != null) continue;
            NpcDirector dir = NpcDirector.Instance;
            if (dir != null && dir.Player != null && t.IsChildOf(dir.Player)) continue;
            Vector3 size = c.bounds.size;
            if (Mathf.Max(size.x, size.z) < MinOccluderSize && size.y < 8f) continue;
            return true;
        }
        return false;
    }

    public static void BeginFrame(int linecastsPerFrame)
    {
        budgetMax = Mathf.Max(1, linecastsPerFrame);
        budget = budgetMax;
    }

    public static int BudgetLeft
    {
        get { return budget; }
    }

    static void UpdatePlanes(Camera cam)
    {
        if (planeFrame == Time.frameCount && planeCam == cam) return;
        GeometryUtility.CalculateFrustumPlanes(cam, planes);
        planeFrame = Time.frameCount;
        planeCam = cam;
    }

    public static bool InFrustum(Camera cam, Bounds bounds)
    {
        if (cam == null) return false;
        UpdatePlanes(cam);
        return GeometryUtility.TestPlanesAABB(planes, bounds);
    }

    public static bool IsVisible(Camera cam, Bounds bounds, Transform ownerRoot, Vector3[] samplePoints)
    {
        if (cam == null) return false;
        if (!InFrustum(cam, bounds)) return false;
        int n = samplePoints == null ? 0 : Mathf.Min(3, samplePoints.Length);
        if (n == 0) return true;
        Vector3 camPos = cam.transform.position;
        for (int i = 0; i < n; i++)
        {
            if (budget <= 0) return true;
            budget--;
            if (!Occluded(camPos, samplePoints[i], ownerRoot)) return true;
        }
        return false;
    }

    public static bool CanSeeSpawn(Vector3 point, float radius)
    {
        NpcDirector d = NpcDirector.Instance;
        Camera cam = d != null ? d.PlayerCamera : null;
        if (cam == null) return false;
        Bounds b = new Bounds(point + Vector3.up * 0.9f, new Vector3(radius * 2f, 1.8f, radius * 2f));
        if (!InFrustum(cam, b)) return false;
        if (budget <= 0) return true;
        budget--;
        return !Occluded(cam.transform.position, point + Vector3.up, null);
    }
}