using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace FPAnimation
{
    /// <summary>
    /// View-model rendering. Default: URP overlay camera (arms never clip walls).
    /// Fallback (layer missing, non-URP, or useOverlayCamera=false): single camera + wall-retract ray.
    /// </summary>
    public class FPViewModelRenderer
    {
        public bool UsesOverlay { get; private set; }
        public int Layer { get; private set; }
        readonly FPAnimSettings s;
        Camera baseCam, overlay;
        float retract, retractVel;
        static RaycastHit hit;

        public FPViewModelRenderer(FPAnimSettings settings)
        {
            s = settings;
            int l = LayerMask.NameToLayer(s.viewModelLayerName);
            if (s.useOverlayCamera && l >= 0 && GraphicsSettings.currentRenderPipeline != null)
            { UsesOverlay = true; Layer = l; }
            else
            {
                Layer = 0;
                if (s.useOverlayCamera)
                    Debug.LogWarning("[FPAnimation] Layer '" + s.viewModelLayerName + "' not found (or not URP). Using single-camera fallback with wall-retract.");
            }
        }

        public void Attach(Camera cam, Transform parent)
        {
            baseCam = cam;
            if (!UsesOverlay) { cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, s.fallbackNearClip); return; }
            cam.cullingMask &= ~(1 << Layer);
            var go = new GameObject("FP ViewModel Camera");
            go.transform.SetParent(parent, false);
            overlay = go.AddComponent<Camera>();
            overlay.fieldOfView = s.overlayFov;
            overlay.nearClipPlane = s.overlayNearClip;
            overlay.farClipPlane = 10f;
            overlay.cullingMask = 1 << Layer;
            overlay.clearFlags = CameraClearFlags.Depth;
            overlay.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            var stack = cam.GetUniversalAdditionalCameraData().cameraStack;
            if (!stack.Contains(overlay)) stack.Add(overlay);
        }

        public void Detach()
        {
            if (!UsesOverlay || baseCam == null) return;
            baseCam.cullingMask |= 1 << Layer;
            if (overlay != null) baseCam.GetUniversalAdditionalCameraData().cameraStack.Remove(overlay);
        }

        public void SyncOverlay(Transform cam)
        {
            if (overlay != null) overlay.transform.SetPositionAndRotation(cam.position, cam.rotation);
        }

        /// <summary>Metres to pull the rig back along the camera forward (single-camera mode only).</summary>
        public float Retract(Transform cam, float dt)
        {
            if (UsesOverlay) return 0f;
            float target = 0f;
            if (Physics.Raycast(cam.position, cam.forward, out hit, s.wallRetractReach, ~0, QueryTriggerInteraction.Ignore))
                target = s.wallRetractReach - hit.distance;
            retract = FPSpring.Step(retract, ref retractVel, target, 0.06f, dt);
            return retract;
        }
    }
}
