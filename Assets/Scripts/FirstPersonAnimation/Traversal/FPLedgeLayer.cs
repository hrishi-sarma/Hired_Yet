using UnityEngine;
using EasyPeasyFirstPersonController;
namespace FPAnimation
{
    public class FPLedgeLayer
    {
        public float W;
        public Vector3 LegL, LegR;
        float wV;

        static readonly float[] times = { 0f, 0.2f, 0.35f, 0.75f, 1f };
        static readonly FPArm[] keys =
        {
            new FPArm { flex = 40f,  abd = 14f, elbow = 40f,  curl = 0.2f, pron = 0.6f },
            new FPArm { flex = 150f, abd = 12f, elbow = 12f,  curl = 0.15f, pron = 0.8f },
            new FPArm { flex = 152f, abd = 10f, elbow = 8f,   curl = 0.95f, pron = 0.9f },
            new FPArm { flex = 95f,  abd = 14f, elbow = 105f, curl = 0.95f, pron = 0.9f },
            new FPArm { flex = 30f,  abd = 14f, elbow = 50f,  curl = 0.2f, pron = 0.9f },
        };

        static readonly Vector3 dangleL = new Vector3(-0.10f, 0.12f, -0.04f), dangleR = new Vector3(0.10f, 0.10f, -0.08f);
        static readonly Vector3 kneeL = new Vector3(-0.10f, 0.45f, 0.30f), kneeR = new Vector3(0.10f, 0.28f, 0.12f);

        static FPArm Sample(float t)
        {
            t = Mathf.Clamp01(t);
            int i = 0;
            while (i < times.Length - 2 && t > times[i + 1]) i++;
            float f = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(times[i], times[i + 1], t));
            FPArm a = keys[i];
            FPArmKinematics.Blend(ref a, keys[i + 1], f);
            return a;
        }

        public void Apply(in FPLoco v, float dt, FPAnimSettings s, FirstPersonController c, ref FPPose p)
        {
            bool on = v.state == FPState.LedgeGrab;
            W = FPSpring.Step(W, ref wV, on ? 1f : 0f, on ? 0.08f : 0.15f, dt);
            if (W < 0.001f) return;

            float t = on ? Mathf.Clamp01(v.stateTime / Mathf.Max(0.05f, c.climbDuration)) : 1f;
            FPArm r = Sample(t), l = Sample(t - 0.05f);
            FPArmKinematics.Blend(ref p.R, r, W);
            FPArmKinematics.Blend(ref p.L, l, W);

            float pull = Mathf.Sin(Mathf.Clamp01((t - 0.35f) / 0.65f) * Mathf.PI);
            p.torsoLean += (6f + 14f * pull) * W;

            float pw = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.3f) / 0.15f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.8f) / 0.2f)));
            LegL = Vector3.Lerp(dangleL, kneeL, pw);
            LegR = Vector3.Lerp(dangleR, kneeR, pw);
        }
    }
}
