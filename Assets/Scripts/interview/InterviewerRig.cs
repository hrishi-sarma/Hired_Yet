using UnityEngine;
using E = FaceEmotion;

public enum RigEvent { Correct, Wrong, Hedge, NoAnswer, Pressure, Warn }

public enum FaceEmotion { Neutral, Friendly, Pleased, Suspicious, Annoyed, Angry, Bored, Sad, Doubt, Shock, Delighted, Disgust, Smug, Worried, Disappointed }

public class InterviewerRig : MonoBehaviour
{
    class Arm
    {
        public Transform up, low, hand;
        public Vector3 restUp, restLow, curUp, curLow;
        public Quaternion rotUp, rotLow;
        public float side = 1f;
        public bool ok;
    }

    class Eye { public Transform patch, sclera, pupil, lid, lower, brow; }

    struct FaceVals { public float lid, lower, brA, brY, smile, widen, asym, open, mw, pup, flush; }

    [Header("Gaze")]
    [SerializeField] Transform lookTarget;
    [SerializeField] float maxHeadYaw = 75f;
    [SerializeField] float behindAngle = 120f;
    [SerializeField] float lookSpeed = 6f;

    [Header("Face overlay (tune these in the Inspector)")]
    [SerializeField] bool faceOverlay = true;
    [SerializeField] bool colorsFromName = true;
    [SerializeField] Color skinColor = new Color(0.6f, 0.4f, 0.28f);
    [SerializeField] Color browColor = new Color(0.13f, 0.1f, 0.09f);
    [SerializeField] float headWidthRatio = 0.8f;
    [SerializeField] float faceDepthRatio = 0.5f;
    [SerializeField] float eyeLine = 0.5f;
    [SerializeField] float mouthLine = 0.17f;
    [SerializeField] float eyeSpread = 0.17f;
    [SerializeField] float faceScale = 1f;
    [SerializeField] Vector3 faceNudge;

    [Header("Seating")]
    [SerializeField] bool seatedPose = true;
    [SerializeField] bool forceSeated;
    [SerializeField] float seatLift;
    [SerializeField] float hipRise = 0.35f;

    [Header("Hands")]
    [SerializeField] float handSize = 0.5f;
    [SerializeField] Vector3 thumbNudge;

