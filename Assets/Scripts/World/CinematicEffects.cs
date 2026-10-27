using UnityEngine;

public class CinematicEffects : MonoBehaviour
{
    void Awake() { Destroy(gameObject); }
}

public class CinematicOutlineStage : WorldStage
{
    public override int Order { get { return StageOrder.Props + 30; } }
    public override string StageName { get { return "Outlines"; } }
    public override void Run(WorldContext c) { }
}
