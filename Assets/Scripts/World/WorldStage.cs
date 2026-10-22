using UnityEngine;

public static class StageOrder
{
    public const int FoundationTest = 510;
    public const int HeightBase = 100;
    public const int Zones = 200;
    public const int RoadPlan = 300;
    public const int Flatten = 400;
    public const int TerrainApply = 500;
    public const int Roads = 600;
    public const int Sidewalks = 700;
    public const int Lots = 800;
    public const int Buildings = 900;
    public const int Props = 1000;
    public const int Vegetation = 1100;
    public const int Boundary = 1200;
    public const int Validate = 1300;
}

public abstract class WorldStage : MonoBehaviour
{
    public bool stageEnabled = true;

    public abstract int Order { get; }

    public abstract string StageName { get; }

    public abstract void Run(WorldContext context);
}
