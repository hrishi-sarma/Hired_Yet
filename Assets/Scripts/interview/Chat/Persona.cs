using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class PersonaDef
{
    public string baseName, id, title, name, flavor, open, ready, motto;
    public float talk, warm, rude, emote, scold, adv, fumble;
    public char start;
    public float[] ms = { 1f, 1f, 1f, 1f };   // per-mood sensitivity (Patient, Impatient, Paranoid, Bored)
    public float expr = 1f;                    // facial expression scale
    public string[] quirks, advice;
}

/// <summary>
/// Interviewer persona (fixed for the whole session, separate from Mood) with procedural lore.
/// Facts are drawn lazily from PersonaData pools the first time they are asked, then stay fixed (consistent recall).
/// </summary>
public static partial class Persona
{
    public static PersonaDef Cur;
    public static string Force;                          // dev: set an id to force a persona
    public static string Me, Company, City, Topic;
    public static int Years, Age;

    static readonly List<PersonaDef> defs = new List<PersonaDef>();
    class Topic_ { public string id, key; public string[][] alts; public float sens; }
    static readonly List<Topic_> topics = new List<Topic_>();
    static readonly Dictionary<string, string[]> lore = new Dictionary<string, string[]>();
    static readonly Dictionary<string, int> asked = new Dictionary<string, int>();
    static string happy = "mixed";

    static readonly string[] Companies =
    {
        "Stark Industries", "Wayne Enterprises", "Oscorp", "Umbrella Corporation", "Cyberdyne Systems", "Weyland-Yutani", "Tyrell Corporation", "Initech", "Dunder Mifflin", "Wonka Industries", "Monsters Inc.", "Pied Piper", "Hooli", "Los Pollos Hermanos", "Vandelay Industries", "Acme Corporation", "Globex Corporation", "Soylent Corp", "Aperture Science", "Black Mesa", "Buy n Large", "Gringotts Bank", "Duff Brewing", "The Krusty Krab", "Planet Express", "Wernham Hogg", "Sterling Cooper", "Prestige Worldwide", "Bluth Company", "Massive Dynamic", "LexCorp", "The Daily Planet", "Rekall Inc.", "Nakatomi Trading", "Virtucon", "Paper Street Soap Co.", "Spacely Sprockets", "Slate Rock & Gravel", "Cogswell Cogs", "Sirius Cybernetics", "Bubba Gump Shrimp Co.", "Oceanic Airlines", "Veridian Dynamics", "Roxxon Energy", "Hammer Industries", "Pawnee Parks & Rec", "Stay Puft Foods", "Pizza Planet", "Dharma Initiative", "InGen", "Delos Destinations", "Mom's Friendly Robot Co.", "Cheers Bar & Grill", "Central Perk", "Strickland Propane", "Gekko & Co."
    };
    static readonly string[] Cities = { "Leeds", "Pune", "Rotterdam", "Austin", "Brno", "Lisbon" };
    static readonly string[] Back = { "And you?", "What about you?", "How about yourself?" };
    static readonly string[] Ask = { "What made you apply?", "How did you hear about us?", "What are you hoping for here?" };
    static readonly string[] Stop = { "the", "a", "an", "to", "my", "some", "really", "very" };

    public static int Count { get { Load(); return defs.Count; } }

    static void Load()
    {
        if (defs.Count > 0) return;
        if (SemanticTopic == null) SemanticTopic = Semantic.Topic;
        foreach (string line in PersonaData.Defs)
        {
            string[] f = line.Split('~');
            string[] t = f[3].Split(',');
            PersonaDef d = new PersonaDef();
            d.id = f[0]; d.title = f[1]; d.name = f[2];
            d.talk = float.Parse(t[0]) / 100f; d.warm = float.Parse(t[1]) / 100f; d.rude = float.Parse(t[2]) / 100f; d.emote = float.Parse(t[3]) / 100f;
            d.scold = float.Parse(t[4]) / 100f; d.adv = float.Parse(t[5]) / 100f; d.fumble = float.Parse(t[6]) / 100f;
            d.start = f[4][0]; d.flavor = f[5]; d.quirks = f[6].Split(';'); d.open = f[7]; d.ready = f[8]; d.motto = f[9];
            d.advice = f.Length > 10 ? f[10].Split(';') : new string[0];
            float ov = 0.65f + d.emote * 0.7f;
            d.ms = new[] {
                Mathf.Clamp((0.6f + 0.9f * d.warm) * ov, 0.35f, 1.7f),
                Mathf.Clamp((0.55f + 0.9f * d.rude + (d.start == 'I' ? 0.25f : 0f) - 0.3f * d.warm) * ov, 0.35f, 1.7f),
                Mathf.Clamp((0.7f + (d.start == 'A' ? 0.5f : 0f) + d.fumble * 0.4f) * ov, 0.35f, 1.7f),
                Mathf.Clamp((0.6f + (d.start == 'B' ? 0.5f : 0f) + (1f - d.emote) * 0.3f) * ov, 0.35f, 1.7f) };
            d.expr = Mathf.Clamp(0.4f + d.emote * 1.1f, 0.4f, 1.5f);
            defs.Add(d);
        }
        foreach (string line in PersonaData.Topics)
        {
            string[] f = line.Split('~');
            Topic_ t = new Topic_();
            t.id = f[0]; t.sens = float.Parse(f[2]) / 100f; t.key = f[3];
            string[] alts = f[1].Split('|');
            t.alts = new string[alts.Length][];
            for (int i = 0; i < alts.Length; i++) t.alts[i] = alts[i].Split(' ');
            topics.Add(t);
        }
    }

