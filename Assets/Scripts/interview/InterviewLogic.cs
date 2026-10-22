using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum AnswerKind { Correct, Wrong, Hedge, None }

/// <summary>Static hand-off of the last interview result (read it from any later scene).</summary>
public static class InterviewResult
{
    public static bool Passed;
    public static float FinalSuspicion;
    public static int Asked;
    public static int Correct;
    public static string JobId = "";
    public static string Outcome = "";
    public static string Rating = "";
    public static float Score;
    public static string Tier = "";
    public static string MoodName = "";
    public static int Counters;
    public static int Contradictions;
    public static string ProofResult = "";
    public static string Report = "";      // full finalized report text (rich text) stored in History
}

/// <summary>
/// Builds the question list for one run from DrawConfig:
/// - follows the ordered slots (bucket / difficulty / type)
/// - avoids repeating tags within `tagCooldown` previous questions
/// - guarantees at least minYes "yes" and minNo "no" yes/no answers
/// </summary>
public static class QuestionDrawer
{
    public static List<QuestionData> Draw(JobData job, DrawConfig cfg)
    {
        if (job == null || job.questions == null || cfg == null || cfg.slots == null)
            return new List<QuestionData>();

        List<QuestionData> fallback = null;
        for (int attempt = 0; attempt < 300; attempt++)
        {
            bool strict = attempt < 200; // first try with tag cooldown, then relax it
            List<QuestionData> picks = TryDraw(job, cfg, strict);
            if (picks == null) continue;
            if (Balanced(picks, cfg)) return picks;
            if (fallback == null) fallback = picks;
        }
        if (fallback != null) return fallback;

        List<QuestionData> last = TryDraw(job, cfg, false);
        return last != null ? last : new List<QuestionData>();
    }

    static List<QuestionData> TryDraw(JobData job, DrawConfig cfg, bool strict)
    {
        int count = cfg.slots.Length;
        if (cfg.questionsPerRun > 0) count = Mathf.Min(count, cfg.questionsPerRun);

        List<QuestionData> result = new List<QuestionData>();
        HashSet<string> used = new HashSet<string>();

        for (int i = 0; i < count; i++)
        {
            DrawSlot slot = cfg.slots[i];
            List<QuestionData> candidates = new List<QuestionData>();
            for (int k = 0; k < job.questions.Length; k++)
            {
                QuestionData q = job.questions[k];
                if (q.bucket != slot.bucket || q.difficulty != slot.difficulty || q.type != slot.type) continue;
                if (used.Contains(q.id)) continue;
                if (strict && SharesRecentTag(q, result, cfg.tagCooldown)) continue;
                candidates.Add(q);
            }

            if (candidates.Count == 0)
            {
                if (strict) return null; // retry with another random order
                continue;                // data simply lacks this slot: skip it
            }

            QuestionData pick = candidates[Random.Range(0, candidates.Count)];
            used.Add(pick.id);
            result.Add(pick);
        }
        return result;
    }

    static bool SharesRecentTag(QuestionData q, List<QuestionData> chosen, int cooldown)
    {
        if (cooldown <= 0 || q.tags == null) return false;
        int start = Mathf.Max(0, chosen.Count - cooldown);
        for (int i = start; i < chosen.Count; i++)
        {
            string[] other = chosen[i].tags;
            if (other == null) continue;
            for (int a = 0; a < q.tags.Length; a++)
                for (int b = 0; b < other.Length; b++)
                    if (q.tags[a] == other[b]) return true;
        }
        return false;
    }

    static bool Balanced(List<QuestionData> picks, DrawConfig cfg)
    {
        int yes = 0, no = 0;
        for (int i = 0; i < picks.Count; i++)
        {
            if (picks[i].type != "yesno") continue;
            if (string.Equals(picks[i].correct, "yes", System.StringComparison.OrdinalIgnoreCase)) yes++;
            else if (string.Equals(picks[i].correct, "no", System.StringComparison.OrdinalIgnoreCase)) no++;
        }
        return yes >= cfg.minYes && no >= cfg.minNo;
    }
}

public static class AnswerEvaluator
{
    static readonly string[] DontKnow = { "idk", "i dont know", "dont know", "no idea", "dunno", "pass", "skip" };

    // ---------- yes / no ----------
    /// <param name="choice">"yes", "no" or "maybe"</param>
    public static AnswerKind EvaluateYesNo(QuestionData q, string choice)
    {
        if (choice == "maybe") return AnswerKind.Hedge;
        return string.Equals(q.correct, choice, System.StringComparison.OrdinalIgnoreCase)
            ? AnswerKind.Correct
            : AnswerKind.Wrong;
    }

    // ---------- short typed answers ----------
    public static AnswerKind EvaluateShort(QuestionData q, string text, ParsingConfig parsing)
    {
        string input = Normalize(text);
        if (input.Length == 0) return AnswerKind.None;

        List<string> answers = new List<string>();
        if (!string.IsNullOrEmpty(q.correct)) answers.Add(q.correct);
        if (q.accepted != null) answers.AddRange(q.accepted);

        for (int i = 0; i < answers.Count; i++)
        {
            string a = Normalize(answers[i]);
            if (a.Length == 0) continue;
            if (input == a) return AnswerKind.Correct;
            if (a.Length >= 3 && ContainsPhrase(input, a)) return AnswerKind.Correct;

            int tolerance = a.Length >= 8 ? 2 : (a.Length >= 5 ? 1 : 0); // forgive small typos
            if (tolerance > 0 && Levenshtein(input, a) <= tolerance) return AnswerKind.Correct;
        }

        if (parsing != null && parsing.hedgeWords != null && ContainsAny(input, parsing.hedgeWords)) return AnswerKind.Hedge;
        if (ContainsAny(input, DontKnow)) return AnswerKind.Hedge;
        return AnswerKind.Wrong;
    }

    // ---------- suspicion ----------
    /// <summary>
    /// Positive = more suspicion. Strength multiplier only scales penalties.
    /// Consecutive hedges cost progressively more (anti "always say maybe" exploit).
    /// </summary>
    public static float Delta(AnswerKind kind, QuestionData q, SuspicionConfig s, float multiplier, int hedgeStreak)
    {
        bool hard = q.difficulty == "hard";
        switch (kind)
        {
            case AnswerKind.Correct: return hard ? s.correctHard : s.correctEasy;
            case AnswerKind.Hedge: return s.hedge * (1f + 0.5f * hedgeStreak) * multiplier;
            case AnswerKind.None: return s.noAnswer * multiplier;
        }

        float v;
        if (q.bucket == "satirical") v = hard ? s.wrongSatiricalHard : s.wrongSatiricalEasy;
        else if (q.type == "yesno") v = hard ? s.wrongYesNoHard : s.wrongYesNoEasy;
        else v = hard ? s.wrongShortHard : s.wrongShortEasy;
        return v * multiplier;
    }

