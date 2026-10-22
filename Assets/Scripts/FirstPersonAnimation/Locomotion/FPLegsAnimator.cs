using System;
using UnityEngine;
namespace FPAnimation
{
    /// <summary>Foot targets in body space (metres at 1.75 eye height, ground = y 0, ankle height 0.07).</summary>
    public class FPLegsAnimator
    {
        /// <summary>(isLeftFoot, intensity 0..1). Hook for footstep sounds, dust, splashes.</summary>
        public static event Action<bool, float> FootDown;

        public Vector3 FootL, FootR;
        Vector3 vL, vR;
        int lastStep = int.MinValue;
        bool init;

        static readonly Vector3 restL = new Vector3(-0.10f, 0.07f, 0.04f), restR = new Vector3(0.10f, 0.07f, 0.04f);
        static readonly Vector3 slideL = new Vector3(-0.10f, 0.14f, 0.45f), slideR = new Vector3(0.10f, 0.10f, 0.40f);
        static readonly Vector3 jumpL = new Vector3(-0.10f, 0.14f, 0.10f), jumpR = new Vector3(0.10f, 0.20f, -0.14f);
        static readonly Vector3 fallL = new Vector3(-0.14f, 0.05f, 0f), fallR = new Vector3(0.14f, 0.05f, 0f);

        public void Evaluate(in FPLoco v, FPLocomotionLayer lo, FPAirLayer air, float dt, FPAnimSettings s)
        {
            float a = lo.Phase * Mathf.PI * 2f, w = lo.MoveW;
            float amp = Mathf.Lerp(Mathf.Lerp(s.legAmpWalk, s.legAmpSprint, lo.SprintW), s.legAmpWalk * 0.5f, lo.CrouchW);
            float lift = Mathf.Lerp(0.08f, 0.16f, lo.SprintW);
            float sn = Mathf.Sin(a), cs = Mathf.Cos(a);

            // left foot shares the right arm's phase, so arms and legs stay opposite
            Vector3 L = restL + new Vector3(0f, Mathf.Max(0f, cs) * lift * w, sn * amp * w);
            Vector3 R = restR + new Vector3(0f, Mathf.Max(0f, -cs) * lift * w, -sn * amp * w);

            L = Vector3.Lerp(L, slideL, lo.SlideW); R = Vector3.Lerp(R, slideR, lo.SlideW);
            Vector3 aL = Vector3.Lerp(jumpL, fallL, air.FallN), aR = Vector3.Lerp(jumpR, fallR, air.FallN);
            L = Vector3.Lerp(L, aL, air.AirW); R = Vector3.Lerp(R, aR, air.AirW);

            if (!init) { FootL = L; FootR = R; init = true; }
            FootL = FPSpring.Step(FootL, ref vL, L, 0.035f, dt);
            FootR = FPSpring.Step(FootR, ref vR, R, 0.035f, dt);

            // foot-down events at phase 0.25 (left) and 0.75 (right)
            int n = Mathf.FloorToInt(lo.Phase * 2f + 0.5f);
            if (n != lastStep)
            {
                if (lastStep != int.MinValue && w > 0.5f) FootDown?.Invoke((n & 1) == 1, lo.SpeedNorm);
                lastStep = n;
            }
        }
    }
}