    /// <summary>Start of an interview: pick the persona and roll fresh lore.</summary>
    public static void Begin()
    {
        ResetDialogue();
        Load();
        Cur = null;
        if (!string.IsNullOrEmpty(Force)) foreach (PersonaDef d in defs) if (d.id == Force) Cur = d;
        if (Cur == null) Cur = defs[Random.Range(0, defs.Count)];
        lore.Clear(); asked.Clear();
        if (Cur.baseName == null) Cur.baseName = Cur.name;
        string alt;   // one of the persona's two matched names, rolled each interview
        Cur.name = (PersonaData.AltName.TryGetValue(Cur.id, out alt) && Random.value < 0.5f) ? alt : Cur.baseName;
        Me = Cur.name;
        Company = Companies[Random.Range(0, Companies.Length)];
        City = Cities[Random.Range(0, Cities.Length)];
        Years = Random.Range(2, 19);
        Age = Random.Range(26, 38) + Years / 2;
        Topic = null;
        float y = Cur.warm + 0.15f, n = (Cur.rude + (1f - Cur.warm)) * 0.35f, m = 0.35f, r = Random.value * (y + n + m);
        happy = r < y ? "yes" : (r < y + n ? "no" : "mixed");
    }

    public static float MoodMul(Mood m) { return Cur != null ? Cur.ms[(int)m] : 1f; }
    public static float ExprScale { get { return Cur != null ? Cur.expr : 1f; } }

    public static string Title { get { return Cur != null ? Cur.title : ""; } }

    public static Mood StartMood(Mood rolled)
    {
        if (Cur == null || Cur.start == '-' || Random.value > 0.75f) return rolled;
        switch (Cur.start) { case 'P': return Mood.Patient; case 'I': return Mood.Impatient; case 'A': return Mood.Paranoid; case 'B': return Mood.Bored; }
        return rolled;
    }

    static string Pick(string[] a) { return a[Random.Range(0, a.Length)]; }

    static string Fill(string s, string name)
    {
        return s.Replace("{name}", string.IsNullOrEmpty(name) ? "there" : name).Replace("{me}", Me ?? "I").Replace("{years}", Years.ToString())
                .Replace("{age}", Age.ToString()).Replace("{company}", Company ?? "the company").Replace("{city}", City ?? "nearby")
                .Replace("{topic}", Topic ?? "that").Replace("{clock}", System.DateTime.Now.ToString("h:mm tt"));
    }

    public static string Opener(Mood mood, string name)
    {
        string s = Cur != null ? Cur.open : "";
        // the player is already seated: no sit orders, and no weather chatter that contradicts the persona's own opening
        if (Cur == null || Cur.talk > 0.3f) s += " " + SmallTalk.Opener(mood, name, Cur != null);
        return Fill(s, name).Trim();
    }

    public static string ReadyLine(string name) { return Cur != null ? Fill(Cur.ready, name) : null; }

    /// <summary>Extra reply a strict persona adds when the player is rude.</summary>
    public static string ScoldLine()
    {
        if (Cur == null) return null;
        if (Cur.scold > 0.5f) return Pick(PersonaData.Scold);
        if (Cur.rude > 0.6f) return Pick(PersonaData.Insults);
        return null;
    }

    // ---------------- P2: persona-voiced interview remarks ----------------
    static int Cls(Remark r)
    {
        switch (r)
        {
            case Remark.Correct: case Remark.ProofOk: case Remark.BluffOk: return 0;
            case Remark.Wrong: case Remark.Hedge: case Remark.NoAnswer: case Remark.ProofBad: case Remark.BluffBad:
            case Remark.Warn1: case Remark.Warn2: case Remark.Impatient: case Remark.SlowMild: return 1;
        }
        return 2;
    }

