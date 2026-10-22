using UnityEngine;

public static class WorldUtil
{
    /// <summary>
    /// Edit-mode safe destroy. In Play mode / builds it is a normal Destroy (end of frame);
    /// in edit mode (world baking, scene tools) it uses DestroyImmediate, because Destroy is not allowed there
    /// and spams "Destroy may not be called from edit mode!" errors.
    /// </summary>
    public static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) { Object.Destroy(o); return; }
#if UNITY_EDITOR
        // never delete project assets (materials / meshes / prefabs on disk) from a generation pass
        if (UnityEditor.EditorUtility.IsPersistent(o)) return;
#endif
        Object.DestroyImmediate(o);
    }

    static Material defaultLit;

    /// <summary>
    /// A material that really renders in the active pipeline. In the EDITOR GraphicsSettings.currentRenderPipeline.defaultMaterial
    /// (and a primitive's default sharedMaterial) is the URP Lit material, but in a BUILD URP returns null for it, so primitives
    /// fall back to the built-in "Standard" shader, which URP never draws (invisible objects). Always clone THIS instead.
    /// </summary>
    public static Material DefaultLit()
    {
        if (defaultLit != null) return defaultLit;
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (rp != null && rp.defaultMaterial != null && rp.defaultMaterial.shader != null && rp.defaultMaterial.shader.isSupported)
        { defaultLit = rp.defaultMaterial; return defaultLit; }
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (sh == null) sh = Shader.Find("Standard");
        defaultLit = new Material(sh) { name = "DefaultLit (runtime)" };
        return defaultLit;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { defaultLit = null; }

    public static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) Kill(t.GetChild(i).gameObject);
    }
}
