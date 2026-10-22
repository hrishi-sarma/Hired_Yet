using UnityEngine;
namespace FPAnimation
{
    /// <summary>
    /// World event player action. Greet/Goodbye pick a random variant from a pool (one-shots). During runs until the
    /// layer shortens Duration. Arms are smoothed through critically damped springs so poses have weight and follow-through.
    /// Inputs: FPEvents.EventIntensity (effort), KickEvent (tap), EventProgress (task 0..1), EventArrow* (sequence keys).
    /// </summary>
    public class FPEventAction : FPAction
    {
        const int GreetCount = 7, ByeCount = 6;
        static int lastGreet = -1, lastBye = -1;

        readonly EventStage stage;
        readonly PlayerEventAnim anim;
        readonly int variant;
        readonly Transform target;
        float clr; bool clrInit; Collider[] pcols;
        float carry = 1f, lc, dl, inten, kick, prog, phase, leanS, leanV, dipS, dipV, arrowT = 9f;
        int kicksSeen, arrowsSeen, arrow;
        bool arrowOk = true, init;
        FPArm cR, cL; FPArmVel vR, vL;

        public FPEventAction(EventStage stage, PlayerEventAnim anim, Transform target, float seconds)
        {
            this.stage = stage; this.anim = anim; this.target = target;
            Duration = stage == EventStage.During ? 1e6f : Mathf.Max(1.4f, seconds);
            kicksSeen = FPEvents.EventKicks; arrowsSeen = FPEvents.EventArrowCount;
            if (anim == PlayerEventAnim.Greet) variant = Pick(ref lastGreet, GreetCount);
            else if (anim == PlayerEventAnim.Goodbye) variant = Pick(ref lastBye, ByeCount);
        }

        static int Pick(ref int last, int count)
        {
            int v = Random.Range(0, count);
            if (v == last) v = (v + 1 + Random.Range(0, count - 1)) % count;
            last = v; return v;
        }

        public override bool ShouldInterrupt(in FPActionCtx x) { return stage != EventStage.During && Disturbed(x); }

        static FPArm A(float flex, float abd, float elbow, float curl, float pron)
        { return new FPArm { flex = flex, abd = abd, elbow = elbow, curl = curl, pron = pron }; }

        static FPArm Mix(FPArm a, FPArm b, float k)
        {
            return new FPArm
            {
                flex = Mathf.Lerp(a.flex, b.flex, k), abd = Mathf.Lerp(a.abd, b.abd, k), elbow = Mathf.Lerp(a.elbow, b.elbow, k),
                curl = Mathf.Lerp(a.curl, b.curl, k), pron = Mathf.Lerp(a.pron, b.pron, k), off = Vector3.Lerp(a.off, b.off, k)
            };
        }

        float ClearanceTo(Transform t, Vector3 p)
        {
            if (pcols == null) pcols = t.GetComponentsInChildren<Collider>();
            float best = 9f;
            for (int i = 0; i < pcols.Length; i++)
            {
                Collider c = pcols[i];
                if (c == null || !c.enabled || c.isTrigger) continue;
                float d = Vector3.Distance(p, c.ClosestPoint(p));
                if (d < best) best = d;
            }
            return best;
        }

        static float Ov(float x) { x = Mathf.Clamp01(x) - 1f; return 1f + 2.70158f * x * x * x + 1.70158f * x * x; }   // ease-out-back
        static Vector3 Jit(float t, float seed, float amp)
        { return new Vector3(Mathf.PerlinNoise(t, seed) - 0.5f, Mathf.PerlinNoise(t, seed + 7f) - 0.5f, Mathf.PerlinNoise(t, seed + 13f) - 0.5f) * amp; }

