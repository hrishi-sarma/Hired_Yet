using System.Collections.Generic;
using UnityEngine;

public static class HighwayRibbon
{
    static Material RoadMaterial(WorldContext c, GameObject self, HighwaySpec s)
    {
        Transform r = c.LayerRoot(PlacementLayer.Road);
        for (int i = 0; i < r.childCount; i++)
        {
            Transform ch = r.GetChild(i);
            if (ch.gameObject == self) continue;
            Renderer rd = ch.GetComponentInChildren<Renderer>();
            if (rd != null && rd.sharedMaterial != null) return rd.sharedMaterial;
        }
        return StylizedMaterialFactory.Get("asphalt", s.tint, 0);
    }

    public static void Plan(WorldContext c, RoadNetwork net, HighwaySpec s, HighwayPath raw)
    {
        float topStart;
        float topEnd;
        List<Vector2> a = Gate(net, s, new List<Vector2>(raw.points), out topStart);
        if (topStart == 0f) topStart = c.HeightAt(a[0].x, a[0].y);
        List<Vector2> rev = new List<Vector2>(a);
        rev.Reverse();
        List<Vector2> b = Gate(net, s, rev, out topEnd);
        b.Reverse();
        if (topEnd == 0f) topEnd = c.HeightAt(b[b.Count - 1].x, b[b.Count - 1].y);
        if (b.Count < 2)
        {
            Debug.LogWarning("HighwayRibbon: path too short after trimming, using the raw path.");
            b = new List<Vector2>(raw.points);
        }
        for (int it = 0; it < s.chaikinIterations; it++) b = Chaikin(b);
        b = Resample(b, Mathf.Max(2f, s.sampleSpacing));

        int n = b.Count;
        float[] hs = new float[n];
        for (int i = 0; i < n; i++) hs[i] = c.HeightAt(b[i].x, b[i].y);
        int pin = Mathf.CeilToInt(s.gateLength / Mathf.Max(2f, s.sampleSpacing)) + 1;
        for (int i = 0; i < n; i++)
        {
            if (i <= pin) hs[i] = topStart;
            else if (i >= n - 1 - pin) hs[i] = topEnd;
        }
        float[] tmp = new float[n];
        for (int it = 0; it < s.smoothPasses; it++)
        {
            for (int i = 0; i < n; i++)
            {
                bool pinned = i <= pin || i >= n - 1 - pin;
                tmp[i] = pinned ? hs[i] : (hs[i - 1] + hs[i] * 2f + hs[i + 1]) * 0.25f;
            }
            float[] sw = hs;
            hs = tmp;
            tmp = sw;
        }

        HighwayPath path = new HighwayPath(b);
        net.highway = path;
        net.highwayHeights = hs;
        Carve(c, path, hs, s);

        float maxGrade = 0f;
        for (int i = 0; i < n - 1; i++)
        {
            float len = Vector2.Distance(b[i], b[i + 1]);
            if (len > 0f) maxGrade = Mathf.Max(maxGrade, Mathf.Abs(hs[i + 1] - hs[i]) / len);
        }
        Debug.Log("Highway: " + Mathf.RoundToInt(path.length) + " m, " + n + " points, max grade " + (maxGrade * 100f).ToString("0.0") + "%");
    }

    static List<Vector2> Gate(RoadNetwork net, HighwaySpec s, List<Vector2> pts, out float top)
    {
        top = 0f;
        int idx = -1;
        for (int i = 0; i < pts.Count; i++)
        {
            if (!net.InFootprint(pts[i], s.width))
            {
                idx = i;
                break;
            }
        }
        if (idx < 0 || net.lines.Count == 0) return pts;

        Vector2 ps = pts[idx];
        float bestD = float.MaxValue;
        Vector2 bestQ = ps;
        RoadLine bestLine = net.lines[0];
        for (int i = 0; i < net.lines.Count; i++)
        {
            RoadLine l = net.lines[i];
            Vector2 ab = l.b - l.a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(ps - l.a, ab) / len2) : 0f;
            Vector2 q = l.a + ab * t;
            float d = Vector2.Distance(ps, q);
            if (d < bestD)
            {
                bestD = d;
                bestQ = q;
                bestLine = l;
            }
        }

        Vector2 dir = (bestLine.b - bestLine.a).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        if (Vector2.Dot(perp, ps - bestQ) < 0f) perp = -perp;
        Vector2 e = bestQ + perp * bestLine.width * 0.5f;
        top = bestLine.roadTop;

        List<Vector2> r = new List<Vector2>();
        r.Add(e);
        r.Add(e + perp * s.gateLength * 0.5f);
        r.Add(e + perp * s.gateLength);
        for (int j = idx; j < pts.Count; j++)
            if (Vector2.Distance(pts[j], e) > s.gateLength + s.sampleSpacing) r.Add(pts[j]);
        return r;
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

