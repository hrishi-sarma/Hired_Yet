using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(InterviewRoomGenerator))]
public class InterviewRoomGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        InterviewRoomGenerator gen = (InterviewRoomGenerator)target;

        EditorGUILayout.Space(12);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            fixedHeight = 36
        };

        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.45f);
        if (GUILayout.Button("🎲 Regenerate / Randomize Room", buttonStyle))
        {
            gen.Regenerate();
            EditorUtility.SetDirty(gen);
        }

        GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
        if (GUILayout.Button("🧹 Clear Generated Room", GUILayout.Height(26)))
        {
            gen.Clear();
            EditorUtility.SetDirty(gen);
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "Tip: You can assign custom 3D Asset Store prefabs in the 'Catalog' slot above. " +
            "Any slot left empty will automatically use stylized procedural low-poly furniture and props.",
            MessageType.Info
        );
    }
}
