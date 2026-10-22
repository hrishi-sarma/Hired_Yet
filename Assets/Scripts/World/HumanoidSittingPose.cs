using UnityEngine;

/// <summary>
/// Automatically poses a humanoid character into a seated office pose.
/// Works by directly manipulating bone transforms each LateUpdate.
///
/// KEY GEOMETRY NOTE (OfficeCharactersPack rig):
///   The LeftUpperLeg bind-pose quaternion is (1, 0, 0, 0) = 180° around X.
///   When we set localRotation = Euler(+θ, 0, 0) on the thigh, the calf's local
///   "downward" direction in UpperLeg-space is at angle (180° - θ), NOT -θ.
///   So: thighAngle = 85° → calfAngle must be 180-85 = 95° to hang straight down.
/// </summary>
public class HumanoidSittingPose : MonoBehaviour
{
    [Header("Seat Adjustments")]
    [Tooltip("Target height for the character's hips off the ground (metres).")]
    public float seatHeight = 0.50f;

    [Header("Leg Pose")]
    [Tooltip("Euler X on the UpperLeg bone.  85° = thigh nearly horizontal pointing forward.")]
    [Range(60f, 110f)] public float thighForwardAngle = 85f;
    [Tooltip("Z-axis spread on the thighs. Increase if a skirt or pants mesh clips between the legs.")]
    [Range(0f, 20f)] public float legSpreadAngle = 8f;

    [Header("Arm Pose")]
    [Tooltip("Z-axis collapse (0 = T-pose horizontal, 90 = arm straight down).")]
    [Range(40f, 90f)] public float armDownAngle = 80f;
    [Tooltip("Y-axis bend at the elbow.  Positive = bend toward body midline.")]
    [Range(20f, 90f)] public float elbowBendAngle = 70f;
    [Tooltip("Small outward spread so arms don't clip the torso.")]
    [Range(-30f, 30f)] public float armSpreadAngle = 5f;

    Animator  _animator;
    Transform _hips;
    Transform _leftThigh,  _rightThigh;
    Transform _leftCalf,   _rightCalf;
    Transform _leftFoot,   _rightFoot;
    Transform _leftUpperArm,  _rightUpperArm;
    Transform _leftForearm,   _rightForearm;

    bool    _bonesFound;
    float   _standingHipsWorldY;  // World Y of hips when character is standing (measured at Awake)

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _animator = GetComponent<Animator>();
        FindBones();

