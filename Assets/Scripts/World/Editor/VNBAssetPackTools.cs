using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class VNBAssetPackTools
{
    const string CatalogPath = "Assets/Settings/InterviewRoomCatalog.asset";
    const string VnbPath = "Assets/VNB - Office Set";

    [MenuItem("Tools/Interview Game/1. Fix Pink Materials (Upgrade to URP)")]
    public static void FixPinkMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            EditorUtility.DisplayDialog("Error", "Universal Render Pipeline/Lit shader not found. Ensure URP is installed in the project.", "OK");
            return;
        }

        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { VnbPath });
        int count = 0;

        foreach (string guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Check if using legacy Standard or incompatible shader
            if (mat.shader.name == "Standard" || mat.shader.name.StartsWith("Legacy") || mat.shader.name.Contains("Error"))
            {
                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;

                mat.shader = urpLit;

                if (mainTex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (bumpMap != null && mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", bumpMap);

                EditorUtility.SetDirty(mat);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Materials Upgraded", $"Successfully converted {count} materials in '{VnbPath}' to Universal Render Pipeline/Lit!", "OK");
        Debug.Log($"Fixed {count} materials in {VnbPath} to Universal Render Pipeline/Lit.");
    }

    [MenuItem("Tools/Interview Game/2. Auto-Populate Catalog with Office Set")]
    public static void AutoPopulateCatalog()
    {
        // 1. Ensure Catalog exists
        InterviewRoomCatalog catalog = AssetDatabase.LoadAssetAtPath<InterviewRoomCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<InterviewRoomCatalog>();
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        // 2. Find prefabs in VNB pack (only actual office desks from Prefabs/Office, and proper chairs)
        catalog.deskPrefabs = FindPrefabsMatching(VnbPath + "/Prefabs/Office", "desk_1", "desk_2", "desk_3", "simple_desk");
        catalog.interviewerChairPrefabs = FindPrefabsMatching(VnbPath, "office_chair", "standard_office_chair_1");
        catalog.guestChairPrefabs = FindPrefabsMatching(VnbPath, "simple_chair_1", "simple_office_chair_variant");
        
        catalog.plantPrefabs = FindPrefabsMatching(VnbPath, "bamboo", "nature_deco_plant", "nature_deco_1");
        catalog.carpetPrefabs = FindPrefabsMatching(VnbPath, "rug_a", "rug_b");
        
        catalog.wallDecorPrefabs = FindPrefabsMatching(VnbPath, "painting", "whiteboard", "clock");
        catalog.cornerPropPrefabs = FindPrefabsMatching(VnbPath, "trash_bin", "shelf", "coffee_machine", "fan");
        catalog.deskClutterPrefabs = FindPrefabsMatching(VnbPath, "monitor", "keyboard", "mouse", "phone", "pencil_holder", "paper_stray", "pile_of_paper", "computer_new");

        // 3. Find the 3 Interviewer Characters from OfficeCharactersPack (Manager, OfficeWorker, Receptionist - strictly excluding Janitor)
        catalog.interviewerPrefabs = FindPrefabsMatching("Assets/OfficeCharactersPack/Prefabs/URP", "Manager", "OfficeWorker", "Receptionist");

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        // 4. If RoomGenerator is in the scene, link and regenerate
        InterviewRoomGenerator gen = Object.FindFirstObjectByType<InterviewRoomGenerator>();
        if (gen != null)
        {
            gen.catalog = catalog;
            gen.Regenerate();
            EditorUtility.SetDirty(gen);
        }

        EditorUtility.DisplayDialog("Catalog Populated",
            $"InterviewRoomCatalog has been populated with office props and characters!\n\n" +
            $"- Desks: {catalog.deskPrefabs.Length}\n" +
            $"- Chairs: {catalog.interviewerChairPrefabs.Length + catalog.guestChairPrefabs.Length}\n" +
            $"- Interviewers: {catalog.interviewerPrefabs.Length} (Manager, OfficeWorker, Receptionist)\n" +
            $"- Plants: {catalog.plantPrefabs.Length}\n" +
            $"- Rugs: {catalog.carpetPrefabs.Length}\n" +
            $"- Wall Decor: {catalog.wallDecorPrefabs.Length}\n" +
            $"- Corner Props: {catalog.cornerPropPrefabs.Length}\n" +
            $"- Desk Clutter: {catalog.deskClutterPrefabs.Length}\n\n" +
            "The room has been regenerated with these new models!", "Awesome!");

        Selection.activeObject = catalog;
    }

    [MenuItem("Tools/Interview Game/3. Assign Interviewer Characters (Manager, Worker, Receptionist)")]
    public static void AssignInterviewerCharacters()
    {
        InterviewRoomCatalog catalog = AssetDatabase.LoadAssetAtPath<InterviewRoomCatalog>(CatalogPath);
        if (catalog == null)
        {
            EditorUtility.DisplayDialog("Error", "Catalog not found at Assets/Settings/InterviewRoomCatalog.asset. Run Setup first.", "OK");
            return;
        }

        // Strictly pick Manager, OfficeWorker, Receptionist (Janitor is excluded)
        catalog.interviewerPrefabs = FindPrefabsMatching("Assets/OfficeCharactersPack/Prefabs/URP", "Manager", "OfficeWorker", "Receptionist");
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        InterviewRoomGenerator gen = Object.FindFirstObjectByType<InterviewRoomGenerator>();
        if (gen != null)
        {
            gen.catalog = catalog;
            gen.Regenerate();
            EditorUtility.SetDirty(gen);
        }

        EditorUtility.DisplayDialog("Interviewer Characters Assigned",
            $"Successfully assigned {catalog.interviewerPrefabs.Length} characters to the catalog:\n\n" +
            "- Manager\n- OfficeWorker\n- Receptionist\n\n(Janitor excluded)\n\n" +
            "The room has been regenerated with a random interviewer!", "Great!");
        Selection.activeObject = catalog;
    }

    static GameObject[] FindPrefabsMatching(string searchFolder, params string[] keywords)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { searchFolder });
        List<GameObject> matches = new List<GameObject>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            foreach (string kw in keywords)
            {
                if (fileName.Contains(kw.ToLowerInvariant()))
                {
                    GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (p != null && !matches.Contains(p))
                    {
                        matches.Add(p);
                    }
                    break;
                }
            }
        }

        return matches.ToArray();
    }
}
