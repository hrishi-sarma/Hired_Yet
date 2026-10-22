using System;
using UnityEngine;
namespace FPAnimation
{
    /// <summary>
    /// Guitar ready/playing pose. Chest space = x right, y up, z forward. Guitar-local = z toward headstock, y = soundboard normal, x across the strings.
    /// Real-world hold: neck to the player's left and tilted up, soundboard (strings) faces AWAY from the player, back of the neck sits in the left palm.
    /// Tune live on "FP Rig (Auto)".
    /// </summary>
    [Serializable]
    public class FPGuitarSettings
    {
        [Header("Guitar mount (chest space)")]
        public Vector3 mountPos = new Vector3(0.14f, -0.30f, 0.27f);   // body centre
        public Vector3 neckDir = new Vector3(-0.90f, 0.34f, 0.12f);    // toward the headstock (left, up, a little forward)
        public Vector3 faceDir = new Vector3(0.06f, 0.42f, 0.90f);     // soundboard normal: points away from the player
        public Vector3 lowerOffset = new Vector3(0.05f, -0.45f, -0.10f); // where the guitar sits when lowered / raising
        [Header("Hands (guitar-local mitten centre)")]
        public Vector3 leftHandLocal = new Vector3(0.036f, 0.075f, 0.40f);  // fingers wrap up from under the neck onto the strings, near fret 3
        public Vector3 rightHandLocal = new Vector3(-0.02f, 0.095f, 0.0f); // over the strings by the soundhole
        public Vector3 leftHandUp = new Vector3(1f, -0.3f, 0f);        // back of the hand, guitar-local (palm faces the neck)
        public Vector3 rightHandUp = new Vector3(0f, 1f, 0f);          // back of the hand (palm faces the strings)
        public Vector3 leftElbowHint = new Vector3(-0.2f, -1f, -0.25f);
        public Vector3 rightElbowHint = new Vector3(1f, 0.1f, -0.1f);
        public float leftCurl = 0.6f, rightCurl = 0.3f;
        public float wristBehindHand = 0.07f;                          // mitten centre sits this far ahead of the wrist
        [Header("Playing")]
        public float strumWidth = 0.075f, strumLift = 0.015f;
        public float defaultPeriod = 0.5f;                             // seconds per strum cycle when no beat is detected
        public float chordBeats = 4f;                                  // strum cycles per chord change
        public float[] chordFret = { 0.44f, 0.40f, 0.46f, 0.37f };     // left-hand position along the neck per chord
        public float[] chordCurl = { 0.55f, 0.72f, 0.62f, 0.78f };     // finger squeeze per chord
    }

    /// <summary>Pins both wrists to points on the guitar (IK), animates strum + fretting while playing, publishes the guitar mount.</summary>
    public class FPGuitarLayer
    {
        public Vector3 UpL = Vector3.down, UpR = Vector3.up;   // chest-space back-of-hand vectors
        public float HandW;                                      // how strongly SolveArm applies them

        float phase, period = 0.5f, lastOnset, avg, vibe, prevS, chordClock, fretZ = 0.44f, fretVel, curlL = 0.5f, curlVel;

        public void Apply(FPAnimSettings s, Vector3 origin, Quaternion frame, ref FPPose p)
        {
            float b = FPEvents.GuitarBlend;
            HandW = 0f;
            if (b <= 0.001f) { FPEvents.GuitarVibe = 0f; return; }
            var g = s.guitar;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float e = b * b * (3f - 2f * b);
            float play = Mathf.Clamp01(FPEvents.GuitarPlay), lvl = FPEvents.GuitarLevel;

            // ---- tempo from loudness onsets ----
            avg = Mathf.Lerp(avg, lvl, dt * 1.5f);
            if (play > 0.5f && lvl > avg * 1.3f + 0.04f && Time.time - lastOnset > 0.2f)
            {
                float iv = Time.time - lastOnset; lastOnset = Time.time;
                if (iv < 1.2f) period = Mathf.Lerp(period, Mathf.Clamp(iv, 0.35f, 0.8f), 0.12f);
            }
            else if (Time.time - lastOnset > 2f) period = Mathf.Lerp(period, g.defaultPeriod, dt * 0.5f);
            if (play > 0.02f) { phase += dt / period; chordClock += dt; }

            // ---- strum ----
            float amp = play * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(lvl * 1.5f));
            float sOsc = Mathf.Sin(phase * Mathf.PI * 2f);
            if ((prevS < 0f && sOsc >= 0f) || (prevS > 0f && sOsc <= 0f)) vibe = Mathf.Max(vibe, (0.6f + 0.4f * amp) * play);
            prevS = sOsc;
            vibe *= Mathf.Exp(-dt * 5f);
            FPEvents.GuitarVibe = vibe;