        // Disable the Animator immediately — we drive bones manually.
        // Must happen AFTER FindBones so GetBoneTransform still works.
        if (_animator != null)
            _animator.enabled = false;
    }

    // ── Bone discovery ────────────────────────────────────────────────────────

    void FindBones()
    {
        // Primary path: Humanoid avatar mapping
        if (_animator != null && _animator.isHuman)
        {
            _hips         = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _leftThigh    = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _rightThigh   = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _leftCalf     = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _rightCalf    = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            _leftFoot     = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot    = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _rightUpperArm= _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _leftForearm  = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _rightForearm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }

        // Fallback: hierarchy name search (works for non-Humanoid or unmapped rigs)
        if (_hips == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLowerInvariant();
                if      (_hips         == null && (n == "hips" || n.Contains("pelvis"))) _hips = t;
                else if (_leftThigh    == null && n.Contains("left")  && n.Contains("upper") && n.Contains("leg")) _leftThigh = t;
                else if (_rightThigh   == null && n.Contains("right") && n.Contains("upper") && n.Contains("leg")) _rightThigh = t;
                else if (_leftCalf     == null && n.Contains("left")  && (n.Contains("lower") || n.Contains("calf") || n.Contains("shin")) && n.Contains("leg")) _leftCalf = t;
                else if (_rightCalf    == null && n.Contains("right") && (n.Contains("lower") || n.Contains("calf") || n.Contains("shin")) && n.Contains("leg")) _rightCalf = t;
                else if (_leftUpperArm == null && n.Contains("left")  && n.Contains("upper") && n.Contains("arm")) _leftUpperArm = t;
                else if (_rightUpperArm== null && n.Contains("right") && n.Contains("upper") && n.Contains("arm")) _rightUpperArm = t;
                else if (_leftForearm  == null && n.Contains("left")  && (n.Contains("lower") || n.Contains("fore")) && n.Contains("arm")) _leftForearm = t;
                else if (_rightForearm == null && n.Contains("right") && (n.Contains("lower") || n.Contains("fore")) && n.Contains("arm")) _rightForearm = t;
            }
        }

        if (_hips != null)
        {
            // Record the character's natural standing hip height in WORLD space.
            // We use world space throughout to sidestep Manager_Rig's -90° X coordinate flip,
            // which makes localPosition.y point sideways rather than up.
            _standingHipsWorldY = _hips.position.y;
            _bonesFound = true;
            Debug.Log($"[HumanoidSittingPose] Bones found on {gameObject.name}. " +
                      $"Hips={_hips.name}, standingHipsWorldY={_standingHipsWorldY:F3}, " +
                      $"LThigh={_leftThigh?.name}, LCalf={_leftCalf?.name}");
        }
        else
        {
            Debug.LogWarning($"[HumanoidSittingPose] Could not find Hips bone on {gameObject.name}!");
        }
    }

    // ── Pose every frame ──────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (!_bonesFound) return;
        ApplyPose();
    }

    void ApplyPose()
    {
        // ── 1. Hip height ──────────────────────────────────────────────────────
        // IMPORTANT: Use _hips.position (WORLD space), NOT localPosition.
        // Reason: The Hips bone is a child of Manager_Rig which has a -90° X rotation
        // baked into the FBX. In Manager_Rig's local space:
        //   local +Y  →  world -Z  (horizontal, not vertical!)
        //   local +Z  →  world +Y  (vertical)
        // So setting localPosition.y was moving the hips sideways, not up/down.
        // World-space position bypasses the entire bone chain and scale issues.
        if (_hips != null)
        {
            Vector3 wp = _hips.position;
            wp.y = seatHeight;            // Place hips exactly at the chair seat level
            _hips.position = wp;
        }

        // ── 2. Thighs ─────────────────────────────────────────────────────────
        // OfficeCharactersPack bind pose: UpperLeg has 180° X baked in.
        // Setting localRotation = Euler(+θ, 0, 0) rotates the bone's local +Y
        // to (0, cos θ, sin θ) in parent (Hips) space.
        // At θ = 85°: (0, 0.087, 0.996) → nearly horizontal, pointing forward (+Z). ✓
        // legSpreadAngle opens legs outward (Z-axis) to avoid skirt/pants mesh clipping.
        float tA = thighForwardAngle;
        float sp = legSpreadAngle;
        if (_leftThigh  != null) _leftThigh .localRotation = Quaternion.Euler(tA, 0f, -sp); // left spreads -Z
        if (_rightThigh != null) _rightThigh.localRotation = Quaternion.Euler(tA, 0f,  sp); // right spreads +Z

        // ── 3. Calves ─────────────────────────────────────────────────────────
        // In UpperLeg-local space (after rotating thigh by tA around X),
        // the world-down direction sits at angle (180° - tA), NOT at -tA.
        //
        // Proof: calf extends in UpperLeg-local (0, cos(calfA), sin(calfA)).
        //   World direction = cos(calfA)·ŷ_UL + sin(calfA)·ẑ_UL
        //   ŷ_UL in world = (0, cos(tA), sin(tA))
        //   ẑ_UL in world = (0, -sin(tA), cos(tA))
        //   For world direction = (0, -1, 0):
        //     cos(calfA)·cos(tA) + sin(calfA)·(-sin(tA)) = -1
        //     → cos(calfA - tA) = -1  → calfA - tA = 180°  → calfA = 180° - tA  ✓
        float calfA = 180f - tA;
        if (_leftCalf  != null) _leftCalf .localRotation = Quaternion.Euler(calfA, 0f, 0f);
        if (_rightCalf != null) _rightCalf.localRotation = Quaternion.Euler(calfA, 0f, 0f);

        // ── 3b. Feet — tuck toes slightly downward so they don't poke backward ─
        // LeftFoot/RightFoot bind pose has -24° X (0.9135 w, -0.4068 x) already baked.
        // A small extra +X nudge points toes further toward the floor.
        if (_leftFoot  != null) _leftFoot .localRotation = Quaternion.Euler( 20f, 0f, 0f);
        if (_rightFoot != null) _rightFoot.localRotation = Quaternion.Euler( 20f, 0f, 0f);

        // ── 4. Upper Arms ─────────────────────────────────────────────────────
        // Z-rotation pulls arm down from T-pose; small X-rotation tilts forward.
        if (_leftUpperArm  != null) _leftUpperArm .localRotation = Quaternion.Euler(15f, -armSpreadAngle,  armDownAngle);
        if (_rightUpperArm != null) _rightUpperArm.localRotation = Quaternion.Euler(15f,  armSpreadAngle, -armDownAngle);

        // ── 5. Forearms ───────────────────────────────────────────────────────
        // Y-rotation bends elbow inward toward body so hands rest on the desk/lap.
        if (_leftForearm  != null) _leftForearm .localRotation = Quaternion.Euler(0f,  elbowBendAngle, 0f);
        if (_rightForearm != null) _rightForearm.localRotation = Quaternion.Euler(0f, -elbowBendAngle, 0f);
    }
}
