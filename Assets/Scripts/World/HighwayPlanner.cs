using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HighwaySettings
{
    public float gridStep = 16f;
    public float gradeWeight = 40f;
    [Range(0.01f, 0.5f)] public float maxGrade = 0.1f;
    public float steepPenalty = 300f;
    [Range(0f, 1f)] public float wobble = 0.35f;
    public float wobbleFrequency = 0.012f;
    public float borderMargin = 40f;
    [Range(0, 5)] public int chaikinIterations = 3;
    [Range(0, 30)] public int relaxIterations = 12;
    public float sampleSpacing = 8f;
    [Range(0.3f, 1f)] public float endInset = 0.85f;
}

public class HighwayPath
{
    public readonly List<Vector2> points;
    public readonly float[] cumulative;
    public readonly float length;

    public HighwayPath(List<Vector2> pts)
    {
        points = pts;
        cumulative = new float[pts.Count];
        for (int i = 1; i < pts.Count; i++) cumulative[i] = cumulative[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);
        length = pts.Count > 0 ? cumulative[pts.Count - 1] : 0f;
    }

    public Vector2 Start
    {
        get { return points[0]; }
    }

    public Vector2 End
    {
        get { return points[points.Count - 1]; }
    }

    int SegmentIndex(float distance)
    {
        distance = Mathf.Clamp(distance, 0f, length);
        int lo = 0;
        int hi = points.Count - 2;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (cumulative[mid] <= distance) lo = mid;
            else hi = mid - 1;
        }
        return Mathf.Max(0, lo);
    }

    public Vector2 PointAt(float distance)
    {
        if (points.Count < 2) return points[0];
        distance = Mathf.Clamp(distance, 0f, length);
        int i = SegmentIndex(distance);
        float seg = cumulative[i + 1] - cumulative[i];
        float t = seg > 0f ? (distance - cumulative[i]) / seg : 0f;
        return Vector2.Lerp(points[i], points[i + 1], t);
    }

    public Vector2 TangentAt(float distance)
    {
        if (points.Count < 2) return Vector2.right;
        int i = SegmentIndex(distance);
        Vector2 d = points[i + 1] - points[i];
        return d.sqrMagnitude > 0f ? d.normalized : Vector2.right;
    }

    public float DistanceTo(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < points.Count - 1; i++)
        {
            float d = SegmentDistance(p, points[i], points[i + 1]);
            if (d < best) best = d;
        }
        return best;
    }

    public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }
}

