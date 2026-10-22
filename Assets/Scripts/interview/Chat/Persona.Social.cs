using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Social acts aimed at the interviewer: compliments, flirting, backhanded remarks, "are you ok?", "what if you were a doctor",
/// "what were we doing". Detected by meaning (Semantic.Classify), answered in the persona's flavor. Escalates if the player keeps flirting.
/// </summary>
public static partial class Persona
{
    public static bool NoProbe;         // UI sets this on the last small-talk turn: no follow-up question the player can't answer
    public static string LastSocial;    // looks, flirt, praise, slight, concern, hypo, recap: lets the UI adjust rapport and mood
    public static int FlirtCount;

    static readonly string[] SocSet = { "looks", "flirt", "praise", "concern" };
    static readonly string[] Flavors = { "dry", "warm", "stern", "quirky", "grand", "worn" };

    // one entry per flavor in the order above, "a|b" = two variants
    static readonly Dictionary<string, string[]> SocLines = new Dictionary<string, string[]>
    {
        { "looks",  new[] { "Flattery. How suspicious.|I'll pretend I didn't hear that.", "Oh, aren't you sweet! Thank you.|That's kind of you to say, {name}.", "Appearance is not on the agenda.|Keep it professional, {name}.", "My eyes? They've seen things. Thank you!|Ha! My mirror disagrees on Mondays.", "Naturally. Many have noticed.|You have taste, I'll grant you that.", "Thanks. They're tired, though.|Kind of you. Few notice me lately." } },
        { "flirt",  new[] { "That's not an interview question.|Bold. Inappropriate, but bold.", "Oh my! Let's keep this professional, dear.|Flattered, but I'm your interviewer.", "That is inappropriate. Consider it a warning.|This is an interview, not a date.", "Is this a bit? I can't tell if this is a bit.|Goodness. My cat would be scandalized.", "Tempting, but no.|Charming, yet I must decline.", "Haven't heard that in years. No, but thank you.|Ha. Too tired for that." } },
        { "praise", new[] { "Noted. I'll try not to let it go to my head.|Kind. Suspicious, but kind.", "Thank you, that really means a lot.|You're too kind, {name}.", "Thank you. Flattery won't change the score.|Appreciated. The score remains the score.", "Oh! Put that in writing, please!|I'll frame that. Thank you!", "Yes, quite. But do continue.|Of course. Go on.", "Thanks. Nice to hear for once.|Well. Thank you." } },
        { "slight", new[] { "Fair. I've heard worse.|Thanks for your honesty, I suppose.", "Oh. That stings a little, {name}.|Well, that wasn't very nice.", "Noted. And that goes on your record.|That was uncalled for.", "Ouch! Okay. Ouch.|Rude, but at least it's original.", "You wound me. Barely.|Opinions are cheap. Mine are not.", "Yeah. I get that a lot.|Can't argue with that, honestly." } },
        { "again",  new[] { "Twice. Stop.", "Dear, please stop now.", "Second warning. Next one ends this.", "Okay, this is officially a bit.", "Persistence is not charm.", "Please. Stop." } },
    };
    // Patient, Impatient, Paranoid, Bored
    static readonly string[][] Concern = { new[] { "I'm fine, thank you for asking. Just focused." }, new[] { "Nothing's wrong. I just dislike wasted time." }, new[] { "Nothing. Why do you ask? Did someone say something?" }, new[] { "Long week. Never mind me." } };
    static readonly string[][] Hypo = { new[] { "A fun thought. But my title is {title}, and I'm quite happy with it." }, new[] { "Hypotheticals waste time. My title is {title}. Next." }, new[] { "Why would you ask that? I am {title}, as you know." }, new[] { "Maybe. My title is {title}, though. Moving on." } };
    static readonly string[][] Recap = { new[] { "We're having a little small talk before the real interview." }, new[] { "Small talk. Which is almost over." }, new[] { "Small talk. Why do you ask?" }, new[] { "Small talk. Riveting." } };

    /// <summary>"what", "huh", "sorry what", "pardon": the player did not hear or follow, so repeat.</summary>
    public static bool IsHuh(string said)
    {
        List<string> t = Toks(said);
        if (t.Count == 0 || t.Count > 2) return false;
        string a = t[0];
        if (a != "what" && a != "huh" && a != "eh" && a != "pardon" && a != "sorry") return false;
        if (t.Count == 1) return a != "sorry";
        return t[1] == "what" || t[1] == "huh" || t[1] == "sorry";
    }

