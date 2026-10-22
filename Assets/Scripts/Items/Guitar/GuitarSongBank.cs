using System;
using System.Collections.Generic;
using UnityEngine;

namespace Items
{
    // Inspector-editable list of songs the guitar can play. Lives at Assets/Resources/GuitarSongBank.asset
    // (auto-created by the editor script). Add mp3s as slots in the Inspector.
    [CreateAssetMenu(fileName = "GuitarSongBank", menuName = "Items/Guitar Song Bank")]
    public class GuitarSongBank : ScriptableObject
    {
        public enum Order { Shuffle, Sequential }

        [Serializable]
        public class Slot
        {
            public AudioClip clip;
            [Tooltip("Shown on screen when the song starts. Empty = clip file name.")]
            public string title;
            [Range(0f, 1f)] public float volume = 1f;

            public bool Valid { get { return clip != null; } }
            public string DisplayTitle
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(title)) return title.Trim();
                    return clip != null ? clip.name.Replace('_', ' ').Replace('-', ' ').Trim() : "Untitled";
                }
            }
        }

        public List<Slot> slots = new List<Slot>();
        [Tooltip("Shuffle never repeats the previous tune back to back.")]
        public Order order = Order.Shuffle;
        [Range(0f, 1f)] public float masterVolume = 0.8f;
        [Range(0f, 2f)] public float fadeIn = 0.15f;
        [Range(0f, 2f)] public float fadeOut = 0.35f;
        [Tooltip("If the song ends while E is still held, loop it. Off = silence until E is pressed again.")]
        public bool loopWhileHeld = true;
        [Tooltip("Tempo of the songs. With Loop Bars it sets the exact loop length (3/4 or 4/4 is detected from the clip length).")]
        public float loopBpm = 120f;
        [Tooltip("Length of each song in bars; the loop is exactly this long, so repeats are gap-free.")]
        public int loopBars = 8;
        [Header("Song title on screen")]
        public float titleHold = 2.5f;
        public float titleFade = 1.2f;

        public const string ResourceName = "GuitarSongBank";
        static GuitarSongBank cached;
        [NonSerialized] int last = -1;

        public static GuitarSongBank Load()
        {
            if (cached == null) cached = Resources.Load<GuitarSongBank>(ResourceName);
            return cached;
        }

        public int ValidCount
        {
            get { int n = 0; for (int i = 0; i < slots.Count; i++) if (slots[i] != null && slots[i].Valid) n++; return n; }
        }

        // Index of the next tune to play, or -1 if no slot has a clip.
        public int Next()
        {
            var ok = new List<int>();
            for (int i = 0; i < slots.Count; i++) if (slots[i] != null && slots[i].Valid) ok.Add(i);
            if (ok.Count == 0) return -1;
            int pick;
            if (order == Order.Sequential)
            {
                pick = ok[0];
                for (int k = 0; k < ok.Count; k++) if (ok[k] > last) { pick = ok[k]; break; }
            }
            else
            {
                pick = ok[UnityEngine.Random.Range(0, ok.Count)];
                if (ok.Count > 1) while (pick == last) pick = ok[UnityEngine.Random.Range(0, ok.Count)];
            }
            last = pick;
            return pick;
        }
    }
}
