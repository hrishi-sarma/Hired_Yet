using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Conversation memory for the interviewer: it remembers what it just asked, so the player's next line is read as an
/// ANSWER to that question (not as a random statement), and "what do you mean" is answered by rephrasing that question.
/// Answer type (money, passion, ...) comes from Semantic.Classify, so synonyms work without adding phrases.
/// </summary>
public static partial class Persona
{
    public static string Pending;      // apply, heard, hope, feel, other, or null
    static string pendingQ, lastLine;

    static readonly string[] AppSet = { "money", "passion", "growth", "stability", "need", "referral", "company", "local" };
    static readonly string[] VolSet = { "money", "need" };
    static readonly string[] FeelSet = { "pos", "neg" };
    static readonly string[] Hedges = { "guess", "suppose", "maybe", "perhaps", "kinda", "sorta", "probably", "dunno", "idk", "whatever" };

    // one line per mood: Patient, Impatient, Paranoid, Bored
    static readonly Dictionary<string, string[]> Rx = new Dictionary<string, string[]>
    {
        { "money",     new[] { "Honest. Plenty of people won't admit that.", "At least you're candid. Money, then.", "Money. Hm. Is that all you came for?", "Money. Sure. Everyone's here for something." } },
        { "passion",   new[] { "Passion is lovely to hear. Keep that spark.", "Enthusiasm. Good. Show it in the answers.", "Passion? Prove it in the answers.", "Enthusiastic. Fine." } },
        { "growth",    new[] { "Wanting to grow is a good sign.", "Ambition. Good. Now show it.", "Growth, you say. Growth for whom?", "Career growth. The classic." } },
        { "stability", new[] { "Stability is a sensible thing to want.", "Safe choice. Fine.", "Security. Or are you running from something?", "Stable. Sensible. A bit dull. Fine." } },
        { "need",      new[] { "I understand. Many of us started that way.", "You needed work. Understood. Let's see what you've got.", "You needed a job. Convenient timing.", "Needed a job. Fair enough." } },
        { "referral",  new[] { "Word of mouth, lovely. That counts for a lot.", "Good source. Fine.", "Who exactly told you? Hm.", "A tip, then. Okay." } },
        { "company",   new[] { "Kind of you to say. We do try.", "Good. You did your homework.", "You researched us? Why, exactly?", "Sure, the company. Okay." } },
        { "local",     new[] { "Convenient. That matters more than people think.", "Close by. Practical.", "Close by. How close, exactly?", "Close. Okay." } },
        { "unsure",    new[] { "That's alright, not everyone knows.", "You don't know? Think harder.", "You don't know? Or you won't say?", "Don't know. Join the club." } },
        { "joke",      new[] { "Ha. Good. Now, seriously.", "Very funny. Moving on.", "Is that a joke? I can't tell.", "Heh. Sure." } },
    };
    static readonly string[][] AckPlain = { new[] { "I see. Go on.", "Mm, I follow.", "Right. Noted." }, new[] { "Noted. Moving on.", "Mm. Fine.", "Yes, yes." }, new[] { "Hm. Why mention that?", "Interesting. Why tell me?" }, new[] { "Sure.", "Mm-hm.", "If you say so." } };
    static readonly string[][] VagueShort = { new[] { "Short and sweet. I'll take it." }, new[] { "That's it? Fine." }, new[] { "Hm. Just that?" }, new[] { "Okay." } };
    static readonly string[][] VagueLong = { new[] { "I see. That's a fair reason." }, new[] { "Hm. Vague, but fine." }, new[] { "Hm. Is that the whole story?" }, new[] { "Right. Sure." } };

    static void ResetDialogue() { Pending = null; pendingQ = null; lastLine = null; LastSocial = null; FlirtCount = 0; NoProbe = false; }

    /// <summary>Call with every line the interviewer says. Remembers the open question, if the line ends with one.</summary>
    public static void Note(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        lastLine = line; Pending = null; pendingQ = null;
        int q = line.LastIndexOf('?');
        if (q < 0) return;
        int s = Mathf.Max(line.LastIndexOf('.', q), line.LastIndexOf('!', q)) + 1;
        pendingQ = line.Substring(s, q - s + 1).Trim();
        string l = pendingQ.ToLowerInvariant();
        if (l.Contains("apply")) Pending = "apply";
        else if (l.Contains("hear about") || l.Contains("find out about") || l.Contains("come across")) Pending = "heard";
        else if (l.Contains("hoping") || l.Contains("looking for") || l.Contains("want out of")) Pending = "hope";
        else if (l.Contains("how are you") || l.Contains("how are things") || l.Contains("how do you feel") || l.Contains("how's your day") || l.Contains("how is your day")) Pending = "feel";
        else Pending = "other";
    }

    /// <summary>Answer to "repeat that" / "what do you mean": rephrases the open question, or repeats the last line.</summary>
    public static string Restate(bool clarify, string prev)
    {
        string line = null;
        if (clarify)
        {
            switch (Pending)
            {
                case "apply": line = "I mean: why did you want this job in particular?"; break;
                case "heard": line = "I mean: where did you first come across this job?"; break;
                case "hope": line = "I mean: what do you want to get out of working here?"; break;
                case "feel": line = "I mean: how are you feeling right now?"; break;
                case "other": line = pendingQ != null ? "I asked: " + pendingQ : null; break;
            }
        }
        if (line == null) line = string.IsNullOrEmpty(prev ?? lastLine) ? null : (clarify ? "In short: " : "I said: ") + (prev ?? lastLine);
        return line == null ? null : Voice(line, Mood.Patient, null, false);
    }

    static bool Has(List<string> t, params string[] w) { foreach (string x in w) if (t.Contains(x)) return true; return false; }

    static bool Unsure(List<string> t)
    {
        if (Has(t, "idk", "dunno", "unsure")) return true;
        for (int i = 0; i < t.Count - 1; i++)
        {
            bool neg = System.Array.IndexOf(Negs, t[i]) >= 0 || t[i] == "no";
            if (neg && (t[i + 1] == "idea" || t[i + 1] == "know" || t[i + 1] == "sure")) return true;
        }
        return false;
    }

    static string Vague(Mood m, int words) { return From(words <= 2 ? VagueShort : VagueLong, m, null); }

    /// <summary>Reads the player's line in the context of the open question. Null = nothing sensible to say.</summary>
    static string Dlg(List<string> t, Mood mood, string name, bool pend)
    {
        string k;
        if (Has(t, "lol", "haha", "hahaha", "lmao", "hehe", "joking", "kidding")) k = "joke";
        else if (pend && Pending != "feel" && Unsure(t)) k = "unsure";
        else k = Semantic.Classify(t, !pend ? VolSet : (Pending == "feel" ? FeelSet : AppSet), pend ? 0.8f : 0.985f);
        if (k == null) return pend && Pending != "feel" ? Vague(mood, t.Count) : null;
        if (k == "pos") return Flv("happy", Happy, mood, name);
        if (k == "neg") return Flv("notwell", NotWell, mood, name);
        return Fill(Rx[k][(int)mood], name);
    }
}
