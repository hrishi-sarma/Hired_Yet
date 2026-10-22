using UnityEngine;

[AddComponentMenu("Procedural World/Stages/7 Props")]
public class PropStage : WorldStage
{
    [Header("Street Lamps")]
    public bool lamps = true;
    public float suburbSpacing = 20f;
    public float citySpacing = 24f;
    public float poleHeight = 5f;
    public float poleWidth = 0.3f;
    public float headSize = 0.9f;
    public ModelSlot pole = ModelSlot.Primitive(PrimitiveKind.Cylinder, new Color(0.15f, 0.15f, 0.17f));
    public ModelSlot head = ModelSlot.Primitive(PrimitiveKind.Sphere, new Color(1f, 0.9f, 0.6f));
    public ModelSlot whole = new ModelSlot();

    public override int Order
    {
        get { return StageOrder.Props; }
    }

    public override string StageName
    {
        get { return "Props"; }
    }

    bool HasWhole()
    {
        if (whole == null || whole.variants == null) return false;
        for (int i = 0; i < whole.variants.Length; i++)
            if (whole.variants[i] != null) return true;
        return false;
    }

    public override void Run(WorldContext c)
    {
        if (c.roads == null)
        {
            Debug.LogError("Props need the Roads stage enabled.");
            return;
        }
        if (!lamps) return;
        head.colliders = ColliderMode.None;
        WorldRng rng = c.Rng("props");
        bool useWhole = HasWhole();
        int placed = 0;
        int rejected = 0;
        for (int k = 0; k < c.roads.blocks.Count; k++)
        {
            BlockInfo b = c.roads.blocks[k];
            float spacing = b.zone == ZoneType.City ? citySpacing : suburbSpacing;
            float o = b.lotRect.width * 0.5f + b.sidewalkWidth * 0.5f;
            float len = b.lotRect.width + b.sidewalkWidth;
            int count = Mathf.Max(1, Mathf.RoundToInt(len / Mathf.Max(4f, spacing)));
            for (int side = 0; side < 4; side++)
            {
                for (int i = 0; i < count; i++)
                {
                    float t = (i + 0.5f) * len / count - len * 0.5f;
                    Vector2 p;
                    if (side == 0) p = new Vector2(b.center.x + t, b.center.y - o);
                    else if (side == 1) p = new Vector2(b.center.x + t, b.center.y + o);
                    else if (side == 2) p = new Vector2(b.center.x - o, b.center.y + t);
                    else p = new Vector2(b.center.x + o, b.center.y + t);
                    Vector3 baseCenter = new Vector3(p.x, b.sidewalkTop, p.y);
                    OrientedBox box = OrientedBox.FromBase(baseCenter, new Vector3(poleWidth + 0.2f, poleHeight, poleWidth + 0.2f), 0f);
                    if (!c.occupancy.IsFree(box, PlacementLayer.Prop, 0f))
                    {
                        rejected++;
                        continue;
                    }
                    Transform parent = c.LayerRoot(PlacementLayer.Prop);
                    GameObject go;
                    if (useWhole)
                    {
                        go = ModelFitter.Spawn(whole, baseCenter, 0f, new Vector3(headSize, poleHeight, headSize), parent, rng.Raw);
                    }
                    else
                    {
                        go = ModelFitter.Spawn(pole, baseCenter, 0f, new Vector3(poleWidth, poleHeight, poleWidth), parent, rng.Raw);
                        GameObject headObject = ModelFitter.Spawn(head, new Vector3(p.x, b.sidewalkTop + poleHeight - headSize * 0.5f, p.y), 0f, new Vector3(headSize, headSize * 0.5f, headSize), go.transform, rng.Raw);
                        Renderer[] headRenderers = headObject.GetComponentsInChildren<Renderer>(true);
                        for (int h = 0; h < headRenderers.Length; h++) { headRenderers[h].sharedMaterial = StylizedMaterialFactory.GetLampGlow(); }
                    }
                    go.name = "StreetLamp";
                    StreetLampLights.Register(new Vector3(p.x, b.sidewalkTop + poleHeight - headSize * 0.5f, p.y), 12f);
                    c.occupancy.Add(box, PlacementLayer.Prop, go);
                    placed++;
                }
            }
        }
        Debug.Log("Props: " + placed + " street lamps placed, " + rejected + " rejected");
    }
}
