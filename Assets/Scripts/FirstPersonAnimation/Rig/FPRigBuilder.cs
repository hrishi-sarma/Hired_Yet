using UnityEngine;
using UnityEngine.Rendering;
namespace FPAnimation
{
    public class FPRig
    {
        public Transform viewRoot, bodyRoot, torso, hips;
        public FPLimb armL, armR, legL, legR;
        public FPHand handL, handR;
        public int renderers;
    }

    /// <summary>Builds the whole primitive rig once. 4 shared materials, all auto-colliders destroyed.</summary>
    public class FPRigBuilder
    {
        readonly FPAnimSettings s;
        readonly int layer;
        readonly bool web;
        int renderers;
        Material skin, sleeve, trousers, shoes, baseMat;

        public FPRigBuilder(FPAnimSettings settings, int viewModelLayer, bool webHands)
        { s = settings; layer = viewModelLayer; web = webHands; }

        Material Mat(Color c)
        {
            var m = new Material(baseMat) { color = c };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            return m;
        }

        public Transform Prim(PrimitiveType t, string name, Transform parent, Material m, bool shadow, Vector3 lp, Quaternion lr, Vector3 ls)
        {
            var go = GameObject.CreatePrimitive(t);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name; go.layer = layer;
            var tr = go.transform;
            tr.SetParent(parent, false);
            tr.localPosition = lp; tr.localRotation = lr; tr.localScale = ls;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            renderers++;
            if (shadow && s.castBodyShadow)
            {
                var sh = new GameObject("shadow");
                sh.layer = 0;
                sh.transform.SetParent(tr, false);
                sh.AddComponent<MeshFilter>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                var sr = sh.AddComponent<MeshRenderer>();
                sr.sharedMaterial = m; sr.shadowCastingMode = ShadowCastingMode.ShadowsOnly; sr.receiveShadows = false;
                renderers++;
            }
            return tr;
        }

        // capsule lying along local +Z, starting at 'start'
        void Seg(Transform parent, float len, float radius, Material m, float start = 0f)
        {
            Prim(PrimitiveType.Capsule, "seg", parent, m, true, new Vector3(0, 0, start + len * 0.5f),
                 Quaternion.Euler(90, 0, 0), new Vector3(radius * 2f, len * 0.5f, radius * 2f));
        }

        Transform Joint(Transform parent, string n)
        { var t = new GameObject(n).transform; t.SetParent(parent, false); return t; }

        FPLimb Limb(Transform parent, string n, float l1, float l2, float r1, float r2, Material m1, Material m2)
        {
            var l = new FPLimb { l1 = l1, l2 = l2, root = Joint(parent, n + "_root"), mid = Joint(parent, n + "_mid"), end = Joint(parent, n + "_end") };
            Seg(l.root, l1, r1, m1);
            Seg(l.mid, l2, r2, m2);
            return l;
        }

