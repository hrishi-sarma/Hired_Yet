using UnityEngine;
using Random = System.Random;

namespace Items
{
    public static class ItemFactory
    {
        static readonly string[] fakeNames = PopCultureNames.Full;

        static int Range(Random r, int lo, int hi) { return r.Next(lo, hi + 1); }

        static void RarityRange(Rarity r, out int lo, out int hi)
        {
            switch (r)
            {
                case Rarity.Common:    lo = 20; hi = 45; break;
                case Rarity.Uncommon:  lo = 40; hi = 65; break;
                case Rarity.Rare:      lo = 60; hi = 85; break;
                case Rarity.Epic:      lo = 80; hi = 95; break;
                default:               lo = 90; hi = 100; break;
            }
        }

        public static string RandomOtherName(int seed, string avoid)
        {
            var r = new Random(seed ^ 0x5F3759);
            for (int i = 0; i < 8; i++)
            {
                string n = fakeNames[r.Next(fakeNames.Length)];
                if (n != avoid) return n;
            }
            return fakeNames[0];
        }

        // Player name as entered in the resume form (falls back so tests work without a session).
        public static string PlayerName
        {
            get { return string.IsNullOrEmpty(ResumeSession.CandidateName) ? "You" : ResumeSession.CandidateName; }
        }

        // seed < 0 => random seed. nameOverride: optionally force a variant (e.g. Blank vs Other for cards).
        public static ItemInstance Create(ItemDef def, int seed = -1, NameState? nameOverride = null)
        {
            if (def == null) return null;
            if (seed < 0) seed = UnityEngine.Random.Range(1, int.MaxValue);
            var r = new Random(seed);

            var inst = new ItemInstance { def = def, seed = seed, condition = def.defaultCondition };

            // Name state: forced > random from the def's allowed variants.
            NameState ns = nameOverride ?? def.nameVariants[r.Next(def.nameVariants.Length)];
            inst.nameState = ns;
            switch (ns)
            {
                case NameState.Own:
                    inst.printedName = PlayerName; break;
                case NameState.Other:
                    inst.printedName = RandomOtherName(seed, PlayerName); break;
                case NameState.Illegible:
                    inst.originalName = RandomOtherName(seed, PlayerName);   // exists, but never shown readable
                    inst.printedName = ""; break;
                default:
                    inst.printedName = ""; break;
            }

            // Base stats (only the ones the item uses; others stay 0).
            int lo, hi; RarityRange(def.rarity, out lo, out hi);
            var s = new ItemStats();
            if ((def.usedStats & StatFlags.Quality) != 0)      s.quality = Range(r, lo, hi);
            if ((def.usedStats & StatFlags.Relevance) != 0)    s.relevance = Range(r, lo, hi);
            if ((def.usedStats & StatFlags.Credibility) != 0)  s.credibility = Range(r, lo, hi);
            if ((def.usedStats & StatFlags.Presentation) != 0) s.presentation = Range(r, lo, hi);
            inst.baseStats = s;
            return inst;
        }

        public static ItemInstance Create(int defId, int seed = -1, NameState? nameOverride = null)
        {
            return Create(ItemDatabase.Get(defId), seed, nameOverride);
        }

        // The resume the player spawns with: own name, clean, fixed average stats.
        public static ItemInstance CreatePlayerResume()
        {
            var def = ItemDatabase.PlayerResumeDef;
            var inst = new ItemInstance
            {
                def = def, seed = 1, condition = Condition.Clean, nameState = NameState.Own,
                printedName = PlayerName, isPlayerStarter = true,
                // Stats can later be derived from ResumeSession (education, experience).
                baseStats = new ItemStats(40, 40, 55, 60)
            };
            return inst;
        }
    }
}
