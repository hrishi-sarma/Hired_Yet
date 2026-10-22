using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum Intent { Greet, HowAreYou, Thanks, Sorry, Repeat, Clarify, ReadyStart, Compliment, Rude, AskJob, AskInterviewer, Joke, Unknown }

public static class IntentMatcher
{
    struct Rule { public Intent intent; public float weight; public string[] words; }

    static readonly Rule[] Rules =
    {
        new Rule { intent = Intent.Greet,          weight = 1.0f, words = new[] { "hello", "hi", "hey", "good morning", "good afternoon", "good evening", "greetings" } },
        new Rule { intent = Intent.HowAreYou,      weight = 1.2f, words = new[] { "how are you", "how is your day", "hows it going", "how do you do", "how are things" } },
        new Rule { intent = Intent.Thanks,         weight = 1.0f, words = new[] { "thank you", "thanks", "appreciate" } },
        new Rule { intent = Intent.Sorry,          weight = 1.0f, words = new[] { "sorry", "apologize", "my bad", "excuse me" } },
        new Rule { intent = Intent.Repeat,         weight = 1.2f, words = new[] { "repeat", "say that again", "pardon", "what did you say", "come again" } },
        new Rule { intent = Intent.Clarify,        weight = 1.0f, words = new[] { "what do you mean", "i dont understand", "can you explain", "clarify", "what does that mean" } },
        new Rule { intent = Intent.ReadyStart,     weight = 1.3f, words = new[] { "ready", "lets begin", "lets start", "lets go", "start the interview", "go ahead", "skip", "begin", "start" } },
        new Rule { intent = Intent.Compliment,     weight = 1.0f, words = new[] { "nice office", "great to meet", "pleasure", "lovely", "impressive", "nice to meet you" } },
        new Rule { intent = Intent.Rude,           weight = 1.4f, words = new[] { "shut up", "stupid", "idiot", "boring", "waste of time", "whatever", "dumb" } },
        new Rule { intent = Intent.AskJob,         weight = 1.0f, words = new[] { "the job", "the position", "the role", "salary", "pay", "what will i do" } },
        new Rule { intent = Intent.AskInterviewer, weight = 1.0f, words = new[] { "tell me about you", "your name", "who are you", "do you like your job" } },
        new Rule { intent = Intent.Joke,           weight = 0.9f, words = new[] { "joke", "funny", "knock knock", "haha" } },
    };

    static string Norm(string s)
    {
        var sb = new StringBuilder(" ");
        foreach (char c in (s ?? "").ToLowerInvariant()) if (char.IsLetterOrDigit(c) || c == ' ') sb.Append(c);
        return sb.Append(' ').ToString();
    }

    // true when a and b differ by at most one inserted, deleted or changed letter
    public static bool Near(string a, string b)
    {
        if (a == b) return true;
        int d = a.Length - b.Length;
        if (d > 1 || d < -1) return false;
        int i = 0, j = 0, miss = 0;
        while (i < a.Length && j < b.Length)
        {
            if (a[i] == b[j]) { i++; j++; continue; }
            if (++miss > 1) return false;
            if (d > 0) i++; else if (d < 0) j++; else { i++; j++; }
        }
        return miss + (a.Length - i) + (b.Length - j) <= 1;
    }

    static bool Has(string s, string[] toks, string w)
    {
        if (s.Contains(" " + w + " ")) return true;
        if (w.Length > 5 && s.Contains(w)) return true;
        if (w.IndexOf(' ') >= 0 || w.Length < 5) return false;
        foreach (string t in toks) if (t.Length >= 5 && Near(t, w)) return true;   // one-letter typo tolerance, single long words only
        return false;
    }

    static readonly string[] Negs = { "not", "dont", "never", "isnt", "cant", "wasnt", "wont" };