    static string FirstSentence(string line)
    {
        int cut = SentenceEnd(line);
        return cut > 0 && cut < line.Length - 1 ? line.Substring(0, cut + 1) : line;
    }

    /// <summary>Any interview remark, coloured by the persona (replaces InterviewerPersonality.Line in the UI).</summary>
    public static string Line(Remark r, Mood m)
    {
        string s = InterviewerPersonality.Line(r, m);
        PersonaDef p = Cur;
        if (p == null || string.IsNullOrEmpty(s) || s[0] == '(') return s;
        if (p.emote < 0.2f) s = s.Replace("!", ".");
        if (r == Remark.Contradiction || r == Remark.ProofAsk || r == Remark.Pressure || r == Remark.Dodge) return s;   // glued to a question or slot
        int cls = Cls(r);
        bool flat = p.emote < 0.15f;
        if (p.talk < 0.3f) s = FirstSentence(s);
        string tag = null;
        if (cls == 1 && !flat)
        {
            bool hard = r == Remark.Wrong || r == Remark.Hedge || r == Remark.NoAnswer || r == Remark.ProofBad || r == Remark.BluffBad;
            if (p.rude > 0.6f && Random.value < 0.55f) tag = Pick(PersonaData.Insults);
            else if (p.scold > 0.5f && p.warm > 0.6f && Random.value < 0.5f) tag = hard ? Pick(PersonaData.MotherWrong) : "Oh dear, I'm worried about you.";
            else if (p.scold > 0.5f && Random.value < 0.4f) tag = Pick(PersonaData.StrictWrong);
            else if (p.warm > 0.8f && hard && Random.value < 0.4f) tag = Pick(PersonaData.SoftWrong);
        }
        else if (cls == 0 && !flat)
        {
            if (p.rude > 0.6f && Random.value < 0.4f) tag = Pick(PersonaData.PraiseGrudge);
            else if (p.emote > 0.8f && p.warm > 0.4f && Random.value < 0.4f) tag = Pick(PersonaData.Cheer);
            else if (p.warm > 0.6f && Random.value < 0.4f) tag = Pick(PersonaData.PraiseWarm);
        }
        if (tag != null) s = s.Length > 0 ? s + " " + tag : tag;
        if (!flat && Random.value < 0.08f + p.emote * 0.15f) s = Pick(p.quirks) + " " + s;
        return s;
    }

    public static string Shift(Mood to)
    {
        string s = InterviewerPersonality.MoodShiftLine(to);
        return Cur != null && Cur.emote < 0.2f ? s.Replace("!", ".") : s;
    }

    // ---------------- lore ----------------
    static string[] Fact(string key)
    {
        string[] f;
        if (lore.TryGetValue(key, out f)) return f;
        string[] pool;
        if (key == "motto") f = new[] { Cur.motto, "" };
        else if (key == "advice" && Cur.advice.Length > 0) f = new[] { Pick(Cur.advice), "" };
        else
        {
            string pk = key == "happy" ? "happy." + happy : key + "." + Cur.flavor;
            if (!PersonaData.Pools.TryGetValue(pk, out pool) && !PersonaData.Pools.TryGetValue(key, out pool)) return null;
            string e = Pick(pool);
            int bar = e.IndexOf('|');
            f = bar < 0 ? new[] { e, "" } : new[] { e.Substring(0, bar), e.Substring(bar + 1) };
        }
        lore[key] = f;
        return f;
    }

    // ---------------- matching ----------------
    static readonly Dictionary<string, string> Canon = new Dictionary<string, string>
    {
        {"u","you"},{"ur","your"},{"wanna","want"},{"wanted","want"},{"gonna","going"},{"whats","what"},{"hows","how"},{"grow","grew"},{"grown","grew"},
        {"working","work"},{"worked","work"},{"works","work"},{"kids","kid"},{"children","kid"},{"child","kid"},{"jobs","job"},{"hobbies","hobby"},
        {"favourite","favorite"},{"colour","color"},{"pets","pet"},{"dogs","dog"},{"cats","cat"},{"regrets","regret"},{"fears","fear"},{"songs","song"},
        {"movies","movie"},{"films","movie"},{"books","book"},{"years","year"},{"yrs","year"},{"lives","live"},{"lived","live"},{"living","live"},
        {"studied","study"},{"studying","study"},{"dreams","dream"},{"dreamed","dream"},{"happier","happy"},{"career","job"},{"occupation","job"},{"profession","job"},{"employer","company"},{"firm","company"},{"organisation","company"},{"organization","company"},{"supervisor","boss"},{"paycheck","salary"},{"income","salary"},{"mom","parent"},{"mum","parent"},{"dad","parent"},{"mother","parent"},{"father","parent"},{"parents","parent"},{"ya","you"},{"youre","you"},{"youve","you"},{"youd","you"},{"neighbour","neighbor"},{"neighbours","neighbor"},{"sibling","siblings"},{"lonelier","lonely"},{"scary","scared"},{"afraid","scared"},
    };

