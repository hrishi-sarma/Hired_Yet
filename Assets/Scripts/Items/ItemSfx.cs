using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Items
{
    // Stage 10: procedural SFX (no audio assets needed) + short gamepad haptics.
    public static class ItemSfx
    {
        public enum Kind { Paper, Card, Scribble, Pop, Chime }
        static AudioSource src; static AudioClip[] clips = new AudioClip[5];
        public static float Volume = 0.6f;
        public static bool Haptics = true;

        public static void Play(Kind k, float volume = 1f)
        {
            if (src == null)
            {
                var go = new GameObject("ItemSfx"); Object.DontDestroyOnLoad(go); go.hideFlags = HideFlags.HideInHierarchy;
                src = go.AddComponent<AudioSource>(); src.spatialBlend = 0f; src.playOnAwake = false;
            }
            var c = clips[(int)k] ?? (clips[(int)k] = Make(k));
            src.PlayOneShot(c, Volume * volume * GameSettings.Category("effects"));
        }

        // Sound that suits an item form (card flick vs paper rustle).
        public static void PlayFor(ItemInstance it, float volume = 1f)
        {
            if (it == null) return;
            Play(it.def.form == ItemForm.Card ? Kind.Card : Kind.Paper, volume);
            if (it.def.rarity >= Rarity.Rare) Play(Kind.Chime, 0.5f);
        }

        public static void Haptic(float low, float high, float seconds)
        {
            var pad = Gamepad.current;
            if (!Haptics || pad == null) return;
            pad.SetMotorSpeeds(low, high);
            Runner.Instance.StartCoroutine(Stop(pad, seconds));
        }
        static IEnumerator Stop(Gamepad pad, float s) { yield return new WaitForSecondsRealtime(s); if (pad != null) pad.SetMotorSpeeds(0f, 0f); }

        class Runner : MonoBehaviour
        {
            static Runner inst;
            public static Runner Instance { get { if (inst == null) { var g = new GameObject("ItemSfxRunner"); Object.DontDestroyOnLoad(g); g.hideFlags = HideFlags.HideInHierarchy; inst = g.AddComponent<Runner>(); } return inst; } }
        }

        static AudioClip Make(Kind k)
        {
            const int sr = 22050; var rnd = new System.Random(100 + (int)k);
            float len = k == Kind.Paper ? 0.28f : k == Kind.Card ? 0.09f : k == Kind.Scribble ? 0.4f : k == Kind.Pop ? 0.12f : 0.45f;
            int n = (int)(sr * len); var d = new float[n]; float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n, s = 0f, noise = (float)(rnd.NextDouble() * 2 - 1);
                switch (k)
                {
                    case Kind.Paper: lp += (noise - lp) * 0.35f; s = (noise - lp) * 0.9f * (0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 23f))) * Mathf.Pow(1f - t, 1.5f); break;
                    case Kind.Card: s = noise * Mathf.Exp(-t * 14f) * 0.9f; break;
                    case Kind.Scribble: lp += (noise - lp) * 0.2f; s = lp * 1.6f * (0.5f + 0.5f * Mathf.Sin(t * len * 120f)) * Mathf.Sin(t * Mathf.PI); break;
                    case Kind.Pop: s = Mathf.Sin(2f * Mathf.PI * (380f + 420f * t) * t * len) * Mathf.Exp(-t * 6f) * 0.8f; break;
                    default: s = (Mathf.Sin(2f * Mathf.PI * 1318f * t * len) + 0.6f * Mathf.Sin(2f * Mathf.PI * 1975f * t * len)) * Mathf.Exp(-t * 5f) * 0.35f; break;
                }
                d[i] = Mathf.Clamp(s, -1f, 1f);
            }
            var c = AudioClip.Create("ItemSfx_" + k, n, 1, sr, false); c.SetData(d, 0); return c;
        }
    }
}
