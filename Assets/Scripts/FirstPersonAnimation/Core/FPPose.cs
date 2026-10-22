using UnityEngine;
namespace FPAnimation
{
    /// <summary>One arm described by joint angles (degrees). off = small additive wrist offset in chest space (look inertia, later IK nudges).</summary>
    public struct FPArm { public float flex, abd, elbow, curl, pron, ikW; public Vector3 off, ikTarget; }
    public struct FPArmVel { public float flex, abd, elbow, curl, pron, ikW; public Vector3 off, ikTarget; }

    /// <summary>Layer output.</summary>
    public struct FPPose
    {
        public FPArm L, R;
        public float torsoLean;   // degrees, + = forward

        public static FPPose Rest(FPAnimSettings s)
        {
            var a = new FPArm { flex = s.restFlex, abd = s.restAbd, elbow = s.restElbow, curl = s.restCurl, pron = 0.25f };
            return new FPPose { L = a, R = a };
        }
    }

    public static class FPArmKinematics
    {
        /// <summary>
        /// Forward kinematics in the chest frame (x right, y up, z forward). Arm hangs down at all-zero angles.
        /// flex = swing forward about the shoulder, abd = swing outward, elbow = forearm flexion.
        /// side: +1 right, -1 left.
        /// </summary>
        public static void Solve(in FPArm a, float side, FPAnimSettings s, Vector3 origin, Quaternion frame,
                                 out Vector3 shoulderW, out Vector3 elbowW, out Vector3 wristW, out Quaternion handW)
        {
            float flex = Mathf.Clamp(a.flex, -60f, 170f), abd = Mathf.Clamp(a.abd, -10f, 120f), el = Mathf.Clamp(a.elbow, 0f, 150f);
            Quaternion q = Quaternion.AngleAxis(-flex, Vector3.right) * Quaternion.AngleAxis(side * abd, Vector3.forward);
            Vector3 dU = q * Vector3.down;
            Vector3 dF = (q * Quaternion.AngleAxis(-el, Vector3.right)) * Vector3.down;
            Vector3 sh = new Vector3(side * s.shoulderOffset.x, s.shoulderOffset.y, s.shoulderOffset.z);
            Vector3 e = sh + dU * s.upperArm;
            Vector3 w = e + dF * s.foreArm + a.off;
            // neutral grip: palm faces the body (dorsal side outward), pron blends toward palm-down
            Vector3 up = Vector3.Slerp(new Vector3(side, 0f, 0f), Vector3.up, Mathf.Clamp01(a.pron));
            shoulderW = origin + frame * sh;
            elbowW = origin + frame * e;
            wristW = origin + frame * w;
            handW = frame * Quaternion.LookRotation(dF, up);
        }

        public static void Smooth(ref FPArm c, ref FPArmVel v, in FPArm t, float st, float dt)
        {
            c.flex = FPSpring.Step(c.flex, ref v.flex, t.flex, st, dt);
            c.abd = FPSpring.Step(c.abd, ref v.abd, t.abd, st, dt);
            c.elbow = FPSpring.Step(c.elbow, ref v.elbow, t.elbow, st, dt);
            c.curl = FPSpring.Step(c.curl, ref v.curl, t.curl, st, dt);
            c.pron = FPSpring.Step(c.pron, ref v.pron, t.pron, st, dt);
            c.off = FPSpring.Step(c.off, ref v.off, t.off, st, dt);
            c.ikW = FPSpring.Step(c.ikW, ref v.ikW, t.ikW, st, dt);
            c.ikTarget = t.ikW > 0.001f && c.ikW < 0.02f ? t.ikTarget : FPSpring.Step(c.ikTarget, ref v.ikTarget, t.ikTarget, st, dt);
        }

        public static void Blend(ref FPArm a, in FPArm b, float t)
        {
            a.flex = Mathf.Lerp(a.flex, b.flex, t); a.abd = Mathf.Lerp(a.abd, b.abd, t);
            a.elbow = Mathf.Lerp(a.elbow, b.elbow, t); a.curl = Mathf.Lerp(a.curl, b.curl, t);
            a.pron = Mathf.Lerp(a.pron, b.pron, t); a.off = Vector3.Lerp(a.off, b.off, t);
            a.ikTarget = a.ikW > 0.001f ? Vector3.Lerp(a.ikTarget, b.ikTarget, t) : b.ikTarget;
            a.ikW = Mathf.Lerp(a.ikW, b.ikW, t);
        }
    }
}
