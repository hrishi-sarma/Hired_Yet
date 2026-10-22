using UnityEngine;

[AddComponentMenu("Procedural World/Stages/8 Boundary")]
public class BoundaryStage : WorldStage
{
    [Range(24, 360)] public int steps = 96;
    [Range(0.05f, 0.95f)] public float wallLevel = 0.35f;
    public float rayStep = 2f;
    public float thickness = 3f;
    public float extraHeight = 40f;
    public float edgeMargin = 4f;
    public bool showGizmo = true;

    [System.NonSerialized] Vector3[] gizmoPoints;

    public override int Order
    {
        get { return StageOrder.Boundary; }
    }

    public override string StageName
    {
        get { return "Boundary"; }
    }

    public override void Run(WorldContext c)
    {
        float half = c.Size * 0.5f - edgeMargin;
        float maxR = half * 1.4142136f;
        float maxH = c.settings.maxTerrainHeight;
        float[] r = new float[steps];
        Vector2[] dirs = new Vector2[steps];
        for (int i = 0; i < steps; i++)
        {
            float a = i * Mathf.PI * 2f / steps;
            dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float rad = maxR;
            for (float d = 0f; d < maxR; d += Mathf.Max(0.5f, rayStep))
            {
                Vector2 p = dirs[i] * d;
                if (Mathf.Abs(p.x) > half || Mathf.Abs(p.y) > half)
                {
                    rad = d;
                    break;
                }
                if (c.HeightAt(p.x, p.y) / maxH >= wallLevel)
                {
                    rad = d;
                    break;
                }
            }
            r[i] = rad;
        }

        float[] m = new float[steps];
        for (int i = 0; i < steps; i++) m[i] = Mathf.Min(r[(i + steps - 1) % steps], Mathf.Min(r[i], r[(i + 1) % steps]));
        for (int pass = 0; pass < 2; pass++)
        {
            float[] t = new float[steps];
            for (int i = 0; i < steps; i++) t[i] = (m[(i + steps - 1) % steps] + 2f * m[i] + m[(i + 1) % steps]) * 0.25f;
            m = t;
        }

        Vector2[] pts = new Vector2[steps];
        gizmoPoints = new Vector3[steps];
        for (int i = 0; i < steps; i++)
        {
            Vector2 p = dirs[i] * m[i];
            p.x = Mathf.Clamp(p.x, -half, half);
            p.y = Mathf.Clamp(p.y, -half, half);
            pts[i] = p;
            gizmoPoints[i] = new Vector3(p.x, c.HeightAt(p.x, p.y) + 2f, p.y);
        }

        GameObject root = new GameObject("Boundary");
        root.transform.SetParent(c.root, false);
        float height = maxH + extraHeight * 2f;
        for (int i = 0; i < steps; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[(i + 1) % steps];
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) continue;
            GameObject seg = new GameObject("Wall");
            seg.transform.SetParent(root.transform, false);
            seg.transform.SetPositionAndRotation(new Vector3((a.x + b.x) * 0.5f, maxH * 0.5f, (a.y + b.y) * 0.5f), Quaternion.Euler(0f, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 0f));
            BoxCollider bc = seg.AddComponent<BoxCollider>();
            bc.size = new Vector3(thickness, height, len + 1f);
            seg.isStatic = true;
        }
        float minR = float.MaxValue;
        float maxRad = 0f;
        for (int i = 0; i < steps; i++)
        {
            minR = Mathf.Min(minR, m[i]);
            maxRad = Mathf.Max(maxRad, m[i]);
        }
        Debug.Log("Boundary: " + steps + " wall segments, radius " + Mathf.RoundToInt(minR) + " to " + Mathf.RoundToInt(maxRad) + " m");
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmo || gizmoPoints == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < gizmoPoints.Length; i++) Gizmos.DrawLine(gizmoPoints[i], gizmoPoints[(i + 1) % gizmoPoints.Length]);
    }
}