        void Greet(int v, float t, float n, ref FPArm r, ref FPArm l, ref float lean)
        {
            float w = Mathf.Sin(t * 10f);
            switch (v)
            {
                case 0: r = A(112f, 28f + 20f * w, 100f, 0.15f, 0.3f); lean = 3f * Mathf.Sin(t * 5f); break;                       // wave
                case 1: r = A(80f, 55f, 138f, 0.2f, 0.5f); lean = 3f; break;                                                         // salute
                case 2: r = A(55f, 50f + 4f * Mathf.Sin(t * 2f), 25f, 0.1f, 0f); l = r; lean = -3f; break;                           // open arms
                case 3: r = A(95f, 12f, 85f, 0.05f, 1f); lean = 4f * Mathf.Sin(t * 7f) * (1f - Sm(0.5f, 0.7f, n)); break;            // raised palm, nod
                case 4:                                                                                                              // fist bump offer
                {
                    float b = Sm(0.35f, 0.5f, n) * (1f - Sm(0.5f, 0.65f, n));
                    r = A(78f + 10f * b, 6f, 45f - 15f * b, 1f, 0.5f); r.off = new Vector3(0f, 0f, 0.05f * b); lean = 2f + 4f * b; break;
                }
                case 5: r = A(115f, 30f + 18f * w, 95f, 0.2f, 0.3f); l = A(115f, 30f + 18f * Mathf.Sin(t * 10f + Mathf.PI), 95f, 0.2f, 0.3f); break; // two-hand wave
                default: r = A(50f, 0f, 125f, 0.4f, 0.4f); lean = 12f * Mathf.Sin(n * Mathf.PI); break;                              // hand on chest, bow
            }
        }

        void Bye(int v, float t, float n, ref FPArm r, ref FPArm l, ref float lean)
        {
            FPArm wave = A(110f, 25f + 22f * Mathf.Sin(t * 6f), 100f, 0.15f, 0.3f), chest = A(50f, 0f, 125f, 0.4f, 0.4f);
            switch (v)
            {
                case 0: r = wave; break;                                                                                               // slow wave
                case 1: r = A(112f, 28f + 20f * Mathf.Sin(t * 14f) * (1f - Sm(0.6f, 0.7f, n)), 100f, 0.15f, 0.3f); lean = -2f; break; // quick double wave
                case 2: { float f = Sm(0.4f, 0.7f, n); r = A(80f, 55f + 25f * f, 138f - 70f * f, 0.2f, 0.5f); break; }              // salute and flick off
                case 3: r = Mix(chest, wave, Sm(0.4f, 0.55f, n)); lean = 5f * (1f - Sm(0.4f, 0.55f, n)); break;                     // heart then wave
                case 4: r = A(105f, 15f, 90f - 20f * Mathf.Abs(Mathf.Sin(t * 5f)), 1f, 0.5f); break;                                // fist pump
                default: r = Mix(chest, wave, Sm(0.3f, 0.5f, n)); lean = 14f * Mathf.Sin(n * Mathf.PI); break;                       // bow then wave
            }
        }

