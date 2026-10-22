using System;
using System.Collections.Generic;
using UnityEngine;

public class AudioDirector : MonoBehaviour
{
    public static AudioDirector Instance { get; private set; }
    public static Transform Player { get; private set; }
    public static ZoneType Zone { get; private set; }
    public static float Night { get; private set; }
    public static bool WorldActive { get; private set; }
    public static event Action<ZoneType> ZoneChanged;

    /// <summary>True while the pause menu should own the audio. Time.timeScale == 0 is also treated as paused automatically.</summary>
    public static bool PauseRequested { get; private set; }
    /// <summary>Call from the pause menu (true on open, false on close) if it does not set Time.timeScale to 0.</summary>
    public static void SetPaused(bool paused) { PauseRequested = paused; }

    [Range(0f, 1f)] public float master = 1f;
    public int maxVoices = 16;
    public float tickInterval = 0.2f;

    readonly Dictionary<string, float> volumes = new Dictionary<string, float>();
    SoundPool pool;
    float nextTick, nextFind;

    public static Vector3 Focus
    {
        get { return Player != null ? Player.position : Vector3.zero; }
    }

    public static bool IsNight
    {
        get { return Night > 0.5f; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        AudioConfiguration cfg = AudioSettings.GetConfiguration();
        if (cfg.numRealVoices > 32 || cfg.numVirtualVoices > 64)
        {
            cfg.numRealVoices = Mathf.Min(cfg.numRealVoices, 32);
            cfg.numVirtualVoices = Mathf.Min(cfg.numVirtualVoices, 64);
            AudioSettings.Reset(cfg);
        }
        new GameObject("AudioDirector").AddComponent<AudioDirector>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            WorldUtil.Kill(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        pool = new SoundPool(transform, maxVoices);
        gameObject.AddComponent<AmbientZones>();
        gameObject.AddComponent<FootstepSounds>();
        gameObject.AddComponent<PauseAudio>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [ContextMenu("Log Audio Stats")]
    public void LogStats()
    {
        Debug.Log("[Audio] pool voices " + (pool != null ? pool.Active : 0) + "/" + maxVoices + ", engines " + CarSounds.LiveEngines + "/" + CarSounds.MaxEngines + ", generated clips " + (CarSounds.GeneratedBytes / 1024) + " KB, zone " + Zone + ", night " + Night.ToString("0.00"));
    }

    public static float Volume(string category)
    {
        float m = Instance != null ? Instance.master : 1f;
        float v;
        m *= GameSettings.Category(category);   // the player's per-kind volume (master is applied on the AudioListener)
        if (Instance != null && category != null && Instance.volumes.TryGetValue(category.ToLowerInvariant(), out v)) return m * v;
        return m;
    }

    public static void SetVolume(string category, float value)
    {
        if (Instance == null || category == null) return;
        Instance.volumes[category.ToLowerInvariant()] = Mathf.Clamp01(value);
    }

    public static AudioSource PlayAt(AudioClip clip, Vector3 pos, string category, float volume = 1f, float pitch = 1f, int priority = 0, float minDist = 3f, float maxDist = 40f)
    {
        return Instance != null ? Instance.pool.Play(clip, pos, category, volume, pitch, priority, minDist, maxDist, true) : null;
    }

    public static AudioSource Play2D(AudioClip clip, string category, float volume = 1f, float pitch = 1f, int priority = 0)
    {
        return Instance != null ? Instance.pool.Play(clip, Focus, category, volume, pitch, priority, 1f, 2f, false) : null;
    }

    static Transform FindPlayer()
    {
        if (WorldLauncher.Player != null) return WorldLauncher.Player;
        GameObject go = null;
        try
        {
            go = GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
        }
        return go != null ? go.transform : null;
    }

    void Update()
    {
        if (Time.unscaledTime < nextTick) return;
        nextTick = Time.unscaledTime + tickInterval;
        if (Player == null)
        {
            WorldActive = false;
            if (Time.unscaledTime < nextFind) return;
            nextFind = Time.unscaledTime + 1f;
            Player = FindPlayer();
            if (Player == null) return;
        }
        NpcDirector d = NpcDirector.Instance;
        ZoneMap zm = d != null && d.Ctx != null ? d.Ctx.zoneMap : null;
        WorldActive = zm != null;
        if (!WorldActive) return;
        Night = TrafficClock.NightFactor;
        Vector3 p = Player.position;
        ZoneType z = zm.ZoneAt(p.x, p.z);
        if (z != Zone)
        {
            Zone = z;
            if (ZoneChanged != null) ZoneChanged(z);
        }
    }
}
