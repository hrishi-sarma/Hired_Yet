using System;
using UnityEngine;
namespace FPAnimation
{
    public static class FPEvents
    {
        public static event Action<Transform, bool, float> PatStarted;
        public static event Action<Transform, bool, Action> PickpocketAttempt;
        public static event Action<Vector3> Interact;
        public static event Action<Vector3, Action> FeedStarted;
        /// <summary>World event stage change. PlayerEventAnim.None in During stops the open-ended action.</summary>
        public static event Action<EventStage, PlayerEventAnim, Transform, float> EventStageChanged;

        /// <summary>Fed each frame by the active world event: 0..1 effort, and a counter bumped per tap.</summary>
        public static float EventIntensity;
        public static float EventFocusHeight;       // top of the low object being worked on (0 = no crouch)
        public static float EventLift;             // 0..1 how far the player has stood up with a lifted end (couch)
        public static float EventCarry = 1f;       // Lift pose: 0 = crouched at the items (picking up), 1 = standing with them
        public static float EventSqueeze;          // 0..1 how hard both hands are pressing together (lemon squash)
        public static float EventProgress;          // task completion 0..1
        public static int EventKicks, EventArrow, EventArrowCount;   // arrow 0 up, 1 down, 2 left, 3 right
        public static bool EventArrowOk = true;
        public static Transform HandL, HandR;       // wrist joints of the first-person arms, so event props can follow the hands
        /// <summary>True while the inventory system has an item equipped in the right hand.</summary>
        public static bool HoldingItem;
        /// <summary>Guitar playing pose: 0..1 blend (set by GuitarPoseDriver) and the view-space mount (published by FPGuitarLayer).</summary>
        public static float GuitarBlend;
        public static Vector3 GuitarMountPos;
        public static Quaternion GuitarMountRot = Quaternion.identity;
        /// <summary>Guitar hand animation inputs: held flag, playing intensity 0..1 and live loudness (GuitarPlayer), string vibration (FPGuitarLayer).</summary>
        public static bool GuitarHeld;
        public static float GuitarPlay, GuitarLevel, GuitarVibe;
        public static void KickEvent() { EventKicks++; }

        public static int Raised;

        /// <summary>Sitting state, published by BenchSittingInteraction. Sitting is true from the moment the player starts sitting until the stand-up has finished.</summary>
        public static bool Sitting;
        /// <summary>0 = not sitting, 1 = on a seat (bench / log), 2 = on the ground (cross-legged).</summary>
        public static int SitKind;
        /// <summary>0..1 pose blend (eased in and out by the sitting system).</summary>
        public static float SitBlend;
        /// <summary>Hip height above the ground for SitKind 1 (metres).</summary>
        public static float SitSeatHeight = 0.45f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSit() { Sitting = false; SitKind = 0; SitBlend = 0f; SitSeatHeight = 0.45f; }

        public static bool HasListeners { get { return PickpocketAttempt != null; } }

        public static void RaisePatStarted(Transform animal, bool isDog, float seconds)
        { Raised++; if (PatStarted != null) PatStarted(animal, isDog, seconds); }

        public static void RaisePickpocketAttempt(Transform target, bool success, Action apply)
        { Raised++; if (PickpocketAttempt != null) PickpocketAttempt(target, success, apply); else if (apply != null) apply(); }

        public static void RaiseEventStage(EventStage stage, PlayerEventAnim anim, Transform target, float seconds)
        { Raised++; if (EventStageChanged != null) EventStageChanged(stage, anim, target, seconds); }

        public static void RaiseFeedStarted(Vector3 target, Action release)
        { Raised++; if (FeedStarted != null) FeedStarted(target, release); else if (release != null) release(); }

        public static void RaiseInteract(Vector3 position)
        { Raised++; if (Interact != null) Interact(position); }
    }
}
