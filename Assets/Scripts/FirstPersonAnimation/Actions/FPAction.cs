using UnityEngine;
using EasyPeasyFirstPersonController;
namespace FPAnimation
{
    public struct FPActionCtx
    {
        public FPLoco v;
        public float dt;
        public FPAnimSettings s;
        public FirstPersonController c;
        public Vector3 origin;
        public Quaternion frame;
        public FPItemInHand item;
        public Vector3 camBase;
        public float rawPitch;
        public Vector3 ToChest(Vector3 world) { return Quaternion.Inverse(frame) * (world - origin); }
    }

    public abstract class FPAction
    {
        public float Duration = 1f, T;
        public Vector3 CamMove;
        public float CamPitch;
        public float BodyBack;      // metres the hips/body root stay behind the camera when the torso leans, so head and torso lean together
        public float N { get { return Mathf.Clamp01(T / Mathf.Max(0.01f, Duration)); } }
        public virtual bool Weak { get { return false; } }
        public virtual bool ShouldInterrupt(in FPActionCtx x) { return false; }
        public abstract void Evaluate(in FPActionCtx x, float n, ref FPPose pose, out float dip);
        public virtual void End(bool interrupted, in FPActionCtx x) { }

        protected static float Sm(float a, float b, float t)
        { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t)); }

        protected static bool Disturbed(in FPActionCtx x)
        {
            var st = x.v.state;
            return x.v.sprinting || st == FPState.Jumping || st == FPState.Sliding ||
                   st == FPState.Swimming || st == FPState.LedgeGrab || st == FPState.Fall ||
                   (x.c.input != null && x.c.input.jump);
        }
    }
}
