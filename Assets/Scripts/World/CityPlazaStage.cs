using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Procedural World/Stages/4b City Plaza")]
public class CityPlazaStage : WorldStage
{
    public GameObject customModel;

    public override int Order
    {
        get { return StageOrder.Lots + 5; }
    }

    public override string StageName
    {
        get { return "City Plaza"; }
    }

    static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) WorldUtil.Kill(o);
        else Object.DestroyImmediate(o);
    }

    static GameObject Disc(Transform parent, string name, Vector3 pos, float r, Material m)
    {
        int seg = 36;
        Vector3[] v = new Vector3[seg + 1];
        Vector3[] n = new Vector3[seg + 1];
        Vector2[] uv = new Vector2[seg + 1];
        int[] t = new int[seg * 3];
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            v[i + 1] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            uv[i + 1] = new Vector2(v[i + 1].x, v[i + 1].z) / 6f;
            t[i * 3] = 0;
            t[i * 3 + 1] = (i + 1) % seg + 1;
            t[i * 3 + 2] = i + 1;
        }
        for (int i = 0; i <= seg; i++) n[i] = Vector3.up;
        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.vertices = v;
        mesh.normals = n;
        mesh.uv = uv;
        mesh.triangles = t;
        mesh.RecalculateBounds();
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }


    static GameObject Prim(PrimitiveType t, Transform parent, string name, Vector3 pos, Vector3 scale, Material m, bool collide, float yaw)
    {
        GameObject g = GameObject.CreatePrimitive(t);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collide)
        {
            Collider col = g.GetComponent<Collider>();
            if (col != null) Kill(col);
        }
        else if (t == PrimitiveType.Cylinder)
        {
            // Unity gives cylinders a CapsuleCollider, which bulges far beyond a flat basin or bowl.
            // Swap it for a convex collider built from the real cylinder mesh.
            Collider cap = g.GetComponent<Collider>();
            if (cap != null) { cap.enabled = false; Kill(cap); }
            MeshCollider mc = g.AddComponent<MeshCollider>();
            mc.sharedMesh = g.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;
        }
        return g;
    }

    static void Bench(Transform root, Vector3 pos, float yaw, Material wood, Material metal)
    {
        Transform b = new GameObject("Bench").transform;
        b.SetParent(root, false);
        b.position = pos;
        b.rotation = Quaternion.Euler(0f, yaw, 0f);
        SitSpot.AddBench(b.gameObject, new Vector3(0f, 0.5f, 0f), 0.9f);
        Prim(PrimitiveType.Cube, b, "Seat", pos + b.up * 0.45f, new Vector3(1.8f, 0.1f, 0.5f), wood, true, yaw);
        Vector3 back = b.rotation * new Vector3(0f, 0.8f, -0.22f);
        Prim(PrimitiveType.Cube, b, "Back", pos + back, new Vector3(1.8f, 0.45f, 0.08f), wood, true, yaw);
        for (int sd = -1; sd <= 1; sd += 2)
            Prim(PrimitiveType.Cube, b, "Leg", pos + b.rotation * new Vector3(sd * 0.8f, 0.22f, -0.05f), new Vector3(0.1f, 0.44f, 0.45f), metal, true, yaw);
    }

    static void LampPost(Transform root, Vector3 pos, Material metal, Material glow)
    {
        Transform l = new GameObject("LampPost").transform;
        l.SetParent(root, false);
        Prim(PrimitiveType.Cube, l, "Base", pos + Vector3.up * 0.3f, new Vector3(0.5f, 0.6f, 0.5f), metal, true, 0f);
        Prim(PrimitiveType.Cylinder, l, "Pole", pos + Vector3.up * 2.4f, new Vector3(0.14f, 1.8f, 0.14f), metal, true, 0f);
        Prim(PrimitiveType.Sphere, l, "LampHead", pos + Vector3.up * 4.35f, new Vector3(0.6f, 0.6f, 0.6f), glow, false, 0f);
        Prim(PrimitiveType.Cylinder, l, "LampCap", pos + Vector3.up * 4.7f, new Vector3(0.7f, 0.05f, 0.7f), metal, false, 0f);
        StreetLampLights.Register(pos + Vector3.up * 4.35f, 11f);
    }

    static void PlazaTree(Transform root, Vector3 pos, WorldRng rng)
    {
        Transform t = new GameObject("PlazaTree").transform;
        t.SetParent(root, false);
        float th = rng.Range(2.8f, 3.6f);
        Material bark = CityMaterials.Plain("bark", new Color(0.38f, 0.26f, 0.17f), 0.05f);
        Prim(PrimitiveType.Cylinder, t, "Trunk", pos + Vector3.up * (th * 0.5f), new Vector3(0.5f, th * 0.5f, 0.5f), bark, true, 0f);
        Color[] greens = { new Color(0.2f, 0.5f, 0.2f), new Color(0.3f, 0.56f, 0.22f), new Color(0.16f, 0.42f, 0.24f) };
        Material leaf = StylizedMaterialFactory.Get("foliage", greens[rng.Range(0, greens.Length)], rng.Range(0, 20));
        float r = rng.Range(1.9f, 2.4f);
        Prim(PrimitiveType.Sphere, t, "Canopy", pos + Vector3.up * (th + r * 0.45f), new Vector3(r * 2f, r * 1.7f, r * 2f), leaf, false, 0f);
        Prim(PrimitiveType.Sphere, t, "Canopy", pos + new Vector3(r * 0.7f, th + r * 0.1f, 0f), new Vector3(r * 1.3f, r * 1.1f, r * 1.3f), leaf, false, 0f);
        Prim(PrimitiveType.Sphere, t, "Canopy", pos + new Vector3(-r * 0.6f, th + r * 0.05f, r * 0.3f), new Vector3(r * 1.2f, r * 1.0f, r * 1.2f), leaf, false, 0f);
    }

    static void Decorate(Transform root, WorldRng rng, float cx, float cz, float pavTop, float floorTop, float P)
    {
        Transform d = new GameObject("PlazaDecor").transform;
        d.SetParent(root, false);
        float half = P * 0.5f;
        Vector3 ctr = new Vector3(cx, 0f, cz);
        Material wood = CityMaterials.Plain("parkwood", new Color(0.55f, 0.36f, 0.2f), 0.1f);
        Material metal = StylizedMaterialFactory.Get("paintedMetal", new Color(0.16f, 0.2f, 0.22f), 2);
        Material glow = StylizedMaterialFactory.GetLampGlow();
        Material stone = StylizedMaterialFactory.Get("concrete", new Color(0.84f, 0.82f, 0.76f), 0);
        Material pathM = StylizedMaterialFactory.Get("concrete", new Color(0.6f, 0.52f, 0.44f), 2);
        Material water = CityMaterials.Water();
        Material hedge = StylizedMaterialFactory.Get("foliage", new Color(0.14f, 0.38f, 0.18f), 3);

        GameObject basin = Prim(PrimitiveType.Cylinder, d, "FountainBasinCollider", new Vector3(cx, floorTop + 0.4f, cz), new Vector3(12f, 0.4f, 12f), stone, true, 0f);
        Kill(basin.GetComponent<Renderer>());
        Prim(PrimitiveType.Cylinder, d, "FountainPedestal", new Vector3(cx, floorTop + 1.1f, cz), new Vector3(1.4f, 1.1f, 1.4f), stone, true, 0f);
        Prim(PrimitiveType.Cylinder, d, "FountainBowl", new Vector3(cx, floorTop + 2.1f, cz), new Vector3(3.6f, 0.15f, 3.6f), stone, true, 0f);
        Prim(PrimitiveType.Cylinder, d, "FountainBowlWater", new Vector3(cx, floorTop + 2.27f, cz), new Vector3(3.2f, 0.02f, 3.2f), water, false, 0f);
        Prim(PrimitiveType.Cylinder, d, "FountainStem", new Vector3(cx, floorTop + 2.9f, cz), new Vector3(0.5f, 0.7f, 0.5f), stone, true, 0f);
        Prim(PrimitiveType.Sphere, d, "FountainOrb", new Vector3(cx, floorTop + 3.8f, cz), new Vector3(0.9f, 0.9f, 0.9f), water, false, 0f);

        if (half > 12f)
        {
            Prim(PrimitiveType.Cube, d, "PathX", new Vector3(cx, pavTop + 0.01f, cz), new Vector3(P - 2f, 0.03f, 3.2f), pathM, false, 0f);
            Prim(PrimitiveType.Cube, d, "PathZ", new Vector3(cx, pavTop + 0.012f, cz), new Vector3(3.2f, 0.03f, P - 2f), pathM, false, 0f);
        }

        for (int k = 0; k < 8; k++)
        {
            float a = k * 45f * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(cx + Mathf.Cos(a) * 8.6f, pavTop, cz + Mathf.Sin(a) * 8.6f);
            float yaw = Mathf.Atan2(cx - pos.x, cz - pos.z) * Mathf.Rad2Deg;
            if (k % 2 == 0) continue;
            Bench(d, pos, yaw, wood, metal);
        }

        float lo = 11f, hi = half - 3f;
        if (hi - lo >= 5f)
        {
            float lw = hi - lo;
            Color[] fl = { new Color(0.95f, 0.5f, 0.65f), new Color(1f, 0.85f, 0.25f), new Color(0.9f, 0.25f, 0.2f), new Color(0.85f, 0.85f, 1f) };
            for (int qx = -1; qx <= 1; qx += 2)
                for (int qz = -1; qz <= 1; qz += 2)
                {
                    Vector3 lc = new Vector3(cx + qx * (lo + lw * 0.5f), pavTop + 0.09f, cz + qz * (lo + lw * 0.5f));
                    Material lawn = StylizedMaterialFactory.Get("foliage", new Color(0.3f, 0.55f, 0.2f), 4 + (qx + 1) + (qz + 1) / 2);
                    Prim(PrimitiveType.Cube, d, "Lawn", lc, new Vector3(lw, 0.18f, lw), lawn, true, 0f);
                    Prim(PrimitiveType.Cube, d, "LawnCurb", lc + Vector3.down * 0.0f, new Vector3(lw + 0.3f, 0.12f, lw + 0.3f), stone, false, 0f).transform.position = new Vector3(lc.x, pavTop + 0.05f, lc.z);
                    Vector3 top = new Vector3(lc.x, pavTop + 0.18f, lc.z);
                    PlazaTree(d, top, rng);
                    for (int fi = 0; fi < 12; fi++)
                    {
                        float fa = fi / 12f * Mathf.PI * 2f;
                        Material fm = Mesh_Flower(fl[rng.Range(0, fl.Length)]);
                        Prim(PrimitiveType.Sphere, d, "Flower", top + new Vector3(Mathf.Cos(fa) * 3.2f, 0.18f, Mathf.Sin(fa) * 3.2f), new Vector3(0.38f, 0.3f, 0.38f), fm, false, 0f);
                    }
                    Prim(PrimitiveType.Sphere, d, "Shrub", top + new Vector3(qx * lw * 0.3f, 0.35f, qz * lw * 0.1f), new Vector3(1.3f, 0.9f, 1.3f), hedge, true, 0f);
                    Prim(PrimitiveType.Sphere, d, "Shrub", top + new Vector3(-qx * lw * 0.1f, 0.35f, -qz * lw * 0.3f), new Vector3(1.1f, 0.8f, 1.1f), hedge, true, 0f);
                }
        }

        float e = half - 0.6f;
        float seg = half - 4f;
        if (seg > 3f)
        {
            for (int sgn = -1; sgn <= 1; sgn += 2)
                for (int hs = -1; hs <= 1; hs += 2)
                {
                    float o = hs * (2.5f + seg * 0.5f);
                    Prim(PrimitiveType.Cube, d, "Hedge", new Vector3(cx + o, pavTop + 0.45f, cz + sgn * e), new Vector3(seg, 0.9f, 0.7f), hedge, true, 0f);
                    Prim(PrimitiveType.Cube, d, "Hedge", new Vector3(cx + sgn * e, pavTop + 0.45f, cz + o), new Vector3(0.7f, 0.9f, seg), hedge, true, 0f);
                }
        }

        float lp = half - 1.6f;
        Vector3[] lamps = { new Vector3(lp, 0f, lp), new Vector3(-lp, 0f, lp), new Vector3(lp, 0f, -lp), new Vector3(-lp, 0f, -lp), new Vector3(0f, 0f, lp), new Vector3(0f, 0f, -lp), new Vector3(lp, 0f, 0f), new Vector3(-lp, 0f, 0f) };
        for (int i = 0; i < lamps.Length; i++)
            LampPost(d, new Vector3(cx + lamps[i].x, pavTop, cz + lamps[i].z), metal, glow);

        // Stage 3 polish: additive, deterministic (no RNG, so existing random results are unchanged).
        // Paving inlay ring, litter bins beside the benches, bollards and planters flanking the four entrances.
        if (half >= 19f)
        {
            Material ringM = StylizedMaterialFactory.Get("concrete", new Color(0.8f, 0.74f, 0.64f), 1);
            Material floorM = StylizedMaterialFactory.Get("concrete", new Color(0.72f, 0.7f, 0.66f), 1);
            Prim(PrimitiveType.Cylinder, d, "InlayRing", new Vector3(cx, pavTop + 0.004f, cz), new Vector3(27f, 0.006f, 27f), ringM, false, 0f);
            Prim(PrimitiveType.Cylinder, d, "InlayCore", new Vector3(cx, pavTop + 0.007f, cz), new Vector3(24f, 0.006f, 24f), floorM, false, 0f);
        }

        Material binM = StylizedMaterialFactory.Get("paintedMetal", new Color(0.16f, 0.3f, 0.24f), 2);
        for (int k = 1; k < 8; k += 2)
        {
            float a = k * 45f * Mathf.Deg2Rad;
            Vector3 bp = new Vector3(cx + Mathf.Cos(a) * 8.6f, pavTop, cz + Mathf.Sin(a) * 8.6f);
            float byaw = Mathf.Atan2(cx - bp.x, cz - bp.z) * Mathf.Rad2Deg;
            Vector3 side = Quaternion.Euler(0f, byaw, 0f) * new Vector3(1.7f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, d, "Bin", bp + side + Vector3.up * 0.4f, new Vector3(0.5f, 0.4f, 0.5f), binM, true, 0f);
        }

        if (seg > 3f)
        {
            Vector3 c0 = new Vector3(cx, pavTop, cz);
            for (int dir = 0; dir < 4; dir++)
            {
                float yaw = dir * 90f;
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    Vector3 bol = c0 + q * new Vector3(sd * 2.0f, 0f, e);
                    Prim(PrimitiveType.Cylinder, d, "Bollard", bol + Vector3.up * 0.4f, new Vector3(0.24f, 0.4f, 0.24f), metal, true, 0f);
                    Vector3 pl = c0 + q * new Vector3(sd * 3.7f, 0f, e - 1.5f);
                    Prim(PrimitiveType.Cube, d, "Planter", pl + Vector3.up * 0.3f, new Vector3(1.5f, 0.6f, 1.5f), stone, true, yaw);
                    Prim(PrimitiveType.Sphere, d, "PlanterShrub", pl + Vector3.up * 0.8f, new Vector3(1.1f, 0.8f, 1.1f), hedge, false, 0f);
                }
            }
        }
    }

    static readonly Dictionary<int, Material> flowerMats = new Dictionary<int, Material>();

    static Material Mesh_Flower(Color c)
    {
        int key = c.GetHashCode();
        Material m;
        if (flowerMats.TryGetValue(key, out m) && m != null) return m;
        m = CityMaterials.Plain("flower" + key, c, 0.2f);
        flowerMats[key] = m;
        return m;
    }

    public override void Run(WorldContext c)
    {
        if (c.roads == null || c.roads.blocks.Count == 0 || c.zoneMap == null) return;
        flowerMats.Clear();
        BlockInfo blk = null;
        float best = float.MaxValue;
        for (int i = 0; i < c.roads.blocks.Count; i++)
        {
            BlockInfo b = c.roads.blocks[i];
            if (b.zone != ZoneType.City) continue;
            float d = (b.center - c.zoneMap.cityCenter).sqrMagnitude;
            if (d < best) { best = d; blk = b; }
        }
        if (blk == null) return;

        float sum = 0f;
        int cnt = 0;
        for (int i = 0; i < c.lots.Count; i++)
        {
            LotInfo lot = c.lots[i];
            if (lot.block == blk || blk.lotRect.Contains(lot.center))
            {
                lot.empty = true;
                sum += lot.baseY;
                cnt++;
            }
        }
        float y0 = cnt > 0 ? sum / cnt : c.HeightAt(blk.center.x, blk.center.y);
        float cx = blk.center.x, cz = blk.center.y;
        float P = blk.lotRect.width - 2f;

        Transform root = new GameObject("CityPlaza").transform;
        root.SetParent(c.LayerRoot(PlacementLayer.Prop), false);
        float floorTop = y0 + 0.12f;

        if (customModel != null)
        {
            GameObject m = Instantiate(customModel, new Vector3(cx, floorTop, cz), Quaternion.identity, root);
            m.name = "PlazaModel";
        }
        else
        {
            DetailBatch batch = new DetailBatch();
            Material stone = StylizedMaterialFactory.Get("concrete", new Color(0.84f, 0.82f, 0.76f), 0);
            Material paving = StylizedMaterialFactory.Get("concrete", new Color(0.72f, 0.7f, 0.66f), 1);
            batch.Box(paving, new Vector3(cx, y0 - 0.1f, cz), 0f, new Vector3(P, 0.22f, P));
            batch.Prism(stone, new Vector3(cx, floorTop, cz), 6f, 0.8f, 24);
            batch.Flush(root, "Plaza", true);
            Disc(root, "PlazaWater", new Vector3(cx, floorTop + 0.82f, cz), 5.4f, CityMaterials.Water());
            Decorate(root, c.Rng("plaza"), cx, cz, y0 + 0.01f, floorTop, P);
        }

        c.occupancy.Add(OrientedBox.FromBase(new Vector3(cx, y0, cz), new Vector3(P + 2f, 3f, P + 2f), 0f), PlacementLayer.Building, root.gameObject);
    }
}
