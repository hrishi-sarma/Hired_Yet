using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Procedural World/Stages/3 Roads And Sidewalks")]
public class RoadStage : WorldStage
{
    public DistrictSpec suburb = DistrictSpec.Create(8f, 2f, 36f);
    public DistrictSpec city = DistrictSpec.Create(16f, 4f, 56f);
    public HighwaySpec highway = new HighwaySpec();
    public int cityCornerTolerance = 1;

    class District
    {
        public DistrictSpec spec;
        public float top;
        public readonly List<BlockInfo> blocks = new List<BlockInfo>();
        public readonly List<Vector2> nodes = new List<Vector2>();
        public readonly List<RoadLine> lines = new List<RoadLine>();
    }

    public override int Order
    {
        get { return StageOrder.RoadPlan; }
    }

    public override string StageName
    {
        get { return "Roads And Sidewalks"; }
    }

    public override void Run(WorldContext c)
    {
        if (c.zoneMap == null)
        {
            Debug.LogError("Roads need the Zones stage enabled.");
            return;
        }
        EnsureDynamicBlockSize(c, suburb, ZoneType.Suburb);
        EnsureDynamicBlockSize(c, city, ZoneType.City);

        WorldRng rng = c.Rng("roads");
        RoadNetwork net = new RoadNetwork();
        c.roads = net;

        District a = Plan(c, net, c.zoneMap.suburbCenter, c.zoneMap.suburbRadius, ZoneType.Suburb, suburb);
        District b = Plan(c, net, c.zoneMap.cityCenter, c.zoneMap.cityRadius, ZoneType.City, city);
        HighwayRibbon.Plan(c, net, highway, c.zoneMap.highway);
        SpawnDistrict(c, net, a, rng);
        SpawnDistrict(c, net, b, rng);
        RoadDetailBuilder.BuildLocalRoadDetails(c, net, suburb, city, rng);
        HighwayRibbon.Spawn(c, net, highway);
        RoadDetailBuilder.BuildDistrictPerimeterRails(c);

        Debug.Log("Roads: suburb blocks " + a.blocks.Count + ", city blocks " + b.blocks.Count + ", road pieces " + net.roadPieces + ", sidewalk pieces " + net.sidewalkPieces + ", rejected " + net.rejected);
    }

    void EnsureDynamicBlockSize(WorldContext c, DistrictSpec district, ZoneType zone)
    {
        if (district == null || c.generator == null) return;
        BuildingStage buildings = c.generator.GetComponent<BuildingStage>();
        if (buildings == null || buildings.types == null) return;
        float requiredWidth = 0f;
        float requiredRowDepth = 0f;
        for (int i = 0; i < buildings.types.Length; i++)
        {
            BuildingType t = buildings.types[i];
            if (t == null || t.zone != zone) continue;
            Vector2 fp = buildings.MaxFootprint(t);
            requiredWidth = Mathf.Max(requiredWidth, fp.x + 2f * t.sideSetback);
            requiredRowDepth = Mathf.Max(requiredRowDepth, fp.y + t.frontSetback + t.backSetback);
        }
        float requiredBlock = Mathf.Max(requiredWidth, requiredRowDepth * 2f) + 0.25f;
        if (requiredBlock > district.blockSize)
        {
            Debug.Log("Dynamic " + zone + " grid: increasing block size from " +
                district.blockSize.ToString("0.0") + "m to " + requiredBlock.ToString("0.0") +
                "m to fit configured building footprints (max 10% corrective shrink).");
            district.blockSize = requiredBlock;
        }
    }

