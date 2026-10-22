using UnityEngine;
using EasyPeasyFirstPersonController;
namespace FPAnimation
{
    public class FPCameraShakeReaction
    {
        public float F;
        float fV, prev;
        Vector2 dir;

        public void Apply(float dt, FPAnimSettings s, FirstPersonController c, ref FPPose p)
        {
            float t = c.cameraShakeTimer;
            if (t > 0f && prev <= 0f)
            {
                F = Mathf.Clamp01(0.35f + c.cameraShakeIntensity);
                fV = 0f;
                Vector3 d = c.transform.InverseTransformDirection((Vector3)c.cameraShakeDirection);
                dir = new Vector2(d.x, d.z);
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.zero;
            }
            prev = t;
            F = FPSpring.Step(F, ref fV, 0f, 0.22f, dt);
            if (F < 0.002f) return;

            float k = F;
            p.R.flex += 35f * k; p.L.flex += 35f * k;
            p.R.elbow += 40f * k; p.L.elbow += 40f * k;
            p.R.abd += 10f * k; p.L.abd += 10f * k;
            p.R.curl = Mathf.Lerp(p.R.curl, 0.9f, k); p.L.curl = Mathf.Lerp(p.L.curl, 0.9f, k);
            p.torsoLean -= 6f * k;
            Vector3 j = new Vector3(-dir.x, 0f, -dir.y) * (0.10f * k);
            p.R.off += j; p.L.off += j;
        }
    }
}
