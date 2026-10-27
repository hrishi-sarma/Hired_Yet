using System.Collections.Generic;
using UnityEngine;

public static class RoadDetailBuilder
{
    static Material mat(Color c, string role) { return StylizedMaterialFactory.GetWorldPart(role, c); }
    static readonly Color White = new Color(0.94f, 0.94f, 0.89f);
    static readonly Color Yellow = new Color(0.95f, 0.72f, 0.12f);
    static readonly Color Dark = new Color(0.12f, 0.14f, 0.15f);
    static readonly Color Red = new Color(0.85f, 0.08f, 0.06f);
    static readonly Color Green = new Color(0.08f, 0.68f, 0.22f);
    const float MarkingY = 0.045f;

    static GameObject Box(WorldContext c, string name, Vector3 pos, Vector3 scale, float yaw, Color color, Transform parent = null)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent != null ? parent : c.LayerRoot(PlacementLayer.Prop), false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = scale;
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat(color, name);
        Collider col = go.GetComponent<Collider>();
        if (col != null) WorldUtil.Kill(col);
        go.isStatic = true;
        return go;
    }

    /// <summary>Thin marking laid on a sloped surface: ends at (a, y0) and (b, y1), tilted to the slope.</summary>
    static void SlopedStripe(WorldContext c, Vector2 a, Vector2 b, float y0, float y1, float width, Color color, string name)
    {
        Vector3 pa = new Vector3(a.x, y0 + 0.055f, a.y), pb = new Vector3(b.x, y1 + 0.055f, b.y);
        Vector3 d = pb - pa;
        if (d.sqrMagnitude < 0.0001f) return;
        GameObject go = Box(c, name, (pa + pb) * 0.5f, new Vector3(width, 0.025f, d.magnitude), 0f, color);
        go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
    }

    static void Stripe(WorldContext c, Vector2 center, float y, float yaw, float length, float width, Color color, string name)
    { Box(c, name, new Vector3(center.x, y, center.y), new Vector3(width, 0.025f, length), yaw, color); }

    public static void BuildLocalRoadDetails(WorldContext c, RoadNetwork net, DistrictSpec suburb, DistrictSpec city, WorldRng rng)
    {
        if (net == null) return;
        int laneMarks = 0, signals = 0, signs = 0;
        for (int i = 0; i < net.lines.Count; i++)
        {
            RoadLine l = net.lines[i];
            Vector2 delta = l.b - l.a;
            float len = delta.magnitude;
            if (len < 9f) continue;
            bool runsX = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
            Vector2 dir = delta / len;
            float yaw = runsX ? 90f : 0f;
            float clear = Mathf.Min(Mathf.Max(5.5f, l.width * 0.65f), len * 0.40f);
            float usable = len - clear * 2f;
            float dashSpacing = 7f;
            int dashCount = Mathf.Max(0, Mathf.FloorToInt(usable / dashSpacing));
            for (int d = 0; d < dashCount; d++)
            {
                float along = clear + (d + 0.5f) * usable / dashCount;
                Vector2 p = l.a + dir * along;
                float dashLength = Mathf.Min(2.4f, usable / dashCount * 0.55f);
                Stripe(c, p, l.roadTop + MarkingY, yaw, dashLength, 0.12f, White, "LaneMarking_Dashed");
                laneMarks++;
            }


        }

        for (int i = 0; i < net.blocks.Count; i++)
        {
            BlockInfo b = net.blocks[i];
            float y = b.sidewalkTop;
            float off = b.lotRect.width * 0.5f + b.sidewalkWidth * 0.6f;
            if (i % 2 == 0)
            {
                Vector2 p = new Vector2(b.center.x - off, b.center.y - off);
                signals++;
            }
            if (i % 3 == 0)
            {
                Vector2 p = new Vector2(b.center.x + off, b.center.y + off);
                Box(c, "StreetSign_Pole", new Vector3(p.x, y + 1.35f, p.y), new Vector3(0.09f, 2.7f, 0.09f), 0f, Dark);
                Box(c, "StreetSign_Blue", new Vector3(p.x, y + 2.45f, p.y), new Vector3(0.95f, 0.38f, 0.08f), 0f, new Color(0.08f, 0.3f, 0.72f));
                signs++;
            }
        }
        Debug.Log("Road details: " + laneMarks + " lane-split dashes, " + signals + " traffic signals, " + signs + " street signs. Zebra crossings are disabled.");
    }

    static void AddWallCollider(Transform parent, Vector3 bottom, float height, float thickness, float length, float yaw)
    {
        GameObject go = new GameObject("RailCollider");
        go.transform.SetParent(parent, false);
        go.transform.position = bottom;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.size = new Vector3(thickness, height, length);
        bc.center = new Vector3(0f, height * 0.5f, 0f);
        go.isStatic = true;
    }

    public static void BuildHighwayDetails(WorldContext c, HighwayPath path, float[] heights, HighwaySpec s)
    {
        if (path == null || heights == null || path.points.Count < 2) return;
        int dashes = 0, barrierSegments = 0, signs = 0;
        List<Vector2> pts = path.points;
        float half = s.width * 0.5f;
        float dashSpacing = 7f;
        float lampSpacing = 36f;
        float lampCarry = 0f;
        int lampIndex = 0;
        float carry = 0f;
        float railOffset = half + s.shoulder * 0.65f;
        Vector2[] miter = new Vector2[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 dPrev = i > 0 ? (pts[i] - pts[i - 1]).normalized : (pts[1] - pts[0]).normalized;
            Vector2 dNext = i < pts.Count - 1 ? (pts[i + 1] - pts[i]).normalized : dPrev;
            if (dPrev.sqrMagnitude < 0.0001f) dPrev = dNext;
            if (dNext.sqrMagnitude < 0.0001f) dNext = dPrev;
            Vector2 nPrev = new Vector2(dPrev.y, -dPrev.x);
            Vector2 nNext = new Vector2(dNext.y, -dNext.x);
            Vector2 m = nPrev + nNext;
            if (m.sqrMagnitude < 0.0001f) m = nNext;
            m.Normalize();
            float k = Mathf.Clamp(1f / Mathf.Max(0.5f, Vector2.Dot(m, nNext)), 1f, 2f);
            miter[i] = m * k;
        }
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 a = pts[i], b = pts[i + 1], delta = b - a;
            float len = delta.magnitude;
            if (len < 0.1f) continue;
            Vector2 dir = delta / len;
            Vector2 side = new Vector2(dir.y, -dir.x);
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

            float cursor = dashSpacing - carry;
            while (cursor < len)
            {
                // the dash follows the road slope: its two ends are placed on the road surface (the old flat dash poked out on elevated / sloped sections)
                float dashHalf = Mathf.Min(3.0f, dashSpacing * 0.43f) * 0.5f;
                float d0 = Mathf.Clamp(cursor - dashHalf, 0f, len), d1 = Mathf.Clamp(cursor + dashHalf, 0f, len);
                Vector2 p0 = a + dir * d0, p1 = a + dir * d1;
                float y0 = Mathf.Lerp(heights[i], heights[i + 1], d0 / len), y1 = Mathf.Lerp(heights[i], heights[i + 1], d1 / len);
                SlopedStripe(c, p0 + side * (s.width * 0.16f), p1 + side * (s.width * 0.16f), y0, y1, 0.13f, White, "Highway_LaneMarking_Dash");
                SlopedStripe(c, p0 - side * (s.width * 0.16f), p1 - side * (s.width * 0.16f), y0, y1, 0.13f, White, "Highway_LaneMarking_Dash");
                dashes++; cursor += dashSpacing;
            }
            carry = (carry + len) % dashSpacing;

            const float maxRailPiece = 1.25f;
            int pieces = Mathf.Max(1, Mathf.CeilToInt(len / maxRailPiece));
            for (int sign = -1; sign <= 1; sign += 2)
            {
                for (int piece = 0; piece < pieces; piece++)
                {
                    float t0 = (float)piece / pieces;
                    float t1 = (float)(piece + 1) / pieces;
                    Vector2 railA = a + miter[i] * sign * railOffset;
                    Vector2 railB = b + miter[i + 1] * sign * railOffset;
                    Vector2 qa = Vector2.Lerp(railA, railB, t0);
                    Vector2 qb = Vector2.Lerp(railA, railB, t1);
                    float pieceLen = (qb - qa).magnitude;
                    if (pieceLen < 0.02f) continue;
                    float ya = Mathf.Lerp(heights[i], heights[i + 1], t0);
                    float yb = Mathf.Lerp(heights[i], heights[i + 1], t1);
                    Vector2 qm = (qa + qb) * 0.5f;
                    float ym = (ya + yb) * 0.5f;
                    float railYaw = Mathf.Atan2((qb - qa).x, (qb - qa).y) * Mathf.Rad2Deg;
                    Box(c, "HighwayBarrier_Rail", new Vector3(qm.x, ym + 0.72f, qm.y), new Vector3(0.22f, 0.18f, pieceLen + 0.14f), railYaw, new Color(0.66f, 0.68f, 0.69f));
                    if (piece == 0 || piece == pieces - 1 || piece % Mathf.Max(1, Mathf.RoundToInt(2.5f / maxRailPiece)) == 0)
                        Box(c, "HighwayBarrier_Post", new Vector3(qa.x, ya + 0.45f, qa.y), new Vector3(0.12f, 0.9f, 0.12f), railYaw, new Color(0.58f, 0.6f, 0.61f));
                    if (piece % 3 == 0)
                        Box(c, "HighwayBarrier_Reflector", new Vector3(qm.x, ym + 0.84f, qm.y), new Vector3(0.07f, 0.09f, 0.08f), railYaw, Yellow);
                    barrierSegments++;
                }
            }
            float lampCursor = lampSpacing - lampCarry;
            while (lampCursor < len)
            {
                Vector2 lp = a + dir * lampCursor;
                float lt = lampCursor / len;
                float ly = Mathf.Lerp(heights[i], heights[i + 1], lt);
                float ls = (lampIndex % 2 == 0 ? 1f : -1f);
                Vector2 pole = lp + side * ls * (railOffset + 1f);
                Vector2 toRoad = -side * ls;
                float armYaw = Mathf.Atan2(toRoad.x, toRoad.y) * Mathf.Rad2Deg;
                Color metal = new Color(0.22f, 0.24f, 0.27f);
                Box(c, "HighwayLamp_Pole", new Vector3(pole.x, ly + 3.75f, pole.y), new Vector3(0.2f, 7.5f, 0.2f), armYaw, metal);
                Vector2 armMid = pole + toRoad * 1.2f;
                Box(c, "HighwayLamp_Arm", new Vector3(armMid.x, ly + 7.45f, armMid.y), new Vector3(0.12f, 0.12f, 2.5f), armYaw, metal);
                Vector2 headP = pole + toRoad * 2.3f;
                GameObject head = Box(c, "HighwayLamp_Head", new Vector3(headP.x, ly + 7.35f, headP.y), new Vector3(0.55f, 0.2f, 0.95f), armYaw, Color.white);
                head.GetComponent<Renderer>().sharedMaterial = StylizedMaterialFactory.GetLampGlow();
                StreetLampLights.Register(new Vector3(headP.x, ly + 7.2f, headP.y), 16f);
                lampIndex++;
                lampCursor += lampSpacing;
            }
            lampCarry = (lampCarry + len) % lampSpacing;

            if (i % 12 == 0)
            {
                Vector2 q = (a + b) * 0.5f + side * (half + s.shoulder + 2.2f);
                float y = (heights[i] + heights[i + 1]) * 0.5f;
                Box(c, "HighwaySign_Post", new Vector3(q.x, y + 1.6f, q.y), new Vector3(0.12f, 3.2f, 0.12f), yaw, Dark);
                Box(c, "HighwaySign_Green", new Vector3(q.x, y + 2.8f, q.y), new Vector3(1.8f, 0.9f, 0.12f), yaw, new Color(0.04f, 0.35f, 0.16f));
                signs++;
            }
        }
        for (int sign = -1; sign <= 1; sign += 2)
        {
            int last = pts.Count - 1;
            Vector2 d = (pts[last] - pts[last - 1]).normalized;
            Vector2 side = new Vector2(d.y, -d.x);
            Vector2 q = pts[last] + side * sign * (half + s.shoulder * 0.65f);
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            Box(c, "HighwayBarrier_Post_End", new Vector3(q.x, heights[last] + 0.45f, q.y), new Vector3(0.12f, 0.9f, 0.12f), yaw, new Color(0.58f, 0.6f, 0.61f));
        }
        Debug.Log("Highway dressing: " + dashes + " lane dash pairs, " + barrierSegments + " connected rail segments, " + signs + " signs.");
    }

    public static void BuildDistrictPerimeterRails(WorldContext c)
    {
        if (c == null || c.zoneMap == null) return;
        BuildOnePerimeter(c, c.zoneMap.suburbCenter, c.zoneMap.suburbRadius, ZoneType.Suburb,
            "SuburbPerimeterFence", new Color(0.91f, 0.90f, 0.82f));
    }

    static void BuildOnePerimeter(WorldContext c, Vector2 center, float nominalRadius, ZoneType zone, string rootName, Color color)
    {
        float sampleSpacing = 2.6f;
        int samples = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * nominalRadius / sampleSpacing), 180, 520);
        List<Vector2> path = new List<Vector2>(samples);
        float[] radii = new float[samples];
        Vector2[] dirs = new Vector2[samples];
        for (int i = 0; i < samples; i++)
        {
            float angle = (i / (float)samples) * Mathf.PI * 2f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float lastInside = nominalRadius * 0.55f;
            bool seenZone = false;
            float start = Mathf.Max(10f, nominalRadius - 42f);
            float end = nominalRadius + 42f;
            for (float r = start; r <= end; r += 1.5f)
            {
                Vector2 q = center + dir * r;
                bool inside = c.zoneMap.ZoneAt(q) == zone;
                if (inside) { lastInside = r; seenZone = true; }
                else if (seenZone && r > nominalRadius * 0.58f) break;
            }
            float boundaryRadius = Mathf.Max(nominalRadius * 0.52f, lastInside - 1.2f);
            radii[i] = boundaryRadius;
            dirs[i] = dir;
        }
        // Clean the radius ring so neighbouring samples never jump (jumps made broken / over-long fence pieces).
        float[] med = new float[samples];
        float[] win = new float[7];
        for (int i = 0; i < samples; i++)
        {
            for (int k = 0; k < 7; k++) win[k] = radii[((i + k - 3) % samples + samples) % samples];
            System.Array.Sort(win);
            med[i] = win[3];
        }
        radii = med;
        const float maxStep = 1.6f;
        for (int pass = 0; pass < 4; pass++)
        {
            for (int i = 0; i < samples; i++) radii[i] = Mathf.Min(radii[i], radii[(i + samples - 1) % samples] + maxStep);
            for (int i = samples - 1; i >= 0; i--) radii[i] = Mathf.Min(radii[i], radii[(i + 1) % samples] + maxStep);
        }
        for (int i = 0; i < samples; i++) path.Add(center + dirs[i] * radii[i]);

        GameObject rootObject = new GameObject(rootName);
        Transform root = rootObject.transform;
        root.SetParent(c.LayerRoot(PlacementLayer.Prop), false);
        Material railMat = StylizedMaterialFactory.Get("paintedMetal", color, zone == ZoneType.Suburb ? 1 : 2);
        Material postMat = railMat;
        Transform fenceColliders = new GameObject(rootName + "_Colliders").transform;
        fenceColliders.SetParent(root.parent, false);
        for (int i = 0; i < path.Count; i++)
        {
            Vector2 a = path[i];
            Vector2 b = path[(i + 1) % path.Count];
            Vector2 delta = b - a;
            float len = delta.magnitude;
            if (len < 0.1f) continue;
            if (len > 4.5f) continue;                                  // safety: never emit an elongated piece
            Vector2 mid = (a + b) * 0.5f;
            if (IsDistrictRoadOpening(c, mid) || IsDistrictRoadOpening(c, a) || IsDistrictRoadOpening(c, b)
                || IsDistrictRoadOpening(c, a + delta * 0.25f) || IsDistrictRoadOpening(c, a + delta * 0.75f)) continue;
            float ya = c.HeightAt(a.x, a.y);
            float yb = c.HeightAt(b.x, b.y);
            float ym = (ya + yb) * 0.5f;
            Vector3 d3 = new Vector3(delta.x, yb - ya, delta.y);        // follows the slope
            float len3 = d3.magnitude;
            Quaternion rot = Quaternion.LookRotation(d3.normalized, Vector3.up);
            Vector3 m3 = new Vector3(mid.x, ym, mid.y);
            BoxRot(root, rootName + "_Upper", m3 + Vector3.up * 1.02f, new Vector3(0.10f, 0.12f, len3 + 0.10f), rot, railMat);
            BoxRot(root, rootName + "_Lower", m3 + Vector3.up * 0.52f, new Vector3(0.10f, 0.12f, len3 + 0.10f), rot, railMat);
            if (zone == ZoneType.City)
                BoxRot(root, rootName + "_Mid", m3 + Vector3.up * 0.77f, new Vector3(0.08f, 0.07f, len3 + 0.10f), rot, railMat);
            float yaw = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            AddWallCollider(fenceColliders, new Vector3(mid.x, Mathf.Min(ya, yb), mid.y), 1.12f + Mathf.Abs(yb - ya), 0.14f, len + 0.1f, yaw); // fence height on purpose: the player can jump/hop over it into the forest
            BoxRot(root, rootName + "_Post", new Vector3(a.x, ya + 0.55f, a.y), new Vector3(0.13f, 1.12f, 0.13f), Quaternion.Euler(0f, yaw, 0f), postMat);
            BoxRot(root, rootName + "_Post", new Vector3(b.x, yb + 0.55f, b.y), new Vector3(0.13f, 1.12f, 0.13f), Quaternion.Euler(0f, yaw, 0f), postMat);
        }
        CombineRailMeshes(root);
    }

    static bool IsDistrictRoadOpening(WorldContext c, Vector2 point)
    {
        if (c == null) return false;
        if (c.zoneMap != null && c.zoneMap.highway != null && c.zoneMap.highway.DistanceTo(point) < 20f)
            return true;
        if (c.roads != null)
        {
            for (int i = 0; i < c.roads.lines.Count; i++)
            {
                RoadLine line = c.roads.lines[i];
                float clear = Mathf.Max(3.5f, line.width * 0.5f + 1.8f);
                if (HighwayPath.SegmentDistance(point, line.a, line.b) < clear) return true;
            }
        }
        return false;
    }

    static void BoxRot(Transform parent, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = material;
        Collider col = go.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) WorldUtil.Kill(col);
            else Object.DestroyImmediate(col);
        }
        go.isStatic = true;
    }

    static void BoxWithMaterial(Transform parent, string name, Vector3 pos, Vector3 scale, float yaw, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = scale;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = material;
        Collider col = go.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) WorldUtil.Kill(col);
            else Object.DestroyImmediate(col);
        }
        go.isStatic = true;
    }

    static void CombineRailMeshes(Transform root)
    {
        if (root == null) return;
        Dictionary<Material, List<CombineInstance>> groups = new Dictionary<Material, List<CombineInstance>>();
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter f = filters[i];
            if (f == null || f.sharedMesh == null) continue;
            Renderer r = f.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null) continue;
            List<CombineInstance> list;
            if (!groups.TryGetValue(r.sharedMaterial, out list)) { list = new List<CombineInstance>(); groups.Add(r.sharedMaterial, list); }
            CombineInstance ci = new CombineInstance();
            ci.mesh = f.sharedMesh;
            ci.transform = root.worldToLocalMatrix * f.transform.localToWorldMatrix;
            list.Add(ci);
        }
        int batch = 0;
        foreach (KeyValuePair<Material, List<CombineInstance>> pair in groups)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Combined_" + root.name + "_" + batch;
            int vertices = 0;
            for (int i = 0; i < pair.Value.Count; i++) vertices += pair.Value[i].mesh.vertexCount;
            if (vertices > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.CombineMeshes(pair.Value.ToArray(), true, true, false);
            GameObject combined = new GameObject("CombinedRailBatch_" + batch++);
            combined.transform.SetParent(root, false);
            MeshFilter mf = combined.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            MeshRenderer mr = combined.AddComponent<MeshRenderer>(); mr.sharedMaterial = pair.Key;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child.name.StartsWith("CombinedRailBatch_")) continue;
            if (Application.isPlaying) WorldUtil.Kill(child.gameObject); else Object.DestroyImmediate(child.gameObject);
        }
    }

}