    // ---------- text helpers ----------
    public static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        StringBuilder sb = new StringBuilder(s.Length);
        string lower = s.ToLowerInvariant();
        bool lastSpace = true;
        for (int i = 0; i < lower.Length; i++)
        {
            char c = lower[i];
            if (c == '\'' || c == '\u2019') continue; // "ohm's" -> "ohms"
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastSpace = false;
            }
            else if (!lastSpace)
            {
                sb.Append(' ');
                lastSpace = true;
            }
        }
        return sb.ToString().Trim();
    }

    static bool ContainsPhrase(string normalizedInput, string normalizedPhrase)
    {
        return (" " + normalizedInput + " ").Contains(" " + normalizedPhrase + " ");
    }

    static bool ContainsAny(string normalizedInput, string[] phrases)
    {
        for (int i = 0; i < phrases.Length; i++)
        {
            string p = Normalize(phrases[i]);
            if (p.Length > 0 && ContainsPhrase(normalizedInput, p)) return true;
        }
        return false;
    }

    static int Levenshtein(string a, string b)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        int[] prev = new int[b.Length + 1];
        int[] cur = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Mathf.Min(Mathf.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            int[] t = prev; prev = cur; cur = t;
        }
        return prev[b.Length];
    }
}

/// <summary>
/// Builds questions about the player's own resume: name, hobby, education score, the claimed position,
/// and "combo" questions that mix several resume facts. Generated at runtime (not in interview_data.json).
/// bucket = "personal", difficulty = "easy" (AnswerEvaluator.Delta then uses the easy penalties).
///
/// Variety comes from three layers:
/// 1. kinds: name / hobby / grade / claim / combo (distinct kinds are used first)
/// 2. variants per kind (7 for name, 6 hobby, 7 grade, 3 claim, 2 combo): recall, derived facts
///    (letter counts, first letters, parity, thresholds, rounding), "A or B" choices, exact-value checks
/// 3. random wording templates, real-vs-fake values, and random thresholds / letters every run
/// A variant is not repeated inside one run unless the pool for that kind is exhausted.
/// </summary>
public static class PersonalQuestions
{
    static readonly string[] FakeFirstNames = Items.PopCultureNames.Short;

    static readonly string[] FakeHobbies =
        { "skydiving", "knitting", "stamp collecting", "competitive yodeling", "fencing", "pottery", "birdwatching",
          "origami", "surfing", "beekeeping", "karaoke", "rock climbing", "baking", "gardening", "archery", "chess",
          "painting", "scuba diving", "juggling", "woodworking", "salsa dancing", "fishing", "cycling", "podcasting",
          "model trains", "kite flying", "calligraphy", "geocaching" };

    static readonly string[] FallbackClaims =
        { "Professional cloud shepherd", "Senior wizard of spreadsheets", "Licensed electrician", "Registered nurse", "Chief vibes officer" };

    static readonly string[] StopWords = { "with", "and", "the", "playing", "watching", "going", "making", "doing", "collecting" };
    static readonly string[] ClaimStopWords = { "senior", "junior", "head", "chief", "lead", "licensed", "certified", "registered", "with", "and", "the", "for" };

    static readonly string[] NumberWords =
        { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
          "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty" };

    const string Alphabet = "abcdefghijklmnopqrstuvwxyz";

    // ------------------------------------------------------------------
    // building the list
    // ------------------------------------------------------------------
    public static List<QuestionData> Build(int count, string name, string hobby, int score)
    {
        List<QuestionData> list = new List<QuestionData>();
        count = Mathf.Min(count, 6);
        if (count <= 0 || string.IsNullOrEmpty(name)) return list;

        bool hasHobby = !string.IsNullOrEmpty(hobby);
        bool hasClaim = !string.IsNullOrEmpty(ResumeSession.SelectedText);

        List<string> kinds = new List<string>();
        kinds.Add("name");
        if (hasHobby) kinds.Add("hobby");
        kinds.Add("grade");
        if (hasClaim) kinds.Add("claim");
        if (hasHobby || hasClaim) kinds.Add("combo");
        for (int i = kinds.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            string t = kinds[i]; kinds[i] = kinds[j]; kinds[j] = t;
        }

        Dictionary<string, List<int>> used = new Dictionary<string, List<int>>();
        for (int i = 0; i < count; i++)
        {
            string kind = kinds[i % kinds.Count];
            List<int> usedVariants;
            if (!used.TryGetValue(kind, out usedVariants))
            {
                usedVariants = new List<int>();
                used[kind] = usedVariants;
            }
            int variant = PickVariant(VariantCount(kind), usedVariants);
            list.Add(Make(kind, variant, name, hobby, score));
        }
        return list;
    }

    static int VariantCount(string kind)
    {
        if (kind == "name") return 7;
        if (kind == "hobby") return 6;
        if (kind == "grade") return 7;
        if (kind == "claim") return 3;
        return 2; // combo
    }

    static int PickVariant(int total, List<int> used)
    {
        if (used.Count >= total) used.Clear(); // pool exhausted: start over
        for (int attempt = 0; attempt < 30; attempt++)
        {
            int v = Random.Range(0, total);
            if (!used.Contains(v))
            {
                used.Add(v);
                return v;
            }
        }
        return Random.Range(0, total);
    }

    static QuestionData Make(string kind, int variant, string name, string hobby, int score)
    {
        QuestionData q = new QuestionData();
        q.bucket = "personal";
        q.difficulty = "easy";
        q.type = "yesno";
        q.tags = new string[] { "personal", kind };
        q.id = "personal_" + kind + "_" + variant;
        q.accepted = new string[0];

        if (kind == "name") MakeName(q, variant, name);
        else if (kind == "hobby") MakeHobby(q, variant, hobby);
        else if (kind == "grade") MakeGrade(q, variant, score);
        else if (kind == "claim") MakeClaim(q, variant);
        else MakeCombo(q, variant, name, hobby);
        return q;
    }