        public override void Evaluate(in FPActionCtx x, float n, ref FPPose p, out float dip)
        {
            float t = T, dt = x.dt, st = 0.08f, dipT = 0f;
            if (!init) { init = true; cR = p.R; cL = p.L; carry = FPEvents.EventCarry; }
            if (FPEvents.EventKicks != kicksSeen) { kicksSeen = FPEvents.EventKicks; kick = 1f; phase += 0.1f; }
            kick = Mathf.MoveTowards(kick, 0f, dt * 6f);
            inten = Mathf.MoveTowards(inten, FPEvents.EventIntensity, dt * 4f);
            prog = Mathf.MoveTowards(prog, FPEvents.EventProgress, dt * 1.5f);
            float env = stage == EventStage.During ? Sm(0f, 0.35f, t) : Sm(0f, 0.25f, n) * (1f - Sm(0.8f, 1f, n));
            FPArm r = p.R, l = p.L;
            float lean = 0f;
            Vector3 fwd = x.frame * Vector3.forward; fwd.y = 0f; fwd.Normalize();

            switch (anim)
            {
                case PlayerEventAnim.Greet: st = 0.07f; l = p.L; Greet(variant, t, n, ref r, ref l, ref lean); break;
                case PlayerEventAnim.Goodbye: st = 0.07f; Bye(variant, t, n, ref r, ref l, ref lean); break;

                case PlayerEventAnim.Push:
                {
                    // wind up (arms drawn in), plant the hands, then drive: arms alternate each step with big elbow travel and a shoulder heave
                    float lean0 = Sm(0f, 0.9f, t), reach = Sm(0.5f, 1.6f, t), press = Sm(0.02f, 0.4f, prog);
                    float ps = t * (2.2f + 2.5f * prog), sa = Mathf.Sin(ps), sb = Mathf.Sin(ps + Mathf.PI);
                    float amp = (9f + 16f * inten) * reach, wind = Sm(0.3f, 0.9f, t) * (1f - Sm(0.9f, 1.6f, t)) * (1f - press);
                    float trem = (1f - press) * (0.3f + 0.7f * inten) * reach;
                    float el = 40f - 28f * press + 20f * wind - 16f * kick;
                    FPArm tr = A(76f + 6f * press + 6f * sa * press + 8f * kick, 10f + 5f * sa, el - amp * sa, 0.1f + 0.4f * press, 1f);
                    FPArm tl = A(76f + 6f * press + 6f * sb * press + 8f * kick, 10f + 5f * sb, el - amp * sb, 0.1f + 0.4f * press, 1f);
                    r = Mix(p.R, tr, reach); l = Mix(p.L, tl, reach);
                    r.off += Jit(t * 9f, 1f, 0.025f * trem) + new Vector3(0f, 0.012f * sa * press, -0.02f * sa * press);
                    l.off += Jit(t * 9f, 4f, 0.025f * trem) + new Vector3(0f, 0.012f * sb * press, -0.02f * sb * press);
                    lean = 16f * lean0 + 12f * press + 4f * kick + 2f * sa * press;
                    dipT = -0.015f * press * (0.5f + 0.5f * sa);
                    st = 0.1f; break;
                }
                case PlayerEventAnim.Pull:
                {
                    // reach out with stretching arms, grab, haul back leaning, recover; left arm lags a touch
                    phase += dt * (0.55f + 1.1f * inten);
                    float u = phase - Mathf.Floor(phase), s, grip;
                    FPArm ra = PullArm(u, out s, out grip), la = PullArm(Mathf.Repeat(u - 0.05f, 1f), out float s2, out float g2);
                    float trem = (1f - s) * grip * (0.3f + 0.7f * inten);
                    ra.off += Jit(t * 10f, 2f, 0.03f * trem); la.off += Jit(t * 10f, 5f, 0.03f * trem);
                    r = ra; l = la;
                    lean = Mathf.Lerp(-14f, 14f, s);
                    CamMove = fwd * (0.12f * s - 0.10f * (1f - s));
                    CamPitch = 2.5f * s;
                    dipT = -0.05f * (1f - s) * grip;
                    st = 0.07f; break;
                }
                case PlayerEventAnim.Lift:
                {
                    // crouch to the items first (EventCarry 0: squat, hands low, grip), then stand up with them once lifted (EventCarry 1)
                    carry = Mathf.MoveTowards(carry, FPEvents.EventCarry, dt * 0.9f);
                    float s = Sm(0f, 0.6f, t), trem = 0.01f + 0.03f * inten;
                    float squat = Sm(0f, 0.8f, t) * (1f - carry);
                    FPArm hi = A(30f, 26f, 96f + 3f * Mathf.Sin(t * 3f), 0.9f, 0.5f), lo = A(25f, 14f, 30f, 0.2f + 0.8f * Sm(0.6f, 1f, t), 0.5f);
                    r = Mix(lo, hi, carry); r.off = Jit(t * 7f, 0.3f, trem) + new Vector3(0f, -0.05f * carry, 0.01f * carry);
                    l = r; l.off = new Vector3(-r.off.x, r.off.y, -r.off.z + 0.02f);
                    lean = 22f * squat + (-6f - 3f * inten) * carry;
                    dipT = -0.6f * squat + ((-0.04f * s) - 0.02f * inten) * carry; CamPitch += 14f * squat; st = 0.1f; break;
                }
                case PlayerEventAnim.LiftCrouch:
                {
                    // squat to the corner and grab it; once E is held, drive up with the legs; letting go sinks back down
                    bool held = inten > 0.5f && t > 0.9f;
                    lc = Mathf.MoveTowards(lc, held ? 1f : 0f, dt * (held ? 0.45f : 0.9f));
                    FPEvents.EventLift = lc;
                    float squat = Sm(0f, 0.8f, t) * (1f - lc), grip = Sm(0.6f, 1f, t);
                    FPArm low = A(25f, 14f, 30f, 0.2f + 0.8f * grip, 0.5f), up = A(48f, 6f, 100f, 0.9f, 0.5f);
                    r = Mix(low, up, lc); r.off = Jit(t * 8f, 0.3f, 0.008f + 0.035f * inten * lc);
                    l = r; l.off = -r.off;
                    lean = 22f * squat - 5f * lc * (0.5f + 0.5f * inten);
                    dipT = -0.6f * squat - 0.02f * inten * lc; CamPitch += 14f * squat; st = 0.09f; break;
                }
                case PlayerEventAnim.DragLog:
                {
                    // squat to the log end and take a two-handed grip; once E is held, rise to a half crouch and haul it, leaning back with a tug per stride
                    bool hauling = inten > 0.5f && t > 0.9f;
                    dl = Mathf.MoveTowards(dl, hauling ? 1f : 0f, dt * (hauling ? 0.8f : 1.2f));
                    phase += dt * 2.2f * dl;
                    float tug = 0.5f + 0.5f * Mathf.Sin(phase * Mathf.PI * 2f);
                    float squat = Sm(0f, 0.8f, t), grip = Sm(0.5f, 0.95f, t);
                    FPArm low = A(22f, 14f, 28f, 0.2f + 0.8f * grip, 0.5f);
                    FPArm haul = A(16f - 6f * tug, 10f, 16f + 14f * tug, 1f, 0.5f);
                    r = Mix(low, haul, dl); r.off = Jit(t * 8f, 0.3f, 0.006f + 0.02f * inten * dl) + new Vector3(0f, 0f, -0.04f * tug * dl);
                    l = r; l.off = new Vector3(-r.off.x, r.off.y, r.off.z);
                    lean = Mathf.Lerp(20f * squat, -9f - 4f * tug, dl);
                    dipT = -0.55f * squat * (1f - 0.45f * dl) - 0.02f * tug * dl;
                    CamPitch += 14f * squat * (1f - dl);
                    st = 0.09f; break;
                }
                case PlayerEventAnim.Tinker:
                {
                    r = A(60f, 8f, 85f, 0.3f + 0.5f * Mathf.PerlinNoise(t * 4f, 7f), 0.6f); r.off = Jit(t * 3f, 1f, 0.05f);
                    l = A(60f, 8f, 85f, 0.3f + 0.5f * Mathf.PerlinNoise(t * 4f, 8f), 0.6f); l.off = Jit(t * 3f, 4f, 0.05f);
                    lean = 6f; break;
                }
                case PlayerEventAnim.Stir:
                {
                    float ph = t * (3f + 3f * inten);
                    r = A(65f, 6f, 70f, 0.5f, 0.8f); r.off = new Vector3(0.07f * Mathf.Cos(ph), 0f, 0.07f * Mathf.Sin(ph));
                    l = A(55f, 10f, 80f, 0.4f, 0.6f); lean = 8f + 2f * Mathf.Sin(ph); break;
                }
                case PlayerEventAnim.Support:
                    r = A(45f, 25f, 50f, 0.5f, 0.4f); lean = 6f; break;
                case PlayerEventAnim.Calm:
                {
                    float press = 0.5f + 0.5f * Mathf.Sin(t * 1.6f);
                    r = A(55f + 8f * press, 12f, 55f, 0.2f, 1f); r.off = new Vector3(0f, -0.04f * press, 0f); l = r; break;
                }
                case PlayerEventAnim.Fidget:
                {
                    float work = 0.35f + 0.65f * inten;
                    float f1 = Mathf.Sin(t * (6f + 5f * work)), f2 = Mathf.Sin(t * (7f + 5f * work) + 2.1f);
                    float rock = Mathf.Sin(t * (2.2f + 1.5f * work));
                    r = A(66f + 3f * rock * work, 14f, 98f, 0.5f + 0.3f * f1 * work, 0.85f);
                    l = A(66f - 3f * rock * work, 14f, 98f, 0.5f + 0.3f * f2 * work, 0.85f);
                    r.off = new Vector3(0.006f * f2 * work, 0.008f * f1 * work, 0.004f * rock) + Jit(t * 9f, 3f, 0.012f * work);
                    l.off = new Vector3(-0.006f * f1 * work, 0.008f * f2 * work, -0.004f * rock) + Jit(t * 9f, 5f, 0.012f * work);
                    lean = 7f + 2f * work; dipT = -0.01f * work; st = 0.05f; break;
                }
                case PlayerEventAnim.Inspect:
                {
                    float open = 0.5f + 0.5f * Mathf.Sin(t * 1.5f);
                    r = A(70f, 10f + 8f * open, 70f, 0.6f - 0.4f * open, 0.6f); l = r; lean = 5f; break;
                }
                case PlayerEventAnim.Point:
                    r = A(88f + 2f * Mathf.Sin(t * 2f), 6f, 10f, 0.15f, 0.6f); break;
                case PlayerEventAnim.Dance:
                {
                    float s = Mathf.Sin(t * 4f);
                    r = A(30f, 40f + 25f * s, 70f, 0.4f, 0.5f); l = A(30f, 40f - 25f * s, 70f, 0.4f, 0.5f);
                    lean = 5f * Mathf.Sin(t * 2f); dipT = -0.02f * Mathf.Abs(s); break;
                }
                case PlayerEventAnim.Turn:
                    r = A(65f, 8f, 60f, 0.7f, 0.6f + 0.4f * Mathf.Sin(t * (2f + 3f * inten))); l = A(55f, 10f, 80f, 0.3f, 0.5f); lean = 5f; break;
                case PlayerEventAnim.Bang:
                    r = A(70f + 20f * kick, 8f, 70f - 35f * kick, 1f, 0.5f); lean = 4f + 6f * kick; st = 0.04f; break;
                case PlayerEventAnim.Pour:
                    r = A(55f, 8f, 80f, 0.6f, 0.9f); r.off = new Vector3(0.02f * Mathf.Sin(t * 1.3f), 0f, 0f);
                    l = A(40f, 35f, 30f, 0.2f, 0.5f); lean = 6f; break;

                case PlayerEventAnim.Repair:
                {
                    // crouched, working on a low machine: wrench turns, probing, and a hard tug on each tap
                    float work = 0.5f + 0.5f * inten, probe = Mathf.PerlinNoise(t * 1.3f, 9f);
                    r = A(60f + 6f * probe - 18f * kick, 6f, 75f + 30f * kick, 0.6f, 0.6f + 0.4f * Mathf.Sin(t * (4f + 3f * work)));
                    r.off = Jit(t * 3f, 1f, 0.035f * work) + new Vector3(0f, 0f, -0.05f * kick);
                    l = A(58f, 10f, 82f, 0.5f, 0.5f); l.off = Jit(t * 2f, 4f, 0.02f);
                    lean = 4f - 3f * kick; st = 0.06f; break;
                }
                case PlayerEventAnim.Squeeze:
                {
                    // both hands cup the lemon, press the palms together hard, ease off, repeat; each tap adds an extra squeeze
                    phase += dt * (0.7f + 0.9f * inten);
                    float u = phase - Mathf.Floor(phase);
                    float press = u < 0.4f ? Sm(0f, 0.4f, u) : (u < 0.55f ? 1f : 1f - Sm(0.55f, 1f, u));
                    float sq = Mathf.Clamp01(press * (0.6f + 0.4f * inten) + 0.5f * kick) * Sm(0.1f, 0.7f, t);
                    FPEvents.EventSqueeze = sq;
                    r = A(60f + 4f * sq, 30f - 26f * sq, 92f - 14f * sq, 0.45f + 0.55f * sq, 0.9f);
                    r.off = new Vector3(0f, -0.02f * sq, 0.025f * sq) + Jit(t * 14f, 2f, 0.012f * sq * inten);
                    l = r;
                    lean = 6f + 3f * sq; dipT = -0.015f * sq; st = 0.05f; break;
                }
                case PlayerEventAnim.Bandage:
                {
                    // crouched at the ankle: the left hand cradles the foot and presses, the right hand loops the wrap round it and tugs it tight
                    phase += dt * (0.45f + 0.8f * inten);
                    float u = phase - Mathf.Floor(phase), ang = u * Mathf.PI * 2f;
                    float tug = Sm(0.78f, 0.9f, u) * (1f - Sm(0.9f, 1f, u));
                    float rad = 0.055f - 0.02f * prog;
                    r = A(62f, 10f, 78f - 14f * tug, 0.55f + 0.4f * tug, 0.8f);
                    r.off = new Vector3(rad * Mathf.Cos(ang), rad * 0.7f * Mathf.Sin(ang) - 0.05f * prog, -0.045f * tug + 0.012f * Mathf.Sin(ang * 2f));
                    l = A(52f, 22f, 82f, 0.85f, 0.9f);
                    l.off = new Vector3(-0.045f, -0.065f - 0.012f * tug, 0.02f) + Jit(t * 2f, 4f, 0.008f);
                    lean = 7f + 3f * tug; dipT = -0.01f * tug; st = 0.07f; break;
                }
                case PlayerEventAnim.Mend:
                {
                    float ph = t * (2.5f + 1.5f * inten);
                    r = A(62f, 12f, 80f, 0.5f, 0.7f); r.off = new Vector3(0.05f * Mathf.Cos(ph), 0.015f * Mathf.Sin(2f * ph), 0.05f * Mathf.Sin(ph));
                    l = A(62f, 12f, 80f, 0.5f, 0.7f); l.off = new Vector3(0.05f * Mathf.Cos(ph + Mathf.PI), 0.015f * Mathf.Sin(2f * ph), 0.05f * Mathf.Sin(ph + Mathf.PI));
                    lean = 5f; break;
                }
                case PlayerEventAnim.Pump:
                {
                    float pump = 0.5f + 0.5f * Mathf.Sin(t * 3.2f) + 0.5f * kick;
                    r = A(50f, 15f, 70f, 0.2f + 0.8f * Mathf.Clamp01(pump), 0.5f); r.off = new Vector3(0f, 0f, -0.03f * pump);
                    l = A(58f, 18f, 70f, 0.9f, 0.5f); l.off = Jit(t * 2f, 3f, 0.012f);
                    lean = 3f; st = 0.06f; break;
                }
                case PlayerEventAnim.Sort:
                {
                    // alternate arms: reach down to the pile, grab, lift to chest, toss to the side, return
                    phase += dt * (0.5f + 0.8f * inten);
                    int cyc = Mathf.FloorToInt(phase); float u = phase - cyc;
                    FPArm ready = A(55f, 15f, 95f, 0.35f, 0.5f), reachA = A(78f, 8f, 35f, 0.2f, 0.7f), chestA = A(70f, 12f, 115f, 1f, 0.5f), act;
                    if (u < 0.3f) act = Mix(ready, reachA, Ov(u / 0.3f));
                    else if (u < 0.4f) { act = reachA; act.curl = 0.2f + 0.8f * Sm(0.3f, 0.4f, u); }
                    else if (u < 0.7f) { reachA.curl = 1f; act = Mix(reachA, chestA, Sm(0.4f, 0.7f, u)); }
                    else if (u < 0.9f) { FPArm toss = A(60f, 55f, 60f, 1f - 0.8f * Sm(0.78f, 0.9f, u), 0.5f); act = Mix(chestA, toss, Sm(0.7f, 0.82f, u)); }
                    else { FPArm toss = A(60f, 55f, 60f, 0.2f, 0.5f); act = Mix(toss, ready, Sm(0.9f, 1f, u)); }
                    if ((cyc & 1) == 0) { r = act; l = ready; } else { l = act; r = ready; }
                    lean = 6f; st = 0.06f; break;
                }
                case PlayerEventAnim.Ponder:
                    r = A(62f, 0f, 135f + 3f * Mathf.Sin(t * 1.1f), 0.5f, 0.4f); l = A(40f, 10f, 110f, 0.4f, 0.4f); lean = 3f + 1.5f * Mathf.Sin(t * 0.8f); break;
                case PlayerEventAnim.Chop:
                {
                    float c = 0.5f + 0.5f * Mathf.Sin(t * (5f + 3f * inten) + kick * 2f);
                    r = A(52f + 10f * c, 8f, 85f - 40f * c, 0.9f, 0.5f);
                    l = A(50f, 15f, 95f, 0.7f, 0.6f); l.off = new Vector3(0.015f * Mathf.Sin(t * 5f), 0f, 0f);
                    lean = 8f + 3f * c; dipT = -0.012f * c; st = 0.04f; break;
                }

                case PlayerEventAnim.Arrows:
                {
                    // the arm that matches the arrow key just pressed snaps out, then drops back to a ready stance
                    if (FPEvents.EventArrowCount != arrowsSeen) { arrowsSeen = FPEvents.EventArrowCount; arrow = FPEvents.EventArrow; arrowOk = FPEvents.EventArrowOk; arrowT = 0f; }
                    arrowT += dt;
                    float hold = 1f - Sm(0.18f, 0.5f, arrowT);
                    FPArm ready = A(62f, 15f, 95f, 0.35f, 0.5f), tr = ready, tl = ready;
                    float ll = 0f;
                    switch (arrow)
                    {
                        case 0: tr = A(150f, 18f, 20f, 0.2f, 0.5f); tl = tr; break;                  // up: both hands high
                        case 1: tr = A(8f, 28f, 6f, 0.9f, 1f); tl = tr; ll = 8f; break;             // down: both hands slam low
                        case 2: tl = A(45f, 88f, 18f, 0.2f, 0.5f); break;                           // left arm sweeps out
                        default: tr = A(45f, 88f, 18f, 0.2f, 0.5f); break;                          // right arm sweeps out
                    }
                    r = Mix(ready, tr, hold); l = Mix(ready, tl, hold);
                    if (!arrowOk) { float f = hold * Mathf.Sin(t * 45f); r.off = new Vector3(0.03f * f, 0f, 0f); l.off = r.off; r = Mix(r, ready, 0.5f * hold); l = Mix(l, ready, 0.5f * hold); ll = -5f * hold; }
                    lean = ll * hold + 2f * Mathf.Sin(t * 2f); st = 0.05f; break;
                }
            }

            float H = FPEvents.EventFocusHeight;
            if (H > 0.01f && stage == EventStage.During)
            {
                // size-aware stance: crouch until the hands can work on the object, look down at it, edge toward it
                float want = Mathf.Clamp(x.camBase.y - (H + 0.45f), 0f, 1f) * (anim == PlayerEventAnim.Pour ? 0.6f : 1f);
                float cr = want * Sm(0f, 0.9f, t);
                dipT -= cr; lean += 14f * cr; CamPitch += 14f * cr;
                if (target != null)
                {
                    Vector3 to = target.position - x.camBase; to.y = 0f; float d = to.magnitude;
                    if (d > 0.01f) CamMove += (to / d) * Mathf.Clamp(d - 0.9f, 0f, 0.8f) * Mathf.Clamp01(want);
                }
            }

            lean += 0.5f * Mathf.Sin(t * 1.7f);   // breathing
            FPArmKinematics.Smooth(ref cR, ref vR, r, st, dt);
            FPArmKinematics.Smooth(ref cL, ref vL, l, st, dt);
            if (anim == PlayerEventAnim.Push && target != null)
            {
                // never lean the camera or torso into the object: cap the lean by the free distance to its nearest collider
                float dmin = ClearanceTo(target, x.camBase);
                clr = clrInit ? Mathf.MoveTowards(clr, dmin, dt * 5f) : dmin;
                clrInit = true;
                float allowed = Mathf.Max(0f, clr - 0.6f);
                lean = Mathf.Min(lean, Mathf.Asin(Mathf.Clamp01(allowed / 0.75f)) * Mathf.Rad2Deg);
            }
            leanS = FPSpring.Step(leanS, ref leanV, lean, 0.12f, dt);
            // head follows the torso: the camera swings forward and down along the lean arc while the hips stay planted
            float lb = 0.75f * Mathf.Sin(Mathf.Max(0f, leanS) * Mathf.Deg2Rad);
            BodyBack = lb * env;
            if (anim == PlayerEventAnim.Push || anim == PlayerEventAnim.LiftCrouch || anim == PlayerEventAnim.DragLog)
            {
                CamMove += fwd * lb;
                dipT -= 0.75f * (1f - Mathf.Cos(Mathf.Max(0f, leanS) * Mathf.Deg2Rad));
                if (anim == PlayerEventAnim.Push) CamPitch += 0.3f * leanS;
            }
            dipS = FPSpring.Step(dipS, ref dipV, dipT, 0.15f, dt);
            FPArmKinematics.Blend(ref p.R, cR, env);
            FPArmKinematics.Blend(ref p.L, cL, env);
            p.torsoLean = Mathf.Lerp(p.torsoLean, leanS, env);
            CamMove *= env; CamPitch *= env; dip = dipS * env;
        }

        static FPArm PullArm(float u, out float s, out float grip)
        {
            if (u < 0.35f) s = Mathf.Min(1.05f, Ov(u / 0.35f));
            else if (u < 0.42f) s = 1f;
            else if (u < 0.85f) s = 1f - Sm(0.42f, 0.85f, u);
            else s = 0f;
            grip = Sm(0.30f, 0.42f, u) * (1f - Sm(0.85f, 0.95f, u));
            FPArm a = A(Mathf.LerpUnclamped(35f, 105f, s), Mathf.Lerp(18f, 10f, s), Mathf.LerpUnclamped(105f, 8f, s), 0.2f + 0.8f * grip, 0.5f);
            return a;
        }
    }
}
