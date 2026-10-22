using System.Collections.Generic;
using UnityEngine;

/// <summary>Synthesised interviewer vocalisations: one short sound per FaceEmotion (hm, ha, sigh, gasp, grunt, tut...).
/// Override with real clips in Assets/Audio/Interviewer/&lt;Emotion&gt;/*.wav. Synth is public so greeting sounds can reuse it.</summary>
public static class InterviewerSounds
{
    /// <summary>One syllable. f0a/f0b = pitch Hz start/end (0 = noise only), f1/f2 = formants (noise-only: centre of the band).</summary>
    public struct Syl
    {
        public float dur, f0a, f0b, f1, f2, breath, amp, att, rel, gap, rough;
    }

    static Syl S(float dur, float f0a, float f0b, float f1, float f2, float breath, float amp, float att, float rel, float gap, float rough = 0f)
    {
        return new Syl { dur = dur, f0a = f0a, f0b = f0b, f1 = f1, f2 = f2, breath = breath, amp = amp, att = att, rel = rel, gap = gap, rough = rough };
    }

    static Syl[] Seq(FaceEmotion e)
    {
        switch (e)
        {
            case FaceEmotion.Friendly: return new[] { S(.14f, 130, 140, 350, 1200, .1f, 1, .02f, .6f, .05f), S(.2f, 140, 175, 400, 1300, .1f, 1, .02f, .6f, 0) };
            case FaceEmotion.Pleased: return new[] { S(.32f, 120, 170, 400, 1400, .1f, 1, .04f, .5f, 0) };
            case FaceEmotion.Delighted: return new[] { S(.1f, 200, 220, 700, 1200, .5f, 1, .01f, .8f, .08f), S(.1f, 215, 240, 700, 1200, .5f, 1, .01f, .8f, .08f), S(.12f, 230, 270, 700, 1200, .5f, 1, .01f, .9f, 0) };
            case FaceEmotion.Suspicious: return new[] { S(.5f, 105, 125, 320, 850, .15f, 1, .06f, .5f, 0) };
            case FaceEmotion.Doubt: return new[] { S(.32f, 110, 165, 400, 1000, .1f, 1, .04f, .5f, 0) };
            case FaceEmotion.Annoyed: return new[] { S(.05f, 0, 0, 3200, 4500, 1, .8f, .002f, 4f, .12f), S(.05f, 0, 0, 3200, 4500, 1, .7f, .002f, 4f, .06f), S(.22f, 100, 85, 500, 1000, .2f, .8f, .02f, .6f, 0, .5f) };
            case FaceEmotion.Angry: return new[] { S(.42f, 80, 68, 600, 1000, .15f, 1, .03f, .5f, 0, 1f) };
            case FaceEmotion.Bored: return new[] { S(.75f, 120, 90, 500, 1500, .75f, .8f, .12f, .9f, 0) };
            case FaceEmotion.Sad: return new[] { S(.55f, 170, 120, 600, 1000, .2f, .8f, .05f, .6f, 0) };
            case FaceEmotion.Disappointed: return new[] { S(.2f, 150, 140, 650, 1100, .15f, .9f, .03f, .6f, .03f), S(.4f, 140, 95, 600, 1000, .2f, .9f, .03f, .6f, 0) };
            case FaceEmotion.Shock: return new[] { S(.22f, 0, 0, 1800, 3000, 1, .7f, .19f, .15f, .03f), S(.16f, 220, 260, 800, 1300, .1f, 1, .01f, .7f, 0) };
            case FaceEmotion.Disgust: return new[] { S(.5f, 130, 105, 500, 900, .25f, .9f, .04f, .5f, 0, .6f) };
            case FaceEmotion.Smug: return new[] { S(.1f, 140, 150, 450, 1700, .45f, .7f, .01f, .8f, .09f), S(.12f, 135, 140, 450, 1700, .45f, .7f, .01f, .8f, 0) };
            case FaceEmotion.Worried: return new[] { S(.2f, 170, 150, 400, 900, .1f, .9f, .03f, .6f, .06f), S(.3f, 150, 120, 400, 900, .1f, .9f, .03f, .6f, 0) };
        }
        return null; // Neutral: silent
    }

    static float PitchMul(Mood m)
    {
        switch (m)
        {
            case Mood.Impatient: return 1.08f;
            case Mood.Paranoid: return 1.15f;
            case Mood.Bored: return 0.92f;
        }
        return 1f;
    }

