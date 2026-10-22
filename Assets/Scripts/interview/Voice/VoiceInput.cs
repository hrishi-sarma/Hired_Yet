using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

public enum VoiceState { Idle, Listening, Paused, Finishing, Review }

public class VoiceInput : MonoBehaviour
{
    [Serializable] class Payload { public string f; public string i; public float c; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int VoiceWeb_IsSupported();
    [DllImport("__Internal")] static extern void VoiceWeb_Arm();
    [DllImport("__Internal")] static extern void VoiceWeb_Start(string go, string lang);
    [DllImport("__Internal")] static extern void VoiceWeb_Stop();
#endif

    public static VoiceInput Instance { get; private set; }
    public string language = "en-US";

    public VoiceState State { get; private set; }
    public string Committed { get { return committed; } }
    public string Interim { get { return interim; } }
    public string Transcript { get { return (committed + " " + interim).Trim(); } }
    public bool HasText { get { return Transcript.Length > 0; } }
    public float LastConfidence { get; private set; } = -1f;
    public float ListenSeconds { get; private set; }
    public float MicLevel { get; private set; }
    public string MicStatus { get; private set; } = "";
    public bool MeterActive { get; private set; }

    public event Action Changed;
    public event Action<string> Failed;

    string committed = "", interim = "";
    bool ended;
    bool startFailed;

    public static bool IsSupported()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { return VoiceWeb_IsSupported() == 1; } catch (Exception) { return false; }
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return true;
#else
        return false;
#endif
    }

    public static VoiceInput Create()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("VoiceInput");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<VoiceInput>();
        return Instance;
    }

    public void Arm()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        VoiceWeb_Arm();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (VoskBackend.Available()) VoskBackend.Preload();
#endif
    }

    public void Unload()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        VoskBackend.Unload();
