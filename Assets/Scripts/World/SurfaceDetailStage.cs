using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Procedural World/Stages/Surface Details")]
public class SurfaceDetailStage : WorldStage
{
    public bool roadDetails = true;
    public bool roadPatches = true;
    [Range(0f, 1f)] public float patchOpacity = 0.3f;
    [Range(0f, 1f)] public float crackOpacity = 0.6f;
    public bool streetLife = true;
    public bool lawnDetails = true;
    public bool buildingWeathering = true;
    public int maxGrassTufts = 5000;

    public override int Order { get { return StageOrder.Props + 20; } }
    public override string StageName { get { return "Surface Details"; } }

    static Material zebraMat, crackMat, patchMat, puddleMat, manholeMat, dustMat, dustMatL, dustMatR, flowerMat, tuftMat, pathMat;
    static Material puddleRef;
    
    public static Material Mat(string name, Texture2D tex, Color tint, bool cutout, int queue, bool doubleSided)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (sh == null) sh = Shader.Find("Standard");
        Material m = new Material(sh);
        m.name = name;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
        if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", doubleSided ? 0f : 2f);
        if (cutout)
        {
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = 2450;
        }
        else
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", 5f);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 10f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.renderQueue = queue;
        }
        return m;
    }

    static void EnsureMaterials()
    {
        if (crackMat != null) return;
        patchMat = Mat("DetailPatch", DetailTex.Patch(), Color.white, false, 2986, false);
        zebraMat = Mat("DetailZebra", DetailTex.Zebra(), Color.white, false, 2987, false);
        dustMat = Mat("DetailDust", DetailTex.Dust(), Color.white, false, 2985, false);
        dustMatL = Mat("DetailDustL", DetailTex.Dust(1), Color.white, false, 2985, false);
        dustMatR = Mat("DetailDustR", DetailTex.Dust(2), Color.white, false, 2985, false);
        crackMat = Mat("DetailCrack", DetailTex.Crack(), Color.white, false, 2988, false);
        puddleMat = Mat("DetailPuddle", DetailTex.Puddle(), new Color(0.5f, 0.6f, 0.7f, 0.6f), false, 2989, false);
        manholeMat = Mat("DetailManhole", DetailTex.Manhole(), Color.white, false, 2989, false);
        flowerMat = Mat("DetailFlowers", DetailTex.Flowers(), Color.white, false, 2991, false);
        pathMat = Mat("DetailPath", DetailTex.Path(), Color.white, false, 2984, false);
        tuftMat = Mat("DetailGrassTuft", DetailTex.Tuft(), Color.white, true, 2450, true);
        puddleRef = puddleMat;
    }

    static void SetTint(Material m, float a)
    {
        Color c = new Color(1f, 1f, 1f, a);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    public static void TickPuddles()
    {
        if (puddleRef == null) return;
        Color sky = Color.Lerp(RenderSettings.fogColor, Color.white, 0.25f);
        float k = Mathf.Clamp01(RenderSettings.fogColor.grayscale * 1.4f + 0.12f);
        Color c = new Color(Mathf.Lerp(0.05f, sky.r, k), Mathf.Lerp(0.06f, sky.g, k), Mathf.Lerp(0.08f, sky.b, k), 0.62f);
        if (puddleRef.HasProperty("_BaseColor")) puddleRef.SetColor("_BaseColor", c);
        if (puddleRef.HasProperty("_Color")) puddleRef.SetColor("_Color", c);
    }

    static Material Solid(string role, Color col)
    {
        return StylizedMaterialFactory.Get(role, col, 0);
    }

    static Vector2 Rot(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    static void Part(DetailBatch b, Material m, Vector3 origin, float yaw, Vector3 local, Vector3 size)
    {
        float r = yaw * Mathf.Deg2Rad;
        Vector2 ax = new Vector2(Mathf.Cos(r), -Mathf.Sin(r));
        Vector2 az = new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        Vector3 pos = new Vector3(origin.x + ax.x * local.x + az.x * local.z, origin.y + local.y, origin.z + ax.y * local.x + az.y * local.z);
        b.Box(m, pos, yaw, size);
    }

    static float YawOf(Vector2 dir)
    {
        return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
    }

    public override void Run(WorldContext c)
    {
        EnsureMaterials();
        SetTint(patchMat, patchOpacity);
        SetTint(crackMat, crackOpacity);
        AmbientLifeController.Chimneys.Clear();
        WorldRng rng = c.Rng("surface");
        DetailBatch batch = new DetailBatch();
        int roadCount = 0, propCount = 0, lawnCount = 0, weatherCount = 0, tufts = 0;

        if (roadDetails && c.roads != null) roadCount = BuildRoads(c, batch, rng);
        if (streetLife && c.roads != null)
        {
            propCount = BuildStreetProps(c, batch, rng);
        }
        BuildingPass(c, batch, rng, ref lawnCount, ref weatherCount);
        if (lawnDetails) tufts = BuildFenceGrass(c, batch, rng);

        batch.Flush(c.LayerRoot(PlacementLayer.Prop), "Detail");
        if (puddleRef != null)
        {
            GameObject d = new GameObject("PuddleDriver");
            d.transform.SetParent(c.root, false);
            d.AddComponent<SurfaceDetailDriver>();
        }
        TickPuddles();
        Debug.Log("Surface details: " + roadCount + " road decals, " + propCount + " street props, " + lawnCount + " lawn details, " + tufts + " fence tufts, " + weatherCount + " weathered buildings.");
    }

    int BuildRoads(WorldContext c, DetailBatch batch, WorldRng rng)
    {
        int count = 0;
        for (int i = 0; i < c.roads.lines.Count; i++)
        {
            RoadLine l = c.roads.lines[i];
            Vector2 ab = l.b - l.a;
            float len = ab.magnitude;
            float usable = len - l.width;
            if (usable < 8f) continue;
            Vector2 u = ab / len;
            Vector2 v = new Vector2(-u.y, u.x);
            Vector2 mid = (l.a + l.b) * 0.5f;
            float y = l.roadTop;
            float hw = l.width * 0.5f;

            int cracks = rng.Range(2, 5);
            for (int k = 0; k < cracks; k++)
            {
                Vector2 p = mid + u * rng.Range(-usable * 0.5f + 1f, usable * 0.5f - 1f) + v * rng.Range(-hw + 0.8f, hw - 0.8f);
                float sz = rng.Range(2.4f, 4.6f);
                batch.Quad(crackMat, new Vector3(p.x, y + 0.055f, p.y), Rot(u, rng.Range(0f, 360f)), new Vector2(sz, sz), Vector2.one);
                count++;
            }
            bool wantPatch = rng.Chance(0.45f);
            if (roadPatches && wantPatch)
            {
                Vector2 p = mid + u * rng.Range(-usable * 0.5f + 2f, usable * 0.5f - 2f) + v * rng.Range(-hw + 1.4f, hw - 1.4f);
                batch.Quad(patchMat, new Vector3(p.x, y + 0.05f, p.y), u, new Vector2(rng.Range(2f, 4.4f), rng.Range(1.2f, 2.4f)), Vector2.one);
                count++;
            }
            if (rng.Chance(0.4f))
            {
                float side = rng.Chance(0.5f) ? 1f : -1f;
                Vector2 p = mid + u * rng.Range(-usable * 0.5f + 2f, usable * 0.5f - 2f) + v * side * (hw - rng.Range(0.7f, 1.5f));
                float ps = rng.Range(1.5f, 2.6f);
                batch.Quad(puddleMat, new Vector3(p.x, y + 0.06f, p.y), Rot(u, rng.Range(0f, 360f)), new Vector2(ps, ps), Vector2.one);
                count++;
            }
            if (rng.Chance(0.3f))
            {
                float side = rng.Chance(0.5f) ? 1f : -1f;
                Vector2 p = mid + u * rng.Range(-usable * 0.5f + 2f, usable * 0.5f - 2f) + v * side * (hw * 0.5f + rng.Range(-0.3f, 0.3f));
                batch.Quad(manholeMat, new Vector3(p.x, y + 0.065f, p.y), u, new Vector2(1.1f, 1.1f), Vector2.one);
                count++;
            }
            for (int s = 0; s < 2; s++)
            {
                Vector2 U = s == 0 ? u : -u;
                Vector2 V = new Vector2(-U.y, U.x);
                Vector2 e = mid + V * (hw - 0.55f);
                float cap = 2f;
                float main = Mathf.Max(1f, usable - cap * 2f);
                float off = usable * 0.5f - cap * 0.5f;
                Vector2 pl = e - U * off, pr = e + U * off;
                batch.Quad(dustMat, new Vector3(e.x, y + 0.045f, e.y), U, new Vector2(main, 1.1f), new Vector2(main / 1.1f, 1f));
                batch.Quad(dustMatL, new Vector3(pl.x, y + 0.045f, pl.y), U, new Vector2(cap, 1.1f), Vector2.one);
                batch.Quad(dustMatR, new Vector3(pr.x, y + 0.045f, pr.y), U, new Vector2(cap, 1.1f), Vector2.one);
                count++;
            }
        }
        count += BuildCrossings(c, batch);
        return count;
    }

    int BuildCrossings(WorldContext c, DetailBatch batch)
    {
        int count = 0;
        System.Collections.Generic.Dictionary<long, int> degree = new System.Collections.Generic.Dictionary<long, int>();
        for (int i = 0; i < c.roads.lines.Count; i++)
        {
            RoadLine l = c.roads.lines[i];
            long ka = NodeKey(l.a), kb = NodeKey(l.b);
            int n;
            degree.TryGetValue(ka, out n); degree[ka] = n + 1;
            degree.TryGetValue(kb, out n); degree[kb] = n + 1;
        }
        for (int i = 0; i < c.roads.lines.Count; i++)
        {
            RoadLine l = c.roads.lines[i];
            Vector2 ab = l.b - l.a;
            float len = ab.magnitude;
            if (len < l.width * 2f + 8f) continue;
            Vector2 u = ab / len;
            float hw = l.width * 0.5f;
            float span = Mathf.Floor(l.width - 1.5f);
            if (span < 2f) continue;
            for (int e = 0; e < 2; e++)
            {
                Vector2 node = e == 0 ? l.a : l.b;
                int deg;
                if (!degree.TryGetValue(NodeKey(node), out deg) || deg != 4) continue;
                Vector2 dir = e == 0 ? u : -u;
                Vector2 p = node + dir * (hw + 2.2f);
                batch.Quad(zebraMat, new Vector3(p.x, l.roadTop + 0.058f, p.y), dir, new Vector2(3f, span), new Vector2(1.5f, span));
                count++;
            }
        }
        return count;
    }

    static long NodeKey(Vector2 p)
    {
        return (long)Mathf.RoundToInt(p.x * 2f) * 100003L + Mathf.RoundToInt(p.y * 2f);
    }

    static readonly Vector2[] sideDir = { new Vector2(0f, -1f), new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f) };

    int BuildStreetProps(WorldContext c, DetailBatch batch, WorldRng rng)
    {
        int count = 0;
        PropStage ps = c.generator != null ? c.generator.GetComponent<PropStage>() : null;
        Material wood = Solid("wood", new Color(0.45f, 0.30f, 0.18f));
        Material metal = Solid("paintedMetal", new Color(0.18f, 0.2f, 0.22f));
        Material glass = Solid("glass", new Color(0.6f, 0.78f, 0.85f));
        Material sign = Solid("paintedMetal", new Color(0.1f, 0.35f, 0.75f));
        for (int k = 0; k < c.roads.blocks.Count; k++)
        {
            BlockInfo b = c.roads.blocks[k];
            bool city = b.zone == ZoneType.City;
            float spacing = ps != null ? (city ? ps.citySpacing : ps.suburbSpacing) : 22f;
            float len = b.lotRect.width + b.sidewalkWidth;
            int lampCount = Mathf.Max(1, Mathf.RoundToInt(len / Mathf.Max(4f, spacing)));
            float o = b.lotRect.width * 0.5f + b.sidewalkWidth * 0.5f;
            for (int s = 0; s < 4; s++)
            {
                Vector2 O = sideDir[s];
                Vector2 T = new Vector2(-O.y, O.x);
                bool bus = rng.Chance(city ? 0.12f : 0.07f);
                bool bench = !bus && rng.Chance(city ? 0.3f : 0.18f);
                if (!bus && !bench) continue;
                float t;
                if (lampCount == 1) t = (rng.Chance(0.5f) ? 1f : -1f) * len * 0.3f;
                else t = rng.Range(1, lampCount) * len / lampCount - len * 0.5f;
                Vector2 pos = b.center + O * o + T * t;
                Vector3 origin = new Vector3(pos.x, b.sidewalkTop, pos.y);
                float yaw = YawOf(O);
                if (bench)
                {
                    BenchParts(batch, wood, metal, origin, yaw, 0f);
                    // Per-bench interaction anchor; geometry stays batched for performance.
                    GameObject sitAnchor = new GameObject("BenchSitAnchor");
                    sitAnchor.transform.SetParent(transform, true);
                    sitAnchor.transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, yaw, 0f));
                    SitSpot.AddBench(sitAnchor, new Vector3(0f, 0.49f, 0f), 0.8f);
                    count++;
                }
                else
                {
                    if (b.sidewalkWidth >= 3f)
                    {
                        Part(batch, metal, origin, yaw, new Vector3(-1.3f, 0f, -0.35f), new Vector3(0.08f, 2.4f, 0.08f));
                        Part(batch, metal, origin, yaw, new Vector3(1.3f, 0f, -0.35f), new Vector3(0.08f, 2.4f, 0.08f));
                        Part(batch, metal, origin, yaw, new Vector3(0f, 2.4f, -0.1f), new Vector3(3.1f, 0.1f, 1.3f));
                        Part(batch, glass, origin, yaw, new Vector3(0f, 0.35f, -0.62f), new Vector3(2.7f, 1.85f, 0.04f));
                        BenchParts(batch, wood, metal, origin, yaw, -0.25f);
                        GameObject shelterSeat = new GameObject("BenchSitAnchor");
                        shelterSeat.transform.SetParent(transform, true);
                        shelterSeat.transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, yaw, 0f));
                        SitSpot.AddBench(shelterSeat, new Vector3(0f, 0.49f, -0.25f), 0.8f);
                        Part(batch, metal, origin, yaw, new Vector3(2.0f, 0f, 0f), new Vector3(0.06f, 2.5f, 0.06f));
                        Part(batch, sign, origin, yaw, new Vector3(2.0f, 2.0f, 0f), new Vector3(0.5f, 0.42f, 0.05f));
                    }
                    else
                    {
                        Part(batch, metal, origin, yaw, new Vector3(1.2f, 0f, 0f), new Vector3(0.06f, 2.5f, 0.06f));
                        Part(batch, sign, origin, yaw, new Vector3(1.2f, 2.0f, 0f), new Vector3(0.5f, 0.42f, 0.05f));
                        BenchParts(batch, wood, metal, origin, yaw, 0f);
                        GameObject stopSeat = new GameObject("BenchSitAnchor");
                        stopSeat.transform.SetParent(transform, true);
                        stopSeat.transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, yaw, 0f));
                        SitSpot.AddBench(stopSeat, new Vector3(0f, 0.49f, 0f), 0.8f);
                    }
                    count++;
                }
            }
        }
        return count;
    }

    static void BenchParts(DetailBatch b, Material wood, Material metal, Vector3 origin, float yaw, float zOff)
    {
        Part(b, wood, origin, yaw, new Vector3(0f, 0.42f, zOff), new Vector3(1.6f, 0.07f, 0.45f));
        Part(b, wood, origin, yaw, new Vector3(0f, 0.62f, zOff - 0.2f), new Vector3(1.6f, 0.4f, 0.05f));
        Part(b, metal, origin, yaw, new Vector3(-0.7f, 0f, zOff), new Vector3(0.06f, 0.42f, 0.4f));
        Part(b, metal, origin, yaw, new Vector3(0.7f, 0f, zOff), new Vector3(0.06f, 0.42f, 0.4f));
    }

    struct Placed
    {
        public GameObject go;
        public LotInfo lot;
    }

    static bool IsSkyscraper(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("skyscraper") || n.Contains("tower");
    }

    static bool IsHouse(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("house") || n.Contains("cottage") || n.Contains("motel");
    }

    static Transform FindDoor(Transform root)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string n = all[i].name.ToLowerInvariant();
            if (!n.Contains("door") || n.Contains("frame") || n.Contains("garage") || n.Contains("back") || n.Contains("indoor") || n.Contains("outdoor")) continue;
            if (all[i].GetComponentInChildren<Renderer>(true) != null) return all[i];
        }
        return null;
    }

    static Bounds BoundsOf(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        Bounds bb = new Bounds(go.transform.position, Vector3.zero);
        bool first = true;
        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i].gameObject.name.StartsWith("Weather")) continue;
            if (first) { bb = rs[i].bounds; first = false; }
            else bb.Encapsulate(rs[i].bounds);
        }
        return bb;
    }

    void BuildingPass(WorldContext c, DetailBatch batch, WorldRng rng, ref int lawnCount, ref int weatherCount)
    {
        Transform bRoot = c.LayerRoot(PlacementLayer.Building);
        List<GameObject> list = new List<GameObject>();
        for (int i = 0; i < bRoot.childCount; i++)
        {
            Transform ch = bRoot.GetChild(i);
            if (ch.name.Contains("ContactShadow") || IsSkyscraper(ch.name)) continue;
            if (ch.GetComponentInChildren<Renderer>(true) == null) continue;
            if (!StylizedMaterialFactory.LooksLikeBuilding(ch)) continue;
            list.Add(ch.gameObject);
        }
        Material wood = Solid("wood", new Color(0.45f, 0.30f, 0.18f));
        Material post = Solid("wood", new Color(0.35f, 0.24f, 0.15f));
        Material red = Solid("paintedMetal", new Color(0.85f, 0.15f, 0.12f));
        Color[] boxCols = { new Color(0.15f, 0.3f, 0.55f), new Color(0.85f, 0.85f, 0.82f), new Color(0.15f, 0.35f, 0.2f), new Color(0.55f, 0.15f, 0.15f), new Color(0.2f, 0.2f, 0.22f) };
        Color[] binCols = { new Color(0.16f, 0.4f, 0.22f), new Color(0.2f, 0.32f, 0.55f), new Color(0.35f, 0.37f, 0.4f) };
        BuildingWeathering weather = new BuildingWeathering();

        for (int i = 0; i < list.Count; i++)
        {
            GameObject go = list[i];
            Bounds bb = BoundsOf(go);
            Vector2 center = new Vector2(bb.center.x, bb.center.z);
            LotInfo lot = null;
            for (int k = 0; k < c.lots.Count; k++)
            {
                if (c.lots[k].empty) continue;
                if (c.lots[k].rect.Contains(center)) { lot = c.lots[k]; break; }
            }
            if (buildingWeathering && rng.Chance(0.8f)) { if (weather.Apply(go, bb, rng, batch, lot != null && lot.zone == ZoneType.Suburb && IsHouse(go.name))) weatherCount++; }
            if (!lawnDetails || lot == null || lot.zone != ZoneType.Suburb) continue;

            Vector2 F = new Vector2(0f, lot.facingZ >= 0 ? 1f : -1f);
            Vector2 S = new Vector2(F.y, -F.x);
            float extF = Mathf.Abs(F.x) * bb.extents.x + Mathf.Abs(F.y) * bb.extents.z;
            float extS = Mathf.Abs(S.x) * bb.extents.x + Mathf.Abs(S.y) * bb.extents.z;
            float y = lot.baseY;

            Vector2 start = center + F * extF;
            Transform door = FindDoor(go.transform);
            if (door != null)
            {
                Bounds db = BoundsOf(door.gameObject);
                Vector2 dp = new Vector2(db.center.x, db.center.z);
                float along = Vector2.Dot(dp - center, F);
                if (along > extF * 0.2f) start = dp;
            }
            start += F * 0.5f;
            float tExit = (F.y > 0f ? lot.rect.yMax - start.y : start.y - lot.rect.yMin);
            if (tExit > 1.5f && tExit < 32f)
            {
                float width = rng.Range(0.9f, 1.3f);
                Vector2 pc = start + F * (tExit * 0.5f);
                batch.Quad(pathMat, new Vector3(pc.x, y + 0.05f, pc.y), new Vector2(F.y, -F.x), new Vector2(width, tExit), new Vector2(1f, tExit / 3f));
                lawnCount++;
                if (rng.Chance(0.75f))
                {
                    Vector2 mp = start + F * (tExit - 0.7f) + S * (rng.Chance(0.5f) ? 1f : -1f) * rng.Range(1.8f, 2.8f);
                    Vector3 mo = new Vector3(mp.x, y, mp.y);
                    float yaw = YawOf(F);
                    Part(batch, post, mo, yaw, new Vector3(0f, 0f, 0f), new Vector3(0.1f, 1.05f, 0.1f));
                    Part(batch, Solid("paintedMetal", boxCols[rng.Range(0, boxCols.Length)]), mo, yaw, new Vector3(0f, 1.05f, 0f), new Vector3(0.28f, 0.24f, 0.5f));
                    Part(batch, red, mo, yaw, new Vector3(0.18f, 1.12f, -0.1f), new Vector3(0.03f, 0.16f, 0.03f));
                    lawnCount++;
                }
            }

            if (rng.Chance(0.7f))
            {
                float sgn = rng.Chance(0.5f) ? 1f : -1f;
                Vector2 bp = center + S * sgn * (extS + 0.75f) + F * (extF * 0.1f);
                int n = rng.Chance(0.4f) ? 2 : 1;
                for (int q = 0; q < n; q++)
                {
                    Vector2 p = bp + F * (q * 0.7f);
                    if (!lot.rect.Contains(p)) continue;
                    Material bm = Solid("plastic", binCols[rng.Range(0, binCols.Length)]);
                    Vector3 pb = new Vector3(p.x, y, p.y);
                    batch.Prism(bm, pb, 0.3f, 0.8f, 8);
                    batch.Prism(Solid("plastic", new Color(0.12f, 0.13f, 0.14f)), pb + Vector3.up * 0.8f, 0.33f, 0.08f, 8);
                    lawnCount++;
                }
            }

            int clusters = rng.Range(2, 5);
            for (int q = 0; q < clusters; q++)
            {
                Vector2 p = new Vector2(rng.Range(lot.rect.xMin + 1.2f, lot.rect.xMax - 1.2f), rng.Range(lot.rect.yMin + 1.2f, lot.rect.yMax - 1.2f));
                if (Mathf.Abs(p.x - center.x) < bb.extents.x + 1.4f && Mathf.Abs(p.y - center.y) < bb.extents.z + 1.4f) continue;
                float sz = rng.Range(1.4f, 2.6f);
                batch.Quad(flowerMat, new Vector3(p.x, y + 0.06f, p.y), Rot(Vector2.right, rng.Range(0f, 360f)), new Vector2(sz, sz), Vector2.one);
                lawnCount++;
            }
        }
    }

    int BuildFenceGrass(WorldContext c, DetailBatch batch, WorldRng rng)
    {
        int count = 0;
        Renderer[] rs = c.root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length && count < maxGrassTufts; i++)
        {
            string n = rs[i].gameObject.name.ToLowerInvariant();
            if (!n.Contains("fence") && !n.Contains("picket")) continue;
            Bounds b = rs[i].bounds;
            bool alongX = b.size.x >= b.size.z;
            float longLen = alongX ? b.size.x : b.size.z;
            float thick = alongX ? b.size.z : b.size.x;
            if (longLen < 1.2f || longLen > 40f) continue;
            float baseY = b.min.y + 0.04f;
            for (float t = 0.2f; t < longLen - 0.1f && count < maxGrassTufts; t += 0.9f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (!rng.Chance(0.55f)) continue;
                    float off = side * (thick * 0.5f + rng.Range(0.15f, 0.45f));
                    Vector3 p = alongX ? new Vector3(b.min.x + t, baseY, b.center.z + off) : new Vector3(b.center.x + off, baseY, b.min.z + t);
                    float w = rng.Range(0.55f, 0.85f), h = rng.Range(0.3f, 0.55f);
                    float ang = rng.Range(0f, 180f);
                    batch.CrossQuad(tuftMat, p, Rot(Vector2.right, ang), w, h);
                    batch.CrossQuad(tuftMat, p, Rot(Vector2.right, ang + 90f), w, h);
                    count++;
                }
            }
        }
        return count;
    }
}

public class SurfaceDetailDriver : MonoBehaviour
{
    float next;

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.5f;
        SurfaceDetailStage.TickPuddles();
    }
}
