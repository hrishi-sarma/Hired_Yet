#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CharacterBoneInspector
{
    [MenuItem("Tools/Interview Game/Debug Inspect Manager Bones")]
    public static void Inspect()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OfficeCharactersPack/Prefabs/URP/Manager.prefab");
        if (prefab == null)
        {
            Debug.LogError("Manager prefab not found!");
            return;
        }

        GameObject go = Object.Instantiate(prefab);
        Animator anim = go.GetComponent<Animator>();
        if (anim != null && anim.isHuman)
        {
            Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            Transform lThigh = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform lCalf = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform lArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform lForearm = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);

            Debug.Log($"Hips localPos={hips.localPosition}, localRot={hips.localRotation.eulerAngles}");
            Debug.Log($"LeftUpperLeg localPos={lThigh.localPosition}, localRot={lThigh.localRotation.eulerAngles}");
            Debug.Log($"LeftLowerLeg localPos={lCalf.localPosition}, localRot={lCalf.localRotation.eulerAngles}");
            Debug.Log($"LeftUpperArm localPos={lArm.localPosition}, localRot={lArm.localRotation.eulerAngles}");
            Debug.Log($"LeftLowerArm localPos={lForearm.localPosition}, localRot={lForearm.localRotation.eulerAngles}");
        }

        Object.DestroyImmediate(go);
    }
}
#endif
