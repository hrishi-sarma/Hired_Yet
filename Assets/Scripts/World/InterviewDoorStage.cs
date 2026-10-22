using TMPro;
using UnityEngine;

[AddComponentMenu("Procedural World/Stages/4 Interview Door")]
public class InterviewDoorStage : WorldStage
{
    public string sceneName = "InterviewRoom";
    public string fallbackSceneName = "InterviewWorld";
    public string promptText = "[ PRESS E TO START INTERVIEW ]";
    public TMP_FontAsset font;
    public float minBuildingHeight = 20f;
    public Vector3 triggerSize = new Vector3(8f, 6f, 10f);
    public Vector3 doorSize = new Vector3(3f, 3.4f, 0.25f);
    public Color doorColor = new Color(0.2f, 1f, 0.4f);

    public static bool HasChosen;
    public static Vector2 ChosenCenter;

    public override int Order
    {
        get { return StageOrder.Props + 50; }
    }

    public override string StageName
    {
        get { return "Interview Door"; }
    }

    public override void Run(WorldContext c)
    {
        HasChosen = false;
        Vector3 ds = new Vector3(1.1f, 2.2f, 0.12f);
        if (c.zoneMap == null)
        {
            Debug.LogError("Interview Door needs the Zones stage.");
            return;
        }

        Vector2 entry = c.zoneMap.cityCenter;
        if (c.zoneMap.highway != null && c.zoneMap.highway.points.Count > 1)
        {
            Vector2 a = c.zoneMap.highway.Start;
            Vector2 b = c.zoneMap.highway.End;
            entry = (a - c.zoneMap.cityCenter).sqrMagnitude < (b - c.zoneMap.cityCenter).sqrMagnitude ? a : b;
        }

        SpawnRecord best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < c.occupancy.Records.Count; i++)
        {
            SpawnRecord r = c.occupancy.Records[i];
            if (r.layer != PlacementLayer.Building) continue;
            if (c.zoneMap.ZoneAt(r.box.center) != ZoneType.City) continue;
            if (r.box.maxY - r.box.minY < minBuildingHeight) continue;
            float d = (r.box.center - entry).sqrMagnitude;
            if (d >= bestDist) continue;
            bestDist = d;
            best = r;
        }
        for (int pass = 0; pass < 2 && best == null; pass++)
        {
            for (int i = 0; i < c.occupancy.Records.Count; i++)
            {
                SpawnRecord r = c.occupancy.Records[i];
                if (r.layer != PlacementLayer.Building) continue;
                if (pass == 0 && c.zoneMap.ZoneAt(r.box.center) != ZoneType.City) continue;
                float d = (r.box.center - entry).sqrMagnitude;
                if (d >= bestDist) continue;
                bestDist = d;
                best = r;
            }
        }
        if (best == null)
        {
            Debug.LogWarning("Interview Door: no suitable city building found.");
            return;
        }

        OrientedBox box = best.box;
        HasChosen = true;
        ChosenCenter = box.center;
        Vector2 front = box.axisZ;
        Vector2 faceCenter = box.center + front * box.half.y;
        Vector2 zoneCenter = faceCenter + front * (triggerSize.z * 0.5f);

        GameObject root = new GameObject("InterviewDoor");
        root.transform.SetParent(c.LayerRoot(PlacementLayer.Prop), false);
        root.transform.SetPositionAndRotation(new Vector3(zoneCenter.x, box.minY, zoneCenter.y), Quaternion.Euler(0f, box.yawDeg, 0f));
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0f, triggerSize.y * 0.5f, 0f);
        col.size = triggerSize;
        InterviewDoor door = root.AddComponent<InterviewDoor>();
        door.sceneName = sceneName;
        door.fallbackSceneName = fallbackSceneName;
        door.promptText = promptText;
        door.font = font;
        door.doorPoint = new Vector3(faceCenter.x + front.x * 1.5f, box.minY, faceCenter.y + front.y * 1.5f);
        door.hasDoorPoint = true;
        door.promptRadius = 4.5f;

        GameObject marker = PrimitiveFactory.Create(PrimitiveKind.Cube, root.transform);
        marker.name = "DoorMarker";
        Vector2 markerPos = faceCenter + front * (ds.z * 0.5f + 0.05f);
        marker.transform.SetPositionAndRotation(new Vector3(markerPos.x, box.minY + ds.y * 0.5f, markerPos.y), Quaternion.Euler(0f, box.yawDeg, 0f));
        marker.transform.localScale = ds;
        Material leafMat = BuildingDetailStage.CreateDoorMaterial(new Color(0.45f, 0.28f, 0.15f));
        marker.GetComponent<Renderer>().sharedMaterial = leafMat;
        GameObject handle = PrimitiveFactory.Create(PrimitiveKind.Cube, root.transform);
        handle.name = "DoorHandle";
        Vector2 hp = faceCenter + front * (ds.z + 0.1f) + box.axisX * (ds.x * 0.5f - 0.3f);
        handle.transform.SetPositionAndRotation(new Vector3(hp.x, box.minY + 1.1f, hp.y), Quaternion.Euler(0f, box.yawDeg, 0f));
        handle.transform.localScale = new Vector3(0.1f, 0.1f, 0.12f);
        handle.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Get(new Color(0.8f, 0.6f, 0.2f));

        Material glow = new Material(leafMat);
        Color gold = new Color(1f, 0.78f, 0.1f);
        if (glow.HasProperty("_BaseColor")) glow.SetColor("_BaseColor", gold);
        if (glow.HasProperty("_Color")) glow.SetColor("_Color", gold);
        glow.EnableKeyword("_EMISSION");
        glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (glow.HasProperty("_EmissionColor")) glow.SetColor("_EmissionColor", gold * 3.2f);
        float fw = 0.3f, fd = 0.5f;
        float[,] parts =
        {
            { -(ds.x * 0.5f + fw * 0.5f), (ds.y + fw) * 0.5f, fw, ds.y + fw },
            { ds.x * 0.5f + fw * 0.5f, (ds.y + fw) * 0.5f, fw, ds.y + fw },
            { 0f, ds.y + fw * 0.5f, ds.x + fw * 2f, fw }
        };
        for (int i = 0; i < 3; i++)
        {
            GameObject piece = PrimitiveFactory.Create(PrimitiveKind.Cube, root.transform);
            piece.name = "DoorGlowFrame";
            Vector2 pp = faceCenter + box.axisX * parts[i, 0] + front * (fd * 0.5f);
            piece.transform.SetPositionAndRotation(new Vector3(pp.x, box.minY + parts[i, 1], pp.y), Quaternion.Euler(0f, box.yawDeg, 0f));
            piece.transform.localScale = new Vector3(parts[i, 2], parts[i, 3], fd);
            piece.GetComponent<Renderer>().sharedMaterial = glow;
        }
        GameObject lightGo = new GameObject("DoorGlowLight");
        lightGo.transform.SetParent(root.transform, false);
        Vector2 lp = faceCenter + front * 1.8f;
        lightGo.transform.position = new Vector3(lp.x, box.minY + ds.y * 0.6f, lp.y);
        Light gl = lightGo.AddComponent<Light>();
        gl.type = LightType.Point;
        gl.color = gold;
        gl.range = 14f;
        gl.intensity = 4f;
        gl.shadows = LightShadows.None;

        Debug.Log("Interview Door: placed on a " + (box.maxY - box.minY).ToString("F0") + " m building at (" + box.center.x.ToString("F0") + ", " + box.center.y.ToString("F0") + ")");
    }
}