    static void Carve(WorldContext c, HighwayPath path, float[] hs, HighwaySpec s)
    {
        int res = c.Res;
        float half = c.Size * 0.5f;
        float texel = c.TexelSize;
        float flat = s.width * 0.5f + s.shoulder;
        float blend = Mathf.Max(1f, s.blend);
        float reach = flat + blend;
        float[,] best = new float[res, res];
        float[,] target = new float[res, res];
        for (int zi = 0; zi < res; zi++)
            for (int xi = 0; xi < res; xi++)
                best[zi, xi] = float.MaxValue;
        List<Vector2> pts = path.points;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[i + 1];
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach + half) / texel));
            int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach + half) / texel));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach + half) / texel));
            int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + reach + half) / texel));
            for (int zi = z0; zi <= z1; zi++)
            {
                for (int xi = x0; xi <= x1; xi++)
                {
                    Vector2 p = new Vector2(xi * texel - half, zi * texel - half);
                    float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
                    float d = Vector2.Distance(p, a + ab * t);
                    if (d < best[zi, xi])
                    {
                        best[zi, xi] = d;
                        target[zi, xi] = Mathf.Lerp(hs[i], hs[i + 1], t) - s.carveDepth;
                    }
                }
            }
        }
        for (int zi = 0; zi < res; zi++)
        {
            for (int xi = 0; xi < res; xi++)
            {
                float d = best[zi, xi];
                if (d > reach) continue;
                float w = d <= flat ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (d - flat) / blend);
                float tgt = Mathf.Clamp01(target[zi, xi] / c.settings.maxTerrainHeight);
                c.heights[zi, xi] = Mathf.Lerp(c.heights[zi, xi], tgt, w);
            }
        }
    }

    public static void Spawn(WorldContext c, RoadNetwork net, HighwaySpec s)
    {
        HighwayPath path = net.highway;
        float[] hs = net.highwayHeights;
        if (path == null || hs == null) return;
        List<Vector2> p = path.points;
        int n = p.Count;
        float hw = s.width * 0.5f;

        Vector3[] v = new Vector3[n * 2];
        Vector2[] uv = new Vector2[n * 2];
        int[] tri = new int[(n - 1) * 6];
        for (int i = 0; i < n; i++)
        {
            Vector2 t = i == 0 ? p[1] - p[0] : (i == n - 1 ? p[n - 1] - p[n - 2] : p[i + 1] - p[i - 1]);
            t = t.sqrMagnitude > 0f ? t.normalized : Vector2.up;
            Vector2 right = new Vector2(t.y, -t.x);
            Vector2 l = p[i] - right * hw;
            Vector2 r = p[i] + right * hw;
            v[i * 2] = new Vector3(l.x, hs[i], l.y);
            v[i * 2 + 1] = new Vector3(r.x, hs[i], r.y);
            float along = path.cumulative[i] / s.width;
            uv[i * 2] = new Vector2(0f, along);
            uv[i * 2 + 1] = new Vector2(1f, along);
        }
        for (int i = 0; i < n - 1; i++)
        {
            int l0 = i * 2;
            int r0 = i * 2 + 1;
            int l1 = i * 2 + 2;
            int r1 = i * 2 + 3;
            int k = i * 6;
            tri[k] = l0;
            tri[k + 1] = l1;
            tri[k + 2] = r0;
            tri[k + 3] = r0;
            tri[k + 4] = l1;
            tri[k + 5] = r1;
        }

        Mesh mesh = new Mesh();
        mesh.name = "HighwayRibbon";
        mesh.vertices = v;
        mesh.uv = uv;
        mesh.triangles = tri;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject go = new GameObject("Highway");
        go.transform.SetParent(c.LayerRoot(PlacementLayer.Road), false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer hr = go.AddComponent<MeshRenderer>();
        hr.sharedMaterial = RoadMaterial(c, go, s);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        StylizedUVMapper.Apply(hr);
        go.isStatic = true;

        int blocked = 0;
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 d = p[i + 1] - p[i];
            float len = d.magnitude;
            if (len <= 0f) continue;
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            Vector2 mid = (p[i] + p[i + 1]) * 0.5f;
            Vector3 baseCenter = new Vector3(mid.x, (hs[i] + hs[i + 1]) * 0.5f - 0.3f, mid.y);
            OrientedBox box = OrientedBox.FromBase(baseCenter, new Vector3(s.width, 0.3f, len), yaw);
            if (!c.occupancy.IsFree(box, PlacementLayer.Road, 0f)) blocked++;
            c.occupancy.Add(box, PlacementLayer.Road, go);
        }
        if (blocked > 0) Debug.LogWarning("Highway: " + blocked + " segments overlap other reserved objects.");
        RoadDetailBuilder.BuildHighwayDetails(c, path, hs, s);
    }
}
