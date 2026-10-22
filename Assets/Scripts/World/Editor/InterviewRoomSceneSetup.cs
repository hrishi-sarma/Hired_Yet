using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InterviewRoomSceneSetup
{
    const string ScenePath = "Assets/Scenes/InterviewRoom.unity";
    const string CatalogPath = "Assets/Settings/InterviewRoomCatalog.asset";

    [MenuItem("Tools/Interview Game/Setup Procedural Interview Room")]
    public static void Setup()
    {
        // 1. Open scene if not open
        Scene current = EditorSceneManager.GetActiveScene();
        if (current.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            current = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // 2. Ensure Catalog asset exists
        InterviewRoomCatalog catalog = AssetDatabase.LoadAssetAtPath<InterviewRoomCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<InterviewRoomCatalog>();
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Created new catalog at: " + CatalogPath);
        }

        // 3. Find or Create RoomGenerator GameObject
        GameObject genGo = GameObject.Find("RoomGenerator");
        if (genGo == null)
        {
            genGo = new GameObject("RoomGenerator");
            Undo.RegisterCreatedObjectUndo(genGo, "Create Room Generator");
        }

        InterviewRoomGenerator gen = genGo.GetComponent<InterviewRoomGenerator>();
        if (gen == null)
        {
            gen = genGo.AddComponent<InterviewRoomGenerator>();
        }

        // 4. Assign references
        gen.catalog = catalog;
        gen.roomWidth = 7.5f;
        gen.roomDepth = 8.5f;
        gen.roomHeight = 3.2f;
        gen.generateOnStart = true;
        gen.randomizeEachPlay = true;

        // 5. Generate Room
        gen.Generate();

        // 6. Mark scene dirty & save
        EditorUtility.SetDirty(gen);
        EditorSceneManager.MarkSceneDirty(current);
        EditorSceneManager.SaveScene(current);

        Selection.activeGameObject = genGo;

        Debug.Log("Setup Procedural Interview Room successfully completed! Check the Inspector on 'RoomGenerator'.");
    }
}
