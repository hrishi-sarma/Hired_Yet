using System;

public enum CognitiveGameType
{
    MazeRunner = 0,           // WASD / Arrow keys top-down hedge maze (Image 1)
    FlowLineMatch = 1,        // Mouse drag colored dots without crossing lines (Image 2)
    PatternSequence = 2,      // Casual cartoon pattern progression
    BalanceScales = 3,        // Fun fruit & animal seesaw balance
    OddOneOut = 4,            // Pick the item that doesn't fit
    FruitMath = 5,            // Casual cartoon arithmetic
    MemoryGrid = 6,           // Simon Says glowing chalkboard stars
    CircuitWire = 7,          // Rotate garden water pipes to water plant
    CartoonColorMatch = 8,    // Cheerful balloon color match
    DirectionCompass = 9,     // Cartoon animal facing orientation
    NumberSeries = 10,
    ArrowSpin = 11,
    CopyGrid = 12,
    SpotDifference = 13,
    CountShapes = 14,
    TileSum = 15,
    LightsOut = 16,
    SlidePuzzle = 17,
    StarCatcher = 18,
    PaintMixer = 19,
    ClockReader = 20,
    ShopSum = 21,
    NumberDash = 22,
    WordScramble = 23,
    ArrowRush = 24,
    CardPairs = 25,
    TrailMemory = 26,
    HeightRiddle = 27,
    ColorSudoku = 28,
    TowerStack = 29     // 10-29: expansion pool
}

[Serializable]
public class CognitiveRoundResult
{
    public int totalPuzzlesAttempted;
    public int levelsCleared;
    public int levelsFailed;
    public float netSuspicionDelta;
    public float averageSolveTime;
    public int gamesTotal;            // minigames played (3)
    public int gamesPassed;           // minigames passed (>= levelsToPassGame of its 3 levels cleared)
    public bool[] gamePassed = new bool[3];
}