        Transform Tapered(string name, Transform parent, Material m, float wTop, float wBot, float dTop, float dBot, float h)
        {
            float hh = h * 0.5f;
            Vector3[] c =
            {
                new Vector3(-wBot * 0.5f, -hh, -dBot * 0.5f), new Vector3(wBot * 0.5f, -hh, -dBot * 0.5f),
                new Vector3(wBot * 0.5f, -hh, dBot * 0.5f), new Vector3(-wBot * 0.5f, -hh, dBot * 0.5f),
                new Vector3(-wTop * 0.5f, hh, -dTop * 0.5f), new Vector3(wTop * 0.5f, hh, -dTop * 0.5f),
                new Vector3(wTop * 0.5f, hh, dTop * 0.5f), new Vector3(-wTop * 0.5f, hh, dTop * 0.5f)
            };
            int[][] faces =
            {
                new[] { 3, 2, 6, 7 }, new[] { 1, 0, 4, 5 }, new[] { 0, 3, 7, 4 },
                new[] { 2, 1, 5, 6 }, new[] { 7, 6, 5, 4 }, new[] { 0, 1, 2, 3 }
            };
            var verts = new Vector3[24];
            var tris = new int[36];
            for (int f = 0; f < 6; f++)
            {
                int vi = f * 4;
                for (int k = 0; k < 4; k++) verts[vi + k] = c[faces[f][k]];
                Vector3 n = Vector3.Cross(verts[vi + 1] - verts[vi], verts[vi + 2] - verts[vi]);
                Vector3 mid = (verts[vi] + verts[vi + 1] + verts[vi + 2] + verts[vi + 3]) * 0.25f;
                bool flip = Vector3.Dot(n, mid) < 0f;
                int ti = f * 6;
                tris[ti] = vi; tris[ti + 1] = flip ? vi + 2 : vi + 1; tris[ti + 2] = flip ? vi + 1 : vi + 2;
                tris[ti + 3] = vi; tris[ti + 4] = flip ? vi + 3 : vi + 2; tris[ti + 5] = flip ? vi + 2 : vi + 3;
            }
            var mesh = new Mesh { name = name };
            mesh.vertices = verts; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();

            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            renderers++;
            if (s.castBodyShadow)
            {
                var sh = new GameObject("shadow") { layer = 0 };
                sh.transform.SetParent(go.transform, false);
                sh.AddComponent<MeshFilter>().sharedMesh = mesh;
                var sr = sh.AddComponent<MeshRenderer>();
                sr.sharedMaterial = m; sr.shadowCastingMode = ShadowCastingMode.ShadowsOnly; sr.receiveShadows = false;
                renderers++;
            }
            return go.transform;
        }

        public FPRig Build(Transform parent, float sc)
        {
            baseMat = WorldUtil.DefaultLit();   // NOT the primitive's default material: that is the (invisible) built-in Standard shader in a build
            bool chosen = ResumeSession.CharacterIndex >= 0;   // photo-ID character from the resume scene
            skin = Mat(chosen ? ResumeSession.PlayerSkin : s.skin); sleeve = Mat(chosen ? ResumeSession.PlayerOutfit : s.sleeve); trousers = Mat(s.trousers); shoes = Mat(s.shoes);

            var rig = new FPRig();
            rig.viewRoot = Joint(parent, "ViewRoot");
            rig.bodyRoot = Joint(parent, "BodyRoot");

            // arms (view space). Upper arm = sleeve, forearm = bare skin with a sleeve cuff over its first 65%
            rig.armR = Limb(rig.viewRoot, "armR", s.upperArm, s.foreArm, 0.042f, 0.032f, sleeve, skin);
            rig.armL = Limb(rig.viewRoot, "armL", s.upperArm, s.foreArm, 0.042f, 0.032f, sleeve, skin);
            Seg(rig.armR.mid, s.foreArm * 0.65f, 0.040f, sleeve);
            Seg(rig.armL.mid, s.foreArm * 0.65f, 0.040f, sleeve);
            rig.handR = new FPHand(this, rig.armR.end, false, web, skin);
            rig.handL = new FPHand(this, rig.armL.end, true, web, skin);

            // body (yaw only)
            var b = rig.bodyRoot;
            rig.torso = Tapered("torso", b, sleeve, 0.40f * sc, 0.26f * sc, 0.18f * sc, 0.145f * sc, 0.46f * sc);
            rig.hips = Tapered("hips", b, trousers, 0.27f * sc, 0.31f * sc, 0.17f * sc, 0.19f * sc, 0.20f * sc);
            rig.legR = Limb(b, "legR", 0.46f * sc, 0.44f * sc, 0.075f * sc, 0.06f * sc, trousers, trousers);
            rig.legL = Limb(b, "legL", 0.46f * sc, 0.44f * sc, 0.075f * sc, 0.06f * sc, trousers, trousers);
            Prim(PrimitiveType.Cube, "footR", rig.legR.end, shoes, true, new Vector3(0, -0.035f, 0.07f) * sc, Quaternion.identity, new Vector3(0.10f, 0.07f, 0.26f) * sc);
            Prim(PrimitiveType.Cube, "footL", rig.legL.end, shoes, true, new Vector3(0, -0.035f, 0.07f) * sc, Quaternion.identity, new Vector3(0.10f, 0.07f, 0.26f) * sc);

            rig.renderers = renderers;
            return rig;
        }
    }
}