public static class HighwayPlanner
{
    public static HighwayPath Plan(WorldContext c, Vector2 start, Vector2 end, HighwaySettings s, float mountainHeight, WorldRng rng)
    {
        float half = c.Size * 0.5f;
        float step = Mathf.Max(4f, s.gridStep);
        int n = Mathf.CeilToInt(c.Size / step) + 1;
        Vector2 wobOff = new Vector2(rng.Range(2000f, 9000f), rng.Range(2000f, 9000f));
        float limit = half - s.borderMargin;

        float[] hm = new float[n * n];
        float[] wob = new float[n * n];
        bool[] blocked = new bool[n * n];
        for (int j = 0; j < n; j++)
        {
            float z = Mathf.Min(-half + j * step, half);
            for (int i = 0; i < n; i++)
            {
                float x = Mathf.Min(-half + i * step, half);
                int id = j * n + i;
                hm[id] = c.HeightAt(x, z);
                wob[id] = NoiseLib.Fbm(x * s.wobbleFrequency + wobOff.x, z * s.wobbleFrequency + wobOff.y, 3, 2f, 0.5f);
                blocked[id] = hm[id] / c.settings.maxTerrainHeight > mountainHeight || Mathf.Abs(x) > limit || Mathf.Abs(z) > limit;
            }
        }

        int si = Mathf.Clamp(Mathf.RoundToInt((start.x + half) / step), 0, n - 1);
        int sj = Mathf.Clamp(Mathf.RoundToInt((start.y + half) / step), 0, n - 1);
        int ei = Mathf.Clamp(Mathf.RoundToInt((end.x + half) / step), 0, n - 1);
        int ej = Mathf.Clamp(Mathf.RoundToInt((end.y + half) / step), 0, n - 1);
        int startId = sj * n + si;
        int endId = ej * n + ei;
        blocked[startId] = false;
        blocked[endId] = false;

        float[] g = new float[n * n];
        int[] parent = new int[n * n];
        bool[] closed = new bool[n * n];
        bool[] inOpen = new bool[n * n];
        for (int i = 0; i < g.Length; i++)
        {
            g[i] = float.MaxValue;
            parent[i] = -1;
        }
        List<int> open = new List<int>();
        List<float> openF = new List<float>();
        g[startId] = 0f;
        open.Add(startId);
        openF.Add(0f);
        inOpen[startId] = true;
        bool found = false;

        while (open.Count > 0)
        {
            int bi = 0;
            float bf = openF[0];
            for (int k = 1; k < open.Count; k++)
            {
                if (openF[k] < bf)
                {
                    bf = openF[k];
                    bi = k;
                }
            }
            int cur = open[bi];
            int last = open.Count - 1;
            open[bi] = open[last];
            openF[bi] = openF[last];
            open.RemoveAt(last);
            openF.RemoveAt(last);
            inOpen[cur] = false;
            if (cur == endId)
            {
                found = true;
                break;
            }
            closed[cur] = true;
            int cx = cur % n;
            int cz = cur / n;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = cx + dx;
                    int nz = cz + dz;
                    if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                    int nid = nz * n + nx;
                    if (closed[nid] || blocked[nid]) continue;
                    float len = step * ((dx != 0 && dz != 0) ? 1.4142135f : 1f);
                    float grade = Mathf.Abs(hm[nid] - hm[cur]) / len;
                    float mult = 1f + s.gradeWeight * grade + (grade > s.maxGrade ? s.steepPenalty * (grade - s.maxGrade) : 0f) + s.wobble * (wob[nid] + wob[cur]);
                    float ng = g[cur] + len * mult;
                    if (ng >= g[nid]) continue;
                    g[nid] = ng;
                    parent[nid] = cur;
                    float h = Vector2.Distance(new Vector2(nx, nz), new Vector2(ei, ej)) * step;
                    if (inOpen[nid])
                    {
                        int idx = open.IndexOf(nid);
                        openF[idx] = ng + h;
                    }
                    else
                    {
                        open.Add(nid);
                        openF.Add(ng + h);
                        inOpen[nid] = true;
                    }
                }
            }
        }

        List<Vector2> pts = new List<Vector2>();
        if (!found)
        {
            Debug.LogWarning("HighwayPlanner: no path found, using a straight line. Lower mountain height limit or border margin.");
            pts.Add(start);
            pts.Add(end);
        }
        else
        {
            int id = endId;
            while (id != -1)
            {
                pts.Add(new Vector2(Mathf.Min(-half + (id % n) * step, half), Mathf.Min(-half + (id / n) * step, half)));
                id = parent[id];
            }
            pts.Reverse();
            pts[0] = start;
            pts[pts.Count - 1] = end;
        }

        for (int it = 0; it < s.chaikinIterations; it++) pts = Chaikin(pts);
        pts = Resample(pts, Mathf.Max(2f, s.sampleSpacing));
        Relax(pts, s.relaxIterations);
        return new HighwayPath(pts);
    }

    static List<Vector2> Chaikin(List<Vector2> p)
    {
        if (p.Count < 3) return p;
        List<Vector2> r = new List<Vector2>(p.Count * 2);
        r.Add(p[0]);
        for (int i = 0; i < p.Count - 1; i++)
        {
            r.Add(Vector2.Lerp(p[i], p[i + 1], 0.25f));
            r.Add(Vector2.Lerp(p[i], p[i + 1], 0.75f));
        }
        r.Add(p[p.Count - 1]);
        return r;
    }

    static List<Vector2> Resample(List<Vector2> p, float spacing)
    {
        List<Vector2> r = new List<Vector2>();
        r.Add(p[0]);
        float carry = 0f;
        for (int i = 0; i < p.Count - 1; i++)
        {
            Vector2 a = p[i];
            Vector2 b = p[i + 1];
            float seg = Vector2.Distance(a, b);
            if (seg <= 0f) continue;
            float pos = spacing - carry;
            while (pos <= seg)
            {
                r.Add(Vector2.Lerp(a, b, pos / seg));
                pos += spacing;
            }
            carry = seg - (pos - spacing);
        }
        Vector2 endPt = p[p.Count - 1];
        if (Vector2.Distance(r[r.Count - 1], endPt) < spacing * 0.5f && r.Count > 1) r[r.Count - 1] = endPt;
        else r.Add(endPt);
        return r;
    }

    static void Relax(List<Vector2> p, int iterations)
    {
        for (int it = 0; it < iterations; it++)
            for (int i = 1; i < p.Count - 1; i++)
                p[i] = (p[i - 1] + p[i] * 2f + p[i + 1]) * 0.25f;
    }
}
