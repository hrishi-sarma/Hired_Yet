using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

public static class VoskBackend
{
    const string Lib = "libvosk";
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr vosk_model_new(byte[] path);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern void vosk_model_free(IntPtr model);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr vosk_recognizer_new(IntPtr model, float rate);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern void vosk_recognizer_free(IntPtr rec);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int vosk_recognizer_accept_waveform_s(IntPtr rec, short[] data, int length);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr vosk_recognizer_result(IntPtr rec);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr vosk_recognizer_partial_result(IntPtr rec);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr vosk_recognizer_final_result(IntPtr rec);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern void vosk_set_log_level(int level);

    static readonly object gate = new object();
    static IntPtr model = IntPtr.Zero;
    static string modelPath;
    static int users;

    public static string ModelPath
    {
        get { if (modelPath == null || !Directory.Exists(Path.Combine(modelPath, "am"))) modelPath = Find(); return modelPath; }
    }

    static string Find()
    {
        try
        {
            string root = Path.Combine(Application.streamingAssetsPath, "vosk-model");
            if (Directory.Exists(Path.Combine(root, "am"))) return root;
            if (Directory.Exists(root))
                foreach (string d in Directory.GetDirectories(root))
                    if (Directory.Exists(Path.Combine(d, "am"))) return d;
            return root;
        }
        catch (Exception) { return ""; }
    }

    public static bool Available()
    {
        try { return Directory.Exists(Path.Combine(ModelPath, "am")); }
        catch (Exception) { return false; }
    }

    public static void Preload()
    {
        if (model != IntPtr.Zero) return;
        string p = ModelPath;
        ThreadPool.QueueUserWorkItem(delegate { try { EnsureModel(p); } catch (Exception e) { Debug.LogError("[Voice] " + e); } });
    }

    public static void Unload()
    {
        if (!Monitor.TryEnter(gate)) return;
        try
        {
            if (users == 0 && model != IntPtr.Zero) { vosk_model_free(model); model = IntPtr.Zero; }
        }
        finally { Monitor.Exit(gate); }
    }

    static IntPtr EnsureModel(string path)
    {
        lock (gate)
        {
            if (model == IntPtr.Zero)
            {
                vosk_set_log_level(-1);
                model = vosk_model_new(Encoding.UTF8.GetBytes(path + "\0"));
            }
            return model;
        }
    }

    static string Utf8(IntPtr p)
    {
        if (p == IntPtr.Zero) return "";
        int n = 0;
        while (Marshal.ReadByte(p, n) != 0) n++;
        byte[] b = new byte[n];
        Marshal.Copy(p, b, 0, n);
        return Encoding.UTF8.GetString(b);
    }

    public static void AddUser() { Interlocked.Increment(ref users); }

    public class Session
    {
        public const int Partial = 0, Final = 1, Error = 2, Ended = 3;

        readonly ConcurrentQueue<short[]> audio = new ConcurrentQueue<short[]>();
        readonly ConcurrentQueue<KeyValuePair<int, string>> output = new ConcurrentQueue<KeyValuePair<int, string>>();
        readonly string path;
        readonly List<string> devs = new List<string>();
        int devIdx;
        string dev;
        AudioClip clip;
        int last;
        int rate = 16000;
        double phase;
        float t0;
        bool got, settled;
        float peakMax;
        volatile bool run = true;
        string lastPartial = "";

        public bool Done { get; private set; }
        public string StartError { get; private set; }
        public float Peak { get; private set; }

        public string Status
        {
            get
            {
                if (clip == null) return "";
                float age = Time.unscaledTime - t0;
                if (!got && age > 1f) return "no data";
                if (got && peakMax <= 0f && age > 1.5f) return "silent";
                return "";
            }
        }

        public Session()
        {
            devs.Add(null);
            foreach (string d in Microphone.devices) devs.Add(d);
            if (Microphone.devices.Length == 0) { StartError = "audio-capture"; Done = true; return; }
            path = ModelPath;
            devIdx = 0;
            if (!Open(0) && !Next()) { StartError = "audio-capture"; Done = true; return; }
            AddUser();
            Thread th = new Thread(Work);
            th.IsBackground = true;
            th.Priority = System.Threading.ThreadPriority.BelowNormal;
            th.Start();
        }

        bool Open(int i)
        {
            dev = devs[i];
            clip = Microphone.Start(dev, true, 10, 16000);
            if (clip == null) return false;
            rate = clip.frequency;
            last = 0;
            phase = 0;
            got = false;
            peakMax = 0f;
            t0 = Time.unscaledTime;
            Debug.Log("[Voice] mic '" + (dev ?? "default") + "' rate " + rate);
            return true;
        }

        bool Next()
        {
            if (clip != null) { Microphone.End(dev); clip = null; }
            for (int i = devIdx + 1; i < devs.Count; i++)
            {
                devIdx = i;
                if (Open(i)) return true;
            }
            return false;
        }

