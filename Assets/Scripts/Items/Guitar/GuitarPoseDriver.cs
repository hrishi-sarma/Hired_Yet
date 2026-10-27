using FPAnimation;
using UnityEngine;

namespace Items
{
    // Lives on the held guitar. The guitar is never shown in the single-hand carry pose: it is hidden until it rises
    // into the two-handed ready pose (mount published each frame by FPGuitarLayer), and lowers out of view when disturbed.
    // GuitarPlayer sets Target (1 = ready/playing pose, 0 = lowered).
    // Must run AFTER FPAnimationSystem (order 32000): it publishes the guitar mount in its own LateUpdate, so running earlier used last frame's mount and made the guitar jitter against the camera.
    [DefaultExecutionOrder(32001)]
    public class GuitarPoseDriver : MonoBehaviour
    {
        public float blendTime = 0.35f;
        public float playScale = 1f;
        public float Target;                       // 0 = lowered, 1 = ready pose
        public float Blend { get; private set; }   // smoothed 0..1
        public bool Ready { get { return age >= 0.05f; } }

        float age, vel;
        Renderer[] rends;
        Transform[] strings;
        Vector3[] strBase;
        bool shown = true, layered;

        public void Init(HoldPose p) { }   // kept for GuitarModel; the carry pose is no longer used

        void Awake()
        {
            rends = GetComponentsInChildren<Renderer>(true);
            SetShown(false);
        }

        void SetShown(bool on)
        {
            if (on == shown) return;
            shown = on;
            foreach (var r in rends) if (r != null) r.enabled = on;
        }

        // The guitar must live on the view-model layer so it is drawn by the same overlay camera (and depth buffer) as the hands.
        // Otherwise the world camera draws it and the hands always paint over it in first person.
        void PutOnViewModelLayer()
        {
            layered = true;
            var fp = FindAnyObjectByType<FPAnimationSystem>();
            if (fp == null || fp.ViewModel == null || !fp.ViewModel.UsesOverlay) return;
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = fp.ViewModel.Layer;
        }

        void Update()
        {
            if (!layered) PutOnViewModelLayer();
            age += Time.unscaledDeltaTime;
            if (!Ready) return;
            Blend = Mathf.SmoothDamp(Blend, Target, ref vel, blendTime, Mathf.Infinity, Time.unscaledDeltaTime);
            if (Target <= 0f && Blend < 0.002f) { Blend = 0f; vel = 0f; }
            FPEvents.GuitarBlend = Blend;
        }

        void LateUpdate()
        {
            if (Blend < 0.01f) { SetShown(false); return; }
            SetShown(true);
            transform.SetPositionAndRotation(FPEvents.GuitarMountPos, FPEvents.GuitarMountRot);
            transform.localScale = Vector3.one * playScale;
            Vibrate();
        }

        // strings shimmer on every strum
        void Vibrate()
        {
            if (strings == null)
            {
                strings = new Transform[6]; strBase = new Vector3[6];
                for (int k = 0; k < 6; k++) { strings[k] = transform.Find("String" + (k + 1)); if (strings[k] != null) strBase[k] = strings[k].localScale; }
            }
            float v = FPEvents.GuitarVibe;
            for (int k = 0; k < 6; k++)
            {
                if (strings[k] == null) continue;
                float w = 1f + v * 1.6f * Mathf.Sin(Time.time * (70f + k * 9f));
                strings[k].localScale = new Vector3(strBase[k].x * w, strBase[k].y * w, strBase[k].z);
            }
        }

        void OnDestroy() { FPEvents.GuitarBlend = 0f; FPEvents.GuitarVibe = 0f; }
    }
}
