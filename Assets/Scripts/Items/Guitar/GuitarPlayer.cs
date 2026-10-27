using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Items
{
    // Runtime brain of the guitar. Hold E while the guitar is in hand to play; release to stop;
    // press again for a new tune. Later stages (animation, birds, interview) read the public state below.
    [DefaultExecutionOrder(900)]
    public class GuitarPlayer : MonoBehaviour
    {
        public const int GuitarId = 19;
        public static GuitarPlayer Instance { get; private set; }
        public static bool IsPlaying { get { return Instance != null && Instance.Playing; } }
        public static bool Lock;   // set by other systems (cutscenes) to block player-driven playing

        public static event Action<string> PlayStarted;
        public static event Action PlayStopped;

        public bool Held { get; private set; }       // guitar currently in the player's hand
        public bool Playing { get; private set; }    // E held and a tune is sounding (logical state, used by animation)
        public float Intensity { get; private set; } // 0..1 smoothed Playing, for blends
        public string CurrentTitle { get; private set; }
        public float ClipTime { get { return cur != null && cur.clip != null ? cur.time : 0f; } }
        public float ClipLevel { get; private set; } // 0..1 live loudness, for strum timing

        AudioSource src, src2, cur;
        // seamless looping: two sources chained with sample-accurate PlayScheduled over the trimmed (silence-free) part of the clip
        bool looping, nextQueued;
        double segEnd, loopLen;
        int trimStart;
        static readonly System.Collections.Generic.Dictionary<AudioClip, int[]> trims = new System.Collections.Generic.Dictionary<AudioClip, int[]>();
        ItemHold hold;
        GuitarSongBank bank;
        GuitarSongTitleUI titleUi;
        GuitarPoseDriver driver;
        FPAnimation.FPAnimationSystem fp;
        bool scripted, wasWant, armed, warned;
        float targetVol, baseVol, findTimer, startedAt;
        readonly float[] buf = new float[256];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            if (Instance != null) return;
            var go = new GameObject("GuitarPlayer");
            DontDestroyOnLoad(go);
            go.AddComponent<GuitarPlayer>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { WorldUtil.Kill(gameObject); return; }
            Instance = this;
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false; src.spatialBlend = 0f; src.volume = 0f;
            src2 = gameObject.AddComponent<AudioSource>();
            src2.playOnAwake = false; src2.spatialBlend = 0f; src2.volume = 0f;
            cur = src;
            titleUi = new GuitarSongTitleUI();
        }

        void OnDestroy() { if (Instance == this) { Instance = null; FPAnimation.FPEvents.GuitarHeld = false; FPAnimation.FPEvents.GuitarPlay = 0f; FPAnimation.FPEvents.GuitarLevel = 0f; } if (titleUi != null) titleUi.Dispose(); }

        // Lets a scripted sequence (interview) play without the E key.
        public void SetScriptedHold(bool on) { scripted = on; }

        static bool Blocked()
        {
            return Lock || Time.timeScale <= 0f || InventoryUI.IsOpen || DevConsole.Active || EventPanelUI.InputLock;
        }

        // Sprinting, jumping, sliding, swimming, climbing or falling lowers the guitar and stops the tune.
        bool Disturbed()
        {
            if (fp == null) return false;
            var v = fp.Loco;
            var st = v.state;
            return v.sprinting || st == FPAnimation.FPState.Jumping || st == FPAnimation.FPState.Sliding ||
                   st == FPAnimation.FPState.Swimming || st == FPAnimation.FPState.LedgeGrab || st == FPAnimation.FPState.Fall;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (hold == null || fp == null)
            {
                findTimer -= dt;
                if (findTimer <= 0f)
                {
                    findTimer = 0.5f;
                    if (hold == null) hold = FindAnyObjectByType<ItemHold>();
                    if (fp == null) fp = FindAnyObjectByType<FPAnimation.FPAnimationSystem>();
                }
            }
            var obj = hold != null ? hold.HeldObject : null;
            Held = obj != null && hold.Current != null && hold.Current.def.id == GuitarId;
            if (!Held) driver = null;
            else if (driver == null || driver.gameObject != obj) driver = obj.GetComponent<GuitarPoseDriver>();

            bool pose = Held && driver != null && driver.Ready && !Disturbed();   // two-handed ready pose whenever the guitar is held
            bool ok = pose && !Blocked();
            var kb = Keyboard.current;
            bool want = ok && (scripted || (kb != null && kb.eKey.isPressed));
            if (want) { if (!wasWant) armed = true; }
            else armed = false;
            if (driver != null) driver.Target = pose ? 1f : 0f;

            // the tune starts once the guitar is up in playing position
            if (armed && !Playing && driver != null && driver.Blend >= 0.85f) { armed = false; StartTune(); }
            else if (!want && Playing) StopTune();
            wasWant = want;

            // tune ran out (loop off): stop logically but wait for E to be released before a new tune
            if (Playing && !looping && Time.unscaledTime - startedAt > 0.3f && !src.isPlaying && !AudioListener.pause) StopTune();
            if (Playing && looping) TickLoop();

            Fade(dt);
            Intensity = Mathf.MoveTowards(Intensity, Playing ? 1f : 0f, dt * 6f);
            if (titleUi != null) titleUi.Tick(dt);
            FPAnimation.FPEvents.GuitarHeld = Held;
            FPAnimation.FPEvents.GuitarPlay = Intensity;
            FPAnimation.FPEvents.GuitarLevel = ClipLevel;
        }

        void StartTune()
        {
            if (bank == null) bank = GuitarSongBank.Load();
            if (bank == null || bank.ValidCount == 0)
            {
                if (!warned) { warned = true; Debug.LogWarning("[Guitar] No songs. Add mp3 slots in Resources/GuitarSongBank (Tools > Guitar > Select Song Bank)."); }
                return;
            }
            int i = bank.Next();
            if (i < 0) return;
            var slot = bank.slots[i];
            src.Stop(); src2.Stop();
            src.clip = slot.clip; src2.clip = slot.clip;
            src.loop = src2.loop = false;
            src.volume = src2.volume = 0f;
            cur = src;
            looping = bank.loopWhileHeld;
            if (looping)
            {
                int[] tr = Trim(slot.clip, bank.loopBpm, bank.loopBars);
                trimStart = tr[0];
                double outRate = AudioSettings.outputSampleRate;
                loopLen = System.Math.Round((tr[1] - tr[0]) * outRate / slot.clip.frequency) / outRate;   // whole output samples: no drift or overlap between passes
                if (loopLen < 0.5) { looping = false; trimStart = 0; }
            }
            if (looping)
            {
                double t0 = AudioSettings.dspTime + 0.04;
                src.timeSamples = trimStart; src.PlayScheduled(t0); src.SetScheduledEndTime(t0 + loopLen);
                segEnd = t0 + loopLen; nextQueued = false;
            }
            else { src.loop = false; src.Play(); }
            baseVol = bank.masterVolume * slot.volume;
            targetVol = baseVol * GameSettings.Category("effects");
            startedAt = Time.unscaledTime;
            Playing = true;
            CurrentTitle = slot.DisplayTitle;
            if (titleUi != null) titleUi.Show(CurrentTitle, bank.titleHold, bank.titleFade);
            if (PlayStarted != null) PlayStarted(CurrentTitle);
        }

        // queue the next pass slightly before the current one ends; both edges are sample-exact
        void TickLoop()
        {
            double now = AudioSettings.dspTime;
            var other = cur == src ? src2 : src;
            if (!nextQueued && now > segEnd - 0.4)
            {
                other.timeSamples = trimStart; other.PlayScheduled(segEnd); other.SetScheduledEndTime(segEnd + loopLen);
                nextQueued = true;
            }
            if (nextQueued && now >= segEnd) { cur = other; segEnd += loopLen; nextQueued = false; }
        }

        // Loop window [start, end) in clip samples. Length comes from the music (bars x beats x 60/bpm, 3/4 or 4/4 picked from the
        // clip length), so every pass is exactly one musical loop; the start skips the mp3 encoder delay. Falls back to silence trimming.
        static int[] Trim(AudioClip c, float bpm, int bars)
        {
            int[] r;
            if (trims.TryGetValue(c, out r)) return r;
            r = new[] { 0, c.samples };
            int first = -1, last = -1;
            try
            {
                if (c.loadType == AudioClipLoadType.Streaming) throw new System.Exception("streaming clip");
                if (c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
                int ch = c.channels, chunk = 32768;
                var d = new float[chunk * ch];
                const float th = 0.003f;
                for (int pos = 0; pos < c.samples && first < 0; pos += chunk)
                {
                    int n = Mathf.Min(chunk, c.samples - pos);
                    if (!c.GetData(d, pos)) throw new System.Exception("GetData failed");
                    for (int i = 0; i < n * ch; i++) if (Mathf.Abs(d[i]) > th) { first = pos + i / ch; break; }
                }
                for (int pos = Mathf.Max(0, c.samples - chunk); pos >= 0 && last < 0; pos -= chunk)
                {
                    int n = Mathf.Min(chunk, c.samples - pos);
                    if (!c.GetData(d, pos)) throw new System.Exception("GetData failed");
                    for (int i = n * ch - 1; i >= 0; i--) if (Mathf.Abs(d[i]) > th) { last = pos + i / ch + 1; break; }
                    if (pos == 0) break;
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[Guitar] Could not read '" + c.name + "' (" + ex.Message + "). Run Tools > Guitar > Fix Song Import Settings for a gap-free loop."); }

            double clipLen = c.samples / (double)c.frequency, best = double.MaxValue, bestLen = 0;
            if (bpm > 1f && bars > 0)
                for (int beats = 3; beats <= 4; beats++)
                {
                    double len = bars * beats * 60.0 / bpm, diff = System.Math.Abs(len - clipLen);
                    if (len <= clipLen + 0.02 && diff < best) { best = diff; bestLen = len; }
                }
            if (bestLen > 0)
            {
                int len = Mathf.RoundToInt((float)(bestLen * c.frequency));
                int start = first >= 0 ? Mathf.Min(first, Mathf.RoundToInt(0.06f * c.frequency)) : 0;
                start = Mathf.Clamp(start, 0, Mathf.Max(0, c.samples - len));
                r = new[] { start, start + len };
            }
            else if (first >= 0 && last > first) r = new[] { first, last };
            trims[c] = r;
            return r;
        }

        void StopTune()
        {
            Playing = false;
            targetVol = 0f;
            if (PlayStopped != null) PlayStopped();
        }

        void Fade(float dt)
        {
            if (src == null) return;
            if (Playing) targetVol = baseVol * GameSettings.Category("effects");   // ITEMS & GUITAR slider works while a song plays
            float vol = bank != null ? Mathf.Max(0.0001f, bank.masterVolume) : 1f;
            float secs = Playing ? (bank != null ? bank.fadeIn : 0.15f) : (bank != null ? bank.fadeOut : 0.35f);
            src.volume = Mathf.MoveTowards(src.volume, targetVol, dt * vol / Mathf.Max(0.01f, secs));
            src2.volume = src.volume;
            if (!Playing && src.volume <= 0.001f && (src.isPlaying || src2.isPlaying)) { src.Stop(); src2.Stop(); }

            // cheap loudness meter for strum timing
            if (Playing && cur.isPlaying)
            {
                cur.GetOutputData(buf, 0);
                float sum = 0f;
                for (int k = 0; k < buf.Length; k++) sum += buf[k] * buf[k];
                ClipLevel = Mathf.Lerp(ClipLevel, Mathf.Clamp01(Mathf.Sqrt(sum / buf.Length) * 6f), 0.35f);
            }
            else ClipLevel = Mathf.Lerp(ClipLevel, 0f, 0.2f);
        }
    }
}
