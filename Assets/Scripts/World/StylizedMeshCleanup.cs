using UnityEngine;

/// <summary>Owns runtime mesh copies created by the stylized roof/wall submesh splitter.</summary>
public sealed class StylizedMeshCleanup : MonoBehaviour
{
    Mesh generated;
    Mesh original;

    public void Track(Mesh source, Mesh copy)
    {
        original = source;
        generated = copy;
    }

    void OnDestroy()
    {
        if (generated == null || generated == original) return;
        if (Application.isPlaying) WorldUtil.Kill(generated);
        else DestroyImmediate(generated);
        generated = null;
    }
}
