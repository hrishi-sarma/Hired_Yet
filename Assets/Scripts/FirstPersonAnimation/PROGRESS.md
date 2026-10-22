# FirstPersonAnimation: PROGRESS

## Stage 1: DONE (tested, mitten hands approved)
Install: copy this folder to `Assets/Scripts/FirstPersonAnimation/`. No scene setup, no edits to Easy Peasy files.
Assumes no `.asmdef` splits the project. If you have one, reference `EasyPeasyFirstPersonController` from it.

### Manual steps in Unity (once)
1. Edit > Project Settings > Tags and Layers > pick an empty User Layer (e.g. 8) and name it exactly `ViewModel`.
2. Make sure the pipeline is URP and the main camera's Render Type is `Base` (default).
3. Press Play. Hands (cute mitten, no fingers), arms, torso and legs appear. Without the layer the system falls back to single-camera mode (see the debug line).

### What exists
- `Core/FPAnimBootstrap`: auto-installs, polls 4x/s, creates the rig per FirstPersonController, re-attaches after scene loads.
- `Core/FPAnimationSystem`: root object `FP Rig (Auto)` (outside player root), LateUpdate order +100. Rest pose + breathing. The `// ---- pose ----` block is where Stage 2+ layers plug in (output `FPPose`).
- `Core/FPAnimSettings`: all tunables, palette, rendering options. Edit live on the rig object.
- `Core/FPSpring`, `FPTwoBoneIK` (+`FPLimb`), `FPPose`.
- `Rig/FPRigBuilder` (also builds torso/hips/legs/feet; `FPBody.cs` was folded in here), `FPHand` (mitten blob + thumb, `SetCurl(curl, thumbCurl)` squeezes it), `FPViewModelRenderer`.
- `Debug/FPAnimDebug`: state/speed/grounded/mode/renderer-count line under the controller's own label (editor and dev builds).

### Decisions / behaviour
- Arms live in camera space (follow head-bob and tilt). Body lives in yaw-only space at the camera's X/Z, ground = `characterController.bounds.min.y`.
- Everything is on the `ViewModel` layer, drawn by an Overlay camera (FOV 60, near 0.01) added to the base camera stack at runtime. Base camera culls that layer.
- Body parts have a `ShadowsOnly` child on the Default layer as shadow proxy (`castBodyShadow`).
- Capsule MeshRenderers under the player root are disabled (`hideCapsuleRenderers`).
- Materials cloned from the pipeline default material (no Shader.Find, WebGL safe). Primitive colliders destroyed. About 60 renderers total (High hands), fewer with `HandDetail.Web` (auto on WebGL).
- Feet are rigid under the hips for now (no stepping until Stage 2). Crouch camera drop just compresses the legs.

## Stage 2.1: arm anatomy rework (replaces the position-based hands)

### Stage 2.3 (game-reference tuning)
- Walk swing raised to +-30 deg (mean flex 2, elbow 26 +- 9): a clear alternate swing like AAA first-person games. Sprint swing cut to +-24 deg (mean flex 22, elbow 92 +- 8): compact pumping arms with locked 90 deg elbows. All values live in `FPAnimSettings` (walkAmp, sprintAmp, ...).

### Stage 2.2 (feedback fixes)
- Idle: arms hang almost straight (flex 12, abd 10, elbow 16). Hands are mostly off-screen while looking forward, visible when looking down; tune `shoulderOffset` to show more.
- Walk: alternate front/back shoulder swing +-30 deg with a light elbow fold (about 26 deg, flexing 9 deg more on the forward swing). Sprint unchanged.
- Cycle 20% slower for walk, sprint and crouch (stride lengths x1.25: 1.75 / 2.375 / 1.125 m). Hands and legs share the phase.
- Bent-forward body fixed: torso, hips and legs now stack on one vertical axis (body axis 0.13 m behind camera; before, legs were 0.13 m ahead of the torso). Legs are 0.90 m so standing is nearly straight. Torso lean pivots about the hip joint.
Problem: hands were steering-wheel style at the centre with front/back sway. Fix:
- **Joint-angle FK arms** (`FPArmKinematics` in `FPPose.cs`): shoulder flexion (swing forward/back), shoulder abduction (out to the side), elbow flexion. IK is given the FK elbow as pole so it reproduces the FK pose exactly. Layers now output `FPArm {flex, abd, elbow, curl, pron, off}` per arm.
- **Rest pose** is a relaxed carry (flex 32, abd 20, elbow 88): forearms forward, hands low and out to the sides in the lower screen corners, palms facing the body (neutral grip, thumb up).
- **Walk:** arms swing at the shoulder like pendulums, opposite to the legs (right arm forward when left leg forward), +-18 deg, relaxed elbows, small drift toward the midline on the forward swing.
- **Sprint:** about 92 deg elbows, +-24 deg pumping swing, elbow flexes more as the arm comes forward, fists.
- **Arms hang from the body, not the head:** chest frame = player yaw + 70% of camera pitch (`armPitchFollow`) and 60% of tilt. Looking down brings the arms up into view, looking up drops them out.
- **Anatomy cheat (documented):** real shoulders are ~0.30 m below the camera, so hands would never be on screen. The shoulder anchor is raised 0.16 m (`shoulderOffset`), and overlay FOV is now 70.
- Slide, jump, fall, landing, crouch, strafe are all angle based now (fall = abduction, arms fly out; landing = arms extend down and out). Look inertia uses the small `off` wrist offset.
- Arms swing in and out of the lower corners during the gait. That is expected: the back-swing is below the view.