    static string SocPick(string key, int flavor, string name)
    {
        string[] v = SocLines[key][flavor].Split('|');
        return Fill(Pick(v), name);
    }

    /// <summary>Returns the interviewer's reply to a compliment, flirt, concern, hypothetical or recap, else null.</summary>
    public static string Social(string said, Mood mood, string name)
    {
        LastSocial = null;
        if (Cur == null) return null;
        List<string> t = Toks(said);
        if (t.Count == 0) return null;
        bool you = Has(t, "you", "your", "yourself");
        string k = null;
        if (Has(t, "we") && Has(t, "doing", "talking", "saying", "discussing") && (QKind(t, said) != null || said.IndexOf('?') >= 0)) k = "recap";
        else if (you && ((t[0] == "what" && t.Count > 1 && t[1] == "if") || t[0] == "imagine" || t[0] == "suppose"))
        {
            string ta = TitleAnswer(said, mood, name);      // "what if you were a professor" when that IS the persona's title
            if (ta != null) return ta;
            k = "hypo";
        }
        else if (you && !Has(t, "meet", "meeting"))
        {
            string c = Semantic.Classify(t, SocSet, 0.85f);
            bool q = QKind(t, said) != null || said.IndexOf('?') >= 0;
            if (c == "concern") { if (q) k = c; }
            else if (c == "flirt") { if (Has(t, "me", "us", "we") || !q) k = c; }
            else if (c != null)
            {
                bool neg = Has(t, "not", "dont", "never", "no", "isnt", "arent", "aint", "cant");
                if (neg) k = "slight";            // "i dont think you have nice eyes"
                else if (!q) k = c;
            }
        }
        if (k == null) return null;
        LastSocial = k;
        int fi = System.Array.IndexOf(Flavors, Cur.flavor); if (fi < 0) fi = 0;
        string r;
        switch (k)
        {
            case "recap": r = Fill(Pick(Recap[(int)mood]), name) + " " + (NoProbe ? "Say 'ready' when you are." : (pendingQ ?? "Ask me something, or say 'ready'.")); break;
            case "hypo": r = Fill(Pick(Hypo[(int)mood]).Replace("{title}", Cur.title.Substring(0, 1).ToUpperInvariant() + Cur.title.Substring(1).ToLowerInvariant()), name); break;
            case "concern": r = Fill(Pick(Concern[(int)mood]), name); break;
            case "flirt":
                FlirtCount++;
                r = SocPick("flirt", fi, name);
                if (FlirtCount >= 2) r += " " + SocLines["again"][fi];
                break;
            default: r = SocPick(k, fi, name); break;
        }
        return Voice(r, mood, name, true);
    }

    // Offended(level): 1 = big warning (small talk), 2 = rejected after the warning, 3 = rejected on the spot (questionnaire). One line per flavor.
    static readonly string[][] Offend = {
        new[] { "Excuse me? That language ends here. This is your one and only warning. Once more and you are out.", "Oh, {name}. We do not speak like that here. One warning. The next time, I end this interview.", "UNACCEPTABLE. That is your first and final warning. Repeat it and this interview is over.", "Whoa. Whoa-whoa. Absolutely not. One warning, {name}. One. Say it again and you're done.", "How dare you. You will address me with respect. This is your only warning.", "Wow. After all these years, still people like you. That's your warning. One more and you're out." },
        new[] { "That's it. We are finished. Security will show you out. Forget the cognitive test.", "I'm sorry, {name}, but I warned you. This interview is over. You are rejected.", "Enough. Interview terminated. You are rejected. Leave.", "Nope. Done. Out. You're rejected, {name}.", "Enough! Get out of my sight. You are rejected.", "I warned you. We're done here. Rejected." },
        new[] { "No. That language ends this interview, right now. You are rejected.", "Oh, {name}, no. I cannot allow that. This interview is over, and you are rejected.", "Unacceptable. Interview terminated. You are rejected. Leave.", "Hey! No! Absolutely not. You're out, {name}. Rejected.", "You dare? Out. You are rejected.", "No. Not in my room. You're rejected." },
    };

    public static string Offended(int level, string name)
    {
        int fi = Cur == null ? 0 : System.Array.IndexOf(Flavors, Cur.flavor); if (fi < 0) fi = 0;
        return Fill(Offend[level - 1][fi], name);
    }
}
