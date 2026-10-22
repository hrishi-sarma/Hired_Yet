using UnityEngine;
namespace FPAnimation
{
    /// <summary>Jump, fall and landing. LandVal (0..1) also drives the knee dip.</summary>
    public class FPAirLayer
    {
        public float AirW, FallN, LandVal;
        float aV, fV, lV;

        public void Apply(in FPLoco v, float dt, FPAnimSettings s, ref FPPose p)
        {
            bool air = v.airborne && v.state != FPState.Swimming && v.state != FPState.LedgeGrab;
            AirW = FPSpring.Step(AirW, ref aV, air ? 1f : 0f, 0.08f, dt);
            FallN = FPSpring.Step(FallN, ref fV, Mathf.Clamp01(-v.vy / 12f), 0.10f, dt);
            float rise = v.vy > 0f ? Mathf.Clamp01(v.vy / 6f) : 0f;

            // takeoff: arms swing up. falling: arms fly up and out (shoulder abduction), elbows straighten for balance
            float dFlex = (18f * rise + 18f * FallN) * AirW;
            float dAbd = (10f + 40f * FallN) * AirW;
            float dElb = -25f * FallN * AirW;
            p.R.flex += dFlex; p.L.flex += dFlex; p.R.abd += dAbd; p.L.abd += dAbd; p.R.elbow += dElb; p.L.elbow += dElb;
            float open = AirW * FallN;
            p.R.curl = Mathf.Lerp(p.R.curl, 0.1f, open); p.L.curl = Mathf.Lerp(p.L.curl, 0.1f, open);

            // landing: soft = small dip, hard = arms brace down and out
            if (v.justLanded)
            {
                float k = Mathf.Clamp01(v.landSpeed / Mathf.Max(1f, s.landHardSpeed));
                if (k > 0.08f) { LandVal = Mathf.Max(LandVal, k); lV = 0f; }
            }
            LandVal = FPSpring.Step(LandVal, ref lV, 0f, 0.16f, dt);
            p.R.flex += 12f * LandVal; p.L.flex += 12f * LandVal;
            p.R.abd += 15f * LandVal; p.L.abd += 15f * LandVal;
            p.R.elbow -= 30f * LandVal; p.L.elbow -= 30f * LandVal;
            p.torsoLean += 12f * LandVal;
        }
    }
}