    // ------------------------------------------------------------------
    // name
    // ------------------------------------------------------------------
    static void MakeName(QuestionData q, int variant, string name)
    {
        string[] w = Words(name);
        string first = w.Length > 0 ? w[0] : name;
        bool multi = w.Length > 1;
        string last = multi ? w[w.Length - 1] : first;

        if (variant == 0)
        {
            SetShort(q, Pick(new string[] { "State your full name for the record.", "Please say your full name for the record." }), name, w);
        }
        else if (variant == 1)
        {
            SetShort(q, Pick(new string[] { "What is your first name?", "What should I call you? First name only." }), first, null);
        }
        else if (variant == 2)
        {
            bool real = Random.value < 0.5f;
            string shown = real ? name : PickFakeName(name);
            SetYesNo(q, Fill(Pick(new string[] { "Is your name {0}?", "Just to confirm, your name is {0}?", "So you are {0}, correct?" }), shown), real);
        }
        else if (variant == 3)
        {
            int n = CountLetters(first);
            SetShort(q, "How many letters are in your first name?", n.ToString(), NumberWord(n));
        }
        else if (variant == 4)
        {
            bool present = Random.value < 0.5f;
            char c = present ? RandomLetterOf(name) : AbsentLetter(name);
            if (c == '\0')
            {
                present = true;
                c = RandomLetterOf(name);
            }
            if (c == '\0')
            {
                SetYesNo(q, "Is your name " + name + "?", true);
                return;
            }
            SetYesNo(q, Fill(Pick(new string[] { "Does your name contain the letter {0}?", "Is there a {0} anywhere in your name?" }), char.ToUpperInvariant(c).ToString()), present);
        }
        else if (variant == 5)
        {
            if (multi) SetShort(q, "What is your last name?", last, null);
            else SetShort(q, "What letter does your name start with?", FirstLetter(first), null);
        }
        else
        {
            int len = CountLetters(first);
            int t = len + (Random.value < 0.5f ? -1 : 1) * Random.Range(1, 4);
            if (t < 1) t = len + Random.Range(1, 4);
            SetYesNo(q, "Is your first name longer than " + t + " letters?", len > t);
        }
    }

    // ------------------------------------------------------------------
    // hobby
    // ------------------------------------------------------------------
    static void MakeHobby(QuestionData q, int variant, string hobby)
    {
        string[] hobbyWords = SignificantWords(hobby, StopWords);

        if (variant == 0)
        {
            SetShort(q, Pick(new string[] { "What hobby did you put on your resume?", "What do you do in your free time?" }), hobby, hobbyWords);
        }
        else if (variant == 1)
        {
            bool real = Random.value < 0.5f;
            string shown = real ? hobby : PickFakeHobby(hobby);
            SetYesNo(q, Fill(Pick(new string[] { "Is {0} your hobby?", "You listed {0} as a hobby, correct?", "Do you really spend your free time on {0}?" }), shown), real);
        }
        else if (variant == 2)
        {
            string fake = PickFakeHobby(hobby);
            string a = hobby;
            string b = fake;
            if (Random.value < 0.5f) { a = fake; b = hobby; }
            SetShort(q, "Which is your hobby: " + a + " or " + b + "?", hobby, hobbyWords);
        }
        else if (variant == 3)
        {
            bool yes = Random.value < 0.5f;
            string real = FirstLetter(hobby);
            string letter = real;
            if (!yes)
            {
                for (int attempt = 0; attempt < 30 && letter == real; attempt++)
                    letter = Alphabet[Random.Range(0, Alphabet.Length)].ToString().ToUpperInvariant();
                if (letter == real) yes = true;
            }
            SetYesNo(q, "Does your hobby start with the letter " + letter + "?", yes);
        }
        else if (variant == 4)
        {
            SetShort(q, "What letter does your hobby begin with?", FirstLetter(hobby), null);
        }
        else
        {
            SetYesNo(q, Pick(new string[] { "Is your hobby more than one word long?", "Did you need more than one word to describe your hobby?" }), Words(hobby).Length > 1);
        }
    }

    // ------------------------------------------------------------------
    // education score
    // ------------------------------------------------------------------
    static void MakeGrade(QuestionData q, int variant, int score)
    {
        if (variant == 0)
        {
            SetShort(q, "What was your final education score? Numbers only.", score.ToString(), null);
        }
        else if (variant == 1 || variant == 2)
        {
            int t = ThresholdAround(score);
            if (variant == 1) SetYesNo(q, Pick(new string[] { "Did you score above " + t + "% in education?", "Is your education score higher than " + t + "%?" }), score > t);
            else SetYesNo(q, Pick(new string[] { "Is your education score below " + t + "%?", "Did you finish under " + t + "% in education?" }), score < t);
        }
        else if (variant == 3)
        {
            SetYesNo(q, "Is your education score an even number?", score % 2 == 0);
        }
        else if (variant == 4)
        {
            int r = ((score + 5) / 10) * 10;
            SetShort(q, "Round your education score to the nearest ten (5 rounds up). Numbers only.", r.ToString(), null);
        }
        else if (variant == 5)
        {
            bool real = Random.value < 0.4f;
            int shown = score;
            if (!real)
            {
                shown = -1;
                for (int attempt = 0; attempt < 20 && shown < 0; attempt++)
                {
                    int cand = score + (Random.value < 0.5f ? -1 : 1) * Random.Range(1, 8);
                    if (cand >= 0 && cand <= 100 && cand != score) shown = cand;
                }
                if (shown < 0)
                {
                    real = true;
                    shown = score;
                }
            }
            SetYesNo(q, Pick(new string[] { "Did you score exactly " + shown + "%?", "Is " + shown + "% your exact education score?" }), real);
        }
        else
        {
            int[] marks = { 40, 50, 60, 70, 80 };
            int m = marks[Random.Range(0, marks.Length)];
            SetYesNo(q, "Is your education score at least " + m + "%?", score >= m);
        }
    }

    static int ThresholdAround(int score)
    {
        int gap = Random.Range(8, 26);
        bool above = Random.value < 0.5f;
        int t = above ? score - gap : score + gap;
        if (t < 1 || t > 99) t = above ? score + gap : score - gap;
        return Mathf.Clamp(t, 1, 99);
    }

    // ------------------------------------------------------------------
    // claimed position (the experience option the player picked)
    // ------------------------------------------------------------------
    static void MakeClaim(QuestionData q, int variant)
    {
        string claim = ResumeSession.SelectedText;
        string[] claimWords = SignificantWords(claim, ClaimStopWords);

        if (variant == 0)
        {
            SetShort(q, Pick(new string[] { "What position is listed on your resume?", "What is your line of work, according to your resume?" }), claim, claimWords);
        }
        else if (variant == 1)
        {
            bool real = Random.value < 0.5f;
            string shown = real ? claim : PickFakeClaim(claim);
            SetYesNo(q, Fill(Pick(new string[] { "Your resume says: {0}. Is that right?", "Do you stand by this claim: {0}?", "Is \"{0}\" really your line of work?" }), shown), real);
        }
        else
        {
            string fake = PickFakeClaim(claim);
            string a = claim;
            string b = fake;
            if (Random.value < 0.5f) { a = fake; b = claim; }
            SetShort(q, "Which is on your resume: \"" + a + "\" or \"" + b + "\"?", claim, claimWords);
        }
    }

