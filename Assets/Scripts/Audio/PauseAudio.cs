using UnityEngine;

/// <summary>While the game is paused: all game audio fades out fast and freezes (it resumes exactly where it was),
/// then Assets/Audio/Menu/pause_menu.mp3 fades in slowly. On resume the music fades out and the game audio fades back in.
/// Pause is detected from Time.timeScale == 0, or set explicitly with AudioDirector.SetPaused(bool).</summary>
public class PauseAudio : MonoBehaviour
{
    public float fadeOutSeconds = 0.2f;     // game audio -> silence (fast)
    public float musicFadeIn = 3f;          // pause music, gradual
    public float musicFadeOut = 0.35f;      // pause music on resume
    public float resumeFadeIn = 0.3f;       // game audio back in
    [Range(0f, 1f)] public float musicVolume = 0.7f;
    public bool autoDetectTimeScale = true;

    enum State { Running, FadingOut, Paused, MusicOut, ResumeIn }
    State state = State.Running;
    float t, musicT, musicFrom;
    AudioSource music;
    bool warned;

    bool Want { get { return AudioDirector.PauseRequested || (autoDetectTimeScale && Time.timeScale <= 0.001f); } }

    float MusicTarget { get { return musicVolume * AudioDirector.Volume("menu") * GameSettings.Master; } }   // music ignores the listener volume, so master is applied here
    static float M { get { return Mathf.Max(0.0001f, GameSettings.Master); } }

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    AudioClip FindClip()
    {
        SoundCatalog c = SoundCatalog.Instance;
        if (c == null) return null;
        System.Collections.Generic.List<SoundEntry> l = c.Get("menu");
        for (int i = 0; i < l.Count; i++) if (l[i].Has("pause")) return l[i].clip;
        return l.Count > 0 ? l[0].clip : null;
    }

    bool EnsureMusic()
    {
        if (music == null)
        {
            GameObject go = new GameObject("PauseMusic");
            go.transform.SetParent(transform, false);
            music = go.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            music.priority = 0;
            music.ignoreListenerPause = true;    // keeps playing while everything else is frozen
            music.ignoreListenerVolume = true;   // not affected by the game-audio fade
        }
        if (music.clip == null) music.clip = FindClip();
        if (music.clip == null && !warned)
        {
            warned = true;
            Debug.LogWarning("[Audio] Pause music not found: put it at Assets/Audio/Menu/pause_menu.mp3 and run Tools > Audio > Rebuild Sound Catalog.");
        }
        return music.clip != null;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool want = Want;
        switch (state)
        {
            case State.Running:
                AudioListener.volume = GameSettings.Master;   // keeps the player's master volume (this used to be forced back to 1)
                if (want) { state = State.FadingOut; t = 0f; EnsureMusic(); }
                break;

            case State.FadingOut:
                if (!want) { state = State.ResumeIn; t = resumeFadeIn * Mathf.Clamp01(AudioListener.volume / M); break; }
                t += dt;
                AudioListener.volume = (1f - Smooth(t / Mathf.Max(0.01f, fadeOutSeconds))) * GameSettings.Master;
                if (t >= fadeOutSeconds)
                {
                    AudioListener.pause = true;      // freeze every playing sound
                    AudioListener.volume = GameSettings.Master;
                    musicT = 0f;
                    if (EnsureMusic()) { music.volume = 0f; music.Play(); }
                    state = State.Paused;
                }
                break;

            case State.Paused:
                if (!want) { state = State.MusicOut; t = 0f; musicFrom = music != null ? music.volume : 0f; break; }
                musicT += dt;
                if (music != null && music.isPlaying) music.volume = MusicTarget * Smooth(musicT / Mathf.Max(0.01f, musicFadeIn));
                break;

            case State.MusicOut:
                if (want) { state = State.Paused; musicT = musicFadeIn * Mathf.Clamp01(musicFrom / Mathf.Max(0.0001f, MusicTarget)); break; }
                t += dt;
                if (music != null) music.volume = musicFrom * (1f - Smooth(t / Mathf.Max(0.01f, musicFadeOut)));
                if (t >= musicFadeOut)
                {
                    if (music != null) music.Stop();
                    AudioListener.volume = 0f;
                    AudioListener.pause = false;     // game audio continues from where it stopped
                    state = State.ResumeIn;
                    t = 0f;
                }
                break;

            case State.ResumeIn:
                if (want) { state = State.FadingOut; t = fadeOutSeconds * (1f - Mathf.Clamp01(AudioListener.volume / M)); break; }
                t += dt;
                AudioListener.volume = Smooth(t / Mathf.Max(0.01f, resumeFadeIn)) * GameSettings.Master;
                if (t >= resumeFadeIn) { AudioListener.volume = GameSettings.Master; state = State.Running; }
                break;
        }
    }

    void OnDisable()
    {
        AudioListener.pause = false;
        AudioListener.volume = GameSettings.Master;
        if (music != null) music.Stop();
        state = State.Running;
    }
}