#endif
    }

    public void Begin()
    {
        committed = ""; interim = ""; LastConfidence = -1f; ListenSeconds = 0f;
        startFailed = false;
        StartBackend();
        State = startFailed ? VoiceState.Idle : VoiceState.Listening;
        Fire();
    }

    public void Pause()
    {
        if (State != VoiceState.Listening) return;
        FlushInterim(); StopBackend(); State = VoiceState.Paused; Fire();
    }

    public void Resume()
    {
        if (State != VoiceState.Paused) return;
        startFailed = false;
        StartBackend();
        State = startFailed ? VoiceState.Paused : VoiceState.Listening;
        Fire();
    }

    public void Stop()
    {
        if (State != VoiceState.Listening && State != VoiceState.Paused) return;
        bool wasListening = State == VoiceState.Listening;
        State = VoiceState.Finishing; Fire();
        StartCoroutine(Finish(wasListening));
    }

    public void ResetAll()
    {
        StopAllCoroutines();
        if (State == VoiceState.Listening || State == VoiceState.Finishing) StopBackend();
        committed = ""; interim = ""; State = VoiceState.Idle; Fire();
    }

    IEnumerator Finish(bool wasListening)
    {
        ended = !wasListening;
        if (wasListening) StopBackend();
        float t = 0f;
        while (!ended && t < 1.5f) { t += Time.unscaledDeltaTime; yield return null; }
        FlushInterim();
        if (HasText) State = VoiceState.Review;
        else { State = VoiceState.Idle; if (Failed != null) Failed("I didn't hear anything. Try again, or type your answer."); }
        Fire();
    }

    void Update()
    {
        if (State == VoiceState.Listening) ListenSeconds += Time.unscaledDeltaTime;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        PumpVosk();
#endif
    }

    void AppendFinal(string seg)
    {
        seg = (seg ?? "").Trim();
        if (seg.Length == 0) return;
        if (committed.EndsWith(seg, StringComparison.OrdinalIgnoreCase)) return;
        committed = (committed + " " + seg).Trim();
    }

    void FlushInterim() { AppendFinal(interim); interim = ""; }

    void Fire() { if (Changed != null) Changed(); }

    public void OnVoiceResult(string json)
    {
        Payload p = JsonUtility.FromJson<Payload>(json);
        if (p == null) return;
        AppendFinal(p.f);
        interim = p.i ?? "";
        if (p.c >= 0f) LastConfidence = p.c;
        Fire();
    }

    public void OnVoiceEnded(string _) { ended = true; }

    public void OnVoiceError(string code)
    {
        string msg;
        switch (code)
        {
            case "not-allowed": case "service-not-allowed": msg = "Microphone is blocked. Allow it in the browser, or type your answer."; break;
            case "nomic": msg = "No audio data from any microphone. Check that one is plugged in and enabled. Type your answer instead."; break;
            case "audio-capture": msg = "No microphone found. Type your answer instead."; break;
            case "nomodel": msg = "Voice model not found in StreamingAssets/vosk-model. Type your answer instead."; break;
            case "network": msg = "Speech service unreachable (needs internet). Type your answer instead."; break;
            default: msg = "Voice input isn't available right now. Type your answer instead."; break;
        }
        ended = true;
        startFailed = true;
        if (State == VoiceState.Listening) { FlushInterim(); State = HasText ? VoiceState.Paused : VoiceState.Idle; }
        if (Failed != null) Failed(msg);
        Fire();
    }

    void StartBackend()
    {
        ended = false;
#if UNITY_WEBGL && !UNITY_EDITOR
        VoiceWeb_Start(gameObject.name, language);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        StartWin();
#else
        OnVoiceError("unsupported");
#endif
    }

    void StopBackend()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        VoiceWeb_Stop();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        StopWin();
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    DictationRecognizer dict;
    readonly List<VoskBackend.Session> sessions = new List<VoskBackend.Session>();
    VoskBackend.Session latest;
    bool voskMode;

    void PumpVosk()
    {
        MeterActive = false;
        if (sessions.Count == 0) return;
        if (State == VoiceState.Listening && latest != null) { MeterActive = true; MicLevel = latest.Peak; MicStatus = latest.Status; }
        for (int k = sessions.Count - 1; k >= 0; k--)
        {
            VoskBackend.Session s = sessions[k];
            bool isLatest = s == latest;
            s.Pump(
                delegate (string t) { if (isLatest) { interim = t; Fire(); } },
                delegate (string t) { if (isLatest) interim = ""; AppendFinal(t); Fire(); },
                delegate (string e) { OnVoiceError(e); if (e == "nomic") StopWin(); });
            if (s.Done) sessions.RemoveAt(k);
        }
        if (State != VoiceState.Listening) ended = sessions.Count == 0;
    }

    void StartWin()
    {
        StopWin();
        voskMode = true;
        if (!VoskBackend.Available()) { Debug.LogError("[Voice] model not found. Expected an 'am' folder in " + VoskBackend.ModelPath); OnVoiceError("nomodel"); return; }
        VoskBackend.Session s = new VoskBackend.Session();
        if (s.StartError != null) { OnVoiceError(s.StartError); return; }
        sessions.Add(s);
        latest = s;
    }

    void StopWin()
    {
        if (latest != null) { latest.Stop(); }
        StopDictation();
        if (!voskMode) ended = true;
    }

    void StartDictation()
    {
        try
        {
            dict = new DictationRecognizer();
            dict.InitialSilenceTimeoutSeconds = 3600f;
            dict.AutoSilenceTimeoutSeconds = 3600f;
            dict.DictationHypothesis += delegate (string t) { interim = t; Fire(); };
            dict.DictationResult += delegate (string t, ConfidenceLevel c)
            {
                interim = ""; AppendFinal(t);
                LastConfidence = c == ConfidenceLevel.High ? 0.9f : (c == ConfidenceLevel.Medium ? 0.7f : 0.4f);
                Fire();
            };
            dict.DictationComplete += delegate (DictationCompletionCause cause)
            {
                ended = true;
                if (cause != DictationCompletionCause.Complete && cause != DictationCompletionCause.TimeoutExceeded && State == VoiceState.Listening)
                    OnVoiceError("network");
            };
            dict.DictationError += delegate (string err, int hr) { OnVoiceError("failed"); };
            dict.Start();
        }
        catch (Exception) { OnVoiceError("failed"); }
    }

    void StopDictation()
    {
        if (dict == null) return;
        try { if (dict.Status == SpeechSystemStatus.Running) dict.Stop(); dict.Dispose(); } catch (Exception) { }
        dict = null;
    }

    void OnDestroy()
    {
        StopWin();
        StopDictation();
        VoskBackend.Unload();
    }
#endif
}
