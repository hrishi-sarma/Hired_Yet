using UnityEngine;
namespace FPAnimation
{
    /// <summary>Critically damped springs. Allocation free.</summary>
    public static class FPSpring
    {
        public static float Step(float cur, ref float vel, float target, float smoothTime, float dt)
        {
            float w = 2f / Mathf.Max(0.0001f, smoothTime), x = w * dt;
            float e = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float c = cur - target, t = (vel + w * c) * dt;
            vel = (vel - w * t) * e;
            return target + (c + t) * e;
        }
        public static Vector3 Step(Vector3 cur, ref Vector3 vel, Vector3 target, float smoothTime, float dt)
        {
            float w = 2f / Mathf.Max(0.0001f, smoothTime), x = w * dt;
            float e = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            Vector3 c = cur - target, t = (vel + w * c) * dt;
            vel = (vel - w * t) * e;
            return target + (c + t) * e;
        }
    }
}
