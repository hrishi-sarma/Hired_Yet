using System;
using UnityEngine;
namespace FPAnimation
{
    public class FPPickpocketAction : FPAction
    {
        readonly Transform target;
        readonly bool success;
        Action apply;
        bool applied, gotSide;
        float side;
        Vector3 lastPocket;

        public FPPickpocketAction(Transform target, bool success, Action apply)
        {
            this.target = target; this.success = success; this.apply = apply; Duration = 1.2f;
        }

        void Fire() { if (applied) return; applied = true; if (apply != null) apply(); apply = null; }

        public override bool ShouldInterrupt(in FPActionCtx x)
        {
            var st = x.v.state;
            return st == FPState.Swimming || st == FPState.LedgeGrab || st == FPState.Sliding;
        }

        public override void End(bool interrupted, in FPActionCtx x) { Fire(); if (x.item != null) x.item.Show(false); }

        public override void Evaluate(in FPActionCtx x, float n, ref FPPose p, out float dip)
        {
            Vector3 player = x.c.transform.position;
            Vector3 toP = Vector3.zero;
            if (target != null)
            {
                toP = player - target.position; toP.y = 0f;
                toP = toP.sqrMagnitude > 1e-4f ? toP.normalized : -target.forward;
                if (!gotSide) { side = Mathf.Sign(Vector3.Dot(target.right, toP) + 1e-4f); gotSide = true; }
                lastPocket = target.position + Vector3.up * 0.95f + target.right * (0.22f * side) + toP * 0.05f;
            }

            float reach = Sm(0f, 0.25f, n);
            float pinch = Sm(0.25f, 0.45f, n);
            Vector3 pocket = lastPocket;
            Vector3 approach = pocket + toP * (0.12f * (1f - pinch)) - toP * (0.04f * pinch);
            float ikW;
            Vector3 tgt = approach;

            if (success)
            {
                float wd = Sm(0.45f, 0.8f, n);
                Vector3 tuck = x.origin + x.frame * new Vector3(0.12f, -0.25f, 0.30f);
                tgt = Vector3.Lerp(approach, tuck, wd);
                ikW = reach * (1f - Sm(0.78f, 0.9f, n));
                if (n >= 0.8f) Fire();
                if (x.item != null) x.item.Show(n > 0.4f && n < 0.82f);
            }
            else
            {
                float snap = Sm(0.45f, 0.55f, n);
                ikW = reach * (1f - snap);
                if (n >= 0.45f) Fire();
            }

            float oops = success ? 0f : Sm(0.5f, 0.6f, n) * (1f - Sm(0.8f, 1f, n));
            dip = -0.12f * reach * (1f - Sm(0.8f, 1f, n)) - 0.06f * oops;
            p.torsoLean = -6f * oops;
            Vector3 toT = lastPocket - x.camBase; toT.y = 0f;
            float dd = toT.magnitude, hold = reach * (1f - Sm(0.8f, 1f, n));
            CamMove = dd > 0.01f ? toT / dd * (Mathf.Clamp(dd - 0.6f, 0f, 1.2f) * hold) : Vector3.zero;
            float camY = x.camBase.y + dip, hz = Mathf.Max(0.2f, dd - CamMove.magnitude);
            CamPitch = Mathf.Clamp(Mathf.Atan2(camY - lastPocket.y, hz) * Mathf.Rad2Deg - x.rawPitch, 0f, 30f) * hold * 0.6f;
            p.R = new FPArm
            {
                flex = Mathf.Lerp(40f, 75f, oops), abd = Mathf.Lerp(12f, 30f, oops), elbow = Mathf.Lerp(40f, 100f, oops),
                curl = Mathf.Lerp(0.15f, 0.8f, pinch * (success ? 1f : 1f - snapOops(n))),
                pron = 0.5f, ikW = ikW, ikTarget = x.ToChest(tgt)
            };
            p.L = new FPArm
            {
                flex = Mathf.Lerp(20f, 75f, oops), abd = Mathf.Lerp(14f, 30f, oops), elbow = Mathf.Lerp(30f, 100f, oops),
                curl = 0.3f, pron = 0.4f
            };
        }

        static float snapOops(float n) { return Sm(0.5f, 0.6f, n); }
    }
}
