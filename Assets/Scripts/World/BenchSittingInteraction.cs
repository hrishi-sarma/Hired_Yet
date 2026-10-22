using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using EasyPeasyFirstPersonController;
using FPAnimation;

/// <summary>
/// Player sitting. P sits on a nearby bench / fallen log, or cross-legged on grass or dirt when no seat is near; Space or P stands.
/// While seated the controller is parked (its component is disabled, so no jump / fall / walk states can leak out), mouse look is
/// handled here, and the first-person rig is told through FPEvents so legs, torso and the guitar / feeding / inventory layers keep working.
/// Standing eases the view back to the pre-sit orientation before the controller is re-enabled, so it never snaps.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class BenchSittingInteraction : MonoBehaviour
{
    [SerializeField] float seatRange = 1.8f;
    [SerializeField] float sitTime = 0.7f, groundSitTime = 0.95f, standTime = 0.55f;
    [SerializeField] float seatLookLimit = 80f;
    [SerializeField] float groundHintDelay = 2.5f;

    enum Phase { None, Sitting, Seated, Standing }
    enum Mode { Seat, Ground }

    static BenchSittingInteraction instance;

    FirstPersonController fpc;
    CharacterController cc;
    Transform playerRoot, camT;
    FPAnimationSystem fp;
    float findTimer;

    Phase phase; Mode mode;
    float t;
    Vector3 startPos, seatPos, standPos, expectedPos;
    float startYaw, seatYaw, startPitch, lookYawOff, lookPitch, standFromYaw, standFromPitch, camDrop;
    Vector3 standCamPos; Quaternion standCamRot;

    SitSpot.Seat nearSeat; bool hasSeat; float seatTimer;
    bool groundOk; float groundTimer, idleTime;
    string toast; float toastUntil;
    readonly RaycastHit[] hits = new RaycastHit[12];
    readonly Collider[] overlaps = new Collider[16];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (instance != null) return;
        GameObject host = new GameObject("[Bench Sitting Interaction]");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<BenchSittingInteraction>();
    }

    void OnEnable() { FPEvents.EventStageChanged += OnEventStage; }
    void OnDisable() { FPEvents.EventStageChanged -= OnEventStage; }
    void OnDestroy() { if (instance == this) instance = null; HardReset(true); if (stack != null) WorldUtil.Kill(stack.gameObject); }

    // a world event that needs the player's body (greet, lift, push ...) takes over: stand up first
    void OnEventStage(EventStage stage, PlayerEventAnim anim, Transform target, float seconds)
    {
        if (anim != PlayerEventAnim.None && (phase == Phase.Seated || phase == Phase.Sitting)) BeginStand(true);
    }

    // ───────────────────────────── binding ─────────────────────────────

    bool Bind()
    {
        if (fpc != null && playerRoot != null && camT != null && cc != null) return true;
        if (phase != Phase.None) HardReset(false);          // player object was destroyed (scene change): just forget the old state
        fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc == null) return false;
        playerRoot = fpc.transform;
        cc = fpc.characterController != null ? fpc.characterController : fpc.GetComponent<CharacterController>();
        camT = fpc.playerCamera;
        return cc != null && camT != null;
    }

    void HardReset(bool restoreController)
    {
        if (phase != Phase.None && restoreController && fpc != null && playerRoot != null && camT != null)
        {
            camT.localPosition = standCamPos; camT.localRotation = standCamRot;
            playerRoot.rotation = Quaternion.Euler(0f, startYaw, 0f);
            fpc.enabled = true;
        }
        phase = Phase.None;
        FPEvents.Sitting = false; FPEvents.SitKind = 0; FPEvents.SitBlend = 0f;
    }

    // ───────────────────────────── gating ─────────────────────────────

    static bool Blocked()
    {
        if (Time.timeScale <= 0f || PauseMenu.Paused || DevConsole.Active || Items.InventoryUI.IsOpen || EventPanelUI.InputLock) return true;
        EventSystem es = EventSystem.current;
        GameObject sel = es != null ? es.currentSelectedGameObject : null;
        return sel != null && sel.GetComponent<TMP_InputField>() != null;
    }

    bool PlayerReady()
    {
        if (!fpc.isGrounded || fpc.isInWater) return false;
        if (fp == null) return true;
        FPLoco v = fp.Loco;
        return v.state == FPState.Grounded && !v.airborne;
    }

    // ───────────────────────────── update ─────────────────────────────

    void Update()
    {
        if (!Bind()) return;
        if (fp == null)
        {
            findTimer -= Time.unscaledDeltaTime;
            if (findTimer <= 0f) { findTimer = 0.5f; fp = FindAnyObjectByType<FPAnimationSystem>(); }
        }
        float dt = Time.deltaTime;
        var kb = Keyboard.current;

        switch (phase)
        {
            case Phase.None: StandingUpdate(kb, dt); break;
            case Phase.Sitting:
                if ((playerRoot.position - expectedPos).sqrMagnitude > 4f) { HardReset(true); break; }
                t += dt / (mode == Mode.Ground ? groundSitTime : sitTime);
                TickLook();
                if (t >= 1f) { t = 1f; phase = Phase.Seated; }
                ApplyPose(); expectedPos = playerRoot.position;
                break;
            case Phase.Seated:
                if ((playerRoot.position - expectedPos).sqrMagnitude > 4f) { HardReset(true); break; }   // teleported (dev console, respawn)
                TickLook();
                if (kb != null && !Blocked() && (kb.spaceKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) BeginStand(false);
                ApplyPose(); expectedPos = playerRoot.position;
                break;
            case Phase.Standing:
                t += dt / standTime;
                if (t >= 1f)
                {
                    t = 1f;
                    if (kb != null && kb.spaceKey.isPressed) { ApplyPose(); break; }   // wait for release so the controller never sees the key
                    FinishStand();
                    break;
                }
                ApplyPose();
                break;
        }
        UpdatePrompt();
    }

    void StandingUpdate(Keyboard kb, float dt)
    {
        hasSeat = false;
        if (kb == null) return;
        bool moving = playerRoot != null && cc != null && cc.velocity.sqrMagnitude > 0.04f;
        idleTime = moving ? 0f : idleTime + dt;

        seatTimer -= dt; groundTimer -= dt;
        if (seatTimer <= 0f) { seatTimer = 0.1f; RefreshSeat(); }
        if (groundTimer <= 0f) { groundTimer = 0.25f; string why; groundOk = GroundSurfaceOk(out why); }
        hasSeat = nearSeat.spot != null && nearSeat.spot.isActiveAndEnabled;

        if (Blocked()) return;
        if (!kb.pKey.wasPressedThisFrame) return;
        if (InterviewOpen() || !PlayerReady()) return;

        if (hasSeat) { BeginSit(Mode.Seat, nearSeat); return; }
        TryGroundSit();
    }

    static bool InterviewOpen() { return FindAnyObjectByType<InterviewUI>() != null; }

    // ───────────────────────────── seat search ─────────────────────────────

    void RefreshSeat()
    {
        nearSeat = default(SitSpot.Seat);
        if (playerRoot == null || !PlayerReady()) return;
        Vector3 from = playerRoot.position;
        float best = float.MaxValue;
        for (int i = 0; i < SitSpot.All.Count; i++)
        {
            SitSpot s = SitSpot.All[i];
            if (s == null) continue;
            SitSpot.Seat seat;
            if (!s.TrySeat(from, seatRange, out seat) || seat.sq >= best) continue;
            if (!SeatVisible(seat)) continue;
            best = seat.sq; nearSeat = seat;
        }
    }

    bool SeatVisible(SitSpot.Seat seat)
    {
        Vector3 a = camT.position, b = seat.pos + Vector3.up * Mathf.Max(0.3f, seat.topY - cc.bounds.min.y + 0.45f);
        Vector3 d = b - a; float len = d.magnitude;
        if (len < 0.05f) return true;
        int n = Physics.RaycastNonAlloc(a, d / len, hits, len, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (c == null || c.transform.IsChildOf(playerRoot) || hits[i].distance > len - 0.35f) continue;
            if (c.transform.IsChildOf(seat.spot.transform) || NameChain(c).Contains("bench") || c is TerrainCollider) continue;
            return false;
        }
        return true;
    }

    // ───────────────────────────── sit down ─────────────────────────────

    void TryGroundSit()
    {
        if (fp != null && fp.Loco.speed > 1.2f) { Toast("STAND STILL TO SIT"); return; }
        string why;
        if (!GroundSurfaceOk(out why)) { Toast(why); return; }
        SitSpot.Seat none = default(SitSpot.Seat);
        BeginSit(Mode.Ground, none);
    }

    void BeginSit(Mode m, SitSpot.Seat seat)
    {
        mode = m;
        startPos = playerRoot.position;
        startYaw = playerRoot.eulerAngles.y;
        standCamPos = camT.localPosition; standCamRot = camT.localRotation;
        startPitch = Mathf.DeltaAngle(0f, camT.localEulerAngles.x);
        lookPitch = startPitch; lookYawOff = 0f;

        float groundY = cc.bounds.min.y;
        float scale = Mathf.Max(0.1f, fpc.standingCameraHeight / 1.75f);
        float eyeNow = camT.position.y;
        float eyeTarget;
        if (m == Mode.Seat)
        {
            float h = Mathf.Clamp(seat.topY - groundY, 0.25f, 0.85f);
            FPEvents.SitSeatHeight = h;
            eyeTarget = groundY + h + 0.80f * scale;
            seatPos = new Vector3(seat.pos.x, startPos.y, seat.pos.z);
            seatYaw = seat.yaw;
            FPEvents.SitKind = 1;
        }
        else
        {
            eyeTarget = groundY + 0.92f * scale;
            seatPos = startPos; seatYaw = startYaw;
            FPEvents.SitKind = 2;
        }
        camDrop = Mathf.Max(0f, eyeNow - eyeTarget);

        fpc.enabled = false;                 // parks movement, jump, fall and the controller's own camera handling
        FPEvents.Sitting = true; FPEvents.SitBlend = 0f;
        phase = Phase.Sitting; t = 0f; expectedPos = startPos;
        idleTime = 0f;
    }

    // ───────────────────────────── stand up ─────────────────────────────

    void BeginStand(bool forced)
    {
        if (phase == Phase.None || phase == Phase.Standing) return;
        Vector3 fwd = Quaternion.Euler(0f, mode == Mode.Seat ? seatYaw : startYaw, 0f) * Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3[] cand = mode == Mode.Ground
            ? new[] { Vector3.zero, fwd * .5f, -fwd * .5f, right * .5f, -right * .5f }
            : new[] { fwd * .7f, fwd * 1.0f, right * .9f, -right * .9f, -fwd * .7f, Vector3.zero };
        bool found = false; Vector3 pick = seatPos;
        for (int i = 0; i < cand.Length; i++)
        {
            Vector3 q = seatPos + cand[i];
            if (!CapsuleFree(q)) continue;
            pick = q; found = true; break;
        }
        if (!found)
        {
            if (!forced) { Toast("NO ROOM TO STAND"); return; }
            pick = seatPos;
        }
        standPos = pick;
        standFromYaw = playerRoot.eulerAngles.y;
        standFromPitch = lookPitch;
        phase = Phase.Standing; t = 0f;
    }

    bool CapsuleFree(Vector3 pos)
    {
        float r = Mathf.Max(0.1f, cc.radius) * 0.9f;
        float h = Mathf.Max(r * 2f, cc.height);
        Vector3 c = pos + cc.center;
        Vector3 a = c + Vector3.up * (-h * 0.5f + r + 0.08f), b = c + Vector3.up * (h * 0.5f - r);
        int n = Physics.OverlapCapsuleNonAlloc(a, b, r, overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider col = overlaps[i];
            if (col == null || col.transform.IsChildOf(playerRoot)) continue;
            return false;
        }
        return true;
    }

    void FinishStand()
    {
        playerRoot.SetPositionAndRotation(standPos, Quaternion.Euler(0f, startYaw, 0f));
        camT.localPosition = standCamPos; camT.localRotation = standCamRot;
        cc.enabled = false; Physics.SyncTransforms(); cc.enabled = true;
        fpc.enabled = true;
        phase = Phase.None;
        FPEvents.Sitting = false; FPEvents.SitKind = 0; FPEvents.SitBlend = 0f;
        idleTime = 0f; seatTimer = 0f; groundTimer = 0f;
    }

    // ───────────────────────────── pose / look ─────────────────────────────

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    void TickLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked || Blocked()) return;
        if (fpc.input == null) return;
        Vector2 d = fpc.input.lookInput * fpc.mouseSensitivity;   // same input and sensitivity the controller itself uses
        lookPitch = Mathf.Clamp(lookPitch - d.y, -80f, 70f);
        lookYawOff += d.x;
        if (mode == Mode.Seat) lookYawOff = Mathf.Clamp(lookYawOff, -seatLookLimit, seatLookLimit);
    }

    void ApplyPose()
    {
        float e; Vector3 pos; float yaw, pitch;
        if (phase == Phase.Standing)
        {
            float k = Smooth(t); e = 1f - k;
            pos = Vector3.Lerp(seatPos, standPos, k);
            yaw = Mathf.LerpAngle(standFromYaw, startYaw, k);
            pitch = Mathf.Lerp(standFromPitch, startPitch, k);
        }
        else
        {
            float k = phase == Phase.Sitting ? Smooth(t) : 1f; e = k;
            pos = Vector3.Lerp(startPos, seatPos, k);
            yaw = Mathf.LerpAngle(startYaw, seatYaw, k) + lookYawOff;
            pitch = lookPitch;
        }
        playerRoot.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        camT.localPosition = new Vector3(standCamPos.x, standCamPos.y - camDrop * e, standCamPos.z);
        camT.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        FPEvents.SitBlend = e;
        if (phase != Phase.Seated) Physics.SyncTransforms();
    }

    // ───────────────────────────── ground surface ─────────────────────────────

    static string NameChain(Collider col)
    {
        string n = "";
        Transform tr = col.transform;
        for (int i = 0; i < 3 && tr != null; i++, tr = tr.parent) n += tr.name.ToLowerInvariant() + "|";
        Renderer r = col.GetComponent<Renderer>();
        if (r != null && r.sharedMaterial != null) n += r.sharedMaterial.name.ToLowerInvariant();
        return n;
    }

    static bool Has(string s, params string[] k)
    {
        for (int i = 0; i < k.Length; i++) if (s.Contains(k[i])) return true;
        return false;
    }

    /// <summary>True only on natural ground: terrain, grass, dirt, trail, gravel, sand, mud. Never on pavement, wood, metal, floors or water.</summary>
    bool GroundSurfaceOk(out string why)
    {
        why = "CAN'T SIT HERE. GRASS OR DIRT ONLY";
        if (cc == null || playerRoot == null) return false;
        if (fpc.isInWater) { why = "CAN'T SIT IN WATER"; return false; }
        Bounds b = cc.bounds;
        Vector3 o = new Vector3(b.center.x, b.min.y + 0.5f, b.center.z);
        int n = Physics.RaycastNonAlloc(o, Vector3.down, hits, 1.6f, ~0, QueryTriggerInteraction.Collide);
        float bd = float.MaxValue; RaycastHit best = default(RaycastHit); bool got = false, water = false;
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (c == null || c.transform.IsChildOf(playerRoot)) continue;
            if (c.isTrigger) { if (Has(NameChain(c), "water", "pond", "river")) water = true; continue; }
            if (hits[i].distance < bd) { bd = hits[i].distance; best = hits[i]; got = true; }
        }
        if (!got) return false;
        if (water) { why = "CAN'T SIT IN WATER"; return false; }
        if (best.normal.y < 0.88f) { why = "TOO STEEP TO SIT"; return false; }
        if (best.collider is TerrainCollider) return true;
        string nm = NameChain(best.collider);
        if (Has(nm, "road", "asphalt", "highway", "parking", "crosswalk", "sidewalk", "concrete", "plaza", "pavement", "curb", "stair", "metal", "steel", "grate",
                    "wood", "plank", "deck", "porch", "bridge", "pier", "water", "river", "pond", "puddle", "floor", "carpet", "office", "interview", "room", "rug", "tile", "roof"))
            return false;
        if (Has(nm, "grass", "lawn", "turf", "meadow", "lots", "ground", "dirt", "trail", "gravel", "sand", "mud", "soil", "terrain", "moss", "path")) return true;
        ZoneType z = AudioDirector.Zone;                           // unknown surface: natural zones only
        return z == ZoneType.Forest || z == ZoneType.Mountain || z == ZoneType.Open;
    }

    // ───────────────────────────── UI ─────────────────────────────

    void Toast(string s) { toast = s; toastUntil = Time.unscaledTime + 1.8f; }

    PromptStack stack; string shownPrompt = "";
    float calmT;   // seconds the "stand up" prompt has been on screen; it fades away after standHintHold so the view can be enjoyed
    const float StandHintHold = 10f, StandHintFade = 3f;

    // Same card UI as every other interaction prompt (PromptStack). Hidden whenever the game is paused or a menu / inventory is open.
    void UpdatePrompt()
    {
        string prompt = null;
        if (!Blocked())
        {
            InteractionSystem sys = InteractionSystem.Instance;
            bool busy = sys != null && (sys.HasTarget || !string.IsNullOrEmpty(sys.OverridePrompt));
            if (phase == Phase.Seated) { if (!busy) prompt = "[ PRESS P ] Stand up"; }
            else if (phase == Phase.None)
            {
                if (hasSeat && !busy && PlayerReady())
                    prompt = "[ PRESS P ] Sit on " + (nearSeat.spot.kind == SitSpot.Kind.Log ? "log" : "bench");
                else if (!busy && idleTime >= groundHintDelay && groundOk && PlayerReady() && !InterviewOpenCached())
                    prompt = "[ PRESS P ] Sit on the ground";
            }
            if (Time.unscaledTime < toastUntil && phase != Phase.Sitting && phase != Phase.Standing) prompt = toast;
        }
        if (string.IsNullOrEmpty(prompt))
        {
            if (phase != Phase.Seated) calmT = 0f;   // a stand-up prompt hidden by something else (menu, interaction) keeps its progress
            if (stack != null && shownPrompt.Length > 0) { stack.Hide(); stack.SetAlpha(1f); }
            shownPrompt = "";
            return;
        }
        if (stack == null)
        {
            InteractionSystem sys = InteractionSystem.Instance;
            stack = PromptStack.Create("SitPrompt", 100, sys != null ? sys.font : null, false, 90f);
        }
        shownPrompt = prompt;
        float alpha = 1f;
        if (phase == Phase.Seated && prompt == "[ PRESS P ] Stand up")
        {
            calmT += Time.unscaledDeltaTime;
            if (Items.GuitarPlayer.IsPlaying) calmT = Mathf.Max(calmT, StandHintHold);   // playing the guitar: start fading right away
            alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((calmT - StandHintHold) / StandHintFade));
        }
        else calmT = 0f;
        stack.SetAlpha(alpha);
        stack.Show(prompt);
    }

    bool interviewCached; float interviewCheck;
    bool InterviewOpenCached()
    {
        if (Time.unscaledTime >= interviewCheck) { interviewCheck = Time.unscaledTime + 0.5f; interviewCached = InterviewOpen(); }
        return interviewCached;
    }
}