    public static AudioClip Synth(string name, Syl[] seq, float pitch, float timeMul, int seed)
    {
        System.Random r = new System.Random(seed);
        int total = 0;
        for (int i = 0; i < seq.Length; i++) total += (int)((seq[i].dur * timeMul + seq[i].gap) * ProceduralSfx.Rate);
        float[] d = new float[total + 800];
        int pos = 0;
        for (int i = 0; i < seq.Length; i++)
        {
            Syl s = seq[i];
            int n = (int)(s.dur * timeMul * ProceduralSfx.Rate);
            bool noise = s.f0a <= 0f;
            float bw1 = noise ? 700f : 90f, bw2 = noise ? 900f : 130f;
            float r1 = Mathf.Exp(-Mathf.PI * bw1 / ProceduralSfx.Rate), r2 = Mathf.Exp(-Mathf.PI * bw2 / ProceduralSfx.Rate);
            float a11 = 2f * r1 * Mathf.Cos(6.2832f * s.f1 / ProceduralSfx.Rate), a12 = -r1 * r1;
            float a21 = 2f * r2 * Mathf.Cos(6.2832f * s.f2 / ProceduralSfx.Rate), a22 = -r2 * r2;
            float y11 = 0, y12 = 0, y21 = 0, y22 = 0, phase = 0;
            for (int k = 0; k < n; k++)
            {
                float u = k / (float)n, t = k / (float)ProceduralSfx.Rate;
                float f = Mathf.Lerp(s.f0a, s.f0b, u) * pitch * (1f + .015f * Mathf.Sin(6.2832f * 5.5f * t));
                if (s.rough > 0f) f *= 1f + s.rough * ((float)r.NextDouble() - .5f) * .12f;
                phase += f / ProceduralSfx.Rate; if (phase >= 1f) phase -= 1f;
                float nz = (float)r.NextDouble() * 2f - 1f;
                float x = noise ? nz : (1f - s.breath) * (2f * phase - 1f) + s.breath * nz * 1.5f;
                float y1 = (1f - r1) * x + a11 * y11 + a12 * y12; y12 = y11; y11 = y1;
                float y2 = (1f - r2) * x + a21 * y21 + a22 * y22; y22 = y21; y21 = y2;
                float env = Mathf.Min(1f, t / Mathf.Max(.002f, s.att)) * Mathf.Pow(1f - u, s.rel);
                d[pos + k] += (y1 + .6f * y2) * env * s.amp * (noise ? 2.2f : 5f);
            }
            pos += n + (int)(s.gap * ProceduralSfx.Rate);
        }
        return ProceduralSfx.Make(name, d);
    }

    static readonly Dictionary<FaceEmotion, AudioClip[]> gen = new Dictionary<FaceEmotion, AudioClip[]>();
    static readonly Dictionary<FaceEmotion, int> last = new Dictionary<FaceEmotion, int>();
    static AudioSource voice;
    static float lastT = -9f;
    static int lastEmo = -1;
    static float lastEmoT = -99f;
    /// <summary>The same emotion is never voiced twice in a row within this many seconds (a different one, or silence, instead).</summary>
    public static float sameEmotionGap = 10f;

    static AudioClip Pick(FaceEmotion e)
    {
        SoundCatalog c = SoundCatalog.Instance;
        int l;
        if (!last.TryGetValue(e, out l)) l = -1;
        if (c != null)
        {
            List<SoundEntry> list = c.Get("interviewer", e.ToString());
            if (list.Count > 0) { AudioClip a = SoundCatalog.Pick(list, ref l); last[e] = l; return a; }
        }
        AudioClip[] g;
        if (!gen.TryGetValue(e, out g))
        {
            Syl[] seq = Seq(e);
            if (seq != null)
            {
                g = new AudioClip[3];
                for (int i = 0; i < 3; i++)
                {
                    System.Random r = new System.Random((int)e * 131 + i * 17);
                    g[i] = Synth("iv_" + e + i, seq, 1f + ((float)r.NextDouble() - .5f) * .12f, 1f + ((float)r.NextDouble() - .5f) * .16f, (int)e * 7 + i);
                }
            }
            gen[e] = g;
        }
        AudioClip res = SoundCatalog.PickClip(g, ref l);
        last[e] = l;
        return res;
    }

    /// <summary>Plays the sound for an emotion at the interviewer's head. One voice at a time; force (outcomes) interrupts, otherwise a 0.6 s cooldown applies.</summary>
    public static void React(Transform head, FaceEmotion e, Mood mood, bool force = false, float chance = 1f)
    {
        if (head == null || AudioDirector.Instance == null) return;
        if (!force && (Time.unscaledTime - lastT < 0.6f || (voice != null && voice.isPlaying))) return;
        if (chance < 1f && Random.value > chance) return;
        if (!force && (int)e == lastEmo && Time.unscaledTime - lastEmoT < sameEmotionGap) return;
        AudioClip clip = Pick(e);
        if (clip == null) return;
        if (voice != null && voice.isPlaying) voice.Stop();
        float p = PitchMul(mood) * Random.Range(.97f, 1.03f);
        voice = AudioDirector.Player != null
            ? AudioDirector.PlayAt(clip, head.position, "interview", .9f, p, 3, 1.5f, 15f)
            : AudioDirector.Play2D(clip, "interview", .9f, p, 3);
        lastT = Time.unscaledTime;
        lastEmo = (int)e;
        lastEmoT = Time.unscaledTime;
    }
}
