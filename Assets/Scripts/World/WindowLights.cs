using UnityEngine;

[AddComponentMenu("Procedural World/Stages/Window Lights")]
public class WindowLightStage : WorldStage
{
    public override int Order { get { return StageOrder.Props + 35; } }
    public override string StageName { get { return "Window Lights"; } }

    public override void Run(WorldContext c)
    {
        WindowLightDriver[] old = FindObjectsByType<WindowLightDriver>(FindObjectsSortMode.None);
        for (int i = 0; i < old.Length; i++)
        {
            if (Application.isPlaying) WorldUtil.Kill(old[i].gameObject); else DestroyImmediate(old[i].gameObject);
        }
    }
}

[AddComponentMenu("Procedural World/Window Lights")]
public class WindowLightDriver : MonoBehaviour
{
    void Awake()
    {
        WorldUtil.Kill(gameObject);
    }
}
