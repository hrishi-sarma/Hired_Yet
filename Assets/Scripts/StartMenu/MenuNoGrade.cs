using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Start menu only: no colour grading / tonemapping / vignette / bloom / split toning etc.
/// Every post-processing Volume in the scene is switched off (re-checked twice a second because the world's own
/// setup scripts create theirs at runtime). The lens flare (SunLensEffect) is not a Volume and keeps working.
/// </summary>
[DisallowMultipleComponent]
public class MenuNoGrade : MonoBehaviour
{
    float next;

    void Start()
    {
        var atm = FindFirstObjectByType<AtmosphereEffects>();
        if (atm != null) atm.gradeEnabled = false;
        Apply();
    }

    void LateUpdate()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.5f;
        Apply();
    }

    static void Apply()
    {
        var vols = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < vols.Length; i++) if (vols[i].enabled) vols[i].enabled = false;
    }
}