    // single trigger words are only trusted in the right context ("I start work in May" is not "start", "my last job was boring" is not rude)
    static bool Allowed(Intent intent, string w, string[] toks)
    {
        if (w.IndexOf(' ') >= 0) return true;
        if (intent == Intent.ReadyStart)
        {
            if (toks.Length > 4) return false;
            foreach (string t in toks) if (System.Array.IndexOf(Negs, t) >= 0) return false;
        }
        else if (intent == Intent.Rude)
        {
            if (toks.Length <= 3) return true;
            foreach (string t in toks) if (t == "you" || t == "your" || t == "this" || t == "ur" || t == "u") return true;
            return false;
        }
        return true;
    }

    public static Intent Match(string said, out float confidence)
    {
        string s = Norm(said);
        string[] toks = s.Trim().Split(' ');
        float best = 0f; Intent bi = Intent.Unknown;
        foreach (Rule r in Rules)
        {
            float score = 0f;
            foreach (string w in r.words)
            {
                if (Has(s, toks, w) && Allowed(r.intent, w, toks)) score += r.weight * (w.IndexOf(' ') >= 0 ? 1.5f : 1f);
            }
            if (score > best) { best = score; bi = r.intent; }
        }
        confidence = Mathf.Clamp01(best / 2f);
        return best >= 1f ? bi : Intent.Unknown;
    }
}

public static class SmallTalk
{
    // [intent][mood] -> lines. Mood order: Patient, Impatient, Paranoid, Bored.
    static readonly Dictionary<Intent, string[][]> Bank = new Dictionary<Intent, string[][]>
    {
        { Intent.Greet, new[] {
            new[] { "Hello, {name}! Lovely to see you.", "Welcome, welcome! Good {time} to you." },
            new[] { "Yes, hello. Let's not waste time.", "Hi. Sit up straight." },
            new[] { "Hello. You look... nervous.", "Hi. I've read your file." },
            new[] { "Hey.", "Mm. Hi." } } },
        { Intent.HowAreYou, new[] {
            new[] { "Very well, thank you for asking! And you?", "Wonderful, thank you. Ready for a good chat?" },
            new[] { "Busy. Very busy.", "I'd be better if we started." },
            new[] { "Why do you ask?", "Fine. Why, what have you heard?" },
            new[] { "Tired.", "Meh. You?" } } },
        { Intent.Thanks, new[] {
            new[] { "You're very welcome.", "My pleasure, {name}." },
            new[] { "Yes, yes. Moving on.", "Don't thank me yet." },
            new[] { "Hm. Flattery, is it?", "Thanks for what, exactly?" },
            new[] { "Sure.", "Mm-hm." } } },
        { Intent.Sorry, new[] {
            new[] { "No need to apologise. Relax.", "That's quite all right." },
            new[] { "Don't let it happen again.", "Sorry won't save you here." },
            new[] { "Sorry for what? Be specific.", "Interesting choice of words." },
            new[] { "Whatever.", "It's fine." } } },
        { Intent.Repeat, new[] {
            new[] { "Of course, let me say that again.", "Happy to repeat myself." },
            new[] { "Listen properly this time.", "Again? Fine." },
            new[] { "Stalling for time?", "You heard me. But fine." },
            new[] { "Ugh. Again.", "Sure, from the top." } } },
        { Intent.Clarify, new[] {
            new[] { "Good question. Let me put it simply.", "Let me explain." },
            new[] { "It's not complicated.", "Pay attention." },
            new[] { "Why does it confuse you?", "Hm. Interesting that you ask." },
            new[] { "It means what it means.", "Eh. Figure it out." } } },
        { Intent.ReadyStart, new[] {
            new[] { "Wonderful. Let's begin!", "Shall we? Right this way." },
            new[] { "Finally. Let's go.", "About time." },
            new[] { "Good. I'll be watching.", "Very well. Don't slip up." },
            new[] { "Okay. Let's get it over with.", "Fine. Starting." } } },
        { Intent.Compliment, new[] {
            new[] { "How kind of you, {name}!", "Thank you, that's sweet." },
            new[] { "Flattery won't help.", "Hm. Noted." },
            new[] { "What are you after?", "Nice try." },
            new[] { "Uh-huh.", "Cute." } } },
        { Intent.Rude, new[] {
            new[] { "Hm. That wasn't very nice.", "Let's keep this civil." },
            new[] { "Watch your tone.", "Excuse me?! Careful." },
            new[] { "Interesting. Very telling.", "I'll remember that." },
            new[] { "Wow. Okay.", "Charming." } } },
        { Intent.AskJob, new[] {
            new[] { "Wonderful question! We'll cover that.", "The details come later, {name}." },
            new[] { "Questions about pay later.", "Focus on the interview." },
            new[] { "Why so curious about the role?", "That depends on how this goes." },
            new[] { "It's a job.", "Later." } } },
        { Intent.AskInterviewer, new[] {
            new[] { "Aw, I'm just here to help you shine.", "Me? I've done this a long time." },
            new[] { "This interview isn't about me.", "I ask the questions." },
            new[] { "Why do you want to know about me?", "I don't share personal details." },
            new[] { "Nothing exciting.", "Boring story. Skip." } } },
        { Intent.Joke, new[] {
            new[] { "Ha! That's a good one.", "Hah, I needed that." },
            new[] { "I don't have time for jokes.", "Hilarious. Moving on." },
            new[] { "Is that a joke or a confession?", "Hm. Funny. Suspicious." },
            new[] { "Ha. Ha.", "I've heard better." } } },
        { Intent.Unknown, new[] {
            new[] { "Interesting. Tell me more later.", "Mm, I see. Lovely." },
            new[] { "I'm not sure that's relevant.", "Let's get back to the interview." },
            new[] { "Mm. Noted. I'm writing that down.", "Strange thing to say." },
            new[] { "Mm. Noted.", "Right." } } },
    };

