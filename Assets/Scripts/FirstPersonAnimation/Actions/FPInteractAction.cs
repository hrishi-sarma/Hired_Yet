using UnityEngine;
namespace FPAnimation
{
    public class FPInteractAction : FPAction
    {
        readonly Vector3 pos;

        public FPInteractAction(Vector3 pos) { this.pos = pos; Duration = 0.55f; }

        public override bool Weak { get { return true; } }

        public override bool ShouldInterrupt(in FPActionCtx x) { return Disturbed(x); }

        public override void Evaluate(in FPActionCtx x, float n, ref FPPose p, out float dip)
        {
            dip = 0f;
            bool left = x.ToChest(pos).x < -0.05f;
            float up = Sm(0f, 0.3f, n) * (1f - Sm(0.55f, 1f, n));
            float press = Sm(0.3f, 0.45f, n) * (1f - Sm(0.45f, 0.6f, n));
            var a = new FPArm
            {
                flex = Mathf.Lerp(p.R.flex, 80f + 10f * press, up),
                abd = Mathf.Lerp(p.R.abd, 6f, up),
                elbow = Mathf.Lerp(p.R.elbow, 30f - 10f * press, up),
                curl = Mathf.Lerp(p.R.curl, 0.6f, up),
                pron = 0.7f
            };
            if (left) p.L = a; else p.R = a;
        }
    }
}
