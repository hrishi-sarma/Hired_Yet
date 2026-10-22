using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent black overlay that survives scene loads. ToBlack() fades the screen out, then runs a callback (start the load);
/// the LoadingScreen calls Reveal() as soon as it exists, so the loading screen comes up out of the dark instead of popping in.
/// Uses unscaled time, so it works while the world is paused.
/// </summary>
public class ScreenFade : MonoBehaviour
{
    static ScreenFade inst;
    Image img; float safety;

    static ScreenFade Get()
    {
        if (inst != null) return inst;
        var go = new GameObject("ScreenFade", typeof(Canvas), typeof(Image));
        DontDestroyOnLoad(go);
        var cv = go.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 32000;
        inst = go.AddComponent<ScreenFade>();
        inst.img = go.GetComponent<Image>(); inst.img.color = Color.clear; inst.img.raycastTarget = true;
        return inst;
    }

    /// <summary>Fade to black over dur seconds, then call done (e.g. begin loading). The overlay stays until Reveal().</summary>
    public static void ToBlack(float dur, Action done)
    {
        var f = Get(); f.StopAllCoroutines(); f.StartCoroutine(f.Run(0f, 1f, dur, done, false));
    }

    /// <summary>Fade the overlay away (no-op if none is up).</summary>
    public static void Reveal(float dur = 0.5f)
    {
        if (inst == null) return;
        inst.StopAllCoroutines(); inst.StartCoroutine(inst.Run(inst.img.color.a, 0f, dur, null, true));
    }

    IEnumerator Run(float from, float to, float dur, Action done, bool destroy)
    {
        for (float t = 0f; t < dur; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
        {
            float k = Mathf.Clamp01(t / dur); k = k * k * (3f - 2f * k);
            img.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, k));
            yield return null;
        }
        img.color = new Color(0f, 0f, 0f, to);
        if (destroy) { inst = null; WorldUtil.Kill(gameObject); yield break; }
        if (done != null) done();
        // safety: if nothing ever reveals us (load failed / no loading screen), don't leave the game black
        for (safety = 0f; safety < 20f; safety += Time.unscaledDeltaTime) yield return null;
        Reveal(0.5f);
    }
}
