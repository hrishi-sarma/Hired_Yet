using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Small "meaning" layer, pure C# (works on WebGL, no model runtime). Uses 15k GloVe-100 word vectors stored in
/// Resources/PersonaVec.bytes (~1.5 MB). Two jobs: Topic() maps an unmatched question to the closest topic by meaning,
/// Classify() tells what kind of answer the player gave (money, passion, ...). If the file is missing everything returns null.
/// </summary>
public static class Semantic
{
    const int D = 100;
    static bool loaded, ok;
    static readonly Dictionary<string, int> idx = new Dictionary<string, int>();
    static sbyte[] vec;
    static float[] inv;
    static Dictionary<string, int[]> prof;      // topic id -> content word ids
    static Dictionary<string, int[]> seeds;     // answer class -> seed word ids

    static readonly HashSet<string> Stop = new HashSet<string>((
        "what do does did you your yours yourself is are was were the a an to of in on for with at how why when where who which can could would will " +
        "i me my it its this that have has had be been so and or about tell any ever much many there here am im ive id ill like just really kind sort lot some get got go going take").Split(' '));
    static readonly HashSet<string> Neg = new HashSet<string>("not dont never isnt aint cant wasnt hardly no cannot didnt wont arent".Split(' '));

    static readonly string[][] SeedWords = {
        new[] { "money",     "money pay salary paycheck cash income wage earn earning rich paid" },
        new[] { "passion",   "love passion enjoy interest interested curious fascinated excited dream passionate fun" },
        new[] { "growth",    "career grow growth learn learning experience skills opportunity advance promotion future develop" },
        new[] { "stability", "stable stability secure security steady benefits safe insurance" },
        new[] { "need",      "need desperate unemployed jobless broke rent bills survive starving" },
        new[] { "referral",  "friend buddy referral recruiter recommended advertisement online website saw ad linkedin posted cousin told mentioned neighbor" },
        new[] { "company",   "company reputation mission values culture brand team office respected famous" },
        new[] { "local",     "close near nearby commute location walking distance" },
        new[] { "pos",       "good great fine happy excellent wonderful awesome amazing nice" },
        new[] { "neg",       "bad terrible awful sad stressed sick horrible miserable worse" },
        new[] { "looks",     "eyes pretty beautiful handsome gorgeous hair smile cute attractive lovely stunning dress tie suit perfume" },
        new[] { "flirt",     "date dinner kiss marry sexy hot single hug cuddle girlfriend boyfriend" },
        new[] { "praise",    "smart clever kind brilliant genius awesome wonderful amazing impressive wise funny great fantastic talented" },
        new[] { "concern",   "down sad upset angry mad wrong bothering matter okay tired grumpy mood unhappy depressed gloomy" },
    };

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        TextAsset ta = Resources.Load<TextAsset>("PersonaVec");
        if (ta == null) { Debug.LogWarning("[Semantic] Resources/PersonaVec.bytes not found, meaning layer is off"); return; }
        byte[] b = ta.bytes;
        int n = System.BitConverter.ToInt32(b, 0), d = System.BitConverter.ToInt32(b, 4);
        if (d != D || n <= 0) return;
        vec = new sbyte[n * D];
        inv = new float[n];
        int p = 8;
        for (int i = 0; i < n; i++)
        {
            int len = b[p++];
            string w = Encoding.ASCII.GetString(b, p, len); p += len;
            double s = 0;
            for (int k = 0; k < D; k++) { sbyte v = unchecked((sbyte)b[p + k]); vec[i * D + k] = v; s += v * v; }
            p += D;
            inv[i] = (float)(1.0 / System.Math.Sqrt(s + 1e-9));
            idx[w] = i;
        }
        seeds = new Dictionary<string, int[]>();
        foreach (string[] sw in SeedWords) seeds[sw[0]] = Ids(sw[1].Split(' '));
        prof = new Dictionary<string, int[]>();
        foreach (string line in PersonaData.Topics)
        {
            string[] f = line.Split('~');
            var all = new List<string>();
            foreach (string ph in f[1].Split('|')) foreach (string w in ph.Split(' ')) if (w != "$") all.Add(w);
            prof[f[0]] = Ids(all.ToArray());
        }
        ok = true;
    }

    public static bool Ready { get { Load(); return ok; } }

    static int Find(string w)
    {
        int i;
        if (idx.TryGetValue(w, out i)) return i;
        if (w.Length > 3 && w.EndsWith("s") && idx.TryGetValue(w.Substring(0, w.Length - 1), out i)) return i;
        return -1;
    }

    static int[] Ids(string[] words)
    {
        var l = new List<int>();
        foreach (string w in words) { if (Stop.Contains(w)) continue; int i = Find(w); if (i >= 0 && !l.Contains(i)) l.Add(i); }
        return l.ToArray();
    }

    static float Sim(int a, int b)
    {
        int x = a * D, y = b * D, s = 0;
        for (int k = 0; k < D; k++) s += vec[x + k] * vec[y + k];
        return s * inv[a] * inv[b];
    }

    static float Best(int q, int[] set)
    {
        float m = -1f;
        for (int i = 0; i < set.Length; i++) { float s = q == set[i] ? 1f : Sim(q, set[i]); if (s > m) m = s; }
        return m;
    }

    static string[] Split(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in (s ?? "").ToLowerInvariant())
        {
            if (c == '\'' || c == '\u2019') continue;
            sb.Append(c >= 'a' && c <= 'z' ? c : ' ');
        }
        return sb.ToString().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Closest topic id for a question by meaning, or null when unsure (the caller falls back to a generic reply).</summary>
    public static string Topic(string clause)
    {
        Load();
        if (!ok) return null;
        int[] q = Ids(Split(clause));
        if (q.Length == 0) return null;
        string bestId = null; float b1 = 0f, b2 = 0f;
        foreach (KeyValuePair<string, int[]> kv in prof)
        {
            if (kv.Value.Length == 0) continue;
            float tot = 0f;
            foreach (int w in q) tot += Best(w, kv.Value);
            tot /= q.Length;
            if (tot > b1) { b2 = b1; b1 = tot; bestId = kv.Key; } else if (tot > b2) b2 = tot;
        }
        return (b1 >= 0.75f && b1 - b2 >= 0.15f) ? bestId : null;
    }

    /// <summary>What kind of answer is this? Returns one of the allowed class names or null. Negation flips good/bad and drops the rest.</summary>
    public static string Classify(List<string> toks, string[] allowed, float min)
    {
        Load();
        if (!ok) return null;
        string best = null; float bs = min;
        for (int i = 0; i < toks.Count; i++)
        {
            if (Stop.Contains(toks[i])) continue;
            int q = Find(toks[i]);
            if (q < 0) continue;
            bool neg = (i > 0 && Neg.Contains(toks[i - 1])) || (i > 1 && Neg.Contains(toks[i - 2]));
            foreach (string c in allowed)
            {
                float s = Best(q, seeds[c]);
                if (s <= bs) continue;
                string cls = c;
                if (neg) { if (c == "pos") cls = "neg"; else if (c == "neg") cls = "pos"; else continue; }
                bs = s; best = cls;
            }
        }
        return best;
    }
}
