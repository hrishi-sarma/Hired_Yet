using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Items.EditorTools
{
    [CustomEditor(typeof(GuitarSongBank))]
    public class GuitarSongBankEditor : Editor
    {
        ReorderableList list;

        void OnEnable()
        {
            list = new ReorderableList(serializedObject, serializedObject.FindProperty("slots"), true, true, true, true);
            list.elementHeight = EditorGUIUtility.singleLineHeight * 2f + 8f;
            list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Song Slots (mp3 / any AudioClip)");
            list.drawElementCallback = (r, i, active, focused) =>
            {
                var e = list.serializedProperty.GetArrayElementAtIndex(i);
                var clip = e.FindPropertyRelative("clip"); var title = e.FindPropertyRelative("title"); var vol = e.FindPropertyRelative("volume");
                float h = EditorGUIUtility.singleLineHeight;
                var l1 = new Rect(r.x, r.y + 2f, r.width, h);
                var l2 = new Rect(r.x, r.y + h + 5f, r.width, h);
                EditorGUI.LabelField(new Rect(l1.x, l1.y, 44f, h), "#" + (i + 1));
                EditorGUI.PropertyField(new Rect(l1.x + 44f, l1.y, l1.width - 44f, h), clip, GUIContent.none);
                float tw = l2.width * 0.62f;
                var tr = new Rect(l2.x + 44f, l2.y, tw - 44f, h);
                EditorGUI.PropertyField(tr, title, GUIContent.none);
                if (string.IsNullOrEmpty(title.stringValue))
                {
                    string hint = clip.objectReferenceValue != null ? clip.objectReferenceValue.name : "Title (optional)";
                    GUI.Label(tr, "  " + hint, EditorStyles.centeredGreyMiniLabel);
                }
                EditorGUI.LabelField(new Rect(l2.x, l2.y, 44f, h), "Title");
                EditorGUI.Slider(new Rect(l2.x + tw + 6f, l2.y, l2.width - tw - 6f, h), vol, 0f, 1f, GUIContent.none);
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var bank = (GuitarSongBank)target;
            EditorGUILayout.HelpBox("Hold E with the guitar in hand to play. Each new press plays the next tune from these slots.", MessageType.Info);
            if (bank.ValidCount == 0) EditorGUILayout.HelpBox("No songs yet. Drag mp3 files into the box below.", MessageType.Warning);

            DropZone();
            list.DoLayoutList();
            EditorGUILayout.Space(4);
            foreach (var n in new[] { "order", "masterVolume", "fadeIn", "fadeOut", "loopWhileHeld", "loopBpm", "loopBars", "titleHold", "titleFade" })
                EditorGUILayout.PropertyField(serializedObject.FindProperty(n));
            serializedObject.ApplyModifiedProperties();
        }

        void DropZone()
        {
            var r = GUILayoutUtility.GetRect(0f, 44f, GUILayout.ExpandWidth(true));
            GUI.Box(r, "Drop mp3 / audio files here to add slots", EditorStyles.helpBox);
            var ev = Event.current;
            if (!r.Contains(ev.mousePosition)) return;
            if (ev.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy; ev.Use();
            }
            else if (ev.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                var arr = serializedObject.FindProperty("slots");
                foreach (var o in DragAndDrop.objectReferences)
                {
                    var c = o as AudioClip; if (c == null) continue;
                    arr.InsertArrayElementAtIndex(arr.arraySize);
                    var e = arr.GetArrayElementAtIndex(arr.arraySize - 1);
                    e.FindPropertyRelative("clip").objectReferenceValue = c;
                    e.FindPropertyRelative("title").stringValue = "";
                    e.FindPropertyRelative("volume").floatValue = 1f;
                }
                ev.Use();
            }
        }
    }

    // Creates Assets/Resources/GuitarSongBank.asset once, so the Inspector panel is always available.
    [InitializeOnLoad]
    static class GuitarSongBankBootstrap
    {
        const string Dir = "Assets/Resources", Path = Dir + "/GuitarSongBank.asset";

        static GuitarSongBankBootstrap() { EditorApplication.delayCall += () => Ensure(); }

        static GuitarSongBank Ensure()
        {
            var b = AssetDatabase.LoadAssetAtPath<GuitarSongBank>(Path);
            if (b != null) return b;
            var found = AssetDatabase.FindAssets("t:GuitarSongBank");
            if (found.Length > 0) return AssetDatabase.LoadAssetAtPath<GuitarSongBank>(AssetDatabase.GUIDToAssetPath(found[0]));
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Resources");
            b = ScriptableObject.CreateInstance<GuitarSongBank>();
            AssetDatabase.CreateAsset(b, Path);
            AssetDatabase.SaveAssets();
            return b;
        }

        [MenuItem("Tools/Guitar/Select Song Bank")]
        static void Select()
        {
            var b = Ensure(); Selection.activeObject = b; EditorGUIUtility.PingObject(b);
        }
    }
}