    static readonly Dictionary<Intent, List<string>> lastUsed = new Dictionary<Intent, List<string>>();

    static readonly string[][] Openers =
    {
        new[] { "How was your trip here?", "Did you find the place all right?", "I see you enjoy {hobby}. How's that going?" },
        new[] { "You're on time. Good. Nervous?", "Let's skip the pleasantries. Ready?" },
        new[] { "Before we start: anything you'd like to tell me?", "Nervous? You look nervous." },
        new[] { "So. Nice weather, I guess.", "Long day. You okay?" },
    };

    public static void Reset() { lastUsed.Clear(); }

    static string TimeOfDay()
    {
        int h = System.DateTime.Now.Hour;
        return h < 12 ? "morning" : (h < 18 ? "afternoon" : "evening");
    }

    static string Fill(string s, string name)
    {
        string hobby = string.IsNullOrEmpty(ResumeSession.Hobby) ? "your hobbies" : ResumeSession.Hobby;
        return s.Replace("{name}", string.IsNullOrEmpty(name) ? "there" : name).Replace("{hobby}", hobby).Replace("{time}", TimeOfDay());
    }

    public static string Opener(Mood mood, string name, bool noWeather = false)
    {
        string[] a = Openers[(int)mood];
        if (noWeather)
        {
            var keep = new List<string>();
            foreach (string o in a) if (o.IndexOf("weather", System.StringComparison.OrdinalIgnoreCase) < 0) keep.Add(o);
            if (keep.Count > 0) a = keep.ToArray();
        }
        return Fill(a[Random.Range(0, a.Length)], name);
    }

    public static string Reply(Intent intent, Mood mood, string name)
    {
        string[][] byMood;
        if (!Bank.TryGetValue(intent, out byMood)) return "";
        string[] lines = byMood[(int)mood];
        List<string> used;
        if (!lastUsed.TryGetValue(intent, out used)) { used = new List<string>(); lastUsed[intent] = used; }
        List<string> pool = new List<string>();
        foreach (string l in lines) if (!used.Contains(l)) pool.Add(l);
        if (pool.Count == 0) pool.AddRange(lines);
        string pick = pool[Random.Range(0, pool.Count)];
        used.Add(pick);
        if (used.Count > 2) used.RemoveAt(0);
        return Fill(pick, name);
    }
}
