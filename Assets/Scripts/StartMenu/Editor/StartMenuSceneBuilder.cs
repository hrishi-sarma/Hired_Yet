using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Start Menu > Build StartMenu Scene
/// Open (or create) the empty "StartMenu" scene, run the tool: it copies the world rig (generator + stages,
/// director, lights, sky, volumes, RenderSettings) from the game's world scene, swaps WorldBootstrap for
/// MenuWorldBootstrap, and adds the camera rig. Nothing to place by hand. Safe to run again (it rebuilds).
/// </summary>
public static class StartMenuSceneBuilder
{
    const string RigName = "Menu Camera Rig";

    [MenuItem("Tools/Start Menu/Build StartMenu Scene")]
    public static void Build()
    {
        Scene dst = EditorSceneManager.GetActiveScene();
        if (dst.name != "StartMenu" && !EditorUtility.DisplayDialog("Start Menu",
            "The active scene is '" + dst.name + "', not 'StartMenu'. Build into it anyway?", "Build", "Cancel")) return;

        string srcPath = FindScene(WorldLauncher.WorldSceneName);
        if (srcPath == null)
        {
            EditorUtility.DisplayDialog("Start Menu", "Could not find the world scene '" + WorldLauncher.WorldSceneName + "' in the project.", "OK");
            return;
        }

        // clean previous build
        foreach (var go in dst.GetRootGameObjects())
            if (go.GetComponent<MenuWorldBootstrap>() != null || go.GetComponent<StartMenuUI>() != null || go.name == RigName || go.GetComponentInChildren<WorldGenerator>() != null
                || go.GetComponent<Light>() != null || go.GetComponent<UnityEngine.Rendering.Volume>() != null)
                Object.DestroyImmediate(go);

        Scene src = EditorSceneManager.OpenScene(srcPath, OpenSceneMode.Additive);
        int copied = 0; WorldGenerator genCopy = null;
        try
        {
            CopyRenderSettings(src, dst);
            foreach (var root in src.GetRootGameObjects())
            {
                if (Skip(root)) continue;
                GameObject c = Object.Instantiate(root);
                c.name = root.name;
                SceneManager.MoveGameObjectToScene(c, dst);
                copied++;
                var gen = c.GetComponentInChildren<WorldGenerator>();
                if (gen != null) { genCopy = gen; Prepare(gen); }
            }
        }
        finally { EditorSceneManager.CloseScene(src, true); }

        if (genCopy == null)
        {
            EditorUtility.DisplayDialog("Start Menu", "No WorldGenerator found in '" + srcPath + "'. Nothing was built.", "OK");
            return;
        }

        // camera rig
        var rigGo = new GameObject(RigName);
        SceneManager.MoveGameObjectToScene(rigGo, dst);
        var camGo = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener));
        camGo.transform.SetParent(rigGo.transform, false);
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.nearClipPlane = 0.3f; cam.farClipPlane = 2500f; cam.fieldOfView = 50f;
        var rig = rigGo.AddComponent<MenuCameraRig>(); rig.cam = cam;
        rigGo.transform.position = new Vector3(0f, 120f, -300f);

        var boot = genCopy.GetComponent<MenuWorldBootstrap>();
        if (boot == null) boot = genCopy.gameObject.AddComponent<MenuWorldBootstrap>();
        boot.rig = rig;

        AddMenuUI(false);
        EditorSceneManager.MarkSceneDirty(dst);
        EditorSceneManager.SaveScene(dst);
        EditorUtility.DisplayDialog("Start Menu", "Built from '" + srcPath + "' (" + copied + " root objects copied).\nPress Play to preview.\nTune: '" + RigName + "' and the World Generator object.", "OK");
    }

    static bool Skip(GameObject root)
    {
        if (root.GetComponentInChildren<Camera>(true) != null) return true;
        if (root.GetComponentInChildren<AudioListener>(true) != null) return true;
        if (root.GetComponentInChildren<EventSystem>(true) != null) return true;
        if (root.tag == "Player") return true;
        return false;
    }

    static void Prepare(WorldGenerator gen)
    {
        // drop any pre-generated child world, so the scene stays light; MenuWorldBootstrap regenerates it at play
        for (int i = gen.transform.childCount - 1; i >= 0; i--)
            if (gen.transform.GetChild(i).name.StartsWith("World")) Object.DestroyImmediate(gen.transform.GetChild(i).gameObject);
        var old = gen.GetComponent<WorldBootstrap>();
        if (old != null) Object.DestroyImmediate(old);
        gen.generateOnPlay = false;
    }

    static void CopyRenderSettings(Scene src, Scene dst)
    {
        Scene prev = EditorSceneManager.GetActiveScene();
        SceneManager.SetActiveScene(src);
        var sky = RenderSettings.skybox; var am = RenderSettings.ambientMode; var al = RenderSettings.ambientLight;
        var asky = RenderSettings.ambientSkyColor; var aeq = RenderSettings.ambientEquatorColor; var agr = RenderSettings.ambientGroundColor;
        var ai = RenderSettings.ambientIntensity; var fog = RenderSettings.fog; var fc = RenderSettings.fogColor;
        var fd = RenderSettings.fogDensity; var fm = RenderSettings.fogMode; var fs = RenderSettings.fogStartDistance; var fe = RenderSettings.fogEndDistance;
        var sun = RenderSettings.sun; var rm = RenderSettings.defaultReflectionMode; var ri = RenderSettings.reflectionIntensity;
        SceneManager.SetActiveScene(dst);
        RenderSettings.skybox = sky; RenderSettings.ambientMode = am; RenderSettings.ambientLight = al;
        RenderSettings.ambientSkyColor = asky; RenderSettings.ambientEquatorColor = aeq; RenderSettings.ambientGroundColor = agr;
        RenderSettings.ambientIntensity = ai; RenderSettings.fog = fog; RenderSettings.fogColor = fc;
        RenderSettings.fogDensity = fd; RenderSettings.fogMode = fm; RenderSettings.fogStartDistance = fs; RenderSettings.fogEndDistance = fe;
        RenderSettings.defaultReflectionMode = rm; RenderSettings.reflectionIntensity = ri;
        RenderSettings.sun = null;   // re-found by the sky/sun scripts at runtime
        if (prev.IsValid()) SceneManager.SetActiveScene(prev);
    }

    static string FindScene(string name)
    {
        foreach (var g in AssetDatabase.FindAssets("t:Scene " + name))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == name) return p;
        }
        return null;
    }

    [MenuItem("Tools/Start Menu/Add Menu UI To Open Scene")]
    public static void AddMenuUIMenu() { AddMenuUI(true); }

    /// <summary>Adds the StartMenuUI object and copies font + credits from the scene that holds ResumeFormUI.</summary>
    static void AddMenuUI(bool save)
    {
        Scene dst = EditorSceneManager.GetActiveScene();
        foreach (var go in dst.GetRootGameObjects()) if (go.GetComponent<StartMenuUI>() != null) Object.DestroyImmediate(go);
        var ui = new GameObject("Start Menu UI").AddComponent<StartMenuUI>();
        SceneManager.MoveGameObjectToScene(ui.gameObject, dst);

        foreach (var g in AssetDatabase.FindAssets("t:Scene"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            string n = Path.GetFileNameWithoutExtension(p);
            if (n == dst.name || n == WorldLauncher.WorldSceneName || !p.StartsWith("Assets/")) continue;
            Scene s = EditorSceneManager.OpenScene(p, OpenSceneMode.Additive);
            ResumeFormUI f = null;
            foreach (var root in s.GetRootGameObjects()) { f = root.GetComponentInChildren<ResumeFormUI>(true); if (f != null) break; }
            if (f != null) { ui.font = f.handwritingFont; ui.creditsText = f.creditsText; ui.resumeScene = n; EnsureBuildScenes(dst.path, p); }
            EditorSceneManager.CloseScene(s, true);
            if (f != null) break;
        }
        if (ui.font == null) Debug.LogWarning("StartMenu: no ResumeFormUI.handwritingFont found; assign 'Start Menu UI > Font' manually (same font as the resume scene).");
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(dst);
        if (save) EditorSceneManager.SaveScene(dst);
    }

    /// <summary>StartMenu = first scene in Build Settings, resume scene right after it, world scene kept.</summary>
    static void EnsureBuildScenes(string startPath, string resumePath)
    {
        if (string.IsNullOrEmpty(startPath)) return;
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == startPath || s.path == resumePath);
        list.Insert(0, new EditorBuildSettingsScene(startPath, true));
        list.Insert(1, new EditorBuildSettingsScene(resumePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    [MenuItem("Tools/Start Menu/Snap Rig To Scene View (manual camera)")]
    public static void SnapToSceneView()
    {
        var rig = Object.FindFirstObjectByType<MenuCameraRig>();
        var sv = SceneView.lastActiveSceneView;
        if (rig == null || sv == null) return;
        Undo.RecordObject(rig.transform, "Snap rig");
        rig.autoFrame = false;
        rig.transform.SetPositionAndRotation(sv.camera.transform.position, sv.camera.transform.rotation);
        if (rig.cam != null) rig.cam.fieldOfView = sv.camera.fieldOfView;
        EditorUtility.SetDirty(rig);
    }
}
