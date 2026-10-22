using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using FPAnimation;

public class WorldEvent : MonoBehaviour, IInteractable
{
    enum Phase { Idle, Intro, Task, Result, Done }

    public WorldEventConfig cfg;
    public Transform npc;
    public EventNpc npcCtl;

    Phase phase = Phase.Idle;
    int attemptsLeft = 2;
    float phaseEnd;
    IEventHandler handler;
    EventAudio audioCtl;
    bool success;
    string lastShown = "";
    bool registered;
    float tapRecent, patEnd, propM;
    Transform prop;
    Vector3 propStart;
    bool propInit, pushProp;
    EventPropMotion motion;
    bool inPos;
    float offTime;
    bool delivery, walkOn, feed, standalone;
    int couchSign = 1;
    DeliveryHandler dh;
    GameObject marker;
    int npcState;               // 0 none, 1 following the player, 2 walking to the drop spot, 3 settled there
    Vector3 npcGoal;
    public bool FastDrop { get; private set; }
    public WalletSetup wallet;
    string evKey = "";                  // stable id of this event in the (seeded, therefore identical) world, used for the save file
    bool restoredDone, snapProp;

    static string Fm(float v) { return v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture); }
    static float Pf(string s) { float v; float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v); return v; }

    // saved game: this event was already finished in an earlier session, so it comes back finished (no prompt, props where they were left)
    void RestoreDone(string st)
    {
        string[] p = st.Split('|');
        success = p.Length > 0 && p[0] == "1";
        phase = Phase.Done; attemptsLeft = 0; restoredDone = true; snapProp = true;
        if (delivery && p.Length >= 8)
        {
            prop = transform.Find("Prop");
            Vector3 pp = new Vector3(Pf(p[1]), Pf(p[2]), Pf(p[3]));
            transform.position = pp;
            if (prop != null) { prop.position = pp; prop.rotation = Quaternion.Euler(0f, Pf(p[4]), 0f); }
            if (npc != null) npc.position = new Vector3(Pf(p[5]), Pf(p[6]), Pf(p[7]));
        }
    }

    void RecordDone()
    {
        string st = success ? "1" : "0";
        if (delivery)
        {
            Transform pr = prop != null ? prop : transform.Find("Prop");
            if (pr != null)
            {
                Vector3 n = npcState != 0 ? npcGoal : (npc != null ? npc.position : pr.position);
                st += "|" + Fm(pr.position.x) + "|" + Fm(pr.position.y) + "|" + Fm(pr.position.z) + "|" + Fm(pr.eulerAngles.y) + "|" + Fm(n.x) + "|" + Fm(n.y) + "|" + Fm(n.z);
            }
        }
        SaveSystem.NoteEventDone(evKey, st);
    }

    /// <summary>True while the prop should be in the player's hands (delivery events: only once lifted, until E is released).</summary>
    public bool Holding
    {
        get { return phase == Phase.Task && (!delivery || (dh != null && dh.Carrying)); }
    }

    public bool InPosition { get { return inPos; } }
    public PlantsHandler Plants { get; private set; }

    EventPropMotion Mot()
    {
        if (motion == null)
        {
            Transform pr = prop != null ? prop : transform.Find("Prop");
            if (pr != null) motion = pr.GetComponent<EventPropMotion>();
        }
        return motion;
    }

    bool IsPush() { return EventPlacement.TravelFor(cfg) > 0.01f; }

    string PushName()
    {
        switch (cfg.id)
        {
            case "stalled_car": return "CAR";
            case "runaway_cart": return "CART";
            case "fallen_log": return "LOG";
        }
        return "BOULDER";
    }

    // Push events: behind the object, facing the way it moves. Everything else: facing the prop (or NPC if there is none).
    bool PositionOk(Transform pl)
    {
        Transform pr = prop != null ? prop : transform.Find("Prop");
        Vector3 fwd = pl.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) return true;
        fwd.Normalize();
        if (cfg.id == "thirsty_plants" || cfg.id == "pigeon_king") return true;      // the handler checks which tub the player stands at
        if (cfg.id == "garage_sale_couch" && pr != null && Mot() != null)
        {
            // stand against one end of the couch (touching distance) and face it; the end nearest the player is the one raised
            EventPropMotion mo = Mot();
            Vector3 rc = pl.position - pr.position;
            rc.y = 0f;
            float along = Vector3.Dot(rc, pr.forward);
            if (phase != Phase.Task) couchSign = along >= 0f ? 1 : -1;
            float beyond = couchSign * along - mo.Length * 0.5f;
            Vector3 toC = -rc;
            return mo.Gap(pl.position) < 1.0f && beyond > -0.4f && (toC.sqrMagnitude < 0.04f || Vector3.Dot(fwd, toC.normalized) > 0.6f);
        }
        if (cfg.id == "hiker_ankle" && npc != null)
        {
            // the task is on the hiker's leg, not the pack: accept any view that has the legs in it
            Camera cam = Camera.main;
            Vector3 ankle = npc.position + Vector3.up * 0.3f;
            if (cam != null)
            {
                Vector3 tc = ankle - cam.transform.position;
                if (tc.sqrMagnitude > 0.01f && Vector3.Angle(cam.transform.forward, tc) < 50f) return true;
            }
            Vector3 rn = npc.position - pl.position;
            rn.y = 0f;
            return rn.sqrMagnitude < 0.25f || Vector3.Dot(fwd, rn.normalized) > 0.35f;
        }
        Vector3 rel = pl.position - (pr != null ? pr.position : transform.position);
        rel.y = 0f;
        if (pr != null && IsPush())
        {
            Vector3 pf = pr.forward;
            pf.y = 0f;
            pf.Normalize();
            float along = Vector3.Dot(rel, pf), lat = Mathf.Abs(Vector3.Dot(rel, pr.right));
            float rear = -cfg.propSize.z * 0.5f;
            return along < rear + 0.3f && along > rear - 2.4f && lat < cfg.propSize.x * 0.5f + 0.35f && Vector3.Dot(fwd, pf) > 0.75f;
        }
        if (rel.sqrMagnitude < 0.25f) return true;
        return Vector3.Dot(fwd, -rel.normalized) > 0.5f;
    }

    public bool Done
    {
        get { return phase == Phase.Done; }
    }

    public bool Working { get { return phase == Phase.Task; } }
    public bool Finished { get { return phase == Phase.Result || phase == Phase.Done; } }
    public float Progress { get { return handler != null ? handler.Progress : 0f; } }

    public bool Succeeded
    {
        get { return success; }
    }

    public Vector3 Position
    {
        get { return transform.position; }
    }

    public float Range
    {
        get
        {
            NpcDirector d = NpcDirector.Instance;
            return d != null ? d.settings.events.interactRange : 2.6f;
        }
    }

    public string Prompt
    {
        get { return "[ PRESS E ] " + cfg.prompt; }
    }

    public bool CanInteract
    {
        get
        {
            InteractionSystem sys = InteractionSystem.Instance;
            return phase == Phase.Idle && attemptsLeft > 0 && sys != null && !sys.Suppressed;
        }
    }

    public void Init(WorldEventConfig c, int attempts)
    {
        cfg = Patch(c);
        c = cfg;
        feed = c.id == "pigeon_king";
        standalone = c.id == "lost_backpack" || c.id == "lost_wallet";   // no NPC: loose objects that spawn on their own
        delivery = c.id == "grandma_groceries" || c.id == "delivery_boxes" || c.id == "fallen_log";
        attemptsLeft = attempts;
        evKey = c.id + "@" + Mathf.RoundToInt(transform.position.x) + "," + Mathf.RoundToInt(transform.position.z);
        string savedState = SaveSystem.EventState(evKey);
        if (savedState != null) RestoreDone(savedState);
        audioCtl = gameObject.AddComponent<EventAudio>();
        audioCtl.Init(c.soundVolume);
        if (InteractionSystem.Instance != null)
        {
            InteractionSystem.Instance.Register(this);
            registered = true;
        }
    }

    void OnDestroy()
    {
        if (phase == Phase.Task) StopPlayerAnim();
        RemoveMarker();
        EventPanelUI.Hide();
        Clear();
        if (registered && InteractionSystem.Instance != null) InteractionSystem.Instance.Unregister(this);
    }

    public void Interact()
    {
        if (!CanInteract) return;
        NpcDirector d = NpcDirector.Instance;
        phase = Phase.Intro;
        phaseEnd = Time.time + (standalone ? 0.4f : d.settings.events.introSeconds);
        Show(standalone ? "" : feed ? cfg.ask : cfg.npcLabel + ": \"" + cfg.ask + "\"");
        if (npcCtl != null) npcCtl.Play(EventStage.Before);
        if (audioCtl != null) audioCtl.PlayBefore(cfg.id, cfg.soundBefore);
        PlayerAnim(EventStage.Before, cfg.playerBefore, 1.2f);
    }

    // runtime text/behaviour overrides so older saved config assets pick up the new cart and log events
    static WorldEventConfig Patch(WorldEventConfig c)
    {
        if (c.id == "runaway_cart")
        {
            c = Instantiate(c);
            c.prompt = "PUSH THE CART";
            c.ask = "My cart is stuck. Can you help me push it?";
            c.fail = "It will not budge. I will call the store.";
        }
        else if (c.id == "lost_wallet" || c.id == "lost_backpack")
        {
            c = Instantiate(c);
            c.prompt = c.id == "lost_wallet" ? "OPEN THE WALLET" : "OPEN THE BACKPACK";
        }
        else if (c.id == "pigeon_king")
        {
            c = Instantiate(c);
            c.prompt = "CALM THE BIRDS";
            c.ask = "These birds are agitated and need to be calmed down. Feed them some breadcrumbs!";
            c.success = "The flock has calmed down. Here, a little thank you.";
            c.fail = "The pigeons scattered before they could eat.";
            c.playerBefore = PlayerEventAnim.None;
            c.playerAfter = PlayerEventAnim.None;
        }
        else if (c.id == "fallen_log")
        {
            c = Instantiate(c);
            c.prompt = "DRAG THE LOG";
            c.ask = "This log blocks the trail. Grab an end and help me drag it to the marker!";
            c.travelDistance = 0f;
        }
        return c;
    }

    PlayerEventAnim DuringAnim()
    {
        if (cfg.id == "lost_dog") return PlayerEventAnim.PatDog;
        if (cfg.handler == EventHandlerKind.Sequence) return PlayerEventAnim.Arrows;
        switch (cfg.id)
        {
            case "lawnmower": return PlayerEventAnim.Repair;
            case "lemonade_stand": return PlayerEventAnim.Squeeze;
            case "injured_deer": return PlayerEventAnim.Mend;
            case "beekeeper": return PlayerEventAnim.Pump;
            case "mushroom_sorting":
            case "flying_documents": return PlayerEventAnim.Sort;
            case "hermit_riddle": return PlayerEventAnim.Ponder;
            case "food_truck": return PlayerEventAnim.Chop;
            case "lost_backpack": return PlayerEventAnim.Tinker;
            case "lost_wallet": return PlayerEventAnim.Fidget;
            case "hiker_ankle": return PlayerEventAnim.Bandage;
            case "garage_sale_couch": return PlayerEventAnim.LiftCrouch;
            case "fallen_log": return PlayerEventAnim.DragLog;
        }
        return cfg.playerDuring;
    }

    // low objects the player has to crouch to work on (others keep a standing stance)
    bool CrouchTask()
    {
        switch (cfg.id)
        {
            case "lawnmower": case "thirsty_plants": case "sprinkler_valve": case "injured_deer":
            case "mushroom_sorting": case "lost_backpack": case "lost_dog": case "hiker_ankle": return true;
        }
        return false;
    }

    float PropHeight()
    {
        if (prop == null) return cfg.propSize.y;
        Renderer[] rs = prop.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return cfg.propSize.y;
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return Mathf.Max(0.1f, b.size.y);
    }

    void PlayerAnim(EventStage stage, PlayerEventAnim anim, float secs)
    {
        Transform target = npc != null ? npc : transform;
        bool hiker = cfg.id == "hiker_ankle";
        FPEvents.EventFocusHeight = stage == EventStage.During && CrouchTask() ? (hiker ? 0.5f : PropHeight()) : 0f;
        if (prop != null && !hiker && (anim == PlayerEventAnim.Push || anim == PlayerEventAnim.PatDog || FPEvents.EventFocusHeight > 0f)) target = prop;
        if (anim == PlayerEventAnim.PatDog) patEnd = Time.time + secs - 0.2f;
        FPEvents.RaiseEventStage(stage, anim, target, secs);
    }

    void MoveProp(float dt)
    {
        if (!propInit)
        {
            propInit = true;
            prop = transform.Find("Prop");
            if (prop != null) { propStart = prop.position; motion = prop.GetComponent<EventPropMotion>(); }
            pushProp = prop != null && EventPlacement.TravelFor(cfg) > 0.01f &&
                       (cfg.playerDuring == PlayerEventAnim.Push || cfg.id == "stalled_car" || cfg.id == "boulder" || cfg.id == "fallen_log");
        }
        if (!pushProp || (restoredDone && delivery)) return;
        float travel = EventPlacement.TravelFor(cfg), goal = propM;
        if (phase == Phase.Task && handler != null) goal = Mathf.Min(0.6f, travel) * handler.Progress;   // creeps while the player is still bracing
        else if (phase == Phase.Result || phase == Phase.Done) goal = success ? travel : 0f;           // slides the rest of the way on success
        else if (phase == Phase.Idle) goal = 0f;
        if (snapProp) { propM = goal; snapProp = false; }     // restored finished event: prop starts at its end position
        if (motion != null && motion.Blocked) dt = 0f;     // player stands in front of the prop
        propM = Mathf.MoveTowards(propM, goal, (success && phase != Phase.Task ? 1.2f : 0.8f) * dt);
        prop.position = propStart + prop.forward * propM;
    }

    void StopPlayerAnim()
    {
        FPEvents.EventIntensity = 0f;
        FPEvents.EventProgress = 0f;
        FPEvents.EventFocusHeight = 0f;
        FPEvents.EventSqueeze = 0f;
        FPEvents.EventLift = 0f;
        FPEvents.EventCarry = 1f;
        FPEvents.RaiseEventStage(EventStage.During, PlayerEventAnim.None, null, 0f);
    }

    void FeedPlayerAnim(float dt, Keyboard kb)
    {
        if (kb == null) return;
        bool tap = kb.eKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame ||
                   kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
        if (tap) { FPEvents.KickEvent(); tapRecent = 0.6f; }
        Key[] arrows = { Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow };
        for (int k = 0; k < 4; k++)
        {
            if (!kb[arrows[k]].wasPressedThisFrame) continue;
            SequenceHandler sh = handler as SequenceHandler;
            FPEvents.EventArrow = k;
            FPEvents.EventArrowOk = sh == null || sh.LastOk;
            FPEvents.EventArrowCount++;
            break;
        }
        if (handler != null) FPEvents.EventProgress = handler.Progress;
        tapRecent = Mathf.Max(0f, tapRecent - dt);
        float want = (kb.eKey.isPressed || tapRecent > 0f) ? 1f : 0.2f;
        FPEvents.EventIntensity = Mathf.MoveTowards(FPEvents.EventIntensity, want, dt * 3f);
    }

    void Show(string text)
    {
        if (InteractionSystem.Instance == null) return;
        InteractionSystem.Instance.OverridePrompt = text;
        lastShown = text;
    }

    void Clear()
    {
        if (InteractionSystem.Instance != null && InteractionSystem.Instance.OverridePrompt == lastShown) InteractionSystem.Instance.OverridePrompt = "";
        lastShown = "";
    }

    void BackToIdle()
    {
        if (wallet != null) wallet.ShowPlates(false);
        Clear();
        RemoveMarker();
        EventPanelUI.Hide();
        if (phase == Phase.Task) StopPlayerAnim();
        phase = Phase.Idle;
        if (npcCtl != null) npcCtl.PlayIdle();
        if (audioCtl != null) audioCtl.Stop();
    }

    bool Cancelled(Vector3 diff)
    {
        InteractionSystem sys = InteractionSystem.Instance;
        return diff.magnitude > Range + 2.5f || sys == null || sys.Suppressed;
    }

    void Update()
    {
        NpcDirector d = NpcDirector.Instance;
        if (d == null || d.Player == null || cfg == null) return;
        Vector3 diff = d.Player.position - transform.position;
        diff.y = 0f;
        if (npc != null && phase != Phase.Done && (npcState == 0 || npcState == 3) && diff.sqrMagnitude < 400f && diff.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(diff);
            npc.rotation = Quaternion.RotateTowards(npc.rotation, look, 240f * Time.deltaTime);
        }
        inPos = PositionOk(d.Player);
        MoveProp(Time.deltaTime);
        if (delivery) { UpdateNpcMove(d.Player, Time.deltaTime); UpdateArrow(d); if (phase == Phase.Task && dh != null && cfg.id != "fallen_log") FPEvents.EventCarry = dh.Carrying ? 1f : 0f; }
        switch (phase)
        {
            case Phase.Intro: UpdateIntro(diff); break;
            case Phase.Task: UpdateTask(diff); break;
            case Phase.Result: UpdateResult(); break;
        }
    }

    void UpdateIntro(Vector3 diff)
    {
        if (Cancelled(diff))
        {
            BackToIdle();
            return;
        }
        if (Time.time >= phaseEnd) BeginTask();
    }

    void BeginTask()
    {
        phase = Phase.Task;
        dh = delivery ? new DeliveryHandler() : null;
        if (delivery && cfg.id != "fallen_log") FPEvents.EventCarry = 0f;   // groceries and boxes: crouch to pick them up
        handler = delivery ? (IEventHandler)dh : EventUi.Create(cfg.handler);
        OpenHandler oh = handler as OpenHandler;
        if (oh != null && cfg.id == "lost_wallet") oh.label = "OPEN THE WALLET";
        if (feed)
        {
            Transform pr1 = prop != null ? prop : transform.Find("Prop");
            handler = new FeedHandler { flock = pr1 != null ? pr1.GetComponentInChildren<PigeonFlock>() : null };
        }
        if (cfg.id == "thirsty_plants")
        {
            List<Transform> tubs = new List<Transform>();
            Transform pr0 = prop != null ? prop : transform.Find("Prop");
            if (pr0 != null) foreach (Transform t in pr0.GetComponentsInChildren<Transform>()) if (t.name == "Pot") tubs.Add(t);
            Plants = new PlantsHandler { player = NpcDirector.Instance.Player, tubs = tubs.ToArray() };
            handler = Plants;
        }
        FastDrop = false;
        if (handler == null)
        {
            Finish(false);
            return;
        }
        handler.Begin(cfg, NpcDirector.Instance.Rng);
        if (delivery) SetupDelivery(NpcDirector.Instance);
        if (npcCtl != null && !delivery) npcCtl.Play(EventStage.During);
        if (audioCtl != null) audioCtl.Play(cfg.soundDuring, true);
        tapRecent = 0f;
        FPEvents.EventIntensity = 0.2f;
        if (!feed) PlayerAnim(EventStage.During, DuringAnim(), DuringAnim() == PlayerEventAnim.PatDog ? 5f : 0f);   // feeding: the flock's own throw animation runs instead
    }

    void UpdateTask(Vector3 diff)
    {
        if (delivery && dh != null && dh.Carrying)
        {
            // walking with the items: no range or facing checks, the player only has to keep E held until the drop-off
            InteractionSystem isys = InteractionSystem.Instance;
            if (isys == null || isys.Suppressed) dh.Abort();
            dh.canPick = true;
            EventStatus ds = handler.Tick(Time.deltaTime, Keyboard.current);
            FeedPlayerAnim(Time.deltaTime, Keyboard.current);
            Show(handler.Text);
            if (ds == EventStatus.Success) Finish(true);
            else if (ds == EventStatus.Fail) Finish(false);
            return;
        }
        if (Cancelled(diff))
        {
            BackToIdle();
            return;
        }
        if (!inPos)
        {
            // wrong spot (beside the car, back turned...): no input counts, no progress, effort drops
            offTime += Time.deltaTime;
            FPEvents.EventIntensity = Mathf.MoveTowards(FPEvents.EventIntensity, 0.1f, Time.deltaTime * 3f);
            Show(IsPush() ? "[ GET BEHIND THE " + PushName() + " AND FACE THE WAY IT MOVES ]" : (cfg.id == "garage_sale_couch" ? "[ STAND AT THE COUCH CORNER AND FACE IT ]" : "[ FACE THE TASK ]"));
            if (offTime > 12f) BackToIdle();
            return;
        }
        offTime = 0f;
        if (dh != null) dh.canPick = cfg.id != "fallen_log" || (Mot() != null && Mot().Gap(NpcDirector.Instance.Player.position) <= 1.0f);
        HoldHandler hh = handler as HoldHandler;
        EventPropMotion mot = Mot();
        bool couch = cfg.id == "garage_sale_couch" && mot != null;
        if (hh != null) hh.Pause = couch && (FPEvents.EventLift < 0.8f || mot.Contact < 0.6f);
        EventStatus status = handler.Tick(Time.deltaTime, Keyboard.current);
        FeedPlayerAnim(Time.deltaTime, Keyboard.current);
        if (DuringAnim() == PlayerEventAnim.PatDog && Time.time > patEnd) PlayerAnim(EventStage.During, PlayerEventAnim.PatDog, 5f);
        Show(couch && mot.Contact < 0.5f ? "[ STEP CLOSER AND TAKE HOLD OF THE COUCH CORNER ]" : handler.Text);
        if (status == EventStatus.Success) Finish(true);
        else if (status == EventStatus.Fail) Finish(false);
    }

    void Finish(bool ok)
    {
        if (wallet != null) wallet.ShowPlates(false);
        NpcDirector d = NpcDirector.Instance;
        success = ok;
        string text;
        if (ok)
        {
            Items.ItemInstance item = null;
            float chance = Mathf.Clamp01(cfg.reward * d.settings.events.itemChanceScale);
            if (d.Rng.NextDouble() < chance) item = Items.ItemPickups.RollForEvent(cfg.id, d.Rng);
            bool plain = feed || standalone;
            if (item != null)
            {
                string good = standalone ? "You found something useful." : cfg.success;
                text = (plain ? good : cfg.npcLabel + ": \"" + good + "\"") + "  [ + " + item.def.name.ToUpperInvariant() + " ]";
                Items.ItemPickups.Offer(item);
            }
            else
            {
                string none = EventNothing.Pick(cfg.id, d.Rng);   // nothing to give: context-specific line
                text = plain ? none : cfg.npcLabel + ": \"" + none + "\"";
            }
        }
        else
        {
            attemptsLeft--;
            text = feed ? cfg.fail : cfg.npcLabel + ": \"" + cfg.fail + "\"";
        }
        Show(text);
        phase = Phase.Result;
        phaseEnd = Time.time + 3.5f;
        if (npcCtl != null && !delivery) npcCtl.Play(EventStage.After);
        if (audioCtl != null) audioCtl.Play(cfg.soundAfter, false);
        if (delivery) PlaceItems(ok, d);
        EventPanelUI.Hide();
        StopPlayerAnim();
        PlayerAnim(EventStage.After, cfg.playerAfter, 1.2f);
    }

    void UpdateResult()
    {
        if (Time.time < phaseEnd) return;
        Clear();
        phase = (success || attemptsLeft <= 0) ? Phase.Done : Phase.Idle;
        if (phase == Phase.Done) RecordDone();
        if (npcCtl != null && (!delivery || npcState == 0)) npcCtl.PlayIdle();
    }

    // ---------- delivery (groceries, boxes) ----------
    void SetupDelivery(NpcDirector d)
    {
        Vector3 from = (prop != null ? prop : transform).position;
        float radius;
        Vector3 dest = PickDropSpot(d, from, out radius);
        dh.player = d.Player;
        if (cfg.id == "fallen_log")
        {
            dh.liftText = "HOLD E TO GRAB THE LOG END  ";
            dh.carryText = "KEEP HOLDING E  -  DRAG THE LOG TO THE MARKER  ";
        }
        dh.dest = dest;
        dh.radius = radius;
        Vector3 dd = d.Player.position - dest;
        dd.y = 0f;
        dh.startDist = Mathf.Max(8f, dd.magnitude);
        RemoveMarker();
        marker = new GameObject("DropMarker");
        Color c = new Color(1f, 0.85f, 0.25f);
        MakeMarkerPart(marker.transform, PrimitiveType.Cylinder, new Vector3(radius * 2f, 0.015f, radius * 2f), new Vector3(0f, 0.04f, 0f), c);
        MakeMarkerPart(marker.transform, PrimitiveType.Cylinder, new Vector3(0.16f, 2.5f, 0.16f), new Vector3(0f, 2.5f, 0f), c);
        MakeMarkerPart(marker.transform, PrimitiveType.Sphere, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0f, 5.2f, 0f), c);
        marker.transform.position = dest;
    }

    // 2D arrow at the top middle of the screen (up = straight ahead) that points toward the drop-off marker
    void UpdateArrow(NpcDirector d)
    {
        Camera cam = Camera.main;
        if (cam == null || dh == null) return;
        bool show = phase == Phase.Task && dh.Carrying && dh.Dist() > dh.radius;
        if (!show) { EventHud.Clear("delivery"); return; }
        Vector3 f = cam.transform.forward, to = dh.dest - d.Player.position;
        f.y = 0f; to.y = 0f;
        if (f.sqrMagnitude < 1e-4f || to.sqrMagnitude < 1e-4f) return;
        EventHud.Set("delivery", true, Vector3.SignedAngle(f, to, Vector3.up), Mathf.CeilToInt(dh.Dist()) + " m", new Color(1f, 0.85f, 0.25f));
    }

    static void MakeMarkerPart(Transform parent, PrimitiveType type, Vector3 scale, Vector3 localPos, Color c, float rollZ = 0f)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Collider k = g.GetComponent<Collider>();
        if (k != null) WorldUtil.Kill(k);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        g.transform.localRotation = Quaternion.Euler(0f, 0f, rollZ);
        Renderer r = g.GetComponent<Renderer>();
        if (r != null) { r.sharedMaterial = MaterialFactory.Get(c); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
    }

    void RemoveMarker()
    {
        if (marker != null) WorldUtil.Kill(marker);
        marker = null;
        EventHud.Clear("delivery");
    }

    // another building's door, a good walk away. City: the sidewalk beside the door. Suburb doors cannot be reached (big colliders), so the front of the house counts, with a wider zone.
    Vector3 PickDropSpot(NpcDirector d, Vector3 from, out float radius)
    {
        if (cfg.zone == EventZone.Forest)
        {
            radius = 2.6f;
            WorldContext ctx = d.Ctx;
            for (int i = 0; i < 60 && ctx != null; i++)
            {
                float a = (float)d.Rng.NextDouble() * Mathf.PI * 2f, dist = 16f + (float)d.Rng.NextDouble() * 18f;
                float x = from.x + Mathf.Sin(a) * dist, z = from.z + Mathf.Cos(a) * dist;
                if (ctx.zoneMap.ZoneAt(x, z) != ZoneType.Forest || ctx.NormalAt(x, z).y < 0.92f) continue;
                return new Vector3(x, d.GroundY(x, z, from.y + 12f), z);
            }
            Vector3 fd = transform.forward;
            fd.y = 0f;
            Vector3 fb = from + fd.normalized * 16f;
            fb.y = d.GroundY(fb.x, fb.z, from.y + 12f);
            return fb;
        }
        bool city = cfg.zone == EventZone.City;
        ZoneType zt = city ? ZoneType.City : ZoneType.Suburb;
        radius = city ? 2.2f : 4.2f;
        float minD = city ? 22f : 30f, maxD = 75f;
        List<DoorPoint> ok = new List<DoorPoint>(), far = new List<DoorPoint>();
        PedestrianGraph g = d.PedGraph;
        if (g != null)
        {
            for (int i = 0; i < g.Doors.Count; i++)
            {
                DoorPoint dp = g.Doors[i];
                if (dp.block == null || dp.block.zone != zt) continue;
                float dist = (new Vector2(from.x, from.z) - dp.position).magnitude;
                if (dist >= minD && dist <= maxD) ok.Add(dp);
                else if (dist >= 14f && dist <= 140f) far.Add(dp);
            }
        }
        List<DoorPoint> pool = ok.Count > 0 ? ok : far;
        Vector3 res;
        if (pool.Count > 0)
        {
            DoorPoint pick = pool[d.Rng.Next(pool.Count)];
            res = new Vector3(pick.position.x, 0f, pick.position.y);
        }
        else
        {
            Vector3 dir = transform.forward;
            dir.y = 0f;
            res = from + dir.normalized * minD;
        }
        res.y = d.GroundY(res.x, res.z, from.y + 6f);
        return res;
    }

    // items go on the ground just in front of the player (set down at the door, or dropped where E was let go)
    void PlaceItems(bool ok, NpcDirector d)
    {
        RemoveMarker();
        FastDrop = !ok;
        Transform pl = d.Player;
        Vector3 f = pl.forward;
        f.y = 0f;
        f = f.sqrMagnitude > 0.001f ? f.normalized : transform.forward;
        bool drag = cfg.id == "fallen_log" && prop != null;
        Vector3 at = drag ? prop.position : pl.position + f * 0.9f;
        if (!drag) at.y = d.GroundY(at.x, at.z, pl.position.y + 1f);
        Vector3 pPos = prop != null ? prop.position : Vector3.zero;
        Quaternion pRot = prop != null ? prop.rotation : Quaternion.identity;
        Vector3 np = npc != null ? npc.position : Vector3.zero;
        Quaternion nr = npc != null ? npc.rotation : Quaternion.identity;
        transform.position = at;              // the event follows its items so retry range and facing checks still work
        if (npc != null) { npc.position = np; npc.rotation = nr; }
        if (prop != null)
        {
            if (drag) { prop.position = pPos; prop.rotation = pRot; }   // the dragged log stays exactly where it was set down
            else { prop.position = at; prop.rotation = Quaternion.Euler(0f, pl.eulerAngles.y + 180f, 0f); }
        }
        npcGoal = drag ? at + prop.right * 1.5f : at + pl.right * 1.4f - f * 0.2f;       // the NPC walks over and stays beside the items
        npcGoal.y = at.y;
        npcState = 2;
    }

    // the NPC walks along beside the player while the items are carried, then settles next to them
    void UpdateNpcMove(Transform pl, float dt)
    {
        if (npc == null) return;
        NpcDirector d = NpcDirector.Instance;
        if (phase == Phase.Task && dh != null && dh.Carrying && npcState != 1) npcState = 1;
        if (npcState == 0 || npcState == 3) return;
        if (npcState == 1)
        {
            Vector3 f = pl.forward;
            f.y = 0f;
            f = f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
            npcGoal = cfg.id == "fallen_log" ? pl.position - f * 1.2f - pl.right * 1.4f : pl.position - f * 1.7f + pl.right * 0.9f;
        }
        Vector3 p = npc.position, to = npcGoal - p;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist > 0.05f)
        {
            float sp = npcState == 1 ? Mathf.Clamp(dist * 2.5f, 0f, 6.5f) : 2.2f;
            Vector3 np = p + to / dist * Mathf.Min(dist, sp * dt);
            if (dist > 0.3f) npc.rotation = Quaternion.RotateTowards(npc.rotation, Quaternion.LookRotation(to), 540f * dt);
            float gy = d.GroundY(np.x, np.z, p.y + 1.5f);
            np.y = Mathf.Lerp(p.y, gy, Mathf.Min(1f, 12f * dt));
            npc.position = np;
        }
        if (npcCtl != null)
        {
            if (!walkOn && dist > 0.35f) { npcCtl.PlayWalk(); walkOn = true; }
            else if (walkOn && dist < 0.15f) { npcCtl.PlayIdle(); walkOn = false; }
        }
        if (npcState == 2 && dist <= 0.06f)
        {
            npcState = 3;
            walkOn = false;
            if (npcCtl != null) { if (success) npcCtl.Play(EventStage.After); else npcCtl.PlayIdle(); }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, Range);
    }
}
