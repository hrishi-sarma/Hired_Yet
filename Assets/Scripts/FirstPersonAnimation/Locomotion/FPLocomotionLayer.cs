using UnityEngine;
using EasyPeasyFirstPersonController;
namespace FPAnimation
{
    /// <summary>
    /// Anatomical arm motion. Arms swing at the shoulder like pendulums, opposite to the legs
    /// (right arm forward when left leg forward). Walk: small swing, relaxed elbows.
    /// Sprint: ~90 degree elbows, big pumping swing, elbow flexes more as the arm comes forward.
    /// </summary>
    public class FPLocomotionLayer
    {
        public float Phase, MoveW, SprintW, CrouchW, SlideW, SpeedNorm, Strafe;
        float mV, spV, cV, slV, stV, lxV, lyV;
        float lx, ly;

        static readonly FPArm slideR = new FPArm { flex = 55f, abd = 15f, elbow = 15f, curl = 0.1f, pron = 0.5f };
        static readonly FPArm slideL = new FPArm { flex = -30f, abd = 22f, elbow = 30f, curl = 0.5f, pron = 0.25f };

        static float Mix(float rest, float walk, float sprint, float mw, float sw)
        { return Mathf.Lerp(Mathf.Lerp(rest, walk, mw), sprint, sw); }

        public void Apply(in FPLoco v, float dt, FPAnimSettings s, FirstPersonController c, ref FPPose p)
        {
            SpeedNorm = Mathf.Clamp01(v.speed / Mathf.Max(0.1f, c.sprintSpeed));
            bool walking = !v.airborne && v.speed > 0.2f && (v.state == FPState.Grounded || v.state == FPState.Crouching);
            MoveW = FPSpring.Step(MoveW, ref mV, walking ? 1f : 0f, 0.10f, dt);
            SprintW = FPSpring.Step(SprintW, ref spV, v.sprinting ? 1f : 0f, 0.15f, dt);
            CrouchW = FPSpring.Step(CrouchW, ref cV, v.state == FPState.Crouching ? 1f : 0f, 0.12f, dt);
            SlideW = FPSpring.Step(SlideW, ref slV, v.state == FPState.Sliding ? 1f : 0f, 0.10f, dt);
            float strafeT = Mathf.Clamp(v.localVel.x / Mathf.Max(0.1f, c.walkSpeed), -1f, 1f);
            Strafe = FPSpring.Step(Strafe, ref stV, strafeT, 0.12f, dt);

            float stride = Mathf.Lerp(Mathf.Lerp(s.strideWalk, s.strideSprint, SprintW), s.strideCrouch, CrouchW);
            if (walking) Phase += v.speed * dt / stride;
            if (Phase > 1000f) Phase -= 1000f;
            float a = Phase * Mathf.PI * 2f;
            float sn = Mathf.Sin(a), sne = Mathf.Sin(a + 0.5f);   // elbow peaks slightly after the arm is furthest forward

            // mean pose (relaxed carry -> walk -> sprint)
            float flexM = Mix(p.R.flex, s.walkFlex, s.sprintFlex, MoveW, SprintW);
            float elbM = Mix(p.R.elbow, s.walkElbow, s.sprintElbow, MoveW, SprintW);
            float abdM = Mix(p.R.abd, s.walkAbd, s.sprintAbd, MoveW, SprintW);

            // swing amplitude: scales with speed, smaller when crouched
            float speedScale = Mathf.Clamp(v.speed / Mathf.Max(0.1f, c.walkSpeed), 0.5f, 1.2f);
            float crouchK = 1f - 0.4f * CrouchW;
            float A = Mathf.Lerp(s.walkAmp * speedScale, s.sprintAmp, SprintW) * MoveW * crouchK;
            float Ae = Mathf.Lerp(s.walkElbowAmp, s.sprintElbowAmp, SprintW) * MoveW * crouchK;
            float drift = 3f * MoveW;   // forward-swinging arm drifts slightly toward the midline

            p.R.flex = flexM + A * sn;  p.L.flex = flexM - A * sn;
            p.R.elbow = elbM + Ae * sne; p.L.elbow = elbM - Ae * sne;
            p.R.abd = abdM - drift * sn; p.L.abd = abdM + drift * sn;

            // sprint: fists
            p.R.curl = Mathf.Lerp(p.R.curl, 0.85f, SprintW); p.L.curl = Mathf.Lerp(p.L.curl, 0.85f, SprintW);
            p.torsoLean += s.sprintLean * SprintW;

            // crouch: forearms drawn in and a bit higher
            float cr = CrouchW;
            p.R.flex += 8f * cr; p.L.flex += 8f * cr; p.R.elbow += 10f * cr; p.L.elbow += 10f * cr;
            p.R.abd -= 4f * cr; p.L.abd -= 4f * cr;

            // strafe: counter-balance, the arm on the far side opens, the near side closes
            p.R.abd -= Strafe * 6f; p.L.abd += Strafe * 6f;

            // look inertia: hands lag behind the camera, then settle
            Vector2 tgt = Vector2.ClampMagnitude(new Vector2(-v.look.x, -v.look.y) * s.lookGain, s.lookMax);
            lx = FPSpring.Step(lx, ref lxV, tgt.x, s.lookSmooth, dt);
            ly = FPSpring.Step(ly, ref lyV, tgt.y, s.lookSmooth, dt);
            p.R.off += new Vector3(lx, ly, 0f); p.L.off += new Vector3(lx, ly, 0f);

            // slide: right arm braces forward and down, left trails back
            if (SlideW > 0.001f)
            {
                FPArmKinematics.Blend(ref p.R, slideR, SlideW);
                FPArmKinematics.Blend(ref p.L, slideL, SlideW);
                p.torsoLean += s.slideLean * SlideW;
            }
        }
    }
}
