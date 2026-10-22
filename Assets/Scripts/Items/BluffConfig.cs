namespace Items
{
    /// <summary>Stage 8f tunables + odds. Mood enum is reused from the interview room:
    /// Patient = friendly (+10), Bored = tired/distracted (+8), Impatient = irritated (-12), Paranoid = stern (-18).</summary>
    public static class BluffConfig
    {
        public static int Base = 35, Fit = 10, FreelanceResume = 5, Min = 5, Max = 85;
        public static int Gullibility = 0, Attentiveness = 0;   // persona values, -20..20 (set from the interviewer persona)
        public static bool ShowHint = false;                    // show % only when the persona trait is known
        public static bool ResumeListsFreelance = false;

        public static int MoodMod(Mood m)
        {
            switch (m)
            {
                case Mood.Patient: return 10;
                case Mood.Bored: return 8;
                case Mood.Impatient: return -12;
                case Mood.Paranoid: return -18;
                default: return 0;
            }
        }

        public static bool IsFit(ItemInstance i) { return i != null && (i.def.id == 11 || i.def.id == 14 || i.def.id == 15); }

        public static string Line(ItemInstance i)
        {
            if (i == null) return "";
            if (i.def.id == 15) return "That's a client of mine from freelance.";
            if (i.def.id == 11 || i.def.id == 14) return "That's my former employer's card.";
            switch (i.nameState)
            {
                case NameState.Blank: return "I just haven't filled the name in yet.";
                case NameState.Scribbled: return "I corrected the name myself.";
                case NameState.Illegible: return "It got crumpled in my bag.";
            }
            return "That's a client / colleague I worked with.";
        }

        public static int Chance(ItemInstance i, Mood m)
        {
            int c = Base + MoodMod(m) + Gullibility - Attentiveness + (IsFit(i) ? Fit : 0) + (ResumeListsFreelance ? FreelanceResume : 0);
            return c < Min ? Min : (c > Max ? Max : c);
        }
    }
}