        public void Stop()
        {
            if (clip != null)
            {
                ReadMic();
                Microphone.End(dev);
                clip = null;
            }
            run = false;
        }

        void ReadMic()
        {
            if (clip == null) return;
            int pos = Microphone.GetPosition(dev);
            if (pos == last) return;
            got = true;
            float[] buf;
            if (pos > last)
            {
                buf = new float[pos - last];
                clip.GetData(buf, last);
            }
            else
            {
                float[] a = new float[clip.samples - last];
                clip.GetData(a, last);
                float[] b = new float[pos];
                if (pos > 0) clip.GetData(b, 0);
                buf = new float[a.Length + b.Length];
                Array.Copy(a, 0, buf, 0, a.Length);
                Array.Copy(b, 0, buf, a.Length, b.Length);
            }
            last = pos;
            float pk = 0f;
            for (int i = 0; i < buf.Length; i++) { float v = Mathf.Abs(buf[i]); if (v > pk) pk = v; }
            if (pk > peakMax) peakMax = pk;
            Peak = Mathf.Max(pk, Peak * 0.85f);
            audio.Enqueue(ToPcm16(buf));
        }

        readonly AudioClean clean = new AudioClean();
        float lp1, lp2;

        short[] ToPcm16(float[] src)
        {
            if (rate > 16000)
            {
                // anti-alias (2 x one-pole low-pass at 7 kHz) before decimating
                float al = 1f - (float)Math.Exp(-2.0 * Math.PI * 7000.0 / rate);
                for (int i = 0; i < src.Length; i++)
                {
                    lp1 += al * (src[i] - lp1);
                    lp2 += al * (lp1 - lp2);
                    src[i] = lp2;
                }
            }
            double step = rate / 16000.0;
            int count = (int)Math.Ceiling((src.Length - phase) / step);
            if (count < 0) count = 0;
            float[] o = new float[count];
            int n = 0;
            while (phase < src.Length && n < count)
            {
                int i = (int)phase;
                float f = (float)(phase - i);
                float a = src[i];
                float b = i + 1 < src.Length ? src[i + 1] : a;
                o[n++] = a + (b - a) * f;
                phase += step;
            }
            phase -= src.Length;
            return clean.Process(o, n);
        }

        void CheckDevice()
        {
            if (clip == null || settled) return;
            float age = Time.unscaledTime - t0;
            bool noData = !got && age > 1.5f;
            bool zeros = got && peakMax <= 0f && age > 2.5f;
            if (!noData && !zeros) { if (got && peakMax > 0f) settled = true; return; }
            bool hadData = got;
            if (Next()) return;
            settled = true;
            if (hadData) { devIdx = 0; Open(0); return; }
            run = false;
            output.Enqueue(new KeyValuePair<int, string>(Error, "nomic"));
        }

        public void Pump(Action<string> onPartial, Action<string> onFinal, Action<string> onError)
        {
            ReadMic();
            CheckDevice();
            KeyValuePair<int, string> m;
            while (output.TryDequeue(out m))
            {
                if (m.Key == Ended) { Done = true; continue; }
                if (m.Key == Error) { onError(m.Value); continue; }
                JsonText j = JsonUtility.FromJson<JsonText>(m.Value);
                if (j == null) continue;
                if (m.Key == Partial) onPartial(j.partial ?? "");
                else if (!string.IsNullOrEmpty(j.text)) onFinal(j.text);
            }
        }

        [Serializable] class JsonText { public string partial; public string text; }

        void Work()
        {
            IntPtr rec = IntPtr.Zero;
            try
            {
                IntPtr m = EnsureModel(path);
                if (m == IntPtr.Zero) { Debug.LogError("[Voice] model failed to load from " + path); output.Enqueue(new KeyValuePair<int, string>(Error, "failed")); return; }
                rec = vosk_recognizer_new(m, 16000f);
                while (true)
                {
                    short[] b;
                    if (audio.TryDequeue(out b))
                    {
                        if (vosk_recognizer_accept_waveform_s(rec, b, b.Length) != 0)
                        {
                            output.Enqueue(new KeyValuePair<int, string>(Final, Utf8(vosk_recognizer_result(rec))));
                            lastPartial = "";
                        }
                        else if (audio.IsEmpty)
                        {
                            string pj = Utf8(vosk_recognizer_partial_result(rec));
                            if (pj != lastPartial) { lastPartial = pj; output.Enqueue(new KeyValuePair<int, string>(Partial, pj)); }
                        }
                    }
                    else if (!run) break;
                    else Thread.Sleep(15);
                }
                output.Enqueue(new KeyValuePair<int, string>(Final, Utf8(vosk_recognizer_final_result(rec))));
            }
            catch (Exception e) { Debug.LogError("[Voice] " + e); output.Enqueue(new KeyValuePair<int, string>(Error, "failed")); }
            finally
            {
                if (rec != IntPtr.Zero) vosk_recognizer_free(rec);
                Interlocked.Decrement(ref users);
                output.Enqueue(new KeyValuePair<int, string>(Ended, ""));
            }
        }
    }
}
