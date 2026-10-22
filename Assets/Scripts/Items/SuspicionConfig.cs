namespace Items
{
    /// <summary>Single source of truth for suspicion values (Stage 8b table). Tooltip and interviewer both read this.</summary>
    public static class SuspicionConfig
    {
        public static int Other = 30, Scribbled = 18, Blank = 10, Illegible = 4, CrossCheckMismatch = 10, BluffFailExtra = 10;
        public static int RoundCap = 50;   // max suspicion that presented documents can add in one interview round

        /// <summary>Worst-applicable-state suspicion for one item (not stacked).</summary>
        public static int For(ItemInstance i)
        {
            if (i == null) return 0;

            // The authentic resume issued to the player at the start of the run is
            // legitimate by definition. It must never incur name-state suspicion.
            if (i.isPlayerStarter) return 0;

            switch (i.nameState)
            {
                case NameState.Other: return Other;
                case NameState.Scribbled: return Scribbled;
                case NameState.Blank: return Blank;
                case NameState.Illegible: return Illegible;
                default: return 0;
            }
        }
        public static string Level(int points)
        {
            return points >= Other ? "HIGH" : points >= Scribbled ? "MEDIUM" : points >= Blank ? "LOW" : points > 0 ? "LOWEST" : "NONE";
        }
        public static string Level(ItemInstance i) { return Level(For(i)); }
    }
}
