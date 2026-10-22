using System.Collections.Generic;
using UnityEngine;

public class LotInfo
{
    public Rect rect;
    public Vector2 center;
    public int facingZ;
    public float yaw;
    public float baseY;
    public ZoneType zone;
    public BuildingType buildingType;
    public GameObject variant;
    public bool empty;
    public Vector3 footprint;
    public BlockInfo block;
    public GameObject surface;
}

[AddComponentMenu("Procedural World/Stages/4 Lots")]
public class LotStage : WorldStage
{
    public float suburbLotWidth = 12f;
    public float cityLotWidth = 28f;
    public float lotThickness = 0.3f;
    public float maxHouseGap = 6f;
    public int cityOuterRows = 3;
    public float cityCoreBlocks = 1.4f;
    public ModelSlot suburbLot = ModelSlot.Primitive(PrimitiveKind.Cube, new Color(0.38f, 0.5f, 0.28f));
    public ModelSlot cityLot = ModelSlot.Primitive(PrimitiveKind.Cube, new Color(0.5f, 0.5f, 0.5f));

    public override int Order
    {
        get { return StageOrder.Lots; }
    }

    public override string StageName
    {
        get { return "Lots"; }
    }

    public override void Run(WorldContext c)
    {
        if (c.roads == null)
        {
            Debug.LogError("Lots need the Roads stage enabled.");
            return;
        }
        WorldRng rng = c.Rng("lots");
        BuildingStage buildings = c.generator != null ? c.generator.GetComponent<BuildingStage>() : null;
        if (buildings != null) buildings.EnsureCityInfill();
        RoadStage roadStage = c.generator != null ? c.generator.GetComponent<RoadStage>() : null;
        c.lots.Clear();
        int rejected = 0;
        for (int k = 0; k < c.roads.blocks.Count; k++)
        {
            BlockInfo b = c.roads.blocks[k];
            bool cityZone = b.zone == ZoneType.City;
            float target = cityZone ? cityLotWidth : suburbLotWidth;
            ModelSlot surfaceSlot = cityZone ? cityLot : suburbLot;
            int rows = 2;
            if (cityZone && cityOuterRows > 2 && roadStage != null && (b.center - c.zoneMap.cityCenter).magnitude > roadStage.city.Pitch * cityCoreBlocks) rows = cityOuterRows;
            float rowD = b.lotRect.height / rows;

            for (int row = 0; row < rows; row++)
            {
                List<BuildingType> cTypes = new List<BuildingType>();
                List<GameObject> cVariants = new List<GameObject>();
                List<Vector3> cPrints = new List<Vector3>();
                List<float> cWidths = new List<float>();
                float x = b.lotRect.xMin;
                int safety = 0;
                while (x < b.lotRect.xMax - 1f && safety++ < 64)
                {
                    float remaining = b.lotRect.xMax - x;
                    BuildingType chosen = null;
                    GameObject chosenVariant = null;
                    Vector3 footprint = Vector3.zero;
                    float cellWidth;
                    if (buildings != null)
                    {
                        if (!buildings.PickForLot(b.zone, rng, remaining, rowD, out chosen, out chosenVariant, out footprint)) break;
                        cellWidth = Mathf.Max(4f, footprint.x + 2f * chosen.sideSetback);
                        if (cellWidth > remaining + 0.01f) break;
                    }
                    else
                    {
                        int cols = Mathf.Max(1, Mathf.FloorToInt(b.lotRect.width / Mathf.Max(2f, target)));
                        cellWidth = b.lotRect.width / cols;
                        if (cellWidth > remaining + 0.01f) break;
                    }
                    cTypes.Add(chosen);
                    cVariants.Add(chosenVariant);
                    cPrints.Add(footprint);
                    cWidths.Add(cellWidth);
                    x += cellWidth;
                }

                int n = cWidths.Count;
                float left = b.lotRect.width - (x - b.lotRect.xMin);
                float gap = n > 1 ? Mathf.Min(left / (n - 1), maxHouseGap) : 0f;
                float rest = Mathf.Max(0f, left - gap * Mathf.Max(0, n - 1));
                float cursor = b.lotRect.xMin;
                if (rest * 0.5f > 0.05f)
                {
                    if (!AddLot(c, b, row, rowD, cursor, rest * 0.5f, null, null, Vector3.zero, true, surfaceSlot, rng)) rejected++;
                    cursor += rest * 0.5f;
                }
                for (int i = 0; i < n; i++)
                {
                    float width = cWidths[i] + (i < n - 1 ? gap : 0f);
                    if (!AddLot(c, b, row, rowD, cursor, width, cTypes[i], cVariants[i], cPrints[i], false, surfaceSlot, rng)) rejected++;
                    cursor += width;
                }
                float tail = b.lotRect.xMax - cursor;
                if (tail > 0.05f)
                {
                    if (!AddLot(c, b, row, rowD, cursor, tail, null, null, Vector3.zero, true, surfaceSlot, rng)) rejected++;
                }
            }
        }
        Debug.Log("Lots: " + c.lots.Count + " created with type-aware dynamic widths, rejected " + rejected);
    }

    bool AddLot(WorldContext c, BlockInfo b, int row, float rowD, float x, float width, BuildingType type, GameObject variant, Vector3 footprint, bool empty, ModelSlot surfaceSlot, WorldRng rng)
    {
        Rect r = new Rect(x, b.lotRect.yMin + row * rowD, width, rowD);
        LotInfo lot = new LotInfo();
        lot.rect = r;
        lot.center = r.center;
        lot.facingZ = row == 0 ? -1 : 1;
        lot.yaw = row == 0 ? 180f : 0f;
        lot.baseY = b.sidewalkTop;
        lot.zone = b.zone;
        lot.buildingType = type;
        lot.variant = variant;
        lot.footprint = footprint;
        lot.empty = empty;
        lot.block = b;
        lot.surface = RoadPiece.Spawn(c, surfaceSlot, PlacementLayer.Lot, r.center, 0f,
            new Vector3(r.width, lotThickness, rowD), b.sidewalkTop - lotThickness, rng);
        if (lot.surface == null) return false;
        if (b.zone == ZoneType.City) StylizedMaterialFactory.ApplyColoredRole(lot.surface.transform, "concrete", new Color(0.58f, 0.59f, 0.6f), 2);
        c.lots.Add(lot);
        return true;
    }
}