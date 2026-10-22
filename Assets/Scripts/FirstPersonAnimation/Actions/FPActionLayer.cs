using System;
using UnityEngine;
namespace FPAnimation
{
    public class FPActionLayer
    {
        public float W, Dip, Pitch;
        public Vector3 Move;
        public float BodyBack;
        public string Name { get { return cur != null ? cur.GetType().Name + " " + cur.N.ToString("F2") : "-"; } }
        public bool Busy { get { return cur != null; } }
        public FPItemInHand Item;

        FPAction cur;
        FPPose last;
        float lastDip, lastPitch, lastBack, wV;
        Vector3 lastMove;
        FPActionCtx lastCtx;

        public void Subscribe()
        {
            FPEvents.PatStarted += OnPat;
            FPEvents.PickpocketAttempt += OnPick;
            FPEvents.Interact += OnInteract;
            FPEvents.FeedStarted += OnFeed;
            FPEvents.EventStageChanged += OnEventStage;
        }

        public void Unsubscribe()
        {
            FPEvents.PatStarted -= OnPat;
            FPEvents.PickpocketAttempt -= OnPick;
            FPEvents.Interact -= OnInteract;
            FPEvents.FeedStarted -= OnFeed;
            FPEvents.EventStageChanged -= OnEventStage;
            if (cur != null) { cur.End(true, lastCtx); cur = null; }
        }

        void OnPat(Transform a, bool dog, float secs) { Play(dog ? (FPAction)new FPPatDogAction(a, secs) : new FPPatCatAction(a, secs)); }
        void OnPick(Transform t, bool success, Action apply) { Play(new FPPickpocketAction(t, success, apply)); }
        void OnFeed(Vector3 target, Action release) { Play(new FPFeedBirdsAction(target, release)); }
        void OnInteract(Vector3 pos) { Play(new FPInteractAction(pos)); }

        void OnEventStage(EventStage stage, PlayerEventAnim anim, Transform target, float secs)
        {
            if (anim == PlayerEventAnim.None)
            {
                // stop an open-ended event action; it ends on the next Apply and blends out
                if (stage == EventStage.During && (cur is FPEventAction || cur is FPPatAction)) cur.Duration = cur.T;
                return;
            }
            if (anim == PlayerEventAnim.PatDog && target != null) { Play(new FPPatDogAction(target, secs > 0f ? secs : 5f)); return; }
            Play(new FPEventAction(stage, anim == PlayerEventAnim.PatDog ? PlayerEventAnim.Calm : anim, target, secs));
        }

        void Play(FPAction a)
        {
            if (cur != null)
            {
                if (a.Weak) return;
                cur.End(true, lastCtx);
            }
            cur = a;
            last = FPPose.Rest(lastCtx.s != null ? lastCtx.s : new FPAnimSettings());
        }

        public void Apply(ref FPActionCtx x, ref FPPose p)
        {
            x.item = Item;
            lastCtx = x;
            if (cur != null)
            {
                cur.T += x.dt;
                bool stop = cur.ShouldInterrupt(x);
                if (stop || cur.T >= cur.Duration)
                {
                    cur.End(stop, x);
                    cur = null;
                }
                else
                {
                    FPPose pose = FPPose.Rest(x.s);
                    float dip;
                    cur.CamMove = Vector3.zero; cur.CamPitch = 0f; cur.BodyBack = 0f;
                    cur.Evaluate(x, cur.N, ref pose, out dip);
                    last = pose; lastDip = dip; lastMove = cur.CamMove; lastPitch = cur.CamPitch; lastBack = cur.BodyBack;
                }
            }

            W = FPSpring.Step(W, ref wV, cur != null ? 1f : 0f, cur != null ? 0.12f : 0.2f, x.dt);
            Dip = lastDip * W; Move = lastMove * W; Pitch = lastPitch * W; BodyBack = lastBack * W;
            if (W < 0.002f) { W = 0f; Dip = 0f; Pitch = 0f; BodyBack = 0f; Move = Vector3.zero; return; }
            FPArmKinematics.Blend(ref p.R, last.R, W);
            FPArmKinematics.Blend(ref p.L, last.L, W);
            p.torsoLean += last.torsoLean * W;
        }
    }
}
