using System.Collections.Generic;
using UnityEngine;

/// <summary>Stronger 3D cues for every positional voice (pool voices and car engines):
/// inverse-law distance falloff (distance is easier to judge than with Linear), air absorption (far = duller),
/// and a "behind you" low-pass (front/back is audible even with plain stereo panning).
/// If a spatializer plugin (Resonance / Steam Audio / Oculus) is selected in Project Settings > Audio, voices also use it for real HRTF.</summary>
public static class SpatialAudio
{
    /// <summary>How hard far sounds are darkened (cutoff at max distance), Hz.</summary>
    public static float farCutoff = 3500f;
    /// <summary>Cutoff multiplier for a source directly behind the listener (1 = no effect).</summary>
    public static float behindMul = 0.4f;
    /// <summary>Falloff exponent: 1 = physical inverse law, lower = gentler.</summary>
    public static float falloff = 0.75f;
    public static float doppler = 0.4f;

    static readonly Dictionary<int, AnimationCurve> curves = new Dictionary<int, AnimationCurve>();

    static AnimationCurve Curve(float min, float max)
    {
        float n0 = Mathf.Clamp(min / Mathf.Max(max, min + 0.1f), 0.01f, 0.9f);
        int key = Mathf.RoundToInt(n0 * 100f);
        AnimationCurve c;
        if (curves.TryGetValue(key, out c)) return c;
        n0 = key / 100f;
        c = new AnimationCurve();
        c.AddKey(0f, 1f);
        const int N = 14;
        for (int i = 0; i <= N; i++)
        {
            float n = Mathf.Lerp(n0, 1f, i / (float)N);
            float v = Mathf.Pow(n0 / n, falloff);
            if (n > 0.7f) { float f = 1f - (n - 0.7f) / 0.3f; v *= f * f; }   // smooth fade to silence at max distance
            c.AddKey(n, v);
        }
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        curves[key] = c;
        return c;
    }

    /// <summary>Makes an AudioSource a strongly positional voice and adds the distance/behind filter.</summary>
    public static void Configure(AudioSource s, float min, float max)
    {
        if (s == null) return;
        s.spatialBlend = 1f;
        s.spread = 0f;
        s.dopplerLevel = doppler;
        s.minDistance = min;
        s.maxDistance = Mathf.Max(max, min + 1f);
        s.rolloffMode = AudioRolloffMode.Custom;
        s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Curve(min, s.maxDistance));
        if (!string.IsNullOrEmpty(AudioSettings.GetSpatializerPluginName())) s.spatialize = true;
        if (s.GetComponent<SpatialVoice>() == null) s.gameObject.AddComponent<SpatialVoice>();
    }
}

/// <summary>Per-voice low-pass driven by distance and by how far behind the listener the source is.</summary>
[RequireComponent(typeof(AudioSource))]
public class SpatialVoice : MonoBehaviour
{
    AudioSource src;
    AudioLowPassFilter lpf;
    float cur = 22000f;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        lpf = GetComponent<AudioLowPassFilter>();
        if (lpf == null) lpf = gameObject.AddComponent<AudioLowPassFilter>();
        lpf.lowpassResonanceQ = 1f;
        lpf.enabled = false;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        Transform l = cam != null ? cam.transform : AudioDirector.Player;
        if (src == null || l == null || !src.isPlaying || src.spatialBlend < 0.5f) { if (lpf.enabled) { lpf.enabled = false; cur = 22000f; } return; }
        Vector3 d = transform.position - l.position;
        float dist = d.magnitude;
        if (dist < 1.5f) { if (lpf.enabled) { lpf.enabled = false; cur = 22000f; } return; }
        float k = Mathf.Clamp01(dist / Mathf.Max(1f, src.maxDistance));
        float target = Mathf.Lerp(22000f, SpatialAudio.farCutoff, Mathf.Sqrt(k));
        float back = Mathf.Clamp01(-Vector3.Dot(l.forward, d / dist));
        target *= Mathf.Lerp(1f, SpatialAudio.behindMul, back);
        target = Mathf.Max(1500f, target);
        cur = Mathf.Lerp(cur, target, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        lpf.cutoffFrequency = cur;
        lpf.enabled = true;
    }
}
