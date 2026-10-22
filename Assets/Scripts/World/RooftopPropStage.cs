using UnityEngine;

public class BeaconBlinker : MonoBehaviour
{
    static readonly Color On = new Color(1f, 0.1f, 0.08f);
    static readonly Color Off = new Color(0.22f, 0.02f, 0.02f);

    void Update()
    {
        Material m = CityMaterials.Beacon();
        bool lit = Mathf.Repeat(Time.time, 2f) < 0.35f;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", lit ? On : Off);
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", lit ? On * 4f : Color.black);
    }
}

[AddComponentMenu("Procedural World/Stages/5b Rooftop Props")]
public class RooftopPropStage : WorldStage
{
    public int minProps = 2;
    public int maxProps = 5;
    public float beaconMinHeight = 38f;
    public int maxTotalProps = 700;

    public override int Order
    {
        get { return StageOrder.Props + 40; }
    }

    public override string StageName
    {
        get { return "Rooftop Props"; }
    }

    static bool RoofY(Transform b, Bounds bb, float x, float z, bool hasCol, out float y)
    {
        y = bb.max.y;
        if (!hasCol) return true;
        RaycastHit hit;
        if (!Physics.Raycast(new Vector3(x, bb.max.y + 20f, z), Vector3.down, out hit, bb.size.y + 40f)) return false;
        if (!hit.transform.IsChildOf(b)) return false;
        if (hit.normal.y < 0.9f) return false;
        if (hit.point.y < bb.min.y + bb.size.y * 0.6f) return false;
        y = hit.point.y;
        return true;
    }

    public override void Run(WorldContext c)
    {
        if (c.zoneMap == null) return;
        Physics.SyncTransforms();
        Transform bRoot = c.LayerRoot(PlacementLayer.Building);
        WorldRng rng = c.Rng("rooftops");
        DetailBatch batch = new DetailBatch();
        Transform root = new GameObject("RooftopProps").transform;
        root.SetParent(c.LayerRoot(PlacementLayer.Prop), false);

        Material metal = StylizedMaterialFactory.Get("paintedMetal", new Color(0.62f, 0.64f, 0.66f), 1);
        Material dark = StylizedMaterialFactory.Get("paintedMetal", new Color(0.2f, 0.22f, 0.25f), 2);
        Material conc = StylizedMaterialFactory.Get("concrete", new Color(0.7f, 0.7f, 0.68f), 0);
        Material tank = CityMaterials.Plain("tank", new Color(0.55f, 0.4f, 0.28f), 0.1f);

        int props = 0, beacons = 0;
        int count = bRoot.childCount;
        for (int i = 0; i < count && props < maxTotalProps; i++)
        {
            Transform b = bRoot.GetChild(i);
            Renderer[] rs = b.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) continue;
            Bounds bb = rs[0].bounds;
            for (int r = 1; r < rs.Length; r++) bb.Encapsulate(rs[r].bounds);
            if (c.zoneMap.ZoneAt(bb.center.x, bb.center.z) != ZoneType.City) continue;
            string nm = b.name.ToLowerInvariant();
            if (nm.Contains("warehouse") || nm.Contains("station") || nm.Contains("house")) continue;
            if (bb.size.y < 8f || bb.size.x < 8f || bb.size.z < 8f) continue;
            bool hasCol = b.GetComponentInChildren<Collider>() != null;

            int n = minProps + Mathf.FloorToInt(rng.Value() * (maxProps - minProps + 1));
            for (int k = 0; k < n; k++)
            {
                float px = bb.center.x + rng.Range(-0.3f, 0.3f) * bb.size.x;
                float pz = bb.center.z + rng.Range(-0.3f, 0.3f) * bb.size.z;
                float y;
                if (!RoofY(b, bb, px, pz, hasCol, out y)) continue;
                Vector3 p = new Vector3(px, y - 0.05f, pz);
                float yaw = Mathf.Floor(rng.Value() * 4f) * 90f;
                float v = rng.Value();
                if (v < 0.35f)
                {
                    batch.Box(metal, p, yaw, new Vector3(1.8f, 1f, 1.1f));
                    batch.Box(dark, p + Vector3.up * 1f, yaw, new Vector3(0.9f, 0.08f, 0.9f));
                }
                else if (v < 0.6f)
                {
                    batch.Box(dark, p, yaw, new Vector3(1.6f, 0.8f, 1.6f));
                    batch.Prism(tank, p + Vector3.up * 0.8f, 1.2f, 2.2f, 10);
                }
                else if (v < 0.8f)
                {
                    batch.Box(conc, p, yaw, new Vector3(2.8f, 2.3f, 2.4f));
                    batch.Box(dark, p + Vector3.up * 2.3f, yaw, new Vector3(3f, 0.15f, 2.6f));
                }
                else
                {
                    batch.Box(metal, p, 0f, new Vector3(0.12f, 6f, 0.12f));
                    batch.Box(metal, p + Vector3.up * 4.5f, yaw, new Vector3(0.1f, 0.08f, 1.8f));
                    batch.Box(metal, p + Vector3.up * 5.2f, yaw, new Vector3(0.1f, 0.08f, 1.3f));
                }
                props++;
            }

            if (bb.size.y >= beaconMinHeight)
            {
                float y;
                if (RoofY(b, bb, bb.center.x, bb.center.z, hasCol, out y))
                {
                    Vector3 p = new Vector3(bb.center.x, y - 0.05f, bb.center.z);
                    batch.Box(metal, p, 0f, new Vector3(0.1f, 3.5f, 0.1f));
                    GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    s.name = "Beacon";
                    s.transform.SetParent(root, false);
                    s.transform.position = p + Vector3.up * 3.6f;
                    s.transform.localScale = Vector3.one * 0.5f;
                    s.GetComponent<MeshRenderer>().sharedMaterial = CityMaterials.Beacon();
                    s.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (Application.isPlaying) WorldUtil.Kill(s.GetComponent<Collider>());
                    else Object.DestroyImmediate(s.GetComponent<Collider>());
                    beacons++;
                }
            }
        }
        batch.Flush(root, "Rooftop");
        if (beacons > 0) root.gameObject.AddComponent<BeaconBlinker>();
        Debug.Log("Rooftops: " + props + " props, " + beacons + " beacons.");
    }
}
