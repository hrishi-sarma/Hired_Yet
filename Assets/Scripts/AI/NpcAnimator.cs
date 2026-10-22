using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Drives idle / walk / confrontation clips on a pooled NPC through the Playables API.
/// Each NPC receives the randomly resolved clips from its NpcAnimationSet.
/// </summary>
public class NpcAnimator : MonoBehaviour
{
    const int Idle = 0;
    const int Walk = 1;
    const int Confront = 2;
    const float StartWalkSpeed = 0.2f;
    const float StopWalkSpeed = 0.08f;

    readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[3];
    readonly AnimationClip[] clips = new AnimationClip[3];
    readonly float[] weights = new float[3];

    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    ResolvedAnimations cfg;
    Animator animator;
    int target = Walk;
    float scale = 1f;

    public bool Ready { get { return graph.IsValid(); } }

    public float ConfrontationLength
    {
        get { return clips[Confront] != null ? clips[Confront].length : 0f; }
    }

    public void Configure(Animator targetAnimator, ResolvedAnimations resolved)
    {
        Teardown();
        animator = targetAnimator;
        cfg = resolved;
        if (animator == null || !resolved.HasAny) return;

        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        clips[Idle] = resolved.idle;
        clips[Walk] = resolved.walk;
        clips[Confront] = resolved.confrontation;

        graph = PlayableGraph.Create("NpcAnimator");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mixer = AnimationMixerPlayable.Create(graph, 3);

        for (int i = 0; i < 3; i++)
        {
            if (clips[i] == null) continue;
            playables[i] = AnimationClipPlayable.Create(graph, clips[i]);
            playables[i].SetApplyFootIK(true);
            graph.Connect(playables[i], 0, mixer, i);
        }

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "NpcAnimation", animator);
        output.SetSourcePlayable(mixer);
        ApplyWeights();
        graph.Play();
    }

    public void ResetState(float bodyScale, System.Random rng)
    {
        scale = Mathf.Max(0.1f, bodyScale);
        if (!Ready) return;

        target = Walk;
        for (int i = 0; i < 3; i++)
        {
            weights[i] = i == target ? 1f : 0f;
            if (!playables[i].IsValid() || clips[i] == null) continue;

            float len = Mathf.Max(0.01f, clips[i].length);
            playables[i].SetTime(i == Confront ? 0.0 : rng.NextDouble() * len);
        }

        ApplyWeights();
    }

    public float AlertSeconds(float baseSeconds)
    {
        if (!Ready || !cfg.extendAlertToClip || clips[Confront] == null) return baseSeconds;
        return Mathf.Max(baseSeconds, Mathf.Min(clips[Confront].length, cfg.maxAlertSeconds));
    }

    public void Tick(bool alert, float speed, float dt)
    {
        if (!Ready) return;

        int want;
        if (alert) want = Confront;
        else want = speed > (target == Walk ? StopWalkSpeed : StartWalkSpeed) ? Walk : Idle;

        if (want != target)
        {
            if (want == Confront && playables[Confront].IsValid())
                playables[Confront].SetTime(0.0);
            target = want;
        }

        float step = dt / Mathf.Max(0.01f, cfg.crossfade);
        for (int i = 0; i < 3; i++)
            weights[i] = Mathf.MoveTowards(weights[i], i == target ? 1f : 0f, step);
        ApplyWeights();

        if (playables[Idle].IsValid())
            playables[Idle].SetSpeed(cfg.idlePlaybackSpeed);

        if (playables[Walk].IsValid())
        {
            float rate = speed / (Mathf.Max(0.1f, cfg.walkReferenceSpeed) * scale);
            rate = Mathf.Clamp(rate, cfg.walkPlaybackRange.x, cfg.walkPlaybackRange.y);
            rate *= cfg.walkPlaybackMultiplier;
            playables[Walk].SetSpeed(rate);
        }

        if (playables[Confront].IsValid())
            playables[Confront].SetSpeed(cfg.confrontationPlaybackSpeed);

        Wrap(Idle, true);
        Wrap(Walk, true);
        Wrap(Confront, cfg.loopConfrontation);
    }

    void Wrap(int slot, bool loop)
    {
        if (!loop || clips[slot] == null || !playables[slot].IsValid()) return;
        float len = clips[slot].length;
        if (len <= 0.01f) return;
        double t = playables[slot].GetTime();
        if (t >= len) playables[slot].SetTime(t % len);
    }

    void ApplyWeights()
    {
        float sum = weights[0] + weights[1] + weights[2];
        if (sum < 0.0001f) sum = 1f;

        for (int i = 0; i < 3; i++)
            if (playables[i].IsValid())
                mixer.SetInputWeight(i, weights[i] / sum);
    }

    void OnEnable()
    {
        if (graph.IsValid()) graph.Play();
    }

    void OnDisable()
    {
        if (graph.IsValid()) graph.Stop();
    }

    void OnDestroy()
    {
        Teardown();
    }

    void Teardown()
    {
        if (graph.IsValid()) graph.Destroy();

        for (int i = 0; i < 3; i++)
        {
            playables[i] = default(AnimationClipPlayable);
            clips[i] = null;
            weights[i] = 0f;
        }
    }
}