## Stage 2: DONE (written, not yet tested in Unity)
- `Core/FPStateReader`: `FPLoco` snapshot (state enum, stateTime, speed, localVel, vy, airborne with 0.08 s grace, justLanded + landSpeed, sprinting, slideT, move, look).
- `Locomotion/FPLocomotionLayer`: stride phase from distance (1.4 / 1.9 / 0.9 m), anatomical shoulder/elbow swing opposite to the legs (see Stage 2.1), sprint fists and lean, crouch drawn in, strafe shift + roll, look inertia, slide pose (right hand braces, left trails, lean back). Exposes `Phase, MoveW, SprintW, CrouchW, SlideW, SpeedNorm`.
- `Locomotion/FPAirLayer`: takeoff rise, fall arms up/out (scales with fall speed), landing dip (`LandVal` 0..1, soft vs hard by `landHardSpeed`), also drives knee dip and torso lean.
- `Locomotion/FPLegsAnimator`: foot targets (walk cycle, slide legs forward, jump/fall dangle), static `FootDown(isLeft, intensity)` event.
- `FPAnimSettings` gained a Locomotion header (stride, swing, lean, look gain, land speed, grace).
- `FPPose` gained `torsoLean`. System applies it to the torso.
- Slide progress uses stateTime / slideDuration (controller's slideTimer is private).
- Tune live on the `FP Rig (Auto)` object while playing.

## Stage 3: DONE (written, not yet tested in Unity)
- `Traversal/FPLedgeLayer`: keyframed arm reach, grab, pull, push over `climbDuration` (left arm 0.05 behind right), torso lean, legs dangle then knee up.
- `Traversal/FPSwimLayer`: tread, breaststroke and crawl blended by speed vs `swimSpeed` / `swimSprintSpeed`, entry splash, torso lean, kick legs.
- `Traversal/FPCameraShakeReaction`: flinch on rising edge of `cameraShakeTimer`, strength from `cameraShakeIntensity`, jolt away from `cameraShakeDirection`.
- `FPAnimationSystem.LateUpdate`: order is loco, air, ledge, swim, shake, legs, then leg blend for ledge and swim.
- Assumes controller members `climbDuration`, `swimSpeed`, `swimSprintSpeed`, `cameraShakeTimer`, `cameraShakeIntensity`, `cameraShakeDirection` are public.

## Stage 4: DONE (written, not yet tested in Unity)
- `Core/FPEvents` (PatStarted, PickpocketAttempt, Interact, HasListeners), `Core/FPCameraOffset` (apply-then-revert camera dip, order -100).
- `FPArm` gained `ikW` / `ikTarget` (chest-frame wrist target). `FPAnimationSystem.SolveArm` blends FK toward it and aims the hand.
- `Actions/`: `FPAction`, `FPActionLayer` (one action at a time, weak interact never interrupts), `FPPatAction`, `FPPickpocketAction` (success applies at 0.8, caught at 0.45), `FPInteractAction`, `FPItemInHand` (wallet prop).
- Hooks (copies in `ReplaceInOriginalLocations/`, these go back to your original locations): `AnimalAgent.Interact` raises pat, `PickpocketTarget.Interact` split into roll plus `Apply` (with `busy` guard), `InteractionSystem` raises generic interact.

### Next: Stage 5 (Polish). Old Stage 3 note follows.

### Old note: Stage 3 (Traversal and reactions)
`FPLedgeLayer`, `FPSwimLayer`, `FPCameraShakeReaction`. Plug in after `air.Apply(...)` in `FPAnimationSystem.LateUpdate`. Ledge uses `climbDuration`, swim uses `swimSpeed`/`swimSprintSpeed`, flinch uses `cameraShakeTimer`/`cameraShakeDirection`.

## Test checklist (Stage 2)
- Walk, sprint, crouch-walk: hands and feet stay in sync, no slipping.
- Strafe and look around: hands lag then settle.
- Jump, walk off a ledge, land softly and hard.
- Slide in and out: no pops at state changes.
- Debug line shows state and `air` correctly (no flicker on slopes).

## Test checklist (Stage 1)
- Hands stay stable while looking around, no wall clipping (overlay mode).
- Looking down shows torso and legs.
- Trigger an interview: rig keeps following the camera.
- Player capsule mesh is hidden. Player casts a shadow with no visible head.