            // ---- fretting hand: slides between chord shapes ----
            int n = Mathf.Max(1, g.chordFret.Length);
            int idx = (int)(chordClock / (period * Mathf.Max(1f, g.chordBeats))) % n;
            float zT = Mathf.Lerp(g.leftHandLocal.z, g.chordFret[idx], play);
            float cT = Mathf.Lerp(g.leftCurl, g.chordCurl[Mathf.Min(idx, g.chordCurl.Length - 1)], play) + 0.06f * vibe;
            fretZ = Mathf.SmoothDamp(fretZ, zT, ref fretVel, 0.15f, Mathf.Infinity, dt);
            curlL = Mathf.SmoothDamp(curlL, cT, ref curlVel, 0.1f, Mathf.Infinity, dt);

            // ---- guitar mount (lowered while blend is small, tiny breathing sway) ----
            Vector3 n3 = g.neckDir.normalized, f = g.faceDir;
            f -= n3 * Vector3.Dot(n3, f);
            Quaternion mr = Quaternion.LookRotation(n3, f.normalized);
            Vector3 mount = g.mountPos + g.lowerOffset * (1f - e) + new Vector3(0f, Mathf.Sin(Time.time * 1.7f) * 0.003f * e, 0f);
            FPEvents.GuitarMountPos = origin + frame * mount;
            FPEvents.GuitarMountRot = frame * mr;

            Vector3 lLoc = new Vector3(g.leftHandLocal.x, g.leftHandLocal.y, fretZ);
            Vector3 rLoc = g.rightHandLocal + new Vector3(sOsc * g.strumWidth * amp, g.strumLift * sOsc * sOsc * amp, 0.012f * Mathf.Cos(phase * Mathf.PI * 2f) * amp);
            UpL = (mr * g.leftHandUp).normalized;
            UpR = (mr * g.rightHandUp).normalized;
            HandW = e;

            var L = Arm(-1f, s, mount + mr * lLoc, g.leftElbowHint, curlL, 0.3f);
            var R = Arm(1f, s, mount + mr * rLoc, g.rightElbowHint, g.rightCurl, 0.85f);
            FPArmKinematics.Blend(ref p.L, L, e);
            FPArmKinematics.Blend(ref p.R, R, e);
        }

        // Wrist target so the mitten centre lands on 'blob'; FK angles are derived from the IK elbow so the pole matches.
        static FPArm Arm(float side, FPAnimSettings s, Vector3 blob, Vector3 hint, float curl, float pron)
        {
            Vector3 sh = new Vector3(side * s.shoulderOffset.x, s.shoulderOffset.y, s.shoulderOffset.z);
            Vector3 dir = (blob - sh).normalized, elbow = sh, wrist = blob;
            for (int i = 0; i < 3; i++)
            {
                wrist = blob - dir * s.guitar.wristBehindHand;
                FPTwoBoneIK.Solve(sh, wrist, sh + hint.normalized, s.upperArm, s.foreArm, out elbow, out Vector3 end);
                dir = (end - elbow).normalized;
            }
            Vector3 u = (elbow - sh).normalized;
            float theta = Mathf.Asin(Mathf.Clamp(u.x, -1f, 1f)) * Mathf.Rad2Deg;
            float abd = side * theta;
            float flex = -Mathf.Atan2(-u.z, -u.y) * Mathf.Rad2Deg;
            Quaternion q = Quaternion.AngleAxis(-flex, Vector3.right) * Quaternion.AngleAxis(side * abd, Vector3.forward);
            Vector3 fl = Quaternion.Inverse(q) * (wrist - elbow).normalized;
            float el = Mathf.Atan2(fl.z, -fl.y) * Mathf.Rad2Deg;
            return new FPArm { flex = flex, abd = abd, elbow = el, curl = curl, pron = pron, ikW = 1f, ikTarget = wrist, off = Vector3.zero };
        }
    }
}
