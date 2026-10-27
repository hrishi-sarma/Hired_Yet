using UnityEngine;

/// <summary>Soft procedural "tick" for every menu button (no audio asset needed, so it works the same in builds).
/// Plays while the game is paused too. Volume follows the player's sound settings.</summary>
public static class UiClick
{
    const float Level = 0.45f;
    static AudioSource src;
    static AudioClip clip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { src = null; clip = null; }

    public static void Play()
    {
        if (src == null)
        {
            var go = new GameObject("UiClick (Auto)"); Object.DontDestroyOnLoad(go); go.hideFlags = HideFlags.HideInHierarchy;
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false; src.spatialBlend = 0f; src.ignoreListenerPause = true;
        }
        if (clip == null) clip = Make();
        src.pitch = Random.Range(0.95f, 1.07f);
        src.PlayOneShot(clip, Level * GameSettings.Category("effects"));
    }

    static AudioClip Make()
    {
        const int sr = 44100;
        int n = (int)(sr * 0.07f);
        var d = new float[n];
        var rnd = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float tone = Mathf.Sin(6.2832f * (1300f * t - 5000f * t * t)) * Mathf.Exp(-t * 70f);        // short falling blip
            float tick = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 600f) * 0.5f;            // tiny noise transient
            d[i] = (tone * 0.8f + tick) * Mathf.Min(1f, (n - 1 - i) / 120f);                           // end fade: no pop
        }
        var c = AudioClip.Create("ui_click", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }
}
