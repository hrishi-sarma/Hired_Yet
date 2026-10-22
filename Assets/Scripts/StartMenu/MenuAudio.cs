using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stage 4D: the only sounds in the start menu are the daytime bird sounds and the night forest ambience, crossfaded by the
/// day/night state. Every other AudioSource that lives in the StartMenu scene is muted (UI objects excepted). Persistent
/// game audio (AudioDirector pool etc. in DontDestroyOnLoad) is never touched. Reads clips from the SoundCatalog:
/// day = Ambient/Forest "day" bed (falls back to Animals/Birds calls), night = Ambient/Forest "night" bed.
/// </summary>
[DisallowMultipleComponent]
public class MenuAudio : MonoBehaviour
{
    [Range(0f, 1f)] public float dayVolume = 0.55f;
    [Range(0f, 1f)] public float nightVolume = 0.6f;
    public float fadeInSeconds = 3f;
    public float crossfadeSpeed = 0.35f;
    static readonly string[] KeepRoots = { "Start Menu UI", "Post-it Menu", "EventSystem" };

    static MenuAudio instance;
    AudioSource day, night;
    float master, masterTarget = 1f, masterSpeed;
    float dayW, nightW, nextMute;
    bool started;

    /// <summary>Called by StartMenuUI when START is pressed.</summary>
    public static void FadeOut(float seconds)
    {
        if (instance == null) return;
        instance.masterTarget = 0f; instance.masterSpeed = 1f / Mathf.Max(0.05f, seconds);
    }

    void Awake()
    {
        instance = this;
        day = NewSource("Menu Day Birds"); night = NewSource("Menu Night Forest");
        masterSpeed = 1f / Mathf.Max(0.1f, fadeInSeconds);
    }

    void OnDestroy() { if (instance == this) instance = null; }

    AudioSource NewSource(string n)
    {
        var go = new GameObject(n); go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false; s.loop = true; s.spatialBlend = 0f; s.volume = 0f; s.bypassReverbZones = true;
        return s;
    }

    void Begin()
    {
        started = true;
        SoundCatalog cat = SoundCatalog.Instance;
        if (cat == null) { Debug.LogWarning("MenuAudio: no SoundCatalog (run Tools > Audio > Rebuild Sound Catalog)."); return; }
        AudioClip d = PickLoop(cat.Get("ambient", "forest"), "day");
        if (d == null) d = PickAny(cat.Get("animals", "birds"));
        AudioClip n = PickLoop(cat.Get("ambient", "forest"), "night");
        Setup(day, d); Setup(night, n);
    }

    static AudioClip PickLoop(List<SoundEntry> list, string tag)
    {
        var c = new List<AudioClip>();
        for (int i = 0; i < list.Count; i++) if (list[i].clip != null && !list[i].Has("park") && list[i].Has(tag)) c.Add(list[i].clip);
        return c.Count > 0 ? c[Random.Range(0, c.Count)] : null;
    }

    static AudioClip PickAny(List<SoundEntry> list)
    {
        var c = new List<AudioClip>();
        for (int i = 0; i < list.Count; i++) if (list[i].clip != null) c.Add(list[i].clip);
        return c.Count > 0 ? c[Random.Range(0, c.Count)] : null;
    }

    static void Setup(AudioSource s, AudioClip clip)
    {
        if (clip == null) return;
        s.clip = clip; s.time = Random.value * clip.length * 0.9f; s.Play();
    }

    void Update()
    {
        if (!started)
        {
            if (!MenuWorldBootstrap.Ready) return;   // wait for the loading screen to finish
            Begin();
        }
        float dt = Time.unscaledDeltaTime;
        master = Mathf.MoveTowards(master, masterTarget, masterSpeed * dt);

        float nf = Mathf.Clamp01(StylizedMaterialFactory.CurrentNight);
        dayW = Mathf.MoveTowards(dayW, Mathf.Sqrt(1f - nf), crossfadeSpeed * dt);
        nightW = Mathf.MoveTowards(nightW, Mathf.Sqrt(nf), crossfadeSpeed * dt);
        float amb = GameSettings.Category("ambient");   // menu birds / night forest follow the AMBIENCE slider
        day.volume = dayVolume * dayW * master * amb;
        night.volume = nightVolume * nightW * master * amb;
        if (day.clip != null && !day.isPlaying && day.volume > 0.001f) day.Play();
        if (night.clip != null && !night.isPlaying && night.volume > 0.001f) night.Play();
        if (day.volume <= 0.001f && day.isPlaying) day.Pause();
        if (night.volume <= 0.001f && night.isPlaying) night.Pause();

        if (Time.unscaledTime >= nextMute) { nextMute = Time.unscaledTime + 1f; MuteOthers(); }
    }

    void MuteOthers()
    {
        Scene mine = gameObject.scene;
        var all = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            AudioSource s = all[i];
            if (s == day || s == night || s.gameObject.scene != mine) continue;   // never touch DontDestroyOnLoad / other scenes
            Transform r = s.transform.root;
            bool keep = false;
            for (int k = 0; k < KeepRoots.Length; k++) if (r.name == KeepRoots[k]) { keep = true; break; }
            if (!keep && !s.mute) s.mute = true;
        }
    }
}
