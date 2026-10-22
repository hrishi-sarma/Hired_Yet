using UnityEngine;
namespace FPAnimation
{
    [DefaultExecutionOrder(-100)]
    public class FPCameraOffset : MonoBehaviour
    {
        Transform target;
        Vector3 applied;
        public Vector3 Applied { get { return applied; } }

        public void Init(Transform cameraParent) { target = cameraParent; }

        void Update() { Revert(); }

        public void Revert()
        {
            if (target != null && applied != Vector3.zero) target.localPosition -= applied;
            applied = Vector3.zero;
        }

        public void Apply(Vector3 offset)
        {
            Revert();
            if (target == null || offset == Vector3.zero) return;
            target.localPosition += offset;
            applied = offset;
        }

        void OnDestroy() { Revert(); }
    }
}
