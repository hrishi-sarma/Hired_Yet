using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class VoiceIntent
{
    static readonly string[] Yes = { "yes", "yeah", "yep", "yup", "sure", "correct", "right", "absolutely", "definitely", "of course", "affirmative", "indeed", "i do", "i did", "i am", "true", "that is right", "thats right" };
    static readonly string[] No = { "no", "nope", "nah", "never", "negative", "wrong", "false", "not really", "i do not", "i dont", "i did not", "i didnt", "i am not", "im not", "that is wrong", "thats wrong" };
    static readonly string[] Maybe = { "maybe", "perhaps", "possibly", "not sure", "unsure", "i guess", "i think so", "kind of", "sort of", "it depends", "i dont know", "no idea" };
    static readonly string[] One = { "one", "1", "first", "option one", "number one" };
    static readonly string[] Two = { "two", "2", "second", "option two", "number two" };
    static readonly string[] Three = { "three", "3", "third", "option three", "number three" };

    static string Norm(string s)
    {
        StringBuilder sb = new StringBuilder(" ");
        foreach (char ch in (s ?? "").ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) ? ch : (ch == '\'' ? '\0' : ' '));
        return sb.ToString().Replace("\0", "").Replace("  ", " ") + " ";
    }

    static int Last(string said, string[] phrases)
    {
        int best = -1;
        foreach (string p in phrases)
        {
            int i = said.LastIndexOf(" " + p + " ", System.StringComparison.Ordinal);
            if (i > best) best = i;
        }
        return best;
    }

    public static string ToYesNo(string spoken, bool numbered)
    {
        string s = Norm(spoken);
        int y, n, m;
        if (numbered) { y = Last(s, One); n = Last(s, Two); m = Last(s, Three); }
        else { y = Last(s, Yes); n = Last(s, No); m = Last(s, Maybe); }
        int best = Mathf.Max(y, Mathf.Max(n, m));
        if (best < 0) return null;
        if (m == best) return "maybe";
        if (n == best) return "no";
        return "yes";
    }

    static readonly string[] NoneWords = { "no", "nope", "nah", "nothing", "none", "at home", "i dont have", "i do not have", "dont have", "do not have", "i didnt bring", "i did not bring", "didnt bring", "forgot", "left it", "i left", "not with me", "never mind" };
    static readonly string[] OneWords = { "one", "1", "first", "option one", "number one", "resume", "cv", "yes", "yeah", "yep", "sure", "okay", "here you go" };
    static readonly string[] TwoWords = { "two", "2", "second", "option two", "number two" };
    static readonly string[] ThreeWords = { "three", "3", "third", "option three", "number three" };

    static string[] WithName(string[] basis, string name)
    {
        List<string> l = new List<string>(basis);
        if (!string.IsNullOrEmpty(name))
            foreach (string w in Norm(name).Split(' '))
                if (w.Length >= 4) l.Add(w);
        return l.ToArray();
    }

    public static string ToRequest(string spoken, string name2, string name3)
    {
        string s = Norm(spoken);
        int none = Last(s, NoneWords);
        int one = Last(s, OneWords);
        int two = Last(s, WithName(TwoWords, name2));
        int three = Last(s, WithName(ThreeWords, name3));
        int best = Mathf.Max(Mathf.Max(none, one), Mathf.Max(two, three));
        if (best < 0) return null;
        if (none == best) return "none";
        if (three == best) return "3";
        if (two == best) return "2";
        return "1";
    }
}
