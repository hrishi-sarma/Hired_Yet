using UnityEngine;

public enum Perk { None, SmoothTalker, GoodMemory, LuckyCharm, QuickThinker, PokerFace }

public static class ResumeSession
{
    public static InterviewDatabase Database;
    public static JobData[] OfferedJobs;
    public static string[] OfferedTexts;
    public static JobData SelectedJob;
    public static string SelectedText = "";
    public static string CandidateName = "";
    public static string Hobby = "";
    public static int EducationScore;
    public static int RealEducation;
    public static Perk SelectedPerk = Perk.None;
    public static string Experience = "None";   // set by the resume form (None / 1-2 years / 3-5 years / 5+ years)

    // photo-ID character (index into CharacterLibrary.All, -1 = none chosen). Drives skin / outfit colour of the 3D hands + body.
    public static readonly Color DefaultSkin = new Color(0.93f, 0.74f, 0.60f), DefaultOutfit = new Color(0.20f, 0.35f, 0.55f);
    public static int CharacterIndex = -1;
    public static Color PlayerSkin = DefaultSkin, PlayerOutfit = DefaultOutfit;
    public static Texture2D Photo { get { return CharacterIndex < 0 ? null : CharacterLibrary.Portrait(CharacterIndex); } }

    public static void SetCharacter(int idx)
    {
        CharacterIndex = (idx >= 0 && idx < CharacterLibrary.All.Length) ? idx : -1;
        if (CharacterIndex < 0) { PlayerSkin = DefaultSkin; PlayerOutfit = DefaultOutfit; return; }
        PlayerSkin = CharacterLibrary.All[CharacterIndex].skin; PlayerOutfit = CharacterLibrary.All[CharacterIndex].outfit;
    }

    // photograph of the submitted form (RGBA, bottom-up like Texture2D) -> texture of the player's starter resume
    public static Color32[] ResumePixels; public static int ResumeW, ResumeH;

    public static int Embellishment
    {
        get { return Mathf.Max(0, EducationScore - RealEducation); }
    }

    public static bool Ready
    {
        get { return Database != null && Database.jobs != null && Database.jobs.Length >= 3; }
    }

    public static void EnsureLoaded()
    {
        if (Database != null) return;
        TextAsset asset = Resources.Load<TextAsset>("interview_data");
        if (asset == null) return;
        Database = JsonUtility.FromJson<InterviewDatabase>(asset.text);
    }

    public static void NewSession()
    {
        ClearAnswers();
        OfferedJobs = OptionDrawer.Draw(Database.jobs, 3);
        OfferedTexts = new string[OfferedJobs.Length];
        for (int i = 0; i < OfferedJobs.Length; i++)
        {
            string[] variants = OfferedJobs[i].displayVariants;
            OfferedTexts[i] = variants[Random.Range(0, variants.Length)];
        }
    }

    public static void ClearAnswers()
    {
        SelectedJob = null;
        SelectedText = "";
        CandidateName = "";
        Hobby = "";
        EducationScore = 0;
        RealEducation = 0;
        SelectedPerk = Perk.None;
        Experience = "None";
        SetCharacter(-1);
        ResumePixels = null;
        PlayerInventory.Reset();
    }

    public static void SelectOffer(int index)
    {
        SelectedJob = OfferedJobs[index];
        SelectedText = OfferedTexts[index];
    }
}

public static class OptionDrawer
{
    public static JobData[] Draw(JobData[] pool, int count)
    {
        for (int level = 0; level < 3; level++)
        {
            for (int attempt = 0; attempt < 300; attempt++)
            {
                JobData[] picks = PickDistinct(pool, count);
                if (IsValid(picks, level))
                {
                    Shuffle(picks);
                    return picks;
                }
            }
        }
        JobData[] fallback = PickDistinct(pool, count);
        Shuffle(fallback);
        return fallback;
    }

    static bool IsValid(JobData[] picks, int level)
    {
        bool hasRealistic = false;
        bool hasSatirical = false;
        for (int i = 0; i < picks.Length; i++)
        {
            if (picks[i].tone == "realistic") hasRealistic = true;
            if (picks[i].tone == "satirical") hasSatirical = true;
        }
        if (!hasRealistic || !hasSatirical) return false;
        if (level >= 2) return true;

        for (int i = 0; i < picks.Length; i++)
        {
            for (int j = i + 1; j < picks.Length; j++)
            {
                if (picks[i].category == picks[j].category) return false;
                if (level == 0 && picks[i].strength == picks[j].strength) return false;
            }
        }
        return true;
    }

    static JobData[] PickDistinct(JobData[] pool, int count)
    {
        System.Collections.Generic.List<int> indices = new System.Collections.Generic.List<int>();
        while (indices.Count < count)
        {
            int r = Random.Range(0, pool.Length);
            if (!indices.Contains(r)) indices.Add(r);
        }
        JobData[] result = new JobData[count];
        for (int i = 0; i < count; i++) result[i] = pool[indices[i]];
        return result;
    }

    static void Shuffle(JobData[] items)
    {
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            JobData temp = items[i];
            items[i] = items[j];
            items[j] = temp;
        }
    }
}


public static class PerkInfo
{
    static readonly Perk[] All = { Perk.SmoothTalker, Perk.GoodMemory, Perk.LuckyCharm, Perk.QuickThinker, Perk.PokerFace };

    public static Perk[] Draw(int count)
    {
        Perk[] pool = (Perk[])All.Clone();
        for (int i = pool.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Perk t = pool[i];
            pool[i] = pool[j];
            pool[j] = t;
        }
        Perk[] result = new Perk[Mathf.Min(count, pool.Length)];
        for (int i = 0; i < result.Length; i++) result[i] = pool[i];
        return result;
    }

    public static string Name(Perk p)
    {
        switch (p)
        {
            case Perk.SmoothTalker: return "SMOOTH TALKER";
            case Perk.GoodMemory: return "GOOD MEMORY";
            case Perk.LuckyCharm: return "LUCKY CHARM";
            case Perk.QuickThinker: return "QUICK THINKER";
            case Perk.PokerFace: return "POKER FACE";
        }
        return "NONE";
    }

    public static string Description(Perk p)
    {
        switch (p)
        {
            case Perk.SmoothTalker: return "hedging costs half as much suspicion";
            case Perk.GoodMemory: return "no extra penalty for flubbing your own resume";
            case Perk.LuckyCharm: return "your first wrong answer costs nothing";
            case Perk.QuickThinker: return "+6 seconds on every question";
            case Perk.PokerFace: return "rushing, stalling and flip-flopping cost half";
        }
        return "";
    }
}

public static class RiskInfo
{
    public static string Risk(JobData job)
    {
        if (job == null) return "?";
        return job.strength >= 3 ? "HIGH" : (job.strength == 2 ? "MEDIUM" : "LOW");
    }

    public static string Pay(JobData job)
    {
        if (job == null) return "?";
        return job.strength >= 3 ? "SENIOR" : (job.strength == 2 ? "MID" : "ENTRY");
    }
}