    // ------------------------------------------------------------------
    // combo: several resume facts in one yes/no question (one wrong fact makes the answer "no")
    // ------------------------------------------------------------------
    static void MakeCombo(QuestionData q, int variant, string name, string hobby)
    {
        string claim = ResumeSession.SelectedText;
        bool hasHobby = !string.IsNullOrEmpty(hobby);
        bool hasClaim = !string.IsNullOrEmpty(claim);
        if (variant == 0 && !hasHobby) variant = 1;
        if (variant == 1 && !hasClaim) variant = 0;

        bool nameReal = true;
        bool otherReal = true;
        if (Random.value < 0.5f)
        {
            if (Random.value < 0.5f) nameReal = false;
            else otherReal = false;
        }
        string shownName = nameReal ? name : PickFakeName(name);

        if (variant == 0 && hasHobby)
        {
            string shownHobby = otherReal ? hobby : PickFakeHobby(hobby);
            string text = Pick(new string[]
            {
                "So you are {N}, and in your spare time you do {X}. Correct?",
                "Your name is {N} and your hobby is {X}. Is that right?"
            });
            SetYesNo(q, text.Replace("{N}", shownName).Replace("{X}", shownHobby), nameReal && otherReal);
        }
        else if (hasClaim)
        {
            string shownClaim = otherReal ? claim : PickFakeClaim(claim);
            string text = Pick(new string[]
            {
                "So you are {N}, listed as: {X}. Correct?",
                "{N}, applying as: {X}. Is that you?"
            });
            SetYesNo(q, text.Replace("{N}", shownName).Replace("{X}", shownClaim), nameReal && otherReal);
        }
        else
        {
            SetYesNo(q, "Is your name " + shownName + "?", nameReal);
        }
    }

    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------
    static void SetShort(QuestionData q, string text, string correct, string[] accepted)
    {
        q.type = "short";
        q.text = text;
        q.correct = correct;
        if (accepted != null) q.accepted = accepted;
    }

    static void SetYesNo(QuestionData q, string text, bool yes)
    {
        q.type = "yesno";
        q.text = text;
        q.correct = yes ? "yes" : "no";
    }

    static string Pick(string[] options)
    {
        return options[Random.Range(0, options.Length)];
    }

    static string Fill(string template, string value)
    {
        return template.Replace("{0}", value);
    }

    static string[] Words(string s)
    {
        return s.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
    }

    static int CountLetters(string s)
    {
        int n = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsLetter(s[i])) n++;
        }
        return n;
    }

    static string FirstLetter(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsLetter(s[i])) return char.ToUpperInvariant(s[i]).ToString();
        }
        return s.Length > 0 ? s.Substring(0, 1) : "";
    }

    static char RandomLetterOf(string s)
    {
        List<char> letters = new List<char>();
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsLetter(s[i])) letters.Add(char.ToLowerInvariant(s[i]));
        }
        return letters.Count > 0 ? letters[Random.Range(0, letters.Count)] : '\0';
    }

    static char AbsentLetter(string s)
    {
        string lower = s.ToLowerInvariant();
        for (int attempt = 0; attempt < 40; attempt++)
        {
            char c = Alphabet[Random.Range(0, Alphabet.Length)];
            if (lower.IndexOf(c) < 0) return c;
        }
        return '\0';
    }

    static string[] NumberWord(int n)
    {
        if (n >= 0 && n < NumberWords.Length) return new string[] { NumberWords[n] };
        return new string[0];
    }

    /// <summary>Words of 4+ letters that are not filler: typing just one of them is accepted.</summary>
    static string[] SignificantWords(string text, string[] stop)
    {
        List<string> words = new List<string>();
        string[] parts = Words(text);
        for (int i = 0; i < parts.Length; i++)
        {
            string n = AnswerEvaluator.Normalize(parts[i]);
            if (n.Length >= 4 && System.Array.IndexOf(stop, n) < 0) words.Add(parts[i]);
        }
        return words.ToArray();
    }

    static string PickFakeName(string realName)
    {
        string normReal = AnswerEvaluator.Normalize(realName);
        string[] parts = Words(realName);
        for (int attempt = 0; attempt < 30; attempt++)
        {
            string f = FakeFirstNames[Random.Range(0, FakeFirstNames.Length)];
            if ((" " + normReal + " ").Contains(" " + AnswerEvaluator.Normalize(f) + " ")) continue;
            // keep the real surname sometimes: "Jordan Smith" instead of the real "Sam Smith"
            if (parts.Length > 1 && Random.value < 0.5f) return f + " " + string.Join(" ", parts, 1, parts.Length - 1);
            return f;
        }
        return "Zzyzx";
    }

    static string PickFakeHobby(string realHobby)
    {
        string normReal = AnswerEvaluator.Normalize(realHobby);
        for (int attempt = 0; attempt < 30; attempt++)
        {
            string f = FakeHobbies[Random.Range(0, FakeHobbies.Length)];
            string nf = AnswerEvaluator.Normalize(f);
            if (!normReal.Contains(nf) && !nf.Contains(normReal)) return f;
        }
        return "underwater basket weaving";
    }

    /// <summary>A believable wrong claim: prefers the options the player was offered but did not pick.</summary>
    static string PickFakeClaim(string realClaim)
    {
        List<string> candidates = new List<string>();
        string[] offered = ResumeSession.OfferedTexts;
        if (offered != null)
        {
            for (int i = 0; i < offered.Length; i++)
            {
                if (!string.IsNullOrEmpty(offered[i]) && offered[i] != realClaim) candidates.Add(offered[i]);
            }
        }
        if (candidates.Count == 0 && ResumeSession.Database != null && ResumeSession.Database.jobs != null)
        {
            JobData[] jobs = ResumeSession.Database.jobs;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                JobData j = jobs[Random.Range(0, jobs.Length)];
                if (j == ResumeSession.SelectedJob || j.displayVariants == null || j.displayVariants.Length == 0) continue;
                string v = j.displayVariants[Random.Range(0, j.displayVariants.Length)];
                if (v != realClaim) candidates.Add(v);
            }
        }
        if (candidates.Count == 0)
        {
            for (int i = 0; i < FallbackClaims.Length; i++)
            {
                if (FallbackClaims[i] != realClaim) candidates.Add(FallbackClaims[i]);
            }
        }
        return candidates[Random.Range(0, candidates.Count)];
    }
}

/// <summary>
/// Swaps a few job questions for personal ones, keeping the total (questionsPerRun) the same.
/// Personal questions land at random positions. Easy job questions are dropped first so the hard ones stay.
/// </summary>
public static class QuestionMixer
{
    public static List<QuestionData> Mix(List<QuestionData> jobQuestions, int minPersonal, int maxPersonal,
        string name, string hobby, int score)
    {
        if (jobQuestions == null) return new List<QuestionData>();
        if (string.IsNullOrEmpty(name) || maxPersonal <= 0) return jobQuestions;

        int count = Random.Range(Mathf.Max(0, minPersonal), Mathf.Max(minPersonal, maxPersonal) + 1);
        List<QuestionData> personal = PersonalQuestions.Build(count, name, hobby, score);
        List<QuestionData> result = new List<QuestionData>(jobQuestions);

        for (int n = 0; n < personal.Count && result.Count > 1; n++)
        {
            List<int> easy = new List<int>();
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i].difficulty == "easy") easy.Add(i);
            }
            int remove = easy.Count > 0 ? easy[Random.Range(0, easy.Count)] : Random.Range(0, result.Count);
            result.RemoveAt(remove);
        }

        for (int n = 0; n < personal.Count; n++)
        {
            result.Insert(Random.Range(0, result.Count + 1), personal[n]);
        }
        return result;
    }
}
public enum Mood { Patient, Impatient, Paranoid, Bored }
public enum MoodEvent { Correct, Wrong, Hedge, NoAnswer, Slow, Fast, Contradiction, ProofBad, ProofOk, Polite, Rude }

