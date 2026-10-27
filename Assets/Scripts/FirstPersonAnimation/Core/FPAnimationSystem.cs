using UnityEngine;
using EasyPeasyFirstPersonController;
namespace FPAnimation
{
    /// <summary>
    /// Owns the rig. Lives on its own root object (NOT under the player) so InterviewUI.FreezePlayer() cannot freeze it.
    /// Stage 1: rest pose + idle breathing only. Layers plug in later at the "pose" step.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public class FPAnimationSystem : MonoBehaviour
    {
        public FPAnimSettings settings = new FPAnimSettings();
        public FirstPersonController Controller { get; private set; }
        public FPRig Rig { get; private set; }
        public FPViewModelRenderer ViewModel { get; private set; }

        readonly FPStateReader reader = new FPStateReader();
        readonly FPLocomotionLayer loco = new FPLocomotionLayer();
        readonly FPAirLayer air = new FPAirLayer();
        readonly FPLegsAnimator legs = new FPLegsAnimator();
        readonly FPLedgeLayer ledge = new FPLedgeLayer();
        readonly FPSwimLayer swim = new FPSwimLayer();
        readonly FPCameraShakeReaction shake = new FPCameraShakeReaction();
        readonly FPActionLayer actions = new FPActionLayer();
        readonly FPGuitarLayer guitar = new FPGuitarLayer();
        FPCameraOffset camOffset;
        Transform headProxy, neckProxy;
        public string ActionName { get { return actions.Name; } }
        public FPLoco Loco { get { return reader.V; } }
        FPArm sL, sR; FPArmVel vL, vR;
        float scale;

        public static FPAnimationSystem Create(FirstPersonController c)
        {
            var go = new GameObject("FP Rig (Auto)");
            var s = go.AddComponent<FPAnimationSystem>();
            s.Init(c);
            return s;
        }

        void Init(FirstPersonController c)
        {
            Controller = c;
            scale = Mathf.Max(0.1f, c.standingCameraHeight / 1.75f);
            if (settings.hideCapsuleRenderers)
                foreach (var r in c.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;

            ViewModel = new FPViewModelRenderer(settings);
            bool web = settings.handDetail == HandDetail.Web ||
                       (settings.handDetail == HandDetail.Auto && Application.platform == RuntimePlatform.WebGLPlayer);
            Rig = new FPRigBuilder(settings, ViewModel.Layer, web).Build(transform, scale);
            ViewModel.Attach(c.cam, transform);
            FPEvents.HandL = Rig.armL.end;
            FPEvents.HandR = Rig.armR.end;

            var rest = FPPose.Rest(settings);
            sL = rest.L; sR = rest.R;
            camOffset = gameObject.AddComponent<FPCameraOffset>();
            camOffset.Init(c.cameraParent);
            actions.Item = new FPItemInHand(Rig.armR.end, ViewModel.Layer, new Color(0.35f, 0.22f, 0.12f));
            actions.Subscribe();
            if (settings.castBodyShadow)
            {
                var m = Rig.torso.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                headProxy = ShadowProxy(PrimitiveType.Sphere, "headShadow", new Vector3(0.20f, 0.24f, 0.22f) * scale, m);
                neckProxy = ShadowProxy(PrimitiveType.Capsule, "neckShadow", new Vector3(0.10f, 0.07f, 0.10f) * scale, m);
            }
        }

        Transform ShadowProxy(PrimitiveType t, string n, Vector3 sc, Material m)
        {
            var go = GameObject.CreatePrimitive(t);
            WorldUtil.Kill(go.GetComponent<Collider>());
            go.name = n; go.layer = 0;
            go.transform.SetParent(transform, false);
            go.transform.localScale = sc;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            r.receiveShadows = false;
            return go.transform;
        }

        void OnDestroy() { actions.Unsubscribe(); if (camOffset != null) camOffset.Revert(); ViewModel?.Detach(); }

        void LateUpdate()
        {
            if (Controller == null) { WorldUtil.Kill(gameObject); return; }   // scene unloaded, bootstrap will re-attach
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Transform cam = Controller.playerCamera;
            camOffset.Apply(new Vector3(0f, actions.Dip, 0f) + Controller.cameraParent.InverseTransformVector(actions.Move));
            float rawPitch = Mathf.DeltaAngle(0f, cam.localEulerAngles.x);
            if (actions.Pitch != 0f) cam.localRotation = Quaternion.Euler(actions.Pitch, 0f, 0f) * cam.localRotation;
            Vector3 camBase = cam.position - Controller.cameraParent.TransformVector(camOffset.Applied);
            var rig = Rig;
            var st = settings;

            // ---- chest frame: arms hang from the body. Yaw + part of the camera pitch/tilt ----
            Quaternion yaw = Quaternion.Euler(0f, Controller.transform.eulerAngles.y, 0f);
            Vector3 rel = (Quaternion.Inverse(yaw) * cam.rotation).eulerAngles;
            Quaternion frame = yaw * Quaternion.Euler(Mathf.DeltaAngle(0f, rel.x) * st.armPitchFollow, 0f, Mathf.DeltaAngle(0f, rel.z) * st.armTiltFollow);
            float back = ViewModel.Retract(cam, dt);
            Vector3 origin = cam.position - cam.forward * back;
            rig.viewRoot.SetPositionAndRotation(origin, frame);
            ViewModel.SyncOverlay(cam);

            // ---- state + layers (base -> air -> additive idle) ----
            reader.Update(Controller, dt, st);
            var v = reader.V;
            FPPose p = FPPose.Rest(st);
            loco.Apply(v, dt, st, Controller, ref p);
            air.Apply(v, dt, st, ref p);
            ledge.Apply(v, dt, st, Controller, ref p);
            swim.Apply(v, dt, st, Controller, ref p);
            var ax = new FPActionCtx { v = v, dt = dt, s = st, c = Controller, origin = origin, frame = frame, camBase = camBase, rawPitch = rawPitch };
            actions.Apply(ref ax, ref p);
            shake.Apply(dt, st, Controller, ref p);
            legs.Evaluate(v, loco, air, dt, st);
            legs.FootL = Vector3.Lerp(legs.FootL, ledge.LegL, ledge.W);
            legs.FootR = Vector3.Lerp(legs.FootR, ledge.LegR, ledge.W);
            legs.FootL = Vector3.Lerp(legs.FootL, swim.LegL, swim.W);
            legs.FootR = Vector3.Lerp(legs.FootR, swim.LegR, swim.W);

            // ---- sitting: bench / log (knees bent, feet on the floor) or cross-legged on the ground ----
            float sw = Mathf.Clamp01(FPEvents.SitBlend); sw = sw * sw * (3f - 2f * sw);
            int sitKind = FPEvents.SitKind;
            if (sw <= 0.0005f) sitKind = 0;
            Vector3 kneeL = new Vector3(0f, 0f, 1f), kneeR = kneeL;
            if (sitKind == 1)
            {
                float hh = Mathf.Max(0.2f, FPEvents.SitSeatHeight / scale);
                float fz = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0f, 0.68f * 0.68f - hh * hh)), 0.14f, 0.5f);
                legs.FootL = Vector3.Lerp(legs.FootL, new Vector3(-0.15f, 0.07f, fz), sw);
                legs.FootR = Vector3.Lerp(legs.FootR, new Vector3(0.17f, 0.07f, fz + 0.04f), sw);
                kneeL = Vector3.Lerp(kneeL, new Vector3(-0.25f, 0.2f, 1f), sw);
                kneeR = Vector3.Lerp(kneeR, new Vector3(0.25f, 0.2f, 1f), sw);
                p.torsoLean += -5f * sw;
            }
            else if (sitKind == 2)
            {
                legs.FootL = Vector3.Lerp(legs.FootL, new Vector3(0.19f, 0.09f, 0.24f), sw);
                legs.FootR = Vector3.Lerp(legs.FootR, new Vector3(-0.17f, 0.15f, 0.30f), sw);
                kneeL = Vector3.Lerp(kneeL, new Vector3(-1f, 0.45f, 0.35f), sw);
                kneeR = Vector3.Lerp(kneeR, new Vector3(1f, 0.45f, 0.35f), sw);
                p.torsoLean += 4f * sw;
            }

