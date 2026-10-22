using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class ClipIssue
{
    public string kind;
    public PlacementLayer layerA;
    public PlacementLayer layerB;
    public Vector3 position;
    public bool warning;
    public string text;
}

public class ClipReport
{
    public int seed;
    public int records;
    public int pairsTested;
    public int errors;
    public int warnings;
    public int terrainErrors;
    public long milliseconds;
    public List<ClipIssue> issues = new List<ClipIssue>();

    public string Summary()
    {
        return "seed " + seed + ": " + records + " boxes, " + pairsTested + " pairs tested, " + errors + " errors (" + terrainErrors + " terrain), " + warnings + " warnings, " + milliseconds + " ms";
    }
}

[AddComponentMenu("Procedural World/Clip Validator")]
public class ClipValidator : WorldStage
{
    public bool validateInPlayMode = false;
    public bool checkTerrain = true;
    public float terrainTolerance = 0.25f;
    public float floatTolerance = 1f;
    public int maxLoggedIssues = 20;
    public int[] testSeeds = new int[] { 101, 2024, 31337, 777777, 424242 };

    public ClipReport LastReport { get; private set; }

    public override int Order
    {
        get { return StageOrder.Validate; }
    }

    public override string StageName
    {
        get { return "Clip Validator"; }
    }

    public override void Run(WorldContext c)
    {
        if (Application.isPlaying && !validateInPlayMode) return;
        Validate(c);
    }

    public void ClearReport()
    {
        LastReport = null;
    }

    static bool IsHighway(SpawnRecord r)
    {
        return r.owner != null && r.owner.name == "Highway";
    }

    public ClipReport Validate(WorldContext c)
    {
        Stopwatch sw = Stopwatch.StartNew();
        ClipReport rep = new ClipReport();
        rep.seed = c.settings.seed;
        List<SpawnRecord> recs = c.occupancy.Records;
        int n = recs.Count;
        rep.records = n;

        Rect[] aabb = new Rect[n];
        int[] order = new int[n];
        for (int i = 0; i < n; i++)
        {
            aabb[i] = recs[i].box.Aabb;
            order[i] = i;
        }
        System.Array.Sort(order, (a, b) => aabb[a].xMin.CompareTo(aabb[b].xMin));

        for (int ii = 0; ii < n; ii++)
        {
            int i = order[ii];
            Rect ra = aabb[i];
            SpawnRecord a = recs[i];
            for (int jj = ii + 1; jj < n; jj++)
            {
                int j = order[jj];
                Rect rb = aabb[j];
                if (rb.xMin >= ra.xMax) break;
                if (rb.yMin >= ra.yMax || rb.yMax <= ra.yMin) continue;
                SpawnRecord b = recs[j];
                if (a.owner != null && ReferenceEquals(a.owner, b.owner)) continue;
                if (!OccupancyGrid.Conflicts(a.layer, b.layer)) continue;
                rep.pairsTested++;
                if (!OrientedBox.Overlap(a.box, b.box, OccupancyGrid.Epsilon)) continue;
                bool warn = a.layer == PlacementLayer.Road && b.layer == PlacementLayer.Road && (IsHighway(a) || IsHighway(b));
                Vector2 mid = (a.box.center + b.box.center) * 0.5f;
                ClipIssue issue = new ClipIssue();
                issue.kind = "overlap";
                issue.layerA = a.layer;
                issue.layerB = b.layer;
                issue.warning = warn;
                issue.position = new Vector3(mid.x, c.HeightAt(mid.x, mid.y) + 0.5f, mid.y);
                issue.text = a.layer + " x " + b.layer + " at (" + mid.x.ToString("F1") + ", " + mid.y.ToString("F1") + ")" + (warn ? " [highway junction]" : "");
                rep.issues.Add(issue);
                if (warn) rep.warnings++;
                else rep.errors++;
            }
        }

        if (checkTerrain) CheckTerrain(c, rep);

        rep.milliseconds = sw.ElapsedMilliseconds;
        LastReport = rep;
        Log(rep);
        return rep;
    }

    void CheckTerrain(WorldContext c, ClipReport rep)
    {
        List<SpawnRecord> recs = c.occupancy.Records;
        Vector2[] corners = new Vector2[4];
        for (int i = 0; i < recs.Count; i++)
        {
            SpawnRecord r = recs[i];
            if (r.layer == PlacementLayer.Tree || r.layer == PlacementLayer.Lot) continue;
            r.box.Corners(corners);
            float hMax = float.MinValue;
            float hMin = float.MaxValue;
            for (int k = 0; k < 9; k++)
            {
                Vector2 p;
                if (k == 0) p = r.box.center;
                else if (k <= 4) p = corners[k - 1];
                else p = (corners[k - 5] + corners[(k - 4) % 4]) * 0.5f;
                float h = c.HeightAt(p.x, p.y);
                if (h > hMax) hMax = h;
                if (h < hMin) hMin = h;
            }

            string problem = null;
            if (r.layer == PlacementLayer.Road || r.layer == PlacementLayer.Sidewalk)
            {
                if (hMax > r.box.maxY + terrainTolerance) problem = "terrain pokes " + (hMax - r.box.maxY).ToString("F2") + " m above";
            }
            else
            {
                if (hMax > r.box.minY + terrainTolerance) problem = "terrain pokes " + (hMax - r.box.minY).ToString("F2") + " m into";
                else if (r.layer == PlacementLayer.Building && hMin < r.box.minY - floatTolerance) problem = "floats " + (r.box.minY - hMin).ToString("F2") + " m above terrain,";
            }
            if (problem == null) continue;

            ClipIssue issue = new ClipIssue();
            issue.kind = "terrain";
            issue.layerA = r.layer;
            issue.layerB = r.layer;
            issue.warning = false;
            issue.position = new Vector3(r.box.center.x, r.box.maxY + 0.5f, r.box.center.y);
            issue.text = problem + " " + r.layer + " at (" + r.box.center.x.ToString("F1") + ", " + r.box.center.y.ToString("F1") + ")";
            rep.issues.Add(issue);
            rep.errors++;
            rep.terrainErrors++;
        }
    }

    void Log(ClipReport rep)
    {
        StringBuilder sb = new StringBuilder("ClipValidator: " + rep.Summary());
        int shown = 0;
        for (int i = 0; i < rep.issues.Count && shown < maxLoggedIssues; i++)
        {
            if (rep.issues[i].warning) continue;
            sb.Append("\n  ").Append(rep.issues[i].text);
            shown++;
        }
        if (rep.errors > shown) sb.Append("\n  ... ").Append(rep.errors - shown).Append(" more");
        if (rep.errors > 0) Debug.LogWarning(sb.ToString());
        else Debug.Log(sb.ToString());
    }
}
