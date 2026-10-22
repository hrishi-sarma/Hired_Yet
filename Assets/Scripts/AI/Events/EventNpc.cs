using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class EventNpc : MonoBehaviour
{
    const int Idle = 0;
    const int Before = 1;
    const int During = 2;
    const int After = 3;
    const int Walk = 4;
    const int Slots = 5;

    readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[Slots];
    readonly AnimationClip[] clips = new AnimationClip[Slots];
    readonly float[] weights = new float[Slots];

    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    int target;
    float fade = 0.2f;

    public bool Ready { get { return graph.IsValid(); } }

    public void Setup(Animator animator, AnimationClip idle, AnimationClip before, AnimationClip during, AnimationClip after, float crossfade, AnimationClip walk = null)
    {
        if (animator == null) return;
        AnimationClip baseClip = idle != null ? idle : (before != null ? before : (during != null ? during : after));
        if (baseClip == null) return;

        fade = Mathf.Max(0.02f, crossfade);
        clips[Idle] = baseClip;
        clips[Before] = before;
        clips[During] = during;
        clips[After] = after;
        clips[Walk] = walk;

        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        graph = PlayableGraph.Create("EventNpc");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mixer = AnimationMixerPlayable.Create(graph, Slots);
        for (int i = 0; i < Slots; i++)
        {
            if (clips[i] == null) continue;
            playables[i] = AnimationClipPlayable.Create(graph, clips[i]);
            playables[i].SetApplyFootIK(true);
            graph.Connect(playables[i], 0, mixer, i);
        }

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "EventNpcAnimation", animator);
        output.SetSourcePlayable(mixer);
        target = Idle;
        for (int i = 0; i < Slots; i++) weights[i] = i == target ? 1f : 0f;
        ApplyWeights();
        graph.Play();
    }

    public void Play(EventStage stage)
    {
        int slot = stage == EventStage.Before ? Before : (stage == EventStage.During ? During : After);
        SetTarget(clips[slot] != null ? slot : Idle);
    }

    public void PlayWalk()
    {
        SetTarget(clips[Walk] != null ? Walk : Idle);
    }

    public void PlayIdle()
    {
        SetTarget(Idle);
    }

    void SetTarget(int slot)
    {
        if (!Ready) return;
        if (slot != target && playables[slot].IsValid()) playables[slot].SetTime(0.0);
        target = slot;
    }

    void Update()
    {
        if (!Ready) return;
        float step = Time.deltaTime / fade;
        for (int i = 0; i < Slots; i++)
            weights[i] = Mathf.MoveTowards(weights[i], i == target ? 1f : 0f, step);
        ApplyWeights();

        for (int i = 0; i < Slots; i++)
        {
            if (!playables[i].IsValid() || clips[i] == null) continue;
            float len = clips[i].length;
            if (len <= 0.01f) continue;
            double t = playables[i].GetTime();
            if (t < len) continue;
            if (i == After && target == After) SetTarget(Idle);
            else if (i != After) playables[i].SetTime(t % len);
        }
    }

    void ApplyWeights()
    {
        float sum = 0f;
        for (int i = 0; i < Slots; i++) sum += weights[i];
        if (sum < 0.0001f) sum = 1f;
        for (int i = 0; i < Slots; i++)
            if (playables[i].IsValid()) mixer.SetInputWeight(i, weights[i] / sum);
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
        if (graph.IsValid()) graph.Destroy();
    }
}