public enum Remark
{
    Greeting, SlowMild, Impatient, Fast, Correct, Wrong, Hedge, NoAnswer,
    Pressure, Dodge, Contradiction, ProofAsk, ProofOk, ProofBad, BluffOk, BluffBad, Warn1, Warn2
}

public static class InterviewerPersonality
{
    static readonly string[] E = new string[0];

    static readonly string[][] TGreeting =
    {
        new string[] { "Now then. We will start gently." },
        new string[] { "Good. I have limited time, so let's go." },
        new string[] { "Good. I am watching. Let's see what you are hiding." },
        new string[] { "Right. Let's get this over with." },
        E
    };
    static readonly string[][] TSlowMild =
    {
        new string[] { "Take your time.", "No rush. Well, not much of one." },
        new string[] { "Hm. The clock is ticking." },
        new string[] { "Why the hesitation?" },
        new string[] { "I could knit a scarf by now." },
        new string[] { "Still thinking?", "Mm. Pausing a lot, aren't we?" }
    };
    static readonly string[][] TImpatient =
    {
        new string[] { "I do not mean to rush you, but I really must." },
        new string[] { "Come on. I do not have all day." },
        new string[] { "Stalling? That looks bad." },
        new string[] { "Wake me when you have an answer." },
        new string[] { "I have other candidates waiting, you know.", "Can we speed this up, please?", "That is the third long pause. Faster, please." }
    };
    static readonly string[][] TFast =
    {
        new string[] { "Quick. Confident, or just eager?" },
        new string[] { "Fast. Good. I like that. I think." },
        new string[] { "That fast? Suspicious." },
        new string[] { "Wow. Zero thinking. Impressive. Or not." },
        new string[] { "That was quick. Rehearsed, are we?", "Did you even think about that?", "No hesitation at all. Interesting." }
    };
    static readonly string[][] TCorrect =
    {
        new string[] { "Nicely put." },
        new string[] { "Fine. Next." },
        new string[] { "...Convenient." },
        new string[] { "Sure. Whatever." },
        new string[] { "Good.", "Mm-hm.", "That matches my notes." }
    };
    static readonly string[][] TWrong =
    {
        new string[] { "Are you certain? Alright." },
        new string[] { "Seriously?" },
        new string[] { "Interesting. Very interesting." },
        new string[] { "Right. Noted. Sigh." },
        new string[] { "Hm.", "That is not what I expected.", "I will make a note of that." }
    };
    static readonly string[][] THedge =
    {
        new string[] { "Maybe is a very polite non-answer." },
        new string[] { "Maybe? Pick a side." },
        new string[] { "Evasive. I noticed." },
        new string[] { "Maybe. Riveting." },
        new string[] { "Maybe is not an answer.", "Dodging, are we?" }
    };
    static readonly string[][] TNoAnswer =
    {
        E, E, E, E,
        new string[] { "Silence. Bold strategy.", "Hello? Still with me?", "Nothing? Alright." }
    };
    static readonly string[][] TPressure =
    {
        new string[] { "Take a moment. Are you sure about that?" },
        new string[] { "Sure? Last chance to change it." },
        new string[] { "Really? You are sure?" },
        new string[] { "...you sure?" },
        new string[] { "Are you sure about that?", "Think carefully. Are you sure?" }
    };
    static readonly string[][] TDodge =
    {
        new string[] { "Let's try again, gently. Pick one." },
        new string[] { "No dodging. Answer." },
        new string[] { "Avoiding the question again?" },
        new string[] { "Ugh. Just pick one." },
        new string[] { "Do not dodge. Yes or no?", "That is not an answer. Once more:" }
    };
    static readonly string[][] TContradiction =
    {
        new string[] { "Just to double check, you said {0} before:" },
        new string[] { "Quickly now, earlier you said {0}:" },
        new string[] { "Funny. Earlier you told me {0}. Again:" },
        new string[] { "Remind me, you said {0} earlier. Again:" },
        new string[] { "Hold on. Earlier you said {0} to this one:", "Let me check something. You answered {0} before:" }
    };
    static readonly string[][] TProofAsk =
    {
        new string[] { "Whenever you are ready, I will need to see your {0}." },
        new string[] { "Your {0}. Now, please." },
        new string[] { "I want to see your {0}. Now." },
        new string[] { "Procedure. {0}, please." },
        new string[] { "Show me your {0}." }
    };
    static readonly string[][] TProofOk =
    {
        E, E, E, E,
        new string[] { "Everything seems in order.", "That checks out. Good." }
    };
    static readonly string[][] TProofBad =
    {
        E, E, E, E,
        new string[] { "This does not check out.", "Hm. That is a problem." }
    };
    static readonly string[][] TBluffOk =
    {
        E, E, E, E,
        new string[] { "Hm. Fine. Bring it next time.", "I will let it slide. This once." }
    };
    static readonly string[][] TBluffBad =
    {
        E, E, E, E,
        new string[] { "Left at home. Right.", "How convenient." }
    };
    static readonly string[][] TWarn1 =
    {
        E, E, E, E,
        new string[] { "I am starting to doubt this resume.", "Something about you does not add up." }
    };
    static readonly string[][] TWarn2 =
    {
        E, E, E, E,
        new string[] { "One more slip and we are done here.", "You are running out of rope." }
    };

    static string[][] Table(Remark r)
    {
        switch (r)
        {
            case Remark.Greeting: return TGreeting;
            case Remark.SlowMild: return TSlowMild;
            case Remark.Impatient: return TImpatient;
            case Remark.Fast: return TFast;
            case Remark.Correct: return TCorrect;
            case Remark.Wrong: return TWrong;
            case Remark.Hedge: return THedge;
            case Remark.NoAnswer: return TNoAnswer;
            case Remark.Pressure: return TPressure;
            case Remark.Dodge: return TDodge;
            case Remark.Contradiction: return TContradiction;
            case Remark.ProofAsk: return TProofAsk;
            case Remark.ProofOk: return TProofOk;
            case Remark.ProofBad: return TProofBad;
            case Remark.BluffOk: return TBluffOk;
            case Remark.BluffBad: return TBluffBad;
            case Remark.Warn1: return TWarn1;
        }
        return TWarn2;
    }

