using System;
using UnityEngine;
namespace FPAnimation
{
    public enum HandDetail { Auto, High, Web }

    /// <summary>Every tunable number. Defaults work with zero setup. Edit live on the "FP Rig (Auto)" object.</summary>
    [Serializable]
    public class FPAnimSettings
    {
        [Header("Palette (customisation later only changes these)")]
        public Color skin = new Color(0.93f, 0.74f, 0.60f);
        public Color sleeve = new Color(0.20f, 0.35f, 0.55f);
        public Color trousers = new Color(0.22f, 0.22f, 0.27f);
        public Color shoes = new Color(0.12f, 0.10f, 0.10f);

        [Header("Rendering")]
        public bool useOverlayCamera = true;          // URP camera stacking. Falls back automatically if the layer is missing
        public string viewModelLayerName = "ViewModel";
        public float overlayFov = 70f;
        public float overlayNearClip = 0.01f;
        public float fallbackNearClip = 0.02f;        // single-camera mode
        public float wallRetractReach = 0.6f;         // single-camera mode: rig slides back near walls
        public bool castBodyShadow = true;            // ShadowsOnly proxy so the player casts a shadow
        public bool hideCapsuleRenderers = true;      // disables MeshRenderers under the player root
        public HandDetail handDetail = HandDetail.Auto;

        [Header("Arms (anatomical FK: shoulder flexion/abduction + elbow flexion, degrees)")]
        // Real shoulders sit ~0.30 m below the camera, so hands would never be on screen.
        // This anchor is raised ~0.20 m (standard viewmodel cheat) so the hands show in the lower corners.
        public Vector3 shoulderOffset = new Vector3(0.19f, -0.10f, 0.04f);   // right shoulder, camera space, left mirrored
        public float upperArm = 0.30f;
        public float foreArm = 0.27f;
        public float restFlex = 12f, restAbd = 10f, restElbow = 16f;     // relaxed stand: arms hang almost straight at the sides
        [Range(0f, 1f)] public float armPitchFollow = 0.7f;               // arms hang from the body, not the head: chest follows 70% of camera pitch
        [Range(0f, 1f)] public float armTiltFollow = 0.6f;

        [Header("Idle")]
        public float breathHz = 0.25f;
        public float breathAmp = 0.004f;
        public float restCurl = 0.35f;
        public float handSpringTime = 0.05f;

        [Header("Guitar")]
        public FPGuitarSettings guitar = new FPGuitarSettings();

        [Header("Locomotion")]
        public float strideWalk = 1.75f, strideSprint = 2.375f, strideCrouch = 1.125f; // metres per cycle (20% slower cycle than 1.4 / 1.9 / 0.9)
        // arm swing (degrees). Arms swing opposite to the legs. Sprint = ~90 degree elbows and big pumping swing.
        public float walkFlex = 2f, walkAmp = 30f, walkElbow = 26f, walkElbowAmp = 9f, walkAbd = 8f;
        public float sprintFlex = 22f, sprintAmp = 24f, sprintElbow = 75f, sprintElbowAmp = 8f, sprintAbd = 12f;
        public float legAmpWalk = 0.22f, legAmpSprint = 0.34f;                       // foot fore/aft swing
        public float sprintLean = 8f, slideLean = -12f;                              // torso pitch, degrees
        public float lookGain = 0.004f, lookMax = 0.06f, lookSmooth = 0.1f;          // hand look inertia
        public float landHardSpeed = 9f;                                             // fall speed that gives a full-strength landing
        public float groundGrace = 0.08f;                                            // isGrounded flicker filter
    }
}
