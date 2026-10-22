using UnityEngine;

namespace Items
{
    // One concrete item in the world or inventory. Fully described by
    // (defId, seed, nameState, condition, printedName), so it can be saved,
    // dropped and picked up without any loss.
    [System.Serializable]
    public class ItemInstance
    {
        public ItemDef def;
        public int seed;
        public NameState nameState;
        public Condition condition;
        public string printedName = "";   // name shown on the item ("" if blank/illegible)
        public string originalName = "";  // name underneath a scribble-out
        public bool isPlayerStarter;
        public ItemStats baseStats;       // rolled once at creation

        // Final stats = base + modifiers from current name state / condition.
        public ItemStats Stats
        {
            get
            {
                var s = baseStats;
                if (condition == Condition.Crumpled) s.presentation -= 20;
                switch (nameState)
                {
                    case NameState.Other:     s.credibility -= 30; break;
                    case NameState.Illegible: s.credibility -= 15; break;
                    case NameState.Scribbled: s.credibility -= 12; break;
                    case NameState.Blank:     s.credibility -= 5;  break;
                }
                return s.Clamped();
            }
        }

        public bool CanChangeName { get { return def.IsDocument && nameState == NameState.Other; } }
        public bool CanAddName    { get { return nameState == NameState.Blank; } }
        public bool IsNameMismatch { get { return nameState == NameState.Other; } }  // candidate for a bluff (Stage 8)

        // Short description used by tooltips (Stage 7).
        public string NameLabel
        {
            get
            {
                switch (nameState)
                {
                    case NameState.Own:       return "Name: " + printedName;
                    case NameState.Other:     return "Name: " + printedName + " (not yours)";
                    case NameState.Illegible: return "Name: unreadable";
                    case NameState.Scribbled: return "Name: scribbled out and replaced";
                    case NameState.Blank:     return "Name: blank";
                    default:                  return "";
                }
            }
        }
    }
}