    static List<string> Toks(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in (s ?? "").ToLowerInvariant())
        {
            if (c == '\'' || c == '\u2019') continue;
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        var l = new List<string>(sb.ToString().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries));
        for (int i = 0; i < l.Count; i++) { string c; if (Canon.TryGetValue(l[i], out c)) l[i] = c; }
        while (l.Count > 1 && System.Array.IndexOf(Fillers, l[0]) >= 0) l.RemoveAt(0);   // "so what are we doing" -> "what are we doing"
        return l;
    }

    static readonly string[] Fillers = { "so", "well", "um", "uh", "hmm", "okay", "ok", "and", "but", "yeah", "yes", "oh", "like", "then" };

    // one token against one topic word: exact, word-ending (4+ letters) or one-letter typo (5+ letters)
    static bool One(string x, string w)
    {
        return x == w || (w.Length >= 4 && x.StartsWith(w)) || (w.Length >= 5 && IntentMatcher.Near(x, w));
    }

    static bool Tok(List<string> t, string w)
    {
        foreach (string x in t) if (One(x, w)) return true;
        return false;
    }

    static readonly string[] Joiners = { "and", "but", "then", "also", "anyway" };
    static readonly string[] QStart = { "what", "why", "how", "when", "where", "who", "which", "do", "does", "did", "are", "is", "was", "were",
                                        "can", "could", "would", "will", "have", "has", "tell", "any", "ever" };

    /// <summary>Splits what the player said into clauses (punctuation, then and/but/so) so two questions are not mixed into one bag of words.</summary>
    static List<List<string>> Clauses(string said)
    {
        var res = new List<List<string>>();
        foreach (string part in (said ?? "").Split('.', ',', '?', '!', ';', ':'))
        {
            List<string> t = Toks(part);
            if (t.Count == 0) continue;
            var cur = new List<string>();
            foreach (string w in t)
            {
                if (cur.Count >= 2 && System.Array.IndexOf(Joiners, w) >= 0) { res.Add(cur); cur = new List<string>(); continue; }
                cur.Add(w);
            }
            if (cur.Count > 0) res.Add(cur);
        }
        return res;
    }

    // a statement ("I have a dog", "good luck") must not be answered as a question about the interviewer
    static bool IsQ(List<string> t)
    {
        if (System.Array.IndexOf(QStart, t[0]) >= 0) return true;
        foreach (string w in t) if (w == "you" || w == "your" || w == "yourself") return true;
        return false;
    }

    /// <summary>Phrase match: the words must appear in order, at most 2 filler words between them. A trailing "$" means the clause must end there.</summary>
    static int Rank(List<string> t, string[] alt)
    {
        bool end = alt[alt.Length - 1] == "$";
        int n = end ? alt.Length - 1 : alt.Length;
        for (int s = 0; s < t.Count; s++)
        {
            if (!One(t[s], alt[0])) continue;
            int pos = s, gap = 0; bool ok = true;
            for (int j = 1; j < n && ok; j++)
            {
                int q = -1;
                for (int k = pos + 1; k <= pos + 3 && k < t.Count; k++) if (One(t[k], alt[j])) { q = k; break; }
                if (q < 0) ok = false; else { gap += q - pos - 1; pos = q; }
            }
            if (ok && (!end || t.Count - 1 - pos <= 1)) return n * 10 - gap;
        }
        return 0;
    }

    public static System.Func<string, string> SemanticTopic;

    static List<Topic_> FindAll(string said)
    {
        var found = new List<Topic_>();
        foreach (List<string> c in Clauses(said))
        {
            if (!IsQ(c)) continue;
            Topic_ best = null; int bs = 0;
            foreach (Topic_ tp in topics)
                foreach (string[] alt in tp.alts) { int sc = Rank(c, alt); if (sc > bs) { bs = sc; best = tp; } }
            if (best == null)   // second pass: garbled or reordered speech. All words of a 2+ word phrase present anywhere in the clause
            {
                foreach (Topic_ tp in topics)
                    foreach (string[] alt in tp.alts)
                    {
                        if (alt.Length < 2 || alt[alt.Length - 1] == "$") continue;
                        bool all = true;
                        foreach (string w in alt) if (!Tok(c, w)) { all = false; break; }
                        if (all && alt.Length * 7 > bs) { bs = alt.Length * 7; best = tp; }
                    }
            }
            if (best == null && SemanticTopic != null)   // hook for an optional embedding model (MiniLM): returns a topic id or null
            {
                string sid = SemanticTopic(string.Join(" ", c.ToArray()));
                foreach (Topic_ tp in topics) if (tp.id == sid) best = tp;
            }
            if (best != null && !found.Contains(best)) found.Add(best);
            if (found.Count == 2) break;
        }
        return found;
    }

    /// <summary>"and you?", "how about you", "what about yourself", "hbu": the player bounces the question back.</summary>
    public static bool Bounce(string said)
    {
        List<string> t = Toks(said);
        int n = t.Count;
        if (n == 0) return false;
        string l1 = t[n - 1], l2 = n > 1 ? t[n - 2] : "", l3 = n > 2 ? t[n - 3] : "";
        if (l1 == "wbu" || l1 == "hbu") return true;
        if (l1 != "you" && l1 != "yourself") return false;
        return n == 1 || l2 == "and" || (l2 == "about" && (l3 == "how" || l3 == "what"));
    }

    // ---------------- answering ----------------
    enum Style { Direct, Deep, Dodge, Fumble, Silent }

    static Style PickStyle(Topic_ tp, Mood m, bool hasExtra)
    {
        PersonaDef p = Cur;
        if (tp.sens >= 1f) return Style.Dodge;
        bool pat = m == Mood.Patient, imp = m == Mood.Impatient, par = m == Mood.Paranoid, bor = m == Mood.Bored;
        float wDirect = 0.6f + (1f - tp.sens) * 0.5f;
        float wDeep = hasExtra ? (p.talk * 0.5f + p.emote * 0.3f + p.warm * 0.2f) * (pat ? 1.4f : (imp ? 0.3f : 1f)) * (0.3f + tp.sens * 0.7f) : 0f;
        float wDodge = tp.sens * (0.3f + (1f - p.warm) * 0.4f + (par ? 0.3f : 0f));
        float wFumble = p.fumble * (0.2f + tp.sens) * 1.2f;
        float wSilent = (tp.sens * (p.rude * 0.3f + (1f - p.talk) * 0.4f) * ((imp || bor) ? 1.5f : 1f) + 0.02f) * (1.1f - p.warm);
        float r = Random.value * (wDirect + wDeep + wDodge + wFumble + wSilent);
        if ((r -= wDirect) < 0f) return Style.Direct;
        if ((r -= wDeep) < 0f) return Style.Deep;
        if ((r -= wDodge) < 0f) return Style.Dodge;
        if ((r -= wFumble) < 0f) return Style.Fumble;
        return Style.Silent;
    }

    static string Stammer(string a)
    {
        string head = Pick(PersonaData.FumbleStart) + " ";
        string w = a;
        if (a.Length > 1 && char.IsLetter(a[0]))
            w = char.ToUpper(a[0]) + "-" + ((a.StartsWith("I ") || a.StartsWith("I'")) ? "I" : char.ToLower(a[0]).ToString()) + a.Substring(1);
        return head + w + " " + Pick(PersonaData.FumbleEnd);
    }

    /// <summary>If the player asked about the interviewer, returns the in-persona reply (already voiced), else null.</summary>
    public static string Answer(string said, Mood mood, string name)
    {
        if (Cur == null) return null;
        string all = null;
        string tq = TitleAnswer(said, mood, name);
        if (tq != null) return tq;
        foreach (Topic_ tp in FindAll(said))
        {
            string one = AnswerTopic(tp, mood, name);
            if (one != null) all = all == null ? one : all + " " + one;
        }
        return all;
    }

    // "are you a professor?" / "if you're a professor, why are you here": the player points at the persona's own title
    static string TitleAnswer(string said, Mood mood, string name)
    {
        if (Cur == null) return null;
        string[] w = null;
        string n = Cur.name;
        if (n.StartsWith("Prof")) w = new[] { "professor", "prof", "teacher", "lecturer" };
        else if (n.StartsWith("Dr")) w = new[] { "doctor", "dr", "philosopher" };
        else if (n.StartsWith("Sgt")) w = new[] { "sergeant", "sarge", "soldier" };
        else if (n.StartsWith("Sir")) w = new[] { "sir", "knight", "gentleman" };
        else if (n.StartsWith("Madame")) w = new[] { "madame", "mystic", "psychic", "fortune" };
        else if (n.StartsWith("Unit")) w = new[] { "unit", "robot", "machine" };
        if (w == null) return null;
        foreach (List<string> c in Clauses(said))
        {
            if (!IsQ(c)) continue;
            bool hit = false;
            foreach (string x in w) if (Tok(c, x)) hit = true;
            if (!hit) continue;
            bool why = Tok(c, "here") || Tok(c, "why") || Tok(c, "doing");
            string title = n.Substring(0, n.IndexOf(' ') > 0 ? n.IndexOf(' ') : n.Length).TrimEnd('.');
            string[] f; string line;
            if (why && PersonaData.Pools.TryGetValue("whyhere." + Cur.flavor, out f)) { string e = Pick(f); int b = e.IndexOf('|'); line = (b < 0 ? e : e.Substring(0, b)) + (b < 0 ? "" : " " + e.Substring(b + 1)); line = "Yes, " + title + " " + Me.Substring(Me.IndexOf(' ') + 1) + ". " + line; }
            else line = Pick(new[] { "Yes, I'm " + Me + ". Does that surprise you?", "That's the title, yes. Mind the manners.", "I am. Old habits." });
            return Voice(line, mood, name, true);
        }
        return null;
    }

    static string AnswerTopic(Topic_ tp, Mood mood, string name)
    {
        string[] f = Fact(tp.key);
        if (f == null) return null;

        int n; asked.TryGetValue(tp.id, out n); n++; asked[tp.id] = n;
        int mi = (int)mood;
        if (n >= 3) return Voice(Pick(PersonaData.AgainMany[mi]), mood, name, true);

        string a = Fill(f[0], name), b = Fill(f[1], name), line;
        Style st = PickStyle(tp, mood, b.Length > 0);
        switch (st)
        {
            case Style.Deep: line = a + " " + b; break;
            case Style.Dodge:
                string[] fl; PersonaData.DodgeFlavor.TryGetValue(Cur.flavor, out fl);
                line = (fl != null && Random.value < 0.5f) ? Pick(fl) : Pick(PersonaData.DodgeMood[mi]);
                break;
            case Style.Fumble: line = Stammer(a); break;
            case Style.Silent: return Pick(PersonaData.Silent);
            default:
                line = a;
                if (mi != 0 && Random.value < 0.35f) line = PersonaData.MoodOpen[mi] + " " + a;
                break;
        }
        if (n == 2 && st != Style.Silent) line = PersonaData.AgainOnce[mi] + line;
        if (Cur.warm > 0.5f && st == Style.Direct && mood == Mood.Patient && Random.value < 0.5f * Cur.talk && !NoProbe) line += " " + Pick(Back);
        return Voice(line, mood, name, true);
    }

    static readonly string[] Abbr = { "Mr", "Ms", "Mrs", "Dr", "Sgt", "Prof", "Sir" };

    static int SentenceEnd(string line)
    {
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c != '.' && c != '!' && c != '?') continue;
            if (c == '.')
            {
                int st = i; while (st > 0 && char.IsLetter(line[st - 1])) st--;
                if (System.Array.IndexOf(Abbr, line.Substring(st, i - st)) >= 0) continue;
            }
            return i;
        }
        return -1;
    }

    /// <summary>Persona colouring of any line: brevity, quirks, rudeness, warmth, advice, emotion.</summary>
    public static string Voice(string line, Mood mood, string name, bool answer)
    {
        PersonaDef p = Cur;
        if (p == null || string.IsNullOrEmpty(line) || line[0] == '(' || line == "...") return line;
        if (answer && p.talk < 0.3f)
        {
            int cut = SentenceEnd(line);
            if (cut > 0 && cut < line.Length - 1) line = line.Substring(0, cut + 1);
        }
        if (p.emote < 0.2f) line = line.Replace("!", ".");
        if (answer && Random.value < 0.12f + p.emote * 0.25f) line = Pick(p.quirks) + " " + line;
        if (answer && p.rude > 0.6f && Random.value < 0.5f) line += " " + Pick(PersonaData.RudeTags);
        if (answer && p.adv > 0.5f && p.advice.Length > 0 && Random.value < p.adv * 0.25f) line += " " + Pick(p.advice);
        else if (answer && p.warm > 0.8f && Random.value < 0.25f) line += " " + Fill(Pick(PersonaData.WarmTags), name);
        return line;
    }

    static readonly string[][] Nerves = { new[] { "Don't be nervous, {name}. Breathe.", "Nerves are natural. Relax." }, new[] { "Nerves won't help. Focus." }, new[] { "Nervous? About what, exactly?" }, new[] { "Mm. Nerves. Great." } };
    static readonly string[][] Happy = { new[] { "Wonderful, that's what I like to hear.", "Good. Hold on to that energy." }, new[] { "Good. Keep it that way." }, new[] { "Awfully cheerful, aren't you?" }, new[] { "Good for you." } };
    static readonly string[][] Tired = { new[] { "Oh dear. Long day? We'll keep it quick." }, new[] { "Then wake up." }, new[] { "Tired? Or hiding something?" }, new[] { "Same." } };
    static readonly string[][] Likes = { new[] { "{topic}? That sounds lovely.", "Oh, {topic}! Tell me more some other time." }, new[] { "{topic}. Fascinating. Stay focused." }, new[] { "{topic}. Is that so." }, new[] { "{topic}. Cool, I guess." } };
    static readonly string[][] Dislikes = { new[] { "{topic}? Oh dear. Why is that?", "Not a fan of {topic}? Noted, gently." }, new[] { "{topic}. Fine. Moving on." }, new[] { "{topic}? Why such a strong word?" }, new[] { "{topic}. Yeah. Whatever." } };
    static readonly string[][] Asking = { new[] { "Good question. I'd rather ask the questions today.", "Hard to say, honestly." }, new[] { "I ask the questions here.", "Not now. Focus.", "Couldn't tell you. Next." }, new[] { "Why do you ask that?", "What makes you curious about that?" }, new[] { "Dunno. Next.", "No idea. Ask someone else." } };
    static readonly string[][] OneWord = { new[] { "Just one word? Don't be shy." }, new[] { "Speak up. Full sentences." }, new[] { "That's all you have to say?" }, new[] { "Wow. Chatty." } };
    static readonly string[][] Callback = { new[] { "You mentioned {topic} earlier. I liked that." }, new[] { "Earlier you said {topic}. Stay on topic." }, new[] { "You mentioned {topic}. Why bring that up?" }, new[] { "Still on {topic}?" } };
    static readonly string[][] NotWell = { new[] { "Oh dear. Anything I can do, {name}?", "I'm sorry to hear that. We'll go gently." }, new[] { "Then pull yourself together." }, new[] { "Not well? Why not, exactly?" }, new[] { "Huh. Rough." } };
    static readonly string[][] Bounced = { new[] { "I'm well, thank you for asking, {name}.", "Can't complain. Kind of you to ask." }, new[] { "Busy. Let's keep going.", "I'd be better if we got on with it." }, new[] { "Me? Fine. Why do you ask?", "Fine. Don't worry about me." }, new[] { "Same old. Fine.", "Tired, honestly." } };
    static readonly string[] Negs = { "not", "dont", "never", "isnt", "aint", "cant", "wasnt", "hardly" };
    static readonly string[] Pron = { "its", "was", "are", "you", "your", "that", "this", "with", "have", "has", "had", "for", "and", "but", "not", "just", "thats", "what", "when", "they", "them", "their", "there", "here", "about", "from", "were", "been", "would", "could", "should" };
    static readonly string[] Wh = { "what", "why", "how", "where", "when", "who", "which" };
    static readonly string[] Aux = { "are", "do", "does", "did", "is", "was", "were", "would", "can", "could", "will", "have", "has", "should", "you", "u" };

    /// <summary>Type of question anywhere in the sentence: why / how / what / where / yesno, or null for a statement.</summary>
    static string QKind(List<string> t, string raw)
    {
        int n = t.Count;
        for (int i = 0; i < n; i++)
        {
            int w = System.Array.IndexOf(Wh, t[i]);
            if (w < 0) continue;
            bool aux = (i + 1 < n && System.Array.IndexOf(Aux, t[i + 1]) >= 0) || (i + 2 < n && System.Array.IndexOf(Aux, t[i + 2]) >= 0);
            if (!aux && (i != 0 || (i + 1 < n && (t[i + 1] == "a" || t[i + 1] == "an")))) continue;   // "what a nice office" is not a question
            return t[i] == "why" ? "why" : (t[i] == "how" ? "how" : (t[i] == "where" || t[i] == "when" ? "where" : "what"));
        }
        if (System.Array.IndexOf(QStart, t[0]) >= 0 && (t[0] == "do" || t[0] == "does" || t[0] == "did" || t[0] == "are" || t[0] == "is" || t[0] == "can" || t[0] == "could" || t[0] == "would" || t[0] == "will" || t[0] == "have" || t[0] == "was")) return "yesno";
        if ((raw ?? "").IndexOf('?') >= 0) return "yesno";
        return null;
    }

    static readonly string[] Qwords = { "what", "why", "how", "when", "where", "who", "do you", "are you", "did you", "can you", "have you", "will you", "is it" };

    static string From(string[][] b, Mood m, string name) { return Fill(Pick(b[(int)m]), name); }

    // statement reaction: persona-flavoured line most of the time, mood line otherwise
    static string Flv(string key, string[][] b, Mood m, string name)
    {
        string[] f;
        if (Cur != null && PersonaData.FlavorReact.TryGetValue(key + "." + Cur.flavor, out f) && Random.value < 0.6f) return Fill(Pick(f), name);
        return From(b, m, name);
    }

    // ---- review log: questions nobody answered, saved for P3-style tuning ----
    public static readonly List<string> Misses = new List<string>();
    static void LogMiss(string said)
    {
        string line = (Cur != null ? Cur.id : "-") + "\t" + said;
        Debug.Log("[Persona] unmatched question: " + said);
        if (Misses.Contains(line)) return;
        Misses.Add(line);
        try { System.IO.File.AppendAllText(Application.persistentDataPath + "/persona_unmatched.txt", line + "\n"); } catch { }
    }

    /// <summary>A statement from the player (feeling, like/love X, from X, one word, question) -> a reaction, or null.</summary>
    public static string React(string said, Mood mood, string name)
    {
        List<string> t = Toks(said);
        if (t.Count == 0) return null;
        string r = null;

        bool bounce = Bounce(said);
        if (Pending == "apply" || Pending == "hope" || Pending == "heard" || Has(t, "lol", "haha", "hahaha", "lmao"))
        {
            string pr = Dlg(t, mood, name, Pending != null && Pending != "other");   // the player is answering what was just asked
            if (pr != null) { Pending = null; return Voice(pr + (bounce ? " " + From(Bounced, mood, name) : ""), mood, name, true); }
        }
        bool notGood = false;   // "not good", "not very happy", "i'm not fine"
        for (int i = 1; i < t.Count; i++)
            if ((t[i] == "great" || t[i] == "fine" || t[i] == "good" || t[i] == "happy" || t[i] == "excited" || t[i] == "glad" || t[i] == "pumped")
                && (System.Array.IndexOf(Negs, t[i - 1]) >= 0 || (i > 1 && System.Array.IndexOf(Negs, t[i - 2]) >= 0))) notGood = true;
        if (notGood) r = Flv("notwell", NotWell, mood, name);
        else if (Tok(t, "nervous") || Tok(t, "anxious") || Tok(t, "scared") || Tok(t, "worried") || Tok(t, "stressed")) r = Flv("nerves", Nerves, mood, name);
        else if (Tok(t, "tired") || Tok(t, "exhausted") || Tok(t, "sleepy")) r = Flv("tired", Tired, mood, name);
        else if (Tok(t, "excited") || Tok(t, "happy") || Tok(t, "great") || Tok(t, "fine") || Tok(t, "good") || Tok(t, "pumped") || Tok(t, "glad")) r = Flv("happy", Happy, mood, name);

        if (r == null)
        {
            for (int i = 0; i < t.Count - 1; i++)
            {
                string w = t[i];
                bool hates = w == "hate" || w == "dislike" || w == "despise";
                bool likes = hates || w == "like" || w == "love" || w == "enjoy" || w == "play" || w == "into";
                bool from = (w == "from" || (w == "in" && i > 0 && t[i - 1] == "live"));
                if (!likes && !from) continue;
                int j = i + 1;
                while (j < t.Count && System.Array.IndexOf(Stop, t[j]) >= 0) j++;
                if (j >= t.Count) continue;
                var topic = new List<string>();
                for (int k = j; k < t.Count && topic.Count < 3; k++) topic.Add(t[k]);
                Topic = string.Join(" ", topic.ToArray());
                r = hates ? Flv("dislike", Dislikes, mood, name) : Flv("likes", Likes, mood, name);
                break;
            }
        }

        if (r == null && !bounce)
        {
            string kind = QKind(t, said);
            if (kind != null)
            {
                LogMiss(said);
                string[] f;
                // 55%: an in-flavor answer to the type of question; otherwise the mood brush-off
                if (Cur != null && PersonaData.QFallback.TryGetValue(kind + "." + Cur.flavor, out f) && Random.value < 0.55f) r = Fill(Pick(f), name);
                else r = From(Asking, mood, name);
            }
        }
        if (r == null && !bounce) r = Dlg(t, mood, name, Pending != null && Pending != "other");
        if (r == null && t.Count <= 1 && !bounce) r = From(OneWord, mood, name);
        if (r == null && Topic != null && Random.value < 0.5f) r = From(Callback, mood, name);

        if (r == null && !bounce && t.Count >= 2 && Has(t, Hedges)) r = Vague(mood, t.Count);   // hedged ("...i guess"): never echo
        if (r == null && !bounce && t.Count >= 3) r = Fill(Pick(AckPlain[(int)mood]), name);   // plain statement: neutral acknowledgement, never an echo of random words
        if (bounce) { string b = From(Bounced, mood, name); r = r == null ? b : r + " " + b; }
        else if (r != null && !NoProbe && mood == Mood.Patient && Random.value < 0.4f) r += " " + Pick(Ask);
        return r == null ? null : Voice(r, mood, name, true);
    }
}