    // ---------- dynamic mood engine: the interviewer's mood follows how the interview goes ----------
    static float[] moodScore = new float[4];
    static int sinceShift;
    public static Mood Current = Mood.Patient;

    public static void BeginMood(Mood start)
    {
        moodScore = new float[4]; moodScore[(int)start] = 1.4f; Current = start; sinceShift = 0;
    }

    static void Add(Mood m, float v) { moodScore[(int)m] = Mathf.Clamp(moodScore[(int)m] + v * Persona.MoodMul(m), -1f, 3.5f); }   // persona scales how easily each mood is reached

    /// <summary>Feed an event; returns the (possibly changed) mood. Scores decay each event and the winner needs a margin to take over.</summary>
    public static Mood Feel(MoodEvent e, float susp01)
    {
        for (int i = 0; i < 4; i++) moodScore[i] *= 0.9f;
        switch (e)
        {
            case MoodEvent.Correct: Add(Mood.Patient, 0.5f); Add(Mood.Impatient, -0.4f); Add(Mood.Bored, GoodStreak >= 3 ? 0.55f : 0.2f); Add(Mood.Paranoid, -0.1f); break;
            case MoodEvent.Wrong: Add(Mood.Impatient, 0.6f); Add(Mood.Paranoid, 0.5f); Add(Mood.Patient, -0.5f); Add(Mood.Bored, -0.3f); break;
            case MoodEvent.Hedge: Add(Mood.Paranoid, 0.5f); Add(Mood.Impatient, 0.3f); Add(Mood.Patient, -0.3f); break;
            case MoodEvent.NoAnswer: Add(Mood.Impatient, 0.8f); Add(Mood.Bored, 0.2f); Add(Mood.Patient, -0.4f); break;
            case MoodEvent.Slow: Add(Mood.Impatient, 0.55f); Add(Mood.Bored, 0.35f); Add(Mood.Patient, -0.25f); break;
            case MoodEvent.Fast: Add(Mood.Paranoid, 0.45f); Add(Mood.Bored, -0.2f); break;
            case MoodEvent.Contradiction: Add(Mood.Paranoid, 1.0f); Add(Mood.Impatient, 0.3f); break;
            case MoodEvent.ProofBad: Add(Mood.Paranoid, 0.9f); Add(Mood.Impatient, 0.5f); Add(Mood.Patient, -0.6f); break;
            case MoodEvent.ProofOk: Add(Mood.Patient, 0.5f); Add(Mood.Paranoid, -0.6f); break;
            case MoodEvent.Polite: Add(Mood.Patient, 0.45f); Add(Mood.Impatient, -0.35f); break;
            case MoodEvent.Rude: Add(Mood.Impatient, 0.6f); Add(Mood.Patient, -0.45f); break;
        }
        if (susp01 > 0.6f) Add(Mood.Paranoid, 0.2f);
        if (susp01 > 0.8f) Add(Mood.Impatient, 0.2f);
        if (susp01 < 0.2f) Add(Mood.Patient, 0.1f);
        sinceShift++;
        int best = (int)Current;
        for (int i = 0; i < 4; i++) if (moodScore[i] > moodScore[best] + 0.35f && moodScore[i] > 0.9f) best = i;
        if (best != (int)Current && sinceShift >= 2) { Current = (Mood)best; sinceShift = 0; }
        return Current;
    }

    static readonly string[][] ShiftBank =
    {
        new[] { "Hm, let's calm down and take this gently.", "Alright, you're growing on me." },
        new[] { "You are testing my patience.", "That's it. My patience just ran out.", "Do you have any idea how slow this is?" },
        new[] { "Something is off about you. I can feel it.", "I don't like where this is going.", "Wait. Something doesn't add up." },
        new[] { "...This is dragging on.", "Mm. Wake me up when it's interesting.", "Are we done yet?" }
    };
    public static string MoodShiftLine(Mood to) { return Pick(ShiftBank[(int)to]); }

    // ---------- live context (set by the UI) so lines react to the situation ----------
    public static float Susp01; public static int GoodStreak, BadStreak, Asked;
    public static void ResetContext() { Susp01 = 0f; GoodStreak = BadStreak = Asked = 0; }
    public static void Note(AnswerKind k, float susp01, int asked)
    {
        Susp01 = Mathf.Clamp01(susp01); Asked = asked;
        if (k == AnswerKind.Correct) { GoodStreak++; BadStreak = 0; } else { BadStreak++; GoodStreak = 0; }
    }

    // mood index: 0 Patient(friendly) 1 Impatient(rude/angry) 2 Paranoid(accusing) 3 Bored(terse)
    static readonly string[][] OpenPos =
    {
        new[] { "Lovely.", "Wonderful.", "Excellent!", "Great answer.", "Ah, perfect." },
        new[] { "Fine.", "About time.", "Acceptable.", "Hmph. Correct." },
        new[] { "Hm. Plausible.", "Consistent. So far.", "Noted." },
        new[] { "Mm.", "Sure.", "Yep." }
    };
    static readonly string[][] OpenNeg =
    {
        new[] { "Oh dear.", "Ah, not quite.", "Hmm, close, but no.", "Oh, that's a shame." },
        new[] { "Seriously?", "Oh, come ON.", "Unbelievable.", "Are you kidding me?", "Wrong." },
        new[] { "Interesting.", "I see.", "Is that so?", "Hm. Careful now." },
        new[] { "Ugh.", "Nope.", "Mm." }
    };
    static readonly string[][] OpenNeu =
    {
        new[] { "Alright.", "Okay then.", "Right, lovely." },
        new[] { "Right.", "Okay.", "Move on." },
        new[] { "Hm.", "Go on.", "Very well." },
        new[] { "Okay.", "Next.", "Mm." }
    };
    static readonly string[][] TailGood =
    {
        new[] { "You're doing wonderfully!", "Keep it up!" },
        new[] { "Don't get cocky.", "Fine, you know your stuff." },
        new[] { "Suspiciously consistent.", "Too smooth. I'm watching." },
        new[] { "Whatever.", "Yeah, yeah." }
    };
    static readonly string[][] TailBad =
    {
        new[] { "Take a breath, it's okay.", "No worries, we all slip." },
        new[] { "That's a pattern.", "Try harder.", "You're wasting my time." },
        new[] { "That's twice now.", "Your story is wobbling." },
        new[] { "Sigh.", "Painful." }
    };
    static readonly string[][] TailHigh =
    {
        new[] { "I'm getting a little concerned.", "I hope there's an explanation." },
        new[] { "My patience is gone.", "One more and you're out." },
        new[] { "Things aren't adding up.", "I know you're hiding something." },
        new[] { "Can we wrap this up?", "This is going badly." }
    };
    static readonly string[][] GreetBank =
    {
        new[] { "{0} and welcome! So glad you could make it.", "Ah, hello there! Relax, this will be painless.", "{0}! Come in, come in. Nothing to be nervous about.", "Hello! Lovely to meet you. Let's have a nice chat.", "Welcome! I've been looking forward to this one." },
        new[] { "Finally. You're late. Let's go.", "{0}. I hope you're ready, because I'm not waiting.", "Right. Sit. Talk. Fast.", "Another one. Let's see if you're worth my time.", "I have a packed schedule. Don't waste it." },
        new[] { "{0}. No sudden moves. I'm watching you.", "So you're the candidate. Hm. Interesting.", "Close the door behind you. ...Good. Now we talk.", "I've read your resume. Twice. Something's off.", "Hands where I can see them." },
        new[] { "Hi.", "Yeah. Hello.", "Mm. Okay. Start whenever.", "Right. Let's do this.", "...Oh, you're here. Okay." }
    };
    static readonly string[][] SeatBank =
    {
        new[] { "Please, take a seat. Make yourself comfortable.", "Have a seat. No pressure at all.", "Do sit down, there's a chair right there.", "Please, sit. Some water first?" },
        new[] { "Sit. Now.", "Take a seat. Quickly.", "Don't just stand there. Sit.", "Chair. Sit. We're already behind." },
        new[] { "Take a seat. Slowly. Where I can see you.", "Sit. Keep your hands visible.", "The chair. Go on. Carefully.", "Sit down. I'll be watching every move." },
        new[] { "Sit.", "Take a seat, I guess.", "Chair's there.", "...Seat. Yeah." }
    };

