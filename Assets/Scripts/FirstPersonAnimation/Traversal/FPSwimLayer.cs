using UnityEngine;
using EasyPeasyFirstPersonController;
namespace FPAnimation
{
    public class FPSwimLayer
    {
        public float W, StrokeW, CrawlW;
        public Vector3 LegL, LegR;
        float wV, sV, cV, phase;

        public void Apply(in FPLoco v, float dt, FPAnimSettings s, FirstPersonController c, ref FPPose p)
        {
            bool on = v.state == FPState.Swimming;
            W = FPSpring.Step(W, ref wV, on ? 1f : 0f, on ? 0.10f : 0.25f, dt);
            float lo = Mathf.Max(0.5f, c.swimSpeed), hi = Mathf.Max(lo + 0.1f, c.swimSprintSpeed);
            StrokeW = FPSpring.Step(StrokeW, ref sV, on ? Mathf.Clamp01(Mathf.InverseLerp(0.3f, lo * 0.8f, v.speed)) : 0f, 0.2f, dt);
            CrawlW = FPSpring.Step(CrawlW, ref cV, on ? Mathf.Clamp01(Mathf.InverseLerp(lo * 1.05f, hi * 0.9f, v.speed)) : 0f, 0.2f, dt);
            if (W < 0.001f) return;

            float rate = Mathf.Lerp(Mathf.Lerp(0.5f, 0.8f, StrokeW), 1.4f, CrawlW);
            phase += rate * dt;
            if (phase > 1000f) phase -= 1000f;
            float a = phase * Mathf.PI * 2f, sn = Mathf.Sin(a), cs = Mathf.Cos(a);

            FPArm tR = new FPArm { flex = 45f + 22f * sn, abd = 30f, elbow = 70f, curl = 0.15f, pron = 0.8f };
            FPArm tL = new FPArm { flex = 45f - 22f * sn, abd = 30f, elbow = 70f, curl = 0.15f, pron = 0.8f };
            FPArm sA = new FPArm { flex = 75f + 35f * sn, abd = 20f + 35f * (0.5f + 0.5f * cs), elbow = 55f - 35f * sn, curl = 0.1f, pron = 0.85f };
            FPArm cR = new FPArm { flex = 85f + 80f * sn, abd = 15f, elbow = 45f + 35f * Mathf.Cos(2f * a), curl = 0.2f, pron = 0.8f };
            FPArm cL = new FPArm { flex = 85f - 80f * sn, abd = 15f, elbow = 45f + 35f * Mathf.Cos(2f * a), curl = 0.2f, pron = 0.8f };

            FPArmKinematics.Blend(ref tR, sA, StrokeW);
            FPArmKinematics.Blend(ref tL, sA, StrokeW);
            FPArmKinematics.Blend(ref tR, cR, CrawlW);
            FPArmKinematics.Blend(ref tL, cL, CrawlW);

            float splash = on ? Mathf.Clamp01(1f - v.stateTime / 0.5f) : 0f;
            tR.flex += 30f * splash; tL.flex += 30f * splash;
            tR.abd += 25f * splash; tL.abd += 25f * splash;

            FPArmKinematics.Blend(ref p.R, tR, W);
            FPArmKinematics.Blend(ref p.L, tL, W);
            p.torsoLean += (6f + 24f * StrokeW + 14f * CrawlW) * W;

            float ka = a * Mathf.Lerp(1f, 3f, CrawlW);
            float amp = 0.08f + 0.10f * StrokeW + 0.06f * CrawlW;
            LegL = new Vector3(-0.10f, 0.14f + amp * 0.5f * Mathf.Cos(ka), -0.10f + amp * Mathf.Sin(ka));
            LegR = new Vector3(0.10f, 0.14f - amp * 0.5f * Mathf.Cos(ka), -0.10f - amp * Mathf.Sin(ka));
        }
    }
}