    District Plan(WorldContext c, RoadNetwork net, Vector2 center, float radius, ZoneType zone, DistrictSpec s)
    {
        District d = new District();
        d.spec = s;
        ZoneMap zm = c.zoneMap;
        float pitch = s.Pitch;
        float rw = s.roadWidth;
        int n = Mathf.Max(1, Mathf.CeilToInt(2f * radius / pitch));
        float half = n * pitch * 0.5f;
        float r = pitch * 0.5f;
        bool[,] nodeUsed = new bool[n + 1, n + 1];
        bool[,] hUsed = new bool[n, n + 1];
        bool[,] vUsed = new bool[n + 1, n];
        float sum = 0f;

        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                Vector2 bc = center + new Vector2((i + 0.5f) * pitch - half, (j + 0.5f) * pitch - half);
                if (zm.ZoneAt(bc) != zone) continue;
                int bad = (zm.ZoneAt(bc.x - r, bc.y - r) != zone ? 1 : 0) + (zm.ZoneAt(bc.x + r, bc.y - r) != zone ? 1 : 0) + (zm.ZoneAt(bc.x - r, bc.y + r) != zone ? 1 : 0) + (zm.ZoneAt(bc.x + r, bc.y + r) != zone ? 1 : 0);
                if (bad > (zone == ZoneType.City ? cityCornerTolerance : 0)) continue;
                sum += c.HeightAt(bc.x, bc.y);
                nodeUsed[i, j] = true;
                nodeUsed[i + 1, j] = true;
                nodeUsed[i, j + 1] = true;
                nodeUsed[i + 1, j + 1] = true;
                hUsed[i, j] = true;
                hUsed[i, j + 1] = true;
                vUsed[i, j] = true;
                vUsed[i + 1, j] = true;
                BlockInfo bi = new BlockInfo();
                bi.zone = zone;
                bi.center = bc;
                bi.sidewalkWidth = s.sidewalkWidth;
                bi.ix = i;
                bi.iz = j;
                bi.lotRect = new Rect(bc.x - s.blockSize * 0.5f, bc.y - s.blockSize * 0.5f, s.blockSize, s.blockSize);
                d.blocks.Add(bi);
            }
        }

        if (d.blocks.Count == 0)
        {
            Debug.LogWarning("Roads: no blocks fit in the " + zone + " zone. Increase its radius or lower block size.");
            return d;
        }

        float ground = sum / d.blocks.Count;
        d.top = ground + s.carveDepth;
        float texel = c.TexelSize;
        float fp = pitch + rw;
        for (int k = 0; k < d.blocks.Count; k++)
        {
            BlockInfo bi = d.blocks[k];
            bi.roadTop = d.top;
            bi.sidewalkTop = d.top + s.curbHeight;
            Rect area = new Rect(bi.center.x - fp * 0.5f - texel, bi.center.y - fp * 0.5f - texel, fp + 2f * texel, fp + 2f * texel);
            c.Flatten(area, ground, s.flattenBlend);
            net.footprints.Add(new Rect(bi.center.x - fp * 0.5f, bi.center.y - fp * 0.5f, fp, fp));
            net.blocks.Add(bi);
        }

        for (int j = 0; j <= n; j++)
        {
            for (int i = 0; i <= n; i++)
            {
                Vector2 np = center + new Vector2(i * pitch - half, j * pitch - half);
                if (nodeUsed[i, j]) d.nodes.Add(np);
                if (i < n && hUsed[i, j]) AddLine(d, net, np, np + new Vector2(pitch, 0f), rw);
                if (j < n && vUsed[i, j]) AddLine(d, net, np, np + new Vector2(0f, pitch), rw);
            }
        }
        return d;
    }

    static void AddLine(District d, RoadNetwork net, Vector2 a, Vector2 b, float width)
    {
        RoadLine l = new RoadLine();
        l.a = a;
        l.b = b;
        l.width = width;
        l.roadTop = d.top;
        d.lines.Add(l);
        net.lines.Add(l);
    }

    void SpawnDistrict(WorldContext c, RoadNetwork net, District d, WorldRng rng)
    {
        if (d.blocks.Count == 0) return;
        DistrictSpec s = d.spec;
        float rw = s.roadWidth;
        float t = s.roadThickness;
        float baseY = d.top - t;
        float len = s.Pitch - rw;

        for (int i = 0; i < d.nodes.Count; i++)
        {
            GameObject g = RoadPiece.Spawn(c, s.intersection, PlacementLayer.Road, d.nodes[i], 0f, new Vector3(rw, t, rw), baseY, rng);
            if (g == null) net.rejected++;
            else net.roadPieces++;
        }
        for (int i = 0; i < d.lines.Count; i++)
        {
            RoadLine l = d.lines[i];
            bool horizontal = Mathf.Abs(l.b.x - l.a.x) > 0.01f;
            GameObject g = RoadPiece.Spawn(c, s.straight, PlacementLayer.Road, (l.a + l.b) * 0.5f, horizontal ? 90f : 0f, new Vector3(rw, t, len), baseY, rng);
            if (g == null) net.rejected++;
            else net.roadPieces++;
        }
        for (int i = 0; i < d.blocks.Count; i++)
        {
            int placed = SidewalkBuilder.Build(c, s, d.blocks[i].center, d.top, rng);
            net.sidewalkPieces += placed;
            net.rejected += 4 - placed;
        }
    }
}