    static string Pick(string[] a) { return a[Random.Range(0, a.Length)]; }

    public static string Greeting(Mood m)
    {
        int h = System.DateTime.Now.Hour;
        string tod = h < 12 ? "Good morning" : (h < 17 ? "Good afternoon" : "Good evening");
        return Pick(GreetBank[(int)m]).Replace("{0}", tod);
    }

    public static string SeatLine(Mood m) { return Pick(SeatBank[(int)m]); }

    // 0 positive, 1 negative, 2 neutral
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

    public static string Line(Remark r, Mood m)
    {
        if (r == Remark.Greeting) return Greeting(m);
        int mi = (int)m;
        string[][] t = Table(r);
        List<string> pool = new List<string>(t[4]);
        pool.AddRange(t[mi]); pool.AddRange(t[mi]);
        string core = pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : "";
        // these are glued to a question or carry a {0} slot, so they must stay intact
        if (r == Remark.Contradiction || r == Remark.ProofAsk || r == Remark.Pressure || r == Remark.Dodge) return core;

        int cls = Cls(r);
        string[][] bank = cls == 0 ? OpenPos : (cls == 1 ? OpenNeg : OpenNeu);

        if (m == Mood.Bored)   // minimal talking
        {
            if (core == "") return Random.value < 0.5f ? "" : Pick(bank[mi]);
            string alt = pool[Random.Range(0, pool.Count)];
            if (alt.Length < core.Length) core = alt;
            return core.Length > 22 && Random.value < 0.6f ? Pick(bank[mi]) : core;
        }

        string open = Random.value < 0.4f ? Pick(bank[mi]) : "";
        if (core == "") return Random.value < 0.7f ? (open != "" ? open : Pick(bank[mi])) : "";
        string tail = "";
        if (Random.value < 0.35f)
        {
            if (Susp01 > 0.7f && cls == 1) tail = Pick(TailHigh[mi]);
            else if (cls == 0 && GoodStreak >= 3) tail = Pick(TailGood[mi]);
            else if (cls == 1 && BadStreak >= 2) tail = Pick(TailBad[mi]);
        }
        return (open + " " + core + " " + tail).Trim();
    }

    public static Mood Roll()
    {
        return (Mood)Random.Range(0, 4);
    }

    public static string Tag(Mood m)
    {
        return m.ToString().ToUpperInvariant();
    }

    public static float TimerMultiplier(Mood m)
    {
        switch (m)
        {
            case Mood.Patient: return 1.25f;
            case Mood.Impatient: return 0.7f;
        }
        return 1f;
    }

    public static float PenaltyMultiplier(Mood m)
    {
        switch (m)
        {
            case Mood.Patient: return 0.85f;
            case Mood.Impatient: return 1.1f;
            case Mood.Paranoid: return 1.2f;
            case Mood.Bored: return 0.9f;
        }
        return 1f;
    }

    public static float CounterFactor(Mood m)
    {
        switch (m)
        {
            case Mood.Patient: return 0.7f;
            case Mood.Paranoid: return 1.7f;
            case Mood.Bored: return 0.4f;
        }
        return 1f;
    }

    public static string Describe(Mood m)
    {
        switch (m)
        {
            case Mood.Patient: return "patient - longer timer, gentler penalties";
            case Mood.Impatient: return "impatient - shorter timer, harsher penalties";
            case Mood.Paranoid: return "paranoid - more cross-examination, harsh penalties";
            case Mood.Bored: return "bored - fewer follow-ups, mild penalties";
        }
        return "";
    }
}

public static class ProofCheck
{
    public static bool HasItem;

    static bool HasProofItem(string tag)
    {
        for (int i = 0; i < Items.Inventory.SlotCount; i++)
            if (InterviewInventoryBridge.Satisfies(InterviewInventoryBridge.Get(i), tag)) return true;
        return false;
    }
    public static bool Forged;

    public static void Prepare(JobData job)
    {
        HasItem = InterviewInventoryBridge.Current.Count > 0 && HasProofItem(RequestRules.ForProof(job).tag);
        Forged = false;
    }
}

public static class FirstImpression
{
    // PLACEHOLDER: fill these from the open world before the interview starts.
    // ArrivedLate: the player took too long to reach the interviewer.
    // CarriedResume: the player still holds the printed resume.
    // WorldEventPoints: suspicion added by things that happened on the way (spilled coffee, etc.).
    public static bool ArrivedLate = false;
    public static bool CarriedResume = true;
    public static int WorldEventPoints = 0;

    public static float Compute(float baseSuspicion, List<string> lines)
    {
        float s = baseSuspicion;
        lines.Add(ArrivedLate ? "Punctuality : LATE (+6)" : "Punctuality : ON TIME");
        if (ArrivedLate) s += 6f;
        lines.Add(CarriedResume ? "Paperwork   : RESUME CARRIED" : "Paperwork   : NO RESUME (+8)");
        if (!CarriedResume) s += 8f;
        if (WorldEventPoints != 0)
        {
            lines.Add("Demeanor    : FLUSTERED (+" + WorldEventPoints + ")");
            s += WorldEventPoints;
        }
        else lines.Add("Demeanor    : CALM");
        int bump = ResumeSession.Embellishment / 10;
        if (bump > 0)
        {
            lines.Add("Transcript  : SUSPICIOUSLY POLISHED (+" + bump + ")");
            s += bump;
        }
        return s;
    }
}