    static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }
    static Vector3[] Both(Vector3 u, Vector3 l) { return new[] { u, l, u, l }; }

    static readonly Vector3[] PRest = Both(V(.15f, -1f, .05f), V(.05f, -.85f, .45f));
    static readonly Vector3[] PFront = Both(V(.2f, -1f, .12f), V(-.3f, -.5f, .85f));
    static readonly Vector3[] PFist = Both(V(.5f, -.8f, .3f), V(-.15f, .6f, .9f));
    static readonly Vector3[] PCross = { V(.05f, -.95f, .3f), V(-1f, -.1f, .4f), V(.05f, -.95f, .3f), V(-1f, .12f, .5f) };
    static readonly Vector3[] PChin = { V(.25f, -.65f, .5f), V(-.35f, 1f, .35f), V(.05f, -.95f, .3f), V(-1f, 0f, .55f) };

    enum Ar { None, Thumb, ThumbL, Thumbs2, Clap, SlowClap, Pump, Facepalm, HeadHands, Shrug, Spread, Point, Dismiss, Stop, Temple, Chin, Cross, Tap, Slam, Watch, Mouth, Fidget, Beckon, Surrender, Behind, Plead }
    enum Hm { None, Nod1, Nod2, NodBig, Shake, ShakeFast, ShakeSigh, Tilt, TiltBack, Drop, Away, Roll, Lean, Recoil, Glare, Side, Chuckle, Jolt }
    enum Act { Correct, Wrong, Hedge, NoAnswer, Pressure, Warn, Pass, Fail, Idle }

    class Rx { public Ar arm; public Hm head; public E emo; public float dur; }
    struct HF { public float p, y, r, lean, gx, gy, gw, shut, open; }

    static Rx R(Ar a, Hm h, E e, float d) { return new Rx { arm = a, head = h, emo = e, dur = d }; }

    static readonly float[] SpeedMul = { 1f, 0.8f, 0.95f, 1.25f };
    static readonly float[] AmpMul = { 1f, 1.25f, 1.05f, 0.6f };

    static readonly Rx[][][] Pool =
    {
        new[]
        {
            new[] { R(Ar.Thumb, Hm.Nod2, E.Pleased, 2.2f), R(Ar.Thumbs2, Hm.NodBig, E.Delighted, 2.4f), R(Ar.Clap, Hm.Nod1, E.Delighted, 2.4f), R(Ar.Pump, Hm.Chuckle, E.Delighted, 2f), R(Ar.ThumbL, Hm.Nod1, E.Friendly, 2f) },
            new[] { R(Ar.Thumb, Hm.Nod1, E.Pleased, 1.5f), R(Ar.Beckon, Hm.Nod1, E.Friendly, 1.5f), R(Ar.Dismiss, Hm.Nod2, E.Pleased, 1.4f), R(Ar.Pump, Hm.Nod1, E.Smug, 1.6f), R(Ar.Clap, Hm.Nod2, E.Pleased, 1.7f) },
            new[] { R(Ar.Chin, Hm.Tilt, E.Doubt, 2.2f), R(Ar.Thumb, Hm.Side, E.Suspicious, 2.2f), R(Ar.Chin, Hm.Nod1, E.Smug, 2.2f), R(Ar.Fidget, Hm.Nod1, E.Worried, 2.2f), R(Ar.Point, Hm.TiltBack, E.Smug, 2f) },
            new[] { R(Ar.SlowClap, Hm.Nod1, E.Bored, 2.8f), R(Ar.Thumb, Hm.Nod1, E.Bored, 2f), R(Ar.Behind, Hm.Nod1, E.Pleased, 2.6f), R(Ar.Shrug, Hm.Nod1, E.Neutral, 2f), R(Ar.Watch, Hm.Nod1, E.Bored, 2.4f) }
        },
        new[]
        {
            new[] { R(Ar.None, Hm.ShakeSigh, E.Disappointed, 2.6f), R(Ar.Spread, Hm.Shake, E.Disappointed, 2.6f), R(Ar.Temple, Hm.Drop, E.Sad, 2.8f), R(Ar.Shrug, Hm.Tilt, E.Worried, 2.4f), R(Ar.Chin, Hm.Shake, E.Doubt, 2.4f) },
            new[] { R(Ar.Facepalm, Hm.Shake, E.Annoyed, 2.4f), R(Ar.Slam, Hm.ShakeFast, E.Angry, 2f), R(Ar.Dismiss, Hm.Roll, E.Disgust, 2.2f), R(Ar.Spread, Hm.ShakeFast, E.Annoyed, 2f), R(Ar.Tap, Hm.Glare, E.Annoyed, 2.4f) },
            new[] { R(Ar.Mouth, Hm.Recoil, E.Shock, 2f), R(Ar.Point, Hm.Side, E.Suspicious, 2.2f), R(Ar.Fidget, Hm.Away, E.Worried, 2.4f), R(Ar.Chin, Hm.Glare, E.Suspicious, 2.4f), R(Ar.Cross, Hm.TiltBack, E.Disgust, 2.2f) },
            new[] { R(Ar.Facepalm, Hm.ShakeSigh, E.Disappointed, 2.8f), R(Ar.None, Hm.Roll, E.Bored, 2.4f), R(Ar.Shrug, Hm.Drop, E.Bored, 2.2f), R(Ar.Watch, Hm.ShakeSigh, E.Bored, 2.6f), R(Ar.Behind, Hm.Drop, E.Disappointed, 2.8f) }
        },
        new[]
        {
            new[] { R(Ar.Beckon, Hm.Tilt, E.Doubt, 2f), R(Ar.Plead, Hm.Tilt, E.Friendly, 2.2f), R(Ar.Chin, Hm.Tilt, E.Doubt, 2.2f), R(Ar.Spread, Hm.Tilt, E.Doubt, 1.9f), R(Ar.Stop, Hm.Lean, E.Doubt, 2f) },
            new[] { R(Ar.Tap, Hm.Glare, E.Annoyed, 2f), R(Ar.Beckon, Hm.Lean, E.Annoyed, 1.8f), R(Ar.Spread, Hm.Roll, E.Doubt, 2f), R(Ar.Dismiss, Hm.TiltBack, E.Doubt, 1.8f), R(Ar.Temple, Hm.ShakeFast, E.Annoyed, 2f) },
            new[] { R(Ar.Chin, Hm.Side, E.Suspicious, 2.4f), R(Ar.Cross, Hm.TiltBack, E.Doubt, 2.2f), R(Ar.Fidget, Hm.Away, E.Worried, 2.2f), R(Ar.Point, Hm.Lean, E.Suspicious, 2f), R(Ar.Mouth, Hm.Tilt, E.Doubt, 2f) },
            new[] { R(Ar.Shrug, Hm.Tilt, E.Doubt, 2f), R(Ar.Chin, Hm.Drop, E.Bored, 2.2f), R(Ar.Watch, Hm.Roll, E.Bored, 2.4f), R(Ar.Behind, Hm.TiltBack, E.Doubt, 2.4f), R(Ar.None, Hm.ShakeSigh, E.Bored, 2.2f) }
        },
        new[]
        {
            new[] { R(Ar.Beckon, Hm.Lean, E.Friendly, 2f), R(Ar.Plead, Hm.Tilt, E.Worried, 2.2f), R(Ar.Spread, Hm.Tilt, E.Neutral, 1.8f), R(Ar.Chin, Hm.Lean, E.Doubt, 2f), R(Ar.Stop, Hm.Nod1, E.Friendly, 1.8f) },
            new[] { R(Ar.Tap, Hm.Glare, E.Annoyed, 2.2f), R(Ar.Watch, Hm.Lean, E.Annoyed, 2.2f), R(Ar.Beckon, Hm.Glare, E.Angry, 1.8f), R(Ar.Spread, Hm.Roll, E.Annoyed, 2f), R(Ar.Slam, Hm.Jolt, E.Angry, 1.8f) },
            new[] { R(Ar.Chin, Hm.Glare, E.Suspicious, 2.4f), R(Ar.Point, Hm.Lean, E.Suspicious, 2f), R(Ar.Cross, Hm.Side, E.Suspicious, 2.2f), R(Ar.Fidget, Hm.Away, E.Worried, 2.2f), R(Ar.Mouth, Hm.Recoil, E.Shock, 1.8f) },
            new[] { R(Ar.Watch, Hm.Drop, E.Bored, 2.6f), R(Ar.Behind, Hm.Roll, E.Bored, 2.6f), R(Ar.None, Hm.Drop, E.Bored, 2.2f), R(Ar.Shrug, Hm.Tilt, E.Bored, 2f), R(Ar.Temple, Hm.ShakeSigh, E.Bored, 2.4f) }
        },
        new[]
        {
            new[] { R(Ar.Chin, Hm.Lean, E.Suspicious, 2.4f), R(Ar.Stop, Hm.Lean, E.Neutral, 2f), R(Ar.Plead, Hm.Lean, E.Doubt, 2.2f), R(Ar.Beckon, Hm.Tilt, E.Doubt, 2f), R(Ar.Cross, Hm.Glare, E.Suspicious, 2.4f) },
            new[] { R(Ar.Point, Hm.Glare, E.Angry, 2f), R(Ar.Tap, Hm.Lean, E.Annoyed, 2.4f), R(Ar.Slam, Hm.Glare, E.Angry, 2f), R(Ar.Beckon, Hm.Lean, E.Annoyed, 1.8f), R(Ar.Cross, Hm.Glare, E.Annoyed, 2.4f) },
            new[] { R(Ar.Point, Hm.Lean, E.Suspicious, 2.2f), R(Ar.Chin, Hm.Glare, E.Suspicious, 2.6f), R(Ar.Mouth, Hm.Side, E.Suspicious, 2.2f), R(Ar.Cross, Hm.Side, E.Doubt, 2.4f), R(Ar.Fidget, Hm.Lean, E.Worried, 2.2f) },
            new[] { R(Ar.Cross, Hm.Lean, E.Suspicious, 2.4f), R(Ar.Chin, Hm.Lean, E.Suspicious, 2.4f), R(Ar.Watch, Hm.Glare, E.Annoyed, 2.2f), R(Ar.Stop, Hm.Lean, E.Neutral, 2f), R(Ar.Behind, Hm.Lean, E.Doubt, 2.4f) }
        },
        new[]
        {
            new[] { R(Ar.Stop, Hm.Glare, E.Annoyed, 2.2f), R(Ar.Point, Hm.Lean, E.Annoyed, 2.2f), R(Ar.Spread, Hm.Shake, E.Disappointed, 2.4f), R(Ar.Cross, Hm.Glare, E.Angry, 2.4f), R(Ar.Chin, Hm.ShakeSigh, E.Disappointed, 2.4f) },
            new[] { R(Ar.Point, Hm.Glare, E.Angry, 2.2f), R(Ar.Slam, Hm.Glare, E.Angry, 2f), R(Ar.Stop, Hm.ShakeFast, E.Angry, 2f), R(Ar.Facepalm, Hm.Roll, E.Angry, 2.2f), R(Ar.Dismiss, Hm.Glare, E.Disgust, 2f) },
            new[] { R(Ar.Point, Hm.Side, E.Angry, 2.2f), R(Ar.Mouth, Hm.Recoil, E.Shock, 2f), R(Ar.Point, Hm.Lean, E.Suspicious, 2.4f), R(Ar.Cross, Hm.Glare, E.Angry, 2.4f), R(Ar.Stop, Hm.Recoil, E.Shock, 2f) },
            new[] { R(Ar.Point, Hm.Lean, E.Annoyed, 2.2f), R(Ar.Stop, Hm.Glare, E.Annoyed, 2.2f), R(Ar.Dismiss, Hm.Roll, E.Disgust, 2.2f), R(Ar.Facepalm, Hm.Shake, E.Annoyed, 2.4f), R(Ar.Slam, Hm.Jolt, E.Angry, 2f) }
        },
        new[]
        {
            new[] { R(Ar.Thumbs2, Hm.NodBig, E.Delighted, 3f), R(Ar.Clap, Hm.Nod2, E.Delighted, 3f), R(Ar.Pump, Hm.Chuckle, E.Delighted, 2.6f), R(Ar.Plead, Hm.Nod2, E.Friendly, 2.8f), R(Ar.Spread, Hm.NodBig, E.Delighted, 2.8f) },
            new[] { R(Ar.Thumb, Hm.Nod1, E.Pleased, 2.4f), R(Ar.Pump, Hm.Nod2, E.Smug, 2.4f), R(Ar.Clap, Hm.Nod1, E.Pleased, 2.6f), R(Ar.Beckon, Hm.Nod1, E.Friendly, 2.2f), R(Ar.Spread, Hm.Nod2, E.Smug, 2.4f) },
            new[] { R(Ar.Thumb, Hm.Side, E.Smug, 2.8f), R(Ar.Chin, Hm.Nod1, E.Smug, 2.8f), R(Ar.Fidget, Hm.Nod2, E.Worried, 2.6f), R(Ar.Point, Hm.Tilt, E.Smug, 2.6f), R(Ar.Surrender, Hm.Chuckle, E.Pleased, 2.6f) },
            new[] { R(Ar.SlowClap, Hm.Nod1, E.Pleased, 3f), R(Ar.Thumb, Hm.Nod1, E.Neutral, 2.4f), R(Ar.Behind, Hm.Nod2, E.Pleased, 3f), R(Ar.Shrug, Hm.Nod1, E.Neutral, 2.4f), R(Ar.Watch, Hm.Nod1, E.Pleased, 2.6f) }
        },
        new[]
        {
            new[] { R(Ar.HeadHands, Hm.Drop, E.Sad, 3.2f), R(Ar.Spread, Hm.ShakeSigh, E.Disappointed, 3f), R(Ar.Temple, Hm.Shake, E.Sad, 3f), R(Ar.Plead, Hm.Shake, E.Disappointed, 3f), R(Ar.None, Hm.ShakeSigh, E.Sad, 3f) },
            new[] { R(Ar.Slam, Hm.ShakeFast, E.Angry, 2.6f), R(Ar.Facepalm, Hm.ShakeFast, E.Angry, 2.8f), R(Ar.Point, Hm.Glare, E.Angry, 2.8f), R(Ar.Dismiss, Hm.Roll, E.Disgust, 2.6f), R(Ar.Surrender, Hm.Roll, E.Annoyed, 2.8f) },
            new[] { R(Ar.Point, Hm.Side, E.Angry, 3f), R(Ar.Mouth, Hm.Recoil, E.Shock, 2.6f), R(Ar.Cross, Hm.Glare, E.Disgust, 3f), R(Ar.Chin, Hm.Side, E.Suspicious, 3f), R(Ar.HeadHands, Hm.Away, E.Worried, 3f) },
            new[] { R(Ar.Facepalm, Hm.ShakeSigh, E.Disappointed, 3f), R(Ar.Behind, Hm.Roll, E.Bored, 3f), R(Ar.Shrug, Hm.Drop, E.Bored, 2.6f), R(Ar.Watch, Hm.ShakeSigh, E.Bored, 2.8f), R(Ar.Dismiss, Hm.Drop, E.Disgust, 2.6f) }
        },
        new[]
        {
            new[] { R(Ar.Chin, Hm.Tilt, E.Friendly, 2.4f), R(Ar.Fidget, Hm.Nod1, E.Friendly, 2.4f), R(Ar.Temple, Hm.Drop, E.Neutral, 2.2f), R(Ar.Plead, Hm.Nod1, E.Friendly, 2.2f), R(Ar.None, Hm.Chuckle, E.Friendly, 1.8f) },
            new[] { R(Ar.Watch, Hm.Roll, E.Annoyed, 2.2f), R(Ar.Tap, Hm.Glare, E.Annoyed, 2.4f), R(Ar.Temple, Hm.ShakeSigh, E.Annoyed, 2.4f), R(Ar.Dismiss, Hm.TiltBack, E.Neutral, 1.8f), R(Ar.Cross, Hm.Glare, E.Annoyed, 2.2f) },
            new[] { R(Ar.Fidget, Hm.Away, E.Worried, 2.4f), R(Ar.Chin, Hm.Side, E.Suspicious, 2.4f), R(Ar.Mouth, Hm.Away, E.Worried, 2.2f), R(Ar.Cross, Hm.Glare, E.Suspicious, 2.4f), R(Ar.None, Hm.Away, E.Neutral, 2f) },
            new[] { R(Ar.Watch, Hm.Roll, E.Bored, 2.4f), R(Ar.Behind, Hm.Drop, E.Bored, 3f), R(Ar.Temple, Hm.ShakeSigh, E.Bored, 2.6f), R(Ar.Shrug, Hm.Tilt, E.Bored, 2f), R(Ar.None, Hm.Roll, E.Bored, 2.2f) }
        }
    };

    static Vector3[] One(Vector3 u, Vector3 l) { return new[] { u, l, Vector3.zero, Vector3.zero }; }
    static Vector3[] OneL(Vector3 u, Vector3 l) { return new[] { Vector3.zero, Vector3.zero, u, l }; }
    static Vector3[] Two(Vector3 u, Vector3 l) { return new[] { u, l, u, l }; }

    static float Env(float t)
    {
        float x = Mathf.Clamp01(Mathf.Min(t / 0.18f, (1f - t) / 0.22f));
        return x * x * (3f - 2f * x);
    }

    static FaceVals F(float lid = 0f, float lower = 0f, float brA = 0f, float brY = 0f, float smile = 0f, float widen = 0f, float asym = 0f, float open = 0f, float mw = 1f, float pup = 1f, float flush = 0f)
    {
        return new FaceVals { lid = lid, lower = lower, brA = brA, brY = brY, smile = smile, widen = widen, asym = asym, open = open, mw = mw, pup = pup, flush = flush };
    }

    // persona expressiveness: scales every deviation from the neutral face
    static FaceVals Scl(FaceVals v, float s)
    {
        if (Mathf.Abs(s - 1f) < 0.01f) return v;
        v.lid *= s; v.lower *= s; v.brA *= s; v.brY *= s; v.smile *= s; v.widen *= s; v.asym *= s; v.open *= s; v.flush *= s;
        v.mw = 1f + (v.mw - 1f) * s; v.pup = 1f + (v.pup - 1f) * s;
        return v;
    }

    static FaceVals Mix(FaceVals a, FaceVals b, float k)
    {
        return new FaceVals
        {
            lid = Mathf.Lerp(a.lid, b.lid, k), lower = Mathf.Lerp(a.lower, b.lower, k), brA = Mathf.Lerp(a.brA, b.brA, k), brY = Mathf.Lerp(a.brY, b.brY, k),
            smile = Mathf.Lerp(a.smile, b.smile, k), widen = Mathf.Lerp(a.widen, b.widen, k), asym = Mathf.Lerp(a.asym, b.asym, k), open = Mathf.Lerp(a.open, b.open, k),
            mw = Mathf.Lerp(a.mw, b.mw, k), pup = Mathf.Lerp(a.pup, b.pup, k), flush = Mathf.Lerp(a.flush, b.flush, k)
        };
    }

    Transform spine, chest, upperChest, neck, head;
    Quaternion rSpine, rChest, rUpper, rNeck, rHead;
    readonly Arm[] arms = { new Arm(), new Arm() };
    readonly Vector3[] want = new Vector3[4];
    readonly Eye[] eyes = { new Eye(), new Eye() };
    Transform faceRig, mouthPatch, mouthC, mouthL, mouthR;
    Material srcMat;
    readonly System.Collections.Generic.Dictionary<int, Material> mats = new System.Collections.Generic.Dictionary<int, Material>();

    float W = 0.2f, H = 0.25f;
    float clock, yawV, pitchV, curYaw, curPitch, curLean;
    float blinkU = 1f, nextBlink = 2f, glanceT, nextGlance = 4f, glanceYaw, targetSearch;
    bool ready, active, speaking, queuedOffer;
    int outcome;
    Mood mood;
    float susp, prevSusp = -1f, agit, burst, reactT, nod, shake, tilt, pressureT;
    FaceEmotion reactE;
    FaceVals fv;
    int gesture;
    float gestureT, gestureDur, gestureAmp = 1f;
    Vector3 seatWorld;
    Transform hips;
    Vector3 hipsLocal0;
    float seatDelta, seatBlend, seatScan;
    int scanLeft = 12;
    bool seated;
    readonly Arm[] legs = { new Arm(), new Arm() };
    readonly Quaternion[] rFoot = new Quaternion[2];
    readonly Transform[] thumbs = new Transform[2];
    readonly bool[] thumbOn = new bool[2];
    Material skinMat;
    Rx cur;
    Act curAct;
    bool playing;
    float playT, playDur, amp = 1f, rsign = 1f, idleT = 8f;
    readonly int[] lastIdx = new int[9];
    HF hf;

    public Vector3 HeadPosition { get { return head != null ? head.position + transform.up * H * 0.5f : transform.position + Vector3.up * 1.5f; } }

    void Awake()
    {
        var all = GetComponentsInChildren<Transform>(true);
        head = Find(all, "Head"); neck = Find(all, "Neck"); spine = Find(all, "Spine");
        chest = Find(all, "Chest"); upperChest = Find(all, "UpperChest");
        if (head == null) { enabled = false; return; }
        rSpine = RootRot(spine); rChest = RootRot(chest); rUpper = RootRot(upperChest); rNeck = RootRot(neck); rHead = RootRot(head);
        SetupArm(arms[0], Find(all, "RightUpperArm"), Find(all, "RightLowerArm"), Find(all, "RightHand"));
        SetupArm(arms[1], Find(all, "LeftUpperArm"), Find(all, "LeftLowerArm"), Find(all, "LeftHand"));
        hips = FindAny(all, "Hips", "Pelvis");
        if (hips != null) hipsLocal0 = hips.localPosition;
        SetupArm(legs[0], FindAny(all, "RightUpperLeg", "RightThigh", "RightUpLeg"), FindAny(all, "RightLowerLeg", "RightCalf", "RightShin"), FindAny(all, "RightFoot"));
        SetupArm(legs[1], FindAny(all, "LeftUpperLeg", "LeftThigh", "LeftUpLeg"), FindAny(all, "LeftLowerLeg", "LeftCalf", "LeftShin"), FindAny(all, "LeftFoot"));
        for (int i = 0; i < 2; i++) if (legs[i].ok) rFoot[i] = RootRot(legs[i].hand);
        var anim = GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController == null) anim.enabled = false;

        string n = name.ToLowerInvariant();
        if (colorsFromName)
        {
            if (n.Contains("manager")) { skinColor = new Color(0.43f, 0.27f, 0.2f); browColor = new Color(0.1f, 0.08f, 0.08f); }
            else if (n.Contains("receptionist")) { skinColor = new Color(0.59f, 0.39f, 0.27f); browColor = new Color(0.1f, 0.08f, 0.08f); }
            else if (n.Contains("office")) { skinColor = new Color(0.75f, 0.59f, 0.47f); browColor = new Color(0.33f, 0.21f, 0.14f); }
        }
        var rend = GetComponentInChildren<Renderer>();
        srcMat = rend != null ? rend.sharedMaterial : null;
        var end = Find(all, "HeadEnd");
        H = (end != null ? Vector3.Distance(head.position, end.position) : 0.25f) * faceScale;
        W = H * headWidthRatio;
        if (faceOverlay) BuildFace();
        skinMat = Mat(skinColor);
        MakeThumbs();
        fv = F();
        nextBlink = Random.Range(1.5f, 4f);
        ready = true;
    }

    static Transform FindAny(Transform[] all, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            var t = Find(all, names[i]);
            if (t != null) return t;
        }
        return null;
    }

    static Transform Find(Transform[] all, string n)
    {
        for (int i = 0; i < all.Length; i++)
            if (all[i].name == n || all[i].name.EndsWith(":" + n)) return all[i];
        return null;
    }

    Quaternion RootRot(Transform t) { return t == null ? Quaternion.identity : Quaternion.Inverse(transform.rotation) * t.rotation; }
    Vector3 RootDir(Vector3 w) { return transform.InverseTransformDirection(w).normalized; }
    static Vector3 Outward(Arm a, Vector3 v) { return new Vector3(v.x * a.side, v.y, v.z); }

    void SetupArm(Arm a, Transform u, Transform l, Transform h)
    {
        a.up = u; a.low = l; a.hand = h;
        a.ok = u != null && l != null && h != null;
        if (!a.ok) return;
        a.restUp = RootDir(l.position - u.position);
        a.restLow = RootDir(h.position - l.position);
        a.rotUp = RootRot(u); a.rotLow = RootRot(l);
        a.side = transform.InverseTransformPoint(u.position).x >= 0f ? 1f : -1f;
        a.curUp = Outward(a, a.restUp); a.curLow = Outward(a, a.restLow);
    }

    Material Mat(Color c)
    {
        int key = c.GetHashCode();
        Material m;
        if (mats.TryGetValue(key, out m)) return m;
        m = srcMat != null ? new Material(srcMat) : new Material(Shader.Find("Standard"));
        m.mainTexture = null;
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        mats[key] = m;
        return m;
    }

    Transform Part(string n, Color c)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = n;
        WorldUtil.Kill(g.GetComponent<Collider>());
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = Mat(c);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        g.transform.SetParent(faceRig, false);
        return g.transform;
    }

    void BuildFace()
    {
        var go = new GameObject("Face Rig");
        faceRig = go.transform;
        faceRig.SetParent(head, true);
        faceRig.rotation = transform.rotation;
        faceRig.position = head.position + transform.up * H * 0.5f + transform.forward * W * faceDepthRatio + transform.TransformDirection(faceNudge);
        Color white = new Color(0.95f, 0.95f, 0.95f), dark = new Color(0.06f, 0.05f, 0.07f);
        for (int i = 0; i < 2; i++)
        {
            var e = eyes[i];
            e.patch = Part("Patch", skinColor);
            e.sclera = Part("Sclera", white);
            e.pupil = Part("Pupil", dark);
            e.lid = Part("Lid", skinColor);
            e.lower = Part("LowerLid", skinColor);
            e.brow = Part("Brow", browColor);
        }
        mouthPatch = Part("MouthPatch", skinColor);
        Color mc = new Color(0.14f, 0.05f, 0.06f);
        mouthC = Part("MouthC", mc); mouthL = Part("MouthL", mc); mouthR = Part("MouthR", mc);
    }

    public void SetActive(bool on, Mood m)
    {
        active = on; mood = m;
        outcome = 0; reactT = 0f; burst = 0f; prevSusp = -1f; gesture = 0; queuedOffer = false; speaking = false;
        playing = false; thumbOn[0] = thumbOn[1] = false; idleT = Random.Range(6f, 12f);
        if (on) { scanLeft = Mathf.Max(scanLeft, 3); seatScan = 0f; }
        if (!on) susp = 0f;
    }

    public void SetSuspicion(float s)
    {
        s = Mathf.Clamp01(s);
        if (prevSusp >= 0f && active && outcome == 0)
        {
            if (s - prevSusp > 0.03f) { reactE = s > 0.6f ? FaceEmotion.Angry : FaceEmotion.Annoyed; reactT = Mathf.Max(reactT, 1.2f); burst += 0.3f; InterviewerSounds.React(head, reactE, mood, false, 0.6f); }
            else if (prevSusp - s > 0.02f) { reactE = FaceEmotion.Pleased; reactT = Mathf.Max(reactT, 1.2f); InterviewerSounds.React(head, reactE, mood, false, 0.5f); }
        }
        prevSusp = s; susp = s;
    }

    public void SetSpeaking(bool b) { speaking = b; }
    public void SetMood(Mood m) { if (m == mood) return; mood = m; burst += 0.4f; }

    Ar greetAr; float greetFreq = 9f; bool greetDouble;

    public void GreetAndOffer(Vector3 seat)
    {
        seatWorld = seat; queuedOffer = true;
        Ar[] opts; Hm[] heads; E emo;
        greetFreq = 9f; greetDouble = false; gestureAmp = Random.Range(0.8f, 1.1f);
        switch (mood)
        {
            case Mood.Patient:
                opts = new[] { Ar.None, Ar.None, Ar.Spread, Ar.Beckon, Ar.Clap };
                heads = new[] { Hm.Nod1, Hm.Tilt, Hm.Nod2 };
                emo = Random.value < 0.5f ? E.Friendly : E.Pleased; break;
            case Mood.Impatient:
                opts = new[] { Ar.Dismiss, Ar.Point, Ar.Tap, Ar.Beckon };
                heads = new[] { Hm.Roll, Hm.Glare, Hm.Side };
                emo = E.Annoyed; gestureAmp = 0.55f; break;
            case Mood.Paranoid:
                opts = new[] { Ar.Chin, Ar.Temple, Ar.Point, Ar.Cross };
                heads = new[] { Hm.Glare, Hm.Side, Hm.Tilt };
                emo = E.Suspicious; break;
            default:
                opts = new[] { Ar.Watch, Ar.Shrug, Ar.None, Ar.Fidget };
                heads = new[] { Hm.Away, Hm.Drop, Hm.Nod1 };
                emo = E.Bored; gestureAmp = 0.4f; greetFreq = 4f; break;
        }
        greetAr = opts[Random.Range(0, opts.Length)];
        greetDouble = greetAr == Ar.None && mood == Mood.Patient && Random.value < 0.4f;
        gesture = 1; gestureT = 0f; gestureDur = Random.Range(1.2f, 2.2f);
        reactE = emo; reactT = 3f;
        if (ready)
        {
            cur = R(Ar.None, heads[Random.Range(0, heads.Length)], emo, gestureDur);
            curAct = Act.Idle; playing = true; playT = 0f; playDur = gestureDur;
            amp = AmpMul[(int)mood]; rsign = Random.value < 0.5f ? -1f : 1f;
        }
    }

    public void Trigger(RigEvent e)
    {
        if (!active) return;
        switch (e)
        {
            case RigEvent.Correct: Play(Act.Correct); break;
            case RigEvent.Wrong: burst += 0.6f; Play(Act.Wrong); break;
            case RigEvent.Hedge: Play(Act.Hedge); break;
            case RigEvent.NoAnswer: burst += 0.3f; Play(Act.NoAnswer); break;
            case RigEvent.Pressure: pressureT = 3f; Play(Act.Pressure); break;
            case RigEvent.Warn: burst += 0.5f; Play(Act.Warn); break;
        }
    }

    public void SetOutcome(bool passed)
    {
        outcome = passed ? 1 : -1;
        if (!passed) burst += 0.6f;
        playing = false;
        Play(passed ? Act.Pass : Act.Fail);
    }

    void Play(Act a)
    {
        if (!ready || !active) return;
        bool final = curAct == Act.Pass || curAct == Act.Fail;
        if (playing && final && a != Act.Pass && a != Act.Fail) return;
        if (playing && curAct == a && !final) return;
        var set = Pool[(int)a][(int)mood];
        int i = Random.Range(0, set.Length);
        if (i == lastIdx[(int)a]) i = (i + 1 + Random.Range(0, set.Length - 1)) % set.Length;
        lastIdx[(int)a] = i;
        cur = set[i]; curAct = a; playing = true; playT = 0f;
        playDur = cur.dur * SpeedMul[(int)mood]; amp = AmpMul[(int)mood];
        rsign = Random.value < 0.5f ? -1f : 1f;
        reactE = cur.emo; reactT = playDur;
        InterviewerSounds.React(head, cur.emo, mood, a == Act.Pass || a == Act.Fail, a == Act.Idle ? 0.25f : 1f);
    }

    void Reaction(float dt)
    {
        hf = default(HF);
        if (playing)
        {
            playT += dt;
            if (playT >= playDur) playing = false;
            else hf = Head(cur.head, playT / playDur);
        }
        else if (active && outcome == 0 && !speaking && reactT <= 0f)
        {
            idleT -= dt;
            if (idleT <= 0f) { idleT = Random.Range(8f, 15f); Play(Act.Idle); }
        }
    }

    HF Head(Hm h, float t)
    {
        var o = new HF();
        float e = Env(t), tau = 6.2832f;
        switch (h)
        {
            case Hm.Nod1: o.p = 14f * Mathf.Sin(t * Mathf.PI); break;
            case Hm.Nod2: o.p = 13f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)); break;
            case Hm.NodBig: o.p = 22f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f)) * (1f - t * 0.4f); o.lean = 3f * e; break;
            case Hm.Shake: o.y = 17f * Mathf.Sin(t * tau * 2f) * e; o.p = 10f * e; o.lean = -2f * e; break;
            case Hm.ShakeFast: o.y = 24f * Mathf.Sin(t * tau * 4f) * e; o.p = 4f * e; break;
            case Hm.ShakeSigh: o.p = 15f * e; o.y = 12f * Mathf.Sin(t * tau * 1.5f) * e; o.lean = -6f * e; o.shut = 0.85f * e; break;
            case Hm.Tilt: o.r = 14f * rsign * e; o.y = 4f * e; break;
            case Hm.TiltBack: o.p = -9f * e; o.r = -6f * rsign * e; o.lean = -6f * e; break;
            case Hm.Drop: o.p = 24f * e; o.lean = 5f * e; o.shut = 0.3f * e; break;
            case Hm.Away: o.y = 34f * rsign * e; o.p = 4f * e; break;
            case Hm.Roll:
                float a = Mathf.Lerp(-1.4f, 1.4f, t);
                o.gx = Mathf.Sin(a); o.gy = Mathf.Cos(a); o.gw = e; o.p = -9f * e; o.r = -5f * rsign * e; break;
            case Hm.Lean: o.lean = 12f * e; o.p = 5f * e; break;
            case Hm.Recoil: o.lean = -10f * e; o.p = -8f * e - 12f * Mathf.Exp(-t * 9f); break;
            case Hm.Glare: o.lean = 8f * e; o.p = 9f * e; o.gy = 0.6f; o.gw = e; break;
            case Hm.Side: o.y = 26f * rsign * e; o.r = 5f * rsign * e; o.gx = -rsign * 0.9f; o.gw = e; break;
            case Hm.Chuckle: o.p = 6f * Mathf.Sin(t * tau * 6f) * e; o.lean = 2f * Mathf.Sin(t * tau * 6f) * e; o.open = 0.5f * Mathf.Abs(Mathf.Sin(t * tau * 3f)) * e; break;
            case Hm.Jolt: o.p = -14f * Mathf.Exp(-t * 10f); o.lean = -8f * Mathf.Exp(-t * 7f); o.open = 0.8f * Mathf.Exp(-t * 4f); break;
        }
        return o;
    }

    void ArmFx(Ar a, float t, Vector3[] bp)
    {
        float e = Env(t), pi = Mathf.PI;
        Vector3[] g = null;
        switch (a)
        {
            case Ar.Thumb: g = One(V(.4f, -.45f, .8f), V(.05f, 1f, .35f + .12f * Mathf.Sin(t * pi * 6f))); thumbOn[0] = e > .4f; break;
            case Ar.ThumbL: g = OneL(V(.4f, -.45f, .8f), V(.05f, 1f, .35f + .12f * Mathf.Sin(t * pi * 6f))); thumbOn[1] = e > .4f; break;
            case Ar.Thumbs2: g = Two(V(.4f, -.45f, .8f), V(.05f, 1f, .35f + .12f * Mathf.Sin(t * pi * 6f))); thumbOn[0] = thumbOn[1] = e > .4f; break;
            case Ar.Clap: g = Two(V(.15f, -.55f, .8f), V(-.6f - .4f * Mathf.Abs(Mathf.Sin(t * pi * 7f)), .35f, .6f)); break;
            case Ar.SlowClap: g = Two(V(.15f, -.55f, .8f), V(-.6f - .4f * Mathf.Abs(Mathf.Sin(t * pi * 3.5f)), .35f, .6f)); break;
            case Ar.Pump: g = One(V(.5f, -.2f + .35f * Mathf.Sin(t * pi * 5f), .6f), V(.15f, 1f, .2f + .2f * Mathf.Sin(t * pi * 5f))); break;
            case Ar.Facepalm: g = One(V(.25f, -.6f, .5f), V(-.6f, 1f, .5f)); break;
            case Ar.HeadHands: g = Two(V(.3f, -.35f, .45f), V(-.8f, 1f, .5f)); break;
            case Ar.Shrug: g = Two(V(.65f, -.75f, .1f), V(.8f, .3f + .15f * Mathf.Sin(t * pi * 4f), .7f)); break;
            case Ar.Spread: g = Two(V(.95f, -.2f, .25f), V(1f, .15f, .55f)); break;
            case Ar.Point:
                if (lookTarget != null) { Aim(0, lookTarget.position, .05f, e); want[1] = Vector3.Lerp(bp[1], want[1], e); }
                return;
            case Ar.Dismiss: g = One(V(.7f, -.5f, .4f), V(.9f, .1f, .3f + .5f * Mathf.Sin(t * pi * 8f))); break;
            case Ar.Stop: g = Two(V(.35f, -.4f, .8f), V(0f, .3f, 1f)); break;
            case Ar.Temple: g = One(V(.55f, -.4f, .2f), V(-.3f + .15f * Mathf.Sin(t * pi * 6f), 1f, .15f)); break;
            case Ar.Chin: g = One(V(.25f, -.65f, .5f), V(-.35f + .06f * Mathf.Sin(t * pi * 5f), 1f, .35f)); break;
            case Ar.Cross: g = PCross; break;
            case Ar.Tap:
                float s0 = Mathf.Abs(Mathf.Sin(t * pi * 6f)), s1 = Mathf.Abs(Mathf.Cos(t * pi * 6f));
                g = new[] { V(.2f, -.8f, .5f), V(-.3f, -.25f - .4f * s0, .9f), V(.2f, -.8f, .5f), V(-.3f, -.25f - .4f * s1, .9f) };
                break;
            case Ar.Slam:
                float h = t < .5f ? t * 2f : Mathf.Max(0f, 1f - (t - .5f) * 7f);
                g = Two(V(.4f, -.3f + .2f * h, .6f), V(.1f, -.6f + 1.6f * h, .6f));
                break;
            case Ar.Watch: g = OneL(V(.2f, -.7f, .5f), V(-.8f, .45f, .7f)); break;
            case Ar.Mouth: g = One(V(.3f, -.5f, .5f), V(-.7f, .85f, .65f)); break;
            case Ar.Fidget: g = Two(V(.2f, -1f, .12f), V(-.3f + .3f * Mathf.Sin(t * pi * 10f), -.5f, .85f)); break;
            case Ar.Beckon: g = One(V(.4f, -.45f, .7f), V(.1f, .2f + .4f * Mathf.Sin(t * pi * 10f), 1f)); break;
            case Ar.Surrender: g = Two(V(.8f, .1f, .1f), V(.35f + .1f * Mathf.Sin(t * pi * 6f), 1f, .2f)); break;
            case Ar.Behind: g = Two(V(.85f, .15f, -.05f), V(-.7f, .6f, -.55f)); break;
            case Ar.Plead: g = Two(V(.15f, -.8f, .35f), V(-.85f, .3f + .1f * Mathf.Sin(t * pi * 6f), .5f)); break;
        }
        if (g == null) return;
        for (int i = 0; i < 4; i++)
            if (g[i] != Vector3.zero) want[i] = Vector3.Lerp(bp[i], g[i], e);
    }

    void FindTarget()
    {
        if (lookTarget != null && lookTarget.gameObject.activeInHierarchy) return;
        targetSearch -= Time.deltaTime;
        if (targetSearch > 0f) return;
        targetSearch = 1.5f;
        Camera cam = null;
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) cam = p.GetComponentInChildren<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam != null) lookTarget = cam.transform;
    }

    float Nz(float s) { return Mathf.PerlinNoise(clock * 1.7f + s, s * 3.1f) - 0.5f; }

    void Set(Transform t, Quaternion rest, Quaternion q)
    {
        if (t != null) t.rotation = transform.rotation * q * rest;
    }

    void LateUpdate()
    {
        if (!ready) return;
        float dt = Time.deltaTime;
        clock += dt;
        FindTarget();
        reactT = Mathf.Max(0f, reactT - dt); pressureT = Mathf.Max(0f, pressureT - dt);
        nod = Mathf.MoveTowards(nod, 0f, dt * 0.7f); shake = Mathf.MoveTowards(shake, 0f, dt * 0.7f); tilt = Mathf.MoveTowards(tilt, 0f, dt * 0.6f);
        burst = Mathf.MoveTowards(burst, 0f, dt * 0.35f);
        float agitWant = active ? Mathf.Clamp01(susp * 1.15f + (mood == Mood.Impatient ? 0.2f : 0f) + (mood == Mood.Paranoid ? 0.1f : 0f) + burst) : 0f;
        agit = Mathf.Lerp(agit, agitWant, 1f - Mathf.Exp(-3f * dt));

        Reaction(dt);
        float yaw, pitch, w;
        Gaze(dt, out yaw, out pitch, out w);
        Torso(dt);
        Arms(dt);
        Legs(dt);
        if (faceRig != null) Face(dt, yaw, pitch, w);
    }

    void Gaze(float dt, out float yaw, out float pitch, out float w)
    {
        Vector3 tp = lookTarget != null ? lookTarget.position : head.position + transform.forward;
        Vector3 d = transform.InverseTransformPoint(tp) - transform.InverseTransformPoint(head.position);
        yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        pitch = -Mathf.Atan2(d.y, Mathf.Sqrt(d.x * d.x + d.z * d.z)) * Mathf.Rad2Deg;
        w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(behindAngle - 30f, behindAngle, Mathf.Abs(yaw)));
        if (active)
        {
            if (mood == Mood.Bored) w *= 0.7f;
            if (mood == Mood.Paranoid) w = Mathf.Max(w, 0.9f);
        }
        glanceT -= dt;
        if (glanceT <= 0f)
        {
            nextGlance -= dt;
            if (nextGlance <= 0f)
            {
                float freq = !active ? 7f : (mood == Mood.Paranoid ? 2.5f : (mood == Mood.Bored ? 3.5f : 6f));
                nextGlance = Random.Range(freq, freq * 1.8f);
                glanceT = Random.Range(0.5f, 1.2f);
                glanceYaw = Random.Range(-1f, 1f) * (active && mood == Mood.Paranoid ? 30f : 22f);
            }
        }
        float wantYaw = Mathf.Clamp(yaw, -maxHeadYaw, maxHeadYaw) * w + Nz(1f) * 5f;
        float wantPitch = Mathf.Clamp(pitch, -25f, 25f) * w + Nz(2f) * 3f;
        if (glanceT > 0f) { wantYaw = Mathf.Lerp(wantYaw, glanceYaw, 0.7f); }
        float st = 1f / Mathf.Max(0.5f, lookSpeed) * 0.5f;
        curYaw = Mathf.SmoothDampAngle(curYaw, wantYaw, ref yawV, st);
        curPitch = Mathf.SmoothDampAngle(curPitch, wantPitch, ref pitchV, st);
        yaw = yaw * w;
        pitch = pitch * w;
    }

    void Torso(float dt)
    {
        float leanWant = 0f;
        if (active)
        {
            leanWant = (mood == Mood.Bored ? -5f : (mood == Mood.Paranoid ? 4f : 0f)) + susp * 6f + (pressureT > 0f ? 9f : 0f);
        }
        curLean = Mathf.Lerp(curLean, leanWant, 1f - Mathf.Exp(-3f * dt));
        float breath = Mathf.Sin(clock * 1.6f) * 0.7f;
        float roll = Mathf.Sin(clock * 0.5f) * 1.2f + Nz(5f) * agit * 6f;
        Quaternion qt = Quaternion.Euler(curLean + hf.lean * amp + breath + Nz(6f) * agit * 3f, curYaw * 0.2f, roll);

        float pAdd = Mathf.Sin(clock * 5f) * (speaking ? 1.5f : 0f) + Mathf.Sin(clock * 6f) * 7f * nod;
        float yAdd = Mathf.Sin(clock * 14f) * 10f * shake;
        float rAdd = 10f * tilt;
        if (active && mood == Mood.Bored && outcome == 0) { pAdd += 6f; rAdd += 5f; }
        if (active && susp > 0.7f) pAdd += 5f * susp;
        Quaternion qh = Quaternion.Euler(curPitch + pAdd + hf.p * amp, curYaw + yAdd + hf.y * amp, rAdd + hf.r * amp);

        Set(spine, rSpine, Quaternion.Slerp(Quaternion.identity, qt, 0.4f));
        Set(chest, rChest, Quaternion.Slerp(Quaternion.identity, qt, upperChest != null ? 0.7f : 1f));
        Set(upperChest, rUpper, qt);
        Set(neck, rNeck, qt * Quaternion.Slerp(Quaternion.identity, qh, 0.4f));
        Set(head, rHead, qt * qh);
    }

    Vector3[] BasePose()
    {
        if (!active || outcome != 0) return outcome > 0 ? PFront : (outcome < 0 ? PCross : PRest);
        if (susp >= 0.75f) return PFist;
        if (susp >= 0.5f || mood == Mood.Impatient) return PCross;
        if (mood == Mood.Paranoid) return ((int)(clock / 7f) % 2 == 0) ? PChin : PFront;
        if (mood == Mood.Bored) return PRest;
        return PFront;
    }

    void Aim(int armIdx, Vector3 worldTarget, float upLift, float blend)
    {
        var a = arms[armIdx];
        if (!a.ok) return;
        Vector3 d = RootDir(worldTarget - a.up.position);
        d.y += upLift;
        d.Normalize();
        Vector3 o = Outward(a, d);
        want[armIdx * 2] = Vector3.Lerp(want[armIdx * 2], o, blend);
        want[armIdx * 2 + 1] = o;
    }

    void Arms(float dt)
    {
        Vector3[] bp = BasePose();
        System.Array.Copy(bp, want, 4);
        thumbOn[0] = thumbOn[1] = false;
        float tremor = agit * 0.3f;

        if (gesture != 0)
        {
            gestureT += dt;
            if (gestureT >= gestureDur)
            {
                if (gesture == 1 && queuedOffer) { gesture = 2; gestureT = 0f; gestureDur = 2.6f; queuedOffer = false; }
                else gesture = 0;
            }
        }

        if (gesture == 1)
        {
            if (greetAr != Ar.None) ArmFx(greetAr, Mathf.Clamp01(gestureT / gestureDur), bp);
            else
            {
                want[0] = V(.85f, .5f, .1f);
                want[1] = V(.25f + Mathf.Sin(clock * greetFreq) * .55f * gestureAmp, 1f, .1f);
                if (greetDouble)
                {
                    want[2] = V(.85f, .5f, .1f);
                    want[3] = V(.25f + Mathf.Sin(clock * greetFreq + 1.2f) * .55f * gestureAmp, 1f, .1f);
                }
            }
        }
        else if (gesture == 2)
        {
            Vector3 sd = arms[0].ok ? RootDir(seatWorld - arms[0].up.position) : Vector3.right;
            int gi = (Mathf.Sign(sd.x) == arms[0].side) ? 0 : 1;
            Aim(gi, seatWorld, 0.1f, 0.8f);
        }
        else if (gesture == 3 && lookTarget != null)
        {
            Aim(0, lookTarget.position, 0.05f, 1f);
            tremor += 0.04f;
        }
        else if (playing)
        {
            ArmFx(cur.arm, playT / playDur, bp);
        }
        else
        {
            if (speaking)
            {
                want[1] += V(Mathf.Sin(clock * 4.3f) * .25f, Mathf.Abs(Mathf.Sin(clock * 2.2f)) * .45f, .1f);
                want[0] += V(.1f, .15f, 0f);
            }
            if (active && mood == Mood.Impatient && bp == PCross) want[1].y += Mathf.Sin(clock * 10f) * .15f;
            if (active && susp >= 0.85f && outcome == 0 && (clock % 9f) < 1.6f && lookTarget != null) Aim(0, lookTarget.position, 0.05f, 1f);
        }

        float k = 1f - Mathf.Exp(-(7f + agit * 4f) * dt);
        for (int i = 0; i < 2; i++)
        {
            var a = arms[i];
            if (!a.ok) continue;
            a.curUp = Vector3.Slerp(a.curUp, want[i * 2].normalized, k);
            a.curLow = Vector3.Slerp(a.curLow, want[i * 2 + 1].normalized, k);
            Vector3 n = V(Nz(10f + i * 7f), Nz(20f + i * 7f), Nz(30f + i * 7f)) * tremor;
            Vector3 du = (Outward(a, a.curUp) + n).normalized, dl = (Outward(a, a.curLow) + n).normalized;
            a.up.rotation = transform.rotation * Quaternion.FromToRotation(a.restUp, du) * a.rotUp;
            a.low.rotation = transform.rotation * Quaternion.FromToRotation(a.restLow, dl) * a.rotLow;
        }
        Thumbs();
    }

    FaceEmotion Resolve()
    {
        if (!active) return FaceEmotion.Neutral;
        if (reactT > 0f) return reactE;
        if (outcome > 0) return mood == Mood.Patient ? FaceEmotion.Delighted : FaceEmotion.Pleased;
        if (outcome < 0) return mood == Mood.Patient ? FaceEmotion.Disappointed : (mood == Mood.Bored ? FaceEmotion.Bored : (mood == Mood.Paranoid ? FaceEmotion.Suspicious : FaceEmotion.Angry));
        float s = susp + (mood == Mood.Impatient ? 0.1f : 0f);
        if (s >= 0.85f) return FaceEmotion.Angry;
        if (s >= 0.68f) return FaceEmotion.Annoyed;
        if (s >= 0.48f) return FaceEmotion.Suspicious;
        if (mood == Mood.Bored) return FaceEmotion.Bored;
        if (mood == Mood.Impatient && s > 0.3f) return FaceEmotion.Annoyed;
        if (mood == Mood.Paranoid && s > 0.2f) return FaceEmotion.Suspicious;
        return s < 0.25f && mood == Mood.Patient ? FaceEmotion.Friendly : FaceEmotion.Neutral;
    }

    static FaceVals Vals(FaceEmotion e)
    {
        switch (e)
        {
            case FaceEmotion.Friendly: return F(lid: .18f, brA: -.25f, brY: .5f, smile: 1.1f, widen: .1f, mw: 1.15f, flush: .08f);
            case FaceEmotion.Pleased: return F(lid: .3f, brA: -.1f, brY: .7f, smile: 1.5f, mw: 1.3f, flush: .12f);
            case FaceEmotion.Delighted: return F(lid: .1f, brA: -.3f, brY: 1.1f, smile: 1.8f, widen: .35f, open: .55f, mw: 1.4f, pup: 1.2f, flush: .2f);
            case FaceEmotion.Suspicious: return F(lid: .42f, lower: .4f, brA: .55f, brY: -.25f, smile: -.3f, pup: .8f);
            case FaceEmotion.Annoyed: return F(lid: .5f, lower: .2f, brA: .8f, brY: -.35f, smile: -.7f, mw: .9f, flush: .12f);
            case FaceEmotion.Angry: return F(lid: .55f, lower: .4f, brA: 1.6f, brY: -.7f, smile: -1.5f, widen: .1f, open: .15f, mw: 1.1f, pup: .7f, flush: .45f);
            case FaceEmotion.Bored: return F(lid: .7f, brY: -.1f, smile: -.25f);
            case FaceEmotion.Sad: return F(lid: .38f, brA: -1.5f, brY: .3f, smile: -1f, pup: 1.1f);
            case FaceEmotion.Disappointed: return F(lid: .4f, lower: .1f, brA: -1f, brY: .1f, smile: -.9f);
            case FaceEmotion.Doubt: return F(lid: .25f, lower: .2f, brA: .5f, brY: .5f, smile: -.3f, asym: 1.4f);
            case FaceEmotion.Shock: return F(brA: -.2f, brY: 1.3f, widen: .5f, open: 1f, mw: .6f, pup: .55f, flush: -.35f);
            case FaceEmotion.Disgust: return F(lid: .5f, lower: .5f, brA: .9f, brY: -.3f, smile: -1.1f, asym: .5f, mw: .7f);
            case FaceEmotion.Smug: return F(lid: .5f, brA: .3f, brY: .3f, smile: .9f, asym: .8f);
            case FaceEmotion.Worried: return F(lid: .15f, brA: -1.2f, brY: .8f, smile: -.5f, widen: .2f, pup: .8f);
        }
        return F(lid: .12f, smile: .05f);
    }

    void MakeThumbs()
    {
        for (int i = 0; i < 2; i++)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "Thumb";
            WorldUtil.Kill(g.GetComponent<Collider>());
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = Mat(skinColor);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.SetActive(false);
            thumbs[i] = g.transform;
        }
    }

    void Thumbs()
    {
        float hs = H * handSize;
        for (int i = 0; i < 2; i++)
        {
            var t = thumbs[i];
            if (t == null) continue;
            var a = arms[i];
            bool on = thumbOn[i] && a.ok;
            if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
            if (!on) continue;
            Vector3 dir = (a.hand.position - a.low.position).normalized;
            t.position = a.hand.position + dir * 0.5f * hs + Vector3.up * 0.9f * hs + transform.TransformDirection(thumbNudge);
            t.rotation = transform.rotation;
            t.localScale = V(0.34f * hs, 0.7f * hs, 0.34f * hs);
        }
    }

    void OnDisable()
    {
        for (int i = 0; i < 2; i++) if (thumbs[i] != null) thumbs[i].gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        for (int i = 0; i < 2; i++) if (thumbs[i] != null) WorldUtil.Kill(thumbs[i].gameObject);
    }

    static bool ChairName(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("chair") || n.Contains("seat") || n.Contains("stool");
    }

    void ScanSeat()
    {
        if (hips == null || !legs[0].ok || !legs[1].ok) { scanLeft = 0; return; }
        Vector3 hw = hips.parent.TransformPoint(hipsLocal0);
        bool found = false;
        float best = float.NegativeInfinity;
        var hits = Physics.RaycastAll(hw + Vector3.up * 0.7f, Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i].collider;
            if (c.transform.IsChildOf(transform) || c.GetComponentInParent<CharacterController>() != null) continue;
            float y = hits[i].point.y;
            if (y > hw.y - 0.35f && y < hw.y + 0.3f && y > best) { best = y; found = true; }
        }
        if (found)
        {
            seatDelta = Mathf.Clamp(best + hipRise * H + seatLift - hw.y, -0.4f, 0.4f);
            seated = true; scanLeft = 0;
            return;
        }
        var near = Physics.OverlapSphere(hw, 0.9f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < near.Length; i++)
        {
            var c = near[i];
            if (c.transform.IsChildOf(transform)) continue;
            if (ChairName(c.name) || (c.transform.parent != null && ChairName(c.transform.parent.name)))
            {
                seatDelta = seatLift; seated = true; scanLeft = 0;
                return;
            }
        }
    }

    void Legs(float dt)
    {
        if (hips == null || !seatedPose) return;
        if (!seated && scanLeft > 0)
        {
            seatScan -= dt;
            if (seatScan <= 0f)
            {
                seatScan = 1.5f; scanLeft--;
                ScanSeat();
                if (!seated && forceSeated && scanLeft == 0) { seated = true; seatDelta = seatLift; }
            }
        }
        seatBlend = Mathf.MoveTowards(seatBlend, seated ? 1f : 0f, dt * 2.5f);
        if (seatBlend <= 0f) return;
        hips.localPosition = hipsLocal0 + hips.parent.InverseTransformVector(Vector3.up * (seatDelta * seatBlend));
        float sk = 1f - Mathf.Exp(-6f * dt);
        for (int i = 0; i < 2; i++)
        {
            var a = legs[i];
            if (!a.ok) continue;
            float tap = 0f, jig = 0f;
            if (active && outcome == 0)
            {
                if (mood == Mood.Impatient && i == 0) tap = Mathf.Max(0f, Mathf.Sin(clock * 11f)) * 0.35f;
                if (mood == Mood.Paranoid && i == 1) jig = Mathf.Sin(clock * 16f) * 0.08f * (0.4f + agit);
                if (mood == Mood.Bored && i == 0) jig = Mathf.Sin(clock * 1.7f) * 0.12f;
            }
            Vector3 wu = V(.12f, -.03f + jig * .5f, 1f).normalized;
            Vector3 wl = V(.04f, -1f, -.1f + tap + jig * 2f).normalized;
            a.curUp = Vector3.Slerp(a.curUp, wu, sk);
            a.curLow = Vector3.Slerp(a.curLow, wl, sk);
            a.up.rotation = transform.rotation * Quaternion.FromToRotation(a.restUp, Outward(a, a.curUp)) * a.rotUp;
            a.low.rotation = transform.rotation * Quaternion.FromToRotation(a.restLow, Outward(a, a.curLow)) * a.rotLow;
            a.hand.rotation = transform.rotation * rFoot[i];
        }
    }

    void Face(float dt, float yaw, float pitch, float w)
    {
        fv = Mix(fv, Scl(Vals(Resolve()), Persona.ExprScale), 1f - Mathf.Exp(-11f * dt));

        nextBlink -= dt;
        if (nextBlink <= 0f && blinkU >= 1f) { blinkU = 0f; nextBlink = Random.Range(2f, 5f) * (active && mood == Mood.Bored ? 0.6f : 1f); }
        float blinkLen = (active && mood == Mood.Bored) ? 0.5f : 0.18f;
        blinkU = Mathf.Min(1f, blinkU + dt / blinkLen);
        float blink = blinkU < 1f ? Mathf.Sin(blinkU * Mathf.PI) : 0f;
        float lidC = Mathf.Clamp01(Mathf.Max(Mathf.Max(fv.lid, blink), hf.shut));

        float gx = Mathf.Clamp((yaw - curYaw) / 30f, -1f, 1f) * w + (glanceT > 0f ? Mathf.Clamp(glanceYaw / 30f, -1f, 1f) * 0.5f : 0f);
        float gy = -Mathf.Clamp((pitch - curPitch) / 20f, -1f, 1f) * w;
        if (active && mood == Mood.Paranoid) gx += Nz(40f) * 0.6f;
        gx = Mathf.Lerp(gx, hf.gx, hf.gw);
        gy = Mathf.Lerp(gy, hf.gy, hf.gw);
        gx = Mathf.Clamp(gx, -1f, 1f);

        if (skinMat != null)
            skinMat.color = fv.flush >= 0f ? Color.Lerp(skinColor, new Color(0.8f, 0.18f, 0.14f), fv.flush) : Color.Lerp(skinColor, new Color(0.88f, 0.82f, 0.76f), -fv.flush);

        float ew = 0.2f * W * (1f + fv.widen * 0.35f), eh = 0.12f * H * (1f + fv.widen * 0.6f), ey = (eyeLine - 0.5f) * H;
        float pupil = Mathf.Min(0.085f * W * fv.pup, eh * 0.9f);
        float lh = eh * 1.1f;
        float jit = agit > 0.6f ? Nz(50f) * 0.004f * H : 0f;

        for (int i = 0; i < 2; i++)
        {
            var e = eyes[i];
            float x = (i == 0 ? -1f : 1f) * eyeSpread * W;
            float rule = x > 0f ? 1f : -1f;
            e.patch.localPosition = V(x, ey, 0.004f); e.patch.localScale = V(ew * 1.3f, eh * 1.5f, 0.004f);
            e.sclera.localPosition = V(x, ey, 0.006f); e.sclera.localScale = V(ew, eh, 0.004f);
            e.pupil.localPosition = V(x + gx * (ew * 0.5f - pupil * 0.5f), ey + gy * (eh * 0.5f - pupil * 0.5f) + jit, 0.0085f);
            e.pupil.localScale = V(pupil, pupil, 0.004f);

            float lidY = ey + eh * 0.5f - lidC * eh + lh * 0.5f;
            e.lid.localPosition = V(x, lidY, 0.011f); e.lid.localScale = V(ew * 1.25f, lh, 0.004f);
            e.lid.localRotation = Quaternion.Euler(0f, 0f, Mathf.Max(0f, fv.brA) * 14f * rule);

            float lowY = ey - eh * 0.5f + fv.lower * eh - lh * 0.5f;
            e.lower.localPosition = V(x, lowY, 0.011f); e.lower.localScale = V(ew * 1.25f, lh, 0.004f);

            float raise = (i == 1 ? fv.asym * 0.09f * H : 0f);
            float brY = ey + eh * 0.5f + 0.1f * H + fv.brY * 0.09f * H + raise;
            float brAng = i == 1 && fv.asym > 0.1f ? -fv.brA * 8f : fv.brA * 22f;
            e.brow.localPosition = V(x, brY, 0.012f); e.brow.localScale = V(ew * 1.25f, 0.055f * H, 0.004f);
            e.brow.localRotation = Quaternion.Euler(0f, 0f, brAng * rule);
        }

        float my = (mouthLine - 0.5f) * H;
        float open = Mathf.Max(fv.open, hf.open);
        if (speaking) open = Mathf.Max(open, Mathf.Clamp01(Mathf.PerlinNoise(clock * 14f, 0.5f) * 1.6f - 0.3f) * 0.8f);
        float th = 0.026f * H, mw = fv.mw;
        mouthPatch.localPosition = V(0f, my, 0.004f); mouthPatch.localScale = V(0.34f * W * Mathf.Max(1f, mw), 0.12f * H + open * 0.18f * H, 0.004f);
        mouthC.localPosition = V(0f, my - open * 0.06f * H, 0.008f); mouthC.localScale = V(0.12f * W * mw * (1f + open * 0.3f), th + open * 0.15f * H, 0.004f);
        float ang = fv.smile * 30f, hl = 0.06f * W * mw, rad = ang * Mathf.Deg2Rad;
        float sx = 0.06f * W * mw + hl * Mathf.Cos(rad), sy = hl * Mathf.Sin(rad) - open * 0.04f * H;
        mouthR.localPosition = V(sx, my + sy, 0.008f); mouthR.localScale = V(hl * 2f, th, 0.004f); mouthR.localRotation = Quaternion.Euler(0f, 0f, ang);
        mouthL.localPosition = V(-sx, my + sy, 0.008f); mouthL.localScale = V(hl * 2f, th, 0.004f); mouthL.localRotation = Quaternion.Euler(0f, 0f, -ang);
    }
}
