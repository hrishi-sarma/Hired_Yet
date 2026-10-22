using System.Collections.Generic;
using UnityEngine;

public enum EventStage { Before, During, After }

public enum EventHandlerKind { Hold, Mash, Sequence, Timing, Choice, Sorting, Object }

public enum PlayerEventAnim
{
    None, Greet, Goodbye, Push, Pull, Lift, Tinker, Stir, Support, Calm,
    Inspect, Point, Dance, Turn, Bang, Pour,
    PatDog, Arrows, Repair, Squeeze, Mend, Pump, Sort, Ponder, Chop, Bandage, LiftCrouch, DragLog, Fidget
}

[System.Serializable]
public class TimingParams
{
    public float pointerSpeed = 1.2f;
    [Range(0.05f, 0.5f)] public float zoneWidth = 0.22f;
    [Min(1)] public int hits = 3;
    [Tooltip("Seconds taken off the clock by a missed press (a miss also loses one hit).")]
    [Min(0f)] public float missPenalty = 2f;
    [Tooltip("Seconds allowed after the first press when the config Limit is 0.")]
    [Min(1f)] public float timeLimit = 18f;
}

[System.Serializable]
public class ChoiceQuestion
{
    [TextArea] public string question;
    public string[] options = new string[3];
    public int correct;
}

[System.Serializable]
public class ChoiceParams
{
    public List<ChoiceQuestion> questions = new List<ChoiceQuestion>();
    [Min(1)] public int asked = 1;
}

[System.Serializable]
public class SortItem
{
    public string name;
    public Color color = Color.white;
    public int category;
}

[System.Serializable]
public class SortingParams
{
    public string[] categories = { "Safe", "Poisonous" };
    public List<SortItem> items = new List<SortItem>();
    [Min(0)] public int allowedMistakes = 1;
}

[System.Serializable]
public class ObjectParams
{
    public float holdSeconds = 2f;
    [Min(1)] public int count = 6;
    public float radius = 5f;
}
