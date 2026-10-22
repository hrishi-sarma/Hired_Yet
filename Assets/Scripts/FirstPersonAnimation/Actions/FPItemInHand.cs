using UnityEngine;
namespace FPAnimation
{
    public class FPItemInHand
    {
        readonly GameObject go;

        public FPItemInHand(Transform wrist, int layer, Color color)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "item";
            go.layer = layer;
            go.transform.SetParent(wrist, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0.12f);
            go.transform.localScale = new Vector3(0.08f, 0.015f, 0.11f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = new Material(WorldUtil.DefaultLit()) { color = color };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
        }

        public void Show(bool on) { if (go != null && go.activeSelf != on) go.SetActive(on); }
    }
}
