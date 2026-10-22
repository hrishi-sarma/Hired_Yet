using System;
using UnityEngine;
namespace FPAnimation
{
    public class FPFeedBirdsAction : FPAction
    {
        readonly Vector3 target;
        Action release;
        bool fired;

        public FPFeedBirdsAction(Vector3 target, Action release)
        {
            this.target = target; this.release = release; Duration = 2.6f;
        }

        void Fire()
        {
            if (fired) return;
            fired = true;
            if (release != null) release();
            release = null;
        }

        public override bool ShouldInterrupt(in FPActionCtx x)
        {
            var st = x.v.state;
            return st == FPState.Swimming || st == FPState.LedgeGrab || st == FPState.Sliding || st == FPState.Jumping || x.v.sprinting;
        }

        public override void End(bool interrupted, in FPActionCtx x) { Fire(); }

        public override void Evaluate(in FPActionCtx x, float n, ref FPPose p, out float dip)
        {
            float t = n * Duration;
            Vector3 to = target - x.camBase; to.y = 0f;
            Vector3 dir = to.sqrMagnitude > 1e-4f ? to.normalized : x.frame * Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);

            float reach = Sm(0f, 0.4f, t), lift = Sm(0.4f, 0.95f, t), thrw = Sm(0.95f, 1.22f, t), back = Sm(1.22f, 1.55f, t);
            float flick = Sm(1.6f, 1.8f, t) * (1f - Sm(1.8f, 2.05f, t)), out_ = Sm(2.05f, 2.5f, t);

            Vector3 pocket = x.origin + x.frame * new Vector3(0.24f, -0.34f, 0.14f);
            Vector3 wind = x.origin + dir * 0.18f + side * 0.2f + Vector3.up * -0.06f;
            Vector3 release0 = x.origin + dir * 0.62f + side * 0.04f + Vector3.up * -0.18f;
            Vector3 follow = x.origin + dir * 0.72f - side * 0.06f + Vector3.up * -0.02f;
            Vector3 rest = x.origin + x.frame * new Vector3(0.2f, -0.3f, 0.22f);

            Vector3 tgt = Vector3.Lerp(pocket, wind, lift);
            tgt = Vector3.Lerp(tgt, release0, thrw);
            tgt = Vector3.Lerp(tgt, follow, back * (1f - flick));
            tgt += (dir * 0.08f + Vector3.up * 0.04f) * flick;
            tgt = Vector3.Lerp(tgt, rest, out_);
            if (t >= 1.15f) Fire();

            float grab = Sm(0.25f, 0.45f, t) * (1f - Sm(1.12f, 1.22f, t));
            float curl = Mathf.Lerp(0.15f, 0.95f, grab) * (1f - out_ * 0.5f);
            if (t > 1.6f && t < 2.05f) curl = Mathf.Lerp(0.8f, 0.1f, flick);
            float env = Sm(0f, 0.3f, t) * (1f - Sm(2.2f, 2.6f, t));

            dip = -0.05f * env;
            p.torsoLean = 7f * thrw * env - 3f * lift * (1f - thrw) * env;
            CamPitch = Mathf.Clamp(Mathf.Atan2(x.camBase.y - 0.05f - target.y, Mathf.Max(0.5f, to.magnitude)) * Mathf.Rad2Deg - x.rawPitch, 0f, 22f) * env * 0.5f;

            p.R = new FPArm
            {
                flex = Mathf.Lerp(30f, 70f, lift), abd = 14f, elbow = Mathf.Lerp(80f, 40f, thrw), curl = curl,
                pron = Mathf.Lerp(0.3f, 0.9f, thrw), ikW = 0.9f * env, ikTarget = x.ToChest(tgt)
            };
            FPArm l = p.L;
            l.flex = Mathf.Lerp(l.flex, 28f, env); l.elbow = Mathf.Lerp(l.elbow, 70f, env); l.abd = Mathf.Lerp(l.abd, 16f, env);
            p.L = l;
        }
    }
}
