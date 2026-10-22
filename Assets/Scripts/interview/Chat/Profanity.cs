using System.Collections.Generic;
using System.Text;

/// <summary>
/// Offensive-language check for typed or spoken answers. Token based (no Scunthorpe problem), tolerant of f*ck / fuuuck / sh1t / f u c k,
/// and insults like "stupid" only count when aimed at the interviewer ("you are stupid", not "this question is stupid").
/// </summary>
public static class Profanity
{
    // exact tokens (short words that are also parts of normal words)
    static readonly HashSet<string> Exact = new HashSet<string>(
        "fk fck fuk fck sht shyt dick dicks cock cocks cunt cunts twat prick pricks arse arsehole piss pissed kys stfu gtfo wtf jerkoff".Split(' '));
    // prefixes: also catch -ing, -er, -ed, -s, -head ...
    static readonly string[] Stems = {
        "fuck", "shit", "bullshit", "bitch", "asshole", "ashole", "bastard", "whore", "slut", "wanker", "douche", "jackass", "dumbass", "dipshit",
        "motherfuck", "retard", "faggot", "nigg", "dickhead", "cocksuck", "cunt", "twat", "piss"
    };
    static readonly string[] NotStems = { "shitake", "shiitake", "pisa", "pissarro", "pistachio" };
    static readonly string[] Phrases = {
        "go to hell", "piss off", "screw you", "kill yourself", "up yours", "eat shit", "go die", "drop dead", "suck my", "bite me"
    };
    // only offensive when aimed at the interviewer
    static readonly HashSet<string> Insults = new HashSet<string>(
        "idiot idiots moron moronic stupid dumb loser jerk ugly fat trash garbage pathetic useless clown creep scum freak imbecile".Split(' '));
    static readonly HashSet<string> Aim = new HashSet<string>("you your youre yourself u ur".Split(' '));

    static string Collapse(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s) if (sb.Length == 0 || sb[sb.Length - 1] != c) sb.Append(c);
        return sb.ToString();
    }

    static List<string> Tokens(string said)
    {
        var sb = new StringBuilder();
        foreach (char c0 in (said ?? "").ToLowerInvariant())
        {
            char c = c0;
            switch (c)
            {
                case '@': case '4': c = 'a'; break;
                case '$': case '5': c = 's'; break;
                case '0': c = 'o'; break;
                case '1': case '!': c = 'i'; break;
                case '3': c = 'e'; break;
                case '7': c = 't'; break;
                case '*': case '#': c = '\u0001'; break;          // masked letter: dropped, "f*ck" -> "fck"
                case '\'': case '\u2019': continue;
            }
            if (c == '\u0001') continue;
            sb.Append(c >= 'a' && c <= 'z' ? c : ' ');
        }
        var raw = new List<string>(sb.ToString().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries));
        var l = new List<string>();
        for (int i = 0; i < raw.Count; i++)           // "f u c k" -> "fuck"
        {
            if (raw[i].Length == 1 && i + 1 < raw.Count && raw[i + 1].Length == 1)
            {
                var run = new StringBuilder();
                int j = i;
                while (j < raw.Count && raw[j].Length == 1) run.Append(raw[j++]);
                if (run.Length >= 3) { l.Add(run.ToString()); i = j - 1; continue; }
            }
            l.Add(raw[i]);
        }
        for (int i = 0; i < l.Count; i++) l[i] = Collapse(l[i]);
        return l;
    }

    static readonly string[] CStems = Build();
    static string[] Build() { var a = new string[Stems.Length]; for (int i = 0; i < a.Length; i++) a[i] = Collapse(Stems[i]); return a; }

    static readonly string[] CPhrases = BuildPhrases();
    static string[] BuildPhrases()
    {
        var a = new string[Phrases.Length];
        for (int i = 0; i < a.Length; i++) { string[] w = Phrases[i].Split(' '); for (int k = 0; k < w.Length; k++) w[k] = Collapse(w[k]); a[i] = string.Join(" ", w); }
        return a;
    }

    public static bool Is(string said)
    {
        List<string> t = Tokens(said);
        if (t.Count == 0) return false;
        bool aimed = false;
        foreach (string w in t) if (Aim.Contains(w)) { aimed = true; break; }
        foreach (string w in t)
        {
            if (Exact.Contains(w)) return true;
            bool ok = true;
            foreach (string n in NotStems) if (w.StartsWith(Collapse(n))) { ok = false; break; }
            if (ok) foreach (string s in CStems) if (w.StartsWith(s)) return true;
            if (aimed && Insults.Contains(w)) return true;
        }
        string joined = " " + string.Join(" ", t.ToArray()) + " ";
        foreach (string p in CPhrases) if (joined.Contains(" " + p)) return true;
        return false;
    }
}
