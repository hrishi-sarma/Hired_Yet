using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(WorldGenerator))]
public class WorldGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        WorldGenerator g = (WorldGenerator)target;

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate", GUILayout.Height(28)))
            {
                g.Generate();
                MarkDirty(g);
            }
            if (GUILayout.Button("Clear", GUILayout.Height(28)))
            {
                g.Clear();
                MarkDirty(g);
            }
        }
        if (GUILayout.Button("Add Missing Stages"))
        {
            AddMissingStages(g.gameObject);
            MarkDirty(g);
        }
        if (GUILayout.Button("New Seed + Generate"))
        {
            g.NewSeed();
            g.Generate();
            MarkDirty(g);
            EditorUtility.SetDirty(g);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate"))
            {
                ClipValidator v = GetValidator(g);
                if (g.Context == null) Debug.LogWarning("Generate the world first, then Validate.");
                else
                {
                    v.Validate(g.Context);
                    SceneView.RepaintAll();
                }
            }
            if (GUILayout.Button("Validate Seeds"))
            {
                RunSeeds(g);
                MarkDirty(g);
            }
        }
        ClipValidator last = g.GetComponent<ClipValidator>();
        if (last != null && last.LastReport != null)
            EditorGUILayout.HelpBox(last.LastReport.Summary(), last.LastReport.errors == 0 ? MessageType.Info : MessageType.Warning);

        WorldStage[] stages = g.GetOrderedStages();
        string list = stages.Length == 0 ? "No stages found. Add stage components to this object." : "Run order:";
        for (int i = 0; i < stages.Length; i++)
            list += "\n" + stages[i].Order + "  " + stages[i].StageName + (stages[i].stageEnabled ? "" : "  (disabled)");
        EditorGUILayout.HelpBox(list, MessageType.Info);
    }

    static ClipValidator GetValidator(WorldGenerator g)
    {
        ClipValidator v = g.GetComponent<ClipValidator>();
        if (v == null) v = Undo.AddComponent<ClipValidator>(g.gameObject);
        return v;
    }

    static void RunSeeds(WorldGenerator g)
    {
        ClipValidator v = GetValidator(g);
        int original = g.settings.seed;
        int[] seeds = v.testSeeds;
        StringBuilder sb = new StringBuilder("Seed validation:");
        int failed = 0;
        try
        {
            for (int i = 0; i < seeds.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Validating seeds", "Seed " + seeds[i], (float)i / seeds.Length);
                g.settings.seed = seeds[i];
                v.ClearReport();
                g.Generate();
                if (v.LastReport == null)
                {
                    sb.Append("\n  seed ").Append(seeds[i]).Append(": generation failed, no report");
                    failed++;
                    continue;
                }
                sb.Append("\n  ").Append(v.LastReport.Summary());
                if (v.LastReport.errors > 0) failed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            g.settings.seed = original;
        }
        g.Generate();
        sb.Append("\n").Append(failed == 0 ? "ALL SEEDS CLEAN" : failed + " seed(s) with problems");
        Debug.Log(sb.ToString());
    }

    static void AddMissingStages(GameObject go)
    {
        foreach (System.Type t in TypeCache.GetTypesDerivedFrom<WorldStage>())
        {
            if (t.IsAbstract) continue;
            if (go.GetComponent(t) == null) Undo.AddComponent(go, t);
        }
        if (go.GetComponent<WorldBootstrap>() == null) Undo.AddComponent<WorldBootstrap>(go);
        if (go.GetComponent<NpcDirector>() == null) Undo.AddComponent<NpcDirector>(go);
    }

    static void MarkDirty(WorldGenerator g)
    {
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
    }

    [MenuItem("GameObject/Procedural World/Create World Generator", false, 10)]
    static void Create()
    {
        GameObject go = new GameObject("World Generator");
        go.AddComponent<WorldGenerator>();
        AddMissingStages(go);
        Undo.RegisterCreatedObjectUndo(go, "Create World Generator");
        Selection.activeGameObject = go;
    }
}