public class InterviewReport
{
    public JobData job;
    public Mood mood;
    public bool forced;
    public float suspicion;
    public float max = 100f;
    public float passFraction = 0.6f;
    public float passScore = 60f;
    public int asked;
    public int correct;
    public int counters;
    public int contradictions;
    public int slow;
    public int fast;
    public int embellishment;
    public int realEducation;
    public int claimedEducation;
    public string proofResult = "";
    public bool proofFailed;
    public bool cognitiveRan;
    public bool cognitiveFailed;
    public bool misconduct;
    public int cognitivePassed;
    public int cognitiveTotal;
    public List<string> notes = new List<string>();
}

public static class Debrief
{
    public static string Short(string s, int n)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("<", "").Replace(">", "").Replace("\n", " ");
        return s.Length <= n ? s : s.Substring(0, n - 2) + "..";
    }

    static string Rate(float score)
    {
        if (score >= 88f) return "S";
        if (score >= 75f) return "A";
        if (score >= 60f) return "B";
        if (score >= 45f) return "C";
        if (score >= 30f) return "D";
        return "F";
    }

    static string Quip(string rating)
    {
        switch (rating)
        {
            case "S": return "The interviewer asks if you want their job.";
            case "A": return "Smooth. Almost too smooth.";
            case "B": return "Solid. Nobody checked the footnotes.";
            case "C": return "You scraped through on vibes alone.";
            case "D": return "A decision made out of pity.";
        }
        return "The interviewer is already telling this story at lunch.";
    }

    static void Row(System.Text.StringBuilder sb, string dimHex, string valHex, string l1, string v1, string l2, string v2)
    {
        sb.Append("<color=").Append(dimHex).Append(">").Append(l1).Append("</color><pos=31%><color=").Append(valHex).Append(">").Append(v1).Append("</color>");
        if (l2 != null)
            sb.Append("<pos=53%><color=").Append(dimHex).Append(">").Append(l2).Append("</color><pos=82%><color=").Append(valHex).Append(">").Append(v2).Append("</color>");
        sb.Append('\n');
    }

    public static string Build(InterviewReport r, out string title, out bool hired)
    {
        float max = r.max > 0f ? r.max : 100f;
        float calm = 1f - Mathf.Clamp01(r.suspicion / max);
        float ratio = r.asked > 0 ? (float)r.correct / r.asked : 0f;
        float score = calm * 55f + ratio * 35f - r.contradictions * 5f - (r.slow + r.fast) * 1.5f;
        float bonus = (r.job != null ? (r.job.strength - 1) * 4f : 0f) + r.embellishment / 5f;
        bool underBar = r.suspicion < max * r.passFraction;
        hired = !r.forced && underBar && score + bonus > r.passScore;
        if (hired) score += bonus;
        else score = Mathf.Min(score, 40f);
        if (r.forced) score = Mathf.Min(score, 20f);
        score = Mathf.Clamp(score, 0f, 100f);
        string rating = Rate(score);

        int strength = r.job != null ? r.job.strength : 1;
        string tier = strength >= 3 ? "SENIOR" : (strength == 2 ? "ASSOCIATE" : "JUNIOR");
        string band = rating == "S" ? "TOP" : (rating == "A" ? "HIGH" : (rating == "B" ? "STANDARD" : (rating == "C" ? "LOW" : "MINIMUM")));

        string reason;
        bool probation = hired && r.suspicion >= max * 0.3f;
        if (r.misconduct) { title = "[ REJECTED ]"; reason = "You were dismissed for abusive language."; }
        else if (r.cognitiveRan && r.cognitiveFailed) { title = "[ REJECTED ]"; reason = "You failed the cognitive test."; }
        else if (r.forced) { title = "[ BUSTED ]"; reason = "They saw straight through you."; }
        else if (!hired)
        {
            title = "[ REJECTED ]";
            reason = r.proofFailed ? "Your paperwork did not check out." : (r.contradictions >= 2 ? "Your story kept changing." : (underBar ? "Too many wrong answers to hire you." : "They were not convinced."));
        }
        else if (probation) { title = "[ HIRED - ON PROBATION ]"; reason = "They hired you. They will be watching."; }
        else if (rating == "S" || rating == "A") { title = "[ HIRED - FAST TRACK ]"; reason = "You talked your way straight to the top."; }
        else { title = "[ HIRED ]"; reason = "You talked your way in."; }

        string tierText = hired ? tier + (probation ? " (PROBATION)" : "") : "NO OFFER";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        string dimHex = hired ? "#12703A" : "#9A2020";   // dark enough to read on the cream result card
        string valHex = hired ? "#073D1B" : "#5E0F0F";
        string rule = "<color=" + dimHex + ">" + new string('-', 44) + "</color>\n";
        sb.Append("<color=").Append(valHex).Append(">").Append(reason).Append("</color>\n");
        sb.Append("<color=").Append(dimHex).Append(">").Append(Quip(rating)).Append("</color>\n");
        sb.Append(rule);
        // the grade itself is shown as a big badge by the UI (InterviewUI.Grade.cs)
        Row(sb, dimHex, valHex, "TIER", hired ? tier : "NONE", "SALARY BAND", hired ? band : "NONE");
        Row(sb, dimHex, valHex, "MOOD", InterviewerPersonality.Tag(r.mood), "ANSWERS", r.correct + "/" + r.asked);
        Row(sb, dimHex, valHex, "FOLLOW-UPS", r.counters.ToString(), "CONTRADICTIONS", r.contradictions.ToString());
        Row(sb, dimHex, valHex, "STALLED/RUSHED", r.slow + "/" + r.fast, "SUSPICION", Mathf.RoundToInt(r.suspicion).ToString("000") + "/" + Mathf.RoundToInt(max));
        Row(sb, dimHex, valHex, "PROOF", string.IsNullOrEmpty(r.proofResult) ? "NOT CHECKED" : Short(r.proofResult, 40), null, null);
        if (r.cognitiveRan) Row(sb, dimHex, valHex, "COGNITIVE", r.cognitivePassed + "/" + r.cognitiveTotal + " PASSED", null, null);
        if (r.embellishment > 0) Row(sb, dimHex, valHex, "EDUCATION", "claimed " + r.claimedEducation + "% (real " + r.realEducation + "%)", null, null);
        sb.Append(rule);

        InterviewResult.Passed = hired;
        InterviewResult.FinalSuspicion = r.suspicion;
        InterviewResult.Asked = r.asked;
        InterviewResult.Correct = r.correct;
        InterviewResult.JobId = r.job != null ? r.job.id : "";
        InterviewResult.Outcome = title;
        InterviewResult.Rating = rating;
        InterviewResult.Score = score;
        InterviewResult.Tier = tierText;
        InterviewResult.MoodName = InterviewerPersonality.Tag(r.mood);
        InterviewResult.Counters = r.counters;
        InterviewResult.Contradictions = r.contradictions;
        InterviewResult.ProofResult = r.proofResult;
        return sb.ToString();
    }
}