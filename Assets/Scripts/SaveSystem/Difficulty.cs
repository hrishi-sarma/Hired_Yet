using UnityEngine;
using Items;

/// <summary>The resume's EXPERIENCE entry is the difficulty setting:
/// None = EASY, 1-2 years = NORMAL, 3-5 years = HARD, 5+ years = EXPERT. Higher difficulty = tougher checks, better loot.</summary>
public static class Difficulty
{
    public static readonly string[] Names = { "EASY", "NORMAL", "HARD", "EXPERT" };

    public static int Level
    {
        get
        {
            switch (ResumeSession.Experience)
            {
                case "1-2 years": return 1;
                case "3-5 years": return 2;
                case "5+ years": return 3;
            }
            return 0;
        }
    }

    //                                           EASY  NORMAL HARD  EXPERT
    static readonly float[] pickpocket = { 1.00f, 0.90f, 0.78f, 0.65f };   // multiplies the pickpocket success chance (0.5 -> 0.33 at EXPERT)
    static readonly float[] eventTime  = { 1.00f, 0.90f, 0.80f, 0.70f };   // time allowed for world timing events
    static readonly float[] pointer    = { 1.00f, 1.10f, 1.20f, 1.35f };   // timing-bar pointer speed
    static readonly float[] zone       = { 1.00f, 0.95f, 0.88f, 0.80f };   // timing-bar green zone width
    static readonly float[] cogTime    = { 1.00f, 0.92f, 0.85f, 0.78f };   // cognitive test time limits
    static readonly float[] rare       = { 1.00f, 1.25f, 1.60f, 2.10f };   // rare item weight
    static readonly float[] epic       = { 1.00f, 1.30f, 1.80f, 2.60f };   // epic item weight
    static readonly float[] legendary  = { 0.00f, 0.005f, 0.015f, 0.03f }; // chance per random roll to hand out a legendary item

    public static float PickpocketMul { get { return pickpocket[Level]; } }
    public static float PointerMul { get { return pointer[Level]; } }
    public static float ZoneMul { get { return zone[Level]; } }
    public static float CognitiveTimeMul { get { return cogTime[Level]; } }
    public static int MazeExtra { get { return Level >= 2 ? 2 : 0; } }     // bigger hedge mazes on HARD / EXPERT
    public static float LegendaryChance { get { return legendary[Level]; } }

    /// <summary>Scales a world-event time limit (0 stays 0 = "no limit").</summary>
    public static float Limit(float seconds) { return seconds > 0f ? seconds * eventTime[Level] : seconds; }

    public static float RarityMul(Rarity r)
    {
        if (r == Rarity.Rare) return rare[Level];
        if (r == Rarity.Epic) return epic[Level];
        return 1f;
    }
}
