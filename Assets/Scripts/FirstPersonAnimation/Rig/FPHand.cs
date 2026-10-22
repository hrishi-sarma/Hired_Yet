using UnityEngine;
namespace FPAnimation
{
    /// <summary>Cute mitten ("uni hand"): one rounded blob + a little thumb. No fingers.</summary>
    public class FPHand
    {
        readonly Transform body, thumb;
        readonly float thumbSide;
        static readonly Vector3 bodyScale = new Vector3(0.105f, 0.08f, 0.13f);

        public FPHand(FPRigBuilder b, Transform wrist, bool left, bool web, Material skin)
        {
            thumbSide = left ? 1f : -1f;
            body = b.Prim(PrimitiveType.Sphere, "mitten", wrist, skin, false, new Vector3(0, 0, 0.07f), Quaternion.identity, bodyScale);
            thumb = new GameObject("thumb").transform;
            thumb.SetParent(wrist, false);
            thumb.localPosition = new Vector3(thumbSide * 0.04f, 0.004f, 0.035f);
            thumb.localRotation = Quaternion.Euler(0, thumbSide * 30f, 0);
            b.Prim(PrimitiveType.Capsule, "thumbBlob", thumb, skin, false, new Vector3(0, 0, 0.03f), Quaternion.Euler(90, 0, 0), new Vector3(0.042f, 0.032f, 0.042f));
        }

        /// <summary>curl 0..1 squeezes the mitten slightly and tucks the thumb in.</summary>
        public void SetCurl(float curl, float thumbCurl)
        {
            curl = Mathf.Clamp01(curl); thumbCurl = Mathf.Clamp01(thumbCurl);
            body.localScale = new Vector3(bodyScale.x, bodyScale.y, bodyScale.z * (1f - 0.18f * curl));
            body.localPosition = new Vector3(0, 0, 0.07f - 0.01f * curl);
            thumb.localRotation = Quaternion.Euler(0, thumbSide * (30f - 25f * thumbCurl), 0);
        }
    }
}