            float t = Time.time;
            float idle = 1f - loco.MoveW * 0.7f;
            float br = Mathf.Sin(t * Mathf.PI * 2f * st.breathHz) * st.breathAmp;
            float brDeg = Mathf.Sin(t * Mathf.PI * 2f * st.breathHz) * 1.2f * idle;
            p.L.flex += brDeg; p.R.flex += brDeg * 0.9f; p.L.elbow -= brDeg * 0.6f; p.R.elbow -= brDeg * 0.6f;
            float flex = Mathf.Sin(t * 0.5f) * 0.04f * idle;
            p.L.curl += flex; p.R.curl -= flex;

            // While carrying an inventory item, lock the right arm into a raised carry pose.
            // This override is deliberately after locomotion, look inertia, action and shake layers,
            // so movement sway cannot make the held item wobble with the arm.
            if (FPEvents.HoldingItem && !FPEvents.GuitarHeld)
            {
                p.R.flex = 56f;
                p.R.abd = 10f;
                p.R.elbow = 76f;
                p.R.curl = 0.25f;
                p.R.pron = 0.35f;
                p.R.off = Vector3.zero;
                p.R.ikW = 0f;
                p.R.ikTarget = Vector3.zero;
            }

            // Guitar playing pose: both wrists pinned to the guitar (blend 0 = no effect)
            guitar.Apply(st, origin, frame, cam.position - camBase, ref p);

