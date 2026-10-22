using UnityEngine;
namespace FPAnimation
{
    public static class FPTwoBoneIK
    {
        /// <summary>Analytic two-bone solve. Elbow/knee bends toward the pole point.</summary>
        public static void Solve(Vector3 root, Vector3 target, Vector3 pole, float l1, float l2, out Vector3 mid, out Vector3 end)
        {
            Vector3 d = target - root;
            float dist = d.magnitude;
            Vector3 dir = dist > 1e-5f ? d / dist : Vector3.forward;
            dist = Mathf.Clamp(dist, Mathf.Abs(l1 - l2) + 0.001f, (l1 + l2) * 0.999f);
            end = root + dir * dist;
            float a = Mathf.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2f * l1 * dist), -1f, 1f);
            float s = Mathf.Sqrt(1f - a * a);
            Vector3 pd = pole - root;
            Vector3 perp = pd - dir * Vector3.Dot(pd, dir);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(dir, Vector3.right);
            perp.Normalize();
            mid = root + dir * (l1 * a) + perp * (l1 * s);
        }
    }

    /// <summary>Three joint transforms (root, mid, end) driven by the solver.</summary>
    public class FPLimb
    {
        public Transform root, mid, end;
        public float l1, l2;
        public void Solve(Vector3 rootPos, Vector3 target, Vector3 pole)
        {
            FPTwoBoneIK.Solve(rootPos, target, pole, l1, l2, out Vector3 m, out Vector3 e);
            Vector3 up = Mathf.Abs(Vector3.Dot((m - rootPos).normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            root.SetPositionAndRotation(rootPos, Quaternion.LookRotation(m - rootPos, up));
            mid.SetPositionAndRotation(m, Quaternion.LookRotation(e - m, up));
            end.position = e;
        }
    }
}
