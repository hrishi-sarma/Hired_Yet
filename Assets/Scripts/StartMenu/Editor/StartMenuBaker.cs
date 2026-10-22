using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stage 4A: generates the world IN THE OPEN SCENE (edit mode), frames the camera from it, strips gameplay clutter and
/// the runtime director, and saves the scene. Afterwards the world is visible in the Scene view and Play needs no generation.
/// Tools > Start Menu > Bake World / Clear Baked World
/// </summary>
public static class StartMenuBaker
{
    // roots copied from the game scene that the menu never uses
    static readonly string[] DropRoots = { "InventoryUI", "InventoryCanvas", "ItemHold", "ItemsDBtest", "WorldItemManager", "BreadPiece", "TrafficLights (runtime)", "Menu Focus" };

    [MenuItem("Tools/Start Menu/Bake World (generate in scene)")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Start Menu", "Stop Play mode first.", "OK"); return; }
        var gen = Object.FindFirstObjectByType<WorldGenerator>();
        var boot = gen != null ? gen.GetComponent<MenuWorldBootstrap>() : null;
        var rig = Object.FindFirstObjectByType<MenuCameraRig>();
        if (gen == null || boot == null || rig == null)
        {
            EditorUtility.DisplayDialog("Start Menu", "Run 'Tools > Start Menu > Build StartMenu Scene' first (needs World Generator + Menu World Bootstrap + Menu Camera Rig).", "OK");
            return;
        }
        Scene scene = gen.gameObject.scene;
        if (string.IsNullOrEmpty(scene.path)) { EditorUtility.DisplayDialog("Start Menu", "Save the scene first.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Bake World", "Generates the world in this scene with seed " + boot.fixedSeed + " (can take a minute), then saves the scene. Continue?", "Bake", "Cancel")) return;

        int removed = RemoveClutter(scene, rig);
        var dir = gen.GetComponent<NpcDirector>(); if (dir != null) dir.enabled = false;   // never started in the menu; kept only as the model/animation settings source for the baked cast
        var door = gen.GetComponent<InterviewDoorStage>(); if (door == null) door = gen.gameObject.AddComponent<InterviewDoorStage>();
        door.stageEnabled = false;                                                                  // no interview-door / office indicator in the menu world
        gen.settings.seed = boot.fixedSeed;
        gen.generateOnPlay = false;

        bool ok = false;
        try
        {
            var r = gen.GenerateRoutine();
            while (r.MoveNext())
                if (EditorUtility.DisplayCancelableProgressBar("Baking world", gen.CurrentStage, gen.Progress)) { Debug.LogWarning("StartMenu: bake cancelled."); break; }
            ok = gen.LastGenerateOk && gen.Context != null;
        }
        finally { EditorUtility.ClearProgressBar(); }
        if (!ok) { EditorUtility.DisplayDialog("Start Menu", "Generation failed or was cancelled. See the Console.", "OK"); return; }

        // freeze the automatic framing into the rig's transform / FOV (the world data is not available at runtime any more)
        bool prev = rig.autoFrame;
        if (rig.autoFrame) { rig.Frame(gen.Context); rig.autoFrame = false; }   // first bake only: afterwards the rig transform / FOV 55 you set are kept
        else rig.Frame(gen.Context);                                          // just computes the focus point for the cast
        EditorUtility.SetDirty(rig); if (rig.cam != null) EditorUtility.SetDirty(rig.cam);

        try { StartMenuCastBaker.Bake(gen, rig); }   // Stage 4B: roaming cars / NPCs / animals (needs the live WorldContext)
        catch (System.Exception ex) { Debug.LogError("StartMenu cast bake failed: " + ex); }

        CaptureNight(gen);

        // generated meshes / materials / textures only exist in memory: without this they are serialized INTO the scene file,
        // which becomes huge and unreadable in a build ("level0 is corrupted! Position out of bounds!")
        int moved = SceneAssetPersister.Persist(scene);

        boot.useBakedWorld = true;
        EditorUtility.SetDirty(boot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Start Menu", "World baked and scene saved.\nMoved " + moved + " generated meshes/materials/textures into Assets/BakedSceneAssets (scene file: " + SceneAssetPersister.SceneSizeText(scene) + ").\nRemoved " + removed + " unused root objects.\nCheck the Scene view, then press Play.\nTo move the camera, just move '" + rig.name + "' (auto framing is now off).", "OK");
    }

    /// <summary>Stores the runtime-only glow materials + lamp positions in the scene (see MenuNightData).</summary>
    static void CaptureNight(WorldGenerator gen)
    {
        var old = Object.FindFirstObjectByType<MenuNightData>(FindObjectsInactive.Include);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("Menu Night Data");
        var d = go.AddComponent<MenuNightData>();
        var m = new System.Collections.Generic.List<Material>(); var c = new System.Collections.Generic.List<Color>();
        var s = new System.Collections.Generic.List<float>(); var x = new System.Collections.Generic.List<float>();
        StylizedMaterialFactory.ExportGlow(m, c, s, x);
        d.glowMaterials = m.ToArray(); d.glowColors = c.ToArray(); d.glowStrength = s.ToArray(); d.glowMax = x.ToArray();
        var lp = new System.Collections.Generic.List<Vector3>(); var lr = new System.Collections.Generic.List<float>();
        StreetLampLights.Export(lp, lr);
        d.lampPositions = lp.ToArray(); d.lampRanges = lr.ToArray();
        Debug.Log("StartMenu: captured " + m.Count + " glow materials and " + lp.Count + " street lamps for night.");
    }

    [MenuItem("Tools/Start Menu/Clear Baked World")]
    public static void Clear()
    {
        var gen = Object.FindFirstObjectByType<WorldGenerator>();
        if (gen == null) return;
        gen.Clear();
        var boot = gen.GetComponent<MenuWorldBootstrap>();
        if (boot != null) { boot.useBakedWorld = false; EditorUtility.SetDirty(boot); }
        EditorSceneManager.MarkSceneDirty(gen.gameObject.scene);
    }

    static int RemoveClutter(Scene scene, MenuCameraRig rig)
    {
        int n = 0;
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go == null || go == rig.gameObject) continue;
            bool drop = System.Array.IndexOf(DropRoots, go.name) >= 0;
            // stray default camera (the Menu Camera inside the rig is the only one needed)
            if (!drop && go.name == "Main Camera" && go.GetComponent<Camera>() != null) drop = true;
            if (drop) { Object.DestroyImmediate(go); n++; }
        }
        return n;
    }
}