            FPArmKinematics.Smooth(ref sR, ref vR, p.R, st.handSpringTime, dt);
            FPArmKinematics.Smooth(ref sL, ref vL, p.L, st.handSpringTime, dt);
            SolveArm(rig.armR, rig.handR, sR, 1f, origin, frame);
            SolveArm(rig.armL, rig.handL, sL, -1f, origin, frame);

            // ---- body (yaw only, so looking down shows torso and legs) ----
            float ground = Controller.characterController.bounds.min.y;
            Vector3 c = cam.position;
            Transform br0 = rig.bodyRoot;
            Quaternion yawQ = Quaternion.Euler(0f, Controller.transform.eulerAngles.y, 0f);
            float camH = c.y - ground, s = scale;
            // body axis sits 0.13 m behind the camera (head is forward of the spine), so torso, hips and legs stack on ONE vertical axis
            br0.SetPositionAndRotation(new Vector3(c.x, ground, c.z) - yawQ * Vector3.forward * ((0.13f + Mathf.Clamp01(-actions.Dip / 0.8f) * 0.3f + actions.BodyBack) * s), yawQ);
            float hipFloor = Mathf.Lerp(0.30f, 0.07f, sitKind == 2 ? sw : 0f);
            float hipY = Mathf.Max(camH - 0.80f * s - air.LandVal * 0.20f * s, hipFloor * s);
            rig.hips.localPosition = new Vector3(0f, hipY + 0.04f * s, 0f);
            // torso leans about the hip joint, not about its own centre
            Quaternion lean = Quaternion.Euler(p.torsoLean, 0f, 0f);
            // torso spans from its original bottom (0.72 below the camera) up to just under the shoulder anchor, so the arms attach to its top corners
            float torsoTop = st.shoulderOffset.y * s - 0.025f * s;
            float torsoBot = -0.72f * s;
            rig.torso.localScale = new Vector3(1f, Mathf.Max(0.5f, (torsoTop - torsoBot) / (0.46f * s)), 1f);
            rig.torso.localPosition = new Vector3(0f, hipY, 0f) + lean * new Vector3(0f, camH + (torsoTop + torsoBot) * 0.5f - hipY + br * 0.5f, -0.03f * s);
            rig.torso.localRotation = lean;
            if (headProxy != null)
            {
                headProxy.SetPositionAndRotation(c - yawQ * Vector3.forward * (0.07f * s) - Vector3.up * (0.04f * s), yawQ);
                neckProxy.SetPositionAndRotation(c - yawQ * Vector3.forward * (0.10f * s) - Vector3.up * (0.22f * s), yawQ);
            }
            SolveLeg(rig.legR, br0, 1f, hipY, s, legs.FootR, kneeR);
            SolveLeg(rig.legL, br0, -1f, hipY, s, legs.FootL, kneeL);
        }

        void SolveArm(FPLimb arm, FPHand hand, in FPArm a, float side, Vector3 origin, Quaternion frame)
        {
            FPArmKinematics.Solve(a, side, settings, origin, frame, out Vector3 sh, out Vector3 el, out Vector3 wr, out Quaternion hr);
            if (a.ikW > 0.001f) wr = Vector3.Lerp(wr, origin + frame * a.ikTarget, Mathf.Clamp01(a.ikW));
            arm.Solve(sh, wr, el);
            if (a.ikW > 0.001f)
            {
                Vector3 d = arm.end.position - arm.mid.position;
                if (d.sqrMagnitude > 1e-6f)
                {
                    Vector3 up = Vector3.Slerp(frame * new Vector3(side, 0f, 0f), Vector3.up, Mathf.Clamp01(a.pron));
                    hr = Quaternion.Slerp(hr, Quaternion.LookRotation(d, up), Mathf.Clamp01(a.ikW));
                    if (guitar.HandW > 0.001f)
                        hr = Quaternion.Slerp(hr, Quaternion.LookRotation(d, frame * (side < 0f ? guitar.UpL : guitar.UpR)), guitar.HandW);
                }
            }
            arm.end.rotation = hr;
            hand.SetCurl(a.curl, a.curl * 0.6f);
        }

        void SolveLeg(FPLimb leg, Transform body, float side, float hipY, float s, Vector3 footLocal, Vector3 knee)
        {
            Vector3 root = body.TransformPoint(new Vector3(side * 0.09f * s, hipY, 0f));
            Vector3 foot = body.TransformPoint(footLocal * s);
            Vector3 pole = body.TransformPoint(new Vector3(side * 0.09f * s, hipY, 0f) + knee);   // knee points toward 'knee' (forward when standing)
            leg.Solve(root, foot, pole);
            leg.end.rotation = body.rotation;
        }
    }
